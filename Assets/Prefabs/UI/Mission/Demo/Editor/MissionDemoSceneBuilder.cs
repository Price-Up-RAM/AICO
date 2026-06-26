#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 미션 데모 씬을 코드로 생성한다. Camera + EventSystem + Canvas + MissionView 프리팹 +
/// MissionDemo(시작 시 패널 열고 샘플 진행도 주입)를 배치하고 저장한다.
/// 사용: Unity 메뉴 → Tools/Mission/Build Demo Scene
/// </summary>
public static class MissionDemoSceneBuilder
{
    private const string MainPrefab = "Assets/Prefabs/UI/Mission/MissionView/Prefabs/MissionView.prefab";
    private const string SceneDir = "Assets/Prefabs/UI/Mission/Demo";
    private const string ScenePath = SceneDir + "/MissionDemo.unity";

    [MenuItem("Tools/Mission/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        EnsureDir();

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라 + 오디오 리스너(도장 효과음용)
        GameObject cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        Camera camera = cam.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        cam.transform.position = new Vector3(0f, 0f, -10f);

        // 이벤트 시스템 (UI 입력)
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // 캔버스
        GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 미션 프리팹 인스턴스
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefab);
        if (prefab == null)
        {
            Debug.LogError("[Mission] 메인 프리팹을 찾을 수 없습니다: " + MainPrefab +
                " (먼저 Tools/Mission/Build Mission Prefab 실행)");
            return;
        }

        GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // 데모 컨트롤러
        GameObject demoGO = new GameObject("MissionDemo");
        MissionDemo demo = demoGO.AddComponent<MissionDemo>();
        demo.EditorSet(panel.GetComponent<MissionView>(), "ko");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Mission] 데모 씬 생성 완료: " + ScenePath);

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
