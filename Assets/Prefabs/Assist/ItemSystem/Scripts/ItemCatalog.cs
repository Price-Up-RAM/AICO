using System;
using System.Collections.Generic;
using UnityEngine;

// 카테고리 레지스트리 행: 카테고리 이름 ↔ 해당 카테고리 카탈로그
[Serializable]
public class ItemCategoryEntry
{
    public string category;              // 카테고리 이름 (예: "선물")
    public ItemCategoryCatalog catalog;  // 해당 카테고리 카탈로그 (null 허용 — 빈 카테고리)
}

// 아이템 카테고리 레지스트리 (에셋, Resources/ItemCatalog.asset). 카탈로그 2계층 중 1계층:
//   ItemCatalog(카테고리 레지스트리) → ItemCategoryCatalog 파생(카테고리별 아이템).
// 이 에셋은 "어떤 카테고리가 있고 각 카테고리가 어느 카탈로그를 쓰는지"만 관리한다.
// 새 아이템 추가 = 해당 카테고리의 하위 카탈로그 에셋에만 추가 (이 레지스트리는 수정 불필요).
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "ItemSystem/Item Catalog (Category Registry)")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private List<ItemCategoryEntry> categories = new List<ItemCategoryEntry>();  // 등록된 카테고리 목록 (필드명 "categories" 고정 — 도구가 SerializedObject로 기록)
    [Header("Related Catalogs")]
    [SerializeField] private AnchorCatalog anchorCatalog;
    [SerializeField] private EquipCatalog equipCatalog;
    [SerializeField] private ItemRuntimeSpritePoseCatalog runtimeSpritePoseCatalog;
    [SerializeField] private ItemRuntimeSpriteEffectCatalog runtimeSpriteEffectCatalog;

    private static ItemCatalog cachedDefault;

    [NonSerialized] private bool duplicateCategoryWarned;  // Categories()가 여러 번 불려도 경고는 1회만 남긴다

    public IReadOnlyList<ItemCategoryEntry> CategoryEntries
    {
        get
        {
            return categories;
        }
    }

    public AnchorCatalog AnchorCatalog => anchorCatalog;
    public EquipCatalog EquipCatalog => equipCatalog;
    public ItemRuntimeSpritePoseCatalog RuntimeSpritePoseCatalog => runtimeSpritePoseCatalog;
    public ItemRuntimeSpriteEffectCatalog RuntimeSpriteEffectCatalog => runtimeSpriteEffectCatalog;

    public static ItemCatalog Default
    {
        get
        {
            if (cachedDefault == null)
            {
                cachedDefault = Resources.Load<ItemCatalog>("ItemCatalog");
            }

            return cachedDefault;
        }
    }

    // Resources 경로 밖으로 이동해도 씬/다른 카탈로그의 직렬화 참조로 로드되면 기본 카탈로그가 된다.
    private void OnEnable()
    {
        if (cachedDefault == null)
        {
            cachedDefault = this;
        }
    }

    private void OnDisable()
    {
        if (cachedDefault == this)
        {
            cachedDefault = null;
        }
    }

    // 유효 카테고리 이름 목록 (빈 문자열/중복 스킵, 등록 순서 유지)
    public List<string> Categories()
    {
        List<string> result = new List<string>();

        foreach (ItemCategoryEntry categoryEntry in categories)
        {
            if (categoryEntry == null || string.IsNullOrWhiteSpace(categoryEntry.category))
            {
                continue;
            }

            if (result.Contains(categoryEntry.category))
            {
                // 중복 카테고리는 스킵
                if (duplicateCategoryWarned == false)
                {
                    duplicateCategoryWarned = true;
                    Debug.LogWarning($"[ItemSystem][ItemCatalog] 중복 카테고리 스킵: {categoryEntry.category}");
                }
                continue;
            }

            result.Add(categoryEntry.category);
        }

        return result;
    }

    // 카테고리 이름으로 하위 카탈로그 조회 (없으면 null)
    public ItemCategoryCatalog CatalogForCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return null;
        }

        foreach (ItemCategoryEntry categoryEntry in categories)
        {
            if (categoryEntry == null || string.IsNullOrWhiteSpace(categoryEntry.category))
            {
                continue;
            }

            if (categoryEntry.category == category)
            {
                return categoryEntry.catalog;
            }
        }

        return null;
    }

    // 키로 엔트리 조회: 카테고리 등록 순으로 자식 카탈로그에 위임, 첫 히트 반환 (없으면 null)
    // 레지스트리 자체 캐시 없음 — 자식이 lazy map을 보유하므로 카테고리 수만큼의 O(1) 조회로 충분하다.
    public ItemEntry Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (ItemCategoryEntry categoryEntry in categories)
        {
            if (categoryEntry == null || categoryEntry.catalog == null)
            {
                continue;
            }

            ItemEntry entry = categoryEntry.catalog.GetEntry(key);

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

    // key가 속한 첫 카테고리 이름 (없으면 null)
    public string CategoryForKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (ItemCategoryEntry categoryEntry in categories)
        {
            if (categoryEntry == null || categoryEntry.catalog == null || string.IsNullOrWhiteSpace(categoryEntry.category))
            {
                continue;
            }

            if (categoryEntry.catalog.Contains(key))
            {
                return categoryEntry.category;
            }
        }

        return null;
    }

    // 선물 능력 조회: 자식 중 ItemGiftCatalog 타입에서 키를 찾아 인연도 상승량 반환 (미등재/비선물이면 false/0)
    public bool TryGetGiftPoints(string key, out int affinityPoints)
    {
        affinityPoints = 0;

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        foreach (ItemCategoryEntry categoryEntry in categories)
        {
            if (categoryEntry == null || categoryEntry.catalog == null)
            {
                continue;
            }

            ItemGiftCatalog giftCatalog = categoryEntry.catalog as ItemGiftCatalog;

            if (giftCatalog == null)
            {
                continue;
            }

            ItemGiftEntry giftEntry = giftCatalog.GetGift(key);

            if (giftEntry != null)
            {
                affinityPoints = giftEntry.affinityPoints;
                return true;
            }
        }

        return false;
    }

    // 인스펙터 편집 시 경고 래치 리셋 (수정된 구성으로 다시 1회 경고할 수 있게)
    private void OnValidate()
    {
        duplicateCategoryWarned = false;
    }
}
