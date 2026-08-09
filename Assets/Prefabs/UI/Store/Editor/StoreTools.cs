using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Store(상점) 셋업 도구: 카탈로그 생성(태그 레지스트리 + 태그별 상품 카탈로그 5종 + InventoryCatalog additive 등록
// + 포즈/이펙트 프리뷰 상세(Detail) 카탈로그 + NoImage 스프라이트),
// UI 프리팹 베이크(확인 팝업 → 상점 패널 순), SUIT-Bold 폰트 적용, 데모씬 빌드. InventorySystemTools 패턴을 따른다.
public static class StoreTools
{
    private const string Root = "Assets/Prefabs/UI/Store";
    private const string ResourcesDir = Root + "/Resources";
    private const string PrefabsDir = Root + "/Prefabs";
    private const string DemoDir = Root + "/Demo";
    private const string CatalogPath = ResourcesDir + "/StoreCatalog.asset";
    private const string TagEquipCatalogPath = ResourcesDir + "/StoreEquipCatalog.asset";
    private const string TagPoseCatalogPath = ResourcesDir + "/StorePoseCatalog.asset";
    private const string TagFxCatalogPath = ResourcesDir + "/StoreEffectCatalog.asset";
    private const string TagGiftCatalogPath = ResourcesDir + "/StoreGiftCatalog.asset";
    private const string TagMiscCatalogPath = ResourcesDir + "/StoreMiscCatalog.asset";
    private const string DetailPoseCatalogPath = ResourcesDir + "/StoreDetailPoseCatalog.asset";
    private const string DetailEffectCatalogPath = ResourcesDir + "/StoreDetailEffectCatalog.asset";
    private const string NoImagePath = ResourcesDir + "/StoreNoImage.png";
    private const string SpritesDir = Root + "/Sprites";
    private const string RerollIconPath = SpritesDir + "/RerollDieIcon.png";
    private const string PrefabPath = PrefabsDir + "/StorePanel.prefab";
    private const string ConfirmPrefabPath = PrefabsDir + "/StoreConfirm.prefab";
    private const string DemoScenePath = DemoDir + "/StoreDemo.unity";
    private const string FontPath = "Assets/FontAssets/SUIT-Bold.asset";

    // 약결합 대상 (InventorySystem — 데이터/프리팹만 참조, 코드는 수정하지 않음)
    private const string InventoryCatalogPath = "Assets/Prefabs/Assist/InventorySystem/Resources/InventoryCatalog_Demo.asset";
    private const string InventoryPanelPath = "Assets/Prefabs/Assist/InventorySystem/InventoryPanel.prefab";

    // 포즈 프리뷰 리그 참조 대상 (모두 읽기 전용 — 절대 수정하지 않음)
    private const string AronaPocPath = "Assets/Prefabs/Char_toon/arona_6_clean_POC.prefab";
    private const string PoseGreetingClipPath = "Assets/Animation/AIGen/WaveLeftHand.anim";
    private const string PoseDanceClipPath = "Assets/Animation/mixamo/Dance_loop/Gangnam Style.anim";
    private const string PoseSitFbxPath = "Assets/Char/diana/sitting in a chair in front of the desk looking around.fbx";
    private const string PortraitLayerName = "PortraitModel";  // TagManager 레이어 (코드 참조 0 — 프리뷰 격리용). 리그와 이름으로 공유

    // 이펙트 프리뷰 캡처 대상 파티클 프리팹 (모두 읽기 전용 참조 — 절대 수정하지 않음)
    private const string FxLoveAuraPath = "Assets/Prefabs/Fx/Fx_LoveAura.prefab";  // 실제 머리 쓰다듬기 하트 이펙트 (확정 바인딩)
    private const string FxPatStarPath = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR4 Falling Stars.prefab";  // 별이 쏟아지는 이펙트 — '쓰다듬기: 별'과 직접 매치 (Fx_MagicAura는 룬 문양이라 부적합)
    private const string FxClickSparklePath = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Misc/CFXR2 Shiny Item (Loop).prefab";  // Stars/Circles 반짝임 루프 — '클릭: 반짝임'과 매치

    // 전체 셋업 (카탈로그 + UI 프리팹 + 폰트 + 데모씬)
    [MenuItem("Tools/Store/Setup All (catalog + UI prefab + font + demo scene)")]
    public static void SetupAll()
    {
        CreateCatalog();
        BuildUiPrefab();
        ApplyFont();
        BuildDemoScene();
        Debug.Log("[Store][StoreTools] Setup All 완료.");
    }

    // batchmode -executeMethod 진입점 (다이얼로그 절대 금지)
    public static void BatchBuildAll()
    {
        CreateCatalog();
        BuildUiPrefab();
        ApplyFont();
        BuildDemoScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[Store][StoreTools] BatchBuildAll 완료.");
    }

    // ── 1) 카탈로그 생성/갱신: 레거시 프리뷰 에셋 정리 → 태그별 상품 카탈로그 5종 → 태그 레지스트리(StoreCatalog)
    //      → InventoryCatalog additive 등록 → 포즈/이펙트 프리뷰 상세 카탈로그 2종 → NoImage 스프라이트 ──
    [MenuItem("Tools/Store/1. Create Catalog")]
    public static StoreCatalog CreateCatalog()
    {
        EnsureDir(ResourcesDir);

        // (a) 레거시 정리: 클래스 리네임으로 구 프리뷰 에셋(StorePoseCatalog.asset/StoreEffectCatalog.asset)이
        //     Detail 타입으로 재해석된다 — 태그 카탈로그가 같은 경로를 쓰므로 먼저 삭제해 자리를 비운다
        //     (신형 StoreTagCatalog 타입이면 타입 불일치로 로드가 null을 반환해 삭제되지 않는다)
        DeleteLegacyPreviewAsset<StoreDetailPoseCatalog>(TagPoseCatalogPath);
        DeleteLegacyPreviewAsset<StoreDetailEffectCatalog>(TagFxCatalogPath);

        // (b) 태그별 상품 카탈로그 5종 — key는 InventoryCatalog/EquipCatalog와 같은 키 공간 (장착물 4종은 공유 키,
        //     나머지 12종은 상점 전용 키). 아이콘은 상점 카탈로그 소유(iconType File/Runtime — Inventory 아이콘과 별개),
        //     detailText는 카드 보조 표기 전용 자유 텍스트(성능 수치 아님 — 아이템 성능은 아이템 시스템 소유)
        Sprite[] equipIcons = {
            LoadSpriteByGuid("8aa77dfd81aed7a42ad1413b98563049", "arona_a_chipao"),
            LoadSpriteByGuid("55381bb255052cf4e93142224e9246c4", "arona_a_idolfrontribbon"),
            LoadSpriteByGuid("f77a588aa9c001a498023ffc85b4b4be", "arona_a_pareo"),
            LoadSpriteByGuid("e493f40f0fbd4644a93445e5eded5528", "hairpin_placeholder")
        };
        StoreTagCatalog equipCat = CreateTagCatalog(
            TagEquipCatalogPath,
            new[] { "arona_a_chipao", "arona_a_idolfrontribbon", "arona_a_pareo", "hairpin_placeholder" },
            new[] { "치파오", "아이돌 프론트리본", "파레오", "헤어핀" },
            new[] { 300, 200, 250, 150 },
            new[] { StoreIconType.File, StoreIconType.File, StoreIconType.File, StoreIconType.File },
            equipIcons,
            new[] { "", "", "", "" });
        StoreTagCatalog poseCat = CreateTagCatalog(
            TagPoseCatalogPath,
            new[] { "pose_greeting", "pose_dance", "pose_sit" },
            new[] { "포즈: 인사", "포즈: 댄스", "포즈: 앉기" },
            new[] { 150, 300, 200 },
            new[] { StoreIconType.Runtime, StoreIconType.Runtime, StoreIconType.Runtime },
            new Sprite[] { null, null, null },
            new[] { "", "", "" });
        StoreTagCatalog fxCat = CreateTagCatalog(
            TagFxCatalogPath,
            new[] { "fx_pat_heart", "fx_pat_star", "fx_click_sparkle" },
            new[] { "쓰다듬기: 하트", "쓰다듬기: 별", "클릭: 반짝임" },
            new[] { 250, 250, 200 },
            new[] { StoreIconType.Runtime, StoreIconType.Runtime, StoreIconType.Runtime },
            new Sprite[] { null, null, null },
            new[] { "", "", "" });
        StoreTagCatalog giftCat = CreateTagCatalog(
            TagGiftCatalogPath,
            new[] { "gift_s", "gift_m", "gift_l" },
            new[] { "선물(소)", "선물(중)", "선물(대)" },
            new[] { 50, 120, 300 },
            new[] { StoreIconType.File, StoreIconType.File, StoreIconType.File },
            new Sprite[] { null, null, null },
            new[] { "친밀도 +10", "친밀도 +30", "친밀도 +100" });
        StoreTagCatalog miscCat = CreateTagCatalog(
            TagMiscCatalogPath,
            new[] { "snack_banana", "potion_energy", "ticket_random" },
            new[] { "바나나", "에너지 드링크", "랜덤 티켓" },
            new[] { 10, 30, 80 },
            new[] { StoreIconType.File, StoreIconType.File, StoreIconType.File },
            new Sprite[] { null, null, null },
            new[] { "", "", "" });

        // (c) StoreCatalog(태그 레지스트리) 갱신 — 기존 에셋을 재사용해 guid 보존(프리팹의 직렬화 참조 유지).
        //     additive: 기존 태그 행은 보존(사용자 재배열/추가/제거 존중), 기본 태그 중 누락분만 뒤에 추가하고
        //     기본 태그 행의 catalog 참조가 비어 있으면 채워만 준다.
        StoreCatalog cat = AssetDatabase.LoadAssetAtPath<StoreCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<StoreCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        string[] tagNames = { "장착물", "포즈", "이펙트", "선물", "잡화" };
        StoreTagCatalog[] tagCatalogs = { equipCat, poseCat, fxCat, giftCat, miscCat };

        SerializedObject so = new SerializedObject(cat);
        SerializedProperty list = so.FindProperty("tags");

        HashSet<string> existingTags = new HashSet<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i);
            string tagName = row.FindPropertyRelative("tag").stringValue;
            existingTags.Add(tagName);

            // 기본 태그인데 카탈로그 참조가 비어 있으면 보충 (사용자가 지정한 참조는 불변)
            SerializedProperty catalogProp = row.FindPropertyRelative("catalog");
            if (catalogProp.objectReferenceValue == null)
            {
                int defaultIndex = System.Array.IndexOf(tagNames, tagName);
                if (defaultIndex >= 0)
                {
                    catalogProp.objectReferenceValue = tagCatalogs[defaultIndex];
                }
            }
        }

        int addedTags = 0;
        for (int i = 0; i < tagNames.Length; i++)
        {
            if (existingTags.Contains(tagNames[i]))
            {
                continue;
            }

            list.arraySize = list.arraySize + 1;
            SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("tag").stringValue = tagNames[i];
            e.FindPropertyRelative("catalog").objectReferenceValue = tagCatalogs[i];
            addedTags = addedTags + 1;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(cat);

        // (d) 상점 전용 키를 InventoryCatalog에 등록 (AddToMain이 카탈로그 검증을 하므로 필수)
        RegisterStoreKeysToInventoryCatalog();

        // (e) 포즈/이펙트 프리뷰 상세 카탈로그 + NoImage 플레이스홀더 생성/갱신 (프리뷰 캡처 리그와 StoreManager가 사용)
        CreateDetailPoseCatalog();
        CreateDetailEffectCatalog();
        EnsureNoImageSprite();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Store][StoreTools] 태그 레지스트리 준비: {CatalogPath} (태그 {tagNames.Length}개).");
        return cat;
    }

    // 클래스 리네임으로 Detail 타입이 된 구 프리뷰 에셋을 삭제해 같은 경로에 태그 카탈로그를 만들 자리를 확보한다
    private static void DeleteLegacyPreviewAsset<T>(string path) where T : ScriptableObject
    {
        T legacy = AssetDatabase.LoadAssetAtPath<T>(path);
        if (legacy == null)
        {
            return;
        }

        AssetDatabase.DeleteAsset(path);
        Debug.Log($"[Store][StoreTools] 레거시 프리뷰 에셋 삭제(태그 카탈로그 경로 확보): {path}");
    }

    // 태그별 상품 카탈로그(StoreTagCatalog) Load-or-Create 후 "누락 키만" 기본 엔트리로 추가한다.
    // 기존 엔트리 필드는 사용자 소유라 절대 덮어쓰지 않되, 기본 키와 일치하는 행의 "빈 값"만 기본값으로 보충한다
    // (구 스키마(giftPoints 시절) 에셋은 iconType/icon/detailText가 File(0)/null/""로 로드되므로 이 보충이 유일한 이행 경로).
    private static StoreTagCatalog CreateTagCatalog(string path, string[] keys, string[] names, int[] prices, StoreIconType[] iconTypes, Sprite[] icons, string[] detailTexts)
    {
        StoreTagCatalog cat = AssetDatabase.LoadAssetAtPath<StoreTagCatalog>(path);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<StoreTagCatalog>();
            AssetDatabase.CreateAsset(cat, path);
        }

        SerializedObject so = new SerializedObject(cat);
        SerializedProperty list = so.FindProperty("entries");

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
                SerializedProperty iconTypeProp = row.FindPropertyRelative("iconType");
                SerializedProperty iconProp = row.FindPropertyRelative("icon");
                SerializedProperty detailTextProp = row.FindPropertyRelative("detailText");

                // 스키마 이행 규칙: File(0) + 빈 icon은 NoImage 상태라 사용자 의도로 볼 수 없다 —
                // 기본이 Runtime인 키(포즈/이펙트)는 Runtime으로 승격한다 (구 스키마 에셋을 캡처 대상으로 복귀)
                if (iconTypeProp.enumValueIndex == (int)StoreIconType.File
                    && iconProp.objectReferenceValue == null
                    && iconTypes[i] == StoreIconType.Runtime)
                {
                    iconTypeProp.enumValueIndex = (int)StoreIconType.Runtime;
                }

                // 빈 값 보충: File 모드 행의 icon이 비어 있고 기본 icon이 있으면 채움 (사용자가 지정한 icon은 불변).
                // 사용자가 Runtime으로 바꿔둔 행은 icon을 쓰지 않으므로 건드리지 않는다.
                if (iconTypeProp.enumValueIndex == (int)StoreIconType.File
                    && iconProp.objectReferenceValue == null && icons[i] != null)
                {
                    iconProp.objectReferenceValue = icons[i];
                }

                // 빈 값 보충: detailText가 비어 있고 기본이 있으면 채움
                if (string.IsNullOrEmpty(detailTextProp.stringValue) == true && string.IsNullOrEmpty(detailTexts[i]) == false)
                {
                    detailTextProp.stringValue = detailTexts[i];
                }

                continue;
            }

            // arraySize 증가는 마지막 요소를 복제하므로 모든 필드를 명시적으로 덮어쓴다
            list.arraySize = list.arraySize + 1;
            SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("key").stringValue = keys[i];
            e.FindPropertyRelative("displayName").stringValue = names[i];
            e.FindPropertyRelative("price").intValue = prices[i];
            e.FindPropertyRelative("iconType").enumValueIndex = (int)iconTypes[i];
            e.FindPropertyRelative("icon").objectReferenceValue = icons[i];
            e.FindPropertyRelative("detailText").stringValue = detailTexts[i];
            added = added + 1;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(cat);
        Debug.Log($"[Store][StoreTools] 태그 카탈로그 준비: {path} (신규 {added} / 유지 {list.arraySize - added}).");
        return cat;
    }

    // guid → 아이콘 스프라이트 로드 (텍스처의 Sprite 서브에셋 — InventorySystemTools와 같은 원본 PNG, Assets/Model/Sprite).
    // 로드 실패 시 null + Warning — 카드는 NoImage로 폴백한다
    private static Sprite LoadSpriteByGuid(string guid, string keyForLog)
    {
        string iconPath = AssetDatabase.GUIDToAssetPath(guid);
        Sprite icon = string.IsNullOrEmpty(iconPath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (icon == null)
        {
            Debug.LogWarning($"[Store][StoreTools] 아이콘 스프라이트 로드 실패: key={keyForLog} guid={guid} (NoImage 폴백)");
        }
        return icon;
    }

    // 상점 전용 키(포즈/이펙트/선물/잡화 12종)를 InventoryCatalog_Demo에 additive 등록 (이미 있으면 스킵, 코드 수정 없음)
    private static void RegisterStoreKeysToInventoryCatalog()
    {
        InventoryCatalog invCat = AssetDatabase.LoadAssetAtPath<InventoryCatalog>(InventoryCatalogPath);
        if (invCat == null)
        {
            Debug.LogError($"[Store][StoreTools] InventoryCatalog을 찾을 수 없습니다: {InventoryCatalogPath} " +
                           "(이 에셋은 커밋된 베이크 산출물입니다 — 생성 도구 Tools/InventorySystem은 삭제되었으니 리포지토리에서 복원하세요. 상점 카탈로그만 생성하고 계속합니다)");
            return;
        }

        string[] keys = {
            "pose_greeting", "pose_dance", "pose_sit",
            "fx_pat_heart", "fx_pat_star", "fx_click_sparkle",
            "gift_s", "gift_m", "gift_l",
            "snack_banana", "potion_energy", "ticket_random"
        };
        string[] names = {
            "포즈: 인사", "포즈: 댄스", "포즈: 앉기",
            "쓰다듬기: 하트", "쓰다듬기: 별", "클릭: 반짝임",
            "선물(소)", "선물(중)", "선물(대)",
            "바나나", "에너지 드링크", "랜덤 티켓"
        };

        SerializedObject so = new SerializedObject(invCat);
        SerializedProperty list = so.FindProperty("entries");
        if (list == null)
        {
            Debug.LogError("[Store][StoreTools] InventoryCatalog에 'entries' 직렬화 필드가 없어 상점 키를 등록하지 못했습니다.");
            return;
        }

        // 기존 키 수집 (additive — 기존 엔트리는 절대 건드리지 않는다)
        HashSet<string> existing = new HashSet<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty keyProp = list.GetArrayElementAtIndex(i).FindPropertyRelative("key");
            if (keyProp != null)
            {
                existing.Add(keyProp.stringValue);
            }
        }

        int added = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            if (existing.Contains(keys[i]))
            {
                continue;
            }

            list.arraySize = list.arraySize + 1;
            SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("key").stringValue = keys[i];
            e.FindPropertyRelative("displayName").stringValue = names[i];
            e.FindPropertyRelative("icon").objectReferenceValue = null;  // 아이콘 없음 → UI가 이름 텍스트로 폴백
            e.FindPropertyRelative("description").stringValue = "";
            e.FindPropertyRelative("maxStack").intValue = 99;
            e.FindPropertyRelative("category").stringValue = "store";
            added = added + 1;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(invCat);
        Debug.Log($"[Store][StoreTools] InventoryCatalog 상점 키 등록: 신규 {added}개 / 스킵 {keys.Length - added}개.");
    }

    // 포즈 프리뷰 상세 카탈로그 생성/갱신: 포즈 키 3종에 프로젝트 실물 클립을 바인딩 (클립은 읽기 전용 참조)
    private static void CreateDetailPoseCatalog()
    {
        EnsureDir(ResourcesDir);

        StoreDetailPoseCatalog poseCat = AssetDatabase.LoadAssetAtPath<StoreDetailPoseCatalog>(DetailPoseCatalogPath);
        if (poseCat == null)
        {
            poseCat = ScriptableObject.CreateInstance<StoreDetailPoseCatalog>();
            AssetDatabase.CreateAsset(poseCat, DetailPoseCatalogPath);
        }

        // pose_sit 참고: 프로젝트에 순수 '앉기' .anim이 없어 diana FBX의 휴머노이드 앉기 클립(SMPLH_Animation)을 쓴다
        // (보이지 않는 의자에 앉은 모습). FBX가 이동하면 Assets/Animation/mixamo/Float_loop/Floating.anim으로 대체 가능.
        string[] keys = { "pose_greeting", "pose_dance", "pose_sit" };
        AnimationClip[] clips = {
            LoadClipAsset(PoseGreetingClipPath),
            LoadClipAsset(PoseDanceClipPath),
            LoadSitClipFromFbx(PoseSitFbxPath)
        };

        // additive: 기존 엔트리는 보존(사용자의 clip/freeze 편집 존중), 누락 키만 추가.
        // 단 기본 키의 clip이 비어 있으면 바인딩만 보충한다 (임포트 실패 후 재실행 복구용).
        SerializedObject so = new SerializedObject(poseCat);
        SerializedProperty list = so.FindProperty("entries");

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
                SerializedProperty clipProp = row.FindPropertyRelative("clip");
                if (clipProp.objectReferenceValue == null && clips[i] != null)
                {
                    clipProp.objectReferenceValue = clips[i];
                }
                continue;
            }

            // arraySize 증가는 마지막 요소를 복제하므로 모든 필드를 명시적으로 덮어쓴다
            list.arraySize = list.arraySize + 1;
            SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("key").stringValue = keys[i];
            e.FindPropertyRelative("clip").objectReferenceValue = clips[i];  // 로드 실패 시 null — 리그가 null 가드
            e.FindPropertyRelative("freezeMin").floatValue = 0.2f;
            e.FindPropertyRelative("freezeMax").floatValue = 0.8f;
            added = added + 1;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(poseCat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Store][StoreTools] 포즈 상세 카탈로그 준비: {DetailPoseCatalogPath} (신규 {added} / 유지 {list.arraySize - added}).");
    }

    // 이펙트 프리뷰 상세 카탈로그 생성/갱신: fx 키 3종에 프로젝트 실물 파티클 프리팹을 바인딩 (프리팹은 읽기 전용 참조)
    private static void CreateDetailEffectCatalog()
    {
        EnsureDir(ResourcesDir);

        StoreDetailEffectCatalog fxCat = AssetDatabase.LoadAssetAtPath<StoreDetailEffectCatalog>(DetailEffectCatalogPath);
        if (fxCat == null)
        {
            fxCat = ScriptableObject.CreateInstance<StoreDetailEffectCatalog>();
            AssetDatabase.CreateAsset(fxCat, DetailEffectCatalogPath);
        }

        string[] keys = { "fx_pat_heart", "fx_pat_star", "fx_click_sparkle" };
        GameObject[] prefabs = {
            LoadEffectPrefab(FxLoveAuraPath),
            LoadEffectPrefab(FxPatStarPath),
            LoadEffectPrefab(FxClickSparklePath)
        };

        // additive: 기존 엔트리 보존(누락 키만 추가), 기본 키의 effectPrefab이 비어 있으면 바인딩만 보충
        SerializedObject so = new SerializedObject(fxCat);
        SerializedProperty list = so.FindProperty("entries");

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
                SerializedProperty prefabProp = row.FindPropertyRelative("effectPrefab");
                if (prefabProp.objectReferenceValue == null && prefabs[i] != null)
                {
                    prefabProp.objectReferenceValue = prefabs[i];
                }
                continue;
            }

            // arraySize 증가는 마지막 요소를 복제하므로 모든 필드를 명시적으로 덮어쓴다
            list.arraySize = list.arraySize + 1;
            SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("key").stringValue = keys[i];
            e.FindPropertyRelative("effectPrefab").objectReferenceValue = prefabs[i];  // 로드 실패 시 null — 리그가 null 가드, 카드는 NoImage 폴백
            e.FindPropertyRelative("simulateTime").floatValue = 1.5f;
            added = added + 1;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(fxCat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Store][StoreTools] 이펙트 상세 카탈로그 준비: {DetailEffectCatalogPath} (신규 {added} / 유지 {list.arraySize - added}).");
    }

    // 파티클 프리팹 로드 (실패 시 에러 로그 + null)
    private static GameObject LoadEffectPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[Store][StoreTools] 이펙트 프리팹을 찾을 수 없습니다: {path} (해당 이펙트 카드는 NoImage 폴백)");
        }
        return prefab;
    }

    // 단독 .anim 에셋 로드 (실패 시 에러 로그 + null)
    private static AnimationClip LoadClipAsset(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            Debug.LogError($"[Store][StoreTools] 애니메이션 클립을 찾을 수 없습니다: {path} (해당 포즈는 아이콘 없이 텍스트 폴백)");
        }
        return clip;
    }

    // FBX 서브에셋에서 애니메이션 클립 추출 — 두 diana FBX 모두 클립명이 'SMPLH_Animation'이라 반드시 경로로 로드한다
    private static AnimationClip LoadSitClipFromFbx(string fbxPath)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (o is AnimationClip c && c.name.StartsWith("__preview") == false)
            {
                return c;
            }
        }

        Debug.LogError($"[Store][StoreTools] FBX에서 앉기 클립을 찾을 수 없습니다: {fbxPath} (해당 포즈는 아이콘 없이 텍스트 폴백)");
        return null;
    }

    // 'NO IMAGE' 플레이스홀더 스프라이트 베이크 — 런타임이 Resources.Load<Sprite>("StoreNoImage")로 읽으므로 경로/이름 고정
    private static Sprite EnsureNoImageSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(NoImagePath);
        if (existing != null)
        {
            return existing;
        }

        EnsureDir(ResourcesDir);

        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color body = new Color(0.133f, 0.149f, 0.180f, 1f);
        Color border = new Color(0.227f, 0.247f, 0.290f, 1f);
        Color clear = new Color(0f, 0f, 0f, 0f);

        // 몸체: 전면 라운드 사각(라운드 24) + 경계 안쪽 2px 보더
        float cx = 128f, cy = 128f, half = 128f, radius = 24f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - cx) - (half - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - cy) - (half - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                bool inBody = dist <= radius;
                bool inInner = dist <= radius - 2f;  // 반경 2px 축소 판정 — 몸체이면서 여기 못 들면 보더
                tex.SetPixel(x, y, inBody ? (inInner ? body : border) : clear);
            }
        }

        DrawNoImageText(tex);

        tex.Apply();
        WriteAndImportSpritePng(tex, NoImagePath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(NoImagePath);
    }

    // 중앙 'NO IMAGE' 텍스트를 5x7 픽셀 폰트(4배 스케일)로 그린다
    private static void DrawNoImageText(Texture2D tex)
    {
        // 글리프 비트맵 (행 순서: 최상단→최하단, '1'=칠함)
        string[] n = { "10001", "11001", "10101", "10011", "10001", "10001", "10001" };
        string[] o = { "01110", "10001", "10001", "10001", "10001", "10001", "01110" };
        string[] i5 = { "11111", "00100", "00100", "00100", "00100", "00100", "11111" };
        string[] m = { "10001", "11011", "10101", "10101", "10001", "10001", "10001" };
        string[] a = { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
        string[] g = { "01110", "10001", "10000", "10111", "10001", "10001", "01110" };
        string[] e = { "11111", "10000", "10000", "11110", "10000", "10000", "11111" };
        string[] space = { "00000", "00000", "00000", "00000", "00000", "00000", "00000" };
        string[][] letters = { n, o, space, i5, m, a, g, e };  // "NO IMAGE"

        Color textColor = new Color(0.55f, 0.58f, 0.64f, 1f);
        const int scale = 4;
        const int cell = 6;      // 글자 5셀 + 간격 1셀
        const int startX = 34;   // (256 - 47셀*4px) / 2
        const int startY = 114;  // (256 - 7행*4px) / 2

        for (int gi = 0; gi < letters.Length; gi++)
        {
            string[] rows = letters[gi];
            int baseX = startX + gi * cell * scale;
            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    if (rows[r][c] != '1')
                    {
                        continue;
                    }

                    // Texture2D의 y는 아래→위 — 위 행부터 정의된 글리프 행을 뒤집어 그린다
                    int px = baseX + c * scale;
                    int py = startY + (6 - r) * scale;
                    for (int oy = 0; oy < scale; oy++)
                    {
                        for (int ox = 0; ox < scale; ox++)
                        {
                            tex.SetPixel(px + ox, py + oy, textColor);
                        }
                    }
                }
            }
        }
    }

    // 주사위 모양 리롤 아이콘(랜덤 은유) 베이크 — 페이지바 포즈 리롤 버튼용
    private static Sprite EnsureRerollIcon()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(RerollIconPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureDir(SpritesDir);

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color body = new Color(0.85f, 0.87f, 0.90f, 1f);
        Color pip = new Color(0.137f, 0.157f, 0.196f, 1f);
        Color clear = new Color(0f, 0f, 0f, 0f);

        // 몸체: 라운드 사각(half 26 / 라운드 10) + 주사위 5눈(반지름 5 원)
        float cx = 32f, cy = 32f, half = 26f, radius = 10f;
        Vector2[] pips = {
            new Vector2(18f, 18f), new Vector2(46f, 18f), new Vector2(32f, 32f),
            new Vector2(18f, 46f), new Vector2(46f, 46f)
        };
        const float pipRadius = 5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - cx) - (half - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - cy) - (half - radius), 0f);
                if (dx * dx + dy * dy > radius * radius)
                {
                    tex.SetPixel(x, y, clear);
                    continue;
                }

                bool inPip = false;
                for (int p = 0; p < pips.Length; p++)
                {
                    float px = x + 0.5f - pips[p].x;
                    float py = y + 0.5f - pips[p].y;
                    if (px * px + py * py <= pipRadius * pipRadius)
                    {
                        inPip = true;
                        break;
                    }
                }
                tex.SetPixel(x, y, inPip ? pip : body);
            }
        }

        tex.Apply();
        WriteAndImportSpritePng(tex, RerollIconPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(RerollIconPath);
    }

    // 텍스처를 PNG로 저장하고 Single 스프라이트로 임포트 (Jukebox EnsureYoutubeIcon 레시피 — 텍스처는 여기서 파괴)
    private static void WriteAndImportSpritePng(Texture2D tex, string assetPath)
    {
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    // ── 2) UI 프리팹 베이크: 확인 팝업 프리팹을 먼저 굽고 상점 패널에 주입 (Jukebox 의존 프리팹 패턴) ──
    [MenuItem("Tools/Store/2. Build UI Prefab")]
    public static void BuildUiPrefab()
    {
        EnsureDir(PrefabsDir);

        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogWarning($"[Store][StoreTools] SUIT-Bold 폰트를 찾을 수 없습니다: {FontPath} (기본 폰트로 빌드, '3. Apply SUIT-Bold Font'로 보정 가능)");
        }

        // (a) 구매 확인 팝업 프리팹
        GameObject confirmGo = new GameObject("StoreConfirm", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            confirmGo.layer = 5;
            StoreConfirmView confirmView = confirmGo.AddComponent<StoreConfirmView>();
            confirmView.EditorBuild(rounded, font);
            PrefabUtility.SaveAsPrefabAsset(confirmGo, ConfirmPrefabPath);
            Debug.Log($"[Store][StoreTools] 확인 팝업 프리팹 저장: {ConfirmPrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(confirmGo);
        }

        // (b) 상점 패널 프리팹 — 저장된 팝업 '에셋'을 다시 로드해 주입해야 참조가 올바르게 직렬화된다
        GameObject confirmAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmPrefabPath);

        // 카탈로그(태그 레지스트리) 선행 보장 + 참조 베이크 — 런타임 Resources 폴백을 Awake 1회로 최소화
        StoreCatalog registry = AssetDatabase.LoadAssetAtPath<StoreCatalog>(CatalogPath);
        if (registry == null || registry.Tabs().Count == 0)
        {
            registry = CreateCatalog();
        }

        GameObject go = new GameObject("StorePanel", typeof(RectTransform), typeof(CanvasGroup));
        try
        {
            go.layer = 5;
            StoreView view = go.AddComponent<StoreView>();
            view.EditorSetConfirmPrefab(confirmAsset);
            view.EditorSetRerollSprite(EnsureRerollIcon());
            view.EditorSetCatalog(registry);
            view.EditorBuild(rounded, font);

            // 확인 팝업을 패널 자식으로 함께 베이크 — 런타임은 Instantiate 없이 BindExisting이 바로 연결한다
            if (confirmAsset != null)
            {
                GameObject confirmChild = (GameObject)PrefabUtility.InstantiatePrefab(confirmAsset, go.transform);
                confirmChild.name = "StoreConfirm";
            }

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Debug.Log($"[Store][StoreTools] UI 프리팹 저장: {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ── 3) 두 프리팹의 모든 TMP_Text 폰트를 SUIT-Bold로 교체 (베이크 후 필수 마지막 단계) ──
    [MenuItem("Tools/Store/3. Apply SUIT-Bold Font")]
    public static void ApplyFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[Store][StoreTools] SUIT-Bold 폰트를 찾을 수 없습니다: {FontPath}");
            return;
        }

        ApplyFontToPrefab(font, ConfirmPrefabPath);
        ApplyFontToPrefab(font, PrefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ApplyFontToPrefab(TMP_FontAsset font, string prefabPath)
    {
        // LoadPrefabContents는 프리팹 부재 시 예외를 던지므로 존재 여부를 선행 확인한다
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogError($"[Store][StoreTools] 프리팹을 찾을 수 없습니다: {prefabPath} (먼저 '2. Build UI Prefab'을 실행하세요)");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        int changed = 0;
        try
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                text.font = font;
                EditorUtility.SetDirty(text);
                changed = changed + 1;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[Store][StoreTools] SUIT-Bold 적용 완료: {prefabPath}, TMP_Text {changed}개");
    }

    // ── 4) 데모씬 빌드 (카메라 + 매니저 + Canvas + 인벤토리/상점 패널 + 데모 컨트롤러 + 안내) ──
    [MenuItem("Tools/Store/4. Build Demo Scene")]
    public static void BuildDemoScene()
    {
        // 카탈로그/프리팹 선행 보장. 존재만 검사하면 안 된다 — 재편 이전 구 스키마 에셋은
        // guid가 보존돼 신형 StoreCatalog로 로드되지만 tags가 비어 있어 상점이 조용히 텅 빈다.
        StoreCatalog cat = AssetDatabase.LoadAssetAtPath<StoreCatalog>(CatalogPath);
        if (cat == null || cat.Tabs().Count == 0)
        {
            cat = CreateCatalog();
        }

        GameObject storePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (storePrefab == null)
        {
            BuildUiPrefab();
            ApplyFont();  // 단독 실행 경로에서도 한글이 SUIT-Bold로 보이게 보장
            storePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        EnsureDir(DemoDir);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라 (UI 전용 씬 — 어두운 단색 배경)
        GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        Camera cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        int portraitLayer = LayerMask.NameToLayer(PortraitLayerName);  // 리그(StorePosePreviewRig)와 같은 이름 해석 — 단일 출처
        if (portraitLayer >= 0)
        {
            cam.cullingMask = cam.cullingMask & ~(1 << portraitLayer);  // 포즈 프리뷰 리그 레이어 제외
        }
        else
        {
            Debug.LogWarning($"[Store][StoreTools] '{PortraitLayerName}' 레이어가 없어 메인 카메라 마스크를 조정하지 않습니다 (리그가 화면에 비칠 수 있음).");
        }
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        // EventSystem (드래그 시작 판정 거리 축소 — 판매 드래그 반응 개선)
        GameObject esGo = new GameObject("EventSystem");
        EventSystem es = esGo.AddComponent<EventSystem>();
        es.pixelDragThreshold = 5;
        esGo.AddComponent<StandaloneInputModule>();

        // 매니저 (Awake에서 Resources 카탈로그 자동 로드. 캐릭터는 배치하지 않음)
        GameObject invMgrGo = new GameObject("InventorySystemManager");
        invMgrGo.AddComponent<InventorySystemManager>();

        GameObject equipMgrGo = new GameObject("EquipManager");
        equipMgrGo.AddComponent<EquipManager>();

        // 상시 상점 서비스(프리뷰 캐시/리롤/NoImage). 없으면 런타임 자동 생성되지만 씬에 명시 배치
        GameObject storeMgrGo = new GameObject("StoreManager");
        storeMgrGo.AddComponent<StoreManager>();

        // 캔버스 (InventoryPanel과 크기 호환 세팅)
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f, 1440f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 인벤토리 패널 (MAIN 섹션, 우측). 없으면 안내만 하고 나머지는 계속 빌드 (약결합 — 직접 빌드하지 않음)
        InventoryView inventoryView = null;
        GameObject invPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPanelPath);
        if (invPanelPrefab != null)
        {
            GameObject invInst = (GameObject)PrefabUtility.InstantiatePrefab(invPanelPrefab, canvasGo.transform);
            invInst.name = "InventoryPanel_Main";
            inventoryView = invInst.GetComponentInChildren<InventoryView>(true);
            if (inventoryView != null)
            {
                inventoryView.ConfigureSection(InventorySection.Main);
            }

            RectTransform invRt = (RectTransform)invInst.transform;
            invRt.anchorMin = new Vector2(1f, 0.5f);
            invRt.anchorMax = new Vector2(1f, 0.5f);
            invRt.pivot = new Vector2(1f, 0.5f);
            invRt.anchoredPosition = new Vector2(-60f, 0f);
        }
        else
        {
            Debug.LogError($"[Store][StoreTools] 인벤토리 패널 프리팹 없음: {InventoryPanelPath} " +
                           "(InventoryPanel.prefab은 커밋된 베이크 산출물입니다 — 생성 도구 Tools/InventorySystem은 삭제되었으니 리포지토리에서 복원하세요. 인벤토리 패널 없이 씬을 계속 빌드합니다)");
        }

        // 상점 패널 (좌측, Show 상태 — S로 토글)
        StoreView storeView = null;
        if (storePrefab != null)
        {
            GameObject storeInst = (GameObject)PrefabUtility.InstantiatePrefab(storePrefab, canvasGo.transform);
            storeView = storeInst.GetComponentInChildren<StoreView>(true);

            RectTransform storeRt = (RectTransform)storeInst.transform;
            storeRt.anchorMin = new Vector2(0f, 0.5f);
            storeRt.anchorMax = new Vector2(0f, 0.5f);
            storeRt.pivot = new Vector2(0f, 0.5f);
            storeRt.anchoredPosition = new Vector2(60f, 0f);
        }
        else
        {
            Debug.LogError($"[Store][StoreTools] 상점 패널 프리팹 없음: {PrefabPath}");
        }

        // 포즈/이펙트 프리뷰 리그 (오프스크린 + PortraitModel 레이어 격리 — 카드 아이콘 캡처용).
        // 리그는 참조가 없으면 무동작이므로 프리팹/카탈로그가 없어도 씬 빌드는 계속된다.
        // 상세 카탈로그는 리그에 직접 넘기지 않는다 — StoreManager가 Resources에서 로드하므로 존재만 보장.
        if (AssetDatabase.LoadAssetAtPath<StoreDetailPoseCatalog>(DetailPoseCatalogPath) == null)
        {
            CreateDetailPoseCatalog();
        }
        if (AssetDatabase.LoadAssetAtPath<StoreDetailEffectCatalog>(DetailEffectCatalogPath) == null)
        {
            CreateDetailEffectCatalog();
        }

        GameObject pocPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AronaPocPath);
        if (pocPrefab == null)
        {
            Debug.LogError($"[Store][StoreTools] arona POC 프리팹 없음: {AronaPocPath} (포즈 카드 아이콘은 텍스트 폴백으로 표시. 씬 빌드는 계속합니다)");
        }

        GameObject rigGo = new GameObject("StorePosePreviewRig");
        rigGo.transform.position = new Vector3(0f, -1000f, 0f);
        StorePosePreviewRig rig = rigGo.AddComponent<StorePosePreviewRig>();
        rig.EditorSet(pocPrefab);

        // 데모 컨트롤러 (참조는 에디터 전용 세터, grants는 SerializedObject로 직렬화 기록)
        GameObject demoGo = new GameObject("StoreDemoController");
        StoreDemoController demo = demoGo.AddComponent<StoreDemoController>();
        demo.EditorSet(storeView, inventoryView);
        WriteDemoGrants(demo);

        // 안내 UI (legacy Text — SUIT-Bold 교체 대상 아님, GraphicRaycaster 없음)
        BuildInfoCanvas();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        Debug.Log($"[Store][StoreTools] 데모씬 저장: {DemoScenePath}. Play → S 상점 토글, I 인벤토리 토글, G +500G, 1~4 지급, 5 포즈 리롤, " +
                  "카드 클릭 → 수량 선택 → 계산하기, 슬롯→판매존 드래그 판매.");
    }

    // StoreDemoController.grants(직렬화 리스트)에 데모 지급 바인딩 기록 (필드가 private이라 SerializedObject 사용)
    private static void WriteDemoGrants(StoreDemoController demo)
    {
        KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };
        string[] itemKeys = { "arona_a_chipao", "gift_s", "pose_dance", "snack_banana" };

        SerializedObject so = new SerializedObject(demo);
        SerializedProperty list = so.FindProperty("grants");
        if (list == null)
        {
            Debug.LogWarning("[Store][StoreTools] StoreDemoController에 'grants' 직렬화 필드가 없어 데모 지급 바인딩을 기록하지 못했습니다.");
            return;
        }

        list.arraySize = keys.Length;
        for (int i = 0; i < keys.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            SerializedProperty keyProp = e.FindPropertyRelative("key");
            SerializedProperty itemProp = e.FindPropertyRelative("itemKey");
            if (keyProp == null || itemProp == null)
            {
                Debug.LogWarning("[Store][StoreTools] grants 엔트리에 key/itemKey 필드가 없어 데모 지급 바인딩을 기록하지 못했습니다.");
                return;
            }

            keyProp.intValue = (int)keys[i];
            itemProp.stringValue = itemKeys[i];
        }
        so.ApplyModifiedProperties();
    }

    // 안내 텍스트용 오버레이 캔버스 (legacy UI Text — 클릭을 받을 필요가 없어 GraphicRaycaster를 붙이지 않는다)
    private static void BuildInfoCanvas()
    {
        GameObject canvasGo = new GameObject("InfoCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler infoScaler = canvasGo.AddComponent<CanvasScaler>();
        infoScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        infoScaler.referenceResolution = new Vector2(2560f, 1440f);
        infoScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        infoScaler.matchWidthOrHeight = 0.5f;

        GameObject textGo = new GameObject("InfoText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.raycastTarget = false;  // 상점/인벤토리 클릭을 가로채지 않도록
        text.font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");
        text.text = "Store Demo\nS: 상점 / I: 인벤토리 / G: +500G / 1~4: 아이템 지급 / 5: 포즈 리롤\n" +
                    "구매: 카드 클릭 → 수량 선택 → 계산하기 / 판매: 슬롯→판매존 드래그";
        text.fontSize = 22;
        text.color = Color.white;
        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta = new Vector2(900f, 120f);
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
