using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterDetailState
{
    public string characterId;
    public int affinityPoints = 0;
    public int affinityLevel = 0;
    public string affinityStageName = "낯선 사이";
    public string source = "오리지널";
    public string voiceId = "";
}

// CharAttributes(불변)  + SettingCharManager(가변) 데이터 조립 공장 + 실시간 동기화
public class CharacterDetailStateManager : MonoBehaviour
{
    public static CharacterDetailStateManager instance;
    public static CharacterDetailStateManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CharacterDetailStateManager>();
            }

            // MainScene 원본은 이 컴포넌트를 씬 오브젝트에 배치하지만,
            // KAI 루트에는 별도 배치가 없다. MR에서 금지하는 신규 루트
            // GameObject 생성을 피하고 감사 대상인 기존 매니저에 결합한다.
            if (instance == null)
            {
                GameObject managerHost = GameManager.Instance != null
                    ? GameManager.Instance.gameObject
                    : UIManager.Instance != null
                        ? UIManager.Instance.gameObject
                        : null;

                if (managerHost != null)
                {
                    instance = managerHost.GetComponent<CharacterDetailStateManager>();
                    if (instance == null)
                    {
                        instance = managerHost.AddComponent<CharacterDetailStateManager>();
                    }
                }
            }

            return instance;
        }
    }

    public event Action<string, CharacterDetailState> StateChanged; // 상태 변경 이벤트
    private bool settingEventRegistered;

    private void OnEnable()
    {
        RegisterSettingEvent();
    }

    private void OnDisable()
    {
        UnregisterSettingEvent();
    }

    private void RegisterSettingEvent()
    {
        if (settingEventRegistered || SettingCharManager.Instance == null)
        {
            return;
        }

        SettingCharManager.Instance.OnCharacterSettingChanged += OnCharacterSettingChanged;
        settingEventRegistered = true;
    }

    private void UnregisterSettingEvent()
    {
        if (!settingEventRegistered || SettingCharManager.Instance == null)
        {
            return;
        }

        SettingCharManager.Instance.OnCharacterSettingChanged -= OnCharacterSettingChanged;
        settingEventRegistered = false;
    }

    private void OnCharacterSettingChanged(string characterId)
    {
        StateChanged?.Invoke(characterId, GetState(characterId));
    }

    // 현재 UI 언어 코드 (설정 부재 시 ko)
    private string GetUiLanguage()
    {
        if (SettingManager.Instance != null && SettingManager.Instance.settings != null && !string.IsNullOrEmpty(SettingManager.Instance.settings.ui_language))
        {
            return SettingManager.Instance.settings.ui_language;
        }
        return "ko";
    }

    private CharAttributes GetCharAttributes(string charCode)
    {
        CharManager charManager = CharManager.Instance;
        if (charManager != null && charManager.charList != null)
        {
            foreach (var obj in charManager.charList)
            {
                if (obj == null) continue;
                var attr = obj.GetComponent<CharAttributes>();
                if (attr != null && attr.charcode == charCode)
                    return attr;
            }
        }
        return null;
    }

    public CharacterDetailState GetState(string characterId)
    {
        RegisterSettingEvent();

        CharacterDetailState state = new CharacterDetailState { characterId = characterId };
        if (string.IsNullOrEmpty(characterId)) return state;

        CharAttributes attr = GetCharAttributes(characterId);
        if (attr != null)
        {
            state.source = attr.source;
        }

        // 출전은 character_database.json(마스터)이 우선 — 다국어(ko/ja/en) 중 현재 UI 언어로 해석, CharAttributes 값은 미등재 캐릭터 폴백
        // (기능 태그는 의상 엔트리의 bool 4종 + tagSpecials — CharacterFeatureTags가 직접 조회한다)
        if (CharManager.Instance != null)
        {
            ChangeCharInfo dbCharacter = CharManager.Instance.FindCharacterInfoByCharacterId(characterId);
            if (dbCharacter != null && dbCharacter.source != null)
            {
                string localizedSource = dbCharacter.source.Get(GetUiLanguage());
                if (!string.IsNullOrEmpty(localizedSource))
                {
                    state.source = localizedSource;
                }
            }
        }

        SettingCharManager settingManager = SettingCharManager.Instance;
        var setting = settingManager != null
            ? settingManager.GetCharCodeSetting(characterId)
            : null;
        if (setting != null)
        {
            state.affinityPoints = setting.affinityPoints;
            state.voiceId = setting.voiceId;
        }

        // 인연도 레벨/단계 명칭 파생 계산
        state.affinityLevel = AffinityData.LevelFor(state.affinityPoints);
        state.affinityStageName = AffinityData.StageNameFor(state.affinityLevel);

        return state;
    }

    // 인연도 증감 — 레벨업 시 미션 AF0004(인연도 레벨업) 보고
    public void AddAffinityPoints(string characterId, int amount)
    {
        SettingCharManager settingManager = SettingCharManager.Instance;
        if (settingManager == null) return;

        var setting = settingManager.GetCharCodeSetting(characterId);
        int levelBefore = setting != null ? AffinityData.LevelFor(setting.affinityPoints) : 0;
        settingManager.AddAffinityPoints(characterId, amount);
        int levelAfter = setting != null ? AffinityData.LevelFor(setting.affinityPoints) : levelBefore;
        if (levelAfter > levelBefore && Application.isPlaying && MissionList.Instance != null)
        {
            MissionList.Instance.Report("AF0004", levelAfter - levelBefore);
        }
    }
    public void SetVoice(string characterId, string voiceId)
    {
        if (SettingCharManager.Instance != null)
        {
            SettingCharManager.Instance.SetVoice(characterId, voiceId);
        }
    } // 음성 설정

    public static string BuildCharacterId(ChangeCharInfo charInfo, ChangeCharClothesInfo clothes) 
    { 
        if (clothes != null && !string.IsNullOrEmpty(clothes.charAttr_charcode)) 
            return clothes.charAttr_charcode.ToLower();
            
        if (charInfo != null && !string.IsNullOrEmpty(charInfo.name)) 
            return charInfo.name.ToLower();
            
        return string.Empty; 
    }
}
