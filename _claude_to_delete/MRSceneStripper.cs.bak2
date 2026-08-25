// MR 씬 전용 — 데스크톱 전용 컴포넌트를 런타임에 비활성화한다.
//
// 왜 씬 오버라이드가 아니라 코드인가:
//   1) 타입 기준이라 데스크톱 팀이 컴포넌트를 새로 붙여도 목록에만 있으면 자동으로 꺼진다.
//   2) 씬 오버라이드가 줄어들어 Root260616 프리팹 갱신이 깔끔해지고 Revert All 사고 위험이 없다.
//   3) "MR에서 무엇을 왜 끄는가"가 한 파일에 모여 git diff로 추적된다.
//
// 그리고 분류되지 않은 컴포넌트를 발견하면 경고한다.
// 데스크톱 쪽에 새 매니저가 추가되면 MR 빌드 첫 실행 로그에서 바로 드러나게 하기 위함이다.
// (Phase 1에서 TransparentWindow의 GPU readback 코루틴을 찾느라 프로파일러를 몇 시간 판 경험 때문)
//
// 원본 불변 원칙: 이 스크립트는 MR 씬에만 배치한다. 데스크톱 씬에는 존재하지 않으므로 영향이 없다.
// 배치 위치: SampleSceneKAI-MR 씬 루트 (KAIManager 옆)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-10000)]  // 다른 컴포넌트의 Start()보다 먼저 돌아야 비활성화가 의미를 갖는다
public class MRSceneStripper : MonoBehaviour
{
    [Header("동작")]
    [Tooltip("비활성화한 컴포넌트를 로그로 남긴다")]
    [SerializeField] private bool logDisabled = true;

    [Tooltip("분류되지 않은 컴포넌트를 경고한다 (데스크톱 쪽 신규 추가 감지)")]
    [SerializeField] private bool warnUnclassified = true;

    [Tooltip("감사(audit) 실행까지 대기 시간. 지연 초기화되는 매니저를 기다린다")]
    [SerializeField] private float auditDelaySeconds = 5f;

    [Tooltip("서브 캐릭터 등 뒤늦게 스폰되는 오브젝트용 안전망 스윕 주기")]
    [SerializeField] private float safetySweepInterval = 5f;

    [Tooltip("기동 직후 이 시간(초) 동안은 안전망을 촘촘하게 돈다. " +
             "KAIManager.Start()처럼 늦게 도는 초기화가 데스크톱 컴포넌트를 새로 붙이기 때문이다(§4-46).")]
    [SerializeField] private float earlyWindowSeconds = 20f;

    [Tooltip("기동 창 동안의 안전망 주기(초).")]
    [SerializeField] private float earlySweepInterval = 0.25f;

