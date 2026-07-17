using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json.Linq;

public class JarvisServerManager : MonoBehaviour
{
    private static JarvisServerManager instance;

    public static JarvisServerManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<JarvisServerManager>();
            }
            return instance;
        }
    }

    private const int ServerPort = 5000;

    // 실행 파일/프로세스명 계약 — 일반명("server" 등)은 무관한 타사 프로세스와
    // GetProcessesByName 이 충돌(기동 오차단)하므로 고유명 server_jarvis 를 쓴다.
    // 파일명의 단일 출처: InstallerManager 등 외부 판정도 이 상수를 참조한다.
    public const string ServerExeName = "server_jarvis.exe";
    private const string ServerProcessName = "server_jarvis";

    // /health 응답의 정체 확인 값 (server_impl_health.py 의 "app" 필드와 계약)
    private const string HealthAppId = "my-little-jarvis-plus";

    // 헬스체크 파라미터: 기동 직후엔 1초 간격으로 집중 확인하고,
    // 집중 구간이 지나면 저빈도 재확인으로 전환해 연결 상태가 스스로 회복되게 한다.
    private const float HealthPrimaryWindowSeconds = 60f;
    private const float HealthPrimaryIntervalSeconds = 1f;
    private const float HealthRecheckIntervalSeconds = 10f;
    private const int HealthRequestTimeoutSeconds = 2;  // 요청 1건당 타임아웃 (hang 응답자로 인한 무한대기 방지)

    private Process jarvisProcess;  // 실행된 서버 프로세스를 저장할 변수
    private int llmProcessPid = -1;  // LLM 서버 프로세스 PID
    private Coroutine healthCheckCoroutine;  // 중복 헬스체크 코루틴 방지용 핸들

    private void Awake()
    {
        UnityEngine.Debug.Log("[Jarvis] JarvisServerManager initialized");
        // 자동 시작 제거 - InstallStatusManager에서 수동으로 호출
    }

    // InstallStatusManager에서 호출할 초기화 함수
    public void InitializeForLiteOrFull()
    {
        UnityEngine.Debug.Log("[Jarvis] InitializeForLiteOrFull() called");
        if (SettingManager.Instance.settings.isStartServerOnInit)
        {
            RunJarvisServerWithCheck();
        }
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
            // (하드스톱하지 않는 이유: 포트는 기존/외부 서버가 정상 서빙 중일 수 있다 — 이중 기동 레이스 등)
            if (HasOwnedProcessExited())
            {
                jarvisProcess = null;
                StatusManager.Instance.IsServerConnected = false;
                primaryWindowExpired = true;
                UnityEngine.Debug.LogWarning("[Jarvis] 서버 프로세스가 기동 직후 종료됨 - 저빈도 재확인으로 전환");
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
                        UnityEngine.Debug.Log("[Jarvis] 서버 연결 성공 - StatusManager 업데이트" + identityNote);
                        // C01("서버 준비") 발화는 기동 직후 성공에만 — 한참 뒤의 지연 성공이
                        // 진행 중인 다른 시나리오/선택지 위에 나레이션을 덮지 않게 한다.
                        if (!primaryWindowExpired)
                        {
                            StartCoroutine(ScenarioCommonManager.Instance.Run_C01_ServerStarted());
                        }
                        healthCheckCoroutine = null;
                        yield break;
                    }
                    // :5000 응답자가 우리 서버가 아님 (다른 앱이 포트 점유) - 연결 처리하지 않고 재확인 지속
                    UnityEngine.Debug.LogWarning("[Jarvis] :5000 응답자가 예상한 서버가 아님" + identityNote + " - 재확인 계속");
                }
            }

            float elapsed = Time.realtimeSinceStartup - startTime;
            if (!primaryWindowExpired && elapsed >= HealthPrimaryWindowSeconds)
            {
                primaryWindowExpired = true;
                StatusManager.Instance.IsServerConnected = false;
                UnityEngine.Debug.LogWarning($"[Jarvis] {HealthPrimaryWindowSeconds:F0}초 내 서버 응답 없음 - C01 보류, {HealthRecheckIntervalSeconds:F0}초 간격 재확인으로 전환");
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

    // /health 응답 본문으로 응답자의 정체를 확인한다.
    // - "app" 필드가 기대값과 일치 → 우리 서버
    // - "app" 필드가 있는데 다른 값 → 다른 서버가 포트 점유 중 → 거부
    // - 필드 없음/JSON 아님 → app 필드 도입 전 구버전 서버로 간주해 허용 (로그로만 표시)
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
        UnityEngine.Debug.Log("[Jarvis] IsJarvisServerRunning() called");
        bool result = Process.GetProcessesByName(ServerProcessName).Length > 0;
        UnityEngine.Debug.Log("[Jarvis] IsJarvisServerRunning() result: " + result);
        return result;
    }

    public void RunJarvisServer()
    {
        UnityEngine.Debug.Log("[Jarvis] RunJarvisServer() start");

        // 기존에 켜져있는거 있는지 확인
        if (IsJarvisServerRunning())
        {
            // 이전 기동분 핸들이 이미 죽어 있으면 폐기 — 살아있는 외부/신규 서버를
            // 죽은 핸들(HasOwnedProcessExited) 때문에 실패로 오판하지 않게 한다.
            // (살아있는 소유 핸들은 ShutdownServer 를 위해 유지)
            if (HasOwnedProcessExited())
            {
                jarvisProcess = null;
            }
            // AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleInf();
            // AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText("Already Served");
            UnityEngine.Debug.Log("[Jarvis] Launch aborted: already running");
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
                    UnityEngine.Debug.LogWarning("[Jarvis] Process.Start 가 프로세스를 반환하지 않음: " + jarvisServerPath);
                }
            }
            catch (System.Exception e)
            {
                jarvisProcess = null;
                UnityEngine.Debug.LogError($"[Jarvis] 서버 실행 실패 (보안 프로그램 차단 가능성): {jarvisServerPath} - {e.Message}");
            }
            // AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleInf();
            // AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText("Init server...");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[Jarvis] Executable not found: " + jarvisServerPath);
        }

        UnityEngine.Debug.Log("[Jarvis] RunJarvisServer() end");
    }

    public Process RunJarvisServerProcess(string exePath)
    {
        UnityEngine.Debug.Log("[Jarvis] RunJarvisServerProcess() start: " + exePath);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--is_unity",
            UseShellExecute = true,
            CreateNoWindow = false
        };

        Process process = Process.Start(startInfo);

        UnityEngine.Debug.Log("[Jarvis] RunJarvisServerProcess() end");
        return process;
    }

    private IEnumerator CheckHealth()
    {
        UnityEngine.Debug.Log("[Jarvis] CheckHealth() start");

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
                    UnityEngine.Debug.Log("[Jarvis] Health check OK");
                    AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText("Server OK");
                    yield break;
                }
            }

            yield return new WaitForSeconds(0.5f);
            timer += 0.5f;
        }

        UnityEngine.Debug.LogWarning("[Jarvis] Health check failed");
        AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText("Server Fail");

        UnityEngine.Debug.Log("[Jarvis] CheckHealth() end");
    }

    // 서버 로드 후 받아온 LLM PID 정보를 저장
    public void SetProcessInfo(int llm_process_pid)
    {
        llmProcessPid = llm_process_pid;
        UnityEngine.Debug.Log($"[Jarvis] Process info stored - LLM Process PID: {llmProcessPid}");
    }

    // PID로 프로세스를 종료하는 헬퍼 메서드
    private void KillProcessByPid(int pid, string processName)
    {
        if (pid <= 0) return;

        try
        {
            Process process = Process.GetProcessById(pid);
            if (process != null && !process.HasExited)
            {
                process.Kill();
                UnityEngine.Debug.Log($"[Jarvis] {processName} process (PID: {pid}) killed successfully");
            }
        }
        catch (System.ArgumentException)
        {
            UnityEngine.Debug.Log($"[Jarvis] {processName} process (PID: {pid}) already terminated");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Jarvis] Failed to kill {processName} process (PID: {pid}): {e.Message}");
        }
    }

    // /shutdown 방식 vs Process 직접 종료에서 후자 선택
    public void ShutdownServer()
    {
        UnityEngine.Debug.Log("[Jarvis] ShutdownServer() start");

        // 1. jarvisProcess 종료
        if (jarvisProcess != null && !jarvisProcess.HasExited)
        {
            try
            {
                jarvisProcess.Kill();
                UnityEngine.Debug.Log("[Jarvis] Main server process killed");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Jarvis] Failed to kill main process: {e.Message}");
            }
        }
        else
        {
            UnityEngine.Debug.Log("[Jarvis] No running main server process to kill.");
        }

        // 2. LLM 프로세스 종료
        KillProcessByPid(llmProcessPid, "LLM");

        // 3. StatusManager 업데이트 및 PID 초기화
        StatusManager.Instance.IsServerConnected = false;
        llmProcessPid = -1;
        UnityEngine.Debug.Log("[Jarvis] All processes terminated - StatusManager 업데이트");

        UnityEngine.Debug.Log("[Jarvis] ShutdownServer() end");
    }

    private void OnApplicationQuit()
    {
        UnityEngine.Debug.Log("[Jarvis] OnApplicationQuit() -> ShutdownServer()");
        ShutdownServer();
    }
}
