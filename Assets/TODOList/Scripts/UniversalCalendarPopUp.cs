using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UniversalCalendarPopUp : MonoBehaviour
{
    private const int NUMBER_CALENDAR_DAYS = 42;

    public static UniversalCalendarPopUp Instance = null;

    [Header("달력 구성")]
    public GameObject btnPerDayToInstantiate;
    public Transform buttonParents;
    public GameObject calendarPicker;

    [Header("Calendar Picker Assets")]
    public Text txtMonthAndYear;
    public Button btnNextMonth;
    public Button btnPreviousMonth;

    private DateTime m_targetDay = DateTime.Now;
    private List<UniversalCalendarDay> m_btnDays = new List<UniversalCalendarDay>();
    private Action<DateTime> m_clientAction;
    private bool _isInitialized;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        btnNextMonth.onClick.AddListener(DisplayNextMonth);
        btnPreviousMonth.onClick.AddListener(DisplayPreviousMonth);
        UniversalCalendarDay.onDayPressed += OnDayPressed;

        calendarPicker.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        btnNextMonth.onClick.RemoveListener(DisplayNextMonth);
        btnPreviousMonth.onClick.RemoveListener(DisplayPreviousMonth);
        UniversalCalendarDay.onDayPressed -= OnDayPressed;
    }

    public void ShowCalendarPopUp(Action<DateTime> p_clientAction)
    {
        m_clientAction = p_clientAction;
        calendarPicker.SetActive(true);

        if (!_isInitialized)
        {
            _isInitialized = true;
            SpawnDaysItem();
        }

        m_targetDay = DateTime.Now;
        UpdateMonthAndYearDisplay();
        InitializeCalendarDisplay(m_targetDay.Year, m_targetDay.Month);
    }

    public void HideCalendar()
    {
        calendarPicker.SetActive(false);
    }

    public void RefreshDots()
    {
        if (_isInitialized)
            InitializeCalendarDisplay(m_targetDay.Year, m_targetDay.Month);
    }

    void InitializeCalendarDisplay(int p_year, int p_month)
    {
        List<CalendarTool.CalendarDisplayData> monthDate = CalendarTool.GetNumberDaysOfAMonth(p_year, p_month, NUMBER_CALENDAR_DAYS);
        for (int x = 0; x < monthDate.Count; ++x)
        {
            Color color = monthDate[x].isPartOfMonth ? new Color(0f, 0f, 0f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            m_btnDays[x].InitButton(monthDate[x].dateTime, monthDate[x].day.ToString(), monthDate[x].isPartOfMonth, color);
        }
    }

    void SpawnDaysItem()
    {
        for (int x = 0; x < NUMBER_CALENDAR_DAYS; ++x)
        {
            UniversalCalendarDay btnDays = Instantiate(btnPerDayToInstantiate).GetComponent<UniversalCalendarDay>();
            btnDays.transform.SetParent(buttonParents, false);
            m_btnDays.Add(btnDays);
        }
    }

    void UpdateMonthAndYearDisplay()
    {
        txtMonthAndYear.text = m_targetDay.ToShortMonthName() + " " + m_targetDay.Year;
    }

    void DisplayNextMonth()
    {
        m_targetDay = m_targetDay.AddMonths(1);
        UpdateMonthAndYearDisplay();
        InitializeCalendarDisplay(m_targetDay.Year, m_targetDay.Month);
    }

    void DisplayPreviousMonth()
    {
        m_targetDay = m_targetDay.AddMonths(-1);
        UpdateMonthAndYearDisplay();
        InitializeCalendarDisplay(m_targetDay.Year, m_targetDay.Month);
    }

    void OnDayPressed(DateTime p_dateTimeClicked)
    {
        m_clientAction?.Invoke(p_dateTimeClicked);
    }
}
