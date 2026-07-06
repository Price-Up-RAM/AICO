#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// AIStatus 데모 씬을 코드로 생성한다. Camera + EventSystem + Canvas + AIStatusView 프리팹 +
/// AIStatusDemo(시작 시 패널 열고 샘플/실데이터 표시)를 배치하고 저장한다.
/// 씬에 ServerManager는 두지 않는다 — 대신 AIStatusClient의 fallbackBaseUrl(127.0.0.1:5000)로
/// 직접 /status를 호출하므로, 로컬 파이썬 서버가 떠 있으면 실제 통신이 검증된다.
///
/// 사용: Unity 메뉴 → Tools/AIStatus/Build Demo Scene (먼저 Build AIStatus Prefab 실행 필요)
/// </summary>
public static class AIStatusDemoSceneBuilder
{
    private const string MainPrefab = "Assets/Prefabs/UI/AIStatus/AIStatusView/Prefabs/AIStatusView.prefab";
    private const string SceneDir = "Assets/Prefabs/UI/AIStatus/Demo";
    private const string ScenePath = SceneDir + "/AIStatusDemo.unity";
    private const string FallbackBaseUrl = "http://127.0.0.1:5000";

    [MenuItem("Tools/AIStatus/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        EnsureDir();

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라
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

        // AIStatus 프리팹 인스턴스
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefab);
        if (prefab == null)
        {
            Debug.LogError("[AIStatus] 메인 프리팹을 찾을 수 없습니다: " + MainPrefab +
                " (먼저 Tools/AIStatus/Build AIStatus Prefab 실행)");
            return;
        }

        GameObject panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // 데모 씬에서는 ServerManager가 없으므로, 직접 호출용 fallback 주소를 주입.
        // 실패 시엔 샘플을 유지(showOfflineOnFailure=false)해 UI 모양이 사라지지 않게 한다.
        AIStatusClient client = panel.GetComponent<AIStatusClient>();
        if (client != null)
        {
            client.EditorConfigure(FallbackBaseUrl, false);
        }

        // 데모 컨트롤러
        GameObject demoGO = new GameObject("AIStatusDemo");
        AIStatusDemo demo = demoGO.AddComponent<AIStatusDemo>();
        demo.EditorSet(panel.GetComponent<AIStatusView>(), false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[AIStatus] 데모 씬 생성 완료: " + ScenePath);

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
