#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PolicyView 프리팹의 모든 TMP_Text 폰트를 SUIT-Bold로 교체한다 (베이크 후 필수 마지막 단계).
/// 아울러 SUIT-Bold의 폴백에 NotoSansJP를 보장한다(전역 1회, 멱등) — jp 약관 표시용.
///
/// 사용: Unity 메뉴 → Tools/Policy/2. Apply SUIT-Bold Font
/// </summary>
public static class PolicyFontApply
{
    private const string PrefabPath = "Assets/Prefabs/UI/Policy/PolicyView/Prefabs/PolicyView.prefab";
    private const string FontPath = "Assets/FontAssets/SUIT-Bold.asset";
    private const string JpFallbackPath = "Assets/FontAssets/NotoSansJP-Regular SDF.asset";

    [MenuItem("Tools/Policy/2. Apply SUIT-Bold Font")]
    public static void ApplySuitBold()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError("[Policy][Font] SUIT-Bold 폰트를 찾을 수 없습니다: " + FontPath);
            return;
        }

        EnsureJpFallback(font);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError("[Policy][Font] 프리팹을 찾을 수 없습니다: " + PrefabPath);
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
        Debug.Log($"[Policy][Font] SUIT-Bold 적용 완료: {PrefabPath}, TMP_Text {changed}개");
    }

    // SUIT-Bold 아틀라스에는 일본어 글리프가 없다. 폴백에 NotoSansJP를 추가해 jp 문서를 표시한다.
    private static void EnsureJpFallback(TMP_FontAsset suit)
    {
        TMP_FontAsset jp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JpFallbackPath);
        if (jp == null)
        {
            Debug.LogWarning("[Policy][Font] NotoSansJP 폴백 폰트를 찾을 수 없습니다: " + JpFallbackPath);
            return;
        }
        if (suit.fallbackFontAssetTable == null)
        {
            suit.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }
        if (!suit.fallbackFontAssetTable.Contains(jp))
        {
            suit.fallbackFontAssetTable.Add(jp);
            EditorUtility.SetDirty(suit);
            AssetDatabase.SaveAssets();
            Debug.Log("[Policy][Font] SUIT-Bold 폴백에 NotoSansJP를 추가했습니다.");
        }
    }
}
#endif
