#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillView 프리팹의 뷰 전환 버튼 아이콘(listIcon/gridIcon)을 Flat GUI의 List/Grid 스프라이트로 지정한다.
/// SerializedObject로 private [SerializeField] 필드에 값만 넣는다(계층/레이아웃 불변).
///
/// 사용: Unity 메뉴 → Tools/Skills/Assign View Icons
/// </summary>
public static class SkillViewIconApply
{
    private const string PrefabPath = "Assets/Prefabs/UI/Skills/SkillView/Prefabs/SkillView.prefab";
    private const string ListIconPath = "Assets/Devion Games/Flat GUI/Icons/List.png";
    private const string GridIconPath = "Assets/Devion Games/Flat GUI/Icons/Grid.png";

    [MenuItem("Tools/Skills/Assign View Icons")]
    public static void AssignViewIcons()
    {
        Sprite listSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ListIconPath);
        Sprite gridSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GridIconPath);
        if (listSprite == null || gridSprite == null)
        {
            Debug.LogError($"[Skills][Icons] 스프라이트를 찾을 수 없습니다: {ListIconPath} / {GridIconPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError("[Skills][Icons] 프리팹을 찾을 수 없습니다: " + PrefabPath);
            return;
        }

        try
        {
            SkillView view = root.GetComponent<SkillView>();
            if (view == null)
            {
                Debug.LogError("[Skills][Icons] SkillView 컴포넌트를 찾을 수 없습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(view);
            so.FindProperty("listIcon").objectReferenceValue = listSprite;
            so.FindProperty("gridIcon").objectReferenceValue = gridSprite;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Skills][Icons] 뷰 전환 아이콘 지정 완료 (listIcon=List.png, gridIcon=Grid.png)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
