// DeepLX 상수 정의
// Python deeplx_python/constants.py 포팅
public static class DeepLxConstants
{
    // DeepL API URL
    public const string API_URL = "https://www2.deepl.com/jsonrpc";
    
    // HTTP 상태 코드
    public const int HTTP_STATUS_OK = 200;
    public const int HTTP_STATUS_BAD_REQUEST = 400;
    public const int HTTP_STATUS_NOT_FOUND = 404;
    public const int HTTP_STATUS_TOO_MANY_REQUESTS = 429;
    public const int HTTP_STATUS_INTERNAL_ERROR = 500;
    public const int HTTP_STATUS_SERVICE_UNAVAILABLE = 503;
    
    // iOS 앱 흉내 헤더 (User-Agent 우회)
    public static readonly System.Collections.Generic.Dictionary<string, string> COMMON_HEADERS = new System.Collections.Generic.Dictionary<string, string>
    {
        { "Content-Type", "application/json" },
        { "User-Agent", "DeepL/1627620 CFNetwork/3826.500.62.2.1 Darwin/24.4.0" },
        { "Accept", "*/*" },
        { "X-App-Os-Name", "iOS" },
        { "X-App-Os-Version", "18.4.0" },
        { "Accept-Language", "en-US,en;q=0.9" },
        { "Accept-Encoding", "gzip, deflate, br" },
        { "X-App-Device", "iPhone16,2" },
        { "Referer", "https://www.deepl.com/" },
        { "X-Product", "translator" },
        { "X-App-Build", "1627620" },
        { "X-App-Version", "25.1" }
    };
    
    // 지원 언어 매핑 (소문자)
    public static readonly System.Collections.Generic.Dictionary<string, string> LANGUAGE_MAP = new System.Collections.Generic.Dictionary<string, string>
    {
        { "bg", "BG" }, { "bulgarian", "BG" },
        { "zh", "ZH" }, { "chinese", "ZH" },
        { "cs", "CS" }, { "czech", "CS" },
        { "da", "DA" }, { "danish", "DA" },
        { "nl", "NL" }, { "dutch", "NL" },
        { "en", "EN" }, { "english", "EN" },
        { "et", "ET" }, { "estonian", "ET" },
        { "fi", "FI" }, { "finnish", "FI" },
        { "fr", "FR" }, { "french", "FR" },
        { "de", "DE" }, { "german", "DE" },
        { "el", "EL" }, { "greek", "EL" },
        { "hu", "HU" }, { "hungarian", "HU" },
        { "it", "IT" }, { "italian", "IT" },
        { "ja", "JA" }, { "japanese", "JA" }, { "jp", "JA" },
        { "lv", "LV" }, { "latvian", "LV" },
        { "lt", "LT" }, { "lithuanian", "LT" },
        { "pl", "PL" }, { "polish", "PL" },
        { "pt", "PT" }, { "portuguese", "PT" },
        { "ro", "RO" }, { "romanian", "RO" },
        { "ru", "RU" }, { "russian", "RU" },
        { "sk", "SK" }, { "slovak", "SK" },
        { "sl", "SL" }, { "slovenian", "SL" },
        { "es", "ES" }, { "spanish", "ES" },
        { "sv", "SV" }, { "swedish", "SV" },
        { "tr", "TR" }, { "turkish", "TR" },
        { "id", "ID" }, { "indonesian", "ID" },
        { "uk", "UK" }, { "ukrainian", "UK" },
        { "ko", "KO" }, { "korean", "KO" }
    };
}
