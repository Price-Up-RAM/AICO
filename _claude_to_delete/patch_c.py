# -*- coding: utf-8 -*-
# Phase 5 — 라우터 배선 본체 (STT 2곳 / 시그니처+가드 / 함수 필터)
# 파일별 개행 자동 판별
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


# ===== C1. ExecuteVlRouterRun 시그니처 확장 (sttLang) =====
patch("ApiVlRouterManager.cs",
"""    public void ExecuteVlRouterRun(
        string query,
        Action<JObject> onEvent = null,
        Action<bool, string> onComplete = null,
        int maxRetry = 5
    )
    {
        currentOnEvent = onEvent;""",
"""    // sttLang: STT가 자동 감지한 입력 언어. 빈 문자열이면 SettingManager의 ai_language_in을 그대로 쓴다.
    // (데스크톱 SendBtn 경로는 인자를 넘기지 않으므로 기존 동작 그대로다)
    public void ExecuteVlRouterRun(
        string query,
        Action<JObject> onEvent = null,
        Action<bool, string> onComplete = null,
        int maxRetry = 5,
        string sttLang = ""
    )
    {
        currentSttLang = sttLang;
        currentOnEvent = onEvent;""",
"C1. ExecuteVlRouterRun 시그니처")

patch("ApiVlRouterManager.cs",
"""    private string currentIntentImage = "off";  // 이번 run의 이미지 전송 모드 (off/auto/force)""",
"""    private string currentIntentImage = "off";  // 이번 run의 이미지 전송 모드 (off/auto/force)
    private string currentSttLang = "";  // STT가 감지한 입력 언어. 비어 있으면 설정값을 쓴다""",
"C2. currentSttLang 필드")

patch("ApiVlRouterManager.cs",
"""            WriteFormField(writer, boundary, "ai_language_in", SettingManager.Instance.settings.ai_language_in ?? "");""",
"""            // STT가 감지한 입력 언어가 있으면 설정값 대신 그것을 보낸다.
            // conversation_stream 경로와 같은 규칙이다 (APIManager.cs의 ai_lang_in 처리).
            string languageIn = SettingManager.Instance.settings.ai_language_in ?? "";
            if (!string.IsNullOrEmpty(currentSttLang))
            {
                languageIn = currentSttLang;
            }
            WriteFormField(writer, boundary, "ai_language_in", languageIn);""",
"C3. ai_language_in 전송부")

# ===== C4. intent_image MR 가드 =====
patch("ApiVlRouterManager.cs",
"""            Debug.Log($"[VlRouterRun] Image Info - ChatBalloon (IsChatting=true): intent_image={balloonImageInfo}");
            return balloonImageInfo;""",
"""            Debug.Log($"[VlRouterRun] Image Info - ChatBalloon (IsChatting=true): intent_image={balloonImageInfo}");
            return ApplyMRImageGuard(balloonImageInfo);""",
"C4a. IsChatting 경로 가드")

patch("ApiVlRouterManager.cs",
"""        Debug.Log($"[VlRouterRun] Image Info - Direct call (IsChatting=false): ai_use_image_idx={aiUseImageIdx}, intent_image={intentImage}");
        return intentImage;
    }""",
"""        Debug.Log($"[VlRouterRun] Image Info - Direct call (IsChatting=false): ai_use_image_idx={aiUseImageIdx}, intent_image={intentImage}");
        return ApplyMRImageGuard(intentImage);
    }

    // MR에서 공급 가능한 이미지가 없는데 intent_image가 켜져 있으면 요청 자체가 죽는다
    // (ExecuteRouterRunCoroutine에서 캡처 실패 시 yield break).
    // Phase 4-C의 MRHandFrameGesture가 손 프레임을 주입해 뒀으면 IsScreenshotAreaSet()이 true가 되어
    // 이 가드를 통과하고, 그 PNG가 그대로 라우터의 image 필드로 나간다.
    private string ApplyMRImageGuard(string intentImage)
    {
#if UNITY_ANDROID || UNITY_EDITOR
        if (intentImage == "off")
        {
            return intentImage;
        }
        if (ScreenshotManager.Instance == null || !ScreenshotManager.Instance.IsScreenshotAreaSet())
        {
            Debug.Log($"[VlRouterRun/MR] intent_image={intentImage} → off 강등 (주입된 손 프레임 없음). 이미지 없이 대화만 진행한다");
            return "off";
        }
#endif
        return intentImage;
    }""",
"C4b. Direct 경로 가드 + ApplyMRImageGuard")

