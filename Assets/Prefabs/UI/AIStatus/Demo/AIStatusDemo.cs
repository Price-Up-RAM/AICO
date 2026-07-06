using UnityEngine;

/// <summary>
/// AIStatus 데모. 씬 시작 시 패널을 열고, 서버가 없을 때도 UI 모양을 볼 수 있게 샘플 스냅샷을 주입한다.
/// 씬에 ServerManager가 없어도 AIStatusClient의 fallbackBaseUrl(데모 씬 빌더가 127.0.0.1:5000으로 설정)로
/// 실제 /status를 호출한다 → 서버가 떠 있으면 실데이터가 샘플을 덮어쓴다.
/// </summary>
public class AIStatusDemo : MonoBehaviour
{
    [SerializeField] private AIStatusView view;
    [SerializeField] private bool seedSampleSnapshot = true;
    [Tooltip("샘플 스냅샷을 full(벤치/fit 포함)로 채울지 여부")]
    [SerializeField] private bool full = false;

    private void Start()
    {
        if (view == null)
        {
            view = FindFirstObjectByType<AIStatusView>();
        }

        if (view == null)
        {
            Debug.LogWarning("[AIStatusDemo] AIStatusView를 찾지 못했습니다.");
            return;
        }

        // 샘플을 먼저 밀어넣는다(서버 응답이 오면 AIStatusClient가 덮어씀).
        if (seedSampleSnapshot)
        {
            view.SetStatus(BuildSample(full));
        }

        view.Show();
    }

    // 데모용 샘플 스냅샷: GPU 1개 + 시스템 + (full이면 벤치/fit).
    private static AIStatusData.AIStatusSnapshot BuildSample(bool full)
    {
        AIStatusData.AIStatusSnapshot s = new AIStatusData.AIStatusSnapshot();
        s.ok = true;
        s.level = full ? "full" : "lite";

        s.llm.running = true;
        s.llm.modelName = "Qwen3-8B-Q4_K_M.gguf";
        s.llm.health = "ok";
        s.llm.slotsTotal = 1;
        s.llm.slotsProcessing = 0;
        s.llm.nCtx = 32768;

        s.gpuAvailable = true;
        s.gpus.Add(new AIStatusData.GpuDevice
        {
            index = 0, name = "NVIDIA RTX (sample)", vramTotalGb = 24f, vramFreeGb = 18f,
            vramUsedMb = 6144f, utilPercent = 40f, tempC = 62f
        });

        s.system.available = true;
        s.system.ramTotalGb = 32f;
        s.system.ramAvailableGb = 18f;
        s.system.ramPercent = 44f;
        s.system.cpuLogical = 16;
        s.system.cpuPhysical = 8;
        s.system.cpuPercent = 22f;

        if (full)
        {
            s.benchmark.available = true;
            s.benchmark.nPredict = 32;
            s.benchmark.elapsedSec = 0.67f;
            s.benchmark.predictedPerSecond = 48f;
            s.benchmark.promptPerSecond = 520f;
            s.benchmark.predictedN = 32;

            s.fitModels = new[]
            {
                new AIStatusData.FitModel { model = "Qwen3-8B-Q4_K_M.gguf", needVramGb = 8f, maxNGpuLayers = 37, expectedGpuLayers = 37, verdict = "recommended" },
                new AIStatusData.FitModel { model = "Qwen3-14B-Q4_K_M.gguf", needVramGb = 12f, maxNGpuLayers = 41, expectedGpuLayers = 41, verdict = "recommended" },
                new AIStatusData.FitModel { model = "Qwen3-32B-Q4_K_M.gguf", needVramGb = 24f, maxNGpuLayers = 65, expectedGpuLayers = 65, verdict = "loadable_now" },
                new AIStatusData.FitModel { model = "Qwen3.6-35B-A3B-UD-Q4_K_M.gguf", needVramGb = 8f, maxNGpuLayers = 41, expectedGpuLayers = 41, isMoe = true, isMultimodal = true, verdict = "cpu_offload" },
            };
        }

        return s;
    }

#if UNITY_EDITOR
    public void EditorSet(AIStatusView statusView, bool isFull)
    {
        view = statusView;
        full = isFull;
    }
#endif
}
