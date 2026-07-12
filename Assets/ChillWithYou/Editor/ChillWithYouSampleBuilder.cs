#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ChillWithYou 데모 빌더 — 본편(ChillModeManager/ChillSitData/HY Motion Animator) 기준.
/// 1) POC 프리팹 정비: 매니저 의존 마스코트 스크립트만 제거한 캐릭터 복사본 3종
///    (charcode는 본편과 동일: diana / arona_tripo / arona — 데모 튜닝값이 그대로 본편 데이터가 되도록).
///    + HY Motion Animator의 LookAround 트리거 배선 수정(멱등).
/// 2) ChillWithYouSample 씬 베이크: 본편 Canvas_Char 환경 + Desk_Set(본편처럼 transform 0) +
///    ChillModeManager 프리팹(참조 배선) + 데모 캐릭터 + 튜닝 UI(ChillWithYouDemoController).
/// 사용: Tools → ChillWithYou → 1, 2 순서대로. batchmode는 BuildAll.
/// </summary>
public static class ChillWithYouSampleBuilder
{
    private const string DianaPrefabPath = "Assets/Char/diana/diana_rigging.prefab";
    private const string Arona6PrefabPath = "Assets/Prefabs/Char_toon/arona_6_clean_POC2.prefab";
    private const string AronaSfmPrefabPath = "Assets/Prefabs/Char_toon/arona_sfm_POC.prefab";
    private const string PocPrefabPath = "Assets/ChillWithYou/Prefabs/AICO_POC.prefab";

    private const string DeskSetPrefabPath = "Assets/ChillWithYou/Prefabs/Desk_Set.prefab";
    private const string ChillManagerPrefabPath = "Assets/ChillWithYou/Prefabs/ChillModeManager.prefab";
    private const string SitDataPath = "Assets/ChillWithYou/ScriptableObjects/ChillSitData.asset";
    private const string ChillControllerPath = "Assets/ChillWithYou/Materials/Animation/HY Motion Animator.controller";
    private const string SuitBoldFontPath = "Assets/FontAssets/SUIT-Bold.asset";
    private const string ScenePath = "Assets/ChillWithYou/ChillWithYouSample.unity";

    // 구 데모 컨트롤러(AICO_POC_Animator) 삭제로 참조가 끊긴 프리팹의 복구용 — arona 원본들이 쓰는 공용 idle 컨트롤러
    private const string FallbackControllerPath = "Assets/Animation/mixamo/Blend_Animation_Controller.controller";
    private const string SeatPointName = "chairSeatPoint";
    private const int CharLayer = 3; // 본편 Canvas_Char/캐릭터 레이어
    private const int UILayer = 5;