    // =========================================================
    // 1. 씬에 상주하는 데스크톱 전용 매니저
    // =========================================================
    // MR에서 동작할 수 없거나(Win32 의존) 대상이 없는(데스크톱 화면) 것들.
    private static readonly Type[] DesktopOnlyTypes =
    {
        // --- 창/데스크톱 셸 (Win32) ---
        typeof(TransparentWindow),              // 투명 오버레이 창. 매 프레임 GPU readback으로 14.9ms 소모했다
        typeof(TrayIconManager),                // 트레이 아이콘 (System.Windows.Forms)
        typeof(TaskbarInfo),                    // 작업표시줄 위치 → MRUK FLOOR가 대체
        typeof(WindowCollisionManager),         // 창 충돌 판정 → MRUK 볼륨 앵커가 대체
        typeof(WindowCollisionUI),              // 위 디버그 오버레이
        typeof(ClipboardManager),               // Win32 클립보드

        // --- 전역 입력 훅 (Win32) ---
        typeof(GlobalInputKeyboardManager),
        typeof(GlobalInputMouseClickManager),
        typeof(GlobalInputMouseMoveManager),
        typeof(GlobalInputVariableManager),

        // --- 화면 캡처 / OCR (대상 화면이 MR에 없음) ---
        typeof(ScreenshotOCRRectManager),
        typeof(ScreenshotOCRManager),
        typeof(OCRManager),
        typeof(OCRAutoMapManager),
        typeof(PIPManager),

        // --- 전역 핫키 (MR에 키보드 없음) ---
        typeof(HotkeyManager),                  // 주의: 파일명은 HotKeyManager.cs지만 클래스는 HotkeyManager
        typeof(HotKeyCatalogManager),
        typeof(HotKeyActionManager),

        // --- 데스크톱 컨텍스트 메뉴 트리거 (2026-08-18 이동) ---
        // 예전에는 ReviewedKeepTypes("검토했고 유지")에 있었으나 **잘못된 분류였다.**
        // MenuTrigger.Update()가 매 프레임 UpdateRadialMenuActionPosition()을 부르고,
        // 그 안에서 라디얼 메뉴의 anchoredPosition을
        //   (캐릭터X, 캐릭터Y + 200 * char_size/100 + 100)  ← char_size 100이면 +300
        // 으로 덮어쓴다. 라디얼 메뉴는 이제 최상위 월드 캔버스라 그 300이 **300 m**가 되어
        // 메뉴가 하늘로 날아간다(§4-38 계열). 실측 2026-08-18: 메뉴가 열린 직후 사라짐 —
        // Close()는 한 번도 불리지 않았고, 위치만 y=300으로 고정돼 있었다.
        //
        // 마우스 우클릭/더블클릭으로 캐릭터 메뉴를 여는 데스크톱 전용 트리거이며,
        // MR에서는 Phase 4-A의 MRIntentRouter가 그 역할을 대신한다.
        typeof(MenuTrigger),
        typeof(MenuTriggerKAI),

        // --- Operator / VL 에이전트 (화면 보고 마우스·키보드 조작) ---
        typeof(OperatorManager),
        typeof(OperatorModeManager),
        typeof(ApiVlEngineManager),
        typeof(ApiVlAgentManager),
        typeof(ApiVlPlannerManager),
        typeof(ApiVlRouterManager),
        typeof(ApiVlRouterResponseManager),
        typeof(ApiAgentFunctionManager),

        // --- 로컬 Python 서버 기동 (Android에서 Process.Start 불가) ---
        typeof(InstallStatusManager),           // JarvisServerManager/SampleServerManager를 호출하는 진입점
        typeof(JarvisServerManager),
        typeof(SampleServerManager),
        typeof(InstallerManager),
        typeof(ScenarioInstallerManager),
        typeof(DownloadManager),

        // --- 기타 데스크톱 전용 ---
        typeof(DebugManager),                   // Process.Start("explorer.exe" / "notepad.exe")

        // BackgroundService — 폰 앱용 Android AAR 브릿지(com.example.mylittlejarvisandroid.Bridge).
        // 앱이 백그라운드일 때도 VAD로 음성을 계속 받기 위한 포그라운드 서비스인데,
        // Quest 빌드에는 해당 AAR이 없어 pluginClass가 null이고 StopService()가 NRE를 낸다.
        // 헤드셋을 벗으면 앱이 멈추므로 시나리오 자체가 성립하지 않는다. (실측 예외의 주원인)
        typeof(BackgroundService),

        // --- 캔버스 픽셀 좌표계에 묶인 이동 시스템 ---
        // PhysicsManager는 Win32를 쓰지 않지만 캐릭터를 RectTransform.anchoredPosition으로 걷게 한다.
        // 래퍼 없이는 moveSpeed 120(픽셀/초)이 초속 120m가 되어 캐릭터가 날아간다 (Quest 실측 확인).
        //
        // 참고 — MRCharacterWorldRoot의 픽셀 공간 래퍼(1/120) 안에서는 이 값이 약 1.0 m/s로
        // 자연스럽게 환산되므로 기술적으로는 그대로 켤 수 있다. 실제로 검증도 됐다.
        // 다만 이동 방식은 Phase 2에서 '방 안 자율 이동'으로 설계하기로 했으므로 그때까지 꺼둔다.
        // 켤 경우 MRCharacterWorldRoot의 wanderRangeMeters로 범위를 제한할 것
        // (자체 경계가 Canvas_Char 폭이라 ±8m까지 걸어간다).
        typeof(PhysicsManager),

        // --- 마우스 드래그 리사이즈 (ISDK 드래그 이벤트가 그대로 오발동시킨다) ---
        // Image_ChatBalloon에 붙은 데스크톱 전용 창 가장자리 드래그-리사이즈 핸들러.
        // IBeginDragHandler/IDragHandler를 표준으로 구현하고 있어서 PointableCanvasModule이
        // 손 포크/레이 드래그를 그대로 배달해준다 — 즉 "옮기려고 잡았는데 리사이즈된다"의 원인.
        // 실측(Quest 3S, 2026-08-10): 드래그 중 매 프레임 sizeDelta 변경 → 레이아웃 리빌드로
        // 프레임이 30fps대로 떨어지고, BoundsClipper 크기가 바뀐 rect와 어긋나 손 모형이
        // 표면에 붙은 것처럼 보였다. 말풍선은애초에 사용자가 드래그로 옮기는 대상이 아니므로
        // (MRBalloonWorldFollow가 위치를 담당) MR에서는 통째로 끈다.
        typeof(UIResizeHandler),

        // --- 마우스 드래그 이동 핸들러들 (UIResizeHandler와 같은 이유) ---
        // 전부 IBeginDragHandler/IDragHandler를 구현한 데스크톱 전용 "창 끌어서 옮기기"
        // 스크립트다. MR에서는 ISDK grab(§4-22)이 이동을 담당하므로 켜두면 손 드래그가
        // 두 경로로 동시에 처리돼 패널이 튀거나 anchoredPosition이 매 프레임 덮어써진다.
        // 그룹 C 패널(TalkMenuImage / DebugMenuImage / PortraitMask / Image_DebugBalloon*)에
        // 붙어 있으니 MR 전환 시 반드시 함께 꺼야 한다.
        typeof(DragUIHandler),
        typeof(UIDragHandler),
        typeof(DragHandler),
        typeof(SubDragHandler),

        // --- XR Interaction Toolkit의 중복 UI 레이캐스터 ---
        // PointableCanvasModule(_exclusiveMode)이 XRUIInputModule은 비활성화하지만,
        // TrackedDeviceGraphicRaycaster 자체는 BaseRaycaster라 RaycasterManager에 남아있는 한
        // EventSystem.RaycastAll()이 매 포인터 업데이트마다 계속 호출한다 — 아무 것도 소비하지
        // 않는 순수 오버헤드. Building Block으로 캔버스에 UI 상호작용을 추가할 때 같이 붙는
        // 경우가 있으니 새 캔버스를 만들 때마다 있는지 확인할 것.
        typeof(UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster),
    };

