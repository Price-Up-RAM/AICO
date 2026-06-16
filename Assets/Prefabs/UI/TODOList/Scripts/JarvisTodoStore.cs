using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class JarvisTodoStore : MonoBehaviour
{
    [Serializable]
    public class TodoItem
    {
        public string id;
        public string content;
        public bool isCompleted;
        public string dateKey;
        public string time;
        public int idx;
    }

    [Serializable]
    private class TodoSaveData
    {
        public List<TodoItem> items = new List<TodoItem>();
    }

    public static JarvisTodoStore Instance { get; private set; }
    public event Action Changed;

    private readonly TodoSaveData saveData = new TodoSaveData();
    private string savePath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        savePath = Path.Combine(Application.persistentDataPath, "jarvis_todolist.json");
        Load();
    }

    public List<TodoItem> GetItemsByDate(DateTime date)
    {
        string dateKey = ToDateKey(date);
        List<TodoItem> result = saveData.items.FindAll(item => item != null && item.dateKey == dateKey);
        result.Sort(CompareItems);
        return result;
    }

    public Dictionary<string, int> GetCountsByDate(int year, int month)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < saveData.items.Count; i++)
        {
            TodoItem item = saveData.items[i];
            if (item == null || string.IsNullOrEmpty(item.dateKey))
            {
                continue;
            }

            if (!DateTime.TryParseExact(item.dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                continue;
            }

            if (date.Year != year || date.Month != month)
            {
                continue;
            }

            counts.TryGetValue(item.dateKey, out int count);
            counts[item.dateKey] = count + 1;
        }

        return counts;
    }

    public int GetCompletedCount(DateTime date)
    {
        List<TodoItem> items = GetItemsByDate(date);
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].isCompleted)
            {
                count++;
            }
        }

        return count;
    }

    public TodoItem AddItem(DateTime date, string content)
    {
        return AddItem(date, string.Empty, content);
    }

    public TodoItem AddItem(DateTime date, string time, string content)
    {
        string normalizedTime = NormalizeTime(time);
        string cleanContent = (content ?? string.Empty).Trim();
        ExtractLeadingTime(ref normalizedTime, ref cleanContent);

        TodoItem item = new TodoItem
        {
            id = Guid.NewGuid().ToString("N"),
            content = cleanContent,
            dateKey = ToDateKey(date),
            time = normalizedTime,
            idx = GetNextIdx(ToDateKey(date)),
            isCompleted = false
        };

        saveData.items.Add(item);
        SaveAndNotify();
        return item;
    }

    public void SetCompleted(string id, bool isCompleted)
    {
        TodoItem item = FindById(id);
        if (item == null)
        {
            return;
        }

        bool wasCompleted = item.isCompleted;
        if (wasCompleted == isCompleted)
        {
            return;
        }

        item.isCompleted = isCompleted;
        if (wasCompleted && !isCompleted)
        {
            ReassignIndexes(item.dateKey);
        }

        SaveAndNotify();
    }

    public void SetContent(string id, string content)
    {
        TodoItem item = FindById(id);
        if (item == null)
        {
            return;
        }

        string normalizedTime = string.Empty;
        string cleanContent = (content ?? string.Empty).Trim();
        ExtractLeadingTime(ref normalizedTime, ref cleanContent);
        normalizedTime = NormalizeTime(normalizedTime);

        if (item.time == normalizedTime && item.content == cleanContent)
        {
            return;
        }

        item.time = normalizedTime;
        item.content = cleanContent;
        SaveAndNotify();
    }

    public void DeleteItem(string id)
    {
        int removed = saveData.items.RemoveAll(item => item != null && item.id == id);
        if (removed > 0)
        {
            SaveAndNotify();
        }
    }

    public void Reorder(DateTime date, List<string> orderedIds)
    {
        if (orderedIds == null)
        {
            return;
        }

        string dateKey = ToDateKey(date);
        for (int i = 0; i < orderedIds.Count; i++)
        {
            TodoItem item = FindById(orderedIds[i]);
            if (item != null && item.dateKey == dateKey)
            {
                item.idx = i;
            }
        }

        SaveAndNotify();
    }

    public void ReassignIndexes(DateTime date)
    {
        ReassignIndexes(ToDateKey(date));
        SaveAndNotify();
    }

    private void Load()
    {
        saveData.items.Clear();
        if (!File.Exists(savePath))
        {
            return;
        }

        try
        {
            TodoSaveData loaded = JsonUtility.FromJson<TodoSaveData>(File.ReadAllText(savePath));
            if (loaded != null && loaded.items != null)
            {
                saveData.items.AddRange(loaded.items);
                NormalizeLoadedItems();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[JarvisTodoStore] Load failed: " + ex.Message);
        }
    }

    private void SaveAndNotify()
    {
        Save();
        Changed?.Invoke();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(savePath, JsonUtility.ToJson(saveData, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[JarvisTodoStore] Save failed: " + ex.Message);
        }
    }

    private void NormalizeLoadedItems()
    {
        Dictionary<string, int> nextByDate = new Dictionary<string, int>();
        for (int i = 0; i < saveData.items.Count; i++)
        {
            TodoItem item = saveData.items[i];
            if (item == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(item.id))
            {
                item.id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrEmpty(item.dateKey))
            {
                item.dateKey = ToDateKey(DateTime.Now);
            }

            item.time = NormalizeTime(item.time);
            if (!nextByDate.ContainsKey(item.dateKey))
            {
                nextByDate[item.dateKey] = 0;
            }

            if (item.idx < 0)
            {
                item.idx = nextByDate[item.dateKey];
            }

            nextByDate[item.dateKey] = Mathf.Max(nextByDate[item.dateKey], item.idx + 1);
        }
    }

    private TodoItem FindById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return saveData.items.Find(item => item != null && item.id == id);
    }

    private int GetNextIdx(string dateKey)
    {
        int maxIdx = -1;
        for (int i = 0; i < saveData.items.Count; i++)
        {
            TodoItem item = saveData.items[i];
            if (item != null && item.dateKey == dateKey)
            {
                maxIdx = Mathf.Max(maxIdx, item.idx);
            }
        }

        return maxIdx + 1;
    }

    private void ReassignIndexes(string dateKey)
    {
        if (string.IsNullOrEmpty(dateKey))
        {
            return;
        }

        List<TodoItem> items = saveData.items.FindAll(item => item != null && item.dateKey == dateKey);
        items.Sort(CompareItems);
        for (int i = 0; i < items.Count; i++)
        {
            items[i].idx = i;
        }
    }

    private static int CompareItems(TodoItem left, TodoItem right)
    {
        if (left.isCompleted != right.isCompleted)
        {
            return left.isCompleted ? 1 : -1;
        }

        int idxCompare = left.idx.CompareTo(right.idx);
        if (idxCompare != 0)
        {
            return idxCompare;
        }

        bool leftHasTime = !string.IsNullOrEmpty(left.time);
        bool rightHasTime = !string.IsNullOrEmpty(right.time);
        if (leftHasTime && rightHasTime)
        {
            int timeCompare = string.CompareOrdinal(left.time, right.time);
            if (timeCompare != 0)
            {
                return timeCompare;
            }
        }

        if (leftHasTime != rightHasTime)
        {
            return leftHasTime ? -1 : 1;
        }

        return string.CompareOrdinal(left.id, right.id);
    }

    private static string ToDateKey(DateTime date)
    {
        return date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string NormalizeTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (TimeSpan.TryParseExact(trimmed, new[] { "h\\:mm", "hh\\:mm" }, CultureInfo.InvariantCulture, out TimeSpan time))
        {
            return time.ToString("hh\\:mm", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    private static void ExtractLeadingTime(ref string normalizedTime, ref string content)
    {
        if (!string.IsNullOrEmpty(normalizedTime) || string.IsNullOrWhiteSpace(content) || content.Length < 4)
        {
            return;
        }

        string[] parts = content.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        string parsedTime = NormalizeTime(parts[0]);
        if (string.IsNullOrEmpty(parsedTime))
        {
            return;
        }

        normalizedTime = parsedTime;
        content = parts.Length > 1 ? parts[1].Trim() : string.Empty;
    }
}
