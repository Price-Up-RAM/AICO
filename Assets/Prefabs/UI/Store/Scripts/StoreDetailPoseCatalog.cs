using System;
using System.Collections.Generic;
using UnityEngine;

// 포즈 상세(프리뷰) 카탈로그 엔트리: 포즈 키 ↔ 미리보기 클립/정지 구간 메타데이터 (순수 캡처 설정 — 아이콘 소스는 StoreEntry 소유)
[Serializable]
public class StoreDetailPoseEntry
{
    public string key;              // 포즈 식별 키 (StoreCatalog/InventoryCatalog와 같은 키 공간, 예: pose_greeting)
    public AnimationClip clip;      // 미리보기용 휴머노이드 클립 (로드 실패 시 null — 리그가 스킵)
    public float freezeMin = 0.2f;  // 정지 위치 하한 (정규화 0~1)
    public float freezeMax = 0.8f;  // 정지 위치 상한 (정규화 0~1)
}

// Store 포즈 상세(프리뷰) 카탈로그 (에셋). key→엔트리 조회. StoreTagCatalog와 같은 lazy map 패턴.
[CreateAssetMenu(fileName = "StoreDetailPoseCatalog", menuName = "Store/Store Detail Pose Catalog")]
public class StoreDetailPoseCatalog : ScriptableObject
{
    [SerializeField] private List<StoreDetailPoseEntry> entries = new List<StoreDetailPoseEntry>();  // 등록된 포즈 목록

    private Dictionary<string, StoreDetailPoseEntry> map;  // 키 조회 캐시

    // 키→엔트리 맵 구성
    private void BuildMap()
    {
        map = new Dictionary<string, StoreDetailPoseEntry>();

        foreach (StoreDetailPoseEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            if (map.ContainsKey(entry.key))
            {
                // 중복 키는 스킵
                Debug.LogWarning($"[Store][StoreDetailPoseCatalog] 중복 키 스킵: {entry.key}");
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
    public StoreDetailPoseEntry Get(string key)
    {
        EnsureMap();

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (map.TryGetValue(key, out StoreDetailPoseEntry entry))
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

    public IReadOnlyList<StoreDetailPoseEntry> Entries
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
