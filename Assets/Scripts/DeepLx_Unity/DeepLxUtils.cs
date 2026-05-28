using System;
using System.Linq;

// DeepLX 유틸리티 함수들
// Python deeplx_python/utils.py 포팅
public static class DeepLxUtils
{
    // 텍스트에서 'i' 문자의 개수를 반환
    public static int GetICount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        
        return text.Count(c => c == 'i');
    }
    
    // 요청 ID용 랜덤 번호 생성
    public static long GetRandomNumber()
    {
        Random random = new Random();
        int base_number = random.Next(0, 99999) + 8300000;
        return base_number * 1000L;
    }
    
    // i 개수를 기반으로 타임스탬프 생성
    public static long GetTimestamp(int iCount)
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        if (iCount != 0)
        {
            int adjustedCount = iCount + 1;
            return ts - (ts % adjustedCount) + adjustedCount;
        }
        
        return ts;
    }
    
    // 특정 간격 규칙에 따라 요청 JSON 문자열 포맷
    // Python의 format_post_string 포팅
    public static string FormatPostString(string jsonString, long requestId)
    {
        // 먼저 모든 형태를 콜론 붙은 형태로 정규화 (Newtonsoft.Json은 기본적으로 ": "를 사용)
        jsonString = jsonString.Replace("\"method\": \"", "\"method\":\"");
        jsonString = jsonString.Replace("\"method\" : \"", "\"method\":\"");
        
        // 특정 조건에 따라 공백 추가 (Python과 동일한 로직)
        bool shouldAddSpace = ((requestId + 5) % 29 == 0) || ((requestId + 3) % 13 == 0);
        
        if (shouldAddSpace)
        {
            jsonString = jsonString.Replace("\"method\":\"", "\"method\" : \"");
        }
        else
        {
            jsonString = jsonString.Replace("\"method\":\"", "\"method\": \"");
        }
        
        return jsonString;
    }
    
    // 언어 이름이나 코드를 표준 언어 코드로 변환
    public static string AbbreviateLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
            return null;
        
        // 하이픈이 있는 경우 첫 번째 부분만 사용 (예: en-US -> en)
        string langCode = language.Split('-')[0].ToLower();
        
        if (DeepLxConstants.LANGUAGE_MAP.ContainsKey(langCode))
        {
            return DeepLxConstants.LANGUAGE_MAP[langCode];
        }
        
        return null;
    }
    
    // 텍스트를 줄 단위로 분할하고 처리
    // Python의 split_and_process 포팅
    public static string[] SplitAndProcess(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new string[0];
        
        string[] lines = text.Split('\n');
        string[] result = new string[lines.Length];
        
        for (int i = 0; i < lines.Length; i++)
        {
            result[i] = string.IsNullOrWhiteSpace(lines[i]) ? "\n" : lines[i];
        }
        
        return result;
    }
}
