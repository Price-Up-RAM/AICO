using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json.Linq;

public class SampleServerManager : MonoBehaviour
{
    private static SampleServerManager instance;

    public static SampleServerManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SampleServerManager>();
            }
            return instance;
        }
    }

    private const int ServerPort = 5000;

    // 실행 파일/프로세스명 계약 — 감지 프로세스명은 실행 파일(server_sample.exe)과 반드시 일치해야 한다.
    // 불일치하면 자기 중복 기동은 못 잡고 무관 프로세스에는 오탐한다.
    private const string ServerExeName = "server_sample.exe";
    private const string ServerProcessName = "server_sample";

    // /health 응답의 정체 확인 값 (server_impl_health.py 의 "app" 필드와 계약)
    private const string HealthAppId = "my-little-jarvis-plus";

    // 헬스체크 파라미터 (JarvisServerManager 와 동일 정책)
    private const float HealthPrimaryWindowSeconds = 60f;
    private const float HealthPrimaryIntervalSeconds = 1f;
    private const float HealthRecheckIntervalSeconds = 10f;
    private const int HealthRequestTimeoutSeconds = 2;

    private Process jarvisProcess;  // 실행된 서버 프로세스를 저장할 변수
    private Coroutine healthCheckCoroutine;  // 중복 헬스체크 코루틴 방지용 핸들

    private void Start()
    {
        UnityEngine.Debug.Log("[Jarvis_Sample] SampleServerManager initialized");
        // 자동 시작 제거 - InstallStatusManager에서 수동으로 호출
    }

    // InstallStatusManager에서 호출할 초기화 함수
    public void InitializeForSample()
    {
        UnityEngine.Debug.Log("[Jarvis_Sample] SettingManager Fixed");
        SettingManager.Instance.settings.isShowChatBoxOnClick = true;
        UnityEngine.Debug.Log("[Jarvis_Sample] InitializeForSample() called");
        RunJarvisServerWithCheck();
    }

    public void RunJarvisServerWithCheck()
    {
        RunJarvisServer();
        if (healthCheckCoroutine != null)
        {
            StopCoroutine(healthCheckCoroutine);
        }
        healthCheckCoroutine = StartCoroutine(CheckHealthAndNotify());
    }

    private IEnumerator CheckHealthAndNotify()
    {
        string url = $"http://127.0.0.1:{ServerPort}/health";
        float startTime = Time.realtimeSinceStartup;
        bool primaryWindowExpired = false;

        while (true)
        {
            // 우리가 띄운 프로세스가 죽었으면 실패를 명확히 기록하고 저빈도 재확인으로 전환.
            // (하드스톱하지 않는 이유: 포트는 기존/외부 서버가 정상 서빙 중일 수 있다)
            if (HasOwnedProcessExited())
            {
                jarvisProcess = null;
                StatusManager.Instance.IsServerConnected = false;
                primaryWindowExpired = true;
                UnityEngine.Debug.LogWarning("[Jarvis_Sample] 서버 프로세스가 기동 직후 종료됨 - 저빈도 재확인으로 전환");
            }

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.timeout = HealthRequestTimeoutSeconds;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string identityNote;
                    if (IsExpectedServerIdentity(www.downloadHandler.text, out identityNote))
                    {
                        StatusManager.Instance.IsServerConnected = true;
                        UnityEngine.Debug.Log("[Jarvis_Sample] 서버 연결 성공 - StatusManager 업데이트" + identityNote);
                        // C01("서버 준비") 발화는 기동 직후 성공에만 (지연 성공의 시나리오 간섭 방지)
                        if (!primaryWindowExpired)
                        {
                            StartCoroutine(ScenarioCommonManager.Instance.Run_C01_ServerStarted());
                        }
                        healthCheckCoroutine = null;
                        yield break;
                    }
                    UnityEngine.Debug.LogWarning("[Jarvis_Sample] :5000 응답자가 예상한 서버가 아님" + identityNote + " - 재확인 계속");
                }
            }

            float elapsed = Time.realtimeSinceStartup - startTime;
            if (!primaryWindowExpired && elapsed >= HealthPrimaryWindowSeconds)
            {
                primaryWindowExpired = true;
                StatusManager.Instance.IsServerConnected = false;
                UnityEngine.Debug.LogWarning($"[Jarvis_Sample] {HealthPrimaryWindowSeconds:F0}초 내 서버 응답 없음 - C01 보류, {HealthRecheckIntervalSeconds:F0}초 간격 재확인으로 전환");
            }

            yield return new WaitForSeconds(primaryWindowExpired ? HealthRecheckIntervalSeconds : HealthPrimaryIntervalSeconds);
        }
    }

    // 우리가 직접 띄운 프로세스가 이미 종료됐는지 확인 (핸들 접근 불가 시 판단 보류)
    private bool HasOwnedProcessExited()
    {
        if (jarvisProcess == null) return false;
        try
        {
            return jarvisProcess.HasExited;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    // /health 응답 본문으로 응답자의 정체를 확인한다. (정책은 JarvisServerManager 와 동일)
    private bool IsExpectedServerIdentity(string body, out string note)
    {
        try
        {
            JObject json = JObject.Parse(body);
            string app = (string)json["app"];
            if (string.IsNullOrEmpty(app))
            {
                note = " (구버전 서버: app 필드 없음)";
                return true;
            }
            if (app == HealthAppId)
            {
                note = "";
                return true;
            }
            note = $" (app='{app}')";
            return false;
        }
        catch (System.Exception)
        {
            note = " (비JSON 응답 - 구버전/미상 서버)";
            return true;
        }
    }


    public bool IsJarvisServerRunning()
    {
        UnityEngine.Debug.Log("[Jarvis_Sample] IsJarvisServerRunning() called");
        bool result = Process.GetProcessesByName(ServerProcessName).Length > 0;
        UnityEngine.Debug.Log("[Jarvis_Sample] IsJarvisServerRunning() result: " + result);
        return result;
    }

    public void RunJarvisServer()
    {
        UnityEngine.Debug.Log("[Jarvis_Sample] RunJarvisServer() start");

        // 기존에 켜져있는거 있는지 확인
        if (IsJarvisServerRunning())
        {
            // 이전 기동분 핸들이 이미 죽어 있으면 폐기 — 살아있는 서버를 죽은 핸들 때문에 오판하지 않게 한다.
            if (HasOwnedProcessExited())
            {
                jarvisProcess = null;
            }
            // AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleInf();
            // AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText("Already Served");
            UnityEngine.Debug.Log("[Jarvis_Sample] Launch aborted: already running");
            return;
        }

        // 파일 확인
        string executablePath = Application.dataPath;
        string jarvisServerPath = Path.Combine(Path.GetDirectoryName(executablePath), ServerExeName);

        if (File.Exists(jarvisServerPath))
        {
            // 보안 프로그램(SmartScreen/AV) 차단 등으로 Process.Start 가 던지면
            // 호출 흐름 전체가 죽지 않도록 여기서 흡수하고 명확히 로그를 남긴다.
            try
            {
                jarvisProcess = RunJarvisServerProcess(jarvisServerPath);
                if (jarvisProcess == null)
                {
                    UnityEngine.Debug.LogWarning("[Jarvis_Sample] Process.Start 가 프로세스를 반환하지 않음: " + jarvisServerPath);
                }
            }
            catch (System.Exception e)
            {
                jarvisProcess = null;
                UnityEngine.Debug.LogError($"[Jarvis_Sample] 서버 실행 실패 (보안 프로그램 차단 가능성): {jarvisServerPath} - {e.Message}");
            }
            // AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleInf();
            // AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText("Init server...");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[Jarvis_Sample] Executable not found: " + jarvisServerPath);
        }

        UnityEngine.Debug.Log("[Jarvis_Sample] RunJarvisServer() end");
    }

    public Process RunJarvisServerProcess(string exePath)
    {
        UnityEngine.Debug.Log("[Jarvis_Sample] RunJarvisServerProcess() start: " + exePath);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            // Arguments = "--is_unity",
            UseShellExecute = true,
            CreateNoWindow = false
        };

        Process process = Process.Start(startInfo);

        UnityEngine.Debug.Log("[Jarvis_Sample] RunJarvisServerProcess() end");
        return process;
    }

    private IEnumerator CheckHealth()
    {
        UnityEngine.Debug.Log("[Jarvis_Sample] CheckHealth() start");

        string url = $"http://127.0.0.1:{ServerPort}/health";
        float timeout = 5f;
        float timer = 0f;

        while (timer < timeout)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.Log("[Jarvis_Sample] Health check OK");
                    AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText("Server OK");
                    yield break;
                }
            }

            yield return new WaitForSeconds(0.5f);
            timer += 0.5f;
        }

        UnityEngine.Debug.LogWarning("[Jarvis_Sample] Health check failed");
        AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText("Server Fail");

        UnityEngine.Debug.Log("[Jarvis_Sample] CheckHealth() end");
    }

    // /shutdown 방식 vs Process 직접 종료에서 후자 선택
    public void ShutdownServer()
    {
        UnityEngine.Debug.Log("[Jarvis_Sample] ShutdownServer() start");

        if (jarvisProcess != null && !jarvisProcess.HasExited)
        {
            try
            {
                jarvisProcess.Kill();
                StatusManager.Instance.IsServerConnected = false;
                UnityEngine.Debug.Log("[Jarvis_Sample] Server process killed - StatusManager 업데이트");
            }
            catch
            {
                UnityEngine.Debug.LogWarning("[Jarvis_Sample] Failed to kill process.");
            }
        }
        else
        {
            UnityEngine.Debug.Log("[Jarvis_Sample] No running server process to kill.");
        }

        UnityEngine.Debug.Log("[Jarvis_Sample] ShutdownServer() end");
    }

    private void OnApplicationQuit()
    {
        UnityEngine.Debug.Log("[Jarvis_Sample] OnApplicationQuit() -> ShutdownServer()");
        ShutdownServer();
    }
}
