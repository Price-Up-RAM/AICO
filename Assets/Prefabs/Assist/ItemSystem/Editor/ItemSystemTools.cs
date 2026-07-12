using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ItemSystem 셋업 도구: 카테고리별 아이템 카탈로그 6종(장착물/포즈/이펙트/선물/잡화 — Store 태그와 동일 이름 — + 재화) 생성
// + 카테고리 레지스트리(ItemCatalog) 등록. StoreTools의 additive 관용구를 따른다
// (기존 엔트리/행은 불변 + 누락 기본 키만 추가, 기본 키의 빈 icon과 기본 카테고리 행의 빈 catalog만 보충
//  — 인스펙터 편집이 재실행에도 보존된다).
// 예외: 이번 라운드에 한해 ItemEntry 플래그 5종의 1회 스키마 마이그레이션이 기존 행에도 기본값을 기록한다
// (각 마이그레이션 블록의 주석 참조 — 다음 라운드에 블록 제거로 불변 원칙 복귀).
public static class ItemSystemTools
{
    // ItemEntry 플래그 5종의 카테고리별 기본값 묶음 (신규 행 기록 + 1회 스키마 마이그레이션 공용)
    private struct ItemFlagDefaults
    {
        public bool isBuyable;      // 상점 구매 가능
        public bool isSellable;     // 상점 판매 가능
        public bool isCountable;    // 여러 개 보유 가능 (false = 1개 한정)
        public bool isEquipable;    // 장착 가능 (EquipSystem 대상)
        public bool isSpendable;    // 사용 시 소모 (소모품/증정품)

        public ItemFlagDefaults(bool buyable, bool sellable, bool countable, bool equipable, bool spendable)
        {
            isBuyable = buyable;
            isSellable = sellable;
            isCountable = countable;
            isEquipable = equipable;
            isSpendable = spendable;
        }
    }

    private const string Root = "Assets/Prefabs/Assist/ItemSystem";
    private const string ResourcesDir = Root + "/Resources";
    private const string CatalogPath = ResourcesDir + "/ItemCatalog.asset";
    private const string EquipPath = ResourcesDir + "/ItemEquipCatalog.asset";
    private const string PosePath = ResourcesDir + "/ItemPoseCatalog.asset";
    private const string EffectPath = ResourcesDir + "/ItemEffectCatalog.asset";
    private const string GiftPath = ResourcesDir + "/ItemGiftCatalog.asset";
    private const string MiscPath = ResourcesDir + "/ItemMiscCatalog.asset";
    private const string CurrencyPath = ResourcesDir + "/ItemCurrencyCatalog.asset";

    // batchmode -executeMethod 진입점 (다이얼로그 절대 금지)
    public static void BatchBuildAll()
    {
        CreateCatalog();
        AssetDatabase.SaveAssets();
        Debug.Log("[ItemSystem][ItemSystemTools] BatchBuildAll 완료.");
    }

