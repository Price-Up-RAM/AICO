using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// EquipSystem(완전 독립) 셋업 도구: POC 프리팹에 EquipSocket 부착, 카탈로그 생성, 데모씬 빌드.
public static class EquipSystemTools
{
    private const string AronaPocPath = "Assets/Prefabs/Char_toon/arona_6_clean_POC.prefab";
    private const string MariPocPath = "Assets/Prefabs/Char_toon/Mari_Original_Mesh_POC.prefab";
    private const string HairpinPrefabPath = "Assets/Model/Prefab/hairpin_placeholder.prefab";
    private const string ResourcesDir = "Assets/Prefabs/Assist/EquipSystem/Resources";
    private const string CatalogPath = "Assets/Prefabs/Assist/EquipSystem/Resources/EquipCatalog_Demo.asset";
    private const string DemoScenePath = "Assets/Prefabs/Assist/EquipSystem/EquipDemo.unity";

    // 전체 셋업 (소켓 + 카탈로그 + 데모씬)
    [MenuItem("Tools/EquipSystem/Setup All (sockets + catalog + demo scene)")]
    public static void SetupAll()
    {
        SetupPocSockets();
        CreateCatalog();
        BuildDemoScene();
        Debug.Log("[EquipSystem] Setup All 완료.");
    }

