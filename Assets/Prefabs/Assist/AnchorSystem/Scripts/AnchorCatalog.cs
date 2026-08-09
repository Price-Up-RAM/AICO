using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class AnchorEntry
{
    public string key;
    public GameObject overridePrefab;
    [FormerlySerializedAs("targetSlotId")]
    public string anchorName;

    public GameObject prefab
    {
        get
        {
            if (overridePrefab != null)
            {
                return overridePrefab;
            }

            ItemCatalog itemCatalog = ItemCatalog.Default;
            ItemEntry item = itemCatalog != null ? itemCatalog.Get(key) : null;
            return item != null ? item.prefab : null;
        }
    }
}

[CreateAssetMenu(fileName = "AnchorCatalog", menuName = "Assist/Anchor Catalog")]
public sealed class AnchorCatalog : ScriptableObject
{
    [SerializeField] private List<AnchorEntry> entries = new List<AnchorEntry>();

    private Dictionary<string, AnchorEntry> map;

    public AnchorEntry Get(string key)
    {
        EnsureMap();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        map.TryGetValue(key, out AnchorEntry entry);
        return entry;
    }

    public bool Contains(string key)
    {
        return Get(key) != null;
    }

    public IReadOnlyList<AnchorEntry> Entries
    {
        get
        {
            return entries;
        }
    }

    private void EnsureMap()
    {
        if (map != null)
        {
            return;
        }

        map = new Dictionary<string, AnchorEntry>();
        foreach (AnchorEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || map.ContainsKey(entry.key))
            {
                continue;
            }

            map.Add(entry.key, entry);
        }
    }

    private void OnValidate()
    {
        foreach (AnchorEntry entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            entry.key = entry.key != null ? entry.key.Trim() : string.Empty;
            entry.anchorName =
                entry.anchorName != null ? entry.anchorName.Trim() : string.Empty;
        }

        map = null;
    }
}
