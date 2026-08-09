using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemUseType
{
    Consume = 0,
    Equip = 1,
    Anchor = 2,
}

public enum ItemClass
{
    None = 0,
    Doll = 1,
}

public enum ItemIconType
{
    File = 0,
    Runtime = 1,
}

// 아이템 공통 정체 엔트리 — 모든 카테고리 엔트리의 부모 (능력 필드는 카테고리별 파생 엔트리가 확장)
[Serializable]
public class ItemEntry
{
    public string key;              // 아이템 식별 키 (Inventory/Equip/Store 카탈로그와 같은 키 공간)
    public string displayName;      // 표시 이름
    public Sprite icon;             // 대표 아이콘 (null 허용)
    [TextArea] public string description;  // 설명
    public int basePrice = 1000;    // 상점 기본 구매가. Store는 별도 가격을 복제하지 않는다.
    public ItemIconType iconType = ItemIconType.File;
    public GameObject prefab;       // Anchor/Equip 공용 기본 프리팹. 시스템 카탈로그 override가 우선한다.
    public int maxStack = 99;       // InventorySystem의 스택 상한 단일 원본
    public ItemUseType useType = ItemUseType.Consume;
    public ItemClass itemClass = ItemClass.None;

    // ── 최상위 메타데이터 플래그 6종. Store/Inventory/Equip이 이 값을 단일 원본으로 사용한다.
    //    bool 신필드는 구 직렬화 데이터에서 전부 false로 로드되므로 기존 행은 ItemSystemTools의
    //    1회 스키마 마이그레이션이 기본값을 기록한다. ──
    public bool isBuyable = true;    // 상점 구매 가능
    public bool isSellable = true;   // 상점 판매 가능
    public bool isCountable = true;  // 여러 개 보유 가능 (false = 유효 maxStack 1, 수량 UI 미표시)
    public bool isMainOnly;           // MAIN 인벤토리에만 존재 가능. CHAR/Anchor store 이동 금지
    public bool isEquipable;         // 장착 가능 (EquipSystem 대상)
    public bool isSpendable;         // 사용 시 소모 (소모품/증정품)

    public int EffectiveMaxStack
    {
        get
        {
            return isCountable ? Mathf.Max(1, maxStack) : 1;
        }
    }
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