# ===== C5. STT 종착점 2곳 =====
patch("STTUtil.cs",
"""        // 대화 시작
        APIManager.Instance.CallConversationStream(query, chatIdx, lang);""",
"""        // 대화 시작 — 라우터+스킬 경로 (Phase 5).
        // 말풍선·TTS 출력은 ApiVlRouterManager 내부에서 처리되므로 호출부에 배선이 필요 없다.
        // chatIdx는 이 파일 상단에서 이미 증가시켰다 — 라우터는 GameManager.chatIdx를 읽기만 하므로
        // 여기서 다시 올리면 이중 증가가 된다.
        ApiVlRouterManager.Instance.ExecuteVlRouterRun(query, sttLang: lang);""",
"C5a. STTUtil 종착점")

patch("WhisperSTTManager.cs",
"""        // 대화 시작 - chatIdx는 string 타입
        APIManager.Instance.CallConversationStream(query, response.chatIdx, response.lang);""",
"""        // 대화 시작 — 라우터+스킬 경로 (Phase 5).
        // 말풍선·TTS 출력은 ApiVlRouterManager 내부에서 처리되므로 호출부에 배선이 필요 없다.
        ApiVlRouterManager.Instance.ExecuteVlRouterRun(query, sttLang: response.lang);""",
"C5b. WhisperSTTManager 종착점")

# ===== C6. MR 미지원 함수 필터 =====
patch("ApiAgentFunctionManager.cs",
"""        return _functionRegistry;
    }

    // 함수 이름 목록만 JSON 배열로 반환""",
"""#if UNITY_ANDROID || UNITY_EDITOR
        StripMRUnsupportedFunctions(_functionRegistry);
#endif

        return _functionRegistry;
    }

#if UNITY_ANDROID || UNITY_EDITOR
    // MR에서 실행 불가능한 함수를 레지스트리에서 제거한다.
    // 목록은 unity_functions_list로 서버에 전달되므로, 여기서 빼면 서버가 애초에 지시하지 못한다.
    // 실행부에서 막는 것보다 낫다 — 서버가 쓸모없는 함수를 골라 라운드를 낭비하지 않는다.
    private static readonly string[] MRUnsupportedFunctions =
    {
        // 데스크톱 커서/WinAPI
        "physical_click", "proxy_click", "physical_drag", "proxy_drag", "physical_scroll", "proxy_scroll",
        // 데스크톱 입력 주입
        "type_text", "send_hotkey",
        // Android에서 Process.Start 불가
        "run_process", "focus_process",
        // ClipboardManager가 MR에서 비활성
        "read_clipboard", "write_clipboard",
        // ScreenshotManager 데스크톱 캡처 (MR 이미지는 MRHandFrameGesture가 담당)
        "capture_screenshot",
        // PhysicsManager가 MR에서 비활성이라 호출 시 NRE — Phase 2에서 되살릴 것
        "character_walk_left", "character_walk_right", "character_stop"
    };

    private void StripMRUnsupportedFunctions(JArray registry)
    {
        if (registry == null)
        {
            return;
        }

        List<string> removed = new List<string>();
        for (int i = registry.Count - 1; i >= 0; i--)
        {
            JObject func = registry[i] as JObject;
            if (func == null)
            {
                continue;
            }

            string name = (string)func["name"];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (Array.IndexOf(MRUnsupportedFunctions, name) >= 0)
            {
                removed.Add(name);
                registry.RemoveAt(i);
            }
        }

        // 조용히 줄이면 "전부 지원한다"로 읽힌다 — 무엇을 뺐는지 남긴다
        UnityEngine.Debug.Log($"[AgentFunc/MR] 미지원 {removed.Count}종 제외, 남은 {registry.Count}종 전송 | 제외=[{string.Join(",", removed.ToArray())}]");
    }
#endif

    // 함수 이름 목록만 JSON 배열로 반환""",
"C6. MR 미지원 함수 필터")

sys.exit(0 if ok else 1)
