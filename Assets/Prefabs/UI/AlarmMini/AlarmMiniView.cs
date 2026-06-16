using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum AlarmMiniMode
{
    Empty,
    Timer,
    Daily
}

public class AlarmMiniView : MonoBehaviour
{
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private Button startPauseButton;
    [SerializeField] private Image startPauseBackgroundImage;
    [SerializeField] private Image startPauseIconImage;
    [SerializeField] private Button resetButton;
    [SerializeField] private Image resetIconImage;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image closeIconImage;
    [SerializeField] private Sprite playSprite;
    [SerializeField] private Sprite pauseSprite;
    [SerializeField] private Sprite playBackgroundSprite;
    [SerializeField] private Sprite pauseBackgroundSprite;
    [SerializeField] private Sprite resetSprite;
    [SerializeField] private Sprite closeSprite;

    public event Action<string> StartRequested;
    public event Action<string> PauseRequested;
    public event Action<string> ResetRequested;
    public event Action<string> CloseRequested;
    public event Action<string, bool> TimerRunningChanged;

    private AlarmMiniMode mode = AlarmMiniMode.Empty;
    private string alarmId = string.Empty;
    private string timerState = AlarmRuntimeState.Idle;
    private int visibleRemainingSeconds;

    private void Awake()
    {
        BindButtons();
        ApplySprites();
        RefreshVisibility();
        RefreshTimeText();
    }

    public void SetupTimer(string id, int seconds)
    {
        alarmId = id;
        mode = AlarmMiniMode.Timer;
        timerState = AlarmRuntimeState.Idle;
        visibleRemainingSeconds = Mathf.Max(1, seconds);
        RefreshVisibility();
        RefreshTimeText();
        RefreshStartPauseVisual();
    }

    public void SetupDaily(string id, DateTime targetTime)
    {
        ClearAlarmContent();
    }

    public void Bind(AlarmItem alarm)
    {
        if (alarm == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            ClearAlarmContent();
            return;
        }

        SetupTimer(alarm.id, alarm.durationSeconds);
    }

    public void RefreshFromManager(AlarmManager manager, AlarmItem alarm)
    {
        if (manager == null || alarm == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            ClearAlarmContent();
            return;
        }

        alarmId = alarm.id;
        mode = AlarmMiniMode.Timer;
        timerState = manager.GetRelativeTimerState(alarm.id);
        if (manager.IsAlarmRinging(alarm.id))
        {
            timerState = AlarmRuntimeState.Ringing;
        }

        visibleRemainingSeconds = manager.GetRemainingSeconds(alarm);
        RefreshVisibility();
        RefreshTimeText();
        RefreshStartPauseVisual();
    }

    public void SetRemainingSeconds(int remainingSeconds)
    {
        visibleRemainingSeconds = Mathf.Max(0, remainingSeconds);
        RefreshTimeText();
    }

    public void SetTargetTime(DateTime targetTime)
    {
        SetRemainingSeconds(0);
    }

    public void SetMode(AlarmMiniMode nextMode)
    {
        mode = nextMode == AlarmMiniMode.Daily ? AlarmMiniMode.Empty : nextMode;
        RefreshVisibility();
        RefreshTimeText();
    }

    public void CloseWindow()
    {
        if (CloseRequested != null)
        {
            CloseRequested.Invoke(alarmId);
        }

        Hide();
    }

    public void ResetTimer()
    {
        if (!string.IsNullOrEmpty(alarmId) && ResetRequested != null)
        {
            ResetRequested.Invoke(alarmId);
        }
    }

    public void ClearAlarmContent()
    {
        alarmId = string.Empty;
        mode = AlarmMiniMode.Empty;
        timerState = AlarmRuntimeState.Idle;
        visibleRemainingSeconds = 0;
        RefreshVisibility();
        RefreshTimeText();
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void BindButtons()
    {
        if (startPauseButton != null)
        {
            startPauseButton.onClick.RemoveListener(OnStartPauseClicked);
            startPauseButton.onClick.AddListener(OnStartPauseClicked);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetTimer);
            resetButton.onClick.AddListener(ResetTimer);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseWindow);
            closeButton.onClick.AddListener(CloseWindow);
        }
    }

    private void ApplySprites()
    {
        if (resetIconImage != null)
        {
            resetIconImage.sprite = resetSprite;
        }

        if (closeIconImage != null)
        {
            closeIconImage.sprite = closeSprite;
        }

        RefreshStartPauseVisual();
    }

    private void OnStartPauseClicked()
    {
        if (mode != AlarmMiniMode.Timer || string.IsNullOrEmpty(alarmId))
        {
            return;
        }

        if (timerState == AlarmRuntimeState.Running)
        {
            if (PauseRequested != null)
            {
                PauseRequested.Invoke(alarmId);
            }

            if (TimerRunningChanged != null)
            {
                TimerRunningChanged.Invoke(alarmId, false);
            }
        }
        else if (timerState == AlarmRuntimeState.Ringing)
        {
            if (ResetRequested != null)
            {
                ResetRequested.Invoke(alarmId);
            }
        }
        else
        {
            if (StartRequested != null)
            {
                StartRequested.Invoke(alarmId);
            }

            if (TimerRunningChanged != null)
            {
                TimerRunningChanged.Invoke(alarmId, true);
            }
        }
    }

    private void RefreshVisibility()
    {
        bool isTimer = mode == AlarmMiniMode.Timer;

        SetActive(startPauseButton, isTimer);
        SetActive(resetButton, isTimer);
        SetActive(closeButton, true);
    }

    private void RefreshTimeText()
    {
        if (mode == AlarmMiniMode.Empty)
        {
            if (timeText != null)
            {
                timeText.text = "--:--:--";
            }

            return;
        }

        SetTimeText(visibleRemainingSeconds);
    }

    private void SetTimeText(int totalSeconds)
    {
        if (timeText == null)
        {
            return;
        }

        int hours = totalSeconds / 3600;
        int minutes = totalSeconds % 3600 / 60;
        int seconds = totalSeconds % 60;
        timeText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
    }

    private void RefreshStartPauseVisual()
    {
        if (startPauseIconImage != null)
        {
            if (timerState == AlarmRuntimeState.Running)
            {
                startPauseIconImage.sprite = pauseSprite;
            }
            else if (timerState == AlarmRuntimeState.Ringing && resetSprite != null)
            {
                startPauseIconImage.sprite = resetSprite;
            }
            else
            {
                startPauseIconImage.sprite = playSprite;
            }
        }

        Image backgroundImage = startPauseBackgroundImage != null ? startPauseBackgroundImage : startPauseButton != null ? startPauseButton.image : null;
        if (backgroundImage == null)
        {
            return;
        }

        Sprite nextBackground = timerState == AlarmRuntimeState.Running ? pauseBackgroundSprite : playBackgroundSprite;
        if (nextBackground != null)
        {
            backgroundImage.sprite = nextBackground;
        }
    }

    private void SetActive(Button button, bool active)
    {
        if (button != null)
        {
            button.gameObject.SetActive(active);
        }
    }
}
