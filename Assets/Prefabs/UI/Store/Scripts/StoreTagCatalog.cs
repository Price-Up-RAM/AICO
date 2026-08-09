using System;
using System.Collections.Generic;
using UnityEngine;

// 상점 아이콘 소스. File이 기본 — icon이 비어 있으면 NoImage.
// Runtime은 프리뷰 리그 캡처(포즈/이펙트처럼 Detail 카탈로그에 등재된 키에서만 유효 — 미등재면 NoImage).
public enum StoreIconType
{
    File,
    Runtime,
}

// 상점 카탈로그 엔트리: 상품 키 ↔ 가격/아이콘 메타데이터.
// 태그(탭)는 이 엔트리를 소유한 StoreCatalog의 태그 레지스트리가 결정한다 (엔트리에 tab 필드 없음).
[Serializable]
public class StoreEntry
{
    public string key;              // 아이템 식별 키 (InventoryCatalog/EquipCatalog와 같은 키 공간)
    public string displayName;      // InventoryCatalog에 없을 때 폴백 표기
    public int price = 100;         // 구매가(G)
    public StoreIconType iconType = StoreIconType.File;  // 아이콘 소스 (상점 소유 — Inventory 아이콘과 별개)
    public Sprite icon;             // File 모드 아이콘 (비면 NoImage)
    public string detailText;       // 카드 보조 표기 자유 텍스트 (예: "호감도 +100") — 성능 수치가 아니라 표시 전용
}

// 한 태그(탭)의 상품 카탈로그 (에셋). 카탈로그 3계층 중 2계층:
//   StoreCatalog(태그 레지스트리) → StoreTagCatalog(태그별 상품) / Detail 프리뷰 카탈로그는 별도.
// 새 상품 추가 = 해당 태그의 StoreTagCatalog 에셋에만 엔트리를 추가하면 된다.
[CreateAssetMenu(fileName = "StoreTagCatalog", menuName = "Store/Store Tag Catalog")]
public class StoreTagCatalog : ScriptableObject
{
    [SerializeField] private List<StoreEntry> entries = new List<StoreEntry>();  // 등록된 상품 목록

    private Dictionary<string, StoreEntry> map;  // 키 조회 캐시

    // 키→엔트리 맵 구성
    private void BuildMap()
    {
        map = new Dictionary<string, StoreEntry>();

        foreach (StoreEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (map.ContainsKey(entry.key))
            {
                // 중복 키는 스킵
                Debug.LogWarning($"[Store][StoreTagCatalog] 중복 키 스킵: {entry.key}");
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
    public StoreEntry Get(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue(key, out StoreEntry entry))
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

    public IReadOnlyList<StoreEntry> Entries
    {
        get
        {
            return entries;
        }
    }

    // 유효 상품 목록 (등록 순서 유지, 빈 키/중복 키(대표 외) 제외)
    public List<StoreEntry> ValidEntries()
    {
        EnsureMap();

        List<StoreEntry> result = new List<StoreEntry>();

        foreach (StoreEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            // 중복 키는 맵에 등록된 대표 엔트리만 노출
            if (map.TryGetValue(entry.key, out StoreEntry canonical) == false || canonical != entry)
            {
                continue;
            }

            result.Add(entry);
        }

        return result;
    }

    // 인스펙터 편집 시 맵 무효화
    private void OnValidate()
    {
        map = null;
    }
}
