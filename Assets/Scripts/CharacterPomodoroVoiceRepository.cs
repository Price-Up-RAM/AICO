using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class CharacterPomodoroVoiceRecord
{
    public string id;
    public string label;
    public string message;
    public string audioFileName;
    public string source = "generated";
    public string refId;
    public string language;
    public string createdAtUtc;
    public bool enabled = true;
    public PomodoroVoiceSituation situation =
        PomodoroVoiceSituation.Anytime;
}

[Serializable]
public class CharacterPomodoroDefaultState
{
    public string id;
    public bool hasEnabledOverride;
    public bool enabled = true;
    public bool hasMessageOverride;
    public string message;
    public bool hasSituationOverride;
    public PomodoroVoiceSituation situation =
        PomodoroVoiceSituation.Anytime;
}

[Serializable]
public class CharacterPomodoroVoiceMetadata
{
    public string characterName;
    public List<CharacterPomodoroVoiceRecord> dialogues =
        new List<CharacterPomodoroVoiceRecord>();
    public List<string> hiddenDefaultDialogueIds = new List<string>();
    public List<CharacterPomodoroDefaultState> defaultDialogueStates =
        new List<CharacterPomodoroDefaultState>();
}

public sealed class CharacterPomodoroPlaybackCandidate
{
    public string id;
    public string label;
    public string message;
    public bool isGenerated;
    public bool enabled;
    public PomodoroVoiceSituation situation;
    public AudioClip audioClip;
    public string audioFilePath;
    public CharacterPomodoroVoiceRecord generatedRecord;
}

public static class CharacterPomodoroVoiceRepository
{
    private const string MetadataFileName = "metadata.json";
    public static event Action<string> Changed;

    public static string GetPomodoroDirectory(string characterName)
    {
        return Path.Combine(
            Application.persistentDataPath,
            "voice",
            SanitizePathSegment(characterName),
            "pomodoro");
    }

    public static CharacterPomodoroVoiceMetadata Load(string characterName)
    {
        CharacterPomodoroVoiceMetadata metadata = null;
        string path = Path.Combine(
            GetPomodoroDirectory(characterName),
            MetadataFileName);
        try
        {
            if (File.Exists(path))
            {
                metadata = JsonUtility.FromJson<CharacterPomodoroVoiceMetadata>(
                    File.ReadAllText(path));
            }
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[CharacterPomodoroVoice] Metadata load failed. path={path}, error={e.Message}");
        }

        if (metadata == null) metadata = new CharacterPomodoroVoiceMetadata();
        metadata.characterName = characterName ?? string.Empty;
        if (metadata.dialogues == null)
        {
            metadata.dialogues = new List<CharacterPomodoroVoiceRecord>();
        }
        if (metadata.hiddenDefaultDialogueIds == null)
        {
            metadata.hiddenDefaultDialogueIds = new List<string>();
        }
        if (metadata.defaultDialogueStates == null)
        {
            metadata.defaultDialogueStates =
                new List<CharacterPomodoroDefaultState>();
        }
        return metadata;
    }

