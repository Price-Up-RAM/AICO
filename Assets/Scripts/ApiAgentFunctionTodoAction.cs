using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public class ApiAgentFunctionTodoAction : MonoBehaviour
{
    private static ApiAgentFunctionTodoAction instance;
    public static ApiAgentFunctionTodoAction Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ApiAgentFunctionTodoAction>();
            }

            if (instance == null)
            {
                GameObject obj = new GameObject("ApiAgentFunctionTodoAction");
                instance = obj.AddComponent<ApiAgentFunctionTodoAction>();
                DontDestroyOnLoad(obj);
            }

            return instance;
        }
    }

    public bool GetItems(string dateText, out string message)
    {
        if (!TryParseDate(dateText, false, out DateTime date, out message))
        {
            return false;
        }

        JarvisTodoStore store = EnsureStore();
        List<JarvisTodoStore.TodoItem> items = store.GetItemsByDate(date);
        int completed = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].isCompleted)
            {
                completed++;
            }
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        builder.Append(" TODO: ");
        builder.Append(items.Count);
        builder.Append(" total, ");
        builder.Append(completed);
        builder.Append(" completed");

        for (int i = 0; i < items.Count; i++)
        {
            builder.AppendLine();
            builder.Append(items[i].isCompleted ? "- [x] " : "- [ ] ");
            if (!string.IsNullOrEmpty(items[i].time))
            {
                builder.Append(items[i].time);
                builder.Append(" ");
            }
            builder.Append(items[i].content);
        }

        message = builder.ToString();
        return true;
    }

    public bool AddItem(string dateText, string content, string time, out string message)
    {
        if (!TryParseDate(dateText, false, out DateTime date, out message))
        {
            return false;
        }

        string cleanContent = (content ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cleanContent))
        {
            message = "TODO content is empty.";
            return false;
        }

        JarvisTodoStore.TodoItem item = EnsureStore().AddItem(date, time ?? string.Empty, cleanContent);
        message = "TODO added: " + FormatItem(item);
        return true;
    }

    public bool CompleteItem(string dateText, string keyword, out string message)
    {
        if (!TryParseDate(dateText, false, out DateTime date, out message))
        {
            return false;
        }

        JarvisTodoStore store = EnsureStore();
        List<JarvisTodoStore.TodoItem> matches = FindKeywordMatches(store.GetItemsByDate(date), keyword, false);
        if (!TryGetSingleMatch(matches, keyword, out JarvisTodoStore.TodoItem item, out message))
        {
            return false;
        }

        store.SetCompleted(item.id, true);
        message = "TODO completed: " + FormatItem(item);
        return true;
    }

    public bool DeleteItem(string dateText, string keyword, out string message)
    {
        if (!TryParseDate(dateText, false, out DateTime date, out message))
        {
            return false;
        }

        JarvisTodoStore store = EnsureStore();
        List<JarvisTodoStore.TodoItem> matches = FindKeywordMatches(store.GetItemsByDate(date), keyword, null);
        if (!TryGetSingleMatch(matches, keyword, out JarvisTodoStore.TodoItem item, out message))
        {
            return false;
        }

        string deletedText = FormatItem(item);
        store.DeleteItem(item.id);
        message = "TODO deleted: " + deletedText;
        return true;
    }

    public bool ShowTodoList(string dateText, out string message)
    {
        if (!TryParseDate(dateText, true, out DateTime date, out message))
        {
            return false;
        }

        if (UIManager.Instance == null)
        {
            message = "UIManager is missing.";
            return false;
        }

        UIManager.Instance.ShowTODOList(date);
        message = "TODOList opened: " + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryParseDate(string dateText, bool allowTodayFallback, out DateTime date, out string message)
    {
        if (string.IsNullOrWhiteSpace(dateText))
        {
            if (allowTodayFallback)
            {
                date = DateTime.Now.Date;
                message = string.Empty;
                return true;
            }

            date = DateTime.MinValue;
            message = "Date is required in yyyy-MM-dd format.";
            return false;
        }

        if (DateTime.TryParseExact(dateText.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            date = date.Date;
            message = string.Empty;
            return true;
        }

        message = "Invalid date format. Use yyyy-MM-dd.";
        return false;
    }

    private static List<JarvisTodoStore.TodoItem> FindKeywordMatches(
        List<JarvisTodoStore.TodoItem> items,
        string keyword,
        bool? completedFilter
    )
    {
        List<JarvisTodoStore.TodoItem> matches = new List<JarvisTodoStore.TodoItem>();
        string cleanKeyword = (keyword ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cleanKeyword))
        {
            return matches;
        }

        for (int i = 0; i < items.Count; i++)
        {
            JarvisTodoStore.TodoItem item = items[i];
            if (item == null)
            {
                continue;
            }

            if (completedFilter.HasValue && item.isCompleted != completedFilter.Value)
            {
                continue;
            }

            string searchable = FormatItem(item);
            if (searchable.IndexOf(cleanKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                matches.Add(item);
            }
        }

        return matches;
    }

    private static bool TryGetSingleMatch(
        List<JarvisTodoStore.TodoItem> matches,
        string keyword,
        out JarvisTodoStore.TodoItem item,
        out string message
    )
    {
        item = null;
        if (matches.Count == 0)
        {
            message = "No TODO matched keyword: " + (keyword ?? string.Empty);
            return false;
        }

        if (matches.Count == 1)
        {
            item = matches[0];
            message = string.Empty;
            return true;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("Multiple TODO items matched keyword: ");
        builder.Append(keyword ?? string.Empty);
        for (int i = 0; i < matches.Count; i++)
        {
            builder.AppendLine();
            builder.Append("- ");
            builder.Append(FormatItem(matches[i]));
        }

        message = builder.ToString();
        return false;
    }

    private static string FormatItem(JarvisTodoStore.TodoItem item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string prefix = string.IsNullOrEmpty(item.time) ? string.Empty : item.time + " ";
        return prefix + item.content;
    }

    private static JarvisTodoStore EnsureStore()
    {
        if (JarvisTodoStore.Instance != null)
        {
            return JarvisTodoStore.Instance;
        }

        GameObject storeObject = new GameObject("JarvisTodoStore");
        JarvisTodoStore store = storeObject.AddComponent<JarvisTodoStore>();
        DontDestroyOnLoad(storeObject);
        return store;
    }
}
