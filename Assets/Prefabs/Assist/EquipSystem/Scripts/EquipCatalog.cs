using System;
using System.Collections.Generic;
using UnityEngine;

// 카탈로그 엔트리: 키 문자열 ↔ 실제 프리팹 연결 + 자리 이름(해석 사다리) + 크기/미세보정
[Serializable]
public class EquipEntry
{
    public string key;                  // 악세서리 식별 키 (예: "hairpin_placeholder")
    public GameObject overridePrefab;   // Item 기본 프리팹 대신 사용할 선택적 override
    public string category;             // 배타 카테고리 (예: "hat", "bag") — 같은 카테고리는 소켓이 달라도 하나만 장착. 빈 값이면 소켓 단위 배타만 적용
    public string targetSlotId;         // 해석 사다리 2순위 (1순위는 key와 같은 이름의 소켓)
    public List<string> fallbackSlotIds = new List<string>();  // 3순위 폴백: 위가 없을 때 순서대로 시도할 범용 이름들 (예: head, chest, origin)
    public float sizeRatio = 1f;        // 최장변 = refDist 월드환산(rWorld) × 2 × 이 값
    public float fitBias = 1f;          // 최종 크기 미세 보정 (기본 1.0)
    public Vector3 positionOffsetRadii; // placeholder 로컬, rWorld(refDist) 배수 단위 — 무차원
    public Vector3 rotationOffset;      // 회전(오일러) 오프셋 (아이템 고유)

    public GameObject prefab
    {
        get
        {
            if (overridePrefab != null)
            {
                return overridePrefab;
            }

            ItemCatalog itemCatalog = ItemCatalog.Default;
            ItemEntry item = itemCatalog != null ? itemCatalog.Get(key) : null;
            return item != null ? item.prefab : null;
        }
    }
}

// 배타 카테고리 문자열 유틸 — 내용은 자유 문자열이되, 비교는 항상 Trim 후 대소문자 무시(OrdinalIgnoreCase)
public static class EquipCategory
{
    // 카테고리가 지정되어 있는지 (공백만 있으면 미지정 취급)
    public static bool HasValue(string category)
    {
        return string.IsNullOrWhiteSpace(category) == false;
    }

    // 두 카테고리가 같은지 (둘 다 지정된 경우에만 true 가능)
    public static bool IsSame(string a, string b)
    {
        if (HasValue(a) == false || HasValue(b) == false)
        {
            return false;
        }

        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

// EquipSystem 전용 아이템 카탈로그 (완전 독립, 에셋). key→엔트리 조회.
[CreateAssetMenu(fileName = "EquipCatalog", menuName = "Assist/Equip Catalog")]
public class EquipCatalog : ScriptableObject
{
    [SerializeField] private List<EquipEntry> entries = new List<EquipEntry>();  // 등록된 아이템 목록

    private Dictionary<string, EquipEntry> map;  // 키 조회 캐시

    // 키→엔트리 맵 구성
    private void BuildMap()
    {
        map = new Dictionary<string, EquipEntry>();

        foreach (EquipEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (map.ContainsKey(entry.key))
            {
                // 중복 키는 스킵
                Debug.LogWarning($"[EquipCatalog] 중복 키 스킵: {entry.key}");
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
    public EquipEntry Get(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue(key, out EquipEntry entry))
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

    public IReadOnlyList<EquipEntry> Entries
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
