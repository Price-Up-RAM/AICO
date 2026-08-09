using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AnchorTargetEntry
{
    public string key;
    public GameObject prefab;
}

[CreateAssetMenu(
    fileName = "AnchorTargetCatalog",
    menuName = "Assist/Anchor Target Catalog")]
public sealed class AnchorTargetCatalog : ScriptableObject
{
    [SerializeField] private List<AnchorTargetEntry> entries =
        new List<AnchorTargetEntry>();

    private Dictionary<string, AnchorTargetEntry> map;

    public IReadOnlyList<AnchorTargetEntry> Entries
    {
        get
        {
            return entries;
        }
    }

    public AnchorTargetEntry Get(string key)
    {
        EnsureMap();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        map.TryGetValue(key, out AnchorTargetEntry entry);
        return entry;
    }

    private void EnsureMap()
    {
        if (map != null)
        {
            return;
        }

        map = new Dictionary<string, AnchorTargetEntry>(
            StringComparer.Ordinal);
        foreach (AnchorTargetEntry entry in entries)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.key) ||
                map.ContainsKey(entry.key))
            {
                continue;
            }

            map.Add(entry.key, entry);
        }
    }

    private void OnValidate()
    {
        foreach (AnchorTargetEntry entry in entries)
        {
            if (entry != null)
            {
                entry.key =
                    entry.key != null ? entry.key.Trim() : string.Empty;
            }
        }

        map = null;
    }
}
