#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JukeboxDownloader 프리팹의 모든 TMP_Text 폰트를 SUIT-Bold로 교체한다.
/// LoadPrefabContents → font 교체 → SaveAsPrefabAsset. 계층/앵커는 건드리지 않고 폰트 참조만 바꾼다.
///
/// 사용: Unity 메뉴 → Tools/JukeboxDownloader/Apply SUIT-Bold Font
/// </summary>
public static class JukeboxDownloaderFontApply
{
    private const string PrefabPath = "Assets/Prefabs/UI/JukeboxDownloader/Prefabs/JukeboxDownloader.prefab";
    private const string FontPath = "Assets/FontAssets/SUIT-Bold.asset";

    [MenuItem("Tools/JukeboxDownloader/Apply SUIT-Bold Font")]
    public static void ApplySuitBold()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError("[JukeboxDownloader][Font] SUIT-Bold 폰트를 찾을 수 없습니다: " + FontPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError("[JukeboxDownloader][Font] 프리팹을 찾을 수 없습니다: " + PrefabPath);
            return;
        }

        int changed = 0;
        try
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                text.font = font;
                EditorUtility.SetDirty(text);
                changed++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[JukeboxDownloader][Font] SUIT-Bold 적용 완료: {PrefabPath}, TMP_Text {changed}개");
    }

    /// <summary>batchmode 마무리용: 프리팹 재베이크 + SUIT-Bold 적용을 한 번에.</summary>
    public static void BuildAndApplyFont()
    {
        JukeboxDownloaderPrefabBuilder.BuildPrefab();
        ApplySuitBold();
    }
}
#endif
