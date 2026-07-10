#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// EquipSystem 워크벤치 씬(EquipDemo.unity)을 코드로 생성한다.
// 카메라(궤도 컨트롤) + 라이트 + EquipManager + 워크벤치(컨트롤러/마커) + 바닥 + 드롭 안내 GO 구성.
// 사용: Unity 메뉴 → Tools/EquipSystem/Build Workbench Scene (EquipDemo)
// batchmode 호환: -executeMethod EquipWorkbenchSceneBuilder.BuildScene
public static class EquipWorkbenchSceneBuilder
{
    private const string ScenePath = "Assets/Prefabs/Assist/EquipSystem/EquipDemo.unity";

    [MenuItem("Tools/EquipSystem/Build Workbench Scene (EquipDemo)")]
    public static void BuildScene()
    {
        UnityEngine.SceneManagement.Scene scene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 메인 카메라 — 워크벤치 궤도/줌/팬 컨트롤 + 오디오 리스너
        GameObject camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(EquipWorkbenchCamera));
        camGO.tag = "MainCamera";
        Camera cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.09f, 0.10f, 0.13f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 5000f;
        camGO.transform.position = new Vector3(0f, 1.4f, -3.5f);
        camGO.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

        // 디렉셔널 라이트
        GameObject lightGO = new GameObject("Directional Light", typeof(Light));
        Light light = lightGO.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f, 1f);
        light.intensity = 1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 장착 매니저 (카탈로그는 Resources/EquipCatalog에서 자동 로드)
        new GameObject("EquipManager", typeof(EquipManager));

        // 워크벤치 — 컨트롤러(IMGUI 패널/로스터/장착) + 마커(소켓/부착점 화면 표시)
        new GameObject("EquipWorkbench", typeof(EquipWorkbenchController), typeof(EquipWorkbenchMarkers));

        // 바닥 Plane — 캐릭터를 세워 놓을 작업대 바닥
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(2f, 1f, 2f);

        // 드롭 안내용 빈 GO — 하이어라키에서 위치 안내 역할만 한다
        GameObject guide = new GameObject("여기에 캐릭터 프리팹을 끌어다 놓으세요");
        guide.transform.position = Vector3.zero;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[EquipWorkbench] 워크벤치 씬 생성 완료: " + ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
