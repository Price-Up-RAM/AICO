using UnityEngine;
using UnityEditor;

// MR 씬에 MRTMPVirtualKeyboardBinder를 부착한다 (Phase 5).
//
// 왜 필요한가: 이 컴포넌트가 TMP_InputField.onSelect에 후킹해 TouchScreenKeyboard.Open()을
// 호출하는 유일한 코드인데, MR 씬에 부착돼 있지 않았다 (2026-08-25 GUID 실측: 0개).
// 그래서 Quest 시스템 키보드가 뜰 수가 없었다.
//
// 손으로 붙이지 않고 스크립트로 만든 이유는 재현 가능해야 하기 때문이다
// (Kickoff Guide 7-1 F — 결정이 가장 잘 새는 곳이 씬이다).
public static class MRKeyboardBinderSetup
{
    [MenuItem("Tools/MR/키보드 바인더 부착")]
    public static void AttachBinder()
    {
        MRTMPVirtualKeyboardBinder existing = FindBinderInScene();
        if (existing != null)
        {
            Debug.Log($"[MRKeyboardBinderSetup] 이미 부착돼 있다: '{GetPath(existing.transform)}'");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // MRTextInputGuard가 붙은 오브젝트(말풍선)를 1순위로 쓴다 — 입력창과 가장 가깝다.
        GameObject host = null;
        MRTextInputGuard guard = FindGuardInScene();
        if (guard != null)
        {
            host = guard.gameObject;
            Debug.Log($"[MRKeyboardBinderSetup] MRTextInputGuard가 붙은 오브젝트를 숙주로 사용: '{GetPath(host.transform)}'");
        }

        // 없으면 UIManager가 붙은 오브젝트를 쓴다.
        if (host == null)
        {
            UIManager ui = FindUIManagerInScene();
            if (ui != null)
            {
                host = ui.gameObject;
                Debug.Log($"[MRKeyboardBinderSetup] UIManager 오브젝트를 숙주로 사용: '{GetPath(host.transform)}'");
            }
        }

        if (host == null)
        {
            Debug.LogError("[MRKeyboardBinderSetup] 숙주 오브젝트를 찾지 못했다. MR 씬을 열고 다시 실행할 것.");
            return;
        }

        // 숙주가 비활성이면 Awake가 돌지 않아 후킹이 안 된다.
        if (!host.activeInHierarchy)
        {
            Debug.LogWarning($"[MRKeyboardBinderSetup] 숙주 '{GetPath(host.transform)}'가 비활성이다. " +
                             "이대로는 Awake가 실행되지 않아 키보드가 뜨지 않는다. 활성 오브젝트를 골라 다시 실행할 것.");
        }

        MRTMPVirtualKeyboardBinder binder = Undo.AddComponent<MRTMPVirtualKeyboardBinder>(host);
        EditorUtility.SetDirty(host);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
        Selection.activeGameObject = host;

        Debug.Log($"[MRKeyboardBinderSetup] 부착 완료: '{GetPath(host.transform)}' (활성={host.activeInHierarchy})\n" +
                  "fields는 비워두면 실행 시 씬 전체에서 자동 등록된다.\n" +
                  "**씬을 저장할 것(Ctrl+S)** — 저장하지 않으면 반영되지 않는다.");
    }

    [MenuItem("Tools/MR/키보드 상태 리포트")]
    public static void Report()
    {
        MRTMPVirtualKeyboardBinder binder = FindBinderInScene();
        MRTextInputGuard guard = FindGuardInScene();
        TMP_InputFieldReport();

        Debug.Log($"[MRKeyboardBinderSetup] 리포트\n" +
                  $"  MRTMPVirtualKeyboardBinder: {(binder == null ? "없음 — 시스템 키보드가 뜨지 않는다" : "'" + GetPath(binder.transform) + "' (활성=" + binder.gameObject.activeInHierarchy + ")")}\n" +
                  $"  MRTextInputGuard: {(guard == null ? "없음" : "'" + GetPath(guard.transform) + "'")}");
    }

    private static void TMP_InputFieldReport()
    {
        TMPro.TMP_InputField[] fields = Object.FindObjectsByType<TMPro.TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[MRKeyboardBinderSetup] 씬의 TMP_InputField {fields.Length}개");
        for (int i = 0; i < fields.Length; i++)
        {
            TMPro.TMP_InputField f = fields[i];
            UnityEngine.UI.Graphic g = f.GetComponent<UnityEngine.UI.Graphic>();
            string raycast = "(Graphic없음)";
            if (g != null)
            {
                raycast = g.raycastTarget.ToString();
            }
            Debug.Log($"  [{i}] '{GetPath(f.transform)}' interactable={f.interactable} readOnly={f.readOnly} raycastTarget={raycast} 활성={f.gameObject.activeInHierarchy}");
        }
    }

    private static MRTMPVirtualKeyboardBinder FindBinderInScene()
    {
        MRTMPVirtualKeyboardBinder[] found = Resources.FindObjectsOfTypeAll<MRTMPVirtualKeyboardBinder>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject != null && found[i].gameObject.scene.IsValid())
            {
                return found[i];
            }
        }
        return null;
    }

    private static MRTextInputGuard FindGuardInScene()
    {
        MRTextInputGuard[] found = Resources.FindObjectsOfTypeAll<MRTextInputGuard>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject != null && found[i].gameObject.scene.IsValid())
            {
                return found[i];
            }
        }
        return null;
    }

    private static UIManager FindUIManagerInScene()
    {
        UIManager[] found = Resources.FindObjectsOfTypeAll<UIManager>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject != null && found[i].gameObject.scene.IsValid())
            {
                return found[i];
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
