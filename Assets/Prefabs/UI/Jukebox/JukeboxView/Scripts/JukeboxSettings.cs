using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Jukebox 설정 저장/복원(공유). JukeboxView(BGM+마스터)와 JukeboxEnvironmentView(SFX)가
/// 같은 파일(persistentDataPath/jukebox_settings.json)을 읽고 쓴다.
/// </summary>
[Serializable]
public class JukeboxTrackState
{
    public string id;
    public bool enabled;
    public float volume = 1f;
    public int minInterval = 30;
    public int maxInterval = 60;
}

[Serializable]
public class JukeboxSaveData
{
    public float masterVolume = 0.8f;
    public string selectedBgm = string.Empty; // 드롭다운으로 선택된 BGM id (빈 값=끄기)
    public List<JukeboxTrackState> tracks = new List<JukeboxTrackState>();
}

public static class JukeboxSettings
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "jukebox_settings.json");

    public static JukeboxSaveData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                JukeboxSaveData d = JsonUtility.FromJson<JukeboxSaveData>(File.ReadAllText(FilePath));
                if (d != null)
                {
                    return d;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Jukebox] 설정 로드 실패: " + e.Message);
        }
        return new JukeboxSaveData();
    }

    public static void Save(JukeboxSaveData d)
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(d, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Jukebox] 설정 저장 실패: " + e.Message);
        }
    }

    public static JukeboxTrackState GetState(JukeboxSaveData d, string id)
    {
        foreach (JukeboxTrackState s in d.tracks)
        {
            if (s.id == id)
            {
                return s;
            }
        }
        JukeboxTrackState created = new JukeboxTrackState { id = id };
        d.tracks.Add(created);
        return created;
    }
}