    public static bool Save(CharacterPomodoroVoiceMetadata metadata)
    {
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.characterName))
        {
            return false;
        }

        try
        {
            string directory = GetPomodoroDirectory(metadata.characterName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, MetadataFileName),
                JsonUtility.ToJson(metadata, true));
            Changed?.Invoke(metadata.characterName);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[CharacterPomodoroVoice] Metadata save failed. char={metadata.characterName}, error={e.Message}");
            return false;
        }
    }

    public static CharacterPomodoroVoiceRecord AddGeneratedDialogue(
        string characterName,
        string message,
        byte[] wavData,
        string refId,
        string language,
        PomodoroVoiceSituation situation =
            PomodoroVoiceSituation.Anytime)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(message) ||
            wavData == null ||
            wavData.Length == 0)
        {
            return null;
        }

        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        int generatedIndex = metadata.dialogues.Count + 1;
        string id = "generated_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") +
                    "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string audioFileName = id + ".wav";
        try
        {
            string directory = GetPomodoroDirectory(characterName);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, audioFileName), wavData);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[CharacterPomodoroVoice] WAV save failed. char={characterName}, error={e.Message}");
            return null;
        }

        CharacterPomodoroVoiceRecord record = new CharacterPomodoroVoiceRecord
        {
            id = id,
            label = "생성" + generatedIndex,
            message = message.Trim(),
            audioFileName = audioFileName,
            refId = refId ?? string.Empty,
            language = language ?? string.Empty,
            createdAtUtc = DateTime.UtcNow.ToString("o"),
            enabled = true,
            situation = situation
        };
        metadata.dialogues.Add(record);
        return Save(metadata) ? record : null;
    }

    public static bool SetGeneratedDialogueEnabled(
        string characterName,
        string dialogueId,
        bool enabled)
    {
        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        CharacterPomodoroVoiceRecord record = metadata.dialogues.Find(item =>
            item != null && item.id == dialogueId);
        if (record == null)
        {
            return false;
        }

        record.enabled = enabled;
        return Save(metadata);
    }

    public static bool SetDefaultDialogueEnabled(
        string characterName,
        string dialogueId,
        bool enabled)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(dialogueId))
        {
            return false;
        }

        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        CharacterPomodoroDefaultState state =
            GetOrCreateDefaultState(metadata, dialogueId);
        state.hasEnabledOverride = true;
        state.enabled = enabled;
        return Save(metadata);
    }

    public static bool UpdateGeneratedDialogueMessage(
        string characterName,
        string dialogueId,
        string message)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(dialogueId) ||
            string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        CharacterPomodoroVoiceRecord record = metadata.dialogues.Find(item =>
            item != null &&
            item.id == dialogueId &&
            string.Equals(
                item.source,
                "generated",
                StringComparison.OrdinalIgnoreCase));
        if (record == null)
        {
            return false;
        }

        record.message = message.Trim();
        return Save(metadata);
    }

    public static bool UpdateDefaultDialogueMessage(
        string characterName,
        string dialogueId,
        string message)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(dialogueId) ||
            string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        CharacterPomodoroDefaultState state =
            GetOrCreateDefaultState(metadata, dialogueId);
        state.hasMessageOverride = true;
        state.message = message.Trim();
        return Save(metadata);
    }

    public static bool UpdateGeneratedDialogueSituation(
        string characterName,
        string dialogueId,
        PomodoroVoiceSituation situation)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(dialogueId))
        {
            return false;
        }

        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        CharacterPomodoroVoiceRecord record = metadata.dialogues.Find(item =>
            item != null &&
            item.id == dialogueId &&
            string.Equals(
                item.source,
                "generated",
                StringComparison.OrdinalIgnoreCase));
        if (record == null)
        {
            return false;
        }

        record.situation = situation;
        return Save(metadata);
    }

    public static bool UpdateDefaultDialogueSituation(
        string characterName,
        string dialogueId,
        PomodoroVoiceSituation situation)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(dialogueId))
        {
            return false;
        }

        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        CharacterPomodoroDefaultState state =
            GetOrCreateDefaultState(metadata, dialogueId);
        state.hasSituationOverride = true;
        state.situation = situation;
        return Save(metadata);
    }

    public static bool ReplaceGeneratedDialogue(
        string characterName,
        string dialogueId,
        string message,
        byte[] wavData,
        string refId,
        string language)
    {
        if (string.IsNullOrWhiteSpace(message) ||
            wavData == null ||
            wavData.Length == 0)
        {
            return false;
        }

        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        CharacterPomodoroVoiceRecord record = metadata.dialogues.Find(item =>
            item != null &&
            item.id == dialogueId &&
            string.Equals(item.source, "generated", StringComparison.OrdinalIgnoreCase));
        if (record == null || string.IsNullOrWhiteSpace(record.audioFileName))
        {
            return false;
        }

        try
        {
            File.WriteAllBytes(
                Path.Combine(
                    GetPomodoroDirectory(characterName),
                    record.audioFileName),
                wavData);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[CharacterPomodoroVoice] WAV replace failed. char={characterName}, dialogue={dialogueId}, error={e.Message}");
            return false;
        }

        record.message = message.Trim();
        record.refId = refId ?? string.Empty;
        record.language = language ?? string.Empty;
        record.createdAtUtc = DateTime.UtcNow.ToString("o");
        return Save(metadata);
    }

    public static bool DeleteGeneratedDialogue(
        string characterName,
        string dialogueId)
    {
        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        CharacterPomodoroVoiceRecord record = metadata.dialogues.Find(item =>
            item != null &&
            item.id == dialogueId &&
            string.Equals(item.source, "generated", StringComparison.OrdinalIgnoreCase));
        if (record == null)
        {
            return false;
        }

        metadata.dialogues.Remove(record);
        if (!Save(metadata))
        {
            return false;
        }
        try
        {
            string path = Path.Combine(
                GetPomodoroDirectory(characterName),
                record.audioFileName ?? string.Empty);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"[CharacterPomodoroVoice] WAV cleanup failed. char={characterName}, dialogue={dialogueId}, error={e.Message}");
        }
        return true;
    }

    public static bool SetDefaultDialogueHidden(
        string characterName,
        string dialogueId,
        bool hidden)
    {
        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            return false;
        }
        CharacterPomodoroVoiceMetadata metadata = Load(characterName);
        bool contains = metadata.hiddenDefaultDialogueIds.Contains(dialogueId);
        if (hidden && !contains) metadata.hiddenDefaultDialogueIds.Add(dialogueId);
        if (!hidden && contains) metadata.hiddenDefaultDialogueIds.Remove(dialogueId);
        return Save(metadata);
    }

    public static List<CharacterPomodoroPlaybackCandidate> GetDisplayCandidates(
        string characterName,
        CharacterPomodoroVoiceCatalog catalog)
    {
        List<CharacterPomodoroPlaybackCandidate> result =
            new List<CharacterPomodoroPlaybackCandidate>();
        CharacterPomodoroVoiceMetadata metadata = Load(characterName);

        if (catalog != null)
        {
            IReadOnlyList<CharacterPomodoroVoiceCatalog.DefaultPomodoroVoice> defaults =
                catalog.GetDefaults(characterName);
            for (int i = 0; i < defaults.Count; i++)
            {
                CharacterPomodoroVoiceCatalog.DefaultPomodoroVoice item = defaults[i];
                if (item == null ||
                    (!string.IsNullOrWhiteSpace(item.id) &&
                     metadata.hiddenDefaultDialogueIds.Contains(item.id)))
                {
                    continue;
                }
                CharacterPomodoroDefaultState state =
                    FindDefaultState(metadata, item.id);
                result.Add(new CharacterPomodoroPlaybackCandidate
                {
                    id = item.id,
                    label = string.IsNullOrWhiteSpace(item.label)
                        ? "기본" + (i + 1)
                        : item.label,
                    message = state != null &&
                              state.hasMessageOverride
                        ? state.message
                        : item.message,
                    enabled = state != null &&
                              state.hasEnabledOverride
                        ? state.enabled
                        : item.enabled,
                    situation = state != null &&
                                state.hasSituationOverride
                        ? state.situation
                        : item.situation,
                    audioClip = item.audioClip
                });
            }
        }

        for (int i = 0; i < metadata.dialogues.Count; i++)
        {
            CharacterPomodoroVoiceRecord record = metadata.dialogues[i];
            if (record == null) continue;
            result.Add(new CharacterPomodoroPlaybackCandidate
            {
                id = record.id,
                label = string.IsNullOrWhiteSpace(record.label)
                    ? "생성" + (i + 1)
                    : record.label,
                message = record.message,
                isGenerated = true,
                enabled = record.enabled,
                situation = record.situation,
                audioFilePath = Path.Combine(
                    GetPomodoroDirectory(characterName),
                    record.audioFileName ?? string.Empty),
                generatedRecord = record
            });
        }
        return result;
    }

    public static List<CharacterPomodoroPlaybackCandidate> GetPlayableCandidates(
        string characterName,
        CharacterPomodoroVoiceCatalog catalog)
    {
        List<CharacterPomodoroPlaybackCandidate> displayCandidates =
            GetDisplayCandidates(characterName, catalog);
        List<CharacterPomodoroPlaybackCandidate> playable =
            new List<CharacterPomodoroPlaybackCandidate>();
        for (int i = 0; i < displayCandidates.Count; i++)
        {
            CharacterPomodoroPlaybackCandidate candidate = displayCandidates[i];
            if (candidate == null ||
                !candidate.enabled ||
                string.IsNullOrWhiteSpace(candidate.message))
            {
                continue;
            }

            bool hasAudio = candidate.isGenerated
                ? !string.IsNullOrWhiteSpace(candidate.audioFilePath) &&
                  File.Exists(candidate.audioFilePath)
                : candidate.audioClip != null;
            if (hasAudio)
            {
                playable.Add(candidate);
            }
        }
        return playable;
    }

    public static List<CharacterPomodoroPlaybackCandidate> GetPlayableCandidates(
        string characterName,
        CharacterPomodoroVoiceCatalog catalog,
        PomodoroVoiceSituation currentSituation)
    {
        List<CharacterPomodoroPlaybackCandidate> playable =
            GetPlayableCandidates(characterName, catalog);
        return playable.FindAll(candidate =>
            candidate != null &&
            (candidate.situation == PomodoroVoiceSituation.Anytime ||
             candidate.situation == currentSituation));
    }

    private static CharacterPomodoroDefaultState FindDefaultState(
        CharacterPomodoroVoiceMetadata metadata,
        string dialogueId)
    {
        if (metadata == null ||
            metadata.defaultDialogueStates == null ||
            string.IsNullOrWhiteSpace(dialogueId))
        {
            return null;
        }

        return metadata.defaultDialogueStates.Find(state =>
            state != null &&
            string.Equals(state.id, dialogueId, StringComparison.Ordinal));
    }

    private static CharacterPomodoroDefaultState GetOrCreateDefaultState(
        CharacterPomodoroVoiceMetadata metadata,
        string dialogueId)
    {
        CharacterPomodoroDefaultState state =
            FindDefaultState(metadata, dialogueId);
        if (state != null)
        {
            return state;
        }

        state = new CharacterPomodoroDefaultState { id = dialogueId };
        metadata.defaultDialogueStates.Add(state);
        return state;
    }

    private static string SanitizePathSegment(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }
        return result;
    }
}
