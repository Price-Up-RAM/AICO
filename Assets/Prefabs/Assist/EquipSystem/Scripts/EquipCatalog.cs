using System;
using System.Collections.Generic;
using UnityEngine;

// 카탈로그 엔트리: 키 문자열 ↔ 실제 프리팹 연결 + 슬롯/미세보정
[Serializable]
public class EquipEntry
{
    public string key;                  // 악세서리 식별 키 (예: "hairpin_placeholder")
    public GameObject prefab;           // 부착할 프리팹
    public string targetSlotId;         // 어느 슬롯에 붙는가 (예: "hairpin")
    public float fitBias = 1f;          // 볼륨-핏 후 미세 크기 보정 (기본 1.0)
    public Vector3 positionOffset;      // 소켓 로컬 위치 미세 오프셋 (아이템 고유)
    public Vector3 rotationOffset;      // 소켓 로컬 회전(오일러) 오프셋 (아이템 고유)
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
