using System.IO;
using UnityEngine;

// 인벤토리(gold/item1~3) JSON 저장/로드. AlarmRepository 복제 + 평문 JSON.
// 경로: persistentDataPath/inventory.json  (MISSION_Design.md §6.2)
public class InventoryRepository
{
    private const string SaveFileName = "inventory.json";

    public string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public InventoryData Load()
    {
        string savePath = GetSavePath();
        if (!File.Exists(savePath))
        {
            return new InventoryData();
        }

        string json = File.ReadAllText(savePath);
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(json.Trim()))
        {
            return new InventoryData();
        }

        InventoryData data = JsonUtility.FromJson<InventoryData>(json);
        return data ?? new InventoryData();
    }

    public void Save(InventoryData data)
    {
        if (data == null)
        {
            data = new InventoryData();
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
