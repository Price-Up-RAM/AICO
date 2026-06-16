using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AlarmManager))]
[RequireComponent(typeof(AlarmAudioPlayer))]
public class AlarmUI : MonoBehaviour
{
    [SerializeField] private AlarmManager alarmManager;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private AlarmListItemView listItemTemplate;
    [SerializeField] private TMP_Text emptyListText;
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private float listRowHeight = 66f;
    [SerializeField] private float listRowSpacing = 8f;
    [Header("Header")]
    [SerializeField] private Button addAlarmButton;
    [SerializeField] private Button addTimerButton;
    [SerializeField] private Button showMiniAlarmButton;

    [Header("Detail")]
    [SerializeField] private TMP_InputField titleInput;
    [SerializeField] private AlarmWheelPicker meridiemWheel;
    [SerializeField] private AlarmWheelPicker hourWheel;
    [SerializeField] private AlarmWheelPicker minuteWheel;
    [SerializeField] private AlarmWheelPicker secondWheel;
    [SerializeField] private GameObject hourMinuteColonObject;
    [SerializeField] private GameObject secondColonObject;
    [SerializeField] private Dropdown soundDropdown;
    [SerializeField] private Toggle noWeekendToggle;
    [SerializeField] private GameObject weekdayGroup;
    [SerializeField] private AlarmWeekdayButtonView[] weekdayButtons;
    [SerializeField] private GameObject timerPlayGroup;
    [SerializeField] private Button timerStartButton;
    [SerializeField] private Button timerResetButton;
    [SerializeField] private TMP_Text timerCountdownText;

    [Header("Mini Alarm")]
    [SerializeField] private AlarmMiniView miniAlarmPrefab;
    [SerializeField] private RectTransform miniAlarmRoot;
    private Vector2 miniAlarmSpawnOffset;

