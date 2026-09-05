// KAI 제출용 프로토타입 전용 씬 매니저 — SampleSceneKAI에만 배치한다 (Tools/KAI/Build SampleSceneKAI가 생성).
// 기존 스크립트는 수정하지 않고 씬 쪽에서만 동작을 바꾼다:
//   1) 필요할 때만 소환(스폰) 캐릭터를 AICO(charcode "aico")로 고정
//   2) 씬 내 모든 MenuTrigger를 MenuTriggerKAI로 in-place 교체
//      (SubCharManager가 서브 캐릭터에 쓰는 "MenuTrigger 제거 → 대체 트리거 부착" 패턴과 동일)
using UnityEngine;

public class KAIManager : MonoBehaviour
{
    private static KAIManager instance;

    // SampleSceneKAI에 이 매니저가 있으면 접근성 모드로 취급한다.
    // 다른 컴포넌트의 Awake가 먼저 실행돼도 씬 검색으로 정확히 판별한다.
    public static bool IsAccessibilityModeActive
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<KAIManager>();
            }
            return instance != null;
        }
    }

    private const string AicoCharcode = "aico";      // Assets/Char/Aico/Aico.prefab의 CharAttributes.charcode
    private const string AicoPrefabKey = "naost";    // PrefabDataLocal 프리팹 키 (character_database.json AICO 항목)

    // ChangeChar를 사용하는 현재 KAI 씬에서는 꺼 둔다. 이전 제출용 고정 동작이
    // 다시 필요할 때만 인스펙터에서 명시적으로 활성화한다.
    [SerializeField] private bool forceAicoCharacter;

    // Store는 UIManager 통합이 없어 KAI 씬에서만 여기서 연다.
    // 프리팹 참조는 KAISceneBuilder가 씬 생성 시 SerializedObject로 할당한다.
    [SerializeField] private GameObject storePanelPrefab;
    private GameObject storePanelInstance;
    private StoreView storeView;

    // 스윕은 씬 전체(비활성 포함)를 훑기 때문에 비싸다. Quest 실측에서 오브젝트 2588개 기준 약 3.9ms.
    // 그래서 평시에는 느린 안전망 주기로만 돌리고, 실제로 필요한 순간(초기화·캐릭터 교체)에는 즉시 스윕한다.
    private const float SweepIntervalFast = 0.25f;   // 초기 구동 중 스윕 주기 (캐릭터 스폰 대기)
    private const float SweepIntervalIdle = 5f;      // 안정화 후 안전망 스윕 주기
    private const float FastPhaseSeconds = 10f;      // 이 시간까지는 빠른 주기를 유지
    private const float ForceInterval = 1f;          // 캐릭터 고정 재시도 주기

    private float sweepTimer;
    private float forceTimer;
    private float elapsed;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        Debug.Log($"[KAIManager] KAI 프로토타입 씬 활성 — " +
                  $"캐릭터 {(forceAicoCharacter ? "AICO 고정" : "교체 허용")} + MenuTriggerKAI 적용");
        SweepMenuTriggers();   // 초기 1회는 즉시 처리
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (forceAicoCharacter)
        {
            ForceAicoIfNeeded();
        }

        sweepTimer -= Time.deltaTime;
        if (sweepTimer <= 0f)
        {
            // 초기 10초 동안은 캐릭터 스폰을 놓치지 않도록 촘촘히, 그 이후에는 안전망으로만 돈다.
            sweepTimer = (elapsed < FastPhaseSeconds) ? SweepIntervalFast : SweepIntervalIdle;
            SweepMenuTriggers();
        }
    }

    // Store 패널 열기/닫기 — MenuTriggerKAI(런타임 AddComponent라 직렬화 필드가 없음)가 호출한다.
    public static void ToggleStore()
    {
        if (instance == null)
        {
            instance = FindAnyObjectByType<KAIManager>();
        }
        if (instance == null)
        {
            Debug.LogWarning("[KAIManager] KAIManager가 없어 Store를 열 수 없습니다.");
            return;
        }
        instance.ToggleStoreInternal();
    }

    private void ToggleStoreInternal()
    {
        if (storePanelInstance == null)
        {
            // ① 씬에 이미 놓여 있으면 그것을 쓴다 (2026-08-26 추가).
            //
            // 아래 프리팹 인스턴스화 경로는 CanvasManager.canvasUI(메인 Canvas) 아래에 만드는데,
            // MR에서 그 캔버스는 월드 1920 m라 월드 스페이스 패널을 그 안에 넣으면 깨진다
            // (Kickoff Guide §4-18 / §4-36). UIManager.ResolveManagedUI가 다른 패널들에
            // 쓰는 "씬 오브젝트 우선" 규약을 여기에도 적용한다.
            // 씬 배치는 Tools → MR → 상점 패널 배치 가 한다.
            storePanelInstance = FindSceneStorePanel();
            if (storePanelInstance != null)
            {
                storeView = storePanelInstance.GetComponentInChildren<StoreView>(true);

                // 씬에 배치된 패널은 시작 시 비활성으로 저장돼 있다(다른 패널 9개와 같은 규약).
                // StoreView.Show()는 CanvasGroup 알파만 올리므로, SetActive(true)를 같이 해주지 않으면
                // "알파는 1인데 오브젝트가 꺼져 있어 안 보이는" 상태가 된다 (§4-44의 반대 실수).
                if (storePanelInstance.activeSelf == false)
                {
                    storePanelInstance.SetActive(true);
                }

                Debug.Log($"[KAIManager] 씬의 StorePanel을 사용합니다: {storePanelInstance.name}");

                if (storeView != null)
                {
                    storeView.Show();
                }
                return;
            }

            // ② 씬에 없으면 기존 프리팹 경로 (데스크톱 KAI 씬용)
            if (storePanelPrefab == null)
            {
                Debug.LogWarning("[KAIManager] 씬에 StorePanel이 없고 storePanelPrefab도 비어 있어 Store를 열 수 없습니다. " +
                                 "(MR: Tools → MR → 상점 패널 배치 / 데스크톱: KAISceneBuilder 재실행)");
                return;
            }

            Transform parent = null;
            if (CanvasManager.Instance != null && CanvasManager.Instance.canvasUI != null)
            {
                parent = CanvasManager.Instance.canvasUI.transform;
            }
            storePanelInstance = Instantiate(storePanelPrefab, parent);
            storePanelInstance.name = "StorePanel";
            storeView = storePanelInstance.GetComponentInChildren<StoreView>(true);
            Debug.LogWarning("[KAIManager] 씬에 StorePanel이 없어 프리팹을 메인 Canvas 아래 생성했습니다 — " +
                             "MR이라면 §4-18로 깨집니다. Tools → MR → 상점 패널 배치 를 실행하세요.");
            if (storeView != null)
            {
                storeView.Show();   // 첫 오픈은 표시 확정 (Toggle이면 베이크된 alpha에 따라 곧바로 닫힐 수 있다)
            }
            return;
        }

        // 두 번째 이후: 껐다 켜기. 알파만 올리면 비활성 오브젝트에서 안 보인다.
        if (storePanelInstance != null && storePanelInstance.activeSelf == false)
        {
            storePanelInstance.SetActive(true);
        }

        if (storeView != null)
        {
            storeView.Toggle();
        }
    }

    // 토글이 아니라 확정 열기. 메뉴에서 인벤토리와 함께 소환할 때 쓴다 —
    // Toggle이면 이미 열려 있을 때 닫혀버려 "둘 다 열기"가 성립하지 않는다.
    public static void OpenStore()
    {
        if (instance == null)
        {
            instance = FindAnyObjectByType<KAIManager>();
        }
        if (instance == null)
        {
            Debug.LogWarning("[KAIManager] KAIManager가 없어 Store를 열 수 없습니다.");
            return;
        }

        instance.OpenStoreInternal();
    }

    private void OpenStoreInternal()
    {
        if (storeView != null && storeView.IsVisible)
        {
            return;
        }

        ToggleStoreInternal();
    }

    // 씬에 실재하는 StorePanel 찾기 (비활성 포함).
    // 프리팹 에셋이 아니라 씬 오브젝트만 받는다 — scene.IsValid()가 그 판정이다.
    private GameObject FindSceneStorePanel()
    {
        StoreView[] views = Resources.FindObjectsOfTypeAll<StoreView>();
        for (int i = 0; i < views.Length; i++)
        {
            StoreView view = views[i];
            if (view == null || view.gameObject == null)
            {
                continue;
            }

            if (view.gameObject.scene.IsValid() == false)
            {
                continue;
            }

            return view.gameObject;
        }

        return null;
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
        // 배열을 할당하기 전에 존재 여부만 먼저 확인한다. 평시에는 남은 MenuTrigger가 0개라 여기서 끝난다.
        if (FindAnyObjectByType<MenuTrigger>(FindObjectsInactive.Include) == null) return;

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
