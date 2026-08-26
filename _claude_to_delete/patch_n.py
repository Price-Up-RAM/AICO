# -*- coding: utf-8 -*-
# alarm 4종 구현: alarm_get_list / alarm_set / alarm_delete / alarm_toggle
import sys, os

ROOT = os.path.expanduser("~/mnt/UnityProject--AICO/Assets/Scripts")
ok = True


def eol_of(p):
    b = open(p, "rb").read()
    crlf = b.count(b"\r\n")
    if crlf > (b.count(b"\n") - crlf):
        return "\r\n"
    return "\n"


def patch(path, old, new, label):
    global ok
    p = os.path.join(ROOT, path)
    e = eol_of(p)
    data = open(p, "rb").read()
    o = old.replace("\n", e).encode("utf-8")
    n = new.replace("\n", e).encode("utf-8")
    c = data.count(o)
    if c != 1:
        print("FAIL %s : 앵커 %d회 매치" % (label, c))
        ok = False
        return
    open(p, "wb").write(data.replace(o, n))
    print("OK   %s" % label)


# ===== N1. ExecuteAction 분기 4종 =====
patch("ApiAgentFunctionManager.cs",
"""        else if (functionName == "jukebox_play")""",
"""        else if (functionName == "alarm_get_list")
        {
            ExecuteAlarmGetList(onComplete);
        }
        else if (functionName == "alarm_set")
        {
            int hour = GetParam<int>(parameters, "hour", -1);
            int minute = GetParam<int>(parameters, "minute", 0);
            int second = GetParam<int>(parameters, "second", 0);
            string title = GetParam<string>(parameters, "title", "");
            ExecuteAlarmSet(hour, minute, second, title, onComplete);
        }
        else if (functionName == "alarm_delete")
        {
            string keyword = GetParam<string>(parameters, "keyword", "");
            ExecuteAlarmDelete(keyword, onComplete);
        }
        else if (functionName == "alarm_toggle")
        {
            string keyword = GetParam<string>(parameters, "keyword", "");
            ExecuteAlarmToggle(keyword, onComplete);
        }
        else if (functionName == "jukebox_play")""",
"N1. alarm 분기 4종")

