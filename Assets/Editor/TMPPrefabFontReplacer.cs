using TMPro;
using UnityEditor;
using UnityEngine;

public class TMPPrefabFontReplacer : EditorWindow
{
    private TMP_FontAsset targetFont;
    private GameObject targetPrefab;
    private bool includeInactive = true;

    [MenuItem("Tools/TMP/Replace Font In Prefab")]
    public static void ShowWindow()
    {
        GetWindow<TMPPrefabFontReplacer>("TMP Prefab Font Replacer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Replace TMP Font In Prefab", EditorStyles.boldLabel);

        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "Target Font Asset",
            targetFont,
            typeof(TMP_FontAsset),
            false
        );

        targetPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Target Prefab",
            targetPrefab,
            typeof(GameObject),
            false
        );

        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        EditorGUILayout.Space();

        if (GUILayout.Button("Replace Font In Prefab"))
        {
            ReplaceFontInPrefab();
        }
    }

    private void ReplaceFontInPrefab()
    {
        if (targetFont == null)
        {
            Debug.LogError("Target Font Asset이 비어 있습니다.");
            return;
        }

        if (targetPrefab == null)
        {
            Debug.LogError("Target Prefab이 비어 있습니다.");
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(targetPrefab);

        if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab"))
        {
            Debug.LogError("Target Prefab에는 Project 창의 Prefab Asset을 넣어야 합니다.");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        int changedCount = 0;

        try
        {
            TMP_Text[] texts = prefabRoot.GetComponentsInChildren<TMP_Text>(includeInactive);

            foreach (TMP_Text text in texts)
            {
                text.font = targetFont;
                EditorUtility.SetDirty(text);
                changedCount++;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Prefab 폰트 교체 완료: {prefabPath}, TMP_Text {changedCount}개");
    }
}