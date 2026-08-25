# -*- coding: utf-8 -*-
# Phase 5 본 구현 — 바이트 단위 패치 (CRLF 보존, BOM 없음)
import sys, os

ROOT = os.path.expanduser("~/mnt/AICO/Assets/Scripts")
ok = True

def patch(path, old, new, label):
    global ok
    p = os.path.join(ROOT, path)
    data = open(p, "rb").read()
    o = old.replace("\n", "\r\n").encode("utf-8")
    n = new.replace("\n", "\r\n").encode("utf-8")
    c = data.count(o)
    if c != 1:
        print("FAIL %s : 앵커 %d회 매치 (1이어야 함)" % (label, c))
        ok = False
        return
    open(p, "wb").write(data.replace(o, n))
    print("OK   %s" % label)


# ============ A. ref_id — voiceId 빈 값 보정 ============
patch("SettingCharManager.cs",
"""            else
            {
                // KAI 이슈
                settingsCharData.char_code_info_dict["aico"] = new CharCodeSetting
                {
                    voiceId = "woman_01"
                };
                SaveToFile();
            }""",
"""
            // KAI 이슈: aico의 기본 음성을 보장한다.
            // 판정 기준은 '파일 존재 여부'가 아니라 'voiceId가 비었는지'다 —
            // GetCharCodeSetting은 조회만으로 빈 엔트리를 만들고(voiceId=""),
            // 무관한 SaveToFile이 그 엔트리를 파일에 굳히면 '파일 없음' 조건이 영영 성립하지 않는다.
            // 그 상태에서 TTS ref_id는 옵셔널 필드라 조용히 누락된다(경고도 안 남는다).
            if (EnsureDefaultVoiceId("aico", "woman_01"))
            {
                SaveToFile();
            }""",
"A1. LoadSettingChar 시딩 조건 교체")

patch("SettingCharManager.cs",
"""    private void SaveToFile()
    {""",
"""    // charCode의 voiceId가 비어 있으면 기본값을 채운다. 실제로 채웠을 때만 true를 반환한다.
    // 사용자가 캐릭터 상세 패널에서 고른 값은 절대 덮어쓰지 않는다.
    private bool EnsureDefaultVoiceId(string charCode, string defaultVoiceId)
    {
        if (settingsCharData == null)
        {
            return false;
        }
        if (settingsCharData.char_code_info_dict == null)
        {
            return false;
        }

        CharCodeSetting setting = null;
        if (settingsCharData.char_code_info_dict.ContainsKey(charCode))
        {
            setting = settingsCharData.char_code_info_dict[charCode];
        }

        if (setting == null)
        {
            setting = new CharCodeSetting();
            settingsCharData.char_code_info_dict[charCode] = setting;
        }

        if (!string.IsNullOrEmpty(setting.voiceId))
        {
            return false;
        }

        setting.voiceId = defaultVoiceId;
        Debug.Log($"[SettingChar/seed] charcode='{charCode}' voiceId가 비어 있어 기본값 '{defaultVoiceId}'으로 보정");
        return true;
    }

    private void SaveToFile()
    {""",
"A2. EnsureDefaultVoiceId 헬퍼 추가")


# ============ B. MRSceneStripper — 라우터/스킬 3종 해제 ============
patch("MR/MRSceneStripper.cs",
"""        typeof(ApiVlPlannerManager),
        typeof(ApiVlRouterManager),
        typeof(ApiVlRouterResponseManager),
        typeof(ApiAgentFunctionManager),""",
"""        typeof(ApiVlPlannerManager),
        // ApiVlRouterManager / ApiVlRouterResponseManager / ApiAgentFunctionManager는
        // 2026-08-24 Phase 5에서 제외 해제했다 — ReviewedKeepTypes 참고.
        // 이름의 Vl 접두사와 달리 화면 조작과 무관하고, 춤·일정·음원 스킬의 실행기다.""",
"B1. DesktopOnlyTypes에서 3종 제거")

patch("MR/MRSceneStripper.cs",
"""        typeof(ChatModeManager), typeof(ChatHandler), typeof(ServerManager),""",
"""        typeof(ChatModeManager), typeof(ChatHandler), typeof(ServerManager),
        // 라우터 + 스킬 (2026-08-24 Phase 5에서 제외 해제).
        // ApiVlRouterManager는 /router/job/run 호출, ApiVlRouterResponseManager는 툴콜 디스패치,
        // ApiAgentFunctionManager는 character_dance / todo_* / play_sfx 실행기다.
        // 화면 캡처는 intent_image 가드 안에만 있고, MR에서는 손 프레임 주입분을 쓴다.
        typeof(ApiVlRouterManager), typeof(ApiVlRouterResponseManager), typeof(ApiAgentFunctionManager),""",
"B2. ReviewedKeepTypes에 3종 추가")

sys.exit(0 if ok else 1)
