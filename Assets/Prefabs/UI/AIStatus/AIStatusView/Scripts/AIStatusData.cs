using System;
using System.Collections.Generic;

// 서버 /status(lite) · /status/full 응답을 담는 직렬화 데이터 계약.
// AIStatusClient가 JSON을 파싱해 채우고, AIStatusView.SetStatus(AIStatusSnapshot)로 주입한다.
public static class AIStatusData
{
    [Serializable]
    public class GpuDevice
    {
        public int index;
        public string name;
        public float vramTotalGb;
        public float vramFreeGb;
        public float vramUsedMb;
        public float utilPercent;
        public float tempC;
    }

    [Serializable]
    public class SlotDetail
    {
        public int id;
        public bool isProcessing;
        public int nCtx;
    }

    [Serializable]
    public class LlmServer
    {
        public bool running;
        public string modelName;
        public string health;
        public string modelPath;
        public int totalSlots;
        public bool isSleeping;
        public string buildInfo;
        public int nCtx;
        public int slotsTotal;
        public int slotsProcessing;
        public int slotsIdle;
        public List<SlotDetail> slots = new List<SlotDetail>();
        public float sizeGb;
        public long nParams;
        public int nVocab;
        public int nEmbd;

        public bool HealthOk => string.Equals(health, "ok", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public class SystemInfo
    {
        public bool available;
        public float ramTotalGb;
        public float ramAvailableGb;
        public float ramPercent;
        public int cpuLogical;
        public int cpuPhysical;
        public float cpuPercent;
    }

    [Serializable]
    public class Benchmark
    {
        public bool available;
        public int nPredict;
        public float elapsedSec;
        public float predictedPerSecond;
        public float promptPerSecond;
        public int predictedN;
        public int promptN;
    }

    [Serializable]
    public class FitModel
    {
        public string model;
        public float needVramGb;
        public int maxNGpuLayers;
        public int expectedGpuLayers;
        public bool isMoe;
        public bool isMultimodal;
        public bool fitsGpu;
        public bool fitsFreeNow;
        public string verdict; // recommended | loadable_now | cpu_offload | too_large
    }

    [Serializable]
    public class AIStatusSnapshot
    {
        public bool ok;
        public string level = "lite";
        public LlmServer llm = new LlmServer();
        public bool gpuAvailable;
        public List<GpuDevice> gpus = new List<GpuDevice>();
        public SystemInfo system = new SystemInfo();
        public Benchmark benchmark = new Benchmark();
        public FitModel[] fitModels = new FitModel[0];

        public bool HasFull => string.Equals(level, "full", StringComparison.OrdinalIgnoreCase);
    }
}
