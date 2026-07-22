using System;
using System.Collections.Generic;
using System.Text;

// 순수 C# QR 코드 인코더 (외부 라이브러리 의존 없음)
// - Byte 모드 / ECC Level L / 버전 1~5 자동 (payload 최대 106바이트) / 마스크 0~7 페널티 평가 후 선택
// - server_id 공유용 소용량 QR 전용 (ServerQrDisplay에서 사용)
public static class QrCodeEncoder
{
    // 버전별 데이터/ECC 코드워드 수 (ECC Level L, 단일 블록)
    private static readonly int[] DataCodewords = { 0, 19, 34, 55, 80, 108 };
    private static readonly int[] EccCodewords = { 0, 7, 10, 15, 20, 26 };

    // 문자열을 QR 매트릭스로 인코딩. true = 어두운 모듈. 실패(용량 초과) 시 null.
    public static bool[,] Encode(string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text ?? "");

        // 버전 선택 (byte 모드: 4bit 모드 + 8bit 길이 + 데이터)
        int version = 0;
        for (int v = 1; v <= 5; v++)
        {
            int capacityBits = DataCodewords[v] * 8;
            if (4 + 8 + data.Length * 8 <= capacityBits) { version = v; break; }
        }
        if (version == 0) return null;

        int size = 17 + 4 * version;

        // ---- 비트스트림 구성 ----
        List<bool> bits = new List<bool>();
        AppendBits(bits, 0x4, 4);              // 모드: byte (0100)
        AppendBits(bits, data.Length, 8);      // 길이 (버전 1~9: 8비트)
        foreach (byte b in data) AppendBits(bits, b, 8);

        int capacity = DataCodewords[version] * 8;
        int terminator = Math.Min(4, capacity - bits.Count);
        AppendBits(bits, 0, terminator);
        while (bits.Count % 8 != 0) bits.Add(false);

        // 패딩 바이트 (0xEC, 0x11 교대)
        bool padToggle = true;
        while (bits.Count < capacity)
        {
            AppendBits(bits, padToggle ? 0xEC : 0x11, 8);
            padToggle = !padToggle;
        }

        // 코드워드 변환 + Reed-Solomon ECC (단일 블록)
        byte[] codewords = new byte[DataCodewords[version]];
        for (int i = 0; i < codewords.Length; i++)
        {
            int value = 0;
            for (int j = 0; j < 8; j++) value = (value << 1) | (bits[i * 8 + j] ? 1 : 0);
            codewords[i] = (byte)value;
        }
        byte[] ecc = ComputeReedSolomon(codewords, EccCodewords[version]);

        List<bool> stream = new List<bool>();
        foreach (byte b in codewords) AppendBits(stream, b, 8);
        foreach (byte b in ecc) AppendBits(stream, b, 8);

        // ---- 매트릭스 구성 ----
        bool[,] modules = new bool[size, size];    // [row, col]
        bool[,] isFunction = new bool[size, size];

        DrawFinderPatterns(modules, isFunction, size);
        DrawTimingPatterns(modules, isFunction, size);
        DrawAlignmentPattern(modules, isFunction, size, version);

        // 다크 모듈
        modules[size - 8, 8] = true;
        isFunction[size - 8, 8] = true;

        ReserveFormatAreas(isFunction, size);

