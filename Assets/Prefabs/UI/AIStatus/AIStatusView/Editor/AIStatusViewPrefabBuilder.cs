#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AIStatusView 프리팹을 코드(AIStatusView.EditorBuild)로부터 "구워서" 생성한다.
/// 런타임 생성 로직을 재사용하므로 손으로 YAML을 쓸 때 생기는 fileID/GUID 깨짐이 없다.
///
/// 사용: Unity 메뉴 → Tools/AIStatus/Build AIStatus Prefab
/// </summary>
public static class AIStatusViewPrefabBuilder
{
    private const string PrefabDir = "Assets/Prefabs/UI/AIStatus/AIStatusView/Prefabs";
    private const string PrefabPath = PrefabDir + "/AIStatusView.prefab";

    [MenuItem("Tools/AIStatus/Build AIStatus Prefab")]
    public static void BuildPrefab()
    {
        if (!Directory.Exists(PrefabDir))
        {
            Directory.CreateDirectory(PrefabDir);
        }

        GameObject root = new GameObject("AIStatusView", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5; // UI

        try
        {
            AIStatusView view = root.AddComponent<AIStatusView>();
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            view.EditorBuild(uiSprite, font);

            // 서버 통신/데이터 공급 컴포넌트도 함께 부착([RequireComponent(AIStatusView)] 충족).
            root.AddComponent<AIStatusClient>();

            bool ok;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out ok);
            Debug.Log(ok
                ? "[AIStatus][PrefabBuilder] AIStatusView 프리팹을 구웠습니다: " + PrefabPath
                : "[AIStatus][PrefabBuilder] 프리팹 저장 실패: " + PrefabPath);
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