    // =========================================================
    // 2. 런타임 스폰 캐릭터에 붙어 오는 데스크톱 입력/물리
    // =========================================================
    // Aico.prefab에 붙어 있어 CharManager가 스폰할 때 딸려온다. 프리팹은 수정하지 않는다.
    // Phase 2/4에서 MR 대체 컴포넌트(MRFloorPlacement / MRIntentRouter 등)를 붙일 예정.
    private static readonly Type[] CharacterDesktopTypes =
    {
        typeof(ClickHandler),                   // 좌클릭 → 대화. Phase 4에서 MRIntentRouter가 대체
        typeof(DragHandler),                    // 화면좌표 드래그. Phase 4에서 MRRayDragAdapter가 대체
        typeof(WheelHandler),                   // 마우스 휠 (MR에 없음)
        typeof(FallingObject),                  // 2D 캔버스 낙하. Phase 2에서 MRFloorPlacement가 대체
        // MenuTrigger는 KAIManager가 MenuTriggerKAI로 교체하므로 여기서 다루지 않는다.
    };

    // =========================================================
    // 3. 검토 완료 — MR에서 유지하는 컴포넌트
    // =========================================================
    // 감사(audit)에서 "분류되지 않음" 경고를 내지 않기 위한 화이트리스트.
    // 새 컴포넌트가 추가되면 여기에도 없고 위에도 없으므로 경고가 뜬다. 그게 목적이다.
    private static readonly Type[] ReviewedKeepTypes =
    {
        // --- 코어 ---
        typeof(GameManager), typeof(UIManager), typeof(SettingManager), typeof(StatusManager),
        typeof(CanvasManager), typeof(MemoryManager), typeof(PermissionManager),
        typeof(GlobalTimeVariableManager), typeof(AddressableManager), typeof(LanguageManager),

        // --- AI 파이프라인 (플랫폼 무관, MR에서도 그대로 사용) ---
        typeof(APIManager), typeof(APIAroPlaManager), typeof(ApiGeminiMultiClient),
        typeof(ApiGeminiDirectClient), typeof(ApiGeminiCharacterDataManager),
        typeof(ChatModeManager), typeof(ChatHandler), typeof(ServerManager),

        // --- 음성 (MR에서 오히려 1급 입력) ---
        typeof(WhisperSTTManager), typeof(MicrophoneManager), typeof(TTSManager),

        // --- 캐릭터 / 애니메이션 ---
        typeof(AnimationManager), typeof(AnimationPlayerManager), typeof(EmotionManager),
        typeof(SettingCharManager), typeof(ChangeCharManager),

        // --- 대화 / 시나리오 ---
        typeof(DialogueManager), typeof(DialogueCacheManager), typeof(ChoiceManager),
        typeof(ChoiceInputManager), typeof(ScenarioAskManager), typeof(ScenarioCommonManager),
        typeof(ScenarioTutorialManager), typeof(MiniGame20QManager),

        // --- UI / 말풍선 (Phase 3에서 World Space로 전환 예정) ---
        typeof(AnswerBalloonManager), typeof(AnswerBalloonSimpleManager), typeof(AskBalloonManager),
        typeof(ChatBalloonManager), typeof(EmotionBalloonManager), typeof(NoticeBalloonManager),
        typeof(NoticeManager), typeof(PortraitBalloonSimpleManager),
        typeof(UIChatSituationManager), typeof(UIGame20QPanelManager), typeof(UIPositionManager),
        typeof(UIUserCardManager), typeof(EffectManager), typeof(ClickEffecter),

        // --- 서브 캐릭터 ---
        typeof(SubAnswerBalloonManager), typeof(SubAnswerBalloonSimpleManager),
        typeof(SubChatBalloonManager),

        // --- 디버그 (무해, 필요시 별도로 끈다) ---
        typeof(DebugBalloonManager), typeof(DebugBalloonManager2), typeof(DebugMenuManager),


        // (MenuTrigger / MenuTriggerKAI는 2026-08-18에 DesktopOnlyTypes로 옮겼다 — 아래 참고)
    };

