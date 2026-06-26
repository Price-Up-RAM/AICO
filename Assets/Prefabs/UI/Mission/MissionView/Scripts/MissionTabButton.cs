using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 좌측 카테고리 탭 버튼. 계층은 MissionView가 구성하고, 이 컴포넌트는 참조 바인딩 + 동작만.
public class MissionTabButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text countText;

    private MissionCategory category;
    private Action<MissionCategory> onClick;
    private bool bound;

    public void BindExisting()
    {
        if (bound)
        {
            return;
        }

        button = GetComponent<Button>();
        background = GetComponent<Image>();
        label = MissionUi.FindComponent<TMP_Text>(transform, "Label");
        countText = MissionUi.FindComponent<TMP_Text>(transform, "Count");

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        bound = true;
    }

    public void Setup(MissionCategory category, string labelText, string count, bool selected, Action<MissionCategory> onClick)
    {
        BindExisting();
        this.category = category;
        this.onClick = onClick;

        if (label != null)
        {
            label.text = labelText;
        }

        if (countText != null)
        {
            countText.text = count;
        }

        SetSelected(selected);
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
        {
            MissionUi.ApplyRounded(background, selected ? MissionUi.TabSelected : MissionUi.TabBg);
        }

        if (label != null)
        {
            label.color = selected ? MissionUi.TextWhite : MissionUi.TextMuted;
        }
    }

    private void HandleClick()
    {
        onClick?.Invoke(category);
    }
}
