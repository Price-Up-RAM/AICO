using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// 골드 표시 영역의 베이크된 안내 패널을 호버 동안만 표시한다.
// 대상 참조는 InventoryPanel.prefab에 직렬화되며 런타임 이름 탐색은 하지 않는다.
public sealed class InventoryGoldInfoHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipPanel;

    // MR 진단 (2026-08-26) — hover 의미론은 MR에서 진동 루프가 될 수 있다 (Kickoff Guide §4-54).
    // 툴팁이 뜨면서 레이를 가로채면 곧바로 Exit이 오고, 닫히면 다시 Enter가 온다.
    // 실기 사례는 0.5초 주기였다. 추측으로 먼저 고치지 않고 **간격을 재서** 판정한다.
    // 0.8초 안에 Enter가 다시 오면 루프로 보고 경고를 올린다.
    private const float LoopSuspectSeconds = 0.8f;
    private float lastEnterTime = -999f;
    private int rapidEnterCount;

    public void OnPointerEnter(PointerEventData eventData)
    {
        LogHoverInterval();

        if (tooltipPanel != null)
        {
            foreach (TMP_Text target in tooltipPanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (target != null && !string.IsNullOrEmpty(target.text))
                {
                    target.text = LanguageDataInventory.Translate(target.text);
                }
            }

            tooltipPanel.transform.SetAsLastSibling();
            tooltipPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    private void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    // Enter 재진입 간격을 찍는다. 지금 값과 판정 기준을 한 줄에 남긴다 (§7-1 C).
    private void LogHoverInterval()
    {
        float now = Time.unscaledTime;
        float gap = now - lastEnterTime;
        lastEnterTime = now;

        if (gap > LoopSuspectSeconds)
        {
            rapidEnterCount = 0;
            return;
        }

        rapidEnterCount = rapidEnterCount + 1;

        // 사람이 손을 떨어도 두세 번은 난다. 4회 연속부터가 루프다.
        if (rapidEnterCount < 4)
        {
            Debug.Log($"[MRInv/hover] 골드 툴팁 재진입 간격 {gap:F2}초 (임계 {LoopSuspectSeconds}초) | 연속 {rapidEnterCount}회");
            return;
        }

        Debug.LogWarning($"[MRInv/hover] 골드 툴팁 진동 루프 의심 — 간격 {gap:F2}초로 {rapidEnterCount}회 연속 재진입 " +
                         $"(임계 {LoopSuspectSeconds}초) → §4-54 사례. hover를 클릭으로 바꾸거나 툴팁의 레이캐스트를 꺼야 한다");
    }
}
