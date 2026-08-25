# -*- coding: utf-8 -*-
# Phase 5 보강 2 — alarm_set_timer 구현 + 경로 태그 진입점 3곳
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


# ===== E1. 경로 태그를 진입점 3곳에서 각자 세팅 =====
# HandleRouterActEvent/DoneEvent 어느 쪽에서 불려도 항상 맞도록 함수 자신이 태그를 정한다.
patch("ApiVlRouterManager.cs",
"""        routerActionChannel = "스킬";
        if (ApiVlRouterResponseManager.Instance.TryHandleRouterToolCall(eventData, data, offsetX, offsetY, TryProcessReplyListFromRouterData, ExecuteRouterFunction))
        {
            return;
        }

        routerActionChannel = "planner";
        TryHandlePlannerAction(data, offsetX, offsetY);""",
"""        routerActionChannel = "툴콜";
        if (ApiVlRouterResponseManager.Instance.TryHandleRouterToolCall(eventData, data, offsetX, offsetY, TryProcessReplyListFromRouterData, ExecuteRouterFunction))
        {
            return;
        }

        TryHandlePlannerAction(data, offsetX, offsetY);""",
"E1a. act 경로 태그 정리")

patch("ApiVlRouterManager.cs",
"""        if (ApiVlRouterResponseManager.Instance.TryHandleRouterToolCall(eventData, data, offsetX, offsetY, TryProcessReplyListFromRouterData, ExecuteRouterFunction))
        {
            return;
        }

        if (TryHandleUnityEnvelope(data, offsetX, offsetY))
        {
            return;
        }

        TryHandlePlannerAction(data, offsetX, offsetY);
    }""",
"""        routerActionChannel = "툴콜";
        if (ApiVlRouterResponseManager.Instance.TryHandleRouterToolCall(eventData, data, offsetX, offsetY, TryProcessReplyListFromRouterData, ExecuteRouterFunction))
        {
            return;
        }

        if (TryHandleUnityEnvelope(data, offsetX, offsetY))
        {
            return;
        }

        TryHandlePlannerAction(data, offsetX, offsetY);
    }""",
"E1b. done 경로 태그")

patch("ApiVlRouterManager.cs",
"""        Debug.Log($"[VlRouterRun] unity envelope function={functionName}, parameters={parameterLog}");
        ExecuteRouterFunction(functionName, parameters, offsetX, offsetY);
        return true;""",
"""        // 진입점이 자기 태그를 정한다 — 호출부(act/done)가 어디든 항상 맞는다 (Kickoff Guide 7-1 D)
        routerActionChannel = "envelope";
        Debug.Log($"[VlRouterRun] unity envelope function={functionName}, parameters={parameterLog}");
        ExecuteRouterFunction(functionName, parameters, offsetX, offsetY);
        return true;""",
"E1c. envelope 태그")

patch("ApiVlRouterManager.cs",
"""    private bool TryHandlePlannerAction(JObject data, int offsetX, int offsetY)
    {
        string action = data["action"]?.Value<string>() ?? "";""",
"""    private bool TryHandlePlannerAction(JObject data, int offsetX, int offsetY)
    {
        routerActionChannel = "planner";
        string action = data["action"]?.Value<string>() ?? "";""",
"E1d. planner 태그")

# ===== E2. alarm_set_timer 실행부 =====
patch("ApiAgentFunctionManager.cs",
"""        if (functionName == "test")
        {
            UnityEngine.Debug.Log("[ApiAgentFunctionManager] 테스트 기능 실행됨");
            onComplete?.Invoke(true, "테스트 성공");
        }""",
"""        if (functionName == "test")
        {
            UnityEngine.Debug.Log("[ApiAgentFunctionManager] 테스트 기능 실행됨");
            onComplete?.Invoke(true, "테스트 성공");
        }
        else if (functionName == "alarm_set_timer")
        {
            // 서버가 unity_envelope로 보내는 상대 타이머. owner=unity라 클라이언트가 실행한다.
            // ApiVlRouterResponseManager의 tool_alarm_maker 경로와 동작이 같다 —
            // 그쪽은 toolTarget 기반이라 envelope 형식으로 오면 타지 않는다.
            int durationSeconds = GetParam<int>(parameters, "duration_seconds", 0);
            string title = GetParam<string>(parameters, "title", "");
            ExecuteAlarmSetTimer(durationSeconds, title, onComplete);
        }""",
"E2a. alarm_set_timer 분기")