# ===== N2. 구현 =====
patch("ApiAgentFunctionManager.cs",
"""    // ===== 주크박스 (Phase 5) =====""",
"""    // ===== 알람 (Phase 5) =====

    // 알람 하나를 사람이 읽을 수 있는 한 줄로 만든다.
    private string DescribeAlarm(AlarmManager manager, AlarmItem alarm)
    {
        string enabledText = "켜짐";
        if (!alarm.enabled)
        {
            enabledText = "꺼짐";
        }

        if (alarm.alarmType == AlarmType.RelativeTimer)
        {
            int remain = manager.GetRemainingSeconds(alarm);
            string state = manager.GetRelativeTimerState(alarm.id);
            return $"[타이머] {alarm.title} — 남은 {FormatDuration(remain)} ({state}, {enabledText})";
        }

        return $"[알람] {alarm.title} — 매일 {alarm.hour:00}:{alarm.minute:00} ({enabledText})";
    }

    private string FormatDuration(int totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "0초";
        }
        int hours = totalSeconds / 3600;
        int minutes = totalSeconds % 3600 / 60;
        int seconds = totalSeconds % 60;
        if (hours > 0)
        {
            return $"{hours}시간 {minutes}분";
        }
        if (minutes > 0)
        {
            return $"{minutes}분 {seconds}초";
        }
        return $"{seconds}초";
    }

    // 제목 부분 일치로 알람을 찾는다. 여러 개면 null을 반환하고 후보를 넘긴다.
    // (서버 카탈로그의 alarm_delete 설명이 "여러 개가 걸리면 후보를 알려주고 중단"이다)
    private AlarmItem FindAlarmByKeyword(AlarmManager manager, string keyword, out string message)
    {
        message = "";
        List<AlarmItem> alarms = manager.GetAlarms();
        if (alarms == null || alarms.Count == 0)
        {
            message = "설정된 알람이 없습니다.";
            return null;
        }

        if (string.IsNullOrEmpty(keyword))
        {
            // 키워드가 없고 알람이 하나뿐이면 그것으로 본다.
            if (alarms.Count == 1)
            {
                return alarms[0];
            }
            message = $"어느 알람인지 알려주세요. 현재 {alarms.Count}개: {BuildAlarmTitles(alarms)}";
            return null;
        }

        string needle = keyword.ToLower();
        List<AlarmItem> matched = new List<AlarmItem>();
        for (int i = 0; i < alarms.Count; i++)
        {
            string title = alarms[i].title;
            if (string.IsNullOrEmpty(title))
            {
                continue;
            }
            if (title.ToLower().Contains(needle))
            {
                matched.Add(alarms[i]);
            }
        }

        if (matched.Count == 0)
        {
            message = $"'{keyword}'에 해당하는 알람을 찾지 못했습니다. 현재 {alarms.Count}개: {BuildAlarmTitles(alarms)}";
            return null;
        }

        if (matched.Count > 1)
        {
            message = $"'{keyword}'와(과) 맞는 알람이 {matched.Count}개입니다: {BuildAlarmTitles(matched)}";
            return null;
        }

        return matched[0];
    }

    private string BuildAlarmTitles(List<AlarmItem> alarms)
    {
        List<string> titles = new List<string>();
        for (int i = 0; i < alarms.Count; i++)
        {
            if (!string.IsNullOrEmpty(alarms[i].title))
            {
                titles.Add(alarms[i].title);
            }
        }
        return string.Join(", ", titles.ToArray());
    }

    private void ExecuteAlarmGetList(Action<bool, string> onComplete)
    {
        AlarmManager manager = FindAlarmManager();
        if (manager == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/alarm] AlarmManager를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "알람 기능을 사용할 수 없습니다.");
            return;
        }

        List<AlarmItem> alarms = manager.GetAlarms();
        if (alarms == null || alarms.Count == 0)
        {
            UnityEngine.Debug.Log("[AgentFunc/alarm] 목록 조회: 0개");
            onComplete?.Invoke(true, "설정된 알람이 없습니다.");
            return;
        }

        List<string> lines = new List<string>();
        for (int i = 0; i < alarms.Count; i++)
        {
            lines.Add(DescribeAlarm(manager, alarms[i]));
        }

        string result = string.Join(" / ", lines.ToArray());
        UnityEngine.Debug.Log($"[AgentFunc/alarm] 목록 조회 {alarms.Count}개 | {result}");
        onComplete?.Invoke(true, $"알람 {alarms.Count}개: {result}");
    }

    private void ExecuteAlarmSet(int hour, int minute, int second, string title, Action<bool, string> onComplete)
    {
        if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59)
        {
            UnityEngine.Debug.LogWarning($"[AgentFunc/alarm] 시각이 유효하지 않다: {hour}:{minute}:{second}");
            onComplete?.Invoke(false, "알람 시각이 올바르지 않습니다. 몇 시 몇 분인지 알려주세요.");
            return;
        }

        AlarmManager manager = FindAlarmManager();
        if (manager == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/alarm] AlarmManager를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "알람 기능을 사용할 수 없습니다.");
            return;
        }

        string alarmTitle = title;
        if (string.IsNullOrEmpty(alarmTitle))
        {
            alarmTitle = $"알람 {hour:00}:{minute:00}";
        }

        AlarmItem alarm = manager.AddDailyAlarm(alarmTitle, hour, minute, second, "default_alarm");
        if (alarm == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/alarm] AddDailyAlarm이 null을 반환했다");
            onComplete?.Invoke(false, "알람을 추가하지 못했습니다.");
            return;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowAlarmMini();
        }

        UnityEngine.Debug.Log($"[AgentFunc/alarm] 매일 알람 생성: id={alarm.id} title={alarmTitle} {hour:00}:{minute:00}:{second:00}");
        onComplete?.Invoke(true, $"매일 {hour:00}시 {minute:00}분에 울리는 '{alarmTitle}' 알람을 추가했습니다.");
    }

    private void ExecuteAlarmDelete(string keyword, Action<bool, string> onComplete)
    {
        AlarmManager manager = FindAlarmManager();
        if (manager == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/alarm] AlarmManager를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "알람 기능을 사용할 수 없습니다.");
            return;
        }

        AlarmItem target = FindAlarmByKeyword(manager, keyword, out string message);
        if (target == null)
        {
            UnityEngine.Debug.LogWarning($"[AgentFunc/alarm] 삭제 대상 특정 실패 | keyword='{keyword}' | {message}");
            onComplete?.Invoke(false, message);
            return;
        }

        string deletedTitle = target.title;
        manager.DeleteAlarm(target.id);

        // '호출했다'와 '지워졌다'는 다른 사실이다 (Kickoff Guide 4-58).
        List<AlarmItem> after = manager.GetAlarms();
        bool stillThere = false;
        if (after != null)
        {
            for (int i = 0; i < after.Count; i++)
            {
                if (after[i].id == target.id)
                {
                    stillThere = true;
                    break;
                }
            }
        }

        if (stillThere)
        {
            UnityEngine.Debug.LogWarning($"[AgentFunc/alarm] 삭제 실패 - 여전히 목록에 있다: {deletedTitle}");
            onComplete?.Invoke(false, "알람을 삭제하지 못했습니다.");
            return;
        }

        UnityEngine.Debug.Log($"[AgentFunc/alarm] 삭제: '{deletedTitle}' (남은 {(after == null ? 0 : after.Count)}개)");
        onComplete?.Invoke(true, $"'{deletedTitle}' 알람을 삭제했습니다.");
    }

    private void ExecuteAlarmToggle(string keyword, Action<bool, string> onComplete)
    {
        AlarmManager manager = FindAlarmManager();
        if (manager == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/alarm] AlarmManager를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "알람 기능을 사용할 수 없습니다.");
            return;
        }

        AlarmItem target = FindAlarmByKeyword(manager, keyword, out string message);
        if (target == null)
        {
            UnityEngine.Debug.LogWarning($"[AgentFunc/alarm] 토글 대상 특정 실패 | keyword='{keyword}' | {message}");
            onComplete?.Invoke(false, message);
            return;
        }

        bool before = target.enabled;
        manager.ToggleEnabled(target.id);

        // 상태가 실제로 뒤집혔는지 확인한다 (4-58).
        bool after = target.enabled;
        if (after == before)
        {
            UnityEngine.Debug.LogWarning($"[AgentFunc/alarm] 토글 실패 - 상태 불변: '{target.title}' enabled={after}");
            onComplete?.Invoke(false, "알람 상태를 바꾸지 못했습니다.");
            return;
        }

        string stateText = "껐습니다";
        if (after)
        {
            stateText = "켰습니다";
        }

        UnityEngine.Debug.Log($"[AgentFunc/alarm] 토글: '{target.title}' {before} → {after}");
        onComplete?.Invoke(true, $"'{target.title}' 알람을 {stateText}.");
    }

    // ===== 주크박스 (Phase 5) =====""",
"N2. alarm 구현")

