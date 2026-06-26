using System.IO;
using UnityEngine;

// 미션 진행 상태 JSON 저장/로드. AlarmRepository 복제 + 1차 평문 JSON.
// 경로: persistentDataPath/missions.json  (MISSION_Design.md §6.1)
// (develop: HMAC 서명 검증 추가 예정)
public class MissionRepository
{
    private const string SaveFileName = "missions.json";

    public string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public MissionSaveData Load()
    {
        string savePath = GetSavePath();
        if (!File.Exists(savePath))
        {
            return new MissionSaveData();
        }

        string json = File.ReadAllText(savePath);
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(json.Trim()))
        {
            return new MissionSaveData();
        }

        MissionSaveData data = JsonUtility.FromJson<MissionSaveData>(json);
        if (data == null)
        {
            return new MissionSaveData();
        }

        if (data.progresses == null)
        {
            data.progresses = new System.Collections.Generic.List<MissionProgress>();
        }

        return data;
    }

    public void Save(MissionSaveData data)
    {
        if (data == null)
        {
            data = new MissionSaveData();
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
