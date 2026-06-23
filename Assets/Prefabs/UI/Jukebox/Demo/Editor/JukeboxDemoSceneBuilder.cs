#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 주크박스 데모 씬을 코드로 생성한다. Canvas + EventSystem + JukeboxView 프리팹 +
/// JukeboxDemo(시작 시 고정 트랙 자동 재생)를 배치하고 저장한다.
/// 사용: Unity 메뉴 → Tools/Jukebox/Build Demo Scene
/// </summary>
public static class JukeboxDemoSceneBuilder
{
    private const string MainPrefab = "Assets/Prefabs/UI/Jukebox/JukeboxView/Prefabs/JukeboxView.prefab";
    private const string SceneDir = "Assets/Prefabs/UI/Jukebox/Demo";
    private const string ScenePath = SceneDir + "/JukeboxDemo.unity";
    private const string DemoTrackId = "Lofi1";
    private const string AudioDir = "Assets/Audios/BGM";

    [MenuItem("Tools/Jukebox/Build Demo Scene")]
    public static void BuildDemoScene()
    {
        EnsureDir();

        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라 + 오디오 리스너
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

        // 주크박스 프리팹 인스턴스
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefab);
        if (prefab == null)
        {
            Debug.LogError("[Jukebox] 메인 프리팹을 찾을 수 없습니다: " + MainPrefab);
            return;
        }
        GameObject jb = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGO.transform);
        RectTransform rt = jb.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        // MRJukebox (BGM 재생 주체) — 실제 Audios/BGM 클립을 이름/태그로 매핑해 등록.
        GameObject mrGo = new GameObject("MRJukebox", typeof(AudioSource), typeof(MRJukebox));
        MRJukebox mr = mrGo.GetComponent<MRJukebox>();
        // 장르 단일 태그(카테고리). 카테고리끼리 섞이지 않게 함.
        AddClip(mr, "campfire.wav", "campfire", "campfire");
        AddClip(mr, "Lofi1.mp3", "Lofi1", "lofi");
        AddClip(mr, "Lofi2.mp3", "Lofi2", "lofi");
        AddClip(mr, "rain.wav", "rain", "rain");
        AddClip(mr, "rain_heavy.wav", "rain_heavy", "rain");

        // 데모 컨트롤러
        GameObject demoGO = new GameObject("JukeboxDemo");
        JukeboxDemo demo = demoGO.AddComponent<JukeboxDemo>();
        demo.EditorSet(jb.GetComponent<JukeboxView>(), DemoTrackId);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Jukebox] 데모 씬 생성 완료: " + ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void AddClip(MRJukebox mr, string fileName, string trackName, params string[] tags)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "/" + fileName);
        if (clip != null)
        {
            mr.AddTrack(clip, trackName, tags);
        }
        else
        {
            Debug.LogWarning("[Jukebox] 데모 클립 없음: " + AudioDir + "/" + fileName);
        }
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
