using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class CalendarTool
{
    public class CalendarDisplayData 
    {
        public int day;
        public bool isPartOfMonth;
        public DateTime dateTime;

        public void InitCaledarDisplaydata(int p_newDay, bool p_isPartOfMonth, DateTime p_newDateTime) 
        {
            day = p_newDay;
            isPartOfMonth = p_isPartOfMonth;
            dateTime = p_newDateTime;
        }
    }
    public static List<CalendarDisplayData> GetNumberDaysOfAMonth(int p_year, int p_month, int p_daymaxCount)
    {
        List<CalendarDisplayData> procssedMonthDates = new List<CalendarDisplayData>();
        for (int ctr = 0; ctr < p_daymaxCount; ++ctr) 
        {
            procssedMonthDates.Add(new CalendarDisplayData());
        }

        DateTime dateTime = new DateTime(p_year, p_month, 1);

        int x = 0;
        int y = 0;

        //fill current dates of target month
        for (x = (int)dateTime.DayOfWeek, y = 1; x < DateTime.DaysInMonth(p_year, p_month) + (int)dateTime.DayOfWeek; ++x, ++y)
        {
            procssedMonthDates[x].InitCaledarDisplaydata(y, true, dateTime.AddDays(y - 1));
        }

        //fill remaining days after target month
        for (y = 1; x < p_daymaxCount; ++x, ++y)
        {
            procssedMonthDates[x].InitCaledarDisplaydata(y, false, DateTime.Now.AddYears(-1000));
        }

        //fill remaining days before target month
        DateTime lastMonth = new DateTime(p_year, p_month, 1);
        lastMonth = lastMonth.AddMonths(-1);
        for (x = DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month), y = (int)dateTime.DayOfWeek - 1; y >= 0; --x, --y)
        {
            procssedMonthDates[y].InitCaledarDisplaydata(x, false, DateTime.Now.AddYears(-1000));
        }

        return procssedMonthDates;
    }
}
