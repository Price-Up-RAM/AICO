#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// PolicyView 데모 씬을 코드로 생성한다. Camera + EventSystem + Canvas + PolicyView 프리팹 +
/// PolicyViewDemo를 배치하고 저장한다.
///
/// 프리팹은 재베이크하지 않고 커밋본을 그대로 인스턴스화한다(런타임 BindExisting이 탭/본문을 연결).
///
/// 사용: Unity 메뉴 → Tools/Policy/3. Build Demo Scene
/// </summary>
public static class PolicyViewDemoSceneBuilder
{
    private const string MainPrefab = "Assets/Prefabs/UI/Policy/PolicyView/Prefabs/PolicyView.prefab";
    private const string SceneDir = "Assets/Prefabs/UI/Policy/PolicyView/Demo";
    private const string ScenePath = SceneDir + "/PolicyViewDemo.unity";

    [MenuItem("Tools/Policy/3. Build Demo Scene")]
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
            Debug.LogError("[Policy] 메인 프리팹을 찾을 수 없습니다: " + MainPrefab);
            return;
        }

        GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
        RectTransform rt = panel.GetComponent<RectTransform>();
        // 프리팹 원본 크기를 유지하고 중앙 배치.
        Vector2 size = rt.sizeDelta;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        GameObject demoGO = new GameObject("PolicyViewDemo");
        PolicyViewDemo demo = demoGO.AddComponent<PolicyViewDemo>();
        demo.EditorSet(panel.GetComponent<PolicyView>());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Policy] 데모 씬 생성 완료: " + ScenePath);

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
