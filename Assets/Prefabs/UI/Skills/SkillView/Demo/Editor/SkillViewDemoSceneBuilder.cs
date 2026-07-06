#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SkillView 데모 씬을 코드로 생성한다. Camera + EventSystem + Canvas + SkillView 프리팹 +
/// SkillViewDemo를 배치하고 저장한다.
///
/// 프리팹은 재베이크하지 않고 커밋본을 그대로 인스턴스화한다(런타임 BindExisting이 스위치 토글·
/// 목록 버튼·리스트 오버레이를 주입). 패널 크기는 프리팹 원본을 유지한다.
///
/// 사용: Unity 메뉴 → Tools/Skills/Build Demo Scene
/// </summary>
public static class SkillViewDemoSceneBuilder
{
    private const string MainPrefab = "Assets/Prefabs/UI/Skills/SkillView/Prefabs/SkillView.prefab";
    private const string SceneDir = "Assets/Prefabs/UI/Skills/SkillView/Demo";
    private const string ScenePath = SceneDir + "/SkillViewDemo.unity";

    [MenuItem("Tools/Skills/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        EnsureDir();

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        Camera camera = cam.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        cam.transform.position = new Vector3(0f, 0f, -10f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefab);
        if (prefab == null)
        {
            Debug.LogError("[Skills] 메인 프리팹을 찾을 수 없습니다: " + MainPrefab);
            return;
        }

        GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
        RectTransform rt = panel.GetComponent<RectTransform>();
        // 프리팹 원본 크기를 유지하고 중앙 배치(스트레치 금지 → 행 늘어남 방지).
        Vector2 size = rt.sizeDelta;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        GameObject demoGO = new GameObject("SkillViewDemo");
        SkillViewDemo demo = demoGO.AddComponent<SkillViewDemo>();
        demo.EditorSet(panel.GetComponent<SkillView>());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Skills] 데모 씬 생성 완료: " + ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureDir()
    {
        string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, SceneDir);
        if (!Directory.Exists(abs))
        {
            Directory.CreateDirectory(abs);
            AssetDatabase.Refresh();
        }
    }
}
#endif
