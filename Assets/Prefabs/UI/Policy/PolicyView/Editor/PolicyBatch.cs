#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Policy 베이크 일괄 실행 (프리팹 빌드 → SUIT-Bold 폰트 적용 → 데모 씬 생성).
/// batchmode에서 -executeMethod PolicyBatch.BuildAll 로 호출하거나 메뉴로 실행한다.
///
/// 사용: Unity 메뉴 → Tools/Policy/Build All (Prefab+Font+Demo)
/// </summary>
public static class PolicyBatch
{
    [MenuItem("Tools/Policy/Build All (Prefab+Font+Demo)")]
    public static void BuildAll()
    {
        Debug.Log("[Policy][Batch] BuildAll 시작");
        PolicyViewPrefabBuilder.BuildPrefab();
        PolicyFontApply.ApplySuitBold();
        PolicyViewDemoSceneBuilder.BuildDemoScene();
        Debug.Log("[Policy][Batch] BuildAll 완료");
    }
}
#endif