    // ── 1) 카탈로그 생성/갱신: 카테고리별 카탈로그 5종 → 카테고리 레지스트리(ItemCatalog) ──
    [MenuItem("Tools/ItemSystem/1. Create Catalog")]
    public static ItemCatalog CreateCatalog()
    {
        EnsureDir(ResourcesDir);

        // (a) 카테고리별 카탈로그 5종 — key는 InventoryCatalog/EquipCatalog/StoreCatalog와 같은 키 공간(16키).
        //     장착물 아이콘은 Assets/Model/Sprite 원본을 guid로 로드(Store/Inventory와 같은 원본 PNG),
        //     나머지는 icon null(표시는 후속 시스템이 폴백). 장착물/포즈/이펙트/잡화는 능력 없는 ItemBasicCatalog,
        //     선물만 능력(affinityPoints)을 가진 ItemGiftCatalog 타입.
        Sprite[] equipIcons = {
            LoadSpriteByGuid("8aa77dfd81aed7a42ad1413b98563049", "arona_a_chipao"),
            LoadSpriteByGuid("55381bb255052cf4e93142224e9246c4", "arona_a_idolfrontribbon"),
            LoadSpriteByGuid("f77a588aa9c001a498023ffc85b4b4be", "arona_a_pareo"),
            LoadSpriteByGuid("e493f40f0fbd4644a93445e5eded5528", "hairpin_placeholder")
        };
        // 플래그 기본값 — 장착물: 1개 한정 장착 대상 / 포즈·이펙트: 1개 한정 비소모 언락형 /
        // 선물·잡화: 다수 보유 + 사용 시 소모.
        ItemBasicCatalog equipCat = CreateCategoryCatalog<ItemBasicCatalog>(
            EquipPath,
            new[] { "arona_a_chipao", "arona_a_idolfrontribbon", "arona_a_pareo", "hairpin_placeholder" },
            new[] { "치파오", "아이돌 프론트리본", "파레오", "헤어핀" },
            equipIcons,
            null,
            new ItemFlagDefaults(true, true, false, true, false));
        ItemBasicCatalog poseCat = CreateCategoryCatalog<ItemBasicCatalog>(
            PosePath,
            new[] { "pose_greeting", "pose_dance", "pose_sit" },
            new[] { "포즈: 인사", "포즈: 댄스", "포즈: 앉기" },
            new Sprite[] { null, null, null },
            null,
            new ItemFlagDefaults(true, true, false, false, false));
        ItemBasicCatalog fxCat = CreateCategoryCatalog<ItemBasicCatalog>(
            EffectPath,
            new[] { "fx_pat_heart", "fx_pat_star", "fx_click_sparkle" },
            new[] { "쓰다듬기: 하트", "쓰다듬기: 별", "클릭: 반짝임" },
            new Sprite[] { null, null, null },
            null,
            new ItemFlagDefaults(true, true, false, false, false));
        ItemGiftCatalog giftCat = CreateCategoryCatalog<ItemGiftCatalog>(
            GiftPath,
            new[] { "gift_s", "gift_m", "gift_l" },
            new[] { "선물(소)", "선물(중)", "선물(대)" },
            new Sprite[] { null, null, null },
            new[] { 10, 30, 100 },
            new ItemFlagDefaults(true, true, true, false, true));
        ItemBasicCatalog miscCat = CreateCategoryCatalog<ItemBasicCatalog>(
            MiscPath,
            new[] { "snack_banana", "potion_energy", "ticket_random" },
            new[] { "바나나", "에너지 드링크", "랜덤 티켓" },
            new Sprite[] { null, null, null },
            null,
            new ItemFlagDefaults(true, true, true, false, true));

        // (a2) 재화 카탈로그 — 재화의 "정의"만 등록한다 (잔액·증감은 CurrencyManager 소유). 일단 골드 1종.
        ItemCurrencyCatalog currencyCat = CreateCurrencyCatalog(
            CurrencyPath,
            new[] { "currency_gold" },
            new[] { "골드" },
            new[] { "기본 재화. 상점 구매/판매와 미션 보상에 사용된다." },
            new[] { false });
        // ── 차후 재화 추가 예시 (Gem — 유료/프리미엄 재화) ──
        // 위 네 배열에 한 줄씩만 추가하면 끝. 지갑/증감/저장은 키 기반이라 CurrencyManager 코드 수정이
        // 필요 없다 (키 상수는 CurrencyManager의 GemKey 주석 참조):
        //     new[] { "currency_gold", "currency_gem" },
        //     new[] { "골드", "젬" },
        //     new[] { "기본 재화. ...", "프리미엄 재화. 유료 획득 전용." },
        //     new[] { false, true });

        // (b) ItemCatalog(카테고리 레지스트리) 갱신 — 기존 에셋을 재사용해 guid 보존.
        //     additive: 기존 카테고리 행은 보존(사용자 재배열/추가/제거 존중), 기본 카테고리 중 누락분만 뒤에 추가하고
        //     기본 카테고리 행의 catalog 참조가 비어 있으면 채워만 준다.
        ItemCatalog cat = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<ItemCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        string[] categoryNames = { "장착물", "포즈", "이펙트", "선물", "잡화", "재화" };
        ItemCategoryCatalog[] categoryCatalogs = { equipCat, poseCat, fxCat, giftCat, miscCat, currencyCat };

        SerializedObject so = new SerializedObject(cat);
        SerializedProperty list = so.FindProperty("categories");

        HashSet<string> existingCategories = new HashSet<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i);
            string categoryName = row.FindPropertyRelative("category").stringValue;
            existingCategories.Add(categoryName);

