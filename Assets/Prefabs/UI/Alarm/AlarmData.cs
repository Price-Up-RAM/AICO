using System;
using System.Collections.Generic;

public static class AlarmType
{
    public const string DailyTime = "DailyTime";
    public const string RelativeTimer = "RelativeTimer";
}

public static class AlarmTimeDisplayMode
{
    public const string Hour24 = "Hour24";
    public const string AM = "AM";
    public const string PM = "PM";
}

public static class AlarmRuntimeState
{
    public const string Idle = "Idle";
    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Ringing = "Ringing";
}

public static class AlarmSoundType
{
    public const string Character = "Character";
    public const string Music = "Music";
}

[Serializable]
public class AlarmItem
{
    public string id;
    public string title;
    public string alarmType;
    public string audioClipId;
    public string soundType;

    public bool enabled;
    public int hour;
    public int minute;
    public int second;
    public string timeDisplayMode;
    public bool excludeWeekend;
    public bool mondayEnabled;
    public bool tuesdayEnabled;
    public bool wednesdayEnabled;
    public bool thursdayEnabled;
    public bool fridayEnabled;
    public bool saturdayEnabled;
    public bool sundayEnabled;
    public bool weekdaySelectionInitialized;

    public int durationSeconds;

    // Create a saveable daily alarm.
    public static AlarmItem CreateDailyAlarm(string id, string title, int hour, int minute, int second, string audioClipId)
    {
        AlarmItem alarm = new AlarmItem();
        alarm.id = id;
        alarm.title = title;
        alarm.alarmType = AlarmType.DailyTime;
        alarm.audioClipId = audioClipId;
        alarm.soundType = AlarmSoundType.Music;
        alarm.enabled = true;
        alarm.hour = hour;
        alarm.minute = minute;
        alarm.second = 0;
        alarm.timeDisplayMode = AlarmTimeDisplayMode.Hour24;
        alarm.excludeWeekend = false;
        alarm.mondayEnabled = true;
        alarm.tuesdayEnabled = true;
        alarm.wednesdayEnabled = true;
        alarm.thursdayEnabled = true;
        alarm.fridayEnabled = true;
        alarm.saturdayEnabled = true;
        alarm.sundayEnabled = true;
        alarm.weekdaySelectionInitialized = true;
        alarm.durationSeconds = 0;
        return alarm;
    }

    // Create a saveable relative timer.
    public static AlarmItem CreateRelativeTimer(string id, string title, int durationSeconds, string audioClipId)
    {
        AlarmItem alarm = new AlarmItem();
        alarm.id = id;
        alarm.title = title;
        alarm.alarmType = AlarmType.RelativeTimer;
        alarm.audioClipId = audioClipId;
        alarm.soundType = AlarmSoundType.Music;
        alarm.enabled = true;
        alarm.hour = 0;
        alarm.minute = 0;
        alarm.second = 0;
        alarm.timeDisplayMode = AlarmTimeDisplayMode.Hour24;
        alarm.excludeWeekend = false;
        alarm.mondayEnabled = true;
        alarm.tuesdayEnabled = true;
        alarm.wednesdayEnabled = true;
        alarm.thursdayEnabled = true;
        alarm.fridayEnabled = true;
        alarm.saturdayEnabled = true;
        alarm.sundayEnabled = true;
        alarm.weekdaySelectionInitialized = true;
        alarm.durationSeconds = Math.Max(1, durationSeconds);
        return alarm;
    }
}

[Serializable]
public class AlarmSaveData
{
    public List<AlarmItem> alarms = new List<AlarmItem>();
}

public class AlarmTimerRuntime
{
    public string state = AlarmRuntimeState.Idle;  // Current relative timer state
    public float startedAt;  // Runtime start time
    public int pausedRemainingSeconds;  // Remaining seconds when paused
}
