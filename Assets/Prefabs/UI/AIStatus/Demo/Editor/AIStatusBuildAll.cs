#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// 편의 메뉴: 프리팹 베이크 → 데모 씬 빌드를 한 번에 실행한다.
/// (batchmode -executeMethod AIStatusBuildAll.BuildAll 로도 사용)
/// </summary>
public static class AIStatusBuildAll
{
    [MenuItem("Tools/AIStatus/Build All (Prefab + Demo)")]
    public static void BuildAll()
    {
        AIStatusViewPrefabBuilder.BuildPrefab();
        AIStatusFontApply.ApplySuitBold();   // 프리팹 완성 후 SUIT-Bold 적용
        AIStatusDemoSceneBuilder.BuildDemoScene();
    }
}
#endif