    // POC 프리팹에서 옛 Accessory 시스템 컴포넌트 제거 (discard로 스크립트 삭제 시 missing script 방지)
    // 타입명 문자열로 제거하므로 Accessory 스크립트에 컴파일 의존하지 않는다.
    [MenuItem("Tools/EquipSystem/Cleanup Legacy Accessory Components On POC")]
    public static void CleanupLegacyOnPoc()
    {
        StripByTypeName(AronaPocPath);
        StripByTypeName(MariPocPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // prefabPath에서 지정 타입명 컴포넌트를 제거
    private static void StripByTypeName(string prefabPath)
    {
        string[] names = { "AccessorySocket", "EquippedAccessoryMarker" };
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Component[] comps = root.GetComponentsInChildren<Component>(true);
            int removed = 0;

            foreach (Component c in comps)
            {
                if (c == null)
                {
                    continue;
                }

                string tn = c.GetType().Name;
                bool match = false;
                foreach (string n in names)
                {
                    if (tn == n)
                    {
                        match = true;
                        break;
                    }
                }

                if (match)
                {
                    Object.DestroyImmediate(c, true);
                    removed = removed + 1;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[EquipSystem] '{prefabPath}' 레거시 컴포넌트 {removed}개 제거.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 선택한 본(GameObject)에 EquipSocket + CapsuleCollider 원클릭 추가 (캡슐은 캐릭터 크기 비례)
    [MenuItem("Tools/EquipSystem/Add EquipSocket To Selection")]
    public static void AddSocketToSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("EquipSystem", "Hierarchy/프리팹에서 소켓을 붙일 본(GameObject)을 선택하세요.", "확인");
            return;
        }

        // 콜라이더 보장
        CapsuleCollider cap = go.GetComponent<CapsuleCollider>();
        if (cap == null)
        {
            cap = Undo.AddComponent<CapsuleCollider>(go);
        }
        cap.isTrigger = true;
        cap.direction = 1;

        // 캐릭터 높이 비례 캡슐 (선택 GO의 최상위 root 기준)
        float charHeight = MeasureCharHeight(go.transform.root.gameObject);
        Vector3 ls = go.transform.lossyScale;
        float lossyAvg = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
        float capLocal = 0.1f;
        if (charHeight > 0.0001f && lossyAvg > 1e-6f)
        {
            capLocal = (charHeight * 0.05f) / lossyAvg;
        }
        cap.height = capLocal;
        cap.radius = capLocal * 0.33f;

        // 소켓 부착
        EquipSocket es = go.GetComponent<EquipSocket>();
        if (es == null)
        {
            es = Undo.AddComponent<EquipSocket>(go);
        }
        if (string.IsNullOrEmpty(es.slotId))
        {
            es.slotId = "new_slot";
        }

        EditorUtility.SetDirty(go);
        Debug.Log($"[EquipSystem] '{go.name}'에 EquipSocket 추가 (slotId='{es.slotId}', capLocal={capLocal:F4}). Inspector에서 라이브 미리보기로 맞추세요.");
    }

    // ── 1) POC 프리팹의 소켓 GO에 EquipSocket 부착 ──
    // arona(스케일1): hairpin(Slot_HairPin_R) + head1(Slot_Head_1) 두 슬롯. Mari: hairpin.
    // heightFraction: 캐릭터 전체 높이 대비 악세서리 크기 비율 (스케일 제각각 대응). hairpin 작게, head1 크게.
    [MenuItem("Tools/EquipSystem/1. Setup POC Sockets")]
    public static void SetupPocSockets()
    {
        AddEquipSocket(AronaPocPath, "Slot_HairPin_R", "hairpin", 0.03f);
        AddEquipSocket(AronaPocPath, "Slot_Head_1", "head1", 0.09f);
        AddEquipSocket(MariPocPath, "Socket_hairpin", "hairpin", 0.03f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // prefabPath를 열어 socketGoName을 찾아 EquipSocket + CapsuleCollider 보장.
    // 캡슐 크기 = (캐릭터 높이 × heightFraction) 을 소켓 로컬 단위로 환산 (소켓 lossyScale로 나눔).
    private static void AddEquipSocket(string prefabPath, string socketGoName, string slotId, float heightFraction)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform socket = FindByName(root.transform, socketGoName);
            if (socket == null)
            {
                Debug.LogError($"[EquipSystem] 소켓 GO '{socketGoName}' 없음: {prefabPath}");
                return;
            }

            // 캐릭터 전체 렌더러 바운드로 높이(월드) 추정
            float charHeight = MeasureCharHeight(root);
            if (charHeight <= 0.0001f)
            {
                charHeight = 1f;
            }

            // 소켓 로컬 단위로 환산 (소켓 lossyScale 평균으로 나눔)
            Vector3 ls = socket.lossyScale;
            float lossyAvg = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
            if (lossyAvg <= 1e-6f)
            {
                lossyAvg = 1f;
            }
            float capLocal = (charHeight * heightFraction) / lossyAvg;

            // 콜라이더 보장 + 계산된 크기
            CapsuleCollider cap = socket.GetComponent<CapsuleCollider>();
            if (cap == null)
            {
                cap = socket.gameObject.AddComponent<CapsuleCollider>();
            }
            cap.isTrigger = true;
            cap.direction = 1;
            cap.height = capLocal;
            cap.radius = capLocal * 0.33f;
            Debug.Log($"[EquipSystem] '{socketGoName}' 캡슐 크기 계산: charHeight={charHeight:F2}, lossyAvg={lossyAvg:F2}, capLocal={capLocal:F4}");

            // EquipSocket 부착
            EquipSocket es = socket.GetComponent<EquipSocket>();
            if (es == null)
            {
                es = socket.gameObject.AddComponent<EquipSocket>();
            }
            es.slotId = slotId;
            es.fit = EquipFitMode.ContainUniform;
            es.pivot = EquipAnchorPivot.VolumeCenter;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[EquipSystem] '{prefabPath}' '{socketGoName}'에 EquipSocket(slotId='{slotId}') 부착.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 2) EquipCatalog 생성/갱신 (hairpin_placeholder → hairpin) ──
    [MenuItem("Tools/EquipSystem/2. Create Catalog")]
    public static EquipCatalog CreateCatalog()
    {
        // Resources 폴더 보장 (런타임 자동 로드용)
        if (AssetDatabase.IsValidFolder(ResourcesDir) == false)
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/Assist/EquipSystem", "Resources");
        }

        EquipCatalog cat = AssetDatabase.LoadAssetAtPath<EquipCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = ScriptableObject.CreateInstance<EquipCatalog>();
            AssetDatabase.CreateAsset(cat, CatalogPath);
        }

        // 기존에 쓰던 악세서리 전부 등록 (프리팹은 guid로 로드, 회전은 레거시 AccessoryData 값 반영)
        string[] keys = { "arona_a_chipao", "arona_a_idolfrontribbon", "arona_a_pareo", "hairpin_placeholder" };
        string[] guids = { "6b81d2f320327dd4aa52d30e5c7364b0", "1153c1a1bbd721847a6459fc0d4e4006", "dda652562023aa042b5bc4dee2bdacd7", "05ff5a1ed8f74df3a97fa8fb66159cfc" };
        string[] slots = { "head1", "head1", "head1", "hairpin" };
        Vector3[] rots = {
            new Vector3(4.889f, 60.907f, 100.891f),
            new Vector3(-12.62f, 57.244f, 84.548f),
            new Vector3(-16.131f, 52.034f, -19.598f),
            Vector3.zero
        };

        SerializedObject so = new SerializedObject(cat);
        SerializedProperty list = so.FindProperty("entries");
        list.arraySize = keys.Length;

        for (int i = 0; i < keys.Length; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("key").stringValue = keys[i];
            e.FindPropertyRelative("targetSlotId").stringValue = slots[i];
            e.FindPropertyRelative("fitBias").floatValue = 1f;
            e.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
            e.FindPropertyRelative("rotationOffset").vector3Value = rots[i];

            // guid → 프리팹 로드
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            }
            else
            {
                Debug.LogWarning($"[EquipSystem] 프리팹 로드 실패: key={keys[i]} guid={guids[i]}");
            }
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EquipSystem] 카탈로그 준비: {CatalogPath} (엔트리 {keys.Length}개).");
        return cat;
    }

    // ── 3) 데모씬 빌드 (카메라 + 라이트 + Mari POC + EquipManager + DemoController + UI) ──
    [MenuItem("Tools/EquipSystem/3. Build Demo Scene")]
    public static void BuildDemoScene()
    {
        EquipCatalog cat = AssetDatabase.LoadAssetAtPath<EquipCatalog>(CatalogPath);
        if (cat == null)
        {
            cat = CreateCatalog();
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라: arona(스케일1)의 머리/상체를 프레이밍
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

        // 캐릭터 (arona POC, 스케일1): 월드 원점 배치
        GameObject aronaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AronaPocPath);
        GameObject charInst = null;
        if (aronaPrefab != null)
        {
            charInst = (GameObject)PrefabUtility.InstantiatePrefab(aronaPrefab);

            // 프리팹 완전 언팩 (컴포넌트 자유 제거 위해). EquipSocket/캡슐 등은 유지됨.
            PrefabUtility.UnpackPrefabInstance(charInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            charInst.transform.position = Vector3.zero;
            charInst.transform.rotation = Quaternion.identity;

            // 앱 종속 컴포넌트 제거 (매니저 없이 NPE 방지). EquipSocket만 남김.
            StripAppComponents(charInst);

            // 캐릭터 전체가 보이도록 카메라 자동 프레이밍
            FrameCamera(cam, charInst);
        }
        else
        {
            Debug.LogError($"[EquipSystem] arona POC 프리팹 없음: {AronaPocPath}");
        }

        // EquipManager (catalog는 Awake의 Resources 폴백으로 로드됨)
        GameObject mgrGo = new GameObject("EquipManager");
        mgrGo.AddComponent<EquipManager>();

        // 데모 컨트롤러 (target/bindings는 public 필드라 직접 대입으로 정상 저장됨)
        GameObject demoGo = new GameObject("EquipDemoController");
        EquipDemoController demo = demoGo.AddComponent<EquipDemoController>();
        demo.target = charInst;
        demo.equipOnStart = false;
        demo.unequipKey = KeyCode.J;
        demo.unequipSlotId = "head1";
        demo.bindings = new System.Collections.Generic.List<EquipBinding>
        {
            new EquipBinding { key = KeyCode.Alpha3, accessoryKey = "arona_a_chipao" },
            new EquipBinding { key = KeyCode.Alpha4, accessoryKey = "arona_a_idolfrontribbon" },
            new EquipBinding { key = KeyCode.Alpha5, accessoryKey = "arona_a_pareo" },
            new EquipBinding { key = KeyCode.Alpha6, accessoryKey = "hairpin_placeholder" }
        };

        // 안내 UI (별도 오버레이 캔버스)
        BuildInfoCanvas();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        Debug.Log($"[EquipSystem] 데모씬 저장: {DemoScenePath}. Play → 3/4/5(head1) 교체, 6(hairpin), J 해제(head1).");
    }

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

    // 안내 텍스트용 오버레이 캔버스
    private static void BuildInfoCanvas()
    {
        GameObject canvasGo = new GameObject("InfoCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject textGo = new GameObject("InfoText");
        textGo.transform.SetParent(canvasGo.transform, false);
        UnityEngine.UI.Text text = textGo.AddComponent<UnityEngine.UI.Text>();
        text.font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");
        text.text = "EquipSystem Demo (arona)\n3: chipao   4: idolfrontribbon   5: pareo  (head1)\n6: hairpin_placeholder\nJ: Unequip head1";
        text.fontSize = 22;
        text.color = Color.white;
        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta = new Vector2(600f, 120f);
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

    // ── 유틸 ──
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
