using System.Threading.Tasks;

// API 키 관리 (Python kei.py 포팅)
// 사용법: await ApiKei.GetValidatedGeminiKey() 또는 ApiKei.GetNextGeminiKey()

public static class ApiKei
{
    // Gemini API Keys
    public static string GEMINI_API_KEY = "AIzaSyCRQiGPYeHFkgTwicPNgr_XPAr5QcuhHdQ";  // main
    
    public static string[] GEMINI_API_KEYS = new string[]
    {
        "AIzaSyBCxFC9xWF-jVwIcGwPVvZ7ogRMDowxua8",  // 1
        "AIzaSyD4B8-6fCHDddSJC12fbKh60O_R_on2pdU",  // 2
        // "AIzaSyAl8dQMhHLVPizuU_CjQ7k5Ar_ll-ysfGs",  // 3
        // "AIzaSyACaQf-ZCWzNfHzkQQwUDJ2rMrCEf-hHlw",  // 4
        // "AIzaSyDP0GObFu912N2wxQHSc9VSqr41gK6iO_E",  // 99
    };
    
    // 키 순환 인덱스
    private static int currentKeyIndex = 0;
    
    // 무료 Gemini 통화 잔여 횟수 (Sample 에디션 or 사용자 키 없을 때 소모)
    public static int freeGeminiCallCount = 10;
    
    // 검증 캐시
    private static string last_checked_key = "";
    private static bool is_valid = false;
    private static bool is_checking = false;
    
    // 검증된 Gemini API 키 반환 (비동기)
    public static async Task<string> GetValidatedGeminiKey()
    {
        // 1. SettingManager의 키 확인
        string userKey = SettingManager.Instance?.settings?.api_key_gemini ?? "";
        
        // 2. 사용자 키가 있으면 검증
        if (!string.IsNullOrEmpty(userKey))
        {
            // 2-1. 캐시 확인 (같은 키가 이미 검증됨)
            if (userKey == last_checked_key && is_valid)
            {
                return userKey;
            }
            
            // 2-2. 검증 중이 아닐 때만 검증
            if (!is_checking)
            {
                is_checking = true;
                bool valid = await ServerManager.Instance.ValidateGeminiAPIKeyAsync(userKey);
                is_checking = false;
                
                // 2-3. 검증 결과 캐싱
                last_checked_key = userKey;
                is_valid = valid;
                
                if (valid)
                {
                    UnityEngine.Debug.Log("[ApiKei] Using validated user API key");
                    return userKey;
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[ApiKei] User API key invalid, falling back to key array");
                }
            }
        }
        
        // 3. Fallback: 키 배열 사용 (카운트 소진 시 null 반환)
        return GetNextGeminiKey();
    }
    
    // 캐시 초기화 (키 변경 시 호출)
    public static void InvalidateCache()
    {
        last_checked_key = "";
        is_valid = false;
    }
    
    // 다음 Gemini API 키 반환 (순환)
    // 무료 카운트를 소모하며, 0 이하가 되면 시나리오를 트리거하고 null 반환
    public static string GetNextGeminiKey()
    {
        if (GEMINI_API_KEYS.Length == 0)
            return GEMINI_API_KEY;
        
        // 잔여 횟수 차감 (DevMode 제외)
        if (!DevManager.Instance.IsDevModeEnabled())
        {
            freeGeminiCallCount--;
        }
        
        if (freeGeminiCallCount < 0)
        {
            int serverTypeIdx = SettingManager.Instance?.settings?.server_type_idx ?? 0;
            if (serverTypeIdx != 0 && serverTypeIdx != 1)
            {
                UnityEngine.Debug.LogWarning("[ApiKei] Free Gemini call count exhausted. Triggering I03 scenario.");
                // 무료 서버 소진 → I03 시나리오 (서버 설치 or 외부 플랫폼 안내)
                ScenarioInstallerManager.Instance.StartCoroutine(
                    ScenarioInstallerManager.Instance.Scenario_I03_FreeKeyExhausted()
                );
            }
            else
            {
                UnityEngine.Debug.LogWarning("[ApiKei] Free Gemini call count exhausted. I03 scenario skipped in Auto/Local mode.");
            }
            return null;
        }
        
        string key = GEMINI_API_KEYS[currentKeyIndex];
        currentKeyIndex = (currentKeyIndex + 1) % GEMINI_API_KEYS.Length;
        return key;
    }
    
    // 현재 키 인덱스 반환
    public static int GetCurrentKeyIndex()
    {
        return currentKeyIndex;
    }
    
    // 키 인덱스 리셋
    public static void ResetKeyIndex()
    {
        currentKeyIndex = 0;
    }
}
