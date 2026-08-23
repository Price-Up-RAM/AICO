using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using UnityEngine.UI;
using System.IO;
using TMPro;

public class ServerManager : MonoBehaviour
{
    private const int LocalServerTypeIndex = 1;
    private const int RemoteServerTypeIndex = 10;
    private const string DefaultTunnelDomain = "60000123.xyz";

    public string baseUrl = "";
    public string tunnelDomain = DefaultTunnelDomain;  // Cloudflare 터널 고정 URL 루트 도메인
    private Dictionary<string, string> serverUrlCache = new Dictionary<string, string>();  // server_id별 URL 캐시

    private string ngrokUrl;
    private string ngrokStatus;
    private bool isConnected = false;  // 일단 1회라도 연결된적이 있는지(불가역)
    private float connectTimer = 0f;  // 타이머 변수
    private string resolvedConnectionKey = "";  // 서버 타입/ID 변경 뒤 이전 baseUrl 재사용 방지

    public Text serverStatusText;

    // 싱글톤 인스턴스
    private static ServerManager instance;
    public static ServerManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ServerManager>();
            }
            return instance;
        }
    }

    private void Start()
    {
        StartCoroutine(SetBaseUrl());
    }

    void Update()
    {
        if (!isConnected)
        {
            // 타이머가 10초마다 1회씩 동작하도록 설정
            connectTimer += Time.deltaTime;

            if (connectTimer >= 10f)
            {
                // 10초마다 isConnected가 false일 경우 SetBaseUrl을 호출
                StartCoroutine(SetBaseUrl());

                // 타이머 리셋
                connectTimer = 0f;
            }
        }
    }

    public void GetBaseUrl(Action<string> callback)
    {
        // 이미 연결되어 있고 값이 있으면 즉시 반환
        if (isConnected && !string.IsNullOrEmpty(baseUrl) && resolvedConnectionKey == GetConnectionKey())
        {
            callback?.Invoke(baseUrl);
            return;
        }

        // 값이 없으면 코루틴으로 조회 후 콜백 호출
        StartCoroutine(GetBaseUrlCoroutine(callback));
    }

    private string GetConnectionKey()
    {
        SettingManager.SettingsData settings = SettingManager.Instance != null ? SettingManager.Instance.settings : null;
        int serverType = settings != null ? settings.server_type_idx : 0;
        string serverId = settings != null && settings.server_id != null ? settings.server_id.Trim().ToLowerInvariant() : "";
        return serverType + ":" + serverId;
    }

    private IEnumerator GetBaseUrlCoroutine(Action<string> callback)
    {
        // SetBaseUrl 실행하고 완료 대기
        yield return StartCoroutine(SetBaseUrl());
        
        // 완료 후 결과 반환
        callback?.Invoke(baseUrl);
    }

    public string SetBaseUrlToDevServer()
    {
        // SetBaseUrl()을 직접 실행하고 완료될 때까지 대기
        int maxAttempts = 500; // 최대 500프레임 (약 5초)

        IEnumerator setBaseUrlCoroutine = SetDevServer();
        while (setBaseUrlCoroutine.MoveNext() && maxAttempts > 0)
        {
            maxAttempts--;
        }
        if (maxAttempts == 0)
        {
            Debug.LogError("SetBaseUrlToDevServer timeout!");
            return ""; // 실패 시 빈 문자열 반환
        }

        return baseUrl;
    }

    public void GetServerUrlFromServerId(string server_id, Action<string> onComplete)
    {
        // 캐시가 있는 경우 즉시 반환
        if (false)  // 캐시 가져오기 설정 없으면 기본적으로 가져오기
        {
            if (serverUrlCache.ContainsKey(server_id))
            {
                Debug.Log($"[GetServerUrlFromServerId] 캐시 사용: server_id={server_id}, url={serverUrlCache[server_id]}");
                onComplete?.Invoke(serverUrlCache[server_id]);
                return;
            }
        }


        StartCoroutine(GetServerUrlCoroutine(server_id, onComplete));
    }

    private IEnumerator GetServerUrlCoroutine(string server_id, Action<string> onComplete)
    {
        // 일반 서버 ID는 Cloudflare 고정 주소를 직접 조립한다.
        // dev_voice/temp 등 동적 게시 서버는 기존 Supabase 폴백을 사용한다.
        string normalizedServerId;
        if (!IsLegacyPublishedServerId(server_id) && TryNormalizeServerId(server_id, out normalizedServerId))
        {
            string cfUrl = BuildTunnelUrl(normalizedServerId);
            bool cfReachable = false;
            yield return StartCoroutine(IsUrlReachable(cfUrl + "/health", result => cfReachable = result));
            if (cfReachable)
            {
                serverUrlCache[normalizedServerId] = cfUrl;
                Debug.Log($"[GetServerUrlFromServerId] CF 직조립 성공: server_id={normalizedServerId}, url={cfUrl}");
                onComplete?.Invoke(cfUrl);
                yield break;
            }
        }

        // 동적 게시 서버 및 전환기 폴백
        string ngrokSupabaseUrl = "https://lxmkzckwzasvmypfoapl.supabase.co/storage/v1/object/sign/json_bucket/my_little_jarvis_plus_ngrok_server.json?token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1cmwiOiJqc29uX2J1Y2tldC9teV9saXR0bGVfamFydmlzX3BsdXNfbmdyb2tfc2VydmVyLmpzb24iLCJpYXQiOjE3MzM4Mzg4MjYsImV4cCI6MjA0OTE5ODgyNn0.ykDVTXYVXNnKJL5lXILSk0iOqt0_7UeKZqOd1Qv_pSY&t=2024-12-10T13%3A53%3A47.907Z";
        string supabaseApiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imx4bWt6Y2t3emFzdm15cGZvYXBsIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzM4MzUxNzQsImV4cCI6MjA0OTQxMTE3NH0.zmEKHhIcQa4ODekS2skgknlXi8Hbd8JjpjBlFZpPsJ8";

        using (UnityWebRequest request = UnityWebRequest.Get(ngrokSupabaseUrl))
        {
            request.SetRequestHeader("Authorization", $"Bearer {supabaseApiKey}");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[GetServerUrlFromServerId] 요청 실패: {request.error}");
                onComplete?.Invoke(null);
                yield break;
            }

            string jsonResponse = request.downloadHandler.text;
            var fullData = JsonConvert.DeserializeObject<Dictionary<string, NgrokJsonResponse>>(jsonResponse);

            if (fullData != null && fullData.ContainsKey(server_id))
            {
                NgrokJsonResponse data = fullData[server_id];
                Debug.Log($"[GetServerUrlFromServerId] 서버 ID '{server_id}' 의 URL: {data.url}");

                if (data.status == "closed")
                {
                    NoticeBalloonManager.Instance?.ModifyNoticeBalloonText("Supabase Server Closed");
                    onComplete?.Invoke(null);
                    yield break;
                }
                else if (data.status != "open")
                {
                    NoticeBalloonManager.Instance?.ModifyNoticeBalloonText("Supabase Server Not Opened");
                    onComplete?.Invoke(null);
                    yield break;
                }

                // 서버 URL 캐시 저장
                serverUrlCache[server_id] = data.url;
                Debug.Log($"[GetServerUrlFromServerId] server_id '{server_id}' URL 캐시 저장: {data.url}");

                onComplete?.Invoke(data.url);
            }
            else
            {
                Debug.LogWarning($"[GetServerUrlFromServerId] 서버 ID '{server_id}' 없음");
                onComplete?.Invoke(null);
            }
        }
    }

    public TextMeshProUGUI baseUrlText;

    // 선택된 서버 타입에 따라 Base URL 설정
    private IEnumerator SetBaseUrl()
    {
        SettingManager.SettingsData settings = SettingManager.Instance != null ? SettingManager.Instance.settings : null;
        int serverType = settings != null ? settings.server_type_idx : 0;
        string serverId = settings != null ? settings.server_id : "";
        string requestedConnectionKey = serverType + ":" + (serverId ?? "").Trim().ToLowerInvariant();

        // Local과 일반 Server는 Supabase를 조회하지 않는다.
        // Auto 및 temp 같은 레거시 동적 ID에만 게시 URL 폴백을 준비한다.
        bool fixedRemoteServer = serverType == RemoteServerTypeIndex && !IsLegacyPublishedServerId(serverId);
        if (serverType != LocalServerTypeIndex && !fixedRemoteServer)
        {
            yield return StartCoroutine(FetchNgrokJsonData());
        }
        else
        {
            ngrokUrl = null;
            ngrokStatus = null;
        }

        string resolvedBaseUrl = "";
        yield return StartCoroutine(DetermineBaseUrl(serverType, serverId, result => resolvedBaseUrl = result));

        // /health 확인 중 설정이 바뀌었다면 이전 요청 결과를 현재 연결로 확정하지 않는다.
        if (requestedConnectionKey != GetConnectionKey())
        {
            yield return StartCoroutine(SetBaseUrl());
            yield break;
        }

        baseUrl = resolvedBaseUrl;

        if (!string.IsNullOrEmpty(baseUrl))
        {
            isConnected = true;
            resolvedConnectionKey = requestedConnectionKey;
            Debug.Log("Final Base URL: " + baseUrl);
            baseUrlText.text = baseUrl;
        }
        else
        {
            isConnected = false;
            resolvedConnectionKey = "";
            // Debug.LogError("Base URL 설정 실패");
        }
    }

    // URL 순서대로 확인하고 baseUrl 설정
    private IEnumerator DetermineBaseUrl(int serverType, string serverId, Action<string> onComplete)
    {
        bool isReachable = false;
        Debug.Log($"[DetermineBaseUrl] Start! serverType: {serverType}, serverId: {serverId}");

        // Local 선택 시에는 localhost만 사용한다.
        if (serverType == LocalServerTypeIndex)
        {
            Debug.Log("[DetermineBaseUrl] Trying Local (127.0.0.1:5000)");
            yield return StartCoroutine(IsUrlReachable("http://127.0.0.1:5000/health", result => isReachable = result));
            onComplete?.Invoke(isReachable ? "http://127.0.0.1:5000" : "");
            yield break;
        }

        // Server 선택 시 일반 ID는 https://{server_id}.60000123.xyz만 사용한다.
        if (serverType == RemoteServerTypeIndex && !IsLegacyPublishedServerId(serverId))
        {
            string normalizedServerId;
            if (!TryNormalizeServerId(serverId, out normalizedServerId))
            {
                Debug.LogError("[ServerManager] 서버 ID 형식이 올바르지 않습니다: " + serverId);
                onComplete?.Invoke("");
                yield break;
            }

            string cfUrl = BuildTunnelUrl(normalizedServerId);
            Debug.Log($"[DetermineBaseUrl] Trying Cloudflare Tunnel: {cfUrl}");
            yield return StartCoroutine(IsUrlReachable(cfUrl + "/health", result => isReachable = result));
            Debug.Log($"[DetermineBaseUrl] Cloudflare Tunnel Reachable? {isReachable}");
            onComplete?.Invoke(isReachable ? cfUrl : "");
            yield break;
        }

        // Server에서 temp/dev_voice를 명시한 경우에는 localhost로 바꾸지 않고 게시 URL만 사용한다.
        if (serverType == RemoteServerTypeIndex)
        {
            if (!string.IsNullOrEmpty(ngrokUrl))
            {
                yield return StartCoroutine(IsUrlReachable(ngrokUrl + "/health", result => isReachable = result));
            }
            onComplete?.Invoke(isReachable ? ngrokUrl : "");
            yield break;
        }

        // Auto 등 기존 모드는 localhost를 우선 유지한다.
        yield return StartCoroutine(IsUrlReachable("http://127.0.0.1:5000/health", result => isReachable = result));
        if (isReachable)
        {
            onComplete?.Invoke("http://127.0.0.1:5000");
            yield break;
        }

        // temp 등 레거시 동적 서버는 Supabase 게시 URL을 폴백으로 사용한다.
        if (!string.IsNullOrEmpty(ngrokUrl))
        {
            yield return StartCoroutine(IsUrlReachable(ngrokUrl + "/health", result => isReachable = result));
            if (isReachable)
            {
                onComplete?.Invoke(ngrokUrl);
                yield break;
            }
        }

        // AICO의 기존 Auto 폴백 유지
        yield return StartCoroutine(IsUrlReachable("https://minmin496969.loca.lt/health", result => isReachable = result));
        if (isReachable)
        {
            onComplete?.Invoke("https://minmin496969.loca.lt");
            yield break;
        }

        onComplete?.Invoke("");
    }

    private string BuildTunnelUrl(string serverId)
    {
        return "https://" + serverId + "." + GetTunnelDomain();
    }

    private string GetTunnelDomain()
    {
        return string.IsNullOrWhiteSpace(tunnelDomain) ? DefaultTunnelDomain : tunnelDomain.Trim().Trim('.');
    }

    private static bool IsLegacyPublishedServerId(string serverId)
    {
        return string.Equals(serverId, "temp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(serverId, "dev_voice", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeServerId(string value, out string serverId)
    {
        serverId = string.IsNullOrEmpty(value) ? "" : value.Trim().ToLowerInvariant();
        if (serverId.Length < 3 || serverId.Length > 32 ||
            !IsLowerAlphaNumeric(serverId[0]) || !IsLowerAlphaNumeric(serverId[serverId.Length - 1]))
        {
            return false;
        }

        for (int i = 1; i < serverId.Length - 1; i++)
        {
            char c = serverId[i];
            if (!IsLowerAlphaNumeric(c) && c != '-')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsLowerAlphaNumeric(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
    }

    // URL 연결 가능 여부 확인
    private IEnumerator IsUrlReachable(string url, Action<bool> callback)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 3;
            request.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                callback(true);
            }
            else
            {
                Debug.LogWarning($"[IsUrlReachable] Failed: {url}. Result: {request.result}, Code: {request.responseCode}, Error: {request.error}");
                callback(false);
            }
        }
    }

    // 서버 상태 확인
    public void CallCheckServerStatus()
    {
        // Base URL 설정 후 상태 확인
        StartCoroutine(CheckServerStatus());
    }

    public IEnumerator CheckServerStatus()
    {
        serverStatusText.text = "Checking...";

        // Base URL 설정
        yield return StartCoroutine(SetBaseUrl());

        // Base URL 상태 확인 및 Text 선택
        if (string.IsNullOrEmpty(baseUrl))
        {
            serverStatusText.text = "Fail";
            Debug.Log("서버 상태: Fail");
        }
        else if (baseUrl.Contains("127.0.0.1"))
        {
            serverStatusText.text = "Local";
            Debug.Log("서버 상태: Local");
        }
        else if (baseUrl.Contains(GetTunnelDomain()))
        {
            serverStatusText.text = "Tunnel";
            Debug.Log("서버 상태: Tunnel");
        }
        else if (baseUrl.Contains("ngrok"))
        {
            serverStatusText.text = "Ngrok";
            Debug.Log("서버 상태: Ngrok");
        }
        else if (baseUrl.Contains("loca.lt"))
        {
            serverStatusText.text = "LocalTunnel";
            Debug.Log("서버 상태: Loca.lt");
        }
    }

    // FetchNgrokJsonData 구현 (server_id 대기 포함)
    private IEnumerator FetchNgrokJsonData()
    {
        // 최대 3초 동안 server_id 대기
        string server_id = "temp";
        float elapsedTime = 0f;
        const float timeout = 3f;

        while (string.IsNullOrEmpty(SettingManager.Instance.settings?.server_id) && elapsedTime < timeout)
        {
            elapsedTime += Time.deltaTime;
            // Debug.Log("Waiting for server_id to be initialized...");
            yield return null; // 다음 프레임까지 대기
        }

        // 타임아웃 발생 시 기본 값 사용
        if (string.IsNullOrEmpty(SettingManager.Instance.settings?.server_id))
        {
            // Debug.LogWarning("server_id 초기화 시간 초과. 기본 값 사용.");
        }
        else
        {
            server_id = SettingManager.Instance.settings.server_id;
        }

        // Debug.Log("server_id : " + server_id);

        // Supabase 요청 URL 및 API 키
        string ngrokSupabaseUrl = "https://lxmkzckwzasvmypfoapl.supabase.co/storage/v1/object/sign/json_bucket/my_little_jarvis_plus_ngrok_server.json?token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1cmwiOiJqc29uX2J1Y2tldC9teV9saXR0bGVfamFydmlzX3BsdXNfbmdyb2tfc2VydmVyLmpzb24iLCJpYXQiOjE3MzM4Mzg4MjYsImV4cCI6MjA0OTE5ODgyNn0.ykDVTXYVXNnKJL5lXILSk0iOqt0_7UeKZqOd1Qv_pSY&t=2024-12-10T13%3A53%3A47.907Z";
        string supabaseApiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imx4bWt6Y2t3emFzdm15cGZvYXBsIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzM4MzUxNzQsImV4cCI6MjA0OTQxMTE3NH0.zmEKHhIcQa4ODekS2skgknlXi8Hbd8JjpjBlFZpPsJ8";

        using (UnityWebRequest request = UnityWebRequest.Get(ngrokSupabaseUrl))
        {
            // 인증 헤더 추가
            request.SetRequestHeader("Authorization", $"Bearer {supabaseApiKey}");

            // 서버 요청
            yield return request.SendWebRequest();

            // 에러 처리
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                // Debug.LogError($"Error fetching JSON data: {request.error}");
            }
            else
            {
                // JSON 데이터를 문자열로 가져옴
                string jsonResponse = request.downloadHandler.text;

                // JSON 데이터 파싱
                var fullData = JsonConvert.DeserializeObject<Dictionary<string, NgrokJsonResponse>>(jsonResponse);
                if (fullData != null && fullData.ContainsKey(server_id))
                {
                    NgrokJsonResponse data = fullData[server_id];
                    // Debug.Log($"Fetched URL: {data.url}");

                    ngrokUrl = data.url;
                    ngrokStatus = data.status;

                    if (ngrokStatus == "closed")
                    {
                        NoticeBalloonManager.Instance.ModifyNoticeBalloonText("Supabase Server Closed");
                    }
                    else if (ngrokStatus != "open")
                    {
                        NoticeBalloonManager.Instance.ModifyNoticeBalloonText("Supabase Server Not Opened");
                    }
                }
                else
                {
                    ngrokUrl = null;
                    ngrokStatus = null;
                    Debug.LogError($"Server ID '{server_id}' not found in JSON data.");
                }
            }
        }
    }

    // m9dev 서버로 강제 설정
    private IEnumerator SetDevServer()
    {
        // 최대 3초 동안 server_id 대기
        // string server_id = "m9dev";
        string server_id = "sound_dev";

        // Supabase 요청 URL 및 API 키
        string ngrokSupabaseUrl = "https://lxmkzckwzasvmypfoapl.supabase.co/storage/v1/object/sign/json_bucket/my_little_jarvis_plus_ngrok_server.json?token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1cmwiOiJqc29uX2J1Y2tldC9teV9saXR0bGVfamFydmlzX3BsdXNfbmdyb2tfc2VydmVyLmpzb24iLCJpYXQiOjE3MzM4Mzg4MjYsImV4cCI6MjA0OTE5ODgyNn0.ykDVTXYVXNnKJL5lXILSk0iOqt0_7UeKZqOd1Qv_pSY&t=2024-12-10T13%3A53%3A47.907Z";
        string supabaseApiKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imx4bWt6Y2t3emFzdm15cGZvYXBsIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzM4MzUxNzQsImV4cCI6MjA0OTQxMTE3NH0.zmEKHhIcQa4ODekS2skgknlXi8Hbd8JjpjBlFZpPsJ8";

        using (UnityWebRequest request = UnityWebRequest.Get(ngrokSupabaseUrl))
        {
            // 인증 헤더 추가
            request.SetRequestHeader("Authorization", $"Bearer {supabaseApiKey}");

            // 서버 요청
            yield return request.SendWebRequest();

            // 에러 처리
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error fetching JSON data: {request.error}");
            }
            else
            {
                // JSON 데이터를 문자열로 가져옴
                string jsonResponse = request.downloadHandler.text;

                // JSON 데이터 파싱
                var fullData = JsonConvert.DeserializeObject<Dictionary<string, NgrokJsonResponse>>(jsonResponse);
                if (fullData != null && fullData.ContainsKey(server_id))
                {
                    NgrokJsonResponse data = fullData[server_id];
                    Debug.Log($"Fetched URL: {data.url}");

                    ngrokUrl = data.url;
                    ngrokStatus = data.status;

                    if (ngrokStatus == "closed")
                    {
                        NoticeBalloonManager.Instance.ModifyNoticeBalloonText("Supabase Server Closed");
                        yield break;
                    }
                    else if (ngrokStatus != "open")
                    {
                        NoticeBalloonManager.Instance.ModifyNoticeBalloonText("Supabase Server Not Opened");
                        yield break;
                    }

                    // 이대로 호출
                    bool isReachable = false;
                    yield return StartCoroutine(IsUrlReachable(ngrokUrl + "/health", result => isReachable = result));
                    if (isReachable)
                    {
                        baseUrl = ngrokUrl;
                        yield break;
                    }

                    serverStatusText.text = "m9dev";
                    Debug.Log("서버 상태: Loca.lt");

                }
            }
        }
    }

    //////////////////////// APIKeyValidator : 주어진 API 키가 유효한지 검사.
    public Text keyTestResultText;
    public Text keyChoiceInputTestResultText;
    
    // Task 기반 Gemini API Key 검증 (비동기 호출용)
    public async Task<bool> ValidateGeminiAPIKeyAsync(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return false;
        
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(ValidateGeminiAPIKeyAsyncCoroutine(apiKey, tcs));
        return await tcs.Task;
    }

    private IEnumerator ValidateGeminiAPIKeyAsyncCoroutine(string apiKey, TaskCompletionSource<bool> tcs)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        
        yield return request.SendWebRequest();
        
        bool isValid = request.responseCode == 200;
        tcs.SetResult(isValid);
    }
    
    // // TODO : 오픈라우터 예시... 구현 필요.
    // // Task 기반 OpenRouter API Key 검증 (비동기 호출용)
    // public async Task<bool> ValidateOpenRouterAPIKeyAsync(string apiKey)
    // {
    //     if (string.IsNullOrEmpty(apiKey)) return false;
        
    //     var tcs = new TaskCompletionSource<bool>();
    //     StartCoroutine(ValidateOpenRouterAPIKeyAsyncCoroutine(apiKey, tcs));
    //     return await tcs.Task;
    // }

    // private IEnumerator ValidateOpenRouterAPIKeyAsyncCoroutine(string apiKey, TaskCompletionSource<bool> tcs)
    // {
    //     string model = LoadModelFromLocal();
    //     if (string.IsNullOrEmpty(model))
    //     {
    //         bool done = false;
    //         string result = null;
    //         yield return GetLatestFreeOpenRouterModel((fetchedModel) =>
    //         {
    //             result = string.IsNullOrEmpty(fetchedModel) ? "google/gemma-4-31b-it:free" : fetchedModel;
    //             done = true;
    //         });
    //         while (!done) yield return null;
    //         model = result;
    //     }

    //     string url = "https://openrouter.ai/api/v1/chat/completions";
    //     string json = $"{{\"model\": \"{model}\", \"messages\": [{{\"role\": \"user\", \"content\": \"hello\"}}]}}";

    //     UnityWebRequest request = new UnityWebRequest(url, "POST");
    //     byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
    //     request.uploadHandler = new UploadHandlerRaw(bodyRaw);
    //     request.downloadHandler = new DownloadHandlerBuffer();
    //     request.SetRequestHeader("Content-Type", "application/json");
    //     request.SetRequestHeader("Authorization", "Bearer " + apiKey);
    //     request.SetRequestHeader("HTTP-Referer", "https://yourapp.example.com");
    //     request.SetRequestHeader("X-Title", "MyLittleJarvis");

    //     yield return request.SendWebRequest();
        
    //     bool isValid = request.responseCode == 200;
    //     tcs.SetResult(isValid);
    // }
    
    // Gemini Test 버튼으로 호출
    public void CallValidateGeminiAPIKey()
    {
        SettingManager.Instance.serverGeminikeyTestResultText.text = "Testing...";
        keyChoiceInputTestResultText.text = "Testing...";
        string apiKey = SettingManager.Instance.settings.api_key_gemini;
        
        // 검증 후 캐시 무효화하여 재검증 강제
        ApiKei.InvalidateCache();
        
        StartCoroutine(ValidateGeminiAPIKeyWithCache(apiKey));
    }
    
    private IEnumerator ValidateGeminiAPIKeyWithCache(string apiKey)
    {
        yield return StartCoroutine(ValidateGeminiAPIKey(apiKey));
        
        // 검증 결과를 캐시에 저장
        if (SettingManager.Instance.serverGeminikeyTestResultText.text == "Success")
        {
            // 수동으로 캐시 업데이트 (public 필드로 접근 불가하므로 재검증 유도)
            Debug.Log("[ServerManager] API key validated via Test button");
        }
    }
    
    // OpenRouter Test 버튼으로 호출
    public void CallValidateOpenRouterAPIKey()
    {
        SettingManager.Instance.serverOpenRouterkeyTestResultText.text = "Testing...";
        keyChoiceInputTestResultText.text = "Testing...";
        string apiKey = SettingManager.Instance.settings.api_key_openRouter;
        StartCoroutine(ValidateOpenRouterAPIKey(apiKey));
    }

    // Gemini API Key 검증 코루틴
    private IEnumerator ValidateGeminiAPIKey(string apiKey)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        
        yield return request.SendWebRequest();
        
        string result = request.responseCode == 200 ? "Success" : "Fail";
        SettingManager.Instance.serverGeminikeyTestResultText.text = result;
        keyChoiceInputTestResultText.text = result;
    }
    
    // OpenRouter API Key 검증 코루틴
    private IEnumerator ValidateOpenRouterAPIKey(string apiKey)
    {
        string model = LoadModelFromLocal();

        if (string.IsNullOrEmpty(model))
        {
            bool done = false;
            string result = null;

            yield return GetLatestFreeOpenRouterModel((fetchedModel) =>
            {
                result = string.IsNullOrEmpty(fetchedModel) ? "google/gemma-4-31b-it:free" : fetchedModel;
                done = true;
            });

            while (!done)
                yield return null;

            model = result;
        }

        string url = "https://openrouter.ai/api/v1/chat/completions";
        string json = $"{{\"model\": \"{model}\", \"messages\": [{{\"role\": \"user\", \"content\": \"hello\"}}]}}";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.SetRequestHeader("HTTP-Referer", "https://yourapp.example.com");
        request.SetRequestHeader("X-Title", "MyLittleJarvis");

        yield return request.SendWebRequest();

        string testResult = request.responseCode == 200 ? "Success" : "Fail";
        SettingManager.Instance.serverOpenRouterkeyTestResultText.text = testResult;
        keyChoiceInputTestResultText.text = testResult;
        if (request.responseCode == 200)
            Debug.Log($"[OpenRouter] Model used for test: {model}");
    }

    private string LoadModelFromLocal()
    {
        try
        {
            string path = Path.Combine(Application.dataPath, "../config/free_models.txt");
            if (File.Exists(path))
            {
                string line = File.ReadAllLines(path)[0];
                return line.Trim();
            }
        }
        catch { }
        return null;
    }

    private IEnumerator GetLatestFreeOpenRouterModel(Action<string> onResult)
    {
        string url = "https://openrouter.ai/api/v1/models";
        UnityWebRequest request = UnityWebRequest.Get(url);

        yield return request.SendWebRequest();

        if (request.responseCode != 200)
        {
            onResult(null);
            yield break;
        }

        try
        {
            var json = request.downloadHandler.text;
            var wrapped = "{\"data\":" + json + "}"; // JsonUtility가 배열 파싱 못함 대비
            var parsed = JsonUtility.FromJson<OpenRouterModelList>(wrapped);
            string result = null;

            foreach (var model in parsed.data)
            {
                if (model.pricing.prompt == "0" &&
                    model.pricing.completion == "0" &&
                    (model.id.Contains("qwen/") || model.id.Contains("meta-llama/") || model.id.Contains("google/")) &&
                    !model.id.Contains("think") &&
                    !model.id.Contains("deepseek"))
                {
                    result = model.id;
                    break;
                }
            }

            onResult(result);
        }
        catch
        {
            onResult(null);
        }
    }

    private IEnumerator ValidateDefaultGET(string url, string authHeader)
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        if (!string.IsNullOrEmpty(authHeader))
            request.SetRequestHeader("Authorization", authHeader);

        yield return request.SendWebRequest();

        keyTestResultText.text = request.responseCode == 200 ? "Success" : "Fail";
        keyChoiceInputTestResultText.text = request.responseCode == 200 ? "Success" : "Fail";
    }

    [Serializable]
    public class OpenRouterModelList
    {
        public List<OpenRouterModel> data;
    }

    [Serializable]
    public class OpenRouterModel
    {
        public string id;
        public string name;
        public string created;
        public Pricing pricing;

        [Serializable]
        public class Pricing
        {
            public string prompt;
            public string completion;
        }
    }
    //////////////////////// APIKeyValidator End


}

// Ngrok JSON 응답 클래스
[Serializable]
public class NgrokJsonResponse
{
    public string url;
    public string status;
}
