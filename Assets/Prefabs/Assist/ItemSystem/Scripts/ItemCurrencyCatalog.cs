using System;
using System.Collections.Generic;
using UnityEngine;

// 재화 엔트리 — 공통 정체(ItemEntry) + 재화 속성. 재화의 "정의"(이름/아이콘/설명/프리미엄 여부)만 소유하고,
// 잔액(누가 얼마)은 CurrencyManager 지갑 소유 — 아이템의 정체/보관 분리와 같은 원칙.
[Serializable]
public class ItemCurrencyEntry : ItemEntry
{
    public bool premium;  // 유료(프리미엄) 재화 여부 (Gem 등 — 획득 경로 제한/표시 구분용)
    // 상속 필드 maxStack은 재화에서는 사용하지 않는다 (잔액은 스택 개념이 아님 — CurrencyManager가 무시)
}

// 재화 카테고리 카탈로그 (에셋) — "카탈로그 등재 = 존재하는 재화" 불변식의 원천.
// CurrencyManager는 여기 등재된 키만 증감을 허용한다 (오타 키로 잔액이 생기는 사고 차단).
[CreateAssetMenu(fileName = "ItemCurrencyCatalog", menuName = "ItemSystem/Item Currency Catalog")]
public class ItemCurrencyCatalog : ItemCategoryCatalog
{
    [SerializeField] private List<ItemCurrencyEntry> entries = new List<ItemCurrencyEntry>();  // 등록된 재화 목록 (필드명 "entries" 고정 — 도구가 SerializedObject로 기록)

    private Dictionary<string, ItemCurrencyEntry> map;  // 키 조회 캐시

    // 키→엔트리 맵 구성
    private void BuildMap()
    {
        map = new Dictionary<string, ItemCurrencyEntry>();

        foreach (ItemCurrencyEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (map.ContainsKey(entry.key))
            {
                // 중복 키는 스킵
                Debug.LogWarning($"[ItemSystem][ItemCurrencyCatalog] 중복 키 스킵: {entry.key}");
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

    // 키로 재화 엔트리 조회 (없으면 null)
    public ItemCurrencyEntry GetCurrency(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue(key, out ItemCurrencyEntry entry))
        {
            return entry;
        }

        return null;
    }

    // 키로 공통 엔트리 조회 (없으면 null)
    public override ItemEntry GetEntry(string key)
    {
        return GetCurrency(key);
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

    // 공통 엔트리 목록 — IReadOnlyList<T> 공변으로 List<ItemCurrencyEntry>를 복사 없이 그대로 반환
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
