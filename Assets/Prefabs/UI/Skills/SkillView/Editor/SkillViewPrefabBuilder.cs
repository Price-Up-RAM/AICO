#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillView 프리팹을 코드(SkillView.Build)로부터 "구워서" 생성한다.
/// 런타임 생성 로직을 그대로 재사용하므로 손으로 YAML을 쓸 때 생기는
/// fileID/GUID 깨짐 위험이 없다. 메뉴 한 번이면 프리팹에 전체 UI가 들어간다.
///
/// 사용: Unity 메뉴 → Tools/Skills/Build SkillView Prefab
/// </summary>
public static class SkillViewPrefabBuilder
{
    private const string PrefabPath =
        "Assets/Prefabs/UI/Skills/SkillView/Prefabs/SkillView.prefab";

    [MenuItem("Tools/Skills/Build SkillView Prefab")]
    public static void BuildPrefab()
    {
        GameObject root = new GameObject("SkillView", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5; // UI

        try
        {
            SkillView view = root.AddComponent<SkillView>();
            // 빌트인 UISprite(둥근 9-slice). 에디터에서만 받을 수 있고, 프리팹에 참조가 직렬화된다.
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            view.EditorBuild(uiSprite);

            bool ok;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out ok);
            Debug.Log(ok
                ? "[Skills][PrefabBuilder] SkillView 프리팹을 구웠습니다: " + PrefabPath
                : "[Skills][PrefabBuilder] 프리팹 저장 실패: " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