patch("ApiAgentFunctionManager.cs",
"""    // 함수 이름 목록만 JSON 배열로 반환""",
"""    // 상대 타이머 생성 후 즉시 시작한다. AlarmManager는 MR 씬에 존재한다(2026-08-24 GUID 실측 1개).
    private void ExecuteAlarmSetTimer(int durationSeconds, string title, Action<bool, string> onComplete)
    {
        if (durationSeconds <= 0)
        {
            UnityEngine.Debug.LogWarning($"[AgentFunc/alarm] duration_seconds가 유효하지 않다: {durationSeconds}");
            onComplete?.Invoke(false, "타이머 시간이 지정되지 않았습니다.");
            return;
        }

        AlarmManager alarmManager = FindAlarmManager();
        if (alarmManager == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/alarm] AlarmManager를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "알람 기능을 사용할 수 없습니다.");
            return;
        }

        string alarmTitle = title;
        if (string.IsNullOrEmpty(alarmTitle))
        {
            alarmTitle = BuildDefaultAlarmTitle(durationSeconds);
        }

        AlarmItem alarm = alarmManager.AddRelativeTimer(alarmTitle, durationSeconds, "default_alarm");
        alarmManager.StartRelativeTimer(alarm.id);
        UIManager.Instance.ShowAlarmMini();

        UnityEngine.Debug.Log($"[AgentFunc/alarm] 타이머 생성: id={alarm.id}, title={alarmTitle}, seconds={durationSeconds}");
        onComplete?.Invoke(true, $"{alarmTitle} 타이머를 {durationSeconds}초 뒤로 설정했습니다.");
    }

    // 비활성 오브젝트에 붙어 있어도 찾는다 (알람 UI는 닫힌 상태로 저장돼 있다)
    private AlarmManager FindAlarmManager()
    {
        AlarmManager[] components = Resources.FindObjectsOfTypeAll<AlarmManager>();
        for (int i = 0; i < components.Length; i++)
        {
            AlarmManager component = components[i];
            if (component == null || component.gameObject == null)
            {
                continue;
            }

            if (!component.gameObject.scene.IsValid())
            {
                continue;
            }

            return component;
        }

        return null;
    }

    private string BuildDefaultAlarmTitle(int durationSeconds)
    {
        int hours = durationSeconds / 3600;
        int minutes = durationSeconds % 3600 / 60;
        int seconds = durationSeconds % 60;
        if (hours > 0)
        {
            return $"타이머 {hours}시간 {minutes}분";
        }
        if (minutes > 0)
        {
            return $"타이머 {minutes}분";
        }
        return $"타이머 {seconds}초";
    }

    // 함수 이름 목록만 JSON 배열로 반환""",
"E2b. ExecuteAlarmSetTimer 헬퍼")

# ===== E3. 레지스트리에도 등록 (목록과 실행부 일치) =====
patch("ApiAgentFunctionManager.cs",
"""            // Debug
            F("test", "debug", "연결 테스트용. 항상 성공을 반환합니다.", false)""",
"""            // Alarm
            F("alarm_set_timer", "alarm", "지금부터 지정한 시간 뒤에 울리는 타이머를 추가하고 즉시 시작합니다.", false, new JArray {
                P("duration_seconds", "int", true, "몇 초 뒤에 울릴지 (초 단위 총합)"),
                P("title", "string", false, "타이머 제목/메모")
            }),

            // Debug
            F("test", "debug", "연결 테스트용. 항상 성공을 반환합니다.", false)""",
"E3. 레지스트리 등록")

sys.exit(0 if ok else 1)