    private readonly List<AlarmListItemView> visibleRows = new List<AlarmListItemView>();
    private readonly Dictionary<string, AlarmMiniView> activeMiniAlarms = new Dictionary<string, AlarmMiniView>();
    private readonly DayOfWeek[] weekdays =
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    };

    private readonly string[] weekdayLabels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
    private AlarmItem selectedAlarm;
    private bool isRefreshing;
    private float listRefreshProgress;
    private readonly HashSet<string> positionedMiniAlarmIds = new HashSet<string>();

    public event Action<AlarmItem> SelectedAlarmChanged;

    private void Awake()
    {
        miniAlarmSpawnOffset = new Vector2(180f, 100f);

        if (alarmManager == null)
        {
            alarmManager = GetComponent<AlarmManager>();
        }

        ConfigureStaticControls();
        CacheOptionalTimeObjects();
        EnsureTimerPlayControls();
        EnsureMiniAlarmControls();
        BindListeners();
        HideListTemplate();
        DisableListLayoutGroup();
    }

    private void OnEnable()
    {
        if (alarmManager != null)
        {
            alarmManager.AlarmsChanged += RefreshAll;
            alarmManager.AlarmRang += OnAlarmRang;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (alarmManager != null)
        {
            alarmManager.AlarmsChanged -= RefreshAll;
            alarmManager.AlarmRang -= OnAlarmRang;
        }
    }

    private void Update()
    {
        if (alarmManager == null)
        {
            return;
        }

        listRefreshProgress += Time.unscaledDeltaTime;
        if (listRefreshProgress < 1f)
        {
            return;
        }

        listRefreshProgress = 0f;
        RefreshVisibleRows();
        RefreshTimerPlayControls();
        RefreshActiveMiniAlarms();
    }

    public void RefreshAll()
    {
        RefreshList();
        RefreshDetail();
        RefreshActiveMiniAlarms();
    }

    public AlarmItem GetSelectedAlarm()
    {
        return selectedAlarm;
    }

    public void RefreshRuntimeViews()
    {
        RefreshTimerRuntimeViews();
    }

    private void ConfigureStaticControls()
    {
        if (meridiemWheel != null)
        {
            meridiemWheel.ConfigureLabels(new[] { "24H", "AM", "PM" });
        }

        if (minuteWheel != null)
        {
            minuteWheel.ConfigureRange(0, 59, true);
        }

        if (secondWheel != null)
        {
            secondWheel.ConfigureRange(0, 59, true);
        }

        if (soundDropdown != null)
        {
            soundDropdown.ClearOptions();
            soundDropdown.AddOptions(new List<string> { AlarmSoundType.Character, AlarmSoundType.Music });
        }
    }

    private void BindListeners()
    {
        AddClick(addAlarmButton, AddDefaultDailyAlarm);
        AddClick(addTimerButton, AddDefaultRelativeTimer);
        AddClick(showMiniAlarmButton, OnShowMiniAlarmClicked);
        AddClick(timerStartButton, OnTimerStartPauseClicked);
        AddClick(timerResetButton, OnTimerResetClicked);

        if (titleInput != null)
        {
            titleInput.onValueChanged.RemoveListener(OnTitleChanged);
            titleInput.onValueChanged.AddListener(OnTitleChanged);
        }

        if (noWeekendToggle != null)
        {
            noWeekendToggle.onValueChanged.RemoveListener(OnNoWeekendChanged);
            noWeekendToggle.onValueChanged.AddListener(OnNoWeekendChanged);
        }

        if (soundDropdown != null)
        {
            soundDropdown.onValueChanged.RemoveListener(OnSoundDropdownChanged);
            soundDropdown.onValueChanged.AddListener(OnSoundDropdownChanged);
        }

        AddWheelListener(meridiemWheel, OnTimeWheelChanged);
        AddWheelListener(hourWheel, OnTimeWheelChanged);
        AddWheelListener(minuteWheel, OnTimeWheelChanged);
        AddWheelListener(secondWheel, OnTimeWheelChanged);
    }

    private void AddClick(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void AddWheelListener(AlarmWheelPicker wheel, Action<int> action)
    {
        if (wheel != null)
        {
            wheel.ValueChanged += action;
        }
    }

    private void HideListTemplate()
    {
        if (listItemTemplate == null)
        {
            return;
        }

        listItemTemplate.gameObject.SetActive(false);
    }

    private void RefreshList()
    {
        if (alarmManager == null || listContent == null || listItemTemplate == null)
        {
            SetEmptyListVisible(false);
            return;
        }

        List<AlarmItem> alarms = alarmManager.GetAlarms();
        SetEmptyListVisible(alarms.Count == 0);
        EnsureVisibleRowCount(alarms.Count);

        for (int i = 0; i < alarms.Count; i++)
        {
            AlarmItem alarm = alarms[i];
            AlarmListItemView row = visibleRows[i];
            row.gameObject.SetActive(true);
            row.name = "AlarmRow_" + i;
            PositionListRow(row, i);
            row.Setup(alarm, GetRowTitleText(alarm), GetRowTypeText(alarm), GetRowTimeText(alarm), GetRowToggleOn(alarm), SelectAlarm, ToggleAlarmEnabled, DeleteAlarmFromList);
        }

        for (int i = alarms.Count; i < visibleRows.Count; i++)
        {
            if (visibleRows[i] != null)
            {
                visibleRows[i].gameObject.SetActive(false);
            }
        }

        ResizeListContent(alarms.Count);
    }

    private void EnsureVisibleRowCount(int rowCount)
    {
        if (listItemTemplate == null || listContent == null)
        {
            return;
        }

        while (visibleRows.Count < rowCount)
        {
            AlarmListItemView row = Instantiate(listItemTemplate, listContent);
            visibleRows.Add(row);
        }
    }

    private void RefreshVisibleRows()
    {
        if (alarmManager == null)
        {
            return;
        }

        List<AlarmItem> alarms = alarmManager.GetAlarms();
        int count = Mathf.Min(visibleRows.Count, alarms.Count);
        for (int i = 0; i < count; i++)
        {
            AlarmListItemView row = visibleRows[i];
            AlarmItem alarm = alarms[i];
            if (row == null || alarm == null)
            {
                continue;
            }

            row.RefreshDisplay(GetRowTitleText(alarm), GetRowTypeText(alarm), GetRowTimeText(alarm), GetRowToggleOn(alarm));
        }
    }

    private void DisableListLayoutGroup()
    {
        if (listContent == null)
        {
            return;
        }

        LayoutGroup layoutGroup = listContent.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.enabled = false;
        }

        ContentSizeFitter contentSizeFitter = listContent.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter != null)
        {
            contentSizeFitter.enabled = false;
        }
    }

    private void PositionListRow(AlarmListItemView row, int index)
    {
        RectTransform rowRect = row.transform as RectTransform;
        if (rowRect == null)
        {
            return;
        }

        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -index * (listRowHeight + listRowSpacing));
        rowRect.sizeDelta = new Vector2(0f, listRowHeight);
    }

    private void ResizeListContent(int rowCount)
    {
        if (listContent == null)
        {
            return;
        }

        float height = rowCount * listRowHeight;
        if (rowCount > 1)
        {
            height += (rowCount - 1) * listRowSpacing;
        }

        RectTransform contentRect = listContent;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        if (rowCount == 0)
        {
            contentRect.anchoredPosition = Vector2.zero;
        }

        contentRect.sizeDelta = new Vector2(0f, Mathf.Max(0f, height));
    }

    private void RefreshDetail()
    {
        isRefreshing = true;
        bool hasSelection = selectedAlarm != null && alarmManager != null;

        if (detailPanel != null)
        {
            detailPanel.SetActive(hasSelection);
        }

        if (!hasSelection)
        {
            RefreshMiniAlarmButton();
            isRefreshing = false;
            return;
        }

        SetInput(titleInput, selectedAlarm.title);
        RefreshTimeControls();
        RefreshSoundControl();
        RefreshWeekdayControls();
        RefreshTimerPlayControls();
        RefreshMiniAlarmButton();
        isRefreshing = false;
    }

    private void SelectAlarm(AlarmItem alarm)
    {
        selectedAlarm = alarm;
        RefreshDetail();
        NotifySelectedAlarmChanged();
    }

    private void ToggleAlarmEnabled(AlarmItem alarm)
    {
        if (alarm == null || alarmManager == null)
        {
            return;
        }

        if (alarm.alarmType == AlarmType.RelativeTimer)
        {
            ToggleTimerFromList(alarm);
            return;
        }

        alarmManager.ToggleEnabled(alarm.id);
    }

    private void DeleteAlarmFromList(AlarmItem alarm)
    {
        if (alarm == null || alarmManager == null)
        {
            return;
        }

        if (selectedAlarm != null && selectedAlarm.id == alarm.id)
        {
            selectedAlarm = null;
        }

        CloseMiniAlarm(alarm.id, true);
        alarmManager.DeleteAlarm(alarm.id);
        NotifySelectedAlarmChanged();
    }

    private void AddDefaultDailyAlarm()
    {
        if (alarmManager == null)
        {
            return;
        }

        AlarmItem alarm = alarmManager.AddDailyAlarm(string.Empty, 7, 30, 0, "default_alarm");
        SelectAlarm(alarm);
    }

    private void AddDefaultRelativeTimer()
    {
        if (alarmManager == null)
        {
            return;
        }

        AlarmItem alarm = alarmManager.AddRelativeTimer(string.Empty, 600, "default_alarm");
        SelectAlarm(alarm);
    }

    private void OnAlarmRang(AlarmItem alarm)
    {
        SelectAlarm(alarm);
    }

    private void OnTimerStartPauseClicked()
    {
        if (selectedAlarm == null || alarmManager == null || selectedAlarm.alarmType != AlarmType.RelativeTimer)
        {
            return;
        }

        string state = alarmManager.GetRelativeTimerState(selectedAlarm.id);
        if (state == AlarmRuntimeState.Running)
        {
            alarmManager.PauseRelativeTimer(selectedAlarm.id);
        }
        else
        {
            alarmManager.StartRelativeTimer(selectedAlarm.id);
        }

        RefreshTimerRuntimeViews();
    }

    private void OnTimerResetClicked()
    {
        if (selectedAlarm == null || alarmManager == null || selectedAlarm.alarmType != AlarmType.RelativeTimer)
        {
            return;
        }

        alarmManager.ResetRelativeTimer(selectedAlarm.id);
        RefreshTimerRuntimeViews();
    }

    private void OnTitleChanged(string value)
    {
        if (isRefreshing || selectedAlarm == null || alarmManager == null)
        {
            return;
        }

        selectedAlarm.title = value;
        alarmManager.UpdateAlarmTitle(selectedAlarm.id, value);
    }

    private void OnTimeWheelChanged(int value)
    {
        if (isRefreshing || selectedAlarm == null)
        {
            return;
        }

        if (selectedAlarm.alarmType == AlarmType.DailyTime)
        {
            SaveCurrentDailyTime();
        }
        else
        {
            SaveCurrentTimerDuration();
        }
    }

    private void OnSoundDropdownChanged(int value)
    {
        if (isRefreshing || selectedAlarm == null || alarmManager == null)
        {
            return;
        }

        string soundType = GetSoundTypeFromDropdown();
        selectedAlarm.soundType = soundType;
        alarmManager.UpdateAlarmSoundType(selectedAlarm.id, soundType);
    }

    private void OnNoWeekendChanged(bool excludeWeekend)
    {
        if (isRefreshing || selectedAlarm == null || alarmManager == null)
        {
            return;
        }

        selectedAlarm.excludeWeekend = excludeWeekend;
        alarmManager.SetExcludeWeekend(selectedAlarm.id, excludeWeekend);
        RefreshWeekdayControls();
    }

    private void OnWeekdayClicked(DayOfWeek dayOfWeek)
    {
        if (selectedAlarm == null || alarmManager == null)
        {
            return;
        }

        bool currentValue = alarmManager.GetWeekdayValue(selectedAlarm, dayOfWeek);
        alarmManager.SetWeekdayEnabled(selectedAlarm.id, dayOfWeek, !currentValue);
        RefreshWeekdayControls();
    }

    private void SaveCurrentDailyTime()
    {
        if (selectedAlarm == null || alarmManager == null)
        {
            return;
        }

        int hour = GetSelectedHour();
        int minute = GetWheelValue(minuteWheel);
        string displayMode = GetSelectedTimeDisplayMode();

        selectedAlarm.hour = hour;
        selectedAlarm.minute = minute;
        selectedAlarm.second = 0;
        selectedAlarm.timeDisplayMode = displayMode;
        alarmManager.UpdateDailyAlarmTime(selectedAlarm.id, hour, minute, displayMode);
    }

    private void SaveCurrentTimerDuration()
    {
        if (selectedAlarm == null || alarmManager == null)
        {
            return;
        }

        int hour = GetWheelValue(hourWheel);
        int minute = GetWheelValue(minuteWheel);
        int second = GetWheelValue(secondWheel);
        int durationSeconds = Mathf.Max(1, hour * 3600 + minute * 60 + second);

        selectedAlarm.durationSeconds = durationSeconds;
        alarmManager.UpdateRelativeTimer(selectedAlarm.id, selectedAlarm.title, durationSeconds, selectedAlarm.audioClipId);
    }

    private int GetSelectedHour()
    {
        int wheelHour = GetWheelValue(hourWheel);
        int mode = GetWheelValue(meridiemWheel);
        if (mode == 0)
        {
            return wheelHour;
        }

        int normalizedHour = wheelHour;
        if (normalizedHour <= 0)
        {
            normalizedHour = 12;
        }

        if (mode == 1)
        {
            if (normalizedHour == 12)
            {
                return 0;
            }

            return normalizedHour;
        }

        if (normalizedHour == 12)
        {
            return 12;
        }

        return normalizedHour + 12;
    }

    private string GetSelectedTimeDisplayMode()
    {
        int mode = GetWheelValue(meridiemWheel);
        if (mode == 0)
        {
            return AlarmTimeDisplayMode.Hour24;
        }

        if (mode == 1)
        {
            return AlarmTimeDisplayMode.AM;
        }

        return AlarmTimeDisplayMode.PM;
    }

    private void RefreshTimeControls()
    {
        if (selectedAlarm == null)
        {
            return;
        }

        bool isDaily = selectedAlarm.alarmType == AlarmType.DailyTime;
        SetActive(meridiemWheel, isDaily);
        SetActive(hourWheel, true);
        SetActive(minuteWheel, true);
        SetActive(secondWheel, !isDaily);
        SetActive(hourMinuteColonObject, true);
        SetActive(secondColonObject, !isDaily);
        SetActive(timerCountdownText, false);

        if (!isDaily)
        {
            RefreshTimerTimeControls();
            return;
        }

        if (meridiemWheel != null)
        {
            meridiemWheel.SetValue(GetDisplayModeWheelValue(selectedAlarm.timeDisplayMode));
        }

        bool use24Hour = selectedAlarm.timeDisplayMode == AlarmTimeDisplayMode.Hour24;
        if (hourWheel != null)
        {
            if (use24Hour)
            {
                hourWheel.ConfigureRange(0, 23, true);
                hourWheel.SetValue(selectedAlarm.hour);
            }
            else
            {
                hourWheel.ConfigureRange(1, 12, false);
                hourWheel.SetValue(GetAmPmDisplayHour(selectedAlarm.hour));
            }
        }

        if (minuteWheel != null)
        {
            minuteWheel.SetValue(selectedAlarm.minute);
        }
    }

    private void RefreshTimerTimeControls()
    {
        int durationSeconds = Mathf.Max(1, selectedAlarm.durationSeconds);
        int hours = durationSeconds / 3600;
        int minutes = durationSeconds % 3600 / 60;
        int seconds = durationSeconds % 60;

        if (hourWheel != null)
        {
            hourWheel.ConfigureRange(0, 99, true);
            hourWheel.SetValue(hours);
        }

        if (minuteWheel != null)
        {
            minuteWheel.SetValue(minutes);
        }

        if (secondWheel != null)
        {
            secondWheel.SetValue(seconds);
        }
    }

    private void RefreshSoundControl()
    {
        if (selectedAlarm == null || soundDropdown == null)
        {
            return;
        }

        if (selectedAlarm.soundType == AlarmSoundType.Character)
        {
            soundDropdown.value = 0;
        }
        else
        {
            soundDropdown.value = 1;
        }

        soundDropdown.RefreshShownValue();
    }

    private void RefreshWeekdayControls()
    {
        if (selectedAlarm == null)
        {
            return;
        }

        bool isDaily = selectedAlarm.alarmType == AlarmType.DailyTime;
        SetActive(weekdayGroup, isDaily);
        if (!isDaily)
        {
            return;
        }

        SetToggle(noWeekendToggle, selectedAlarm.excludeWeekend);

        if (weekdayButtons == null)
        {
            return;
        }

        int maxCount = Mathf.Min(weekdayButtons.Length, weekdays.Length);
        for (int i = 0; i < maxCount; i++)
        {
            AlarmWeekdayButtonView dayButton = weekdayButtons[i];
            if (dayButton == null)
            {
                continue;
            }

            DayOfWeek day = weekdays[i];
            bool storedValue = alarmManager.GetWeekdayValue(selectedAlarm, day);
            bool clickable = IsWeekdayClickable(day);
            bool displayValue = storedValue;
            if (!clickable)
            {
                displayValue = false;
            }

            dayButton.Setup(day, weekdayLabels[i], displayValue, clickable, OnWeekdayClicked);
        }
    }

    private void RefreshTimerPlayControls()
    {
        EnsureTimerPlayControls();
        bool isTimer = selectedAlarm != null && selectedAlarm.alarmType == AlarmType.RelativeTimer;
        SetActive(timerPlayGroup, isTimer);
        SetActive(timerCountdownText, false);
        if (!isTimer || alarmManager == null)
        {
            return;
        }

        string state = alarmManager.GetRelativeTimerState(selectedAlarm.id);
        bool isRunning = state == AlarmRuntimeState.Running;
        bool isPaused = state == AlarmRuntimeState.Paused;
        bool isRinging = alarmManager.IsAlarmRinging(selectedAlarm.id) || state == AlarmRuntimeState.Ringing;
        bool showCountdown = isRunning || isPaused || isRinging;

        SetActive(hourWheel, !showCountdown);
        SetActive(minuteWheel, !showCountdown);
        SetActive(secondWheel, !showCountdown);
        SetActive(hourMinuteColonObject, !showCountdown);
        SetActive(secondColonObject, !showCountdown);
        SetActive(timerCountdownText, showCountdown);

        if (timerCountdownText != null && showCountdown)
        {
            timerCountdownText.text = AlarmTimeFormatter.FormatRemainingSeconds(alarmManager.GetRemainingSeconds(selectedAlarm));
        }

        SetActive(timerStartButton, !isRinging);
        SetButtonLabel(timerStartButton, GetTimerStartPauseLabel(state));
        SetActive(timerResetButton, isRunning || isPaused || isRinging);
    }

    private void CacheOptionalTimeObjects()
    {
        if (hourMinuteColonObject != null)
        {
            return;
        }

        if (hourWheel == null)
        {
            return;
        }

        Transform parent = hourWheel.transform.parent;
        if (parent == null)
        {
            return;
        }

        Transform colon = parent.Find("ColonText");
        if (colon != null)
        {
            hourMinuteColonObject = colon.gameObject;
        }
    }

    private string GetTimerStartPauseLabel(string state)
    {
        if (state == AlarmRuntimeState.Running)
        {
            return "Pause";
        }

        if (state == AlarmRuntimeState.Paused)
        {
            return "Resume";
        }

        return "Start";
    }

    private void RefreshTimerRuntimeViews()
    {
        RefreshVisibleRows();
        RefreshTimeControls();
        RefreshTimerPlayControls();
        RefreshActiveMiniAlarms();
    }

    private void EnsureMiniAlarmControls()
    {
        if (showMiniAlarmButton == null)
        {
            Transform existing = transform.Find("Header/ShowMiniAlarmButton");
            if (existing != null)
            {
                showMiniAlarmButton = existing.GetComponent<Button>();
            }
        }

        if (showMiniAlarmButton == null)
        {
            showMiniAlarmButton = CreateMiniAlarmButton();
        }

        RefreshMiniAlarmButton();
    }

    private Button CreateMiniAlarmButton()
    {
        Transform header = null;
        if (addAlarmButton != null)
        {
            header = addAlarmButton.transform.parent;
        }
        else
        {
            header = transform.Find("Header");
        }

        if (header == null)
        {
            return null;
        }

        GameObject buttonObject = new GameObject("ShowMiniAlarmButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(header, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        RectTransform addRect = addAlarmButton != null ? addAlarmButton.transform as RectTransform : null;
        if (addRect != null)
        {
            buttonRect.anchorMin = addRect.anchorMin;
            buttonRect.anchorMax = addRect.anchorMax;
            buttonRect.pivot = addRect.pivot;
            buttonRect.anchoredPosition = addRect.anchoredPosition + new Vector2(-addRect.sizeDelta.x - 10f, 0f);
        }
        else
        {
            buttonRect.anchorMin = new Vector2(1f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-260f, -8f);
        }

        buttonRect.sizeDelta = new Vector2(38f, 38f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.3f, 0.33f, 0.39f, 1f);
        buttonImage.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        return button;
    }

    private void RefreshMiniAlarmButton()
    {
        bool showButton = selectedAlarm != null && selectedAlarm.alarmType == AlarmType.RelativeTimer;
        SetActive(showMiniAlarmButton, showButton);
    }

    private void OnShowMiniAlarmClicked()
    {
        if (selectedAlarm == null || selectedAlarm.alarmType != AlarmType.RelativeTimer)
        {
            return;
        }

        if (miniAlarmPrefab == null)
        {
            Debug.LogWarning("[AlarmUI] Mini alarm prefab is not assigned.");
            return;
        }

        AlarmMiniView mini = GetOrCreateMiniAlarm(selectedAlarm);
        if (mini == null)
        {
            return;
        }

        if (!positionedMiniAlarmIds.Contains(selectedAlarm.id))
        {
            PositionMiniAlarmNearHeaderButton(mini);
            positionedMiniAlarmIds.Add(selectedAlarm.id);
        }

        mini.RefreshFromManager(alarmManager, selectedAlarm);
        mini.Show();
        EnsureMiniAlarmOnTop(mini);
    }

    private AlarmMiniView GetOrCreateMiniAlarm(AlarmItem alarm)
    {
        if (alarm == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            return null;
        }

        AlarmMiniView existing;
        if (activeMiniAlarms.TryGetValue(alarm.id, out existing) && existing != null)
        {
            RectTransform existingParent = GetMiniAlarmParent();
            if (existingParent != null && existing.transform.parent != existingParent)
            {
                existing.transform.SetParent(existingParent, true);
            }

            return existing;
        }

        RectTransform parent = GetMiniAlarmParent();

        AlarmMiniView mini = Instantiate(miniAlarmPrefab, parent);
        mini.name = "AlarmMini_" + alarm.id;
        mini.Bind(alarm);
        mini.StartRequested += OnMiniStartRequested;
        mini.PauseRequested += OnMiniPauseRequested;
        mini.ResetRequested += OnMiniResetRequested;
        mini.CloseRequested += OnMiniCloseRequested;
        activeMiniAlarms[alarm.id] = mini;
        return mini;
    }

    private RectTransform GetMiniAlarmParent()
    {
        if (CanvasManager.Instance != null && CanvasManager.Instance.canvasUI != null)
        {
            return CanvasManager.Instance.canvasUI.transform as RectTransform;
        }

        if (miniAlarmRoot != null)
        {
            return miniAlarmRoot;
        }

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            return parentCanvas.transform as RectTransform;
        }

        return transform as RectTransform;
    }

    private void PositionMiniAlarmNearHeaderButton(AlarmMiniView mini)
    {
        if (mini == null)
        {
            return;
        }

        RectTransform miniRect = mini.transform as RectTransform;
        if (miniRect == null)
        {
            return;
        }

        RectTransform parentRect = miniRect.parent as RectTransform;
        RectTransform sourceRect = transform as RectTransform;
        Vector3 basePosition = sourceRect != null ? sourceRect.position : transform.position;
        if (parentRect != null)
        {
            Vector3 localBasePosition = parentRect.InverseTransformPoint(basePosition);
            miniRect.anchorMin = new Vector2(0.5f, 0.5f);
            miniRect.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 targetPosition = new Vector2(localBasePosition.x + miniAlarmSpawnOffset.x, localBasePosition.y + miniAlarmSpawnOffset.y);
            miniRect.anchoredPosition = ClampMiniAlarmPosition(parentRect, miniRect, targetPosition);
        }
        else
        {
            miniRect.position = basePosition + new Vector3(miniAlarmSpawnOffset.x, miniAlarmSpawnOffset.y, 0f);
        }

        Vector3 localPosition = miniRect.localPosition;
        localPosition.z = 10f;
        miniRect.localPosition = localPosition;
    }

    private Vector2 ClampMiniAlarmPosition(RectTransform parentRect, RectTransform miniRect, Vector2 targetPosition)
    {
        if (parentRect == null || miniRect == null)
        {
            return targetPosition;
        }

        Vector2 miniSize = miniRect.rect.size;
        if (miniSize.x <= 0f)
        {
            miniSize.x = miniRect.sizeDelta.x;
        }

        if (miniSize.y <= 0f)
        {
            miniSize.y = miniRect.sizeDelta.y;
        }

        Rect parentBounds = parentRect.rect;
        float halfWidth = Mathf.Max(0f, miniSize.x * 0.5f);
        float halfHeight = Mathf.Max(0f, miniSize.y * 0.5f);
        float minX = parentBounds.xMin + halfWidth;
        float maxX = parentBounds.xMax - halfWidth;
        float minY = parentBounds.yMin + halfHeight;
        float maxY = parentBounds.yMax - halfHeight;

        if (minX > maxX)
        {
            targetPosition.x = parentBounds.center.x;
        }
        else
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minX, maxX);
        }

        if (minY > maxY)
        {
            targetPosition.y = parentBounds.center.y;
        }
        else
        {
            targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);
        }

        return targetPosition;
    }

    private void EnsureMiniAlarmOnTop(AlarmMiniView mini)
    {
        if (mini == null)
        {
            return;
        }

        mini.transform.SetAsLastSibling();

        RectTransform miniRect = mini.transform as RectTransform;
        if (miniRect != null)
        {
            Vector3 localPosition = miniRect.localPosition;
            localPosition.z = 10f;
            miniRect.localPosition = localPosition;
        }
    }

    private void RefreshActiveMiniAlarms()
    {
        if (alarmManager == null || activeMiniAlarms.Count == 0)
        {
            return;
        }

        List<string> staleIds = null;
        foreach (KeyValuePair<string, AlarmMiniView> pair in activeMiniAlarms)
        {
            AlarmMiniView mini = pair.Value;
            AlarmItem alarm = FindAlarmById(pair.Key);
            if (mini == null || alarm == null)
            {
                if (staleIds == null)
                {
                    staleIds = new List<string>();
                }

                staleIds.Add(pair.Key);
                continue;
            }

            mini.RefreshFromManager(alarmManager, alarm);
        }

        if (staleIds == null)
        {
            return;
        }

        for (int i = 0; i < staleIds.Count; i++)
        {
            activeMiniAlarms.Remove(staleIds[i]);
            positionedMiniAlarmIds.Remove(staleIds[i]);
        }
    }

    private void OnMiniStartRequested(string alarmId)
    {
        if (alarmManager == null)
        {
            return;
        }

        alarmManager.StartRelativeTimer(alarmId);
        RefreshTimerRuntimeViews();
    }

    private void OnMiniPauseRequested(string alarmId)
    {
        if (alarmManager == null)
        {
            return;
        }

        alarmManager.PauseRelativeTimer(alarmId);
        RefreshTimerRuntimeViews();
    }

    private void OnMiniResetRequested(string alarmId)
    {
        if (alarmManager == null)
        {
            return;
        }

        alarmManager.ResetRelativeTimer(alarmId);
        RefreshTimerRuntimeViews();
    }

    private void OnMiniCloseRequested(string alarmId)
    {
        CloseMiniAlarm(alarmId, false);
    }

    private void CloseMiniAlarm(string alarmId, bool removeMapping)
    {
        if (string.IsNullOrEmpty(alarmId))
        {
            return;
        }

        AlarmMiniView mini;
        if (!activeMiniAlarms.TryGetValue(alarmId, out mini))
        {
            return;
        }

        if (mini != null)
        {
            mini.Hide();
        }

        if (removeMapping)
        {
            activeMiniAlarms.Remove(alarmId);
            positionedMiniAlarmIds.Remove(alarmId);
        }
    }

    private AlarmItem FindAlarmById(string alarmId)
    {
        if (alarmManager == null || string.IsNullOrEmpty(alarmId))
        {
            return null;
        }

        List<AlarmItem> alarms = alarmManager.GetAlarms();
        for (int i = 0; i < alarms.Count; i++)
        {
            AlarmItem alarm = alarms[i];
            if (alarm != null && alarm.id == alarmId)
            {
                return alarm;
            }
        }

        return null;
    }

    private void NotifySelectedAlarmChanged()
    {
        if (SelectedAlarmChanged != null)
        {
            SelectedAlarmChanged.Invoke(selectedAlarm);
        }
    }

    private void EnsureTimerPlayControls()
    {
        if (timerPlayGroup == null)
        {
            Transform foundGroup = transform.Find("AlarmDetailPanel/TimerPlayGroup");
            if (foundGroup == null)
            {
                foundGroup = transform.Find("AlarmDetailPanel/TimerEditGroup");
            }

            if (foundGroup != null)
            {
                foundGroup.name = "TimerPlayGroup";
                timerPlayGroup = foundGroup.gameObject;
            }
        }

        if (timerPlayGroup == null)
        {
            timerPlayGroup = CreateTimerPlayGroup();
        }

        if (timerPlayGroup == null)
        {
            return;
        }

        ConfigureTimerPlayGroupRect();
        RemoveTimerNote();
        timerStartButton = FindOrCreateTimerButton(timerStartButton, "TimerStartButton", "Start", 14f);
        timerResetButton = FindOrCreateTimerButton(timerResetButton, "TimerResetButton", "Reset", 124f);
        EnsureTimerCountdownText();
    }

    private GameObject CreateTimerPlayGroup()
    {
        Transform detailTransform = null;
        if (detailPanel != null)
        {
            detailTransform = detailPanel.transform;
        }
        else
        {
            detailTransform = transform.Find("AlarmDetailPanel");
        }

        if (detailTransform == null)
        {
            return null;
        }

        GameObject groupObject = new GameObject("TimerPlayGroup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        groupObject.transform.SetParent(detailTransform, false);

        Image groupImage = groupObject.GetComponent<Image>();
        groupImage.color = new Color(0.13f, 0.135f, 0.15f, 1f);
        groupImage.raycastTarget = true;

        return groupObject;
    }

    private void ConfigureTimerPlayGroupRect()
    {
        RectTransform groupRect = timerPlayGroup.transform as RectTransform;
        if (groupRect == null)
        {
            return;
        }

        groupRect.anchorMin = new Vector2(0f, 1f);
        groupRect.anchorMax = new Vector2(1f, 1f);
        groupRect.pivot = new Vector2(0.5f, 1f);
        groupRect.anchoredPosition = new Vector2(12f, -286f);
        groupRect.sizeDelta = new Vector2(-24f, 54f);
    }

    private void EnsureTimerCountdownText()
    {
        if (timerCountdownText != null)
        {
            return;
        }

        Transform parent = null;
        if (hourWheel != null)
        {
            parent = hourWheel.transform.parent;
        }
        else if (minuteWheel != null)
        {
            parent = minuteWheel.transform.parent;
        }

        if (parent == null)
        {
            return;
        }

        Transform existing = parent.Find("TimerCountdownLabel");
        if (existing != null)
        {
            timerCountdownText = existing.GetComponent<TMP_Text>();
            return;
        }

        GameObject labelObject = new GameObject("TimerCountdownLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;

        timerCountdownText = labelObject.GetComponent<TMP_Text>();
        timerCountdownText.text = "00:00:00";
        timerCountdownText.fontSize = 46f;
        timerCountdownText.color = Color.white;
        timerCountdownText.alignment = TextAlignmentOptions.Center;
        timerCountdownText.raycastTarget = false;
        labelObject.SetActive(false);
    }

    private void RemoveTimerNote()
    {
        if (timerPlayGroup == null)
        {
            return;
        }

        Transform note = timerPlayGroup.transform.Find("TimerNote");
        if (note != null)
        {
            note.gameObject.SetActive(false);
        }
    }

    private Button FindOrCreateTimerButton(Button currentButton, string buttonName, string label, float x)
    {
        if (currentButton != null)
        {
            return currentButton;
        }

        Transform existing = timerPlayGroup.transform.Find(buttonName);
        if (existing != null)
        {
            return existing.GetComponent<Button>();
        }

        GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(timerPlayGroup.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 0.5f);
        buttonRect.anchorMax = new Vector2(0f, 0.5f);
        buttonRect.pivot = new Vector2(0f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(x, 0f);
        buttonRect.sizeDelta = new Vector2(96f, 34f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.31f, 0.35f, 0.43f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;

        GameObject labelObject = new GameObject(buttonName + "_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;

        TMP_Text labelText = labelObject.GetComponent<TMP_Text>();
        labelText.text = label;
        labelText.fontSize = 14f;
        labelText.color = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;

        return button;
    }

    private bool IsWeekdayClickable(DayOfWeek day)
    {
        if (selectedAlarm != null && selectedAlarm.excludeWeekend)
        {
            if (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday)
            {
                return false;
            }
        }

        return true;
    }

    private string GetRowTypeText(AlarmItem alarm)
    {
        if (alarm == null || string.IsNullOrWhiteSpace(alarm.title))
        {
            return string.Empty;
        }

        return TruncateRowTitle(alarm.title);
    }

    private string GetRowTitleText(AlarmItem alarm)
    {
        if (alarm == null)
        {
            return string.Empty;
        }

        if (alarm.alarmType == AlarmType.DailyTime)
        {
            return "Alarm";
        }

        return "Timer";
    }

    private string GetRowTimeText(AlarmItem alarm)
    {
        if (alarm.alarmType == AlarmType.DailyTime)
        {
            return AlarmTimeFormatter.FormatDailyTime(alarm);
        }

        if (alarmManager == null)
        {
            return AlarmTimeFormatter.FormatRemainingSeconds(alarm.durationSeconds);
        }

        return AlarmTimeFormatter.FormatRemainingSeconds(alarmManager.GetRemainingSeconds(alarm));
    }

    private bool GetRowToggleOn(AlarmItem alarm)
    {
        if (alarm == null)
        {
            return false;
        }

        if (alarm.alarmType == AlarmType.RelativeTimer)
        {
            if (alarmManager == null)
            {
                return false;
            }

            string state = alarmManager.GetRelativeTimerState(alarm.id);
            return state == AlarmRuntimeState.Running || alarmManager.IsAlarmRinging(alarm.id);
        }

        return alarm.enabled;
    }

    private void ToggleTimerFromList(AlarmItem alarm)
    {
        if (alarm == null || alarmManager == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            return;
        }

        string state = alarmManager.GetRelativeTimerState(alarm.id);
        bool isOn = state == AlarmRuntimeState.Running || alarmManager.IsAlarmRinging(alarm.id);
        if (isOn)
        {
            alarmManager.ResetRelativeTimer(alarm.id);
        }
        else
        {
            alarmManager.StartRelativeTimer(alarm.id);
        }

        RefreshTimerRuntimeViews();
    }

    private string TruncateRowTitle(string value)
    {
        const int maxLength = 30;
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string trimmedValue = value.Trim();
        if (trimmedValue.Length <= maxLength)
        {
            return trimmedValue;
        }

        return trimmedValue.Substring(0, maxLength) + "...";
    }

    private string GetSoundTypeFromDropdown()
    {
        if (soundDropdown == null)
        {
            return AlarmSoundType.Music;
        }

        if (soundDropdown.value == 0)
        {
            return AlarmSoundType.Character;
        }

        return AlarmSoundType.Music;
    }

    private int GetDisplayModeWheelValue(string displayMode)
    {
        if (displayMode == AlarmTimeDisplayMode.AM)
        {
            return 1;
        }

        if (displayMode == AlarmTimeDisplayMode.PM)
        {
            return 2;
        }

        return 0;
    }

    private int GetAmPmDisplayHour(int hour)
    {
        int displayHour = hour % 12;
        if (displayHour == 0)
        {
            displayHour = 12;
        }

        return displayHour;
    }

    private int GetWheelValue(AlarmWheelPicker wheel)
    {
        if (wheel == null)
        {
            return 0;
        }

        return wheel.GetValue();
    }

    private void ClearVisibleRows()
    {
        for (int i = visibleRows.Count - 1; i >= 0; i--)
        {
            if (visibleRows[i] != null)
            {
                Destroy(visibleRows[i].gameObject);
            }
        }

        visibleRows.Clear();
    }

    private void SetEmptyListVisible(bool visible)
    {
        if (emptyListText != null)
        {
            emptyListText.gameObject.SetActive(visible);
        }
    }

    private void SetInput(TMP_InputField target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private void SetToggle(Toggle target, bool value)
    {
        if (target != null)
        {
            target.isOn = value;
        }
    }

    private void SetActive(AlarmWheelPicker target, bool active)
    {
        if (target != null)
        {
            target.gameObject.SetActive(active);
        }
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private void SetActive(Button target, bool active)
    {
        if (target != null)
        {
            target.gameObject.SetActive(active);
        }
    }

    private void SetActive(TMP_Text target, bool active)
    {
        if (target != null)
        {
            target.gameObject.SetActive(active);
        }
    }

    private void SetButtonLabel(Button target, string label)
    {
        if (target == null)
        {
            return;
        }

        TMP_Text text = target.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
        }
    }
}
