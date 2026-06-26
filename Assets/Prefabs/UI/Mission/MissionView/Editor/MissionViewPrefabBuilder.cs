#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MissionView 프리팹을 코드(MissionView.EditorBuild)로부터 "구워서" 생성한다.
/// 런타임 생성 로직을 재사용하므로 손으로 YAML을 쓸 때 생기는 fileID/GUID 깨짐이 없다.
///
/// 사용: Unity 메뉴 → Tools/Mission/Build Mission Prefab
/// </summary>
public static class MissionViewPrefabBuilder
{
    private const string PrefabDir = "Assets/Prefabs/UI/Mission/MissionView/Prefabs";
    private const string PrefabPath = PrefabDir + "/MissionView.prefab";

    [MenuItem("Tools/Mission/Build Mission Prefab")]
    public static void BuildPrefab()
    {
        if (!Directory.Exists(PrefabDir))
        {
            Directory.CreateDirectory(PrefabDir);
        }

        GameObject root = new GameObject("MissionView", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5; // UI

        try
        {
            MissionView view = root.AddComponent<MissionView>();
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            view.EditorBuild(uiSprite, font);

            bool ok;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out ok);
            Debug.Log(ok
                ? "[Mission][PrefabBuilder] MissionView 프리팹을 구웠습니다: " + PrefabPath
                : "[Mission][PrefabBuilder] 프리팹 저장 실패: " + PrefabPath);
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
