# -*- coding: utf-8 -*-
# 바이트 단위 패치 — CRLF 보존, BOM 추가 안 함
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


# ---------- 1. SettingCharManager : LoadSettingChar 결과 로그 ----------
patch("SettingCharManager.cs",
"""        isLoaded = true;
        OnSettingsLoaded?.Invoke();""",
"""        // 진단(Phase 5): 로드 결과를 실측해 한 줄로 남긴다 (Kickoff Guide 7-1 C/D)
        int diagEntryCount = 0;
        string diagKeys = "";
        string diagAicoVoice = "(엔트리없음)";
        if (settingsCharData != null && settingsCharData.char_code_info_dict != null)
        {
            diagEntryCount = settingsCharData.char_code_info_dict.Count;
            diagKeys = string.Join(",", new List<string>(settingsCharData.char_code_info_dict.Keys).ToArray());
            if (settingsCharData.char_code_info_dict.ContainsKey("aico"))
            {
                diagAicoVoice = "'" + settingsCharData.char_code_info_dict["aico"].voiceId + "'";
            }
        }
        Debug.Log($"[SettingChar/load] 파일존재={File.Exists(configFilePath)} 엔트리수={diagEntryCount} keys=[{diagKeys}] | aico.voiceId={diagAicoVoice} | path={configFilePath}");

        isLoaded = true;
        OnSettingsLoaded?.Invoke();""",
"SettingCharManager.LoadSettingChar 로그")

# ---------- 2. SettingCharManager : 엔트리 결손이 발생하는 '그 순간' ----------
patch("SettingCharManager.cs",
"""        if (string.IsNullOrEmpty(charCode)) return null;
        // 로드 전 조회 금지 — 빈 settingsCharData 위에 엔트리를 만들며 저장하면 기존 파일이 통째로 덮어써진다
        if (!isLoaded) return null;
        if (!settingsCharData.char_code_info_dict.ContainsKey(charCode))
        {
            // 조회만으로는 저장하지 않는다 — 실제 변경(AddAffinityPoints/SetVoice 등)이 SaveToFile을 수행
            settingsCharData.char_code_info_dict[charCode] = new CharCodeSetting();
        }""",
"""        if (string.IsNullOrEmpty(charCode)) return null;
        // 로드 전 조회 금지 — 빈 settingsCharData 위에 엔트리를 만들며 저장하면 기존 파일이 통째로 덮어써진다
        if (!isLoaded)
        {
            // 진단(Phase 5): LoadSettingChar 미실행 상태의 조회 (Kickoff Guide 7-1 C)
            Debug.Log($"[SettingChar/miss] charcode='{charCode}' 조회 실패 - isLoaded=false | 원인: LoadSettingChar 미실행 → 제안: CharManager.InitCharacter 조기리턴 경로 확인");
            return null;
        }
        if (!settingsCharData.char_code_info_dict.ContainsKey(charCode))
        {
            // 진단(Phase 5): 엔트리 결손이 여기서 조용히 빈 값으로 메워진다.
            // 사후 조회로는 이 순간을 알 수 없어(엔트리가 생겨 버림) 생성 시점에 찍는다.
            Debug.Log($"[SettingChar/miss] charcode='{charCode}' 엔트리 없어 빈 값 생성 → voiceId 빈 값 (ref_id 누락) | 제안: LoadSettingChar 시딩 조건을 '파일없음'에서 '엔트리없음'으로 확장하면 woman_01");
            // 조회만으로는 저장하지 않는다 — 실제 변경(AddAffinityPoints/SetVoice 등)이 SaveToFile을 수행
            settingsCharData.char_code_info_dict[charCode] = new CharCodeSetting();
        }""",
"SettingCharManager.GetCharCodeSetting 결손 로그")

# ---------- 3. CharManager : 조기 리턴으로 LoadSettingChar가 안 도는 경로 ----------
patch("CharManager.cs",
"""        if (charList.Count == 0)
        {
            Debug.LogError("Character list is empty.");
            return;
        }""",
"""        if (charList.Count == 0)
        {
            Debug.LogError("Character list is empty.");
            // 진단(Phase 5): 이 조기 리턴이면 SettingCharManager.LoadSettingChar가 아예 실행되지 않는다.
            // 그 경우 voiceId는 항상 빈 값이 되어 TTS ref_id가 누락된다.
            Debug.Log("[SettingChar/load] 미실행 - charList.Count=0 조기리턴 (CharManager.InitCharacter) | 결과: voiceId 항상 빈 값 → ref_id 누락");
            return;
        }""",
"CharManager.InitCharacter 조기리턴 로그")

# ---------- 4. TTSManager : 최종 판정 한 줄 ----------
patch("TTSManager.cs",
"""        GameObject currentCharacter = CharManager.Instance != null ? CharManager.Instance.GetCurrentCharacter() : null;
        CharAttributes attributes = currentCharacter != null ? currentCharacter.GetComponent<CharAttributes>() : null;
        if (attributes != null)
        {
            string refId = GetSavedVoiceId(attributes.charcode);
            if (!string.IsNullOrEmpty(refId))
            {
                return refId;
            }

        }

        return GetSavedVoiceId(nickname);""",
"""        GameObject currentCharacter = CharManager.Instance != null ? CharManager.Instance.GetCurrentCharacter() : null;
        CharAttributes attributes = currentCharacter != null ? currentCharacter.GetComponent<CharAttributes>() : null;
        string diagCharcode = "(CharAttributes없음)";
        if (attributes != null)
        {
            diagCharcode = attributes.charcode;
            string refId = GetSavedVoiceId(attributes.charcode);
            if (!string.IsNullOrEmpty(refId))
            {
                // 진단(Phase 5)
                Debug.Log($"[TTS/refid] charcode='{diagCharcode}' nickname='{nickname}' | ref_id='{refId}' (charcode 조회 성공)");
                return refId;
            }

        }

        // 진단(Phase 5): 지금 값과 제안 값을 한 줄에 (Kickoff Guide 7-1 C).
        // ref_id는 옵셔널 필드라 비면 키 자체가 사라지고 경고가 안 남는다 - 그래서 여기서 명시적으로 찍는다.
        string fallbackId = GetSavedVoiceId(nickname);
        if (string.IsNullOrEmpty(fallbackId))
        {
            Debug.Log($"[TTS/refid] charcode='{diagCharcode}' nickname='{nickname}' | voiceId 빈 값 → ref_id 누락 | 직전 [SettingChar/*] 로그에서 원인 확인");
        }
        else
        {
            Debug.Log($"[TTS/refid] charcode='{diagCharcode}' nickname='{nickname}' | ref_id='{fallbackId}' (nickname 폴백 성공)");
        }
        return fallbackId;""",
"TTSManager.ResolveCharacterDetailVoiceRefId 로그")

sys.exit(0 if ok else 1)
