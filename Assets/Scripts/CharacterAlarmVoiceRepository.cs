using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class CharacterAlarmVoiceRecord
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
}

[Serializable]
public class CharacterAlarmDefaultState
{
    public string id;
    public bool hasEnabledOverride;
    public bool enabled = true;
    public bool hasMessageOverride;
    public string message;
}

[Serializable]
public class CharacterAlarmVoiceMetadata
{
    public string characterName;
    public bool customAlarmVoiceEnabled;
    public List<CharacterAlarmVoiceRecord> alarms = new List<CharacterAlarmVoiceRecord>();
    public List<string> hiddenDefaultAlarmIds = new List<string>();
    public List<CharacterAlarmDefaultState> defaultAlarmStates =
        new List<CharacterAlarmDefaultState>();
}

public sealed class CharacterAlarmPlaybackCandidate
{
    public string id;
    public string label;
    public string message;
    public bool isGenerated;
    public bool enabled;
    public AudioClip audioClip;
    public string audioFilePath;
    public CharacterAlarmVoiceRecord generatedRecord;
}

public static class CharacterAlarmVoiceRepository
{
    private const string MetadataFileName = "metadata.json";
    public static event Action<string> Changed;

    public static string GetAlarmDirectory(string characterName)
    {
        string safeCharacterName = SanitizePathSegment(characterName);
        return Path.Combine(Application.persistentDataPath, "voice", safeCharacterName, "alarm");
    }

    public static string GetMetadataPath(string characterName)
    {
        return Path.Combine(GetAlarmDirectory(characterName), MetadataFileName);
    }

    public static CharacterAlarmVoiceMetadata Load(string characterName)
    {
        CharacterAlarmVoiceMetadata metadata = null;
        string metadataPath = GetMetadataPath(characterName);

        try
        {
            if (File.Exists(metadataPath))
            {
                string json = File.ReadAllText(metadataPath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    metadata = JsonUtility.FromJson<CharacterAlarmVoiceMetadata>(json);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterAlarmVoice] Metadata load failed. path={metadataPath}, error={e.Message}");
        }

        if (metadata == null)
        {
            metadata = new CharacterAlarmVoiceMetadata();
        }

        metadata.characterName = characterName ?? string.Empty;
        if (metadata.alarms == null)
        {
            metadata.alarms = new List<CharacterAlarmVoiceRecord>();
        }
        if (metadata.hiddenDefaultAlarmIds == null)
        {
            metadata.hiddenDefaultAlarmIds = new List<string>();
        }
        if (metadata.defaultAlarmStates == null)
        {
            metadata.defaultAlarmStates = new List<CharacterAlarmDefaultState>();
        }

        return metadata;
    }

    public static bool Save(CharacterAlarmVoiceMetadata metadata)
    {
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.characterName))
        {
            return false;
        }

