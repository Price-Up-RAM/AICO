using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterAlarmVoiceCatalog",
    menuName = "Jarvis/Character Alarm Voice Catalog")]
public class CharacterAlarmVoiceCatalog : ScriptableObject
{
    [Serializable]
    public class DefaultAlarmVoice
    {
        public string id = "default_1";
        public string label = "기본1";
        [TextArea(2, 4)] public string message = "시간이 되었습니다.";
        public AudioClip audioClip;
        public bool enabled = true;
    }

    [Serializable]
    public class CharacterDefaults
    {
        [Tooltip("CharAttributes.nickname과 동일한 캐릭터 이름")]
        public string characterName;
        public List<DefaultAlarmVoice> alarms = new List<DefaultAlarmVoice>();
    }

    [SerializeField] private List<CharacterDefaults> characters = new List<CharacterDefaults>();

    public IReadOnlyList<DefaultAlarmVoice> GetDefaults(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return Array.Empty<DefaultAlarmVoice>();
        }

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterDefaults character = characters[i];
            if (character != null &&
                string.Equals(character.characterName, characterName, StringComparison.OrdinalIgnoreCase))
            {
                if (character.alarms != null)
                {
                    return character.alarms;
                }

                return Array.Empty<DefaultAlarmVoice>();
            }
        }

        return Array.Empty<DefaultAlarmVoice>();
    }

    public static CharacterAlarmVoiceCatalog LoadDefault()
    {
        return Resources.Load<CharacterAlarmVoiceCatalog>("CharacterAlarmVoiceCatalog");
    }
}
