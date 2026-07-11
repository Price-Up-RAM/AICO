using System;
using System.Collections.Generic;
using UnityEngine;

// 태그 레지스트리 행: 탭 이름 ↔ 해당 태그의 상품 카탈로그
[Serializable]
public class StoreTagEntry
{
    public string tag;               // 탭 이름 (표시 문자열이자 식별자, 예: "포즈")
    public StoreTagCatalog catalog;  // 이 태그의 상품 카탈로그 (null 허용 — 빈 탭)
}

// 상점 태그 레지스트리 (에셋, Resources/StoreCatalog.asset). 카탈로그 3계층 중 1계층:
//   StoreCatalog(태그 레지스트리) → StoreTagCatalog(태그별 상품) / Detail 프리뷰 카탈로그는 별도.
// 이 에셋은 "어떤 탭이 있고 각 탭이 어느 카탈로그를 쓰는지"만 관리한다.
// 새 상품 추가 = 해당 태그의 StoreTagCatalog 에셋에만 추가 (이 레지스트리는 수정 불필요).
[CreateAssetMenu(fileName = "StoreCatalog", menuName = "Store/Store Catalog (Tag Registry)")]
public class StoreCatalog : ScriptableObject
{
    [SerializeField] private List<StoreTagEntry> tags = new List<StoreTagEntry>();  // 등록된 태그 목록

    [NonSerialized] private bool duplicateTagWarned;  // Tabs()가 리프레시마다 여러 번 불려 경고는 1회만 남긴다

    public IReadOnlyList<StoreTagEntry> TagEntries
    {
        get
        {
            return tags;
        }
    }

    // 유효 태그 이름 목록 (빈 문자열/중복 스킵, 등록 순서 유지)
    public List<string> Tabs()
    {
        List<string> result = new List<string>();

        foreach (StoreTagEntry tagEntry in tags)
        {
            if (tagEntry == null || string.IsNullOrWhiteSpace(tagEntry.tag))
            {
                continue;
            }

            if (result.Contains(tagEntry.tag))
            {
                // 중복 태그는 스킵
                if (duplicateTagWarned == false)
                {
                    duplicateTagWarned = true;
                    Debug.LogWarning($"[Store][StoreCatalog] 중복 태그 스킵: {tagEntry.tag}");
                }
                continue;
            }

            result.Add(tagEntry.tag);
        }

        return result;
    }

    // 탭 이름으로 태그 카탈로그 조회 (없으면 null)
    public StoreTagCatalog CatalogForTab(string tab)
    {
        if (string.IsNullOrEmpty(tab))
        {
            return null;
        }

        foreach (StoreTagEntry tagEntry in tags)
        {
            if (tagEntry == null || string.IsNullOrWhiteSpace(tagEntry.tag))
            {
                continue;
            }

            if (tagEntry.tag == tab)
            {
                return tagEntry.catalog;
            }
        }

        return null;
    }

    // 해당 탭의 상품 목록 (자식 카탈로그 위임, 없으면 빈 리스트)
    public List<StoreEntry> EntriesForTab(string tab)
    {
        StoreTagCatalog tagCatalog = CatalogForTab(tab);

        if (tagCatalog == null)
        {
            return new List<StoreEntry>();
        }

        return tagCatalog.ValidEntries();
    }

    // 키로 엔트리 조회: 태그 등록 순으로 자식 카탈로그에 위임, 첫 히트 반환 (없으면 null)
    // 레지스트리 자체 캐시 없음 — 자식이 lazy map을 보유하므로 태그 수만큼의 O(1) 조회로 충분하다.
    public StoreEntry Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (StoreTagEntry tagEntry in tags)
        {
            if (tagEntry == null || tagEntry.catalog == null)
            {
                continue;
            }

            StoreEntry entry = tagEntry.catalog.Get(key);

            if (entry != null)
            {
                return entry;
            }
        }

        return null;
    }

    // 키 존재 여부
    public bool Contains(string key)
    {
        return Get(key) != null;
    }

    // key가 속한 첫 태그 이름 (없으면 null)
    public string TagForKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (StoreTagEntry tagEntry in tags)
        {
            if (tagEntry == null || tagEntry.catalog == null || string.IsNullOrWhiteSpace(tagEntry.tag))
            {
                continue;
            }

            if (tagEntry.catalog.Contains(key))
            {
                return tagEntry.tag;
            }
        }

        return null;
    }

    // 인스펙터 편집 시 경고 래치 리셋 (수정된 구성으로 다시 1회 경고할 수 있게)
    private void OnValidate()
    {
        duplicateTagWarned = false;
    }
}
