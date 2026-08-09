using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// 골드 표시 영역의 베이크된 안내 패널을 호버 동안만 표시한다.
// 대상 참조는 InventoryPanel.prefab에 직렬화되며 런타임 이름 탐색은 하지 않는다.
public sealed class InventoryGoldInfoHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipPanel;

    public void OnPointerEnter(PointerEventData eventData)
    {
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
}