# ===== N3. 레지스트리 등록 =====
patch("ApiAgentFunctionManager.cs",
"""            // Alarm
            F("alarm_set_timer",""",
"""            // Alarm
            F("alarm_get_list", "alarm", "설정된 알람·타이머 목록을 남은 시간과 함께 조회", false),
            F("alarm_set", "alarm", "매일 지정한 시각에 울리는 알람을 추가 (예: 아침 7시에 깨워줘)", false, new JArray {
                P("hour", "int", true, "시 (0~23)"),
                P("minute", "int", false, "분 (0~59)"),
                P("second", "int", false, "초 (0~59)"),
                P("title", "string", false, "알람 제목/메모")
            }),
            F("alarm_delete", "alarm", "제목 키워드로 알람/타이머를 찾아 삭제. 여러 개가 걸리면 후보를 알려주고 중단", false, new JArray {
                P("keyword", "string", false, "찾을 제목 키워드. 알람이 하나뿐이면 생략 가능")
            }),
            F("alarm_toggle", "alarm", "제목 키워드로 알람을 찾아 활성/비활성 상태를 변경", false, new JArray {
                P("keyword", "string", false, "찾을 제목 키워드. 알람이 하나뿐이면 생략 가능")
            }),
            F("alarm_set_timer",""",
"N3. alarm 레지스트리 등록")

sys.exit(0 if ok else 1)
