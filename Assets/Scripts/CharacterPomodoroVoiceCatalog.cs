using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterPomodoroVoiceCatalog",
    menuName = "Jarvis/Character Pomodoro Voice Catalog")]
public class CharacterPomodoroVoiceCatalog : ScriptableObject
{
    [Serializable]
    public class DefaultPomodoroVoice
    {
        public string id = "default_1";
        public string label = "기본1";
        [TextArea(2, 4)] public string message = "집중할 시간이에요.";
        public AudioClip audioClip;
        public bool enabled = true;
        public PomodoroVoiceSituation situation =
            PomodoroVoiceSituation.Anytime;
    }

    [Serializable]
    public class CharacterDefaults
    {
        [Tooltip("CharAttributes.nickname과 동일한 캐릭터 이름")]
        public string characterName;
        public List<DefaultPomodoroVoice> dialogues = new List<DefaultPomodoroVoice>();
    }

    [SerializeField] private List<CharacterDefaults> characters = new List<CharacterDefaults>();

    public IReadOnlyList<DefaultPomodoroVoice> GetDefaults(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return Array.Empty<DefaultPomodoroVoice>();
        }

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterDefaults character = characters[i];
            if (character != null &&
                string.Equals(
                    character.characterName,
                    characterName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return character.dialogues ?? new List<DefaultPomodoroVoice>();
            }
        }
        return Array.Empty<DefaultPomodoroVoice>();
    }

    public static CharacterPomodoroVoiceCatalog LoadDefault()
    {
        return Resources.Load<CharacterPomodoroVoiceCatalog>(
            "CharacterPomodoroVoiceCatalog");
    }
}
