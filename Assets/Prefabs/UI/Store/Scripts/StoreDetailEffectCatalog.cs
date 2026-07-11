using System;
using System.Collections.Generic;
using UnityEngine;

// 이펙트 상세(프리뷰) 카탈로그 엔트리: fx 키 ↔ 파티클 프리팹/시뮬레이트 시각 메타데이터 (순수 캡처 설정 — 아이콘 소스는 StoreEntry 소유)
[Serializable]
public class StoreDetailEffectEntry
{
    public string key;                 // fx_* 키 (StoreCatalog 와 같은 키 공간)
    public GameObject effectPrefab;    // 파티클 프리팹 (읽기 전용 참조, null 이면 캡처 스킵)
    public float simulateTime = 1.5f;  // ParticleSystem.Simulate 진행 시각(초) — looping 이펙트의 정상 상태 도달 시간
}

// Store 이펙트 상세(프리뷰) 카탈로그 (에셋). key→엔트리 조회. StoreDetailPoseCatalog와 같은 lazy map 패턴.
[CreateAssetMenu(fileName = "StoreDetailEffectCatalog", menuName = "Store/Store Detail Effect Catalog")]
public class StoreDetailEffectCatalog : ScriptableObject
{
    [SerializeField] private List<StoreDetailEffectEntry> entries = new List<StoreDetailEffectEntry>();  // 등록된 이펙트 목록

    private Dictionary<string, StoreDetailEffectEntry> map;  // 키 조회 캐시

    // 키→엔트리 맵 구성
    private void BuildMap()
    {
        map = new Dictionary<string, StoreDetailEffectEntry>();

        foreach (StoreDetailEffectEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (map.ContainsKey(entry.key))
            {
                // 중복 키는 스킵
                Debug.LogWarning($"[Store][StoreDetailEffectCatalog] 중복 키 스킵: {entry.key}");
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
    public StoreDetailEffectEntry Get(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue(key, out StoreDetailEffectEntry entry))
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

    public IReadOnlyList<StoreDetailEffectEntry> Entries
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
