using UnityEditor;
using UnityEngine;

// 볼당기기 배선 도구 (Tools → MR → 볼당기기).
//
// 실제 배선은 런타임에 MRCheekPullBinder가 한다 — 캐릭터가 Instantiate로 생기기 때문이다.
// 이 도구가 하는 일은 두 가지다:
//   1) 씬에 바인더가 있는지 확인하고 없으면 붙인다
//   2) 캐릭터 프리팹의 볼 본 상태를 미리 찍어 본다 (콜라이더·레이어·태그)
//
// Kickoff Guide 7-1 F — 손으로 붙이지 않고 도구로 만드는 이유는 재현 가능해야 하기 때문이다.
public static class MRCheekPullSetup
{
    [MenuItem("Tools/MR/볼당기기 바인더 부착")]
    public static void AttachBinder()
    {
        MRCheekPullBinder existing = FindInScene<MRCheekPullBinder>();
        if (existing != null)
        {
            Debug.Log($"[MRCheekPullSetup] 이미 붙어 있다: '{GetPath(existing.transform)}'");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // 항상 살아 있는 오브젝트에 붙인다. 자주 꺼지는 곳에 붙이면
        // Update가 멈춰 캐릭터 교체를 놓친다 (Kickoff Guide 4-65).
        GameObject host = null;
        CharManager charManager = FindInScene<CharManager>();
        if (charManager != null)
        {
            host = charManager.gameObject;
        }

        if (host == null)
        {
            host = new GameObject("MRCheekPullBinder");
            Undo.RegisterCreatedObjectUndo(host, "MRCheekPullBinder 생성");
            Debug.LogWarning("[MRCheekPullSetup] CharManager를 못 찾아 새 오브젝트를 만들었다. " +
                             "항상 활성인 곳인지 확인할 것.");
        }

        Undo.AddComponent<MRCheekPullBinder>(host);
        EditorUtility.SetDirty(host);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
        Selection.activeGameObject = host;

        Debug.Log($"[MRCheekPullSetup] '{GetPath(host.transform)}'에 부착했다.\n" +
                  "**씬을 저장할 것(Ctrl+S)** — 저장하지 않으면 반영되지 않는다.");
    }

    [MenuItem("Tools/MR/볼당기기 상태 리포트")]
    public static void Report()
    {
        MRCheekPullBinder binder = FindInScene<MRCheekPullBinder>();
        string binderText = "없음 — Tools → MR → 볼당기기 바인더 부착 을 실행할 것";
        if (binder != null)
        {
            binderText = $"'{GetPath(binder.transform)}' (활성={binder.gameObject.activeInHierarchy}, enabled={binder.enabled})";
        }

        Debug.Log($"[MRCheekPullSetup] 리포트\n  MRCheekPullBinder : {binderText}");

        // 선택한 캐릭터(프리팹 또는 씬 인스턴스)의 볼 상태를 찍는다.
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            Debug.Log("[MRCheekPullSetup] 캐릭터 프리팹이나 인스턴스를 선택하고 다시 실행하면 볼 상태도 찍는다.");
            return;
        }

        CharAttributes attrs = target.GetComponent<CharAttributes>();
        string tagText = "(CharAttributes 없음)";
        if (attrs != null && attrs.featureTags != null)
        {
            tagText = string.Join(", ", attrs.featureTags);
        }

        string[] names = { "Character_Ball_L", "Character_Ball_R" };
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[MRCheekPullSetup] '{target.name}' 볼 상태 | featureTags=[{tagText}]");

        for (int i = 0; i < names.Length; i++)
        {
            Transform bone = FindDeep(target.transform, names[i]);
            if (bone == null)
            {
                sb.Append($"\n  {names[i]} : 없음");
                continue;
            }

            SphereCollider col = bone.GetComponent<SphereCollider>();
            string colText = "콜라이더 없음";
            if (col != null)
            {
                Vector3 ls = bone.lossyScale;
                float world = col.radius * Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
                colText = $"radius={col.radius} → 월드 {world:F4}m (지름 {world * 200f:F1}cm)";
            }

            Transform proxy = bone.Find("CheekPullTarget");
            string proxyText = "없음(런타임에 생성됨)";
            if (proxy != null)
            {
                proxyText = "있음";
            }

            sb.Append($"\n  {names[i]} : layer={LayerMask.LayerToName(bone.gameObject.layer)}({bone.gameObject.layer}) | " +
                      $"{colText} | 프록시={proxyText}");
        }

        // 정적 스케일은 중첩 프리팹이면 미해결이라 Play 중 값이 유일한 진실이다 (7-1 B).
        sb.Append("\n  주의: 프리팹 상태의 lossyScale은 루트 스케일이 빠져 부정확할 수 있다. " +
                  "Play 중 [MRCheekPull/실측] 로그를 볼 것.");
        Debug.Log(sb.ToString());
    }

    private static T FindInScene<T>() where T : Component
    {
        T[] found = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject != null && found[i].gameObject.scene.IsValid())
            {
                return found[i];
            }
        }
        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        int depth = 0;
        while (p != null && depth < 4)
        {
            path = p.name + "/" + path;
            p = p.parent;
            depth++;
        }
        return path;
    }
}
