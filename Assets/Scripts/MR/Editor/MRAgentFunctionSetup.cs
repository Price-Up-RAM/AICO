using UnityEngine;
using UnityEditor;

// ApiAgentFunction 중계 컴포넌트를 씬에 부착한다 (Phase 5).
//
// 왜 필요한가 (2026-08-25 GUID 실측):
//   ApiAgentFunctionSfx / ApiAgentFunctionAction / ApiAgentFunctionChatMode 세 컴포넌트는
//   프로젝트 어느 씬에도, 프리팹 864개 어디에도 없다. 그런데 ApiAgentFunctionManager.ExecuteAction은
//   null 체크 없이 .Instance.메서드()를 부르고, Instance 게터는 FindObjectOfType만 하고
//   못 찾으면 null을 반환한다.
//   → play_sfx / set_chat_mode / toggle_chat_mode / get_chat_mode 는 호출 즉시 NRE다.
//   이 4종은 unity_functions_list로 서버에 나가므로, 서버가 고르면 라우터 세션이 예외로 죽는다.
//
// Kickoff Guide 4-64 참고. 손으로 붙이지 않고 스크립트로 만든 이유는 재현 가능해야 하기 때문이다(7-1 F).
public static class MRAgentFunctionSetup
{
    [MenuItem("Tools/MR/에이전트 기능 컴포넌트 부착")]
    public static void AttachAgentFunctionComponents()
    {
        ApiAgentFunctionManager manager = FindInScene<ApiAgentFunctionManager>();
        if (manager == null)
        {
            Debug.LogError("[MRAgentFunctionSetup] 씬에서 ApiAgentFunctionManager를 찾지 못했다. MR 씬을 열고 다시 실행할 것.");
            return;
        }

        GameObject host = manager.gameObject;
        Debug.Log($"[MRAgentFunctionSetup] 숙주: '{GetPath(host.transform)}' (활성={host.activeInHierarchy})");

        if (!host.activeInHierarchy)
        {
            Debug.LogWarning("[MRAgentFunctionSetup] 숙주가 비활성이다. Awake가 돌지 않아 Instance가 잡히지 않는다.");
        }

        int added = 0;
        added += EnsureComponent<ApiAgentFunctionSfx>(host);
        added += EnsureComponent<ApiAgentFunctionAction>(host);
        added += EnsureComponent<ApiAgentFunctionChatMode>(host);

        if (added == 0)
        {
            Debug.Log("[MRAgentFunctionSetup] 세 컴포넌트가 이미 전부 붙어 있다. 변경 없음.");
            Selection.activeGameObject = host;
            return;
        }

        EditorUtility.SetDirty(host);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
        Selection.activeGameObject = host;

        Debug.Log($"[MRAgentFunctionSetup] {added}개 부착 완료.\n" +
                  "**씬을 저장할 것(Ctrl+S)** — 저장하지 않으면 반영되지 않는다.\n" +
                  "주의: ApiAgentFunctionAction.WalkLeft/WalkRight/StopAction은 PhysicsManager를 쓰는데 " +
                  "그건 MRSceneStripper가 끈다. Dance()만 안전하며 character_walk_* 3종은 MRUnsupportedFunctions로 이미 차단돼 있다.");
    }

    [MenuItem("Tools/MR/에이전트 기능 상태 리포트")]
    public static void Report()
    {
        Debug.Log("[MRAgentFunctionSetup] 리포트\n" +
                  $"  ApiAgentFunctionManager : {Describe(FindInScene<ApiAgentFunctionManager>())}\n" +
                  $"  ApiAgentFunctionSfx     : {Describe(FindInScene<ApiAgentFunctionSfx>())}   ← play_sfx\n" +
                  $"  ApiAgentFunctionAction  : {Describe(FindInScene<ApiAgentFunctionAction>())}   ← character_dance(라우터 switch가 가로채므로 없어도 동작)\n" +
                  $"  ApiAgentFunctionChatMode: {Describe(FindInScene<ApiAgentFunctionChatMode>())}   ← set/toggle/get_chat_mode\n" +
                  $"  AlarmManager            : {Describe(FindInScene<AlarmManager>())}\n" +
                  $"  MRJukebox               : {Describe(FindInScene<MRJukebox>())}");
    }

    private static string Describe(Component c)
    {
        if (c == null)
        {
            return "없음 — 호출 시 NRE";
        }
        return $"'{GetPath(c.transform)}' (활성={c.gameObject.activeInHierarchy})";
    }

    private static int EnsureComponent<T>(GameObject host) where T : Component
    {
        T existing = host.GetComponent<T>();
        if (existing != null)
        {
            Debug.Log($"[MRAgentFunctionSetup] {typeof(T).Name} — 이미 있음");
            return 0;
        }

        // 다른 오브젝트에 이미 있으면 중복 부착하지 않는다.
        T elsewhere = FindInScene<T>();
        if (elsewhere != null)
        {
            Debug.Log($"[MRAgentFunctionSetup] {typeof(T).Name} — 다른 오브젝트 '{GetPath(elsewhere.transform)}'에 이미 있음, 건너뜀");
            return 0;
        }

        Undo.AddComponent<T>(host);
        Debug.Log($"[MRAgentFunctionSetup] {typeof(T).Name} — 부착");
        return 1;
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