    // 외부 패키지/플러그인 타입 — typeof()로 참조하면 어셈블리 의존이 생기므로 이름으로만 분류한다.
    private static readonly string[] ReviewedKeepTypeNames =
    {
        "WhisperManager",   // whisper.unity 계열 외부 플러그인. WhisperSTTManager가 사용한다
    };

    // =========================================================
    // 4. 통째로 비활성화할 GameObject (경로 기준)
    // =========================================================
    // 컴포넌트 타입으로 특정할 수 없는 것들(카메라 등). 경로가 바뀌면 경고를 내므로
    // 데스크톱 쪽 씬 구조 변경도 여기서 감지된다.
    // 주의: Canvases/Canvas 는 Phase 3에서 World Space로 전환할 대상이라 여기 넣지 않는다.
    // 2026-08-02: 아래 대상들은 씬에서 **영구 삭제**되었으므로 목록을 비웠다.
    //   Cameras(3종) · Legacy/PIP · Tester · Manager/DevManager · SitSupport
    //   → 삭제 작업은 Editor 도구가 담당한다: Tools → MR → 데스크톱 전용 오브젝트 삭제
    //
    // ⚠ PortraitCamera는 여기 넣지 않는다.
    //   Operator 모드(원격 대화)에서 사용하는 기능이며, 카메라 활성/비활성은
    //   OperatorModeManager가 모드 전환 시 제어한다. 여기서 강제로 끄면 모드가 동작하지 않는다.
    private static readonly string[] DisableObjectPaths =
    {
        // 현재 비어 있음. 씬에서 지울 수 없고 런타임에만 꺼야 하는 오브젝트가 생기면 여기에 추가한다.
    };

    // =========================================================
    // 5. 반드시 켜져 있어야 하는 오브젝트
    // =========================================================
    // 성능 테스트 등으로 임시 비활성화해 놓고 되돌리는 것을 잊기 쉽다.
    // 실제로 CharManager가 꺼진 채 남아 캐릭터가 스폰되지 않는 사고가 있었다.
    private static readonly string[] RequiredActivePaths =
    {
        "Root260616/Manager/CharManager",   // 꺼지면 캐릭터가 스폰되지 않는다
        "Root260616/Manager/UIManager",
        "Root260616/Manager/GameManager",
        "Root260616/Canvases",
    };

