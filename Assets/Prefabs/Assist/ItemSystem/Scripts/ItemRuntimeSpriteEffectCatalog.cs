using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemRuntimeSpriteEffectEntry
{
    public string key;
    public GameObject effectPrefab;
    public float simulateTime = 1.5f;
}

[CreateAssetMenu(
    fileName = "ItemRuntimeSpriteEffectCatalog",
    menuName = "ItemSystem/Runtime Sprite Effect Catalog")]
public sealed class ItemRuntimeSpriteEffectCatalog : ScriptableObject
{
    [SerializeField] private List<ItemRuntimeSpriteEffectEntry> entries =
        new List<ItemRuntimeSpriteEffectEntry>();

    private Dictionary<string, ItemRuntimeSpriteEffectEntry> map;

    public IReadOnlyList<ItemRuntimeSpriteEffectEntry> Entries => entries;

    public ItemRuntimeSpriteEffectEntry Get(string key)
    {
        EnsureMap();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        map.TryGetValue(key, out ItemRuntimeSpriteEffectEntry entry);
        return entry;
    }

    public bool Contains(string key)
    {
        return Get(key) != null;
    }

    private void EnsureMap()
    {
        if (map != null)
        {
            return;
        }

        map = new Dictionary<string, ItemRuntimeSpriteEffectEntry>(StringComparer.Ordinal);
        foreach (ItemRuntimeSpriteEffectEntry entry in entries)
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
        map = null;
    }
}
