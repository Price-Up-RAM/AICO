// 캐릭터 부착형 말풍선(8종)을 캐릭터 앞·사용자 눈높이에 띄우고 빌보드시킨다.
//
// 왜 8개 파일을 포크하지 않았는가
// ------------------------------
// ChatBalloonManager, AnswerBalloonManager, AnswerBalloonSimpleManager, AskBalloonManager,
// EmotionBalloonManager, NoticeBalloonManager, PortraitBalloonSimpleManager,
// SubAnswerBalloonManager, SubChatBalloonManager는 전부 데스크톱과 공유하는 파일이고,
// 전부 같은 패턴을 쓴다 — 자기 RectTransform의 anchoredPosition을
// "characterTransform.anchoredPosition + 오프셋" 으로 Update()/Show()에서 매 프레임 계산한다.
//
// 9개 파일을 각각 포크해 월드 트랜스폼 기준으로 다시 쓰는 대신, 이 컴포넌트 하나를
// 각 말풍선의 (독립 월드 스페이스 캔버스가 된) 루트에 붙이는 쪽을 택했다. 이유:
//   1) 원본 파일을 건드리지 않으므로 데스크톱 회귀 위험이 0이다 (Kickoff Guide §3 공용 스크립트 경고).
//   2) LateUpdate에서 최종 위치를 덮어쓰므로 원본의 anchoredPosition 계산과 충돌하지 않는다.
//   3) 캐릭터 참조 소스가 하나(MRCharacterWorldRoot)로 통일된다.
//
// 사용
// ----
// Tools → MR → 7. 선택 오브젝트를 캐릭터 부착 말풍선으로 변환
// (런타임에 Instantiate되는 EmotionBalloon 같은 경우 프리팹 자체에 미리 붙여둔다.)
//
// ============================================================================
// 2026-08-22 수정 — 네 가지를 고쳤다. 각각 실기 증상이 있었다.
// ============================================================================
//
// ① 말풍선이 캐릭터 무릎 높이에 떴다.
//    원인: worldOffset 기본값이 (0, 0.35, 0)인데 **VRM 캐릭터 루트의 원점은 발밑**이다
//          (§4-48 실측: 캡슐이 발끝 0.00 ~ 머리 1.66). 발밑 + 0.35 m = 무릎.
//    수정: 높이를 캐릭터 기준이 아니라 **사용자 눈높이 기준**으로 잡는다(useEyeHeight).
//          읽기 편한 위치는 "캐릭터의 수평 위치 + 내 눈높이"이지 "캐릭터 발밑 + n cm"가 아니다.
//
// ② 말풍선이 사용자를 등지고 떴다(180도 뒤집힘).
//    원인: **이 파일만 빌보드 부호가 반대였다.** 월드 스페이스 캔버스의 정면은 -Z이므로
//          (§4-20, PointableCanvasModule이 -canvas.transform.forward를 평면 법선으로 쓴다)
//          LookRotation에는 **패널 → 눈이 아니라 눈 → 패널** 방향을 줘야 한다.
//          검증된 세 컴포넌트는 전부 `LookRotation(자기위치 - 눈위치)`다:
//            MRFloatingPanel.FaceCameraYAxisOnly / MRPanelGrabTransformer / MRLazyFollowHUD.FaceEye
//          이 파일만 `LookRotation(눈위치 - 자기위치)`였다.
//    ⚠ Kickoff Guide §4-40이 "이 파일도 셋과 부호가 같다. 세 곳을 대조해 확인했다"고
//      적어놨는데 **문서가 틀렸다.** 같은 오브젝트에 붙은 MRPanelGrabTransformer는
//      올바른 부호였으므로, grab하는 순간 말풍선이 180도 홱 도는 증상이 함께 났을 것이다.
//
// ③ 캐릭터를 레이로 옮겨도 말풍선이 "캐릭터 앞"으로 돌아오지 않았다.
//    원인: 오프셋을 **소환 시 1회만** 계산해 잠갔다(_lockedOffset). 그 뒤로는 캐릭터에 대한
//          상대 오프셋이 고정되므로, 캐릭터가 이동하거나 사용자가 움직이면
//          더 이상 "캐릭터 앞"도 "눈높이"도 아니게 된다.
//    수정: 매 프레임 다시 계산하고, 사용자가 옮긴 뒤에도 **캐릭터가 크게 움직이면
//          재배치**한다(reArmMoveThreshold).
//
// ④ 말풍선을 레이/grab으로 옮기면 Y가 300으로 튀고 다시는 안 돌아왔다.
//    원인: UIPositionManager.GetBalloonAnchoredPosition이 "캐릭터보다 300px 위"를 돌려주는데
//          월드 캔버스에서는 그게 **300 m**다(§4-36/§4-38/§4-45 계열의 네 번째 재발).
//          AnswerBalloonSimpleManager 등 4개 매니저가 그 값을 Update()에서 매 프레임 대입한다.
//          평소에는 이 컴포넌트가 LateUpdate마다 transform.position을 덮어써서 지우고 있었는데,
//          grab이 시작되면 위치 소유권을 사용자에게 넘기느라 덮어쓰기를 멈춘다 → +300이 살아난다.
//    수정: **UIPositionManager 쪽에서 근본 차단**했다(MR 분기에서 Vector2.zero 반환).
//          공급원 한 곳을 막는 편이 호출부 4곳을 고치는 것보다 새는 곳이 없다.
//          그에 따라 여기 있던 "자식[0]의 anchoredPosition을 0으로 리셋" 방어 코드는 제거했다 —
//          **그 코드는 애초에 엇나가 있었다.** 씬 실측 결과 매니저가 쓰는 대상은
//          `answerBalloonSimpleTransform` = **말풍선 루트 자신**이었는데,
//          코드는 자식[0](= `Text (TMP)`)을 리셋하고 있어 한 번도 방어가 된 적이 없다.
//
// 한계
// ----
// 서브 캐릭터(Aropla 모드의 두 번째 캐릭터) 추적은 아직 다루지 않는다 — 메인 캐릭터
// (MRCharacterWorldRoot.CurrentCharacter)만 지원한다. 필요해지면 SetTarget으로 확장한다.