    private static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.13f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.26f, 0.4f, 1f);
    private static readonly Color LabelColor = new Color(0.85f, 0.87f, 0.9f, 1f);
    private static readonly Color HeaderColor = new Color(0.55f, 0.75f, 1f, 1f);

    /// <summary>batchmode 진입점: 프리팹 → 씬 순서로 전체 빌드. 실패 시 예외로 중단(exit≠0).</summary>
    public static void BuildAll()
    {
        if (!BuildPocPrefabs())
        {
            throw new System.Exception("[ChillWithYou] POC 프리팹 빌드 실패 — 씬 베이크를 중단합니다.");
        }
        if (!BuildSampleScene())
        {
            throw new System.Exception("[ChillWithYou] ChillWithYouSample 씬 베이크 실패.");
        }
    }

    [MenuItem("Tools/ChillWithYou/1. Build POC Prefabs (Diana + Arona)")]
    public static void BuildPocPrefabsMenu()
    {
        BuildPocPrefabs();
    }

    [MenuItem("Tools/ChillWithYou/2. Build ChillWithYouSample Scene")]
    public static void BuildSampleSceneMenu()
    {
        BuildSampleScene();
    }

    public static bool BuildPocPrefabs()
    {
        bool ok = FixChillControllerWiring();
        // diana: 원본을 읽어 AICO_POC로 저장(기존 파일 덮어쓰기 → GUID 보존)
        ok &= ProcessPocPrefab(DianaPrefabPath, PocPrefabPath, "diana");
        // arona: 준비된 POC 복사본을 in-place 처리 (원본 arona_6_clean/arona_sfm은 불변)
        ok &= ProcessPocPrefab(Arona6PrefabPath, Arona6PrefabPath, "arona_tripo");
        ok &= ProcessPocPrefab(AronaSfmPrefabPath, AronaSfmPrefabPath, "arona");
        AssetDatabase.SaveAssets();
        return ok;
    }

    public static bool BuildSampleScene()
    {
        if (!IsPocProcessed(PocPrefabPath) || !IsPocProcessed(Arona6PrefabPath) || !IsPocProcessed(AronaSfmPrefabPath))
        {
            Debug.LogError("[ChillWithYou] POC 프리팹이 없거나 미처리 상태입니다. 먼저 1번 메뉴(Build POC Prefabs)를 실행하세요.");
            return false;
        }
        GameObject pocPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PocPrefabPath);
        GameObject deskSetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskSetPrefabPath);
        GameObject managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChillManagerPrefabPath);
        ChillSitData sitData = AssetDatabase.LoadAssetAtPath<ChillSitData>(SitDataPath);
        if (deskSetPrefab == null || managerPrefab == null || sitData == null)
        {
            Debug.LogError(string.Format("[ChillWithYou] 필수 에셋 누락: Desk_Set={0}, ChillModeManager={1}, ChillSitData={2}",
                deskSetPrefab != null, managerPrefab != null, sitData != null));
            return false;
        }

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Main Camera — 본편 Main Camera와 동일한 핵심 설정(FOV 10, 투명 배경, 레이어 3+6만 렌더)
        GameObject camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        Camera camera = camGO.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.0078f, 0.0039f, 0f, 0f);
        camera.fieldOfView = 10f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        camera.cullingMask = (1 << CharLayer) | (1 << 6);
        camGO.transform.position = new Vector3(0f, 1f, -10f);

        // Directional Light
        GameObject lightGO = new GameObject("Directional Light", typeof(Light));
        Light light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95686275f, 0.8392157f);
        light.intensity = 1f;
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        lightGO.transform.position = new Vector3(0f, 3f, 0f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Canvas_Char — 본편과 동일한 캔버스 환경
        GameObject canvasGO = new GameObject("Canvas_Char", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = CharLayer;
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100f;
        canvas.vertexColorAlwaysGammaSpace = true;
        ConfigureScaler(canvasGO.GetComponent<CanvasScaler>());
        canvasGO.GetComponent<GraphicRaycaster>().enabled = false; // 본편 Canvas_Char도 비활성
        RectTransform canvasRt = canvasGO.GetComponent<RectTransform>();

        // Desk_Set — 본편 SampleScene과 동일하게 transform 전부 0 (Enter 시 ChillModeManager 오프셋으로 펼쳐짐)
        GameObject deskSet = (GameObject)PrefabUtility.InstantiatePrefab(deskSetPrefab, canvasRt);
        RectTransform deskRt = deskSet.GetComponent<RectTransform>();
        deskRt.anchorMin = deskRt.anchorMax = deskRt.pivot = new Vector2(0.5f, 0.5f);
        deskRt.sizeDelta = new Vector2(100f, 100f);
        deskRt.anchoredPosition3D = Vector3.zero;
        deskRt.localRotation = Quaternion.identity;
        deskRt.localScale = Vector3.one;

        Transform seatPoint = FindDeep(deskSet.transform, SeatPointName);
        if (seatPoint == null)
        {
            Debug.LogError("[ChillWithYou] Desk_Set에서 " + SeatPointName + "을 찾을 수 없습니다.");
            return false;
        }

        // 데모 캐릭터 — 본편 캐릭터처럼 Canvas_Char 직속 (프리팹 기본 회전/스케일 유지)
        GameObject poc = (GameObject)PrefabUtility.InstantiatePrefab(pocPrefab, canvasRt);
        RectTransform pocRt = poc.GetComponent<RectTransform>();
        pocRt.anchoredPosition3D = new Vector3(0f, -450f, 0f);

        // ChillModeManager — 본편과 같은 프리팹, 씬 참조만 배선
        GameObject managerGO = (GameObject)PrefabUtility.InstantiatePrefab(managerPrefab);
        ChillModeManager manager = managerGO.GetComponent<ChillModeManager>();
        manager.deskSetRoot = deskRt;
        manager.chairRoot = seatPoint.parent;
        manager.chairSeatPoint = seatPoint as RectTransform;
        manager.overrideCharacter = poc;
        // 책상 배치의 단일 출처는 ChillSitData — 베이크 시점에도 SO 값으로 정렬
        manager.deskPositionOffset = sitData.deskPositionOffset;
        manager.deskRotationOffset = sitData.deskRotationOffset;
        manager.deskScaleMultiplier = sitData.deskScaleMultiplier;

        // 데모 메뉴 UI (좌측 상단) — 별도 오버레이 캔버스
        BuildDemoMenu(manager, sitData, canvasRt, poc, pocPrefab);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[ChillWithYou] ChillWithYouSample 씬 생성 완료: " + ScenePath);
        return true;
    }

    // ---------------------------------------------------------------- 프리팹/컨트롤러 처리

    /// <summary>sourcePath 프리팹을 읽어 매니저 의존 스크립트를 제거하고 savePath로 저장(GUID 보존).
    /// 컨트롤러/스케일은 원본 유지 — 착석 시 컨트롤러 교체는 ChillModeManager가 수행한다.</summary>
    private static bool ProcessPocPrefab(string sourcePath, string savePath, string charcode)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (asset == null)
        {
            Debug.LogError("[ChillWithYou] POC 소스 프리팹을 찾을 수 없습니다: " + sourcePath);
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            root.name = Path.GetFileNameWithoutExtension(savePath);

            // 매니저 의존 마스코트 동작 제거 (데모씬에서 NRE 방지; 본편 원본 프리팹은 불변)
            RemoveAll<FallingObject>(root);
            RemoveAll<MenuTrigger>(root);
            RemoveAll<ClickHandler>(root);
            RemoveAll<DragHandler>(root);
            RemoveAll<DragHandler2D>(root);
            RemoveAll<WheelHandler>(root);
            RemoveAll<AnimationController>(root);
            RemoveAll<EmotionFaceAronaController>(root);
            RemoveAll<EmotionFaceAronaNewController>(root);

            // 폐기된 스크립트(구 ChillWithYouSeatAnimator 등) 잔재 정리
            int removed = RemoveMissingScripts(root);
            if (removed > 0)
            {
                Debug.Log("[ChillWithYou] missing script " + removed + "개 정리: " + savePath);
            }

            // 구 데모 컨트롤러(AICO_POC_Animator) 삭제로 컨트롤러 참조가 끊긴 경우 공용 idle 컨트롤러로 복구
            // (유효한 컨트롤러는 손대지 않음 — 착석 시 교체는 ChillModeManager의 몫)
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController == null)
            {
                RuntimeAnimatorController fallback =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackControllerPath);
                if (fallback == null)
                {
                    Debug.LogError("[ChillWithYou] 대체 컨트롤러를 찾을 수 없습니다: " + FallbackControllerPath);
                    return false;
                }
                animator.runtimeAnimatorController = fallback;
                Debug.Log("[ChillWithYou] 끊긴 컨트롤러를 공용 idle 컨트롤러로 복구: " + savePath);
            }

            // charcode는 본편과 동일하게 — ChillSitData 엔트리를 본편과 공유
            CharAttributes attr = root.GetComponent<CharAttributes>();
            if (attr != null)
            {
                attr.charcode = charcode;
            }

            SetLayerRecursive(root, CharLayer);

            PrefabUtility.SaveAsPrefabAsset(root, savePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[ChillWithYou] POC 프리팹 처리 완료: " + savePath + " (charcode=" + charcode + ")");
        return true;
    }

    /// <summary>HY Motion Animator의 LookAround 배선 수정(멱등).
    /// 원본 상태: SitTyping의 전이가 모두 무조건 exit-time이라 SetTrigger("LookAround")가 소비되지 않고,
    /// SitLookAround에는 나가는 전이가 없어 진입 시 복귀 불가였다.</summary>
    private static bool FixChillControllerWiring()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ChillControllerPath);
        if (controller == null)
        {
            Debug.LogError("[ChillWithYou] 착석 컨트롤러를 찾을 수 없습니다: " + ChillControllerPath);
            return false;
        }
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState typing = FindState(sm, "SitTyping");
        AnimatorState look = FindState(sm, "SitLookAround");
        if (typing == null || look == null)
        {
            Debug.LogError("[ChillWithYou] SitTyping/SitLookAround 상태를 찾을 수 없습니다.");
            return false;
        }

        bool changed = false;

        if (!controller.parameters.Any(p => p.name == "LookAround"))
        {
            controller.AddParameter("LookAround", AnimatorControllerParameterType.Trigger);
            changed = true;
        }

        // SitTyping → SitLookAround : LookAround 트리거로 즉시 전이
        AnimatorStateTransition toLook = typing.transitions.FirstOrDefault(t => t.destinationState == look);
        if (toLook == null)
        {
            toLook = typing.AddTransition(look);
            changed = true;
        }
        if (toLook.conditions.Length == 0)
        {
            toLook.AddCondition(AnimatorConditionMode.If, 0f, "LookAround");
            changed = true;
        }
        if (toLook.hasExitTime)
        {
            toLook.hasExitTime = false;
            changed = true;
        }

        // SitLookAround → SitTyping : 1회 재생 후 복귀
        AnimatorStateTransition back = look.transitions.FirstOrDefault(t => t.destinationState == typing);
        if (back == null)
        {
            back = look.AddTransition(typing);
            back.hasExitTime = true;
            back.exitTime = 0.97f;
            back.duration = 0.25f;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(toLook);
            EditorUtility.SetDirty(back);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChillWithYou] HY Motion Animator LookAround 배선 수정 완료");
        }
        return true;
    }

    /// <summary>프리팹이 존재하고 POC 처리(마스코트 스크립트 제거)까지 끝났는지 검사.</summary>
    private static bool IsPocProcessed(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[ChillWithYou] 프리팹 없음: " + prefabPath);
            return false;
        }
        if (prefab.GetComponentInChildren<FallingObject>(true) != null)
        {
            Debug.LogError("[ChillWithYou] 미처리(FallingObject 잔존): " + prefabPath);
            return false;
        }
        return true;
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState child in sm.states)
        {
            if (child.state != null && child.state.name == name) return child.state;
        }
        return null;
    }

    private static int RemoveMissingScripts(GameObject go)
    {
        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        foreach (Transform child in go.transform)
        {
            count += RemoveMissingScripts(child.gameObject);
        }
        return count;
    }

    // ---------------------------------------------------------------- 데모 메뉴 UI

    private static void BuildDemoMenu(ChillModeManager manager, ChillSitData sitData,
        RectTransform canvasRt, GameObject pocInstance, GameObject pocPrefab)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SuitBoldFontPath);
        if (font == null)
        {
            Debug.LogWarning("[ChillWithYou] SUIT-Bold 폰트를 찾을 수 없습니다: " + SuitBoldFontPath);
        }

        GameObject uiCanvasGO = new GameObject("Canvas_DemoUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiCanvasGO.layer = UILayer;
        Canvas uiCanvas = uiCanvasGO.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 100;
        ConfigureScaler(uiCanvasGO.GetComponent<CanvasScaler>());

        GameObject panel = CreateUIObject("DemoMenuPanel", uiCanvasGO.transform, new Vector2(16f, -16f), new Vector2(380f, 800f));
        panel.AddComponent<Image>().color = PanelColor;

        CreateText(panel.transform, "Title", "ChillWithYou 데모", 24f,
            new Vector2(14f, -10f), new Vector2(352f, 28f), font, Color.white, TextAlignmentOptions.Left);

        GameObject demoGO = new GameObject("ChillWithYouDemo");
        ChillWithYouDemoController demo = demoGO.AddComponent<ChillWithYouDemoController>();
        demo.chillManager = manager;
        demo.sitData = sitData;
        demo.charParent = canvasRt;
        demo.currentCharacter = pocInstance;

        // 초기 슬라이더 값 = ChillSitData의 diana 엔트리
        ChillSitData.CharacterSitOffset d = sitData.GetOffset("diana");
        Vector3 pos = d != null ? d.positionOffset : Vector3.zero;
        Vector3 chair = d != null ? d.chairLocalPosition : Vector3.zero;
        float scale = d != null ? d.scaleMultiplier : 1f;
        float rotY = d != null ? Mathf.Repeat(d.rotationOffset.y, 360f) : 180f;

        TMP_Text unused;
        demo.enterButton = CreateButton(panel.transform, "EnterButton", "착석", new Vector2(14f, -48f), new Vector2(172f, 32f), font, out demo.enterButtonLabel);
        demo.pauseButton = CreateButton(panel.transform, "PauseButton", "멈추기", new Vector2(196f, -48f), new Vector2(170f, 32f), font, out demo.pauseButtonLabel);

        CreateText(panel.transform, "CharHeader", "캐릭터 착석 오프셋", 20f,
            new Vector2(14f, -88f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
        demo.charXSlider = CreateSliderRow(panel.transform, "CharX", "위치 X", -114f, -10f, 10f, pos.x, font, out demo.charXValueLabel, pos.x.ToString("0.00"));
        demo.charYSlider = CreateSliderRow(panel.transform, "CharY", "위치 Y", -148f, -10f, 10f, pos.y, font, out demo.charYValueLabel, pos.y.ToString("0.00"));
        demo.charZSlider = CreateSliderRow(panel.transform, "CharZ", "위치 Z", -182f, -10f, 10f, pos.z, font, out demo.charZValueLabel, pos.z.ToString("0.00"));
        demo.charScaleSlider = CreateSliderRow(panel.transform, "CharScale", "크기", -216f, 0.1f, 30f, scale, font, out demo.charScaleValueLabel, scale.ToString("0.00"));
        demo.charRotYSlider = CreateSliderRow(panel.transform, "CharRotY", "회전 Y", -250f, 0f, 360f, rotY, font, out demo.charRotYValueLabel, rotY.ToString("0") + "°");

        CreateText(panel.transform, "ChairHeader", "의자 오프셋", 20f,
            new Vector2(14f, -288f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
        demo.chairXSlider = CreateSliderRow(panel.transform, "ChairX", "의자 X", -314f, -1f, 1f, chair.x, font, out demo.chairXValueLabel, chair.x.ToString("0.00"));
        demo.chairYSlider = CreateSliderRow(panel.transform, "ChairY", "의자 Y", -348f, -1f, 1f, chair.y, font, out demo.chairYValueLabel, chair.y.ToString("0.00"));
        demo.chairZSlider = CreateSliderRow(panel.transform, "ChairZ", "의자 Z", -382f, -1f, 1f, chair.z, font, out demo.chairZValueLabel, chair.z.ToString("0.00"));

        // 책상 슬라이더 초기값 = 시트 앵커(착석 지점 좌표) — 컨트롤러의 ComputeSeatAnchor와 동일 공식
        Vector3 anchor = manager.deskPositionOffset;
        if (manager.chairSeatPoint != null && manager.deskSetRoot != null)
        {
            Vector3 seatLocal = manager.deskSetRoot.InverseTransformPoint(manager.chairSeatPoint.position);
            anchor = manager.deskPositionOffset
                + Quaternion.Euler(manager.deskRotationOffset) * (seatLocal * manager.deskScaleMultiplier);
        }

        CreateText(panel.transform, "DeskHeader", "책상 (착석 지점 기준)", 20f,
            new Vector2(14f, -420f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
        demo.deskXSlider = CreateSliderRow(panel.transform, "DeskX", "위치 X", -446f,
            Mathf.Min(-1280f, anchor.x), Mathf.Max(1280f, anchor.x), anchor.x, font, out demo.deskXValueLabel, anchor.x.ToString("0"));
        demo.deskYSlider = CreateSliderRow(panel.transform, "DeskY", "위치 Y", -480f,
            Mathf.Min(-800f, anchor.y), Mathf.Max(800f, anchor.y), anchor.y, font, out demo.deskYValueLabel, anchor.y.ToString("0"));
        demo.deskScaleSlider = CreateSliderRow(panel.transform, "DeskScale", "전체 크기", -514f,
            Mathf.Min(50f, manager.deskScaleMultiplier), Mathf.Max(1000f, manager.deskScaleMultiplier),
            manager.deskScaleMultiplier, font, out demo.deskScaleValueLabel, manager.deskScaleMultiplier.ToString("0"));

        CreateText(panel.transform, "AngleLabel", "책상 각도", 20f,
            new Vector2(14f, -548f), new Vector2(120f, 24f), font, LabelColor, TextAlignmentOptions.Left);
        demo.angleValueLabel = CreateText(panel.transform, "AngleValue", manager.deskRotationOffset.y.ToString("0") + "°", 18f,
            new Vector2(282f, -548f), new Vector2(86f, 24f), font, Color.white, TextAlignmentOptions.Right);
        demo.turntableButton = CreateButton(panel.transform, "TurntableButton", "턴테이블", new Vector2(14f, -574f), new Vector2(172f, 32f), font, out demo.turntableButtonLabel);
        demo.frontViewButton = CreateButton(panel.transform, "FrontViewButton", "정면", new Vector2(196f, -574f), new Vector2(170f, 32f), font, out unused);
        demo.yawMinusButton = CreateButton(panel.transform, "YawMinusButton", "각도 -15°", new Vector2(14f, -612f), new Vector2(172f, 32f), font, out unused);
        demo.yawPlusButton = CreateButton(panel.transform, "YawPlusButton", "각도 +15°", new Vector2(196f, -612f), new Vector2(170f, 32f), font, out unused);

        demo.logButton = CreateButton(panel.transform, "LogButton", "값 로그", new Vector2(14f, -650f), new Vector2(172f, 32f), font, out unused);
        demo.saveButton = CreateButton(panel.transform, "SaveButton", "데이터 저장", new Vector2(196f, -650f), new Vector2(170f, 32f), font, out unused);
        demo.resetButton = CreateButton(panel.transform, "ResetButton", "리셋 (시작값 복원)", new Vector2(14f, -688f), new Vector2(352f, 32f), font, out unused);

        CreateText(panel.transform, "CharListHeader", "캐릭터", 20f,
            new Vector2(14f, -726f), new Vector2(200f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
        demo.characterPrefabs = new[]
        {
            pocPrefab,
            AssetDatabase.LoadAssetAtPath<GameObject>(Arona6PrefabPath),
            AssetDatabase.LoadAssetAtPath<GameObject>(AronaSfmPrefabPath),
        };
        demo.characterButtons = new Button[3];
        demo.characterButtons[0] = CreateButton(panel.transform, "CharDiana", "Diana", new Vector2(14f, -752f), new Vector2(112f, 34f), font, out unused);
        demo.characterButtons[1] = CreateButton(panel.transform, "CharArona6", "Arona6", new Vector2(134f, -752f), new Vector2(112f, 34f), font, out unused);
        demo.characterButtons[2] = CreateButton(panel.transform, "CharAronaSFM", "SFM", new Vector2(254f, -752f), new Vector2(112f, 34f), font, out unused);
    }

    private static void ConfigureScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f, 1440f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
    }

    private static Slider CreateSliderRow(Transform panel, string name, string label, float y,
        float min, float max, float value, TMP_FontAsset font, out TMP_Text valueLabel, string valueText)
    {
        CreateText(panel, name + "Label", label, 18f,
            new Vector2(14f, y), new Vector2(94f, 24f), font, LabelColor, TextAlignmentOptions.Left);
        Slider slider = CreateSlider(panel, name + "Slider", new Vector2(110f, y), new Vector2(162f, 24f), min, max, value);
        valueLabel = CreateText(panel, name + "Value", valueText, 18f,
            new Vector2(282f, y), new Vector2(86f, 24f), font, Color.white, TextAlignmentOptions.Right);
        return slider;
    }

    private static Slider CreateSlider(Transform parent, string name, Vector2 pos, Vector2 size,
        float min, float max, float value)
    {
        GameObject go = CreateUIObject(name, parent, pos, size);
        Slider slider = go.AddComponent<Slider>();

        GameObject bg = CreateUIObject("Background", go.transform, Vector2.zero, Vector2.zero);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(1f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(0f, 8f);
        bg.AddComponent<Image>().color = new Color(0.22f, 0.24f, 0.28f, 1f);

        GameObject fillArea = CreateUIObject("Fill Area", go.transform, Vector2.zero, Vector2.zero);
        RectTransform faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.5f);
        faRt.anchorMax = new Vector2(1f, 0.5f);
        faRt.pivot = new Vector2(0.5f, 0.5f);
        faRt.anchoredPosition = Vector2.zero;
        faRt.sizeDelta = new Vector2(-20f, 8f);

        GameObject fill = CreateUIObject("Fill", fillArea.transform, Vector2.zero, Vector2.zero);
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0.5f, 0.5f);
        fillRt.sizeDelta = new Vector2(10f, 0f);
        fill.AddComponent<Image>().color = new Color(0.3f, 0.55f, 0.95f, 1f);

        GameObject handleArea = CreateUIObject("Handle Slide Area", go.transform, Vector2.zero, Vector2.zero);
        RectTransform haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = new Vector2(0f, 0.5f);
        haRt.anchorMax = new Vector2(1f, 0.5f);
        haRt.pivot = new Vector2(0.5f, 0.5f);
        haRt.anchoredPosition = Vector2.zero;
        haRt.sizeDelta = new Vector2(-20f, 0f);

        GameObject handle = CreateUIObject("Handle", handleArea.transform, Vector2.zero, new Vector2(20f, 20f));
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.85f, 0.87f, 0.92f, 1f);

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        return slider;
    }

    private static Button CreateButton(Transform parent, string name, string label,
        Vector2 pos, Vector2 size, TMP_FontAsset font, out TMP_Text labelText)
    {
        GameObject go = CreateUIObject(name, parent, pos, size);
        Image img = go.AddComponent<Image>();
        img.color = ButtonColor;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        labelText = CreateText(go.transform, "Label", label, 20f, Vector2.zero, Vector2.zero,
            font, Color.white, TextAlignmentOptions.Center);
        RectTransform lrt = labelText.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = Vector2.zero;
        return btn;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, float fontSize,
        Vector2 pos, Vector2 size, TMP_FontAsset font, Color color, TextAlignmentOptions align)
    {
        GameObject go = CreateUIObject(name, parent, pos, size);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>좌상단 기준(anchor/pivot 0,1) 고정 배치 UI 오브젝트 생성. pos.y는 음수로 내려간다.</summary>
    private static GameObject CreateUIObject(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = UILayer;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go;
    }

    // ---------------------------------------------------------------- 공용 유틸

    private static void RemoveAll<T>(GameObject root) where T : Component
    {
        foreach (T comp in root.GetComponentsInChildren<T>(true))
        {
            Object.DestroyImmediate(comp);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}
#endif
