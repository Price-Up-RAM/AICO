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
/// ChillWithYou 데모 빌더 — 본편(ChillModeManager/ChillSitData/PomodoroDeskAnimator) 기준.
/// 1) POC 프리팹 정비: 매니저 의존 마스코트 스크립트만 제거한 캐릭터 복사본 3종
///    (charcode는 본편과 동일: diana / arona_tripo / arona — 데모 튜닝값이 그대로 본편 데이터가 되도록).
///    + PomodoroDeskAnimator의 LookAround 트리거 배선 수정(멱등).
/// 2) ChillWithYouSample 씬 베이크: 본편 Canvas_Char 환경 + Desk_Set(본편처럼 transform 0) +
///    ChillModeManager 프리팹(참조 배선) + 데모 캐릭터 + SitSupport 패널(공용) + 캐릭터 교체 패널(데모 전용).
/// 사용: Tools → ChillWithYou → 1~4 순서대로. batchmode는 BuildAll(데모까지) / BuildAllAndInstall(본편 설치 포함).
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
    private const string ChillControllerPath = "Assets/ChillWithYou/Materials/Animation/PomodoroDeskAnimator.controller";
    private const string SuitBoldFontPath = "Assets/FontAssets/SUIT-Bold.asset";
    private const string ScenePath = "Assets/ChillWithYou/ChillWithYouSample.unity";

    // 구 데모 컨트롤러(AICO_POC_Animator) 삭제로 참조가 끊긴 프리팹의 복구용 — arona 원본들이 쓰는 공용 idle 컨트롤러
    private const string FallbackControllerPath = "Assets/Animation/mixamo/Blend_Animation_Controller.controller";
    private const string SeatPointName = "chairSeatPoint";
    private const int CharLayer = 3; // 본편 Canvas_Char/캐릭터 레이어
    private const int UILayer = 5;

    private static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.13f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.26f, 0.4f, 1f);
    private static readonly Color HeaderColor = new Color(0.55f, 0.75f, 1f, 1f);

    /// <summary>batchmode 진입점: 프리팹 → 데모 씬 순서로 전체 빌드. 실패 시 예외로 중단(exit≠0).</summary>
    public static void BuildAll()
    {
        if (!BuildPocPrefabs())
        {
            throw new System.Exception("[ChillWithYou] POC 프리팹 빌드 실패 — 씬 베이크를 중단합니다.");
        }
        if (!SitSupportBuilder.BuildSitSupportPrefab())
        {
            throw new System.Exception("[ChillWithYou] SitSupport 프리팹 빌드 실패.");
        }
        if (!DeskSupportBuilder.BuildDeskSupportPrefab())
        {
            throw new System.Exception("[ChillWithYou] DeskSupport 프리팹 빌드 실패.");
        }
        if (!BuildSampleScene())
        {
            throw new System.Exception("[ChillWithYou] ChillWithYouSample 씬 베이크 실패.");
        }
    }

    /// <summary>batchmode 진입점: 전체 빌드 + 본편 SampleScene에 SitSupport/DeskSupport 설치(씬 파일만 수정).</summary>
    public static void BuildAllAndInstall()
    {
        BuildAll();
        if (!SitSupportBuilder.InstallSampleScene())
        {
            throw new System.Exception("[ChillWithYou] SampleScene 설치 실패.");
        }
        if (!DeskSupportBuilder.InstallSampleScene())
        {
            throw new System.Exception("[ChillWithYou] DeskSupport SampleScene 설치 실패.");
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
        // GUI에서 실행 시 열려 있는 씬의 미저장 변경분 보호 (batchmode에서는 통과)
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[ChillWithYou] 사용자가 씬 저장을 취소해 데모 씬 베이크를 중단합니다.");
            return false;
        }

        GameObject pocPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PocPrefabPath);
        GameObject deskSetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskSetPrefabPath);
        GameObject managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChillManagerPrefabPath);
        GameObject sitSupportPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SitSupportBuilder.PrefabPath);
        if (sitSupportPrefab == null)
        {
            // 자가 치유: 3번 메뉴 산출물이 없으면 먼저 생성 (메뉴 번호 순서 의존 제거)
            SitSupportBuilder.BuildSitSupportPrefab();
            sitSupportPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SitSupportBuilder.PrefabPath);
        }
        ChillSitData sitData = AssetDatabase.LoadAssetAtPath<ChillSitData>(SitDataPath);
        if (deskSetPrefab == null || managerPrefab == null || sitData == null || sitSupportPrefab == null)
        {
            Debug.LogError(string.Format("[ChillWithYou] 필수 에셋 누락: Desk_Set={0}, ChillModeManager={1}, ChillSitData={2}, SitSupport={3}",
                deskSetPrefab != null, managerPrefab != null, sitData != null, sitSupportPrefab != null));
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

        // SitSupport 패널(공용) — 데모에서는 항상 표시
        GameObject sitSupport = (GameObject)PrefabUtility.InstantiatePrefab(sitSupportPrefab);
        SitSupportScript sitScript = sitSupport.GetComponent<SitSupportScript>();
        if (sitScript != null && sitScript.panel != null)
        {
            sitScript.panel.SetActive(true);
        }

        // DeskSupport 패널(공용) — 데모에서는 항상 표시
        GameObject deskSupportPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskSupportBuilder.PrefabPath);
        if (deskSupportPrefab != null)
        {
            GameObject deskSupport = (GameObject)PrefabUtility.InstantiatePrefab(deskSupportPrefab);
            DeskSupportScript deskScript = deskSupport.GetComponent<DeskSupportScript>();
            if (deskScript != null && deskScript.panel != null)
            {
                deskScript.panel.SetActive(true);
            }
        }

        // 캐릭터 교체 패널(데모 전용) + 컨트롤러
        BuildDemoCharPanel(canvasRt, poc, pocPrefab, manager);

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

    /// <summary>PomodoroDeskAnimator의 LookAround 배선 수정(멱등).</summary>
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
            Debug.Log("[ChillWithYou] PomodoroDeskAnimator LookAround 배선 수정 완료");
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

    // ---------------------------------------------------------------- 데모 전용 캐릭터 교체 패널

    private static void BuildDemoCharPanel(RectTransform canvasRt, GameObject pocInstance,
        GameObject pocPrefab, ChillModeManager manager)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SuitBoldFontPath);

        GameObject uiCanvasGO = new GameObject("Canvas_DemoChar", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiCanvasGO.layer = UILayer;
        Canvas uiCanvas = uiCanvasGO.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 101;
        ConfigureScaler(uiCanvasGO.GetComponent<CanvasScaler>());

        // SitSupport 패널(좌측, 폭 380) 오른쪽에 배치. 버튼은 런타임에 매니저 목록으로 동적 생성
        // (높이는 ChillWithYouDemoController가 행 수에 맞춰 조절)
        GameObject panel = CreateUIObject("DemoCharPanel", uiCanvasGO.transform, new Vector2(410f, -16f), new Vector2(380f, 106f));
        panel.AddComponent<Image>().color = PanelColor;

        CreateText(panel.transform, "Title", "캐릭터 (데모 전용)", 20f,
            new Vector2(14f, -10f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);

        // 버튼 컨테이너 — 타이틀 아래 전체 영역
        GameObject buttons = CreateUIObject("Buttons", panel.transform, new Vector2(0f, -42f), new Vector2(380f, 64f));

        // 씬 전용 싱글톤 매니저 — 데모의 가변값(캐릭터 목록/스폰 위치/착석 지연) 소유.
        // 캐릭터 추가는 이 GO의 인스펙터에서 characters에 항목을 더하면 된다 (버튼 자동 생성).
        GameObject managerGO = new GameObject("ChillWithYouSampleManager");
        ChillWithYouSampleManager sampleManager = managerGO.AddComponent<ChillWithYouSampleManager>();
        sampleManager.chillManager = manager;
        sampleManager.charParent = canvasRt;
        sampleManager.currentCharacter = pocInstance;
        sampleManager.characters = new System.Collections.Generic.List<ChillWithYouSampleManager.CharacterEntry>
        {
            new ChillWithYouSampleManager.CharacterEntry { label = "Diana", prefab = pocPrefab },
            new ChillWithYouSampleManager.CharacterEntry { label = "Arona6", prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Arona6PrefabPath) },
            new ChillWithYouSampleManager.CharacterEntry { label = "SFM", prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AronaSfmPrefabPath) },
        };

        GameObject demoGO = new GameObject("ChillWithYouDemo");
        ChillWithYouDemoController demo = demoGO.AddComponent<ChillWithYouDemoController>();
        demo.panelRect = panel.GetComponent<RectTransform>();
        demo.buttonContainer = buttons.GetComponent<RectTransform>();
        demo.buttonFont = font;
    }

    private static void ConfigureScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f, 1440f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;
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
