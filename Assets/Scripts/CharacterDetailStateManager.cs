using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterDetailState
{
    public string characterId;
    public int affection = 0;
    public int maxAffection = 300;
    public string affectionLabel = "친밀";
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

        var setting = SettingCharManager.Instance.GetCharCodeSetting(characterId);
        if (setting != null)
        {
            state.affection = setting.affection;
            state.voiceId = setting.voiceId;
        }

        // 호감도 라벨 동적 계산
        state.affectionLabel = state.affection >= 200 ? "매우 친밀" : state.affection >= 100 ? "친밀" : "보통";

        return state;
    }

    public void AddAffection(string characterId, int amount) { SettingCharManager.Instance.AddAffection(characterId, amount); } // 호감도 증감
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
