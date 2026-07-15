#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SitSupport(착석 튜닝 디버그 패널) 빌더.
/// 1) SitSupport.prefab 베이크: 자체 오버레이 캔버스 + 패널(기본 비활성) + SitSupportScript 배선.
/// 2) SampleScene 설치: 씬 루트에 프리팹 인스턴스 추가 — Root260616.prefab은 절대 수정하지 않는다
///    (PrefabUtility.Apply* 금지, 씬 저장만). 열기는 menutrigger Dev → SitSupport.
/// </summary>
public static class SitSupportBuilder
{
    public const string PrefabPath = "Assets/Prefabs/UI/extra/SitSupport.prefab";
    private const string SuitBoldFontPath = "Assets/FontAssets/SUIT-Bold.asset";
    private const string SitDataPath = "Assets/ChillWithYou/ScriptableObjects/ChillSitData.asset";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const int UILayer = 5;

    private static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.13f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.26f, 0.4f, 1f);
    private static readonly Color LabelColor = new Color(0.85f, 0.87f, 0.9f, 1f);
    private static readonly Color HeaderColor = new Color(0.55f, 0.75f, 1f, 1f);

    [MenuItem("Tools/ChillWithYou/3. Build SitSupport Prefab")]
    public static void BuildSitSupportPrefabMenu()
    {
        BuildSitSupportPrefab();
    }

    [MenuItem("Tools/ChillWithYou/4. Install SitSupport Into SampleScene")]
    public static void InstallSampleSceneMenu()
    {
        InstallSampleScene();
    }

    /// <summary>SitSupport.prefab 생성(기존 파일 덮어쓰기 → GUID 보존).</summary>
    public static bool BuildSitSupportPrefab()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SuitBoldFontPath);
        if (font == null)
        {
            Debug.LogWarning("[SitSupport] SUIT-Bold 폰트를 찾을 수 없습니다: " + SuitBoldFontPath);
        }
        ChillSitData sitData = AssetDatabase.LoadAssetAtPath<ChillSitData>(SitDataPath);

        string dir = Path.GetDirectoryName(PrefabPath);
        string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, dir);
        if (!Directory.Exists(abs))
        {
            Directory.CreateDirectory(abs);
            AssetDatabase.Refresh();
        }

        // 루트: 자체 오버레이 캔버스 (항상 활성 — 스크립트/싱글톤 유지) + 패널(기본 비활성)
        GameObject root = new GameObject("SitSupport", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        try
        {
            root.layer = UILayer;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            SitSupportScript script = root.AddComponent<SitSupportScript>();

            GameObject panel = CreateUIObject("Panel", root.transform, new Vector2(16f, -16f), new Vector2(380f, 840f));
            panel.AddComponent<Image>().color = PanelColor;
            script.panel = panel;

            CreateText(panel.transform, "Title", "SitSupport (착석 튜닝)", 24f,
                new Vector2(14f, -10f), new Vector2(352f, 28f), font, Color.white, TextAlignmentOptions.Left);

            TMP_Text unused;
            script.enterButton = CreateButton(panel.transform, "EnterButton", "착석", new Vector2(14f, -48f), new Vector2(172f, 32f), font, out TMP_Text enterLabel);
            script.enterButtonLabel = enterLabel;
            script.pauseButton = CreateButton(panel.transform, "PauseButton", "멈추기", new Vector2(196f, -48f), new Vector2(170f, 32f), font, out TMP_Text pauseLabel);
            script.pauseButtonLabel = pauseLabel;

            // 초기 슬라이더 값 = ChillSitData (없으면 안전한 기본값)
            ChillSitData.CharacterSitOffset d = sitData != null ? sitData.GetOffset("diana") : null;
            Vector3 pos = d != null ? d.positionOffset : Vector3.zero;
            Vector3 chair = d != null ? d.chairLocalPosition : Vector3.zero;
            float scale = d != null ? d.scaleMultiplier : 1f;
            float rotY = d != null ? Mathf.Repeat(d.rotationOffset.y, 360f) : 180f;
            Vector3 deskPos = sitData != null ? sitData.deskPositionOffset : Vector3.zero;
            float deskScale = sitData != null ? sitData.deskScaleMultiplier : 250f;
            float deskYaw = sitData != null ? Mathf.Repeat(sitData.deskRotationOffset.y, 360f) : 0f;

            CreateText(panel.transform, "CharHeader", "캐릭터 착석 오프셋", 20f,
                new Vector2(14f, -88f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
            script.charXSlider = CreateSliderRow(panel.transform, "CharX", "위치 X", -114f, -10f, 10f, pos.x, font, out script.charXValueLabel, pos.x.ToString("0.00"));
            script.charYSlider = CreateSliderRow(panel.transform, "CharY", "위치 Y", -148f, -10f, 10f, pos.y, font, out script.charYValueLabel, pos.y.ToString("0.00"));
            script.charZSlider = CreateSliderRow(panel.transform, "CharZ", "위치 Z", -182f, -10f, 10f, pos.z, font, out script.charZValueLabel, pos.z.ToString("0.00"));
            script.charScaleSlider = CreateSliderRow(panel.transform, "CharScale", "크기", -216f, 0.1f, 30f, scale, font, out script.charScaleValueLabel, scale.ToString("0.00"));
            script.charRotYSlider = CreateSliderRow(panel.transform, "CharRotY", "회전 Y", -250f, 0f, 360f, rotY, font, out script.charRotYValueLabel, rotY.ToString("0") + "°");

            CreateText(panel.transform, "ChairHeader", "의자 오프셋", 20f,
                new Vector2(14f, -288f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
            script.chairXSlider = CreateSliderRow(panel.transform, "ChairX", "의자 X", -314f, -1f, 1f, chair.x, font, out script.chairXValueLabel, chair.x.ToString("0.00"));
            script.chairYSlider = CreateSliderRow(panel.transform, "ChairY", "의자 Y", -348f, -1f, 1f, chair.y, font, out script.chairYValueLabel, chair.y.ToString("0.00"));
            script.chairZSlider = CreateSliderRow(panel.transform, "ChairZ", "의자 Z", -382f, -1f, 1f, chair.z, font, out script.chairZValueLabel, chair.z.ToString("0.00"));

            CreateText(panel.transform, "DeskHeader", "책상 (착석 지점 기준)", 20f,
                new Vector2(14f, -420f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
            // 앵커 정확값은 런타임 Start에서 재계산되므로 베이크는 근사값(deskPos)이어도 무방
            script.deskXSlider = CreateSliderRow(panel.transform, "DeskX", "위치 X", -446f,
                Mathf.Min(-1280f, deskPos.x), Mathf.Max(1280f, deskPos.x), deskPos.x, font, out script.deskXValueLabel, deskPos.x.ToString("0"));
            script.deskYSlider = CreateSliderRow(panel.transform, "DeskY", "위치 Y", -480f,
                Mathf.Min(-800f, deskPos.y), Mathf.Max(800f, deskPos.y), deskPos.y, font, out script.deskYValueLabel, deskPos.y.ToString("0"));
            script.deskScaleSlider = CreateSliderRow(panel.transform, "DeskScale", "전체 크기", -514f,
                Mathf.Min(50f, deskScale), Mathf.Max(1000f, deskScale), deskScale, font, out script.deskScaleValueLabel, deskScale.ToString("0"));

            CreateText(panel.transform, "AngleLabel", "책상 각도", 20f,
                new Vector2(14f, -548f), new Vector2(120f, 24f), font, LabelColor, TextAlignmentOptions.Left);
            script.angleValueLabel = CreateText(panel.transform, "AngleValue", deskYaw.ToString("0") + "°", 18f,
                new Vector2(282f, -548f), new Vector2(86f, 24f), font, Color.white, TextAlignmentOptions.Right);
            script.turntableButton = CreateButton(panel.transform, "TurntableButton", "턴테이블", new Vector2(14f, -574f), new Vector2(172f, 32f), font, out TMP_Text ttLabel);
            script.turntableButtonLabel = ttLabel;
            script.frontViewButton = CreateButton(panel.transform, "FrontViewButton", "정면", new Vector2(196f, -574f), new Vector2(170f, 32f), font, out unused);
            script.yawMinusButton = CreateButton(panel.transform, "YawMinusButton", "각도 -15°", new Vector2(14f, -612f), new Vector2(172f, 32f), font, out unused);
            script.yawPlusButton = CreateButton(panel.transform, "YawPlusButton", "각도 +15°", new Vector2(196f, -612f), new Vector2(170f, 32f), font, out unused);

            // 시점 프리셋 — 책상 배치(위치/각도/전체 크기) 저장 슬롯 3개
            CreateText(panel.transform, "ViewHeader", "시점 (책상 배치 프리셋)", 20f,
                new Vector2(14f, -650f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
            script.viewApplyButtons = new Button[3];
            script.viewApplyButtons[0] = CreateButton(panel.transform, "ViewApply1", "시점 1", new Vector2(14f, -676f), new Vector2(112f, 34f), font, out unused);
            script.viewApplyButtons[1] = CreateButton(panel.transform, "ViewApply2", "시점 2", new Vector2(134f, -676f), new Vector2(112f, 34f), font, out unused);
            script.viewApplyButtons[2] = CreateButton(panel.transform, "ViewApply3", "시점 3", new Vector2(254f, -676f), new Vector2(112f, 34f), font, out unused);
            script.viewSaveButtons = new Button[3];
            script.viewSaveButtons[0] = CreateButton(panel.transform, "ViewSave1", "저장 1", new Vector2(14f, -716f), new Vector2(112f, 34f), font, out unused);
            script.viewSaveButtons[1] = CreateButton(panel.transform, "ViewSave2", "저장 2", new Vector2(134f, -716f), new Vector2(112f, 34f), font, out unused);
            script.viewSaveButtons[2] = CreateButton(panel.transform, "ViewSave3", "저장 3", new Vector2(254f, -716f), new Vector2(112f, 34f), font, out unused);

            script.logButton = CreateButton(panel.transform, "LogButton", "값 로그", new Vector2(14f, -756f), new Vector2(172f, 32f), font, out unused);
            script.saveButton = CreateButton(panel.transform, "SaveButton", "데이터 저장", new Vector2(196f, -756f), new Vector2(170f, 32f), font, out unused);
            script.resetButton = CreateButton(panel.transform, "ResetButton", "리셋 (시작값 복원)", new Vector2(14f, -794f), new Vector2(352f, 32f), font, out unused);

            panel.SetActive(false); // 기본 숨김 — 본편은 Dev 메뉴로, 데모는 씬 오버라이드로 켠다

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SitSupport] 프리팹 베이크 완료: " + PrefabPath);
        return true;
    }

    /// <summary>SampleScene 씬 루트에 SitSupport 인스턴스 설치(멱등). Root260616.prefab은 불변 — 씬 파일만 저장.</summary>
    public static bool InstallSampleScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[SitSupport] 프리팹이 없습니다. 먼저 Build SitSupport Prefab을 실행하세요: " + PrefabPath);
            return false;
        }

        // GUI에서 실행 시 열려 있는 씬의 미저장 변경분 보호 (batchmode에서는 통과)
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[SitSupport] 사용자가 씬 저장을 취소해 설치를 중단합니다.");
            return false;
        }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        // 멱등: 씬에 이미 있으면 재사용 (비활성 포함 탐색)
        SitSupportScript existing = null;
        foreach (SitSupportScript s in Resources.FindObjectsOfTypeAll<SitSupportScript>())
        {
            if (s != null && s.gameObject.scene.IsValid())
            {
                existing = s;
                break;
            }
        }

        if (existing == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "SitSupport";
            existing = instance.GetComponent<SitSupportScript>();
            Debug.Log("[SitSupport] SampleScene 씬 루트에 인스턴스 추가");
        }
        else
        {
            Debug.Log("[SitSupport] SampleScene에 이미 설치되어 있어 재사용");
        }

        // 본편 기본 상태: 루트 활성(싱글톤 유지) + 패널 숨김 (Dev 메뉴로 열기)
        existing.gameObject.SetActive(true);
        if (existing.panel != null)
        {
            existing.panel.SetActive(false);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SitSupport] SampleScene 설치 완료 (씬 파일만 수정, Root260616.prefab 불변)");
        return true;
    }

    // ---------------------------------------------------------------- UI 헬퍼 (ChillWithYouSampleBuilder와 동일 규약)

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
}
#endif