        try
        {
            string directory = GetAlarmDirectory(metadata.characterName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, MetadataFileName),
                JsonUtility.ToJson(metadata, true));
            Changed?.Invoke(metadata.characterName);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterAlarmVoice] Metadata save failed. char={metadata.characterName}, error={e.Message}");
            return false;
        }
    }

    public static bool SetCustomAlarmVoiceEnabled(string characterName, bool enabled)
    {
        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        metadata.customAlarmVoiceEnabled = enabled;
        return Save(metadata);
    }

    public static bool SetGeneratedAlarmEnabled(string characterName, string alarmId, bool enabled)
    {
        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        CharacterAlarmVoiceRecord record = metadata.alarms.Find(item => item != null && item.id == alarmId);
        if (record == null)
        {
            return false;
        }

        record.enabled = enabled;
        return Save(metadata);
    }

    public static bool SetDefaultAlarmEnabled(
        string characterName,
        string alarmId,
        bool enabled)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(alarmId))
        {
            return false;
        }

        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        CharacterAlarmDefaultState state =
            GetOrCreateDefaultState(metadata, alarmId);
        state.hasEnabledOverride = true;
        state.enabled = enabled;
        return Save(metadata);
    }

    public static bool UpdateGeneratedAlarmMessage(
        string characterName,
        string alarmId,
        string message)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(alarmId) ||
            string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        CharacterAlarmVoiceRecord record = metadata.alarms.Find(item =>
            item != null &&
            item.id == alarmId &&
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

    public static bool UpdateDefaultAlarmMessage(
        string characterName,
        string alarmId,
        string message)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(alarmId) ||
            string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        CharacterAlarmDefaultState state =
            GetOrCreateDefaultState(metadata, alarmId);
        state.hasMessageOverride = true;
        state.message = message.Trim();
        return Save(metadata);
    }

    public static CharacterAlarmVoiceRecord AddGeneratedAlarm(
        string characterName,
        string message,
        byte[] wavData,
        string refId,
        string language)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(message) ||
            wavData == null ||
            wavData.Length == 0)
        {
            return null;
        }

        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        int generatedIndex = 1;
        for (int i = 0; i < metadata.alarms.Count; i++)
        {
            CharacterAlarmVoiceRecord existing = metadata.alarms[i];
            if (existing != null && string.Equals(existing.source, "generated", StringComparison.OrdinalIgnoreCase))
            {
                generatedIndex++;
            }
        }

        string id = "generated_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" +
                    Guid.NewGuid().ToString("N").Substring(0, 8);
        string audioFileName = id + ".wav";
        string directory = GetAlarmDirectory(characterName);

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, audioFileName), wavData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterAlarmVoice] WAV save failed. char={characterName}, error={e.Message}");
            return null;
        }

        CharacterAlarmVoiceRecord record = new CharacterAlarmVoiceRecord
        {
            id = id,
            label = "생성" + generatedIndex,
            message = message.Trim(),
            audioFileName = audioFileName,
            source = "generated",
            refId = refId ?? string.Empty,
            language = language ?? string.Empty,
            createdAtUtc = DateTime.UtcNow.ToString("o"),
            enabled = true
        };

        metadata.alarms.Add(record);
        metadata.customAlarmVoiceEnabled = true;
        if (!Save(metadata))
        {
            return null;
        }

        return record;
    }

    public static bool ReplaceGeneratedAlarmAudio(
        string characterName,
        string alarmId,
        byte[] wavData,
        string refId,
        string language)
    {
        if (string.IsNullOrWhiteSpace(characterName) ||
            string.IsNullOrWhiteSpace(alarmId) ||
            wavData == null ||
            wavData.Length == 0)
        {
            return false;
        }

        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        CharacterAlarmVoiceRecord record = metadata.alarms.Find(item =>
            item != null &&
            item.id == alarmId &&
            string.Equals(item.source, "generated", StringComparison.OrdinalIgnoreCase));
        if (record == null || string.IsNullOrWhiteSpace(record.audioFileName))
        {
            return false;
        }

        try
        {
            string directory = GetAlarmDirectory(characterName);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, record.audioFileName), wavData);
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[CharacterAlarmVoice] WAV replace failed. char={characterName}, alarm={alarmId}, error={e.Message}");
            return false;
        }

        record.refId = refId ?? string.Empty;
        record.language = language ?? string.Empty;
        record.createdAtUtc = DateTime.UtcNow.ToString("o");
        metadata.customAlarmVoiceEnabled = true;
        return Save(metadata);
    }

    public static bool ReplaceGeneratedAlarm(
        string characterName,
        string alarmId,
        string message,
        byte[] wavData,
        string refId,
        string language)
    {
        if (string.IsNullOrWhiteSpace(message) ||
            !ReplaceGeneratedAlarmAudio(characterName, alarmId, wavData, refId, language))
        {
            return false;
        }

        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        CharacterAlarmVoiceRecord record = metadata.alarms.Find(item =>
            item != null &&
            item.id == alarmId &&
            string.Equals(item.source, "generated", StringComparison.OrdinalIgnoreCase));
        if (record == null)
        {
            return false;
        }

        record.message = message.Trim();
        return Save(metadata);
    }

    public static bool DeleteGeneratedAlarm(string characterName, string alarmId)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(alarmId))
        {
            return false;
        }

        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        CharacterAlarmVoiceRecord record = metadata.alarms.Find(item =>
            item != null &&
            item.id == alarmId &&
            string.Equals(item.source, "generated", StringComparison.OrdinalIgnoreCase));
        if (record == null)
        {
            return false;
        }

        metadata.alarms.Remove(record);
        if (!Save(metadata))
        {
            return false;
        }

        try
        {
            string path = Path.Combine(
                GetAlarmDirectory(characterName),
                record.audioFileName ?? string.Empty);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                $"[CharacterAlarmVoice] Deleted metadata but WAV cleanup failed. char={characterName}, alarm={alarmId}, error={e.Message}");
        }

        return true;
    }

    public static bool SetDefaultAlarmHidden(
        string characterName,
        string alarmId,
        bool hidden)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(alarmId))
        {
            return false;
        }

        CharacterAlarmVoiceMetadata metadata = Load(characterName);
        bool alreadyHidden = metadata.hiddenDefaultAlarmIds.Contains(alarmId);
        if (hidden && !alreadyHidden)
        {
            metadata.hiddenDefaultAlarmIds.Add(alarmId);
        }
        else if (!hidden && alreadyHidden)
        {
            metadata.hiddenDefaultAlarmIds.Remove(alarmId);
        }
        return Save(metadata);
    }

    public static List<CharacterAlarmPlaybackCandidate> GetDisplayCandidates(
        string characterName,
        CharacterAlarmVoiceCatalog catalog)
    {
        List<CharacterAlarmPlaybackCandidate> candidates = new List<CharacterAlarmPlaybackCandidate>();
        CharacterAlarmVoiceMetadata metadata = Load(characterName);

        if (catalog != null)
        {
            IReadOnlyList<CharacterAlarmVoiceCatalog.DefaultAlarmVoice> defaults = catalog.GetDefaults(characterName);
            for (int i = 0; i < defaults.Count; i++)
            {
                CharacterAlarmVoiceCatalog.DefaultAlarmVoice item = defaults[i];
                if (item == null)
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(item.id) &&
                    metadata.hiddenDefaultAlarmIds.Contains(item.id))
                {
                    continue;
                }
                CharacterAlarmDefaultState state =
                    FindDefaultState(metadata, item.id);

                candidates.Add(new CharacterAlarmPlaybackCandidate
                {
                    id = item.id,
                    label = string.IsNullOrWhiteSpace(item.label) ? "기본" + (i + 1) : item.label,
                    message = state != null &&
                              state.hasMessageOverride
                        ? state.message
                        : item.message,
                    isGenerated = false,
                    enabled = state != null &&
                              state.hasEnabledOverride
                        ? state.enabled
                        : item.enabled,
                    audioClip = item.audioClip
                });
            }
        }

        for (int i = 0; i < metadata.alarms.Count; i++)
        {
            CharacterAlarmVoiceRecord record = metadata.alarms[i];
            if (record == null)
            {
                continue;
            }

            candidates.Add(new CharacterAlarmPlaybackCandidate
            {
                id = record.id,
                label = string.IsNullOrWhiteSpace(record.label) ? "생성" + (i + 1) : record.label,
                message = record.message,
                isGenerated = true,
                enabled = record.enabled,
                audioFilePath = Path.Combine(GetAlarmDirectory(characterName), record.audioFileName ?? string.Empty),
                generatedRecord = record
            });
        }

        return candidates;
    }

    public static List<CharacterAlarmPlaybackCandidate> GetPlayableCandidates(
        string characterName,
        CharacterAlarmVoiceCatalog catalog)
    {
        List<CharacterAlarmPlaybackCandidate> displayCandidates = GetDisplayCandidates(characterName, catalog);
        List<CharacterAlarmPlaybackCandidate> playable = new List<CharacterAlarmPlaybackCandidate>();

        for (int i = 0; i < displayCandidates.Count; i++)
        {
            CharacterAlarmPlaybackCandidate candidate = displayCandidates[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.message))
            {
                continue;
            }

            if (candidate.isGenerated)
            {
                if (!candidate.enabled ||
                    string.IsNullOrWhiteSpace(candidate.audioFilePath) ||
                    !File.Exists(candidate.audioFilePath))
                {
                    continue;
                }
            }
            else if (!candidate.enabled || candidate.audioClip == null)
            {
                continue;
            }

            playable.Add(candidate);
        }

        return playable;
    }

    private static CharacterAlarmDefaultState FindDefaultState(
        CharacterAlarmVoiceMetadata metadata,
        string alarmId)
    {
        if (metadata == null ||
            metadata.defaultAlarmStates == null ||
            string.IsNullOrWhiteSpace(alarmId))
        {
            return null;
        }

        return metadata.defaultAlarmStates.Find(state =>
            state != null &&
            string.Equals(
                state.id,
                alarmId,
                StringComparison.Ordinal));
    }

    private static CharacterAlarmDefaultState GetOrCreateDefaultState(
        CharacterAlarmVoiceMetadata metadata,
        string alarmId)
    {
        CharacterAlarmDefaultState state =
            FindDefaultState(metadata, alarmId);
        if (state != null)
        {
            return state;
        }

        state = new CharacterAlarmDefaultState { id = alarmId };
        metadata.defaultAlarmStates.Add(state);
        return state;
    }

    private static string SanitizePathSegment(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
        {
            result = result.Replace(invalidChars[i], '_');
        }

        return result;
    }
}
