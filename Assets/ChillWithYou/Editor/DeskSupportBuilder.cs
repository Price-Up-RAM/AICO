#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DeskSupport(책상 꾸미기 패널) 빌더.
/// 1) 테스트 장식 프리팹 2종 생성 (원시형 + PolygonOffice 머티리얼 — 실장식 확보 전 검증용)
/// 2) DeskSupportData.asset 생성 (스킨 = PolygonOffice_Material_01_A~04_A, 장식 = 테스트 2종.
///    이미 존재하면 사용자 편집 보존 — 빈 목록만 기본값으로 채움)
/// 3) Desk_Set.prefab에 DecoSlot_01~03 마커 추가 (멱등 — 책상 상판 렌더러 bounds 기반 근사 배치,
///    정확한 위치는 에디터에서 마커를 직접 옮겨 조정)
/// 4) DeskSupport.prefab 베이크 (SitSupport와 동일 규약: 오버레이 캔버스 + 패널 기본 숨김.
///    스킨/슬롯 버튼은 런타임에 DeskSupportData로 동적 생성)
/// 5) SampleScene 설치 (씬 루트 인스턴스, Root260616.prefab 불변)
/// </summary>
public static class DeskSupportBuilder
{
    public const string PrefabPath = "Assets/ChillWithYou/Prefabs/DeskSupport.prefab";
    private const string DataPath = "Assets/ChillWithYou/ScriptableObjects/DeskSupportData.asset";
    private const string DeskSetPath = "Assets/ChillWithYou/Prefabs/Desk_Set.prefab";
    private const string DecoFolder = "Assets/ChillWithYou/Prefabs/Deco";
    private const string MaterialFolder = "Assets/ChillWithYou/Materials";
    private const string SuitBoldFontPath = "Assets/FontAssets/SUIT-Bold.asset";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string DeskPropName = "SM_Prop_Desk_02";
    private const int SlotCount = 3;
    private const int UILayer = 5;

