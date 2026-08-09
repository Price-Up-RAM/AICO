using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemRuntimeSpritePoseEntry
{
    public string key;
    public AnimationClip clip;
    public float freezeMin = 0.2f;
    public float freezeMax = 0.8f;
}

[CreateAssetMenu(
    fileName = "ItemRuntimeSpritePoseCatalog",
    menuName = "ItemSystem/Runtime Sprite Pose Catalog")]
public sealed class ItemRuntimeSpritePoseCatalog : ScriptableObject
{
    [SerializeField] private List<ItemRuntimeSpritePoseEntry> entries =
        new List<ItemRuntimeSpritePoseEntry>();

    private Dictionary<string, ItemRuntimeSpritePoseEntry> map;

    public IReadOnlyList<ItemRuntimeSpritePoseEntry> Entries => entries;

    public ItemRuntimeSpritePoseEntry Get(string key)
    {
        EnsureMap();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        map.TryGetValue(key, out ItemRuntimeSpritePoseEntry entry);
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

        map = new Dictionary<string, ItemRuntimeSpritePoseEntry>(StringComparer.Ordinal);
        foreach (ItemRuntimeSpritePoseEntry entry in entries)
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
