using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

/*
settings_char.json 구조 예시

{
  "last_char": "arona",
  "char_info": [
    {
      "key": "arona",
      "value": { "char_code": "arona", "char_size": 100 }
    },
    {
      "key": "mari",
      "value": { "char_code": "ch002" }
    }
  ],
  "char_code_info": [
    {
      "key": "ch002",
      "value": { "char_size": 95 }
    }
  ]
}
*/

public class SettingCharManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static SettingCharManager instance;
    public static SettingCharManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<SettingCharManager>();
            return instance;
        }
    }

    private string configFilePath;
    public bool isLoaded = false;

    public event Action<string> OnCharacterSettingChanged;
    public event Action OnSettingsLoaded; // LoadSettingChar 완료 통지 — 로드 전 조회를 미룬 UI(카드 테두리 등)의 재적용 트리거

    [Serializable]
    public class CharSetting
    {
        public string char_code;
        public float char_size;
    }

    [Serializable]
    public class CharCodeSetting
    {
        public float char_size;
        public int affinityPoints = 0;                                    // 친밀도 포인트 (0~1000) — 구 affection(0~300) 대체, 마이그레이션 없음
        public List<int> affinityClaimedLevels = new List<int>();         // 수령 완료한 친밀도 레벨 보상
        public List<string> affinityUnlockedIds = new List<string>();     // 해금물 id (카드 테두리/호칭 등 — AffinityRewardType.Border/Title)
        public string voiceId = "";
    }

    [Serializable]
    public class CharInfoEntry
    {
        public string key;
        public CharSetting value;
    }

    [Serializable]
    public class CharCodeInfoEntry
    {
        public string key;
        public CharCodeSetting value;
    }

    [Serializable]
    public class SettingsCharData
    {
        public string last_char;
        public List<CharInfoEntry> char_info = new List<CharInfoEntry>();
        public List<CharCodeInfoEntry> char_code_info = new List<CharCodeInfoEntry>();

        [NonSerialized] public Dictionary<string, CharSetting> char_info_dict = new();
        [NonSerialized] public Dictionary<string, CharCodeSetting> char_code_info_dict = new();

        public void SyncDictionaries()
        {
            char_info_dict.Clear();
            foreach (var entry in char_info)
                char_info_dict[entry.key] = entry.value;

            char_code_info_dict.Clear();
            foreach (var entry in char_code_info)
                char_code_info_dict[entry.key] = entry.value;
        }

        public void RebuildListsFromDict()
        {
            char_info.Clear();
            foreach (var kv in char_info_dict)
                char_info.Add(new CharInfoEntry { key = kv.Key, value = kv.Value });

            char_code_info.Clear();
            foreach (var kv in char_code_info_dict)
                char_code_info.Add(new CharCodeInfoEntry { key = kv.Key, value = kv.Value });
        }
    }

    private SettingsCharData settingsCharData = new SettingsCharData();

    public void LoadSettingChar()
    {
        configFilePath = Path.Combine(Application.persistentDataPath, "config", "settings_char.json");

        try
        {
            if (File.Exists(configFilePath))
            {
                string json = File.ReadAllText(configFilePath);
                settingsCharData = JsonUtility.FromJson<SettingsCharData>(json);
                settingsCharData.SyncDictionaries();
            }

            // KAI 이슈: aico의 기본 음성을 보장한다.
            // 판정 기준은 '파일 존재 여부'가 아니라 'voiceId가 비었는지'다 —
            // GetCharCodeSetting은 조회만으로 빈 엔트리를 만들고(voiceId=""),
            // 무관한 SaveToFile이 그 엔트리를 파일에 굳히면 '파일 없음' 조건이 영영 성립하지 않는다.
            // 그 상태에서 TTS ref_id는 옵셔널 필드라 조용히 누락된다(경고도 안 남는다).
            if (EnsureDefaultVoiceId("aico", "woman_01"))
            {
                SaveToFile();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("LoadSettingChar 실패: " + e.Message);
        }

        // 진단(Phase 5): 로드 결과를 실측해 한 줄로 남긴다 (Kickoff Guide 7-1 C/D)
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
        OnSettingsLoaded?.Invoke();
    }

    // charCode의 voiceId가 비어 있으면 기본값을 채운다. 실제로 채웠을 때만 true를 반환한다.
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
    {
        configFilePath = Path.Combine(Application.persistentDataPath, "config", "settings_char.json");

        try
        {
            string directoryPath = Path.GetDirectoryName(configFilePath);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            settingsCharData.RebuildListsFromDict();

            string json = JsonUtility.ToJson(settingsCharData, true);
            File.WriteAllText(configFilePath, json);

            Debug.Log($"[SettingCharManager] settings_char.json 저장 완료:\n{json}");
        }
        catch (Exception e)
        {
            Debug.LogError("SaveToFile 실패: " + e.Message);
        }
    }

    public void SetLastChar(string charName)
    {
        settingsCharData.last_char = charName;
        SaveToFile();
    }

    public string GetLastChar()
    {
        return settingsCharData.last_char;
    }

    public void SaveSettingCharOutfit(string charName, string charCode)
    {
        Debug.Log($"[SettingCharManager] SaveSettingCharOutfit 호출됨: charName={charName}, charCode={charCode}");

        if (!settingsCharData.char_info_dict.ContainsKey(charName))
            settingsCharData.char_info_dict[charName] = new CharSetting();

        settingsCharData.char_info_dict[charName].char_code = charCode;

        SaveToFile();
    }

    public void SaveSettingCharSize(string charName, float charSize)
    {
        if (!settingsCharData.char_info_dict.ContainsKey(charName))
            settingsCharData.char_info_dict[charName] = new CharSetting();

        settingsCharData.char_info_dict[charName].char_size = charSize;
        SaveToFile();
    }

    public void SaveCharCodeSize(string charCode, float charSize)
    {
        if (!settingsCharData.char_code_info_dict.ContainsKey(charCode))
            settingsCharData.char_code_info_dict[charCode] = new CharCodeSetting();

        settingsCharData.char_code_info_dict[charCode].char_size = charSize;
        SaveToFile();
    }

    public CharCodeSetting GetCharCodeSetting(string charCode)
    {
        if (string.IsNullOrEmpty(charCode)) return null;
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
        }
        return settingsCharData.char_code_info_dict[charCode];
    }

    public void AddAffinityPoints(string charCode, int amount) { var setting = GetCharCodeSetting(charCode); if (setting != null) { setting.affinityPoints = Mathf.Clamp(setting.affinityPoints + amount, 0, AffinityData.MaxPoints); SaveToFile(); OnCharacterSettingChanged?.Invoke(charCode); } }
    public bool IsAffinityRewardClaimed(string charCode, int level) { var setting = GetCharCodeSetting(charCode); return setting != null && setting.affinityClaimedLevels != null && setting.affinityClaimedLevels.Contains(level); }
    public bool ClaimAffinityReward(string charCode, int level) { var setting = GetCharCodeSetting(charCode); if (setting == null) return false; if (setting.affinityClaimedLevels == null) setting.affinityClaimedLevels = new List<int>(); if (setting.affinityClaimedLevels.Contains(level)) return false; setting.affinityClaimedLevels.Add(level); SaveToFile(); OnCharacterSettingChanged?.Invoke(charCode); return true; }
    public bool IsAffinityRewardUnlocked(string charCode, string unlockId) { var setting = GetCharCodeSetting(charCode); return setting != null && setting.affinityUnlockedIds != null && setting.affinityUnlockedIds.Contains(unlockId); }
    public bool UnlockAffinityReward(string charCode, string unlockId) { if (string.IsNullOrEmpty(unlockId)) return false; var setting = GetCharCodeSetting(charCode); if (setting == null) return false; if (setting.affinityUnlockedIds == null) setting.affinityUnlockedIds = new List<string>(); if (setting.affinityUnlockedIds.Contains(unlockId)) return false; setting.affinityUnlockedIds.Add(unlockId); SaveToFile(); OnCharacterSettingChanged?.Invoke(charCode); return true; }
    public void SetVoice(string charCode, string voiceId) { var setting = GetCharCodeSetting(charCode); if (setting != null) { setting.voiceId = voiceId; SaveToFile(); OnCharacterSettingChanged?.Invoke(charCode); } }

    public CharSetting GetCharSetting(string charName)
    {
        if (settingsCharData.char_info_dict.TryGetValue(charName, out var setting))
            return setting;

        return null;
    }

    public float? GetCharSize(string charName)
    {
        if (settingsCharData.char_info_dict.TryGetValue(charName, out var charSetting))
        {
            if (charSetting.char_size > 0)
                return charSetting.char_size;

            if (!string.IsNullOrEmpty(charSetting.char_code) &&
                settingsCharData.char_code_info_dict.TryGetValue(charSetting.char_code, out var codeSetting) &&
                codeSetting.char_size > 0)
            {
                return codeSetting.char_size;
            }
        }

        return null;
    }

    // last_char을 기반으로 최종 char_code 반환 (InitCharacter용)
    public string GetLastCharCode()
    {
        string lastChar = settingsCharData.last_char;

        if (string.IsNullOrEmpty(lastChar))
            return null;

        foreach (GameObject obj in CharManager.Instance.charList)
        {
            var attr = obj.GetComponent<CharAttributes>();
            if (attr != null && attr.charcode == lastChar)
                return lastChar;
        }

        if (settingsCharData.char_info_dict.TryGetValue(lastChar, out var setting))
        {
            if (!string.IsNullOrEmpty(setting.char_code))
                return setting.char_code;
        }

        return lastChar;
    }
}
