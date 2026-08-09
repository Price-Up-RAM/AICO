// KAI 제출용 프로토타입 씬 빌더 — SampleScene을 완전 복제해 SampleSceneKAI를 만들고
// 씬 전용 트리거(KAIManager) 루트 오브젝트를 하이어라키 최상단(스타트 지점)에 추가한다.
// 재실행 시 기존 SampleSceneKAI를 지우고 최신 SampleScene 기준으로 다시 만든다(멱등).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KAISceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
    private const string TargetScenePath = "Assets/Scenes/SampleSceneKAI.unity";

    [MenuItem("Tools/KAI/Build SampleSceneKAI")]
    public static void Build()
    {
        // GUI에서 실행 시 저장 안 된 씬 보호 (batchmode에서는 다이얼로그 불가라 건너뜀)
        if (!Application.isBatchMode)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[KAI] 사용자가 저장을 취소해 빌드를 중단합니다.");
                return;
            }
        }

        // 대상 씬이 열려 있는 상태에서 삭제/복사하지 않도록 원본 씬으로 전환
        if (SceneManager.GetActiveScene().path == TargetScenePath)
        {
            EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath) != null)
        {
            AssetDatabase.DeleteAsset(TargetScenePath);
        }

        if (!AssetDatabase.CopyAsset(SourceScenePath, TargetScenePath))
        {
            Debug.LogError($"[KAI] 씬 복사 실패: {SourceScenePath} → {TargetScenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

        // 씬 전용 트리거: KAIManager (캐릭터 AICO 고정 + MenuTrigger→MenuTriggerKAI 교체)
        GameObject managerGo = new GameObject("KAIManager");
        KAIManager kaiManager = managerGo.AddComponent<KAIManager>();
        managerGo.transform.SetSiblingIndex(0);

        // KAI 씬은 채팅 입력(Enter)을 VL Router로 보낸다 — 원본(SampleScene)에는 없는
        // KAI 전용 직렬화 오버라이드라서 재생성 때마다 빌더가 다시 적용한다.
        ApplyVlRouterSubmitOverride();

        // Function 메뉴(Inventory/Store/Skill)용 배선 — 원본 씬·프리팹 무수정 원칙에 따라
        // 씬 인스턴스에만 직렬화 오버라이드로 건다.
        ApplySkillPrefabOverride();
        ApplyStorePanelPrefab(kaiManager);

        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene))
        {
            Debug.Log($"[KAI] SampleSceneKAI 생성 완료: {TargetScenePath}");
        }
        else
        {
            Debug.LogError("[KAI] SampleSceneKAI 저장 실패");
        }
    }

    // UIManager.skill 필드에 SkillView 프리팹을 할당 — Root260616.prefab의 직렬화가
    // UIManager.cs보다 오래돼 skill 필드가 비어 있고, 비어 있으면 ToggleSkill이 아무것도 못 연다.
    // 프리팹 에셋을 넣어주면 ResolveManagedUI가 canvasUI 아래에 자동 인스턴스화한다.
    private static void ApplySkillPrefabOverride()
    {
        const string SkillPrefabPath = "Assets/Prefabs/UI/Skills/SkillView/Prefabs/SkillView.prefab";
        GameObject skillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillPrefabPath);
        if (skillPrefab == null)
        {
            Debug.LogWarning($"[KAI] SkillView 프리팹을 찾지 못했습니다: {SkillPrefabPath}");
            return;
        }

        UIManager[] managers = Object.FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UIManager manager in managers)
        {
            SerializedObject so = new SerializedObject(manager);
            SerializedProperty prop = so.FindProperty("skill");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = skillPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[KAI] UIManager.skill = SkillView.prefab 적용: {manager.gameObject.name}");
            }
        }

        if (managers.Length == 0)
        {
            Debug.LogWarning("[KAI] UIManager를 찾지 못해 skill 프리팹을 적용하지 못했습니다.");
        }
    }

    // KAIManager.storePanelPrefab에 StorePanel 프리팹을 할당 — Store는 UIManager 통합이 없어
    // KAI 씬에서는 KAIManager.ToggleStore가 이 프리팹을 canvasUI 아래에 지연 인스턴스화한다.
    private static void ApplyStorePanelPrefab(KAIManager kaiManager)
    {
        const string StorePrefabPath = "Assets/Prefabs/UI/Store/Prefabs/StorePanel.prefab";
        GameObject storePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StorePrefabPath);
        if (storePrefab == null)
        {
            Debug.LogWarning($"[KAI] StorePanel 프리팹을 찾지 못했습니다: {StorePrefabPath}");
            return;
        }

        SerializedObject so = new SerializedObject(kaiManager);
        SerializedProperty prop = so.FindProperty("storePanelPrefab");
        if (prop == null)
        {
            Debug.LogWarning("[KAI] KAIManager.storePanelPrefab 필드를 찾지 못했습니다.");
            return;
        }
        prop.objectReferenceValue = storePrefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[KAI] KAIManager.storePanelPrefab = StorePanel.prefab 적용");
    }

    // 씬 내 모든 ChatHandler(비활성 포함)의 useVlRouterForSubmit을 true로 설정
    private static void ApplyVlRouterSubmitOverride()
    {
        ChatHandler[] handlers = Object.FindObjectsByType<ChatHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ChatHandler handler in handlers)
        {
            SerializedObject so = new SerializedObject(handler);
            SerializedProperty prop = so.FindProperty("useVlRouterForSubmit");
            if (prop != null && !prop.boolValue)
            {
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[KAI] useVlRouterForSubmit=true 적용: {handler.gameObject.name}");
            }
        }

        if (handlers.Length == 0)
        {
            Debug.LogWarning("[KAI] ChatHandler를 찾지 못해 useVlRouterForSubmit을 적용하지 못했습니다.");
        }
    }
}