    // 감사 대상 오브젝트 이름 (Root260616 하위)
    private static readonly string[] AuditTargetNames = { "GameManager", "UIManager" };

    private HashSet<Type> _desktopOnly;
    private HashSet<Type> _characterDesktop;
    private HashSet<Type> _classified;

    private GameObject _lastCharacter;
    private float _safetyTimer;
    private float _elapsed;
    private int _disabledCount;
    private bool _secondPassDone;

    private void Awake()
    {
        _desktopOnly = new HashSet<Type>(DesktopOnlyTypes);
        _characterDesktop = new HashSet<Type>(CharacterDesktopTypes);

        _classified = new HashSet<Type>(DesktopOnlyTypes);
        _classified.UnionWith(CharacterDesktopTypes);
        _classified.UnionWith(ReviewedKeepTypes);

        DisableObjects();
        CheckRequiredActive();
        StripSceneManagers();
    }

    // 필수 오브젝트가 꺼져 있으면 크게 경고한다. (자동으로 켜지는 않는다 — 의도적 비활성일 수 있으므로)
    private void CheckRequiredActive()
    {
        foreach (string path in RequiredActivePaths)
        {
            Transform t = FindByPath(path);
            if (t == null)
            {
                Debug.LogWarning($"[MRStripper] 필수 오브젝트 경로를 찾지 못했습니다: '{path}'");
                continue;
            }

            if (t.gameObject.activeInHierarchy) continue;

            Debug.LogError($"[MRStripper] ❌ 필수 오브젝트가 비활성 상태입니다: '{path}'\n" +
                           "  MR 동작에 필요합니다. 성능 테스트 등으로 꺼둔 뒤 되돌리지 않았는지 확인하세요.\n" +
                           "  (CharManager가 꺼져 있으면 캐릭터가 스폰되지 않습니다.)");
        }
    }

    // =========================================================
    // 경로 기준 GameObject 비활성화
    // =========================================================
    private void DisableObjects()
    {
        int done = 0;
        bool anyMiss = false;

        foreach (string path in DisableObjectPaths)
        {
            Transform t = FindByPath(path);
            if (t == null)
            {
                Debug.LogWarning($"[MRStripper] 경로를 찾지 못했습니다: '{path}' — " +
                                 "데스크톱 쪽에서 씬 구조가 바뀌었는지 확인하세요.");
                anyMiss = true;
                continue;
            }

            if (!t.gameObject.activeSelf) continue;

            t.gameObject.SetActive(false);
            done++;
            if (logDisabled) Debug.Log($"[MRStripper] 오브젝트 비활성화: {path}");
        }

        if (done > 0) Debug.Log($"[MRStripper] 데스크톱 전용 오브젝트 {done}개를 비활성화했습니다.");
        if (anyMiss) LogSceneRoots();   // 실패 원인 파악을 돕는다

        // 카메라가 남아 있으면 Passthrough가 가려진다. 활성 카메라를 진단용으로 찍는다.
        Camera[] activeCams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var cs = new StringBuilder($"[MRStripper] 활성 카메라 {activeCams.Length}개: ");
        foreach (Camera c in activeCams) cs.Append($"'{c.name}'(clear={c.clearFlags}, depth={c.depth}) ");
        Debug.Log(cs.ToString());
    }

    // 비활성 오브젝트도 찾아야 하므로 GameObject.Find를 쓸 수 없다.
    // 1) 루트 이름 정확 일치로 탐색 → 2) 실패하면 경로 뒷부분(루트 제외)만으로 재탐색.
    //    프리팹 인스턴스 이름이 "Root260616 (1)" 등으로 바뀌어도 동작하게 하기 위함이다.
    private Transform FindByPath(string path)
    {
        string[] parts = path.Split('/');
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // 1) 루트 이름 정확 일치
        foreach (Transform t in all)
        {
            if (t.parent != null || t.name != parts[0]) continue;

            Transform cur = t;
            for (int i = 1; i < parts.Length && cur != null; i++) cur = cur.Find(parts[i]);
            if (cur != null) return cur;
        }

        // 2) 루트 이름을 무시하고 나머지 경로로 탐색
        if (parts.Length < 2) return null;

        foreach (Transform t in all)
        {
            if (t.parent != null) continue;   // 루트만 후보

            Transform cur = t;
            for (int i = 1; i < parts.Length && cur != null; i++) cur = cur.Find(parts[i]);
            if (cur != null)
            {
                Debug.LogWarning($"[MRStripper] 루트 이름이 '{parts[0]}'이 아닌 '{t.name}'에서 " +
                                 $"'{path}'를 찾았습니다. DisableObjectPaths 갱신을 권장합니다.");
                return cur;
            }
        }

        return null;
    }

