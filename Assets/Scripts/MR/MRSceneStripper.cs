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
        typeof(ScreenshotManager),
        typeof(ScreenshotOCRManager),
        typeof(ScreenshotOCRRectManager),
        typeof(OCRManager),
        typeof(OCRAutoMapManager),
        typeof(PIPManager),

        // --- 전역 핫키 (MR에 키보드 없음) ---
        typeof(HotkeyManager),                  // 주의: 파일명은 HotKeyManager.cs지만 클래스는 HotkeyManager
        typeof(HotKeyCatalogManager),
        typeof(HotKeyActionManager),

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

        // --- 캔버스 픽셀 좌표계에 묶인 이동 시스템 ---
        // PhysicsManager는 Win32를 쓰지 않지만 캐릭터를 RectTransform.anchoredPosition으로 걷게 한다.
        // moveSpeed = 120(픽셀/초)이 월드 공간에서는 초속 120m가 되어 캐릭터가 순식간에 날아가고,
        // anchoredPosition 쓰기가 매 프레임 localPosition.z = -70(캔버스 깊이 상수)을 되살려
        // 캐릭터를 카메라 뒤 70m로 밀어낸다. Quest 실측으로 확인된 문제다.
        // Phase 2에서 MRUK 바닥 위를 걷는 3D 이동으로 대체한다.
        typeof(PhysicsManager),
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
        typeof(NoticeManager), typeof(PortraitBalloonSimpleManager), typeof(TalkMenuManager),
        typeof(UIChatSituationManager), typeof(UIGame20QPanelManager), typeof(UIPositionManager),
        typeof(UIUserCardManager), typeof(EffectManager), typeof(ClickEffecter),

        // --- 서브 캐릭터 ---
        typeof(SubAnswerBalloonManager), typeof(SubAnswerBalloonSimpleManager),
        typeof(SubChatBalloonManager),

        // --- 디버그 (무해, 필요시 별도로 끈다) ---
        typeof(DebugBalloonManager), typeof(DebugBalloonManager2), typeof(DebugMenuManager),

        // --- 플랫폼 인지 있음 (Android 분기를 자체 보유) ---
        typeof(BackgroundService),

        // --- MenuTrigger: KAIManager가 MenuTriggerKAI로 교체한다 ---
        typeof(MenuTrigger),
        typeof(MenuTriggerKAI),   // KAIManager가 런타임에 부착한다 (감사 시점에 이미 존재)
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
    private static readonly string[] DisableObjectPaths =
    {
        "Root260616/Cameras/Main Camera",     // OVR Camera Rig의 CenterEyeAnchor가 대체
        "Root260616/Cameras/UI Camera",
        "Root260616/Cameras/Effect Camera",
        "Root260616/Legacy/PIP",              // PIP 카메라·캔버스
        "Root260616/Tester",                  // 개발용
        "Root260616/Manager/DevManager",      // 개발용
        "Root260616/Canvases/Canvas/PortraitMask/PortraitSystem/PortraitCamera",
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
    private int _disabledCount;

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
            }
        }

        // 서브 캐릭터 등 위 경로로 안 잡히는 경우를 위한 느린 안전망
        _safetyTimer -= Time.deltaTime;
        if (_safetyTimer <= 0f)
        {
            _safetyTimer = safetySweepInterval;
            SafetySweepCharacterComponents();
        }
    }

    // =========================================================
    // 씬 상주 매니저 비활성화
    // =========================================================
    private void StripSceneManagers()
    {
        // Awake 시점 1회만 전수 순회한다. 이후에는 돌지 않는다.
        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MonoBehaviour mb in all)
        {
            if (mb == null) continue;
            if (!_desktopOnly.Contains(mb.GetType())) continue;
            Disable(mb, "데스크톱 전용");
        }

        Debug.Log($"[MRStripper] 데스크톱 전용 컴포넌트 {_disabledCount}개를 비활성화했습니다.");
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

    // 캐릭터 교체 감지로 잡히지 않는 경우(서브 캐릭터 등)를 위한 느린 안전망.
    private void SafetySweepCharacterComponents()
    {
        foreach (Type t in CharacterDesktopTypes)
        {
            // 존재 여부만 먼저 확인해 배열 할당을 피한다.
            if (FindAnyObjectByType(t, FindObjectsInactive.Include) == null) continue;

            UnityEngine.Object[] found = FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (UnityEngine.Object o in found)
            {
                if (o is MonoBehaviour mb && mb.enabled) Disable(mb, "캐릭터 데스크톱 입력(안전망)");
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
            Debug.Log($"[MRStripper] 비활성화: {mb.GetType().Name} ({reason}) — {mb.gameObject.name}");
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
