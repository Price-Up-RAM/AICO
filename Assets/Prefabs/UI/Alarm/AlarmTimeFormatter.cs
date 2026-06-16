using System;

public static class AlarmTimeFormatter
{
    // Format remaining seconds for UI.
    public static string FormatRemainingSeconds(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2}", hours, minutes, seconds);
        }

        return string.Format("{0:D2}:{1:D2}", minutes, seconds);
    }

    // Format a daily alarm time for UI.
    public static string FormatDailyTime(AlarmItem alarm)
    {
        if (alarm == null)
        {
            return string.Empty;
        }

        if (alarm.timeDisplayMode == AlarmTimeDisplayMode.Hour24)
        {
            return string.Format("{0:D2}:{1:D2}", alarm.hour, alarm.minute);
        }

        int displayHour = alarm.hour % 12;
        if (displayHour == 0)
        {
            displayHour = 12;
        }

        string period = "AM";
        if (alarm.timeDisplayMode == AlarmTimeDisplayMode.PM)
        {
            period = "PM";
        }
        else if (alarm.hour >= 12)
        {
            period = "PM";
        }

        return string.Format("{0} {1}:{2:D2}", period, displayHour, alarm.minute);
    }

    // Format a date time for UI.
    public static string FormatDateTime(DateTime time)
    {
        return time.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
