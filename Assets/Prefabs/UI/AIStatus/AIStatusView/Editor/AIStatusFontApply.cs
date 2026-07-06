#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AIStatusView 프리팹의 모든 TMP_Text 폰트를 SUIT-Bold로 교체한다.
/// (TMPPrefabFontReplacer와 동일 로직: LoadPrefabContents → font 교체 → SaveAsPrefabAsset)
/// 프리팹 베이크가 끝난 뒤 마지막에 실행한다.
///
/// 사용: Unity 메뉴 → Tools/AIStatus/Apply SUIT-Bold Font
/// </summary>
public static class AIStatusFontApply
{
    private const string PrefabPath = "Assets/Prefabs/UI/AIStatus/AIStatusView/Prefabs/AIStatusView.prefab";
    private const string FontPath = "Assets/FontAssets/SUIT-Bold.asset";

    [MenuItem("Tools/AIStatus/Apply SUIT-Bold Font")]
    public static void ApplySuitBold()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError("[AIStatus][Font] SUIT-Bold 폰트를 찾을 수 없습니다: " + FontPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError("[AIStatus][Font] 프리팹을 찾을 수 없습니다: " + PrefabPath + " (먼저 Build AIStatus Prefab)");
            return;
        }

        int changed = 0;
        try
        {
            // 비활성(템플릿 포함) 전체 TMP_Text 교체
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
        Debug.Log($"[AIStatus][Font] SUIT-Bold 적용 완료: {PrefabPath}, TMP_Text {changed}개");
    }
}
#endif
