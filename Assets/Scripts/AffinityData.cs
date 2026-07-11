using UnityEngine;

// 카드 테두리 등급 — 인연도 레벨 구간에 대응 (Lv4~6 동 / Lv7~9 은 / Lv10 금).
public enum AffinityBorderTier { None = 0, Bronze = 1, Silver = 2, Gold = 3 }

// 인연도(affinity) 도메인 상수/계산의 단일 출처.
// 계획: Assets/Prefabs/UI/CharacterDetail/Affinity_Plan.md — 100포인트/레벨, 최대 Lv.10(1000pt), 6단계 명칭.
// 주의: "relationship"은 다른 시스템의 예약어 — 이 시스템의 어떤 식별자에도 쓰지 않는다.
public static class AffinityData
{
    public const int PointsPerLevel = 100;
    public const int MaxLevel = 10;
    public const int MaxPoints = PointsPerLevel * MaxLevel; // 1000

    // 누적 포인트 → 레벨 (0~10)
    public static int LevelFor(int points)
    {
        return Mathf.Min(Mathf.Max(points, 0) / PointsPerLevel, MaxLevel);
    }

    // 현재 레벨 안에서의 진행 포인트 (0~100). Lv.MAX면 100 고정.
    public static int PointsInLevel(int points)
    {
        int level = LevelFor(points);
        if (level >= MaxLevel) return PointsPerLevel;
        return Mathf.Max(points, 0) - level * PointsPerLevel;
    }

    // 현재 레벨 안에서의 진행률 (0~1). 게이지 fillAmount용.
    public static float ProgressInLevel(int points)
    {
        return PointsInLevel(points) / (float)PointsPerLevel;
    }

    // 6단계 명칭 (레벨 구간: 0~1 / 2~3 / 4~5 / 6~7 / 8~9 / 10)
    public static string StageNameFor(int level)
    {
        if (level >= 10) return "둘도 없는 사이";
        if (level >= 8) return "마음이 통하는 사이";
        if (level >= 6) return "허물없는 사이";
        if (level >= 4) return "친한 사이";
        if (level >= 2) return "아는 사이";
        return "낯선 사이";
    }

    // 레벨 보상 골드 — Lv.10만 200G, 나머지 100G
    public static int RewardGoldFor(int level)
    {
        return level >= MaxLevel ? 200 : 100;
    }

    // 레벨 보상 표기 (모달용). 골드 외 항목은 아직 표기만(후속 구현).
    public static string RewardDescFor(int level)
    {
        switch (level)
        {
            case 3: return "100G + 전용 장신구 (후속)";
            case 4: return "100G + 카드 동테 (후속)";
            case 7: return "100G + 카드 은테 (후속)";
            case 10: return "200G + 카드 금테 + 명칭 커스텀 (후속)";
            default: return "100G";
        }
    }

    // 카드 테두리 등급 판정 — Lv4~6 동테 / Lv7~9 은테 / Lv10 금테 / 그 미만은 없음
    public static AffinityBorderTier BorderTierFor(int level)
    {
        if (level >= 10) return AffinityBorderTier.Gold;
        if (level >= 7) return AffinityBorderTier.Silver;
        if (level >= 4) return AffinityBorderTier.Bronze;
        return AffinityBorderTier.None;
    }

    // 카드 테두리 틴트 — 보조 테두리(White 프레임)에 곱해질 색. None은 흰색(원본 그대로).
    public static Color BorderTintFor(AffinityBorderTier tier)
    {
        switch (tier)
        {
            case AffinityBorderTier.Bronze: return new Color(0.722f, 0.451f, 0.20f);  // 동 #B87333
            case AffinityBorderTier.Silver: return new Color(0.753f, 0.753f, 0.753f); // 은 #C0C0C0
            case AffinityBorderTier.Gold: return new Color(1f, 0.843f, 0f);           // 금 #FFD700
            default: return Color.white;
        }
    }
}