        // 데이터 배치 (우하단부터 지그재그, 6열 스킵) — 마스크 미적용 상태
        int bitIndex = 0;
        bool upward = true;
        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;  // 타이밍 열 스킵
            for (int vert = 0; vert < size; vert++)
            {
                int row = upward ? (size - 1 - vert) : vert;
                for (int c = 0; c < 2; c++)
                {
                    int col = right - c;
                    if (isFunction[row, col]) continue;
                    modules[row, col] = bitIndex < stream.Count && stream[bitIndex];
                    bitIndex++;
                }
            }
            upward = !upward;
        }

        // 마스크 0~7 전부 적용해보고 페널티 최저 선택 (QR 스펙 표준 절차)
        bool[,] best = null;
        int bestPenalty = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            bool[,] candidate = (bool[,])modules.Clone();
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (!isFunction[row, col] && MaskBit(mask, row, col)) candidate[row, col] = !candidate[row, col];
                }
            }
            DrawFormatInfo(candidate, size, mask);
            int penalty = ComputePenalty(candidate, size);
            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                best = candidate;
            }
        }
        return best;
    }

    // 마스크 패턴 조건 (true면 해당 모듈 반전)
    private static bool MaskBit(int mask, int r, int c)
    {
        switch (mask)
        {
            case 0: return (r + c) % 2 == 0;
            case 1: return r % 2 == 0;
            case 2: return c % 3 == 0;
            case 3: return (r + c) % 3 == 0;
            case 4: return (r / 2 + c / 3) % 2 == 0;
            case 5: return (r * c) % 2 + (r * c) % 3 == 0;
            case 6: return ((r * c) % 2 + (r * c) % 3) % 2 == 0;
            case 7: return ((r + c) % 2 + (r * c) % 3) % 2 == 0;
            default: return false;
        }
    }

    // 마스크 선택용 페널티 점수 (N1 연속 런 / N2 2x2 블록 / N3 파인더 유사 패턴 / N4 흑백 비율)
    private static int ComputePenalty(bool[,] m, int size)
    {
        int penalty = 0;

        // N1: 행/열의 같은 색 5연속 이상 (3 + 초과분)
        for (int axis = 0; axis < 2; axis++)
        {
            for (int i = 0; i < size; i++)
            {
                int run = 1;
                for (int j = 1; j < size; j++)
                {
                    bool cur = axis == 0 ? m[i, j] : m[j, i];
                    bool prev = axis == 0 ? m[i, j - 1] : m[j - 1, i];
                    if (cur == prev) { run++; if (run == 5) penalty += 3; else if (run > 5) penalty += 1; }
                    else run = 1;
                }
            }
        }

        // N2: 같은 색 2x2 블록 (블록당 3)
        for (int r = 0; r < size - 1; r++)
            for (int c = 0; c < size - 1; c++)
                if (m[r, c] == m[r, c + 1] && m[r, c] == m[r + 1, c] && m[r, c] == m[r + 1, c + 1]) penalty += 3;

        // N3: 1011101 앞뒤 0000 패턴 (개당 40)
        int[] patternA = { 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0 };
        int[] patternB = { 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1 };
        for (int axis = 0; axis < 2; axis++)
        {
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j <= size - 11; j++)
                {
                    bool matchA = true, matchB = true;
                    for (int k = 0; k < 11; k++)
                    {
                        bool cell = axis == 0 ? m[i, j + k] : m[j + k, i];
                        if (cell != (patternA[k] == 1)) matchA = false;
                        if (cell != (patternB[k] == 1)) matchB = false;
                        if (!matchA && !matchB) break;
                    }
                    if (matchA) penalty += 40;
                    if (matchB) penalty += 40;
                }
            }
        }

        // N4: 어두운 모듈 비율의 50% 편차 (5%p 단위당 10)
        int dark = 0;
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                if (m[r, c]) dark++;
        int percent = dark * 100 / (size * size);
        penalty += Math.Abs(percent - 50) / 5 * 10;

        return penalty;
    }

    // value의 하위 count비트를 MSB부터 추가
    private static void AppendBits(List<bool> bits, int value, int count)
    {
        for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) != 0);
    }

    // GF(256) Reed-Solomon ECC 계산 (기약다항식 0x11D)
    private static byte[] ComputeReedSolomon(byte[] data, int eccCount)
    {
        // 생성 다항식 (x - a^0)(x - a^1)...(x - a^(eccCount-1)) — 계수는 내림차순, generator[0]=1(최고차)
        byte[] generator = new byte[] { 1 };
        for (int i = 0; i < eccCount; i++)
        {
            byte[] next = new byte[generator.Length + 1];
            byte alpha = GfPower(2, i);
            for (int j = 0; j < generator.Length; j++)
            {
                next[j] ^= generator[j];                        // x 곱 (내림차순에서 같은 인덱스 유지)
                next[j + 1] ^= GfMultiply(generator[j], alpha); // a^i 곱 (한 칸 낮은 차수로)
            }
            generator = next;
        }

        // 다항식 나눗셈의 나머지 (synthetic division)
        byte[] remainder = new byte[eccCount];
        foreach (byte b in data)
        {
            byte factor = (byte)(b ^ remainder[0]);
            Array.Copy(remainder, 1, remainder, 0, eccCount - 1);
            remainder[eccCount - 1] = 0;
            for (int i = 0; i < eccCount; i++)
            {
                remainder[i] ^= GfMultiply(generator[i + 1], factor);
            }
        }
        return remainder;
    }

    private static byte GfMultiply(byte a, byte b)
    {
        int result = 0;
        int aa = a;
        for (int i = 0; i < 8; i++)
        {
            if (((b >> i) & 1) != 0) result ^= aa << i;
        }
        // 0x11D로 축약
        for (int i = 15; i >= 8; i--)
        {
            if (((result >> i) & 1) != 0) result ^= 0x11D << (i - 8);
        }
        return (byte)result;
    }

    private static byte GfPower(byte baseValue, int exponent)
    {
        byte result = 1;
        for (int i = 0; i < exponent; i++) result = GfMultiply(result, baseValue);
        return result;
    }

    // 파인더 패턴 3개 + 분리자
    private static void DrawFinderPatterns(bool[,] modules, bool[,] isFunction, int size)
    {
        int[][] corners = { new[] { 0, 0 }, new[] { 0, size - 7 }, new[] { size - 7, 0 } };
        foreach (int[] corner in corners)
        {
            int top = corner[0], left = corner[1];
            for (int r = -1; r <= 7; r++)
            {
                for (int c = -1; c <= 7; c++)
                {
                    int row = top + r, col = left + c;
                    if (row < 0 || row >= size || col < 0 || col >= size) continue;
                    bool dark = r >= 0 && r <= 6 && c >= 0 && c <= 6 &&
                                (r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4));
                    modules[row, col] = dark;
                    isFunction[row, col] = true;
                }
            }
        }
    }

    // 타이밍 패턴 (6행/6열)
    private static void DrawTimingPatterns(bool[,] modules, bool[,] isFunction, int size)
    {
        for (int i = 8; i < size - 8; i++)
        {
            if (!isFunction[6, i]) { modules[6, i] = i % 2 == 0; isFunction[6, i] = true; }
            if (!isFunction[i, 6]) { modules[i, 6] = i % 2 == 0; isFunction[i, 6] = true; }
        }
    }

    // 얼라인먼트 패턴 (버전 2 이상, 중앙 1개)
    private static void DrawAlignmentPattern(bool[,] modules, bool[,] isFunction, int size, int version)
    {
        if (version < 2) return;
        int center = 4 * version + 10;  // v2:18, v3:22, v4:26, v5:30
        for (int r = -2; r <= 2; r++)
        {
            for (int c = -2; c <= 2; c++)
            {
                bool dark = Math.Max(Math.Abs(r), Math.Abs(c)) != 1;
                modules[center + r, center + c] = dark;
                isFunction[center + r, center + c] = true;
            }
        }
    }

    // 포맷 정보 영역 예약 (데이터 배치에서 제외)
    private static void ReserveFormatAreas(bool[,] isFunction, int size)
    {
        for (int i = 0; i <= 8; i++)
        {
            isFunction[8, i] = true;
            isFunction[i, 8] = true;
        }
        for (int i = 0; i < 8; i++)
        {
            isFunction[8, size - 1 - i] = true;
            isFunction[size - 1 - i, 8] = true;
        }
    }

    // 포맷 정보 기록 (ECC L + 마스크 번호, BCH(15,5) 부호화)
    private static void DrawFormatInfo(bool[,] modules, int size, int mask)
    {
        int formatData = (0x1 << 3) | mask;  // ECC L(01) + 마스크(3비트)

        // BCH 나머지 계산 (생성 다항식 10100110111 = 0x537)
        int rem = formatData;
        for (int i = 0; i < 10; i++) rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
        int format = ((formatData << 10) | rem) ^ 0x5412;  // 마스킹 상수 101010000010010

        // 비트 14(MSB)..0 순서로 배치
        for (int i = 0; i <= 5; i++) SetFormatBit(modules, 8, i, format, 14 - i);         // 좌상 가로 (b14..b9)
        SetFormatBit(modules, 8, 7, format, 8);
        SetFormatBit(modules, 8, 8, format, 7);
        SetFormatBit(modules, 7, 8, format, 6);
        for (int i = 0; i <= 5; i++) SetFormatBit(modules, 5 - i, 8, format, 5 - i);      // 좌상 세로 (b5..b0)

        for (int i = 0; i <= 6; i++) SetFormatBit(modules, size - 1 - i, 8, format, 14 - i);  // 좌하 세로 (b14..b8)
        for (int i = 0; i <= 7; i++) SetFormatBit(modules, 8, size - 8 + i, format, 7 - i);   // 우상 가로 (b7..b0)
    }

    private static void SetFormatBit(bool[,] modules, int row, int col, int format, int bit)
    {
        modules[row, col] = ((format >> bit) & 1) != 0;
    }
}
