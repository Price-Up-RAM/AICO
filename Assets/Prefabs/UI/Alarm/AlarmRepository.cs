using System.IO;
using UnityEngine;

public class AlarmRepository
{
    private const string SaveFileName = "alarms.json";

    // Return the local alarm save path.
    public string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    // Load alarms from local json.
    public AlarmSaveData Load()
    {
        string savePath = GetSavePath();
        if (!File.Exists(savePath))
        {
            return new AlarmSaveData();
        }

        string json = File.ReadAllText(savePath);
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(json.Trim()))
        {
            return new AlarmSaveData();
        }

        AlarmSaveData data = JsonUtility.FromJson<AlarmSaveData>(json);
        if (data == null)
        {
            return new AlarmSaveData();
        }

        if (data.alarms == null)
        {
            data.alarms = new System.Collections.Generic.List<AlarmItem>();
        }

        return data;
    }

    // Save alarms to local json.
    public void Save(AlarmSaveData data)
    {
        if (data == null)
        {
            data = new AlarmSaveData();
        }

        string savePath = GetSavePath();
        string directoryPath = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(savePath, json);
    }
}
