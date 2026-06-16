using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JarvisCalendarDayButton : MonoBehaviour
{
    private DateTime date;
    private Button button;
    private Image background;
    private TextMeshProUGUI label;
    private TextMeshProUGUI dot;
    private Action<DateTime> onClicked;

    public void Build()
    {
        background = gameObject.AddComponent<Image>();
        background.color = Color.white;
        button = gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        label = CreateText("Day", transform, 9, TextAlignmentOptions.Center);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.25f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        dot = CreateText("Dot", transform, 8, TextAlignmentOptions.Center);
        RectTransform dotRect = dot.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0f, 0f);
        dotRect.anchorMax = new Vector2(1f, 0.32f);
        dotRect.offsetMin = Vector2.zero;
        dotRect.offsetMax = Vector2.zero;
        dot.color = new Color(0.58f, 0.28f, 1f, 1f);

        button.onClick.AddListener(() => onClicked?.Invoke(date));
    }

    public void Bind(DateTime day, bool isCurrentMonth, bool isToday, int itemCount, Action<DateTime> clickAction)
    {
        date = day.Date;
        onClicked = clickAction;

        label.text = day.Day.ToString();
        label.color = isCurrentMonth ? Color.black : new Color(0.55f, 0.55f, 0.55f, 1f);
        label.fontStyle = isToday ? FontStyles.Bold : FontStyles.Normal;
        dot.text = itemCount > 0 ? itemCount.ToString() : string.Empty;
        background.color = isToday ? new Color(1f, 0.91f, 0.58f, 1f) : Color.white;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI result = obj.AddComponent<TextMeshProUGUI>();
        result.fontSize = fontSize;
        result.alignment = alignment;
        result.color = Color.black;
        result.enableWordWrapping = false;
        return result;
    }
}