    private static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.13f, 0.92f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.26f, 0.4f, 1f);
    private static readonly Color HeaderColor = new Color(0.55f, 0.75f, 1f, 1f);

    [MenuItem("Tools/ChillWithYou/6. Build DeskSupport Prefab")]
    public static void BuildDeskSupportPrefabMenu()
    {
        BuildDeskSupportPrefab();
    }

    [MenuItem("Tools/ChillWithYou/7. Install DeskSupport Into SampleScene")]
    public static void InstallSampleSceneMenu()
    {
        InstallSampleScene();
    }

    public static bool BuildDeskSupportPrefab()
    {
        List<DeskSupportData.DecoDef> testDecos = EnsureTestDecoPrefabs();
        DeskSupportData data = EnsureData(testDecos);
        if (data == null)
        {
            Debug.LogError("[DeskSupport] DeskSupportData 생성 실패");
            return false;
        }
        if (!EnsureDeskSlots())
        {
            return false;
        }
        return BakePanelPrefab(data);
    }

    // ---------------------------------------------------------------- 테스트 장식 프리팹

    private static List<DeskSupportData.DecoDef> EnsureTestDecoPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(DecoFolder))
        {
            AssetDatabase.CreateFolder("Assets/ChillWithYou/Prefabs", "Deco");
        }

        var defs = new List<DeskSupportData.DecoDef>
        {
            new DeskSupportData.DecoDef
            {
                id = "test_cube",
                label = "큐브(테스트)",
                prefab = EnsurePrimitiveDeco("Deco_TestCube", PrimitiveType.Cube, 0.12f,
                    MaterialFolder + "/PolygonOffice_Material_02_A.mat"),
            },
            new DeskSupportData.DecoDef
            {
                id = "test_sphere",
                label = "구(테스트)",
                prefab = EnsurePrimitiveDeco("Deco_TestSphere", PrimitiveType.Sphere, 0.1f,
                    MaterialFolder + "/PolygonOffice_Material_03_A.mat"),
            },
        };
        return defs;
    }

    // 원시형 기반 장식 프리팹 (멱등 — 이미 있으면 재생성하지 않음). 콜라이더는 제거(클릭 간섭 방지).
    private static GameObject EnsurePrimitiveDeco(string name, PrimitiveType type, float scale, string materialPath)
    {
        string path = DecoFolder + "/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        GameObject go = GameObject.CreatePrimitive(type);
        try
        {
            go.name = name;
            // 피벗(마커 위치)이 바닥에 오도록 절반 높이만큼 올린 자식 구조 대신, 원시형 자체를 올려 저장
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(0f, scale * 0.5f, 0f);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            // 마커 아래 localPosition 0으로 붙는 프리팹이므로, 루트(빈 노드) + 원시형 자식 구조로 저장
            GameObject root = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log("[DeskSupport] 테스트 장식 생성: " + path);
            return prefab;
        }
        finally
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    // ---------------------------------------------------------------- 데이터

    private static DeskSupportData EnsureData(List<DeskSupportData.DecoDef> testDecos)
    {
        DeskSupportData data = AssetDatabase.LoadAssetAtPath<DeskSupportData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DeskSupportData>();
            AssetDatabase.CreateAsset(data, DataPath);
            Debug.Log("[DeskSupport] DeskSupportData 생성: " + DataPath);
        }

        // 사용자 편집 보존 — 비어 있을 때만 기본값 주입
        if (data.skinMaterials == null || data.skinMaterials.Count == 0)
        {
            data.skinMaterials = new List<Material>();
            for (int i = 1; i <= 4; i++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    MaterialFolder + "/PolygonOffice_Material_0" + i + "_A.mat");
                if (material != null)
                {
                    data.skinMaterials.Add(material);
                }
            }
        }
        if (data.decorations == null || data.decorations.Count == 0)
        {
            data.decorations = testDecos;
        }
        data.GetOrCreateDesk(DeskSupportData.DefaultDeskId);

        EditorUtility.SetDirty(data);
        return data;
    }

    // ---------------------------------------------------------------- Desk_Set 슬롯 마커

    private static bool EnsureDeskSlots()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DeskSetPath);
        try
        {
            Transform existing = FindDeep(root.transform, "DecoSlot_01");
            if (existing != null)
            {
                return true; // 멱등 — 이미 설치됨 (위치는 사용자가 에디터에서 조정했을 수 있으니 불변)
            }

            Transform deskProp = FindDeep(root.transform, DeskPropName);
            MeshRenderer deskRenderer = deskProp != null ? deskProp.GetComponentInChildren<MeshRenderer>(true) : null;
            if (deskRenderer == null)
            {
                Debug.LogError("[DeskSupport] Desk_Set에서 " + DeskPropName + " 렌더러를 찾을 수 없습니다.");
                return false;
            }

            // 책상 상판 근사 배치 (좌/중/우) — 프리팹 루트가 항등이라 bounds는 로컬 기준과 동일
            Bounds bounds = deskRenderer.bounds;
            float y = bounds.max.y + 0.005f;
            float[] xRatios = { 0.15f, 0.5f, 0.85f };
            for (int i = 0; i < SlotCount; i++)
            {
                GameObject marker = new GameObject("DecoSlot_0" + (i + 1));
                marker.transform.SetParent(root.transform, false);
                marker.transform.position = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, xRatios[i]), y, bounds.center.z);
                marker.layer = root.layer;
            }

            PrefabUtility.SaveAsPrefabAsset(root, DeskSetPath);
            Debug.Log("[DeskSupport] Desk_Set에 DecoSlot_01~0" + SlotCount + " 마커 추가 (위치는 에디터에서 조정 가능)");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- 패널 프리팹 베이크

    private static bool BakePanelPrefab(DeskSupportData data)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SuitBoldFontPath);
        if (font == null)
        {
            Debug.LogWarning("[DeskSupport] SUIT-Bold 폰트를 찾을 수 없습니다: " + SuitBoldFontPath);
        }

        GameObject root = new GameObject("DeskSupport", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

            DeskSupportScript script = root.AddComponent<DeskSupportScript>();
            script.data = data;
            script.uiFont = font;

            // 데모에서 SitSupport(x16)/캐릭터 패널(x410) 오른쪽 — 본편에서는 어차피 Dev 메뉴로 토글
            GameObject panel = CreateUIObject("Panel", root.transform, new Vector2(804f, -16f), new Vector2(380f, 320f));
            panel.AddComponent<Image>().color = PanelColor;
            script.panel = panel;
            script.panelRect = panel.GetComponent<RectTransform>();

            CreateText(panel.transform, "Title", "DeskSupport (책상 꾸미기)", 24f,
                new Vector2(14f, -10f), new Vector2(352f, 28f), font, Color.white, TextAlignmentOptions.Left);

            CreateText(panel.transform, "SkinHeader", "머티리얼 스킨", 20f,
                new Vector2(14f, -46f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
            GameObject skinContainer = CreateUIObject("SkinButtons", panel.transform, new Vector2(0f, -72f), new Vector2(380f, 40f));
            script.skinContainer = skinContainer.GetComponent<RectTransform>();

            CreateText(panel.transform, "DecoHeader", "장식 슬롯 (클릭 = 순환)", 20f,
                new Vector2(14f, -118f), new Vector2(352f, 22f), font, HeaderColor, TextAlignmentOptions.Left);
            GameObject decoContainer = CreateUIObject("DecoButtons", panel.transform, new Vector2(0f, -144f), new Vector2(380f, 130f));
            script.decoContainer = decoContainer.GetComponent<RectTransform>();

            script.saveButton = CreateButton(panel.transform, "SaveButton", "데이터 저장",
                new Vector2(14f, -272f), new Vector2(352f, 32f), font);

            panel.SetActive(false); // 기본 숨김 — 본편은 Dev 메뉴로, 데모는 씬 오버라이드로 켠다

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[DeskSupport] 프리팹 베이크 완료: " + PrefabPath);
        return true;
    }

    /// <summary>SampleScene 씬 루트에 DeskSupport 인스턴스 설치(멱등). Root260616.prefab 불변 — 씬 파일만 저장.</summary>
    public static bool InstallSampleScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[DeskSupport] 프리팹이 없습니다. 먼저 Build DeskSupport Prefab을 실행하세요: " + PrefabPath);
            return false;
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[DeskSupport] 사용자가 씬 저장을 취소해 설치를 중단합니다.");
            return false;
        }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        DeskSupportScript existing = null;
        foreach (DeskSupportScript s in Resources.FindObjectsOfTypeAll<DeskSupportScript>())
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
            instance.name = "DeskSupport";
            existing = instance.GetComponent<DeskSupportScript>();
            Debug.Log("[DeskSupport] SampleScene 씬 루트에 인스턴스 추가");
        }
        else
        {
            Debug.Log("[DeskSupport] SampleScene에 이미 설치되어 있어 재사용");
        }

        existing.gameObject.SetActive(true);
        if (existing.panel != null)
        {
            existing.panel.SetActive(false); // 본편 기본: 숨김 (Dev → DeskSupport로 열기)
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[DeskSupport] SampleScene 설치 완료 (씬 파일만 수정, Root260616.prefab 불변)");
        return true;
    }

    // ---------------------------------------------------------------- UI 헬퍼 (SitSupportBuilder와 동일 규약)

    private static Button CreateButton(Transform parent, string name, string label,
        Vector2 pos, Vector2 size, TMP_FontAsset font)
    {
        GameObject go = CreateUIObject(name, parent, pos, size);
        Image img = go.AddComponent<Image>();
        img.color = ButtonColor;
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        TMP_Text labelText = CreateText(go.transform, "Label", label, 20f, Vector2.zero, Vector2.zero,
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
}
#endif
