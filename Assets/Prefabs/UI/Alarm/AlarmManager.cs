using System;
using System.Collections.Generic;
using UnityEngine;

public class AlarmManager : MonoBehaviour
{
    [SerializeField] private AlarmAudioPlayer audioPlayer;
    [SerializeField] private float tickSeconds = 1f;
    [SerializeField] private float dailyTriggerWindowSeconds = 2f;

    public event Action AlarmsChanged;
    public event Action<AlarmItem> AlarmRang;

    private AlarmSaveData saveData = new AlarmSaveData();
    private AlarmRepository repository;
    private Dictionary<string, AlarmTimerRuntime> relativeTimerStates = new Dictionary<string, AlarmTimerRuntime>();
    private HashSet<string> dailyTriggerKeys = new HashSet<string>();
    private HashSet<string> ringingAlarmIds = new HashSet<string>();
    private float tickProgress = 0f;

    private void Awake()
    {
        repository = new AlarmRepository();
        if (audioPlayer == null)
        {
            audioPlayer = GetComponent<AlarmAudioPlayer>();
        }

        LoadAlarms();
    }

    private void Update()
    {
        tickProgress += Time.unscaledDeltaTime;
        if (tickProgress < tickSeconds)
        {
            return;
        }

        tickProgress = 0f;
        UpdateAlarmStates();
    }

    // Load saved alarms.
    public void LoadAlarms()
    {
        saveData = repository.Load();
        NormalizeAlarms();
        RebuildRuntimeStates();
        NotifyAlarmsChanged();
    }

    // Save current alarms.
    public void SaveAlarms()
    {
        repository.Save(saveData);
    }

    // Return the alarm list.
    public List<AlarmItem> GetAlarms()
    {
        return saveData.alarms;
    }

    // Add a daily alarm.
    public AlarmItem AddDailyAlarm(string title, int hour, int minute, int second, string audioClipId)
    {
        AlarmItem alarm = AlarmItem.CreateDailyAlarm(CreateAlarmId(), title, hour, minute, second, audioClipId);
        ClampDailyTime(alarm);
        saveData.alarms.Add(alarm);
        SaveAlarms();
        NotifyAlarmsChanged();
        return alarm;
    }

    // Add a relative timer.
    public AlarmItem AddRelativeTimer(string title, int durationSeconds, string audioClipId)
    {
        AlarmItem alarm = AlarmItem.CreateRelativeTimer(CreateAlarmId(), title, durationSeconds, audioClipId);
        saveData.alarms.Add(alarm);
        relativeTimerStates[alarm.id] = CreateRelativeRuntime(alarm);
        SaveAlarms();
        NotifyAlarmsChanged();
        return alarm;
    }

