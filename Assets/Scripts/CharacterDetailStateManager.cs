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
    public string form = "2D";
    public List<string> statusTags = new List<string>();
    public List<string> featureTags = new List<string>();
    public string voiceId = "";
}

// CharAttributes(불변)  + SettingCharManager(가변) 데이터 조립 공장 + 실시간 동기화
public class CharacterDetailStateManager : MonoBehaviour
{
    private static CharacterDetailStateManager instance;
    public static CharacterDetailStateManager Instance { get { if (instance == null) { instance = FindObjectOfType<CharacterDetailStateManager>(); if (instance == null) { instance = new GameObject("CharacterDetailStateManager").AddComponent<CharacterDetailStateManager>(); } } return instance; } } // 싱글톤 인스턴스

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

    private CharAttributes GetCharAttributes(string charCode)
    {
        if (CharManager.Instance.charList != null)
        {
            foreach (var obj in CharManager.Instance.charList)
            {
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
            state.form = attr.form;
            state.statusTags = new List<string>(attr.statusTags);
            state.featureTags = new List<string>(attr.featureTags);
        }

        // 출전/기능 태그는 character_database.json(마스터)이 우선 — CharAttributes 값은 폴백
        // (프리팹 61개 전부가 코드 기본값을 상속하고 있어, 캐릭터별 실데이터는 JSON에서 관리한다)
        if (CharManager.Instance != null)
        {
            ChangeCharInfo dbCharacter = CharManager.Instance.FindCharacterInfoByCharacterId(characterId);
            if (dbCharacter != null)
            {
                if (!string.IsNullOrEmpty(dbCharacter.source))
                {
                    state.source = dbCharacter.source;
                }

                if (dbCharacter.featureTags != null && dbCharacter.featureTags.Count > 0)
                {
                    state.featureTags = new List<string>(dbCharacter.featureTags);
                }
            }
        }

        var setting = SettingCharManager.Instance.GetCharCodeSetting(characterId);
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
        var setting = SettingCharManager.Instance.GetCharCodeSetting(characterId);
        int levelBefore = setting != null ? AffinityData.LevelFor(setting.affinityPoints) : 0;
        SettingCharManager.Instance.AddAffinityPoints(characterId, amount);
        int levelAfter = setting != null ? AffinityData.LevelFor(setting.affinityPoints) : levelBefore;
        if (levelAfter > levelBefore && Application.isPlaying && MissionList.Instance != null)
        {
            MissionList.Instance.Report("AF0004", levelAfter - levelBefore);
        }
    }
    public void SetVoice(string characterId, string voiceId) { SettingCharManager.Instance.SetVoice(characterId, voiceId); } // 음성 설정

    public static string BuildCharacterId(ChangeCharInfo charInfo, ChangeCharClothesInfo clothes) 
    { 
        if (clothes != null && !string.IsNullOrEmpty(clothes.charAttr_charcode)) 
            return clothes.charAttr_charcode.ToLower();
            
        if (charInfo != null && !string.IsNullOrEmpty(charInfo.name)) 
            return charInfo.name.ToLower();
            
        return string.Empty; 
    }
}