using Oculus.Interaction;
using UnityEngine;

public class MRBalloonWorldFollow : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("따라다닐 대상을 명시적으로 지정한다. 비워두면 MRCharacterWorldRoot의 " +
             "현재 메인 캐릭터를 자동으로 쓴다.")]
    [SerializeField] private Transform explicitTarget;

    [Header("배치")]
    [Tooltip("켜면 말풍선 높이를 **사용자 눈높이**로 맞춘다. 끄면 캐릭터 기준 worldOffset.y를 쓴다.\n" +
             "VRM 캐릭터 루트의 원점은 발밑이라(§4-48), 끄면 오프셋이 그대로 발밑 기준이 된다 — " +
             "0.35를 넣으면 무릎에 뜬다.")]
    [SerializeField] private bool useEyeHeight = true;

    [Tooltip("눈높이에서 얼마나 위/아래로 밀지(m). 0이면 정확히 눈높이. " +
             "살짝 내리면(-0.05 정도) 캐릭터 얼굴을 덜 가린다. useEyeHeight가 켜져 있을 때만 쓴다.")]
    [SerializeField] private float eyeHeightOffset = 0f;

    [Tooltip("캐릭터 기준 오프셋(m). useEyeHeight가 켜져 있으면 **y는 무시**되고 x/z만 쓴다.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);

    [Tooltip("캐릭터에서 사용자 쪽으로 이만큼 당겨 띄운다(m). '캐릭터 앞'을 만드는 값이다.")]
    [SerializeField] private float pullTowardUser = 0.15f;

    [Tooltip("홀로그램 모드일 때 위 오프셋들에 곱할 비율.")]
    [SerializeField] private float hologramOffsetScale = 0.3f;

    [Tooltip("캐릭터가 가려지지 않도록 항상 옆으로 비켜나는 오프셋(m). 0이면 비켜나지 않는다.")]
    [SerializeField] private float sideOffset = 0.45f;

    [Header("빌보드")]
    [Tooltip("Y축만 회전해 사용자를 향하게 한다. 완전 빌보드(모든 축)는 어지러워서 쓰지 않는다 " +
             "(MR_Phase3-2_Canvas_Plan.md §3-2-B step 4 확정 사항).")]
    [SerializeField] private bool billboardYOnly = true;

    [Header("추종")]
    [Tooltip("true면 매 프레임 계속 따라다닌다(기존 6종 말풍선). false면 활성화되는 " +
             "순간(소환 시점) 딱 한 번만 위치/회전을 맞추고 그 뒤로는 손대지 않는다.")]
    [SerializeField] private bool continuousFollow = true;

    [Tooltip("사용자가 옮겨둔 포즈를 매 프레임 복원해서 지킨다. **기본 켬.**\n" +
             "말풍선 매니저 4종의 anchoredPosition 대입을 2026-08-22에 막았으므로 " +
             "이 트랜스폼의 쓰기 주체는 이 컴포넌트 하나다.\n" +
             "끄면 '관찰 모드'가 되어 아무것도 쓰지 않는다 — 누가 트랜스폼을 쓰는지 볼 때 쓴다. " +
             "경쟁자가 다시 생기면 상대가 끌고 가고 내가 되돌리는 진동이 되므로, " +
             "발작이 보이면 이걸 끄고 logGrabDiagnostics로 상대를 먼저 특정할 것.")]
    [SerializeField] private bool holdUserPose = true;

    [Tooltip("사용자가 말풍선을 옮긴 뒤라도, **캐릭터가 이 거리(m) 이상 움직이면** " +
             "다시 캐릭터 앞·눈높이로 재배치한다. 0 이하면 재배치하지 않는다.\n" +
             "0.15 정도가 적당하다 — 트래킹 지터(수 mm)로는 발동하지 않고 " +
             "레이로 옮기면 확실히 발동한다. 실기에서 조정할 것.")]
    [SerializeField] private float reArmMoveThreshold = 0.15f;

    [Header("진단")]
    [Tooltip("배치가 갱신될 때마다 '지금 값 + 기준 값'을 한 줄로 찍는다 (§7-1 C). " +
             "위치가 이상할 때 켜고, 맞춘 뒤 끈다. 상태 전이 때만 찍으므로 스팸은 없다.")]
    [SerializeField] private bool logPlacement;

    [Tooltip("**말풍선이 흔들리거나 발작할 때 켠다.** 매 프레임 '누가 트랜스폼을 썼는가'를 찍는다.\n" +
             "이 컴포넌트가 쓴 위치와 다음 프레임 시작 위치가 다르면 = **다른 누군가가 같은 프레임에 덮어썼다**는 뜻이다.\n" +
             "범인 후보: 같은 오브젝트의 MRFloatingPanel(spawnBehavior가 KeepSavedPose가 아니면 배치한다), " +
             "MRPanelGrabTransformer, MRRayDistanceMovementProvider, 그리고 데스크톱 매니저의 anchoredPosition 대입.\n" +
             "프레임마다 찍히므로 확인 후 반드시 끌 것.")]
    [SerializeField] private bool logGrabDiagnostics;

    private static MRCharacterWorldRoot _worldRootCache;
    private Transform _eye;
    private bool _warnedNoTarget;
    private Vector3 _originalScale = Vector3.one;

    private Grabbable _grabbable;
    private bool _grabbableLookedUp;
    private bool _userTookOver;

    private bool _hasPlaced;
    private Vector3 _targetPosAtPlacement;

    private void Awake()
    {
        _originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        // 새로 소환될 때(닫았다 다시 열 때 포함)는 사용자가 옮겨둔 위치를 잊고
        // 다시 캐릭터 기준으로 배치한다 — "열면 캐릭터 앞에 뜨고, 내가 옮기면 그 자리에
        // 남는다"가 의도된 동작이다 (Phase3-2 Plan §3-2-B 소환 동작 확정).
        //
        // ⚠ OnEnable에만 의존하지 않는다. 매니저가 GameObject를 실제로 토글하지 않으면
        //    (§4-34: 이미 active인 오브젝트에 SetActive(true)는 no-op이라 OnEnable이 안 뜬다)
        //    이 리셋이 영영 안 돌기 때문이다. 그래서 캐릭터 이동 기반 재무장을 따로 뒀다.
        _userTookOver = false;
        _hasUserPose = false;
        _hasPlaced = false;

        // 소환 시점에 한 번은 항상 맞춰준다. continuousFollow=true인 경우에도
        // 첫 프레임 LateUpdate 전에 미리 맞춰서 초기 프레임 깜빡임을 줄인다.
        ApplyFollow("소환");
    }

    private void LateUpdate()
    {
        if (!continuousFollow) return;

        // 이 프레임이 시작될 때의 상태. 지난 프레임 끝에 내가 써둔 값과 다르면
        // **그 사이에 누군가 덮어쓴 것**이다 (§4-45 계열 진단).
        //
        // ⚠ 반드시 **내가 쓰기 전에** 읽는다. 쓴 뒤에 읽으면 내 값이 찍혀서
        //    상대의 정체가 영영 안 보인다 (2026-08-22에 이 실수를 두 번 했다).
        Vector3 posAtEntry = transform.position;
        Vector3 localAtEntry = transform.localPosition;
        Vector2 anchoredAtEntry = Vector2.zero;
        RectTransform rtEntry = transform as RectTransform;
        if (rtEntry != null) anchoredAtEntry = rtEntry.anchoredPosition;
        bool wroteThisFrame = false;
        string branch;

        // 잡고 있는 동안은 grab 시스템이 트랜스폼을 소유한다. 손대지 않고,
        // 매 프레임 결과를 기억해 둔다(놓는 순간의 포즈가 곧 사용자가 원한 자리다).
        if (IsBeingGrabbed())
        {
            if (!_userTookOver && logPlacement)
            {
                Debug.Log($"[MRBalloon/{name}] 사용자가 잡음 → 위치 소유권 이전");
            }
            _userTookOver = true;
            _userPosition = transform.position;
            _userRotation = transform.rotation;
            _hasUserPose = true;
            branch = "잡힘(양보)";
        }
        else if (_userTookOver)
        {
            // 사용자가 옮겨둔 상태라도, 캐릭터가 크게 움직였으면 다시 캐릭터 앞으로 데려온다.
            if (ShouldReArm())
            {
                _userTookOver = false;
                _hasUserPose = false;
                _hasPlaced = false;
                ApplyFollow("캐릭터이동 재배치");
                wroteThisFrame = true;
                branch = "재배치";
            }
            else if (holdUserPose && _hasUserPose)
            {
                // 사용자가 놓아둔 포즈를 매 프레임 복원한다.
                //
                // ⚠ 이건 **다른 누구도 이 트랜스폼에 쓰지 않는다는 전제**에서만 옳다.
                //    경쟁자가 남아 있으면 이 복원이 진동을 만든다 — 상대가 끌고 가고
                //    내가 되돌리는 것이 매 프레임 반복된다(2026-08-22 실기에서 발작으로 나타남).
                //    그래서 기본값을 끔으로 두고, 경쟁자를 제거한 뒤에 켠다.
                transform.position = _userPosition;
                transform.rotation = _userRotation;
                wroteThisFrame = true;
                branch = "사용자소유(포즈유지)";
            }
            else
            {
                // 관찰 모드: 아무것도 쓰지 않는다.
                // 내가 손을 떼면 말풍선은 **상대가 놓는 자리**에 가만히 있게 되고,
                // 그 값이 곧 상대의 정체다. 경쟁자를 특정하기 전까지 이쪽이 기본이다.
                branch = "사용자소유(관찰만)";
            }
        }
        else
        {
            ApplyFollow(null);
            wroteThisFrame = true;
            branch = "추종";
        }

        if (logGrabDiagnostics)
        {
            LogGrabDiagnostics(branch, posAtEntry, localAtEntry, anchoredAtEntry, wroteThisFrame);
        }

        _posAfterMyWrite = transform.position;
        _hasPrevFrame = true;
    }

    private Vector3 _posAfterMyWrite;
    private bool _hasPrevFrame;

    // 사용자가 grab으로 놓아둔 포즈. 양보 중에도 이 값을 매 프레임 복원해
    // 데스크톱 매니저의 anchoredPosition 대입을 무력화한다.
    private Vector3 _userPosition;
    private Quaternion _userRotation;
    private bool _hasUserPose;

    // "누가 트랜스폼을 썼는가"를 한 줄로 판정한다.
    //
    // 지난 프레임 끝에 내가 남긴 위치(_posAfterMyWrite)와 이번 프레임 시작 위치(posAtEntry)의
    // 차이가 곧 **나 아닌 누군가가 쓴 양**이다. 잡고 있지 않은데 이 값이 크면
    // 같은 오브젝트의 다른 컴포넌트가 경쟁하고 있다는 뜻이다.
    private void LogGrabDiagnostics(string branch, Vector3 posAtEntry, Vector3 localAtEntry,
                                    Vector2 anchoredAtEntry, bool wroteThisFrame)
    {
        float external = 0f;
        if (_hasPrevFrame) external = Vector3.Distance(_posAfterMyWrite, posAtEntry);

        int grabPoints = -1;
        if (_grabbable != null) grabPoints = _grabbable.GrabPoints.Count;

        // 잡기·판정 면 자식이 켜져 있는지. MRFloatingPanel.hideInteractionWhenTransparent가
        // alpha를 따라 이걸 토글하는데, 드래그 도중 꺼지면 grab이 끊겨 발작으로 보인다.
        string grabFrame = "없음";
        Transform gf = transform.Find("GrabFrame");
        if (gf != null)
        {
            if (gf.gameObject.activeSelf) grabFrame = "켜짐";
            else grabFrame = "꺼짐 ⚠";
        }

        float alpha = -1f;
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null) alpha = cg.alpha;

        // 상대가 어떤 API로 썼는지 지문을 남긴다.
        // anchoredPosition은 **z를 건드리지 않는다.** 그래서 z만 그대로이고 x/y가 바뀌었으면
        // 상대는 anchoredPosition(또는 localPosition의 x/y)을 쓴 것이다.
        // 반대로 z까지 바뀌었으면 월드 position을 통째로 쓴 것이다.
        string api = "-";
        if (_hasPrevFrame && external > 0.01f)
        {
            float dz = Mathf.Abs(posAtEntry.z - _posAfterMyWrite.z);
            if (dz < 0.001f) api = "anchoredPosition/localPos(x,y) ← z가 그대로다";
            else api = "월드 position 통째";
        }

        string verdict = "정상";
        if (external > 0.01f)
        {
            verdict = "⚠ 외부가 트랜스폼을 움직였다";
        }

        // 보간 구멍 안에서는 따옴표를 쓸 수 없다(C# 9). 서식은 미리 지역 변수로 만든다.
        const string F3 = "F3";
        string sEntryWorld = posAtEntry.ToString(F3);
        string sEntryLocal = localAtEntry.ToString(F3);
        string sEntryAnchored = anchoredAtEntry.ToString(F3);
        string sExitWorld = transform.position.ToString(F3);

        Debug.Log($"[MRBalloon/{name}] f{Time.frameCount} {branch} " +
                  $"| 외부움직인양 {external:F3}m (임계 0.010) | 내가씀={wroteThisFrame} " +
                  $"| 상대가쓴API {api}\n" +
                  $"    진입시점(내가 쓰기 전) — world {sEntryWorld} " +
                  $"| local {sEntryLocal} | anchored {sEntryAnchored}\n" +
                  $"    종료 world {sExitWorld} " +
                  $"| GrabPoints={grabPoints} | GrabFrame={grabFrame} | alpha={alpha:F2} | {verdict}");
    }

    // 캐릭터가 마지막 배치 시점에서 임계 이상 움직였는가.
    private bool ShouldReArm()
    {
        if (reArmMoveThreshold <= 0f) return false;
        if (!_hasPlaced) return true;

        Transform target = ResolveTarget(out bool ignored);
        if (target == null) return false;

        float moved = Vector3.Distance(target.position, _targetPosAtPlacement);
        return moved >= reArmMoveThreshold;
    }

    private bool IsBeingGrabbed()
    {
        if (!_grabbableLookedUp)
        {
            _grabbableLookedUp = true;
            _grabbable = GetComponent<Grabbable>();
        }

        return _grabbable != null && _grabbable.GrabPoints.Count > 0;
    }

    /// <summary>다시 캐릭터를 따라다니게 되돌린다 (예: 패널을 닫았다가 새로 소환할 때).</summary>
    public void ResumeFollowing()
    {
        _userTookOver = false;
        _hasUserPose = false;
        _hasPlaced = false;
    }

    // reason이 null이면 로그를 찍지 않는다(매 프레임 추종). 상태가 바뀔 때만 문자열을 넘긴다.
    private void ApplyFollow(string reason)
    {
        Transform eye = ResolveEye();

        Transform target = ResolveTarget(out bool isHologram);
        if (target == null)
        {
            if (!_warnedNoTarget)
            {
                _warnedNoTarget = true;
                Debug.LogWarning($"[MRBalloon] '{name}' 가 따라다닐 캐릭터를 찾지 못했습니다. " +
                                  "MRCharacterWorldRoot가 씬에 있고 캐릭터가 스폰됐는지 확인하세요.");
            }
            return;
        }

        Vector3 targetPos = target.position;

        // 홀로그램일 경우 스케일을 줄이고 오프셋도 함께 줄인다.
        float scaleMul = 1.0f;
        float offsetMul = 1.0f;
        if (isHologram)
        {
            scaleMul = 0.2f;
            offsetMul = hologramOffsetScale;
        }

        Vector3 desiredScale = _originalScale * scaleMul;
        if (transform.localScale != desiredScale)
        {
            transform.localScale = desiredScale;
        }

        // ---- 수평 위치: 캐릭터 위치에서 사용자 쪽으로 당긴다 ("캐릭터 앞") ----
        Vector3 offset = worldOffset * offsetMul;
        Vector3 newPos = targetPos + new Vector3(offset.x, 0f, offset.z);

        // 말풍선이 캐릭터를 가리지 않도록 눈 기준 오른쪽으로 항상 비켜난다.
        if (sideOffset != 0f && eye != null)
        {
            Vector3 sideDir = Vector3.Cross(Vector3.up, eye.position - targetPos);
            sideDir.y = 0f;
            if (sideDir.sqrMagnitude > 0.0001f)
            {
                newPos += sideDir.normalized * (sideOffset * offsetMul);
            }
        }

        if (eye != null)
        {
            Vector3 dirToEye = eye.position - targetPos;
            dirToEye.y = 0f;
            if (dirToEye.sqrMagnitude > 0.001f)
            {
                dirToEye.Normalize();
                newPos += dirToEye * (pullTowardUser * offsetMul);
            }
        }

        // ---- 높이 ----
        // 캐릭터 루트는 발밑이 원점이다(§4-48). 캐릭터 기준으로 잡으면 읽기 힘든 높이가 된다.
        float heightBase;
        string heightMode;
        if (useEyeHeight && eye != null)
        {
            newPos.y = eye.position.y + eyeHeightOffset;
            heightBase = eye.position.y;
            heightMode = "눈높이";
        }
        else
        {
            newPos.y = targetPos.y + offset.y;
            heightBase = targetPos.y;
            heightMode = "캐릭터기준";
        }

        transform.position = newPos;

        // ---- 회전 ----
        // 월드 스페이스 캔버스의 정면은 -Z다(§4-20). 그래서 LookRotation에 주는 방향은
        // **눈 → 패널**이다. 부호를 뒤집으면 정확히 180도 돌아 등을 보인다.
        // 검증된 셋(MRFloatingPanel / MRPanelGrabTransformer / MRLazyFollowHUD)과 부호를 맞췄다.
        if (eye != null)
        {
            Vector3 dir = transform.position - eye.position;
            if (billboardYOnly) dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        _targetPosAtPlacement = targetPos;
        _hasPlaced = true;

        if (logPlacement && reason != null)
        {
            string eyeName = "없음";
            if (eye != null) eyeName = eye.name;

            Debug.Log($"[MRBalloon/{name}] {reason} | 결과위치 {newPos} " +
                      $"| 높이모드 {heightMode} (기준 y={heightBase:F2} → 결과 y={newPos.y:F2}) " +
                      $"| 캐릭터 발밑 y={targetPos.y:F2} " +
                      $"| 당김 {pullTowardUser:F2}m | 재무장임계 {reArmMoveThreshold:F2}m " +
                      $"| 눈={eyeName} | 홀로그램={isHologram}");
        }
    }

    /// <summary>빌보드·높이 기준이 되는 "눈" 트랜스폼.
    ///
    /// Camera.main을 그대로 쓰지 않는다 — 이 씬은 LeftEyeAnchor와 CenterEyeAnchor가 둘 다
    /// MainCamera 태그이고, Camera.main이 null인 프레임도 있다(§4-28).
    /// MRFloatingPanel.ResolveEye / MRLazyFollowHUD.ResolveEye와 같은 패턴이다.</summary>
    private Transform ResolveEye()
    {
        if (_eye != null) return _eye;

        GameObject center = GameObject.Find("CenterEyeAnchor");
        if (center != null)
        {
            _eye = center.transform;
            return _eye;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            _eye = cam.transform;
            return _eye;
        }

        // 눈을 못 찾아도 위치는 잡는다 — 회전만 포기한다.
        // "못 찾으면 그냥 return"은 말풍선을 영영 안 보이게 만든다(§4-29).
        return null;
    }

    private Transform ResolveTarget(out bool isHologram)
    {
        isHologram = false;
        if (explicitTarget != null) return explicitTarget;

        if (_worldRootCache == null)
        {
            _worldRootCache = FindFirstObjectByType<MRCharacterWorldRoot>();
        }

        if (_worldRootCache != null)
        {
            if (_worldRootCache.CurrentHologram != null && _worldRootCache.CurrentHologram.activeInHierarchy)
            {
                isHologram = true;
                return _worldRootCache.CurrentHologram.transform;
            }

            if (_worldRootCache.CurrentCharacter != null)
            {
                return _worldRootCache.CurrentCharacter.transform;
            }
        }

        return null;
    }

    /// <summary>서브 캐릭터 등으로 대상을 런타임에 바꿔야 할 때 호출한다.</summary>
    public void SetTarget(Transform t)
    {
        explicitTarget = t;
        _warnedNoTarget = false;
        _hasPlaced = false;
    }
}
