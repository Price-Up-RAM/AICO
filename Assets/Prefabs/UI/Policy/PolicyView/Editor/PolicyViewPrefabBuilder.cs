#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PolicyView 프리팹을 코드(PolicyView.EditorBuild)로부터 "구워서" 생성한다.
/// Documents 폴더의 언어별 TextAsset(ko/jp/en)을 문서 목록에 연결해 프리팹에 직렬화한다
/// (약관 전문이 프리팹 참조로 번들됨).
///
/// 사용: Unity 메뉴 → Tools/Policy/1. Build PolicyView Prefab
/// </summary>
public static class PolicyViewPrefabBuilder
{
    private const string PrefabDir = "Assets/Prefabs/UI/Policy/PolicyView/Prefabs";
    private const string PrefabPath = PrefabDir + "/PolicyView.prefab";
    private const string DocumentDir = "Assets/Prefabs/UI/Policy/PolicyView/Documents";

    // 탭 표시 순서
    private static readonly string[] DocumentKeys =
    {
        "terms_of_service",
        "privacy_policy",
        "ai_notice",
        "acceptable_use_policy",
    };

    [MenuItem("Tools/Policy/1. Build PolicyView Prefab")]
    public static void BuildPrefab()
    {
        EnsureDir();

        List<PolicyView.PolicyDocument> docs = LoadDocuments();
        if (docs.Count == 0)
        {
            Debug.LogError("[Policy][PrefabBuilder] 문서 TextAsset을 하나도 찾지 못했습니다: " + DocumentDir);
            return;
        }

        GameObject root = new GameObject("Policy", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5; // UI

        try
        {
            PolicyView view = root.AddComponent<PolicyView>();
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            view.EditorBuild(uiSprite, docs);

            bool ok;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out ok);
            Debug.Log(ok
                ? "[Policy][PrefabBuilder] PolicyView 프리팹을 구웠습니다: " + PrefabPath
                : "[Policy][PrefabBuilder] 프리팹 저장 실패: " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static List<PolicyView.PolicyDocument> LoadDocuments()
    {
        List<PolicyView.PolicyDocument> docs = new List<PolicyView.PolicyDocument>();
        foreach (string key in DocumentKeys)
        {
            TextAsset ko = AssetDatabase.LoadAssetAtPath<TextAsset>($"{DocumentDir}/{key}_ko.txt");
            TextAsset jp = AssetDatabase.LoadAssetAtPath<TextAsset>($"{DocumentDir}/{key}_jp.txt");
            TextAsset en = AssetDatabase.LoadAssetAtPath<TextAsset>($"{DocumentDir}/{key}_en.txt");

            if (ko == null)
            {
                Debug.LogError($"[Policy][PrefabBuilder] 한국어 원문이 없습니다: {DocumentDir}/{key}_ko.txt (이 문서는 건너뜀)");
                continue;
            }
            if (jp == null || en == null)
            {
                Debug.LogWarning($"[Policy][PrefabBuilder] {key}의 번역본 누락(jp={(jp != null)}, en={(en != null)}). 해당 언어는 ko로 폴백됩니다.");
            }

            docs.Add(new PolicyView.PolicyDocument { key = key, ko = ko, jp = jp, en = en });
        }
        return docs;
    }

    private static void EnsureDir()
    {
        string abs = Path.Combine(Directory.GetParent(Application.dataPath).FullName, PrefabDir);
        if (!Directory.Exists(abs))
        {
            Directory.CreateDirectory(abs);
            AssetDatabase.Refresh();
        }
    }
}
#endif
