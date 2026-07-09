#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JukeboxDownloader 프리팹 베이커 (JukeboxPrefabBuilder와 동일 방법론).
/// View.EditorBuild()로 UI 계층을 코드로 굽고, 런타임 서버 연동용 Client를 함께 부착한다.
///
/// 사용: Unity 메뉴 → Tools/JukeboxDownloader/Build Prefab
/// </summary>
public static class JukeboxDownloaderPrefabBuilder
{
    private const string Dir = "Assets/Prefabs/UI/JukeboxDownloader/Prefabs";
    private const string MainPath = Dir + "/JukeboxDownloader.prefab";
    private const string FontGuid = "e81f347d6f6c9b047a48e60b200fcd3d"; // Assets/FontAssets/SUIT-Bold.asset

    [MenuItem("Tools/JukeboxDownloader/Build Prefab")]
    public static void BuildPrefab()
    {
        EnsureDir();
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        TMP_FontAsset font = LoadFont();

        GameObject go = new GameObject("JukeboxDownloader", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = 5; // UI
        try
        {
            JukeboxDownloaderView view = go.AddComponent<JukeboxDownloaderView>();
            view.EditorBuild(uiSprite, font);
            go.AddComponent<JukeboxDownloaderClient>(); // 서버 연동 (RequireComponent로 View 보장)

            PrefabUtility.SaveAsPrefabAsset(go, MainPath, out bool ok);
            Debug.Log(ok ? "[JukeboxDownloader] 프리팹 구움: " + MainPath : "[JukeboxDownloader] 프리팹 저장 실패");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureDir()
    {
        string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Dir);
        if (!Directory.Exists(abs))
        {
            Directory.CreateDirectory(abs);
            AssetDatabase.Refresh();
        }
    }

    private static TMP_FontAsset LoadFont()
    {
        string path = AssetDatabase.GUIDToAssetPath(FontGuid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }
}
#endif
