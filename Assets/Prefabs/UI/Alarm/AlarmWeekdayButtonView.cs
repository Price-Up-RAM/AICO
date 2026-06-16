using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlarmWeekdayButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Color activeColor = new Color(0.35f, 0.8f, 0.45f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.52f, 0.54f, 0.58f, 1f);
    [SerializeField] private Color weekdayTextColor = Color.black;
    [SerializeField] private Color saturdayTextColor = new Color(0.1f, 0.25f, 1f, 1f);
    [SerializeField] private Color sundayTextColor = new Color(1f, 0.15f, 0.12f, 1f);

    private DayOfWeek dayOfWeek;
    private bool selected;
    private bool clickable = true;
    private Action<DayOfWeek> clickAction;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(Click);
        }
    }

    public void Setup(DayOfWeek day, string label, bool isSelected, bool isClickable, Action<DayOfWeek> onClick)
    {
        dayOfWeek = day;
        selected = isSelected;
        clickable = isClickable;
        clickAction = onClick;

        if (labelText != null)
        {
            labelText.text = label;
            labelText.color = GetTextColor(day);
        }

        RefreshVisual();
    }

    private void Click()
    {
        if (!clickable || clickAction == null)
        {
            return;
        }

        clickAction.Invoke(dayOfWeek);
    }

    private void RefreshVisual()
    {
        if (button != null)
        {
            button.interactable = clickable;
        }

        if (backgroundImage == null)
        {
            return;
        }

        if (selected && clickable)
        {
            backgroundImage.color = activeColor;
        }
        else
        {
            backgroundImage.color = inactiveColor;
        }
    }

    private Color GetTextColor(DayOfWeek day)
    {
        if (day == DayOfWeek.Saturday)
        {
            return saturdayTextColor;
        }

        if (day == DayOfWeek.Sunday)
        {
            return sundayTextColor;
        }

        return weekdayTextColor;
    }
}
