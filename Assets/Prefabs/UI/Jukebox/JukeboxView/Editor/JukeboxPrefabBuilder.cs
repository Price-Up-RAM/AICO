#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Jukebox 프리팹 베이커. 프리팹은 2개뿐이다.
///  1) JukeboxEnvironmentView (SFX 팝업, 행을 내부에서 인라인 빌드)
///  2) JukeboxView (메인, BGM 드롭다운 + 마스터 볼륨 + 환경음 버튼; 환경음 프리팹 참조)
///
/// 사용: Unity 메뉴 → Tools/Jukebox/Build Jukebox Prefabs
/// </summary>
public static class JukeboxPrefabBuilder
{
    private const string Dir = "Assets/Prefabs/UI/Jukebox/JukeboxView/Prefabs";
    private const string EnvPath = Dir + "/JukeboxEnvironmentView.prefab";
    private const string MainPath = Dir + "/JukeboxView.prefab";
    private const string FontGuid = "8f586378b4e144a9851e7b34d9b748ee";

    [MenuItem("Tools/Jukebox/Build Jukebox Prefabs")]
    public static void BuildPrefabs()
    {
        EnsureDir();
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        TMP_FontAsset font = LoadFont();

        // 1) 환경음(SFX) 프리팹
        GameObject envGO = new GameObject("JukeboxEnvironmentView", typeof(RectTransform), typeof(CanvasGroup));
        envGO.layer = 5;
        try
        {
            envGO.AddComponent<JukeboxEnvironmentView>().EditorBuild(uiSprite, font);
            PrefabUtility.SaveAsPrefabAsset(envGO, EnvPath, out bool ok);
            Debug.Log(ok ? "[Jukebox] 환경음 프리팹 구움: " + EnvPath : "[Jukebox] 환경음 프리팹 저장 실패");
        }
        finally { Object.DestroyImmediate(envGO); }
        GameObject envAsset = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPath);

        // 2) 메인 프리팹
        GameObject mainGO = new GameObject("JukeboxView", typeof(RectTransform), typeof(CanvasGroup));
        mainGO.layer = 5;
        try
        {
            JukeboxView view = mainGO.AddComponent<JukeboxView>();
            view.EditorBuild(uiSprite, font, knob);
            view.EditorSetEnvironmentPrefab(envAsset);
            PrefabUtility.SaveAsPrefabAsset(mainGO, MainPath, out bool ok);
            Debug.Log(ok ? "[Jukebox] 메인 프리팹 구움: " + MainPath : "[Jukebox] 메인 프리팹 저장 실패");
        }
        finally { Object.DestroyImmediate(mainGO); }

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
