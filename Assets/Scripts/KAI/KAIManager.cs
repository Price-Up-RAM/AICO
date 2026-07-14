// KAI 제출용 프로토타입 전용 씬 매니저 — SampleSceneKAI에만 배치한다 (Tools/KAI/Build SampleSceneKAI가 생성).
// 기존 스크립트는 수정하지 않고 씬 쪽에서만 동작을 바꾼다:
//   1) 소환(스폰) 캐릭터를 AICO(charcode "aico")로 고정 — 다른 캐릭터가 뜨면 즉시 교체
//   2) 씬 내 모든 MenuTrigger를 MenuTriggerKAI로 in-place 교체
//      (SubCharManager가 서브 캐릭터에 쓰는 "MenuTrigger 제거 → 대체 트리거 부착" 패턴과 동일)
using UnityEngine;

public class KAIManager : MonoBehaviour
{
    private const string AicoCharcode = "aico";      // Assets/Char/Aico/Aico.prefab의 CharAttributes.charcode
    private const string AicoPrefabKey = "naost";    // PrefabDataLocal 프리팹 키 (character_database.json AICO 항목)

    private const float SweepInterval = 0.25f;       // MenuTrigger 교체 스윕 주기
    private const float ForceInterval = 1f;          // 캐릭터 고정 재시도 주기

    private float sweepTimer;
    private float forceTimer;

    private void Start()
    {
        Debug.Log("[KAIManager] KAI 프로토타입 씬 활성 — 캐릭터 AICO 고정 + MenuTriggerKAI 적용");
    }

    private void Update()
    {
        ForceAicoIfNeeded();

        sweepTimer -= Time.deltaTime;
        if (sweepTimer <= 0f)
        {
            sweepTimer = SweepInterval;
            SweepMenuTriggers();
        }
    }

    // 현재 캐릭터가 AICO가 아니면 AICO로 교체 (초기 스폰·AI 의도(change_model) 등 모든 경로를 커버)
    private void ForceAicoIfNeeded()
    {
        forceTimer -= Time.deltaTime;
        if (forceTimer > 0f) return;
        forceTimer = ForceInterval;

        if (CharManager.Instance == null) return;

        GameObject current = CharManager.Instance.GetCurrentCharacter();
        if (current == null) return;  // CharManager 초기 스폰 대기

        CharAttributes attrs = current.GetComponent<CharAttributes>();
        if (attrs != null && attrs.charcode == AicoCharcode)
        {
            return;  // 이미 AICO
        }

        // Pomodoro 착석 중에는 CharManager가 교체를 차단하므로 시도를 보류
        if (ChatModeManager.Instance != null && ChatModeManager.Instance.IsPomodoroMode()) return;

        Debug.Log($"[KAIManager] 캐릭터를 AICO로 고정합니다. (현재: {(attrs != null ? attrs.charcode : "unknown")})");
        if (!CharManager.Instance.ChangeCharacterFromCharCode(AicoCharcode))
        {
            // charList 미등록 대비: PrefabDataLocal에서 프리팹을 받아 동적 등록 경로로 교체
            GameObject prefab = ChangeCharManager.Instance != null ? ChangeCharManager.Instance.GetLocalPrefab(AicoPrefabKey) : null;
            if (prefab != null)
            {
                CharManager.Instance.ChangeCharacterFromDLC(prefab);
            }
            else
            {
                Debug.LogWarning("[KAIManager] AICO 프리팹을 찾지 못해 캐릭터 고정에 실패했습니다.");
                return;
            }
        }

        // 교체로 생성된 새 인스턴스의 MenuTrigger를 즉시 처리
        SweepMenuTriggers();
    }

    // 비활성 오브젝트 포함, 씬의 모든 MenuTrigger를 같은 GameObject의 MenuTriggerKAI로 교체
    private void SweepMenuTriggers()
    {
        MenuTrigger[] triggers = FindObjectsByType<MenuTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MenuTrigger trigger in triggers)
        {
            GameObject go = trigger.gameObject;
            if (go.GetComponent<MenuTriggerKAI>() == null)
            {
                go.AddComponent<MenuTriggerKAI>();
            }
            Destroy(trigger);
            Debug.Log($"[KAIManager] MenuTrigger → MenuTriggerKAI 교체: {go.name}");
        }
    }
}
