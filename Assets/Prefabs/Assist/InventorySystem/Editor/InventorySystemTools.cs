using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// InventorySystem(완전 독립) 셋업 도구: 카탈로그 생성, UI 프리팹 베이크, SUIT-Bold 폰트 적용, 데모씬 빌드.
// EquipSystemTools를 호출하지 않고 필요한 헬퍼(FrameCamera/StripAppComponents/MeasureCharHeight/FindByName)를 자체 보유한다.
public static class InventorySystemTools
{
    private const string Root = "Assets/Prefabs/Assist/InventorySystem";
    private const string ResourcesDir = Root + "/Resources";
    private const string CatalogPath = ResourcesDir + "/InventoryCatalog_Demo.asset";
    private const string PrefabPath = Root + "/InventoryPanel.prefab";
    private const string DemoScenePath = Root + "/InventoryDemo.unity";
    private const string FontPath = "Assets/FontAssets/SUIT-Bold.asset";
    private const string AronaPocPath = "Assets/Prefabs/Char_toon/arona_6_clean_POC.prefab";

    // 전체 셋업 (카탈로그 + UI 프리팹 + 폰트 + 데모씬)
    [MenuItem("Tools/InventorySystem/Setup All (catalog + UI prefab + font + demo scene)")]
    public static void SetupAll()
    {
        CreateCatalog();
        BuildUiPrefab();
        ApplyFont();
        BuildDemoScene();
        Debug.Log("[InventorySystem] Setup All 완료.");
    }

    // batchmode -executeMethod 진입점 (다이얼로그 절대 금지)
    public static void BatchBuildAll()
    {
        CreateCatalog();
        BuildUiPrefab();
        ApplyFont();
        BuildDemoScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[InventorySystem] BatchBuildAll 완료.");
    }

