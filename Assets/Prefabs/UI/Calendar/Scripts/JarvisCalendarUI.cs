using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JarvisCalendarUI : MonoBehaviour
{
    public event Action<DateTime> DateSelected;

    private DateTime visibleMonth = DateTime.Now.Date;
    private Text monthText;
    private Transform daysRoot;
    private Button previousButton;
    private Button nextButton;
    private Button closeButton;
    private Toggle calendarToggle;
    private Transform calendarPicker;
    private Transform calendarMonthHeader;
    private Transform calendarWeekDisplays;
    private Transform buttonsDaysParent;
    private Image pickerImage;
    private RectTransform rootRect;
    private RectTransform pickerRect;
    private Vector2 expandedSize;
    private readonly List<JarvisCalendarDayButton> dayButtons = new List<JarvisCalendarDayButton>();
    private bool isBound;
    private bool isExpanded = true;

    private void Awake()
    {
        EnsureStore();
        BindExistingPrefab();
    }

    private void OnEnable()
    {
        EnsureStore();
        BindExistingPrefab();
        if (JarvisTodoStore.Instance != null)
        {
            JarvisTodoStore.Instance.Changed += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (JarvisTodoStore.Instance != null)
        {
            JarvisTodoStore.Instance.Changed -= Refresh;
        }
    }

    public void ShowToday()
    {
        visibleMonth = DateTime.Now.Date;
        gameObject.SetActive(true);
        SetExpanded(true);
        Refresh();
    }

    public void Refresh()
    {
        BindExistingPrefab();

        if (monthText != null)
        {
            monthText.text = visibleMonth.ToString("yyyy-MM");
        }

        EnsureDayButtons();

        Dictionary<string, int> counts = JarvisTodoStore.Instance != null
            ? JarvisTodoStore.Instance.GetCountsByDate(visibleMonth.Year, visibleMonth.Month)
            : new Dictionary<string, int>();

        DateTime firstDay = new DateTime(visibleMonth.Year, visibleMonth.Month, 1);
        DateTime gridStart = firstDay.AddDays(-(int)firstDay.DayOfWeek);
        for (int i = 0; i < dayButtons.Count; i++)
        {
            DateTime day = gridStart.AddDays(i);
            counts.TryGetValue(day.ToString("yyyy-MM-dd"), out int count);
            dayButtons[i].Bind(
                day,
                day.Month == visibleMonth.Month,
                day == DateTime.Now.Date,
                count,
                OnDayClicked);
        }
    }

    private void BindExistingPrefab()
    {
        if (isBound)
        {
            return;
        }

        isBound = true;
        gameObject.name = "Calendar";
        rootRect = transform as RectTransform;

        calendarPicker = FindDeepChild(transform, "CalendarPicker");
        calendarMonthHeader = FindDeepChild(transform, "CalendarMonthHeader");
        calendarWeekDisplays = FindDeepChild(transform, "CalendarWeekDisplays");
        buttonsDaysParent = FindDeepChild(transform, "ButtonsDaysParent");
        Transform toggle = FindDeepChild(transform, "BtnCalendarToggle");
        Transform close = FindDeepChild(transform, "BtnCalendarClose");

        if (calendarPicker != null)
        {
            calendarPicker.gameObject.SetActive(true);
            pickerImage = calendarPicker.GetComponent<Image>();
            pickerRect = calendarPicker as RectTransform;
            if (pickerRect != null && rootRect != null)
            {
                expandedSize = pickerRect.sizeDelta;
                pickerRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = pickerRect.sizeDelta;
                rootRect.pivot = pickerRect.pivot;
            }
        }

        if (toggle != null)
        {
            toggle.gameObject.SetActive(true);
            calendarToggle = toggle.GetComponent<Toggle>();
            if (calendarToggle != null)
            {
                calendarToggle.onValueChanged.RemoveAllListeners();
                calendarToggle.onValueChanged.AddListener(SetExpanded);
                calendarToggle.SetIsOnWithoutNotify(isExpanded);
            }

            EnsureToggleDragHandler(toggle);
        }

        if (close != null)
        {
            close.gameObject.SetActive(true);
            closeButton = close.GetComponent<Button>();
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        EnsureDragHandler(calendarPicker);

        daysRoot = buttonsDaysParent;
        monthText = calendarMonthHeader?.GetComponentInChildren<Text>(true);
        previousButton = FindDeepChild(transform, "BtnPreviousMonth")?.GetComponent<Button>();
        nextButton = FindDeepChild(transform, "BtnNextMonth")?.GetComponent<Button>();

        if (previousButton != null)
        {
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(() =>
            {
                visibleMonth = visibleMonth.AddMonths(-1);
                Refresh();
            });
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() =>
            {
                visibleMonth = visibleMonth.AddMonths(1);
                Refresh();
            });
        }

        SetExpanded(isExpanded);
    }

    private void Close()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseCalendar();
            return;
        }

        gameObject.SetActive(false);
    }

    private void EnsureDayButtons()
    {
        if (daysRoot == null)
        {
            return;
        }

        for (int i = dayButtons.Count - 1; i >= 0; i--)
        {
            if (dayButtons[i] == null)
            {
                dayButtons.RemoveAt(i);
            }
        }

        while (dayButtons.Count < 42)
        {
            GameObject dayObject = new GameObject("Day", typeof(RectTransform));
            dayObject.transform.SetParent(daysRoot, false);
            JarvisCalendarDayButton dayButton = dayObject.AddComponent<JarvisCalendarDayButton>();
            dayButton.Build();
            dayButtons.Add(dayButton);
        }
    }

    private void OnDayClicked(DateTime date)
    {
        DateSelected?.Invoke(date);
        if (DateSelected == null && UIManager.Instance != null)
        {
            UIManager.Instance.OnCalendarDateSelected(date);
        }
    }

    private void SetExpanded(bool expanded)
    {
        isExpanded = expanded;

        if (calendarPicker != null)
        {
            calendarPicker.gameObject.SetActive(true);
        }

        if (pickerImage != null)
        {
            pickerImage.enabled = expanded;
        }

        if (calendarMonthHeader != null)
        {
            calendarMonthHeader.gameObject.SetActive(expanded);
        }

        if (calendarWeekDisplays != null)
        {
            calendarWeekDisplays.gameObject.SetActive(expanded);
        }

        if (buttonsDaysParent != null)
        {
            buttonsDaysParent.gameObject.SetActive(expanded);
        }

        Vector2 targetSize = expanded ? expandedSize : Vector2.zero;
        if (targetSize == Vector2.zero && pickerRect != null)
        {
            RectTransform toggleRect = calendarToggle != null ? calendarToggle.GetComponent<RectTransform>() : null;
            targetSize = expanded || toggleRect == null ? pickerRect.sizeDelta : toggleRect.sizeDelta;
        }

        if (pickerRect != null)
        {
            pickerRect.sizeDelta = targetSize;
        }

        if (rootRect != null)
        {
            rootRect.sizeDelta = targetSize;
        }

        if (calendarToggle != null)
        {
            calendarToggle.gameObject.SetActive(true);
            calendarToggle.SetIsOnWithoutNotify(expanded);
        }

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(expanded);
        }
    }

    private void EnsureDragHandler(Transform handle)
    {
        RectTransform targetRect = transform as RectTransform;
        if (handle == null || targetRect == null)
        {
            return;
        }

        DragUIHandler dragHandler = handle.GetComponent<DragUIHandler>();
        if (dragHandler == null)
        {
            handle.gameObject.AddComponent<DragUIHandler>();
        }
    }

    private void EnsureToggleDragHandler(Transform toggle)
    {
        RectTransform targetRect = transform as RectTransform;
        if (toggle == null || targetRect == null)
        {
            return;
        }

        UIDragHandler oldUiDragHandler = toggle.GetComponent<UIDragHandler>();
        if (oldUiDragHandler != null)
        {
            Destroy(oldUiDragHandler);
        }

        DragUIHandler oldDragHandler = toggle.GetComponent<DragUIHandler>();
        if (oldDragHandler != null)
        {
            Destroy(oldDragHandler);
        }

        JarvisCalendarToggleDragHandler dragHandler = toggle.GetComponent<JarvisCalendarToggleDragHandler>();
        if (dragHandler == null)
        {
            dragHandler = toggle.gameObject.AddComponent<JarvisCalendarToggleDragHandler>();
        }

        dragHandler.SetTarget(targetRect);
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void EnsureStore()
    {
        if (JarvisTodoStore.Instance != null)
        {
            return;
        }

        GameObject storeObject = new GameObject("JarvisTodoStore");
        storeObject.AddComponent<JarvisTodoStore>();
        DontDestroyOnLoad(storeObject);
    }
}
