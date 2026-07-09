using System;
using System.Collections.Generic;
using UnityEngine;

// 인벤토리 카탈로그 엔트리: 아이템 키 ↔ 표시 메타데이터
[Serializable]
public class InventoryEntry
{
    public string key;              // 아이템 식별 키 (EquipCatalog key와 동일 문자열 공간)
    public string displayName;      // 표시 이름
    public Sprite icon;             // 아이콘 (null이면 UI가 displayName 텍스트로 표시)
    [TextArea] public string description;   // 아이템 설명
    public int maxStack = 99;       // 스택당 최대 개수
    public string category;         // 분류 (예: "accessory")
}

// InventorySystem 전용 아이템 메타 카탈로그 (완전 독립, 에셋). key→엔트리 조회.
[CreateAssetMenu(fileName = "InventoryCatalog", menuName = "Assist/Inventory Catalog")]
public class InventoryCatalog : ScriptableObject
{
    [SerializeField] private List<InventoryEntry> entries = new List<InventoryEntry>();  // 등록된 아이템 목록

    private Dictionary<string, InventoryEntry> map;  // 키 조회 캐시

    // 키→엔트리 맵 구성
    private void BuildMap()
    {
        map = new Dictionary<string, InventoryEntry>();

        foreach (InventoryEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (map.ContainsKey(entry.key))
            {
                // 중복 키는 스킵
                Debug.LogWarning($"[InventoryCatalog] 중복 키 스킵: {entry.key}");
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
    public InventoryEntry Get(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue(key, out InventoryEntry entry))
        {
            return entry;
        }

        return null;
    }

    // 키 존재 여부
    public bool Contains(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return map.ContainsKey(key);
    }

    public IReadOnlyList<InventoryEntry> Entries
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
