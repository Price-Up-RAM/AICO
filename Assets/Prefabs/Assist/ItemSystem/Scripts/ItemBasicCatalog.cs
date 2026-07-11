using System.Collections.Generic;
using UnityEngine;

// 능력 없는 기본 카테고리 카탈로그 (에셋) — 장착물/포즈/이펙트/잡화처럼 "정체"만 필요한 아이템용.
// 능력(수치)이 필요한 카테고리는 별도 파생 카탈로그(예: ItemGiftCatalog)를 쓴다.
[CreateAssetMenu(fileName = "ItemBasicCatalog", menuName = "ItemSystem/Item Basic Catalog")]
public class ItemBasicCatalog : ItemCategoryCatalog
{
    [SerializeField] private List<ItemEntry> entries = new List<ItemEntry>();  // 등록된 아이템 목록 (필드명 "entries" 고정 — 도구가 SerializedObject로 기록)

    private Dictionary<string, ItemEntry> map;  // 키 조회 캐시

    // 키→엔트리 맵 구성
    private void BuildMap()
    {
        map = new Dictionary<string, ItemEntry>();

        foreach (ItemEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (map.ContainsKey(entry.key))
            {
                // 중복 키는 스킵
                Debug.LogWarning($"[ItemSystem][ItemBasicCatalog] 중복 키 스킵: {entry.key}");
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

    // 키로 엔트리 조회 (없으면 null)
    public override ItemEntry GetEntry(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue(key, out ItemEntry entry))
        {
            return entry;
        }

        return null;
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

    // 공통 엔트리 목록 (복사 없이 내부 리스트 반환)
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