    // Delete an alarm.
    public void DeleteAlarm(string id)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null)
        {
            return;
        }

        saveData.alarms.Remove(alarm);
        if (relativeTimerStates.ContainsKey(id))
        {
            relativeTimerStates.Remove(id);
        }

        if (ringingAlarmIds.Contains(id))
        {
            ringingAlarmIds.Remove(id);
        }

        if (audioPlayer != null && audioPlayer.GetCurrentAlarmId() == id)
        {
            audioPlayer.StopAlarmClip();
        }

        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Update a daily alarm.
    public void UpdateDailyAlarm(string id, string title, int hour, int minute, int second, string timeDisplayMode, bool excludeWeekend, string audioClipId)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.DailyTime)
        {
            return;
        }

        string normalizedTimeDisplayMode = NormalizeTimeDisplayMode(timeDisplayMode);
        if (alarm.title == title &&
            alarm.hour == hour &&
            alarm.minute == minute &&
            alarm.second == second &&
            alarm.timeDisplayMode == normalizedTimeDisplayMode &&
            alarm.excludeWeekend == excludeWeekend &&
            alarm.audioClipId == audioClipId)
        {
            return;
        }

        alarm.title = title;
        alarm.hour = hour;
        alarm.minute = minute;
        alarm.second = second;
        alarm.timeDisplayMode = normalizedTimeDisplayMode;
        alarm.excludeWeekend = excludeWeekend;
        alarm.audioClipId = audioClipId;
        ClampDailyTime(alarm);
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Update the alarm title and save immediately.
    public void UpdateAlarmTitle(string id, string title)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null)
        {
            return;
        }

        if (alarm.title == title)
        {
            return;
        }

        alarm.title = title;
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Update the daily alarm time and save immediately.
    public void UpdateDailyAlarmTime(string id, int hour, int minute, string timeDisplayMode)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.DailyTime)
        {
            return;
        }

        string normalizedTimeDisplayMode = NormalizeTimeDisplayMode(timeDisplayMode);
        if (alarm.hour == hour &&
            alarm.minute == minute &&
            alarm.second == 0 &&
            alarm.timeDisplayMode == normalizedTimeDisplayMode)
        {
            return;
        }

        alarm.hour = hour;
        alarm.minute = minute;
        alarm.second = 0;
        alarm.timeDisplayMode = normalizedTimeDisplayMode;
        ClampDailyTime(alarm);
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Update the alarm sound category and save immediately.
    public void UpdateAlarmSoundType(string id, string soundType)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null)
        {
            return;
        }

        string normalizedSoundType = NormalizeSoundType(soundType);
        if (alarm.soundType == normalizedSoundType)
        {
            return;
        }

        alarm.soundType = normalizedSoundType;
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Update weekend exclusion and save immediately.
    public void SetExcludeWeekend(string id, bool excludeWeekend)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.DailyTime)
        {
            return;
        }

        if (alarm.excludeWeekend == excludeWeekend)
        {
            return;
        }

        alarm.excludeWeekend = excludeWeekend;
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Update one weekday selection and save immediately.
    public void SetWeekdayEnabled(string id, DayOfWeek dayOfWeek, bool enabled)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.DailyTime)
        {
            return;
        }

        if (GetWeekdayValue(alarm, dayOfWeek) == enabled && alarm.weekdaySelectionInitialized)
        {
            return;
        }

        SetWeekdayValue(alarm, dayOfWeek, enabled);
        alarm.weekdaySelectionInitialized = true;
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Update a relative timer.
    public void UpdateRelativeTimer(string id, string title, int durationSeconds, string audioClipId)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            return;
        }

        AlarmTimerRuntime runtime = GetRelativeRuntime(alarm);
        string previousState = runtime.state;
        int normalizedDurationSeconds = Mathf.Max(1, durationSeconds);
        if (alarm.title == title &&
            alarm.durationSeconds == normalizedDurationSeconds &&
            alarm.audioClipId == audioClipId)
        {
            return;
        }

        alarm.title = title;
        alarm.durationSeconds = normalizedDurationSeconds;
        alarm.audioClipId = audioClipId;

        if (previousState == AlarmRuntimeState.Running)
        {
            runtime.state = AlarmRuntimeState.Running;
            runtime.startedAt = Time.unscaledTime;
            runtime.pausedRemainingSeconds = alarm.durationSeconds;
        }
        else if (previousState == AlarmRuntimeState.Paused)
        {
            runtime.state = AlarmRuntimeState.Paused;
            runtime.pausedRemainingSeconds = alarm.durationSeconds;
        }
        else
        {
            relativeTimerStates[id] = CreateRelativeRuntime(alarm);
        }

        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Enable or disable a daily alarm.
    public void SetDailyEnabled(string id, bool enabled)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.DailyTime)
        {
            return;
        }

        if (alarm.enabled == enabled)
        {
            return;
        }

        alarm.enabled = enabled;
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Toggle a daily alarm enabled state and save immediately.
    public void ToggleDailyEnabled(string id)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.DailyTime)
        {
            return;
        }

        alarm.enabled = !alarm.enabled;
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Toggle any alarm enabled state and save immediately.
    public void ToggleEnabled(string id)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null)
        {
            return;
        }

        alarm.enabled = !alarm.enabled;
        SaveAlarms();
        NotifyAlarmsChanged();
    }

    // Start a relative timer.
    public void StartRelativeTimer(string id)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            return;
        }

        AlarmTimerRuntime runtime = GetRelativeRuntime(alarm);
        int remainingSeconds = GetRemainingSeconds(alarm);
        if (remainingSeconds <= 0)
        {
            remainingSeconds = alarm.durationSeconds;
        }

        runtime.state = AlarmRuntimeState.Running;
        runtime.startedAt = Time.unscaledTime;
        runtime.pausedRemainingSeconds = remainingSeconds;
    }

    // Pause a relative timer.
    public void PauseRelativeTimer(string id)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            return;
        }

        AlarmTimerRuntime runtime = GetRelativeRuntime(alarm);
        if (runtime.state != AlarmRuntimeState.Running)
        {
            return;
        }

        runtime.pausedRemainingSeconds = GetRemainingSeconds(alarm);
        runtime.state = AlarmRuntimeState.Paused;
    }

    // Reset a relative timer.
    public void ResetRelativeTimer(string id)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            return;
        }

        relativeTimerStates[id] = CreateRelativeRuntime(alarm);
        if (audioPlayer != null && audioPlayer.GetCurrentAlarmId() == id)
        {
            audioPlayer.StopAlarmClip();
        }

        if (ringingAlarmIds.Contains(id))
        {
            ringingAlarmIds.Remove(id);
        }
    }

    // Dismiss a ringing alarm.
    public void DismissAlarm(string id)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null)
        {
            return;
        }

        if (audioPlayer != null && audioPlayer.GetCurrentAlarmId() == id)
        {
            audioPlayer.StopAlarmClip();
        }

        if (ringingAlarmIds.Contains(id))
        {
            ringingAlarmIds.Remove(id);
        }

        if (alarm.alarmType == AlarmType.RelativeTimer)
        {
            relativeTimerStates[id] = CreateRelativeRuntime(alarm);
        }

        NotifyAlarmsChanged();
    }

    // Calculate remaining seconds.
    public int GetRemainingSeconds(AlarmItem alarm)
    {
        if (alarm == null)
        {
            return 0;
        }

        if (alarm.alarmType == AlarmType.DailyTime)
        {
            if (!alarm.enabled)
            {
                return 0;
            }

            DateTime nextTime = GetNextDailyTime(alarm);
            TimeSpan remaining = nextTime - DateTime.Now;
            return Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalSeconds));
        }

        if (alarm.alarmType == AlarmType.RelativeTimer)
        {
            AlarmTimerRuntime runtime = GetRelativeRuntime(alarm);
            if (runtime.state == AlarmRuntimeState.Running)
            {
                float elapsed = Time.unscaledTime - runtime.startedAt;
                return Mathf.Max(0, runtime.pausedRemainingSeconds - Mathf.FloorToInt(elapsed));
            }

            if (runtime.state == AlarmRuntimeState.Paused)
            {
                return Mathf.Max(0, runtime.pausedRemainingSeconds);
            }

            if (runtime.state == AlarmRuntimeState.Ringing)
            {
                return 0;
            }

            return Mathf.Max(1, alarm.durationSeconds);
        }

        return 0;
    }

    // Calculate the next daily alarm time.
    public DateTime GetNextDailyTime(AlarmItem alarm)
    {
        DateTime now = DateTime.Now;
        DateTime nextTime = new DateTime(now.Year, now.Month, now.Day, alarm.hour, alarm.minute, alarm.second);
        if (nextTime <= now)
        {
            nextTime = nextTime.AddDays(1);
        }

        int guardCount = 0;
        while (!IsWeekdayAllowed(alarm, nextTime.DayOfWeek) && guardCount < 8)
        {
            nextTime = nextTime.AddDays(1);
            guardCount++;
        }

        return nextTime;
    }

    // Return a relative timer state.
    public string GetRelativeTimerState(string id)
    {
        AlarmItem alarm = FindAlarm(id);
        if (alarm == null || alarm.alarmType != AlarmType.RelativeTimer)
        {
            return AlarmRuntimeState.Idle;
        }

        return GetRelativeRuntime(alarm).state;
    }

    // Return whether an alarm is ringing.
    public bool IsAlarmRinging(string id)
    {
        return ringingAlarmIds.Contains(id);
    }

    // Update alarm states.
    public void UpdateAlarmStates()
    {
        for (int i = 0; i < saveData.alarms.Count; i++)
        {
            AlarmItem alarm = saveData.alarms[i];
            if (alarm.alarmType == AlarmType.DailyTime)
            {
                UpdateDailyAlarmState(alarm);
            }
            else if (alarm.alarmType == AlarmType.RelativeTimer)
            {
                UpdateRelativeTimerState(alarm);
            }
        }
    }

    // Detect due daily alarms.
    private void UpdateDailyAlarmState(AlarmItem alarm)
    {
        if (!alarm.enabled)
        {
            return;
        }

        DateTime now = DateTime.Now;
        if (!IsWeekdayAllowed(alarm, now.DayOfWeek))
        {
            return;
        }

        DateTime scheduledTime = new DateTime(now.Year, now.Month, now.Day, alarm.hour, alarm.minute, alarm.second);
        if (now < scheduledTime)
        {
            return;
        }

        if ((now - scheduledTime).TotalSeconds > dailyTriggerWindowSeconds)
        {
            return;
        }

        string triggerKey = alarm.id + "_" + scheduledTime.ToString("yyyyMMdd_HHmmss");
        if (dailyTriggerKeys.Contains(triggerKey))
        {
            return;
        }

        dailyTriggerKeys.Add(triggerKey);
        RingAlarm(alarm);
    }

    // Detect due relative timers.
    private void UpdateRelativeTimerState(AlarmItem alarm)
    {
        AlarmTimerRuntime runtime = GetRelativeRuntime(alarm);
        if (runtime.state != AlarmRuntimeState.Running)
        {
            return;
        }

        if (GetRemainingSeconds(alarm) > 0)
        {
            return;
        }

        runtime.state = AlarmRuntimeState.Ringing;
        runtime.pausedRemainingSeconds = 0;
        RingAlarm(alarm);
    }

    // Ring an alarm.
    private void RingAlarm(AlarmItem alarm)
    {
        ringingAlarmIds.Add(alarm.id);

        if (alarm.soundType == AlarmSoundType.Character)
        {
            PlayCharacterGuidance(alarm);
        }
        else if (audioPlayer != null)
        {
            audioPlayer.PlayAlarmClip(alarm);
        }

        if (AlarmRang != null)
        {
            AlarmRang.Invoke(alarm);
        }

        NotifyAlarmsChanged();
    }

    // Find an alarm by id.
    private AlarmItem FindAlarm(string id)
    {
        for (int i = 0; i < saveData.alarms.Count; i++)
        {
            AlarmItem alarm = saveData.alarms[i];
            if (alarm.id == id)
            {
                return alarm;
            }
        }

        return null;
    }

    // Normalize saved data.
    private void NormalizeAlarms()
    {
        if (saveData == null)
        {
            saveData = new AlarmSaveData();
        }

        if (saveData.alarms == null)
        {
            saveData.alarms = new List<AlarmItem>();
        }

        for (int i = 0; i < saveData.alarms.Count; i++)
        {
            AlarmItem alarm = saveData.alarms[i];
            if (string.IsNullOrEmpty(alarm.id))
            {
                alarm.id = CreateAlarmId();
            }

            if (string.IsNullOrEmpty(alarm.alarmType))
            {
                alarm.alarmType = AlarmType.DailyTime;
            }

            if (string.IsNullOrEmpty(alarm.timeDisplayMode))
            {
                alarm.timeDisplayMode = AlarmTimeDisplayMode.Hour24;
            }

            alarm.soundType = NormalizeSoundType(alarm.soundType);

            if (alarm.alarmType == AlarmType.DailyTime)
            {
                NormalizeWeekdays(alarm);
                ClampDailyTime(alarm);
            }
            else if (alarm.alarmType == AlarmType.RelativeTimer)
            {
                alarm.durationSeconds = Mathf.Max(1, alarm.durationSeconds);
            }
        }
    }

    // Rebuild runtime states.
    private void RebuildRuntimeStates()
    {
        relativeTimerStates.Clear();
        for (int i = 0; i < saveData.alarms.Count; i++)
        {
            AlarmItem alarm = saveData.alarms[i];
            if (alarm.alarmType == AlarmType.RelativeTimer)
            {
                relativeTimerStates[alarm.id] = CreateRelativeRuntime(alarm);
            }
        }
    }

    // Create a relative timer runtime state.
    private AlarmTimerRuntime CreateRelativeRuntime(AlarmItem alarm)
    {
        AlarmTimerRuntime runtime = new AlarmTimerRuntime();
        runtime.state = AlarmRuntimeState.Idle;
        runtime.startedAt = 0f;
        runtime.pausedRemainingSeconds = Mathf.Max(1, alarm.durationSeconds);
        return runtime;
    }

    // Return a relative timer runtime state.
    private AlarmTimerRuntime GetRelativeRuntime(AlarmItem alarm)
    {
        if (!relativeTimerStates.ContainsKey(alarm.id))
        {
            relativeTimerStates[alarm.id] = CreateRelativeRuntime(alarm);
        }

        return relativeTimerStates[alarm.id];
    }

    // Clamp daily time values.
    private void ClampDailyTime(AlarmItem alarm)
    {
        alarm.hour = Mathf.Clamp(alarm.hour, 0, 23);
        alarm.minute = Mathf.Clamp(alarm.minute, 0, 59);
        alarm.second = 0;
        alarm.timeDisplayMode = NormalizeTimeDisplayMode(alarm.timeDisplayMode);
    }

    // Normalize the time display mode.
    private string NormalizeTimeDisplayMode(string timeDisplayMode)
    {
        if (timeDisplayMode == AlarmTimeDisplayMode.Hour24)
        {
            return AlarmTimeDisplayMode.Hour24;
        }

        if (timeDisplayMode == AlarmTimeDisplayMode.AM)
        {
            return AlarmTimeDisplayMode.AM;
        }

        if (timeDisplayMode == AlarmTimeDisplayMode.PM)
        {
            return AlarmTimeDisplayMode.PM;
        }

        return AlarmTimeDisplayMode.Hour24;
    }

    // Normalize the sound category.
    private string NormalizeSoundType(string soundType)
    {
        if (soundType == AlarmSoundType.Character)
        {
            return AlarmSoundType.Character;
        }

        if (soundType == AlarmSoundType.Music)
        {
            return AlarmSoundType.Music;
        }

        return AlarmSoundType.Music;
    }

    // Fill weekday defaults for older save files.
    private void NormalizeWeekdays(AlarmItem alarm)
    {
        if (alarm.weekdaySelectionInitialized)
        {
            return;
        }

        alarm.mondayEnabled = true;
        alarm.tuesdayEnabled = true;
        alarm.wednesdayEnabled = true;
        alarm.thursdayEnabled = true;
        alarm.fridayEnabled = true;
        alarm.saturdayEnabled = true;
        alarm.sundayEnabled = true;
        alarm.weekdaySelectionInitialized = true;
    }

    // Return whether an alarm can ring on a day.
    private bool IsWeekdayAllowed(AlarmItem alarm, DayOfWeek dayOfWeek)
    {
        if (alarm.excludeWeekend && IsWeekend(dayOfWeek))
        {
            return false;
        }

        return GetWeekdayValue(alarm, dayOfWeek);
    }

    // Return one weekday value.
    public bool GetWeekdayValue(AlarmItem alarm, DayOfWeek dayOfWeek)
    {
        if (dayOfWeek == DayOfWeek.Monday)
        {
            return alarm.mondayEnabled;
        }

        if (dayOfWeek == DayOfWeek.Tuesday)
        {
            return alarm.tuesdayEnabled;
        }

        if (dayOfWeek == DayOfWeek.Wednesday)
        {
            return alarm.wednesdayEnabled;
        }

        if (dayOfWeek == DayOfWeek.Thursday)
        {
            return alarm.thursdayEnabled;
        }

        if (dayOfWeek == DayOfWeek.Friday)
        {
            return alarm.fridayEnabled;
        }

        if (dayOfWeek == DayOfWeek.Saturday)
        {
            return alarm.saturdayEnabled;
        }

        return alarm.sundayEnabled;
    }

    // Set one weekday value.
    private void SetWeekdayValue(AlarmItem alarm, DayOfWeek dayOfWeek, bool enabled)
    {
        if (dayOfWeek == DayOfWeek.Monday)
        {
            alarm.mondayEnabled = enabled;
        }
        else if (dayOfWeek == DayOfWeek.Tuesday)
        {
            alarm.tuesdayEnabled = enabled;
        }
        else if (dayOfWeek == DayOfWeek.Wednesday)
        {
            alarm.wednesdayEnabled = enabled;
        }
        else if (dayOfWeek == DayOfWeek.Thursday)
        {
            alarm.thursdayEnabled = enabled;
        }
        else if (dayOfWeek == DayOfWeek.Friday)
        {
            alarm.fridayEnabled = enabled;
        }
        else if (dayOfWeek == DayOfWeek.Saturday)
        {
            alarm.saturdayEnabled = enabled;
        }
        else if (dayOfWeek == DayOfWeek.Sunday)
        {
            alarm.sundayEnabled = enabled;
        }
    }

    // Return whether a date is on the weekend.
    private bool IsWeekend(DateTime time)
    {
        return IsWeekend(time.DayOfWeek);
    }

    // Return whether a day is on the weekend.
    private bool IsWeekend(DayOfWeek dayOfWeek)
    {
        if (dayOfWeek == DayOfWeek.Saturday)
        {
            return true;
        }

        if (dayOfWeek == DayOfWeek.Sunday)
        {
            return true;
        }

        return false;
    }

    // Placeholder for future AI character guidance.
    private void PlayCharacterGuidance(AlarmItem alarm)
    {
        Debug.Log("[AlarmManager] Character guidance placeholder: " + alarm.title);
    }

    // Create an alarm id.
    private string CreateAlarmId()
    {
        return "alarm_" + Guid.NewGuid().ToString("N");
    }

    // Notify listeners about alarm changes.
    private void NotifyAlarmsChanged()
    {
        if (AlarmsChanged != null)
        {
            AlarmsChanged.Invoke();
        }
    }
}
