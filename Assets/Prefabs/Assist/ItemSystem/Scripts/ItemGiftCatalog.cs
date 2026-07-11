using System;
using System.Collections.Generic;
using UnityEngine;

// 선물 엔트리 — 공통 정체(ItemEntry) + 능력(인연도 상승량)
[Serializable]
public class ItemGiftEntry : ItemEntry
{
    public int affinityPoints;  // 증정 시 인연도(affinity) 상승량 — 설계 오너: CharacterDetail/Affinity_Store_Integration.md
}

// 선물 카테고리 카탈로그 (에셋) — 정체에 더해 "증정 시 인연도 상승량" 능력을 소유한다
[CreateAssetMenu(fileName = "ItemGiftCatalog", menuName = "ItemSystem/Item Gift Catalog")]
public class ItemGiftCatalog : ItemCategoryCatalog
{
    [SerializeField] private List<ItemGiftEntry> entries = new List<ItemGiftEntry>();  // 등록된 선물 목록 (필드명 "entries" 고정 — 도구가 SerializedObject로 기록)

    private Dictionary<string, ItemGiftEntry> map;  // 키 조회 캐시

    // 키→엔트리 맵 구성
    private void BuildMap()
    {
        map = new Dictionary<string, ItemGiftEntry>();

        foreach (ItemGiftEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (map.ContainsKey(entry.key))
            {
                // 중복 키는 스킵
                Debug.LogWarning($"[ItemSystem][ItemGiftCatalog] 중복 키 스킵: {entry.key}");
                continue;
            }

            map.Add(entry.key, entry);
        }
    }

    // 맵이 없으면 구성
    private void EnsureMap()
    {
        if (map == null)
        {
            BuildMap();
        }
    }

    // 키로 선물 엔트리 조회 (없으면 null)
    public ItemGiftEntry GetGift(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue(key, out ItemGiftEntry entry))
        {
            return entry;
        }

        return null;
    }

    // 키로 공통 엔트리 조회 (없으면 null)
    public override ItemEntry GetEntry(string key)
    {
        return GetGift(key);
    }

    // 키 존재 여부
    public override bool Contains(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return map.ContainsKey(key);
    }

    // 공통 엔트리 목록 — IReadOnlyList<T> 공변으로 List<ItemGiftEntry>를 복사 없이 그대로 반환
    public override IReadOnlyList<ItemEntry> BaseEntries
    {
        get
        {
            return entries;
        }
    }

    // 인스펙터 편집 시 맵 무효화
    private void OnValidate()
    {
        map = null;
    }
}