    // ── 1) InventoryCatalog 생성/갱신 (데모 아이템 4종) ──
    [MenuItem("Tools/InventorySystem/1. Create Catalog")]
    public static InventoryCatalog CreateCatalog()
    {
        // Resources 폴더 보장 (런타임 자동 로드용)
        if (AssetDatabase.IsValidFolder(ResourcesDir) == false)
        {
            AssetDatabase.CreateFolder(Root, "Resources");
        }

        InventoryCatalog cat = AssetDatabase.LoadAssetAtPath<InventoryCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<InventoryCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        // EquipCatalog와 동일한 key 문자열 공간을 사용한다
        string[] keys = { "arona_a_chipao", "arona_a_idolfrontribbon", "arona_a_pareo", "hairpin_placeholder" };
        string[] names = { "치파오", "아이돌 프론트리본", "파레오", "헤어핀" };
        string[] descs = {
            "머리에 장착하는 치파오 스타일 악세서리.",
            "아이돌 무대용 프론트 리본 악세서리.",
            "머리에 장착하는 파레오 스타일 악세서리.",
            "머리핀 슬롯에 장착하는 헤어핀."
        };
        // 아이콘 스프라이트 (구 Vault AccessoryItem이 쓰던 것과 동일. Assets/Model/Sprite/ — Vault와 무관한 독립 자산)
        string[] iconGuids = {
            "8aa77dfd81aed7a42ad1413b98563049",  // arona_a_chipao.png
            "55381bb255052cf4e93142224e9246c4",  // arona_a_idolfrontribbon.png
            "f77a588aa9c001a498023ffc85b4b4be",  // arona_a_pareo.png
            "e493f40f0fbd4644a93445e5eded5528"   // hairpin_placeholder.png
        };

        SerializedObject so = new SerializedObject(cat);
        SerializedProperty list = so.FindProperty("entries");
        list.arraySize = keys.Length;

        for (int i = 0; i < keys.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("key").stringValue = keys[i];
            e.FindPropertyRelative("displayName").stringValue = names[i];
            e.FindPropertyRelative("description").stringValue = descs[i];
            e.FindPropertyRelative("maxStack").intValue = 99;
            e.FindPropertyRelative("category").stringValue = "accessory";

            // guid → 아이콘 스프라이트 로드 (텍스처의 Sprite 서브에셋)
            string iconPath = AssetDatabase.GUIDToAssetPath(iconGuids[i]);
            Sprite icon = string.IsNullOrEmpty(iconPath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            e.FindPropertyRelative("icon").objectReferenceValue = icon;
            if (icon == null)
            {
                Debug.LogWarning($"[InventorySystem] 아이콘 로드 실패: key={keys[i]} guid={iconGuids[i]}");
            }
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[InventorySystem] 카탈로그 준비: {CatalogPath} (엔트리 {keys.Length}개).");
        return cat;
    }

    // ── 2) UI 프리팹 베이크 (InventoryView.BuildHierarchy로 코드 생성 → 정적 프리팹 저장) ──
    [MenuItem("Tools/InventorySystem/2. Build UI Prefab")]
    public static void BuildUiPrefab()
    {
        // 임시 부모 아래에 전체 계층을 생성한다 (참조는 같은 계층 내부라 프리팹에 정상 직렬화됨)
        GameObject tempParent = new GameObject("InventoryPanel_BakeTemp");
        try
        {
            InventoryView view = InventoryView.BuildHierarchy(tempParent.transform);
            if (view == null)
            {
                Debug.LogError("[InventorySystem] BuildHierarchy가 null을 반환했습니다. 프리팹 베이크 중단.");
                return;
            }

            GameObject panelGo = view.gameObject;
            PrefabUtility.SaveAsPrefabAsset(panelGo, PrefabPath);
            Debug.Log($"[InventorySystem] UI 프리팹 저장: {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(tempParent);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ── 3) 프리팹의 모든 TMP_Text 폰트를 SUIT-Bold로 교체 (베이크 후 필수 마지막 단계) ──
    [MenuItem("Tools/InventorySystem/3. Apply SUIT-Bold Font")]
    public static void ApplyFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError($"[InventorySystem] SUIT-Bold 폰트를 찾을 수 없습니다: {FontPath}");
            return;
        }

        // LoadPrefabContents는 프리팹 부재 시 null이 아니라 예외를 던지므로 존재 여부를 선행 확인한다
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError($"[InventorySystem] 프리팹을 찾을 수 없습니다: {PrefabPath} (먼저 '2. Build UI Prefab'을 실행하세요)");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

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
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[InventorySystem] SUIT-Bold 적용 완료: {PrefabPath}, TMP_Text {changed}개");
    }

    // ── 4) 데모씬 빌드 (카메라 + 라이트 + arona POC + EquipManager + InventorySystemManager + UI + 데모 컨트롤러) ──
    [MenuItem("Tools/InventorySystem/4. Build Demo Scene")]
    public static void BuildDemoScene()
    {
        // 카탈로그/프리팹 선행 보장
        InventoryCatalog cat = AssetDatabase.LoadAssetAtPath<InventoryCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = CreateCatalog();
        }

        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (panelPrefab == null)
        {
            BuildUiPrefab();
            ApplyFont();  // 폰트까지 적용해야 한글(displayName 등)이 정상 표시된다 (단독 실행 경로에서도 SUIT-Bold 보장)
            panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        camGo.transform.position = new Vector3(0f, 1.35f, -1.6f);
        camGo.transform.rotation = Quaternion.Euler(6f, 0f, 0f);

        // 라이트
        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 캐릭터 (arona POC): 월드 원점 배치
        GameObject aronaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AronaPocPath);
        GameObject charInst = null;
        if (aronaPrefab != null)
        {
            charInst = (GameObject)PrefabUtility.InstantiatePrefab(aronaPrefab);

            // 프리팹 완전 언팩 (컴포넌트 자유 제거 위해). EquipSocket/캡슐 등은 유지됨.
            PrefabUtility.UnpackPrefabInstance(charInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            charInst.transform.position = Vector3.zero;
            charInst.transform.rotation = Quaternion.identity;

            // 앱 종속 컴포넌트 제거 (매니저 없이 NPE 방지). EquipSocket/EquipMarker만 남김.
            StripAppComponents(charInst);

            // 캐릭터 전체가 보이도록 카메라 자동 프레이밍
            FrameCamera(cam, charInst);
        }
        else
        {
            Debug.LogError($"[InventorySystem] arona POC 프리팹 없음: {AronaPocPath}");
        }

        // EquipManager (catalog는 Awake의 Resources 폴백으로 로드됨)
        GameObject equipMgrGo = new GameObject("EquipManager");
        equipMgrGo.AddComponent<EquipManager>();

        // InventorySystemManager (catalog/equipCatalog는 Awake의 Resources 폴백으로 로드됨)
        GameObject invMgrGo = new GameObject("InventorySystemManager");
        invMgrGo.AddComponent<InventorySystemManager>();

        // EventSystem (activeInputHandler=Both 이므로 StandaloneInputModule 사용 가능)
        GameObject esGo = new GameObject("EventSystem");
        EventSystem es = esGo.AddComponent<EventSystem>();
        es.pixelDragThreshold = 5;  // 드래그 시작 판정 거리 축소 (기본 10 → 픽업 반응 개선)
        esGo.AddComponent<StandaloneInputModule>();

        // 캔버스 + 인벤토리 패널 (프리팹 인스턴스로 배치)
        // CanvasScaler는 SampleScene의 메인 캔버스(Assets/Migration/Root260616.prefab)와 동일 세팅 —
        // 나중에 본편으로 이식할 때 크기 느낌이 그대로 유지되도록 맞춘다.
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f, 1440f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // MAIN 창(왼쪽) + CHAR 창(오른쪽) — 같은 프리팹을 두 번 인스턴스화하고 섹션만 다르게 지정
        InventoryView mainView = null;
        InventoryView charView = null;
        if (panelPrefab != null)
        {
            GameObject mainInst = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, canvasGo.transform);
            mainInst.name = "InventoryPanel_Main";
            mainView = mainInst.GetComponentInChildren<InventoryView>(true);
            mainView.ConfigureSection(InventorySection.Main);
            RectTransform mainRt = (RectTransform)mainInst.transform;
            mainRt.anchorMin = new Vector2(0f, 0.5f);
            mainRt.anchorMax = new Vector2(0f, 0.5f);
            mainRt.pivot = new Vector2(0f, 0.5f);
            mainRt.anchoredPosition = new Vector2(32f, 0f);
            mainRt.sizeDelta = new Vector2(600f, 512f);

            GameObject charInstUi = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, canvasGo.transform);
            charInstUi.name = "InventoryPanel_Char";
            charView = charInstUi.GetComponentInChildren<InventoryView>(true);
            charView.ConfigureSection(InventorySection.Char);
            RectTransform charRt = (RectTransform)charInstUi.transform;
            charRt.anchorMin = new Vector2(1f, 0.5f);
            charRt.anchorMax = new Vector2(1f, 0.5f);
            charRt.pivot = new Vector2(1f, 0.5f);
            charRt.anchoredPosition = new Vector2(-32f, 0f);
            charRt.sizeDelta = new Vector2(600f, 512f);
        }
        else
        {
            Debug.LogError($"[InventorySystem] 인벤토리 패널 프리팹 없음: {PrefabPath}");
        }

        // 데모 컨트롤러 (target/charcode/mainView/charView/grants는 public 필드라 직접 대입으로 정상 저장됨)
        GameObject demoGo = new GameObject("InventoryDemoController");
        InventoryDemoController demo = demoGo.AddComponent<InventoryDemoController>();
        demo.target = charInst;
        demo.charcode = "arona_poc";
        demo.mainView = mainView;
        demo.charView = charView;
        demo.toggleKey = KeyCode.I;
        demo.grants = new List<InventoryGrantBinding>
        {
            new InventoryGrantBinding { key = KeyCode.Alpha1, itemKey = "arona_a_chipao" },
            new InventoryGrantBinding { key = KeyCode.Alpha2, itemKey = "arona_a_idolfrontribbon" },
            new InventoryGrantBinding { key = KeyCode.Alpha3, itemKey = "arona_a_pareo" },
            new InventoryGrantBinding { key = KeyCode.Alpha4, itemKey = "hairpin_placeholder" }
        };

        // 안내 UI (별도 오버레이 캔버스, legacy Text — 폰트 교체 대상 아님)
        BuildInfoCanvas();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        Debug.Log($"[InventorySystem] 데모씬 저장: {DemoScenePath}. Play → 1~4 지급, I 토글, MAIN 클릭 이동 / CHAR 좌클릭 장착·해제 / CHAR 우클릭 반환.");
    }

    // ── 헬퍼 (EquipSystemTools에서 자체 복제 — 자기완결) ──

    // 캐릭터 렌더러 바운드를 계산해 카메라가 전체를 정면(-Z)에서 프레이밍하도록 배치
    private static void FrameCamera(Camera cam, GameObject charInst)
    {
        Renderer[] rs = charInst.GetComponentsInChildren<Renderer>();
        if (rs == null || rs.Length == 0)
        {
            cam.transform.position = new Vector3(0f, 1f, -3f);
            cam.transform.rotation = Quaternion.identity;
            return;
        }

        // 전체 바운드 합치기
        bool has = false;
        Bounds b = new Bounds();
        foreach (Renderer r in rs)
        {
            if (r == null)
            {
                continue;
            }

            if (has == false)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        // 바운딩 스피어가 화면에 들어오는 거리 계산 (+여백)
        float radius = b.extents.magnitude;
        float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float dist = radius / Mathf.Sin(halfFovRad);
        dist = dist * 1.15f;

        // 정면(-Z)에서 바라봄
        cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - dist);
        cam.transform.rotation = Quaternion.LookRotation(b.center - cam.transform.position, Vector3.up);
        cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
        cam.farClipPlane = dist + radius * 4f;
    }

    // 캐릭터 인스턴스의 앱 종속 스크립트를 모두 제거 (EquipSocket/EquipMarker만 보존). RequireComponent 대비 다중 패스.
    private static void StripAppComponents(GameObject charInst)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            MonoBehaviour[] comps = charInst.GetComponentsInChildren<MonoBehaviour>(true);
            int removed = 0;

            foreach (MonoBehaviour comp in comps)
            {
                if (comp == null)
                {
                    continue;
                }

                if (comp is EquipSocket)
                {
                    continue;
                }

                if (comp is EquipMarker)
                {
                    continue;
                }

                // 앱 스크립트 제거 (RequireComponent 의존이면 이번 패스에서 실패할 수 있어 다음 패스에서 재시도)
                try
                {
                    Object.DestroyImmediate(comp, true);
                    removed = removed + 1;
                }
                catch (System.Exception)
                {
                    // 다음 패스에서 재시도
                }
            }

            if (removed == 0)
            {
                break;
            }
        }
    }

    // 안내 텍스트용 오버레이 캔버스 (legacy UI Text — SUIT-Bold 교체 대상에서 제외하기 위함)
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
        // 안내 전용 캔버스 — 클릭을 받을 필요가 없으므로 GraphicRaycaster를 붙이지 않는다

        GameObject textGo = new GameObject("InfoText");
        textGo.transform.SetParent(canvasGo.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.raycastTarget = false;  // 인벤토리 패널(슬롯) 클릭을 가로채지 않도록
        text.font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");
        text.text = "InventorySystem Demo\n1~4: MAIN에 아이템 지급 / I: 창 토글\n드래그: 칸 위치 이동·스왑, 반대 창 = 이동, 캐릭터에 드롭 = 이동+장착\n좌클릭: MAIN=이동, CHAR=장착·해제 / 우클릭: 상세·장착 메뉴\n헤더: [정렬] 종류→이름 / [X] 닫기 / 푸터: < > 페이지";
        text.fontSize = 22;
        text.color = Color.white;
        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta = new Vector2(700f, 140f);
    }

    // 캐릭터 전체 렌더러 바운드의 높이(월드 Y) 추정
    private static float MeasureCharHeight(GameObject root)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>();
        if (rs == null || rs.Length == 0)
        {
            return 0f;
        }

        bool has = false;
        Bounds b = new Bounds();
        foreach (Renderer r in rs)
        {
            if (r == null)
            {
                continue;
            }

            if (has == false)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        if (has == false)
        {
            return 0f;
        }

        return b.size.y;
    }

    // 이름으로 자식 Transform 깊이 탐색
    public static Transform FindByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
        {
            return null;
        }
        if (root.name == name)
        {
            return root;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindByName(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
