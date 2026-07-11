using System;
using System.Collections.Generic;
using UnityEngine;

// 아이템 공통 정체 엔트리 — 모든 카테고리 엔트리의 부모 (능력 필드는 카테고리별 파생 엔트리가 확장)
[Serializable]
public class ItemEntry
{
    public string key;              // 아이템 식별 키 (Inventory/Equip/Store 카탈로그와 같은 키 공간)
    public string displayName;      // 표시 이름
    public Sprite icon;             // 대표 아이콘 (null 허용)
    [TextArea] public string description;  // 설명
    public int maxStack = 99;       // 스택 상한 — 후속 이관 전까지 런타임 클램프는 InventoryCatalog 값이 적용된다
                                    // (이관 계획: WORKLOG 후속 — InventorySystem이 이 값을 따르게)
}

// 카테고리 카탈로그 공통 베이스 — 레지스트리(ItemCatalog)가 이 타입으로 하위를 참조한다
public abstract class ItemCategoryCatalog : ScriptableObject
{
    // 키로 공통 엔트리 조회 (없으면 null)
    public abstract ItemEntry GetEntry(string key);

    // 키 존재 여부
    public abstract bool Contains(string key);

    // 공통 엔트리 목록 — IReadOnlyList<T>는 공변이라 파생 엔트리 리스트를 복사 없이 그대로 반환 가능
    public abstract IReadOnlyList<ItemEntry> BaseEntries { get; }
}
