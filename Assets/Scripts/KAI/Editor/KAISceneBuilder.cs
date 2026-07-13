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
        managerGo.AddComponent<KAIManager>();
        managerGo.transform.SetSiblingIndex(0);

        // KAI 씬은 채팅 입력(Enter)을 VL Router로 보낸다 — 원본(SampleScene)에는 없는
        // KAI 전용 직렬화 오버라이드라서 재생성 때마다 빌더가 다시 적용한다.
        ApplyVlRouterSubmitOverride();

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
