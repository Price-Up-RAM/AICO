# -*- coding: utf-8 -*-
# Phase 5 보강 — planner 경로가 함수 목록 필터를 우회하는 구멍 + 경로 태그(7-1 D)
import sys, os

ROOT = os.path.expanduser("~/mnt/AICO/Assets/Scripts")
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


# ===== D1. 경로 태그 필드 =====
patch("ApiVlRouterManager.cs",
"""    private string currentSttLang = "";  // STT가 감지한 입력 언어. 비어 있으면 설정값을 쓴다""",
"""    private string currentSttLang = "";  // STT가 감지한 입력 언어. 비어 있으면 설정값을 쓴다
    private string routerActionChannel = "?";  // 지금 실행 중인 액션이 어느 경로로 왔는지 (스킬/planner)""",
"D1. routerActionChannel 필드")

# ===== D2. 두 경로에 태그 부여 =====
patch("ApiVlRouterManager.cs",
"""        Debug.Log($"[VlRouterRun] act data: {data.ToString()}");
        if (ApiVlRouterResponseManager.Instance.TryHandleRouterToolCall(eventData, data, offsetX, offsetY, TryProcessReplyListFromRouterData, ExecuteRouterFunction))
        {
            return;
        }

        TryHandlePlannerAction(data, offsetX, offsetY);""",
"""        Debug.Log($"[VlRouterRun] act data: {data.ToString()}");

        // 액션은 스킬/툴콜 경로와 planner 경로 두 갈래로 들어오는데 종착점은 ExecuteRouterFunction 하나다.
        // 로그에 어느 경로인지 남기지 않으면 콜스택을 세야 갈린다 (Kickoff Guide 7-1 D).
        routerActionChannel = "스킬";
        if (ApiVlRouterResponseManager.Instance.TryHandleRouterToolCall(eventData, data, offsetX, offsetY, TryProcessReplyListFromRouterData, ExecuteRouterFunction))
        {
            return;
        }

        routerActionChannel = "planner";
        TryHandlePlannerAction(data, offsetX, offsetY);""",
"D2. 경로 태그 부여")

# ===== D3. ExecuteRouterFunction MR 가드 =====
patch("ApiVlRouterManager.cs",
"""    // Router function 실행
    private void ExecuteRouterFunction(string functionName, JObject parameters, int offsetX, int offsetY)
    {
        switch (functionName)
        {""",
"""    // MR에서 실행 불가능한 라우터 액션.
    // 주의: 이 switch의 case는 ApiAgentFunctionManager의 함수 레지스트리와 별개다 —
    // planner 액션은 unity_functions_list를 거치지 않고 이름으로 직접 들어오므로
    // 레지스트리 필터(StripMRUnsupportedFunctions)로는 막히지 않는다.
    private static readonly string[] MRBlockedRouterFunctions =
    {
        // ScreenshotManager 데스크톱 캡처 + ClipboardManager(Win32) 의존
        "capture_screenshot", "function_request_screenshot", "screenshot",
        // ExecutorMouseAction — 데스크톱 커서 클릭. MR에는 클릭할 화면이 없다
        "click", "function_request_click", "REQUEST_CLICK"
    };

    // Router function 실행
    private void ExecuteRouterFunction(string functionName, JObject parameters, int offsetX, int offsetY)
    {
        Debug.Log($"[VlRouterRun/{routerActionChannel}] 액션 실행: {functionName}");

#if UNITY_ANDROID || UNITY_EDITOR
        if (Array.IndexOf(MRBlockedRouterFunctions, functionName) >= 0)
        {
            Debug.LogWarning($"[VlRouterRun/{routerActionChannel}] '{functionName}'는 MR 미지원이라 실행하지 않는다 (데스크톱 화면·마우스·클립보드 의존)");
            return;
        }
#endif

        switch (functionName)
        {""",
"D3. ExecuteRouterFunction MR 가드 + 경로 로그")

# ===== D4. ExecuteAction 실행부 가드 =====
patch("ApiAgentFunctionManager.cs",
"""        UnityEngine.Debug.Log($"[ApiAgentFunctionManager] ExecuteAction 호출됨: {functionName}");

        if (functionName == "test")""",
"""        UnityEngine.Debug.Log($"[ApiAgentFunctionManager] ExecuteAction 호출됨: {functionName}");

#if UNITY_ANDROID || UNITY_EDITOR
        // 목록에서 빼는 것만으로는 부족하다 — planner 액션과 저장된 스킬은 unity_functions_list를
        // 거치지 않고 이름으로 직접 들어온다. 실행부에서도 막아야 NRE로 라우터 세션이 죽지 않는다.
        if (Array.IndexOf(MRUnsupportedFunctions, functionName) >= 0)
        {
            UnityEngine.Debug.LogWarning($"[AgentFunc/MR] '{functionName}'는 MR 미지원이라 실행하지 않는다");
            onComplete?.Invoke(false, $"'{functionName}'은 MR 환경에서 지원하지 않는 기능입니다.");
            return;
        }
#endif

        if (functionName == "test")""",
"D4. ExecuteAction 실행부 가드")

sys.exit(0 if ok else 1)
