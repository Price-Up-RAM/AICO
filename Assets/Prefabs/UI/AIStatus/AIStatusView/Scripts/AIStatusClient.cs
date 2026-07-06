using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// AIStatusView의 데이터 공급/모델 제어 담당(SkillCatalogClient 계보).
///  - 현황: ServerManager.GetBaseUrl → /status(lite) 또는 /status/full → Newtonsoft 파싱 → view.SetStatus
///  - 리로드: view.ReloadModelRequested(gguf) → POST /model/load(conversation과 동일 전환) → 현황 갱신
///  - 토큰테스트: view.TokenTestRequested → GET /status/full → tokens/sec 표시
/// server_local_mode는 SettingManager 참조(없으면 GPU). ServerManager가 없으면 fallbackBaseUrl로 직접 호출.
/// </summary>
[RequireComponent(typeof(AIStatusView))]
public class AIStatusClient : MonoBehaviour
{
    [SerializeField] private AIStatusView view;
    [SerializeField] private bool full = false;                 // View.ModeChanged로 갱신
    [SerializeField] private float pollIntervalSec = 0f;        // 0이면 폴링 안 함
    [SerializeField] private string fallbackBaseUrl = "";       // ServerManager 없을 때 직접 호출(데모용)
    [SerializeField] private bool showOfflineOnFailure = true;  // 실패 시 '미연결' 표시(끄면 현재 화면 유지)

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<AIStatusView>();
        }
    }

    private void OnEnable()
    {
        if (view == null)
        {
            return;
        }

        view.RefreshRequested += Reload;
        view.ModeChanged += OnModeChanged;
        view.ReloadModelRequested += OnReloadModel;
        view.TokenTestRequested += OnTokenTest;
        Reload();
        if (pollIntervalSec > 0f)
        {
            StartCoroutine(PollLoop());
        }
    }

    private void OnDisable()
    {
        if (view == null)
        {
            return;
        }

        view.RefreshRequested -= Reload;
        view.ModeChanged -= OnModeChanged;
        view.ReloadModelRequested -= OnReloadModel;
        view.TokenTestRequested -= OnTokenTest;
        StopAllCoroutines();
    }

    private void OnModeChanged(bool isFull)
    {
        full = isFull;
        Reload();
    }

    // ── baseUrl 해석 ──────────────────────────────────────────────────────────
    // ServerManager 있으면 GetBaseUrl 콜백, 없으면 fallbackBaseUrl. 콜백 인자가 비면 fallback으로 대체.
    private void WithBaseUrl(System.Action<string> callback)
    {
        ServerManager sm = FindObjectOfType<ServerManager>();
        if (sm == null)
        {
            callback(fallbackBaseUrl);
            return;
        }

        sm.GetBaseUrl(baseUrl =>
        {
            callback(string.IsNullOrEmpty(baseUrl) ? fallbackBaseUrl : baseUrl);
        });
    }

    // ── 현황 조회 ─────────────────────────────────────────────────────────────
    public void Reload()
    {
        WithBaseUrl(baseUrl =>
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                if (showOfflineOnFailure)
                {
                    view.SetStatus(OfflineSnapshot());
                }

                return;
            }

            StartCoroutine(GetStatusCoroutine(baseUrl));
        });
    }

    private IEnumerator GetStatusCoroutine(string baseUrl)
    {
        string path = full ? "/status/full" : "/status";
        string url = baseUrl.TrimEnd('/') + path;
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AIStatusClient] {path} 실패({req.responseCode}): {req.error}");
                if (showOfflineOnFailure)
                {
                    view.SetStatus(OfflineSnapshot());
                }

                yield break;
            }

            view.SetStatus(Parse(req.downloadHandler.text));
        }
    }

    private IEnumerator PollLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollIntervalSec);
            Reload();
        }
    }

    // ── 리로드 (드롭다운 선택 모델 로드 = conversation과 동일한 /model/load) ──────
    private void OnReloadModel(string fileName)
    {
        WithBaseUrl(baseUrl =>
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                view.SetModelResult("서버 미연결");
                return;
            }

            StartCoroutine(ReloadCoroutine(baseUrl, fileName));
        });
    }

    private IEnumerator ReloadCoroutine(string baseUrl, string fileName)
    {
        string loadUrl = baseUrl.TrimEnd('/') + "/model/load";
        JObject body = new JObject
        {
            ["model_name_Local"] = fileName,
            ["server_local_mode"] = ServerLocalMode(),
        };
        byte[] raw = Encoding.UTF8.GetBytes(body.ToString());

        using (UnityWebRequest req = new UnityWebRequest(loadUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 300; // 모델 로딩이 오래 걸릴 수 있음
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AIStatusClient] /model/load 실패({req.responseCode}): {req.error}");
                view.SetModelResult("로드 실패: " + fileName);
                yield break;
            }
        }

        view.SetModelResult("로드 완료: " + fileName);

        // 로드 후 현황 갱신(현재 모드 기준)
        string path = full ? "/status/full" : "/status";
        using (UnityWebRequest req2 = UnityWebRequest.Get(baseUrl.TrimEnd('/') + path))
        {
            req2.timeout = 180;
            yield return req2.SendWebRequest();
            if (req2.result == UnityWebRequest.Result.Success)
            {
                view.SetStatus(Parse(req2.downloadHandler.text));
            }
        }
    }

    // ── 토큰 테스트 (현재 로드된 모델 tokens/sec) ──────────────────────────────
    private void OnTokenTest()
    {
        WithBaseUrl(baseUrl =>
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                view.SetModelResult("서버 미연결");
                return;
            }

            StartCoroutine(TokenTestCoroutine(baseUrl));
        });
    }

    private IEnumerator TokenTestCoroutine(string baseUrl)
    {
        string url = baseUrl.TrimEnd('/') + "/status/full"; // full이 벤치(tokens/sec)를 포함
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 180;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AIStatusClient] /status/full 실패({req.responseCode}): {req.error}");
                view.SetModelResult("테스트 실패");
                yield break;
            }

            AIStatusData.AIStatusSnapshot snap = Parse(req.downloadHandler.text);
            view.SetStatus(snap);
            float tok = (snap.benchmark != null && snap.benchmark.available) ? snap.benchmark.predictedPerSecond : 0f;
            string model = (snap.llm != null && !string.IsNullOrEmpty(snap.llm.modelName)) ? snap.llm.modelName : "";
            view.SetModelResult(tok > 0f
                ? string.Format("{0}: {1:0.0} tok/s", model, tok)
                : "측정값 없음(서버 미로드?)");
        }
    }

    // server_local_mode: SettingManager 참조(없으면 GPU).
    private static string ServerLocalMode()
    {
        if (SettingManager.Instance != null && SettingManager.Instance.settings != null &&
            !string.IsNullOrEmpty(SettingManager.Instance.settings.server_local_mode))
        {
            return SettingManager.Instance.settings.server_local_mode;
        }

        return "GPU";
    }

    private static AIStatusData.AIStatusSnapshot OfflineSnapshot()
    {
        AIStatusData.AIStatusSnapshot s = new AIStatusData.AIStatusSnapshot();
        s.ok = false;
        s.level = "lite";
        s.llm.running = false;
        return s;
    }

    // /status(/full) JSON → AIStatusSnapshot. 실패/누락은 기본값으로 방어.
    private static AIStatusData.AIStatusSnapshot Parse(string json)
    {
        AIStatusData.AIStatusSnapshot s = new AIStatusData.AIStatusSnapshot();
        if (string.IsNullOrEmpty(json))
        {
            return s;
        }

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AIStatusClient] 파싱 실패: {e.Message}");
            return s;
        }

        s.ok = (bool?)root["ok"] ?? false;
        s.level = (string)root["level"] ?? "lite";

        JObject llm = root["llm_server"] as JObject;
        if (llm != null)
        {
            s.llm.running = (bool?)llm["running"] ?? false;
            s.llm.modelName = (string)llm["model_name"];
            s.llm.health = (string)llm["health"];

            JObject props = llm["props"] as JObject;
            if (props != null)
            {
                s.llm.modelPath = (string)props["model_path"];
                s.llm.totalSlots = (int?)props["total_slots"] ?? 0;
                s.llm.isSleeping = (bool?)props["is_sleeping"] ?? false;
                s.llm.buildInfo = (string)props["build_info"];
                s.llm.nCtx = (int?)props["n_ctx"] ?? 0;
            }

            JObject slots = llm["slots"] as JObject;
            if (slots != null)
            {
                s.llm.slotsTotal = (int?)slots["total"] ?? 0;
                s.llm.slotsProcessing = (int?)slots["processing"] ?? 0;
                s.llm.slotsIdle = (int?)slots["idle"] ?? 0;
                JArray det = slots["detail"] as JArray;
                if (det != null)
                {
                    foreach (JToken t in det)
                    {
                        s.llm.slots.Add(new AIStatusData.SlotDetail
                        {
                            id = (int?)t["id"] ?? 0,
                            isProcessing = (bool?)t["is_processing"] ?? false,
                            nCtx = (int?)t["n_ctx"] ?? 0,
                        });
                    }
                }
            }

            JObject meta = llm["model_meta"] as JObject;
            if (meta != null)
            {
                s.llm.sizeGb = (float?)meta["size_gb"] ?? 0f;
                s.llm.nParams = (long?)meta["n_params"] ?? 0L;
                s.llm.nVocab = (int?)meta["n_vocab"] ?? 0;
                s.llm.nEmbd = (int?)meta["n_embd"] ?? 0;
            }
        }

        JObject gpu = root["gpu"] as JObject;
        if (gpu != null)
        {
            s.gpuAvailable = (bool?)gpu["available"] ?? false;
            JArray devs = gpu["devices"] as JArray;
            if (devs != null)
            {
                foreach (JToken d in devs)
                {
                    s.gpus.Add(new AIStatusData.GpuDevice
                    {
                        index = (int?)d["index"] ?? 0,
                        name = (string)d["name"],
                        vramTotalGb = (float?)d["vram_total_gb"] ?? 0f,
                        vramFreeGb = (float?)d["vram_free_gb"] ?? 0f,
                        vramUsedMb = (float?)d["vram_used_mb"] ?? 0f,
                        utilPercent = (float?)d["util_percent"] ?? 0f,
                        tempC = (float?)d["temp_c"] ?? 0f,
                    });
                }
            }
        }

        JObject sys = root["system"] as JObject;
        if (sys != null)
        {
            s.system.available = (bool?)sys["available"] ?? false;
            s.system.ramTotalGb = (float?)sys["ram_total_gb"] ?? 0f;
            s.system.ramAvailableGb = (float?)sys["ram_available_gb"] ?? 0f;
            s.system.ramPercent = (float?)sys["ram_percent"] ?? 0f;
            s.system.cpuLogical = (int?)sys["cpu_logical"] ?? 0;
            s.system.cpuPhysical = (int?)sys["cpu_physical"] ?? 0;
            s.system.cpuPercent = (float?)sys["cpu_percent"] ?? 0f;
        }

        JObject bench = root["benchmark"] as JObject;
        if (bench != null)
        {
            s.benchmark.available = (bool?)bench["available"] ?? false;
            s.benchmark.nPredict = (int?)bench["n_predict"] ?? 0;
            s.benchmark.elapsedSec = (float?)bench["elapsed_sec"] ?? 0f;
            s.benchmark.predictedPerSecond = (float?)bench["predicted_per_second"] ?? 0f;
            s.benchmark.promptPerSecond = (float?)bench["prompt_per_second"] ?? 0f;
            s.benchmark.predictedN = (int?)bench["predicted_n"] ?? 0;
            s.benchmark.promptN = (int?)bench["prompt_n"] ?? 0;
        }

        JObject fit = root["fit"] as JObject;
        if (fit != null)
        {
            JArray models = fit["models"] as JArray;
            if (models != null)
            {
                List<AIStatusData.FitModel> list = new List<AIStatusData.FitModel>();
                foreach (JToken m in models)
                {
                    list.Add(new AIStatusData.FitModel
                    {
                        model = (string)m["model"],
                        needVramGb = (float?)m["need_vram_gb"] ?? 0f,
                        maxNGpuLayers = (int?)m["max_n_gpu_layers"] ?? 0,
                        expectedGpuLayers = (int?)m["expected_gpu_layers"] ?? 0,
                        isMoe = (bool?)m["is_moe"] ?? false,
                        isMultimodal = (bool?)m["is_multimodal"] ?? false,
                        fitsGpu = (bool?)m["fits_gpu"] ?? false,
                        fitsFreeNow = (bool?)m["fits_free_now"] ?? false,
                        verdict = (string)m["verdict"],
                    });
                }

                s.fitModels = list.ToArray();
            }
        }

        return s;
    }

#if UNITY_EDITOR
    // 데모 씬 빌더가 인스턴스에 직접 주입(직접 호출 주소 + 실패 시 유지).
    public void EditorConfigure(string fallbackUrl, bool offlineOnFailure)
    {
        fallbackBaseUrl = fallbackUrl;
        showOfflineOnFailure = offlineOnFailure;
    }
#endif
}