    // 경로 탐색이 실패했을 때 원인 파악용 — 씬 루트 목록을 찍는다.
    private void LogSceneRoots()
    {
        var sb = new StringBuilder("[MRStripper] 씬 루트 오브젝트: ");
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            if (t.parent == null) sb.Append($"'{t.name}' ");
        }
        Debug.Log(sb.ToString());
    }

    private IEnumerator Start()
    {
        if (!warnUnclassified) yield break;
        yield return new WaitForSeconds(auditDelaySeconds);
        AuditUnclassified();
    }

    private void Update()
    {
        // 캐릭터 교체 감지는 O(1)이다. 전수 스캔을 매 프레임 돌리지 않는다.
        // (KAIManager가 FindObjectsByType 전수 스캔으로 프레임당 3.9ms를 쓰던 전례)
        if (CharManager.Instance != null)
        {
            GameObject current = CharManager.Instance.GetCurrentCharacter();
            if (current != _lastCharacter)
            {
                _lastCharacter = current;
                if (current != null) StripCharacter(current);

                // 캐릭터 교체는 KAIManager.SweepMenuTriggers()를 다시 태워
                // MenuTriggerKAI를 새로 AddComponent 할 수 있다. 5초 안전망을 기다리지 않는다.
                if (_secondPassDone) StripSceneManagers("캐릭터 교체 후");
            }
        }

        // 서브 캐릭터 등 위 경로로 안 잡히는 경우를 위한 안전망.
        // 기동 직후에는 촘촘하게, 그 뒤에는 느리게 돈다 — 실측(2026-08-18)에서
        // KAIManager가 붙인 MenuTriggerKAI 6개를 **오직 이 안전망만** 잡아냈다.
        // 1차(Awake)·2차(첫 프레임 LateUpdate) 둘 다 놓쳤다 = KAIManager.Start()가
        // 첫 프레임보다 늦게 돈다는 뜻이다. 기동 창이 그 간극을 메운다.
        _elapsed += Time.deltaTime;

        _safetyTimer -= Time.deltaTime;
        if (_safetyTimer <= 0f)
        {
            _safetyTimer = _elapsed < earlyWindowSeconds ? earlySweepInterval : safetySweepInterval;
            SafetySweepCharacterComponents();
        }
    }

    // =========================================================
    // 씬 상주 매니저 비활성화
    // =========================================================
    private void StripSceneManagers(string pass = "1차(Awake)")
    {
        int before = _disabledCount;

        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MonoBehaviour mb in all)
        {
            if (mb == null) continue;
            if (!_desktopOnly.Contains(mb.GetType())) continue;
            Disable(mb, "데스크톱 전용");
        }

        Debug.Log($"[MRStripper] {pass}: 데스크톱 전용 컴포넌트 {_disabledCount - before}개 비활성화 (누적 {_disabledCount}개).");
    }

    // =========================================================
    // 2차 패스 — 런타임에 AddComponent 된 데스크톱 컴포넌트
    // =========================================================
    // Awake 1회 전수 순회로는 **부족하다.** 실측 사고(2026-08-18):
    //   KAIManager.Start() → SweepMenuTriggers() → go.AddComponent<MenuTriggerKAI>()
    // 이 스트리퍼의 Awake(실행 순서 -10000)보다 **나중에** 돌기 때문에, 방금 태어난
    // MenuTriggerKAI는 1차 패스를 그대로 통과했다. 그 Update()가 매 프레임 라디얼 메뉴의
    // anchoredPosition.y를 300으로 덮어써 메뉴가 하늘(300 m)로 날아갔다. (Kickoff Guide §4-46)
    //
    // LateUpdate는 그 프레임의 모든 Start()보다 뒤에 돌므로, 한 프레임 안에 잡힌다.
    private void LateUpdate()
    {
        if (_secondPassDone) return;
        _secondPassDone = true;

        StripSceneManagers("2차(첫 프레임 LateUpdate — 런타임 추가분)");
    }

    // =========================================================
    // 캐릭터 부착 컴포넌트 비활성화
    // =========================================================
    private void StripCharacter(GameObject character)
    {
        int before = _disabledCount;

        foreach (Type t in CharacterDesktopTypes)
        {
            Component[] found = character.GetComponentsInChildren(t, true);
            foreach (Component c in found)
            {
                if (c is MonoBehaviour mb) Disable(mb, "캐릭터 데스크톱 입력");
            }
        }

        if (_disabledCount > before)
        {
            Debug.Log($"[MRStripper] 캐릭터 '{character.name}'에서 {_disabledCount - before}개를 비활성화했습니다.");
        }
    }

    private void SafetySweepCharacterComponents()
    {
        // 런타임에 추가되는 컴포넌트를 잡기 위해 매번 30개가 넘는 Type으로 FindObjectsByType를 호출하면
        // 프레임당 100ms 이상의 엄청난 렉(가비지 컬렉션 및 네이티브 마샬링)이 발생합니다.
        // 따라서 한 번의 MonoBehaviour 전수 스캔으로 모든 컴포넌트를 순회하며 타입 검사를 수행하도록 최적화했습니다.
        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null || !mb.enabled) continue;

            Type t = mb.GetType();
            if (_desktopOnly.Contains(t))
            {
                Disable(mb, "데스크톱 전용(안전망)");
            }
            else if (_characterDesktop.Contains(t))
            {
                Disable(mb, "캐릭터 데스크톱 입력(안전망)");
            }
        }
    }

    private void Disable(MonoBehaviour mb, string reason)
    {
        if (!mb.enabled) return;

        mb.enabled = false;
        _disabledCount++;

        if (logDisabled)
        {
            // 기동 후 경과 시간을 같이 찍는다 — 런타임에 붙는 컴포넌트를 얼마나 빨리 잡았는지가
            // "그 사이에 무슨 일을 할 수 있었는가"를 결정한다 (§4-46).
            Debug.Log($"[MRStripper] 비활성화: {mb.GetType().Name} ({reason}) — {mb.gameObject.name} " +
                      $"[기동 후 {Time.timeSinceLevelLoad:F2}s]");
        }
    }

    // =========================================================
    // 감사 — 분류되지 않은 컴포넌트 경고
    // =========================================================
    // 데스크톱 팀이 새 매니저를 추가하면 여기서 걸린다.
    // 경고가 뜨면 위 세 목록 중 하나에 넣어 분류할 것.
    private void AuditUnclassified()
    {
        var unknown = new List<string>();

        foreach (string targetName in AuditTargetNames)
        {
            GameObject target = FindManagerObject(targetName);
            if (target == null)
            {
                Debug.LogWarning($"[MRStripper] 감사 대상 '{targetName}'을 찾지 못했습니다. 씬 구조가 바뀌었는지 확인하세요.");
                continue;
            }

            MonoBehaviour[] comps = target.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour mb in comps)
            {
                if (mb == null) continue;                       // 스크립트 누락(Missing)
                Type t = mb.GetType();
                if (_classified.Contains(t)) continue;
                if (Array.IndexOf(ReviewedKeepTypeNames, t.Name) >= 0) continue;
                unknown.Add($"{targetName}/{t.Name}");
            }
        }

        if (unknown.Count == 0)
        {
            Debug.Log("[MRStripper] 감사 완료 — 분류되지 않은 컴포넌트 없음.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[MRStripper] ⚠ 분류되지 않은 컴포넌트 {unknown.Count}개를 발견했습니다.");
        sb.AppendLine("데스크톱 쪽에 새로 추가된 것으로 보입니다. MR 호환성을 확인하고");
        sb.AppendLine("MRSceneStripper의 DesktopOnlyTypes 또는 ReviewedKeepTypes에 분류해 주세요.");
        foreach (string n in unknown) sb.AppendLine($"  - {n}");
        Debug.LogWarning(sb.ToString());
    }

    private GameObject FindManagerObject(string name)
    {
        // 비활성 오브젝트도 찾기 위해 전수 순회한다. 감사는 1회성이라 비용을 감수한다.
        Transform[] all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            if (t.name != name) continue;
            if (t.parent != null && t.parent.name == "Manager") return t.gameObject;
        }
        return null;
    }
}
