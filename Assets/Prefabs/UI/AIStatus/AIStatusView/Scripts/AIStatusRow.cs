using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GPU 디바이스/모델 fit 카드 겸용 동적 리스트 항목. (MissionCardRow의 BindExisting+Setup 패턴)
// 계층은 AIStatusView가 굽고, 이 컴포넌트는 참조 바인딩 + 데이터 렌더만 담당한다.
public class AIStatusRow : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image badgeBg;
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private RectTransform gaugeFill;
    [SerializeField] private TMP_Text line1Text;
    [SerializeField] private TMP_Text line2Text;

    private bool bound;

    public void BindExisting()
    {
        if (bound)
        {
            return;
        }

        // 인스펙터 등록(베이크 시 직렬화됨) 우선, 비면 이름으로 탐색(fallback).
        titleText = titleText != null ? titleText : AIStatusUi.FindComponent<TMP_Text>(transform, "Title");

        Transform badgeT = AIStatusUi.FindDeepChild(transform, "Badge");
        if (badgeT != null)
        {
            badgeBg = badgeBg != null ? badgeBg : badgeT.GetComponent<Image>();
            badgeText = badgeText != null ? badgeText : AIStatusUi.FindComponent<TMP_Text>(badgeT, "BadgeText");
        }

        gaugeFill = gaugeFill != null ? gaugeFill : AIStatusUi.FindComponent<RectTransform>(transform, "Fill");
        line1Text = line1Text != null ? line1Text : AIStatusUi.FindComponent<TMP_Text>(transform, "Line1");
        line2Text = line2Text != null ? line2Text : AIStatusUi.FindComponent<TMP_Text>(transform, "Line2");
        bound = true;
    }

    // GPU 디바이스 1개를 렌더. 제목=이름, 배지=온도(색), 게이지=사용률, 라인=VRAM/사용량.
    public void SetupGpu(AIStatusData.GpuDevice d)
    {
        BindExisting();
        if (d == null)
        {
            return;
        }

        if (titleText != null) titleText.text = string.IsNullOrEmpty(d.name) ? ("GPU " + d.index) : d.name;
        if (badgeText != null) badgeText.text = Mathf.RoundToInt(d.tempC) + "°C";
        if (badgeBg != null) AIStatusUi.ApplyRounded(badgeBg, TempColor(d.tempC));
        AIStatusUi.SetGauge(gaugeFill, d.utilPercent / 100f);
        if (line1Text != null) line1Text.text = string.Format("VRAM {0:0.0} / {1:0.0} GB free", d.vramFreeGb, d.vramTotalGb);
        if (line2Text != null) line2Text.text = string.Format("Util {0:0}%   Used {1:0} MB", d.utilPercent, d.vramUsedMb);
    }

    // fit 모델 1개를 렌더. 제목=모델명, 배지=verdict(색), 게이지=예상 GPU 레이어 비율, 라인=필요 VRAM/레이어/플래그.
    public void SetupFit(AIStatusData.FitModel m)
    {
        BindExisting();
        if (m == null)
        {
            return;
        }

        if (titleText != null) titleText.text = m.model;
        if (badgeText != null) badgeText.text = VerdictLabel(m.verdict);
        if (badgeBg != null) AIStatusUi.ApplyRounded(badgeBg, VerdictColor(m.verdict));

        float ratio = m.maxNGpuLayers > 0 ? (float)m.expectedGpuLayers / m.maxNGpuLayers : 0f;
        AIStatusUi.SetGauge(gaugeFill, ratio);

        if (line1Text != null)
        {
            line1Text.text = string.Format("Need {0:0} GB   Layers {1}/{2}", m.needVramGb, m.expectedGpuLayers, m.maxNGpuLayers);
        }

        if (line2Text != null)
        {
            string flags = (m.isMoe ? "MoE " : "") + (m.isMultimodal ? "VL" : "");
            line2Text.text = string.IsNullOrEmpty(flags.Trim()) ? "text-only" : flags.Trim();
        }
    }

    // verdict → 배지 색
    private static Color VerdictColor(string verdict)
    {
        switch (verdict)
        {
            case "recommended": return AIStatusUi.StatusOk;
            case "loadable_now": return AIStatusUi.Accent;
            case "cpu_offload": return AIStatusUi.StatusWarn;
            case "too_large": return AIStatusUi.StatusBad;
            default: return AIStatusUi.StatusWarn;
        }
    }

    // verdict → 배지 라벨(짧게)
    private static string VerdictLabel(string verdict)
    {
        switch (verdict)
        {
            case "recommended": return "권장";
            case "loadable_now": return "가능";
            case "cpu_offload": return "CPU";
            case "too_large": return "초과";
            default: return string.IsNullOrEmpty(verdict) ? "-" : verdict;
        }
    }

    // 온도 → 색 (<70 Ok / <85 Warn / else Bad)
    private static Color TempColor(float c)
    {
        if (c < 70f) return AIStatusUi.StatusOk;
        if (c < 85f) return AIStatusUi.StatusWarn;
        return AIStatusUi.StatusBad;
    }
}
