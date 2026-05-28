using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TODOManager : MonoBehaviour
{
    public static TODOManager Instance { get; private set; }

    [Serializable]
    public class TODOItem
    {
        public string id;
        public string content;
        public bool isCompleted;
        public string dateKey; // "yyyy-MM-dd"
        public int order;
    }

    [Serializable]
    private class TODOSaveData
    {
        public List<TODOItem> items = new List<TODOItem>();
    }

    private TODOSaveData m_saveData = new TODOSaveData();
    private string SavePath => Path.Combine(Application.persistentDataPath, "todo_data.json");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public List<TODOItem> GetItemsByDate(DateTime date)
    {
        string key = DateKey(date);
        var list = m_saveData.items.FindAll(item => item.dateKey == key);
        list.Sort((a, b) =>
        {
            // 완료 항목은 무조건 뒤로
            if (a.isCompleted != b.isCompleted)
                return a.isCompleted ? 1 : -1;
            return a.order.CompareTo(b.order);
        });
        return list;
    }

    public TODOItem AddItem(string content, DateTime date)
    {
        var existing = GetItemsByDate(date);
        var item = new TODOItem
        {
            id = Guid.NewGuid().ToString(),
            content = content,
            isCompleted = false,
            dateKey = DateKey(date),
            order = existing.Count
        };
        m_saveData.items.Add(item);
        Save();
        return item;
    }

    // direction: -1 = 위로, +1 = 아래로
    public void MoveItem(string id, int direction)
    {
        var target = m_saveData.items.Find(i => i.id == id);
        if (target == null) return;

        var items = m_saveData.items.FindAll(i => i.dateKey == target.dateKey);
        items.Sort((a, b) => a.order.CompareTo(b.order));

        int idx = items.FindIndex(i => i.id == id);
        int swapIdx = idx + direction;

        if (swapIdx < 0 || swapIdx >= items.Count) return;

        int tempOrder = items[idx].order;
        items[idx].order = items[swapIdx].order;
        items[swapIdx].order = tempOrder;

        Save();
    }

    public void SetCompleted(string id, bool completed)
    {
        var item = m_saveData.items.Find(i => i.id == id);
        if (item == null) return;
        item.isCompleted = completed;
        Save();
    }

    public void DeleteItem(string id)
    {
        m_saveData.items.RemoveAll(i => i.id == id);
        Save();
    }

    public bool HasItemsOnDate(DateTime date)
    {
        string key = DateKey(date);
        return m_saveData.items.Exists(i => i.dateKey == key);
    }

    private string DateKey(DateTime date) => date.ToString("yyyy-MM-dd");

    private void Save()
    {
        File.WriteAllText(SavePath, JsonUtility.ToJson(m_saveData, true));
    }

    private void Load()
    {
        if (!File.Exists(SavePath)) return;
        try
        {
            string json = File.ReadAllText(SavePath);
            m_saveData = JsonUtility.FromJson<TODOSaveData>(json) ?? new TODOSaveData();
        }
        catch
        {
            m_saveData = new TODOSaveData();
        }
    }
}