            // 기본 카테고리인데 카탈로그 참조가 비어 있으면 보충 (사용자가 지정한 참조는 불변)
            SerializedProperty catalogProp = row.FindPropertyRelative("catalog");
            if (catalogProp.objectReferenceValue == null)
            {
                int defaultIndex = System.Array.IndexOf(categoryNames, categoryName);
                if (defaultIndex >= 0)
                {
                    catalogProp.objectReferenceValue = categoryCatalogs[defaultIndex];
                }
            }
        }

        int addedCategories = 0;
        for (int i = 0; i < categoryNames.Length; i++)
        {
            if (existingCategories.Contains(categoryNames[i]))
            {
                continue;
            }

            list.arraySize = list.arraySize + 1;
            SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("category").stringValue = categoryNames[i];
            e.FindPropertyRelative("catalog").objectReferenceValue = categoryCatalogs[i];
            addedCategories = addedCategories + 1;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(cat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ItemSystem][ItemSystemTools] 카테고리 레지스트리 준비: {CatalogPath} (카테고리 {categoryNames.Length}개 / 신규 {addedCategories}개).");
        return cat;
    }

    // 카테고리 카탈로그 Load-or-Create 후 "누락 키만" 기본 엔트리로 추가한다 (공용 additive 헬퍼).
    // 기존 엔트리 필드는 사용자 소유라 절대 덮어쓰지 않되, 기본 키와 일치하는 행의 icon이 비어 있고
    // 기본 icon이 있으면 보충만 한다. affinityPoints는 선택 배열 — null이면 기본 엔트리(ItemEntry),
    // non-null이면 선물 엔트리(ItemGiftEntry)로 간주해 신규 행에만 기록한다.
    // flags는 카테고리 공통 기본값 — 신규 행에 명시 기록 + 기존 행 1회 스키마 마이그레이션(아래 블록)에 사용.
    private static T CreateCategoryCatalog<T>(string path, string[] keys, string[] names, Sprite[] icons, int[] affinityPoints, ItemFlagDefaults flags) where T : ItemCategoryCatalog
    {
        T cat = AssetDatabase.LoadAssetAtPath<T>(path);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(cat, path);
        }

        SerializedObject so = new SerializedObject(cat);
        SerializedProperty list = so.FindProperty("entries");

        // ── 1회 스키마 마이그레이션: bool 신필드(플래그 5종)는 구 데이터에서 전부 false로 로드 —
        //    기존 행에도 카테고리 기본값을 기록해 스키마를 도입한다. 다음 라운드부터 기존 행 불변
        //    원칙 복귀 시 이 블록 제거. ──
        for (int i = 0; i < list.arraySize; i++)
        {
            WriteFlags(list.GetArrayElementAtIndex(i), flags);
        }

        Dictionary<string, SerializedProperty> existing = new Dictionary<string, SerializedProperty>();
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i);
            string rowKey = row.FindPropertyRelative("key").stringValue;
            if (existing.ContainsKey(rowKey) == false)
            {
                existing.Add(rowKey, row);
            }
        }

        int added = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            if (existing.TryGetValue(keys[i], out SerializedProperty row))
            {
                // 빈 값 보충: icon이 비어 있고 기본 icon이 있으면 채움 (사용자가 지정한 icon은 불변)
                SerializedProperty iconProp = row.FindPropertyRelative("icon");
                if (iconProp.objectReferenceValue == null && icons[i] != null)
                {
                    iconProp.objectReferenceValue = icons[i];
                }

                // 빈 값 보충: 기본 선물 키의 affinityPoints가 0(미지정 = '조용히 죽은 선물')이면 기본 수치를 채움.
                // 이 필드에서 0은 빈 값과 구분되지 않으므로 보충 대상으로 보고, 0이 아닌 사용자 값은 불변.
                if (affinityPoints != null && affinityPoints[i] > 0)
                {
                    SerializedProperty pointsProp = row.FindPropertyRelative("affinityPoints");
                    if (pointsProp != null && pointsProp.intValue == 0)
                    {
                        pointsProp.intValue = affinityPoints[i];
                    }
                }

                continue;
            }

            // arraySize 증가는 마지막 요소를 복제하므로 모든 필드를 명시적으로 덮어쓴다
            list.arraySize = list.arraySize + 1;
            SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("key").stringValue = keys[i];
            e.FindPropertyRelative("displayName").stringValue = names[i];
            e.FindPropertyRelative("icon").objectReferenceValue = icons[i];
            e.FindPropertyRelative("description").stringValue = "";
            e.FindPropertyRelative("maxStack").intValue = 99;
            WriteFlags(e, flags);
            if (affinityPoints != null)
            {
                e.FindPropertyRelative("affinityPoints").intValue = affinityPoints[i];
            }
            added = added + 1;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(cat);
        Debug.Log($"[ItemSystem][ItemSystemTools] 카테고리 카탈로그 준비: {path} (신규 {added} / 유지 {list.arraySize - added}).");
        return cat;
    }

    // 재화 카탈로그 Load-or-Create 후 "누락 키만" 추가한다 (additive — 기존 엔트리 불변).
    // 재화 엔트리는 premium(bool)과 description을 함께 기록하며 icon은 인스펙터 지정 몫(null 시작).
    private static ItemCurrencyCatalog CreateCurrencyCatalog(string path, string[] keys, string[] names, string[] descriptions, bool[] premiums)
    {
        ItemCurrencyCatalog cat = AssetDatabase.LoadAssetAtPath<ItemCurrencyCatalog>(path);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<ItemCurrencyCatalog>();
            AssetDatabase.CreateAsset(cat, path);
        }

        SerializedObject so = new SerializedObject(cat);
        SerializedProperty list = so.FindProperty("entries");

        // 재화는 플래그 체계 밖 — 상점 거래·장착·소모 어휘가 재화(잔액형)에 적용되지 않아 전부
        // 보수적 값으로 고정한다: buyable/sellable/equipable/spendable=false, countable=true(잔액은 수량 개념).
        ItemFlagDefaults currencyFlags = new ItemFlagDefaults(false, false, true, false, false);

        // ── 1회 스키마 마이그레이션: bool 신필드(플래그 5종)는 구 데이터에서 전부 false로 로드 —
        //    기존 행에도 위 기본값을 기록해 스키마를 도입한다. 다음 라운드부터 기존 행 불변
        //    원칙 복귀 시 이 블록 제거. ──
        for (int i = 0; i < list.arraySize; i++)
        {
            WriteFlags(list.GetArrayElementAtIndex(i), currencyFlags);
        }

        HashSet<string> existing = new HashSet<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            existing.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue);
        }

        int added = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            if (existing.Contains(keys[i]))
            {
                continue;
            }

            // arraySize 증가는 마지막 요소를 복제하므로 모든 필드를 명시적으로 덮어쓴다
            list.arraySize = list.arraySize + 1;
            SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("key").stringValue = keys[i];
            e.FindPropertyRelative("displayName").stringValue = names[i];
            e.FindPropertyRelative("icon").objectReferenceValue = null;
            e.FindPropertyRelative("description").stringValue = descriptions[i];
            e.FindPropertyRelative("maxStack").intValue = 99;  // 재화에서는 미사용 (ItemCurrencyEntry 주석 참조)
            WriteFlags(e, currencyFlags);
            e.FindPropertyRelative("premium").boolValue = premiums[i];
            added = added + 1;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(cat);
        Debug.Log($"[ItemSystem][ItemSystemTools] 재화 카탈로그 준비: {path} (신규 {added} / 유지 {list.arraySize - added}).");
        return cat;
    }

    // 엔트리 행에 ItemEntry 플래그 5종을 명시 기록한다 (신규 행 기록 + 1회 스키마 마이그레이션 공용)
    private static void WriteFlags(SerializedProperty row, ItemFlagDefaults flags)
    {
        row.FindPropertyRelative("isBuyable").boolValue = flags.isBuyable;
        row.FindPropertyRelative("isSellable").boolValue = flags.isSellable;
        row.FindPropertyRelative("isCountable").boolValue = flags.isCountable;
        row.FindPropertyRelative("isEquipable").boolValue = flags.isEquipable;
        row.FindPropertyRelative("isSpendable").boolValue = flags.isSpendable;
    }

    // guid → 아이콘 스프라이트 로드 (텍스처의 Sprite 서브에셋 — Store/InventorySystemTools와 같은 원본 PNG, Assets/Model/Sprite).
    // 로드 실패 시 null + Warning — 엔트리는 icon null로 남고 표시는 소비측이 폴백한다
    private static Sprite LoadSpriteByGuid(string guid, string keyForLog)
    {
        string iconPath = AssetDatabase.GUIDToAssetPath(guid);
        Sprite icon = string.IsNullOrEmpty(iconPath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (icon == null)
        {
            Debug.LogWarning($"[ItemSystem][ItemSystemTools] 아이콘 스프라이트 로드 실패: key={keyForLog} guid={guid} (icon null로 등록)");
        }
        return icon;
    }

    // 상대 경로 폴더 보장 (Directory.CreateDirectory + Refresh)
    private static void EnsureDir(string assetDir)
    {
        string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetDir);
        if (Directory.Exists(abs) == false)
        {
            Directory.CreateDirectory(abs);
            AssetDatabase.Refresh();
        }
    }
}
