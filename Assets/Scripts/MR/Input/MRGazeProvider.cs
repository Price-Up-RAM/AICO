// 머리 시선 조준 채널 (MR_Phase4A_Input_Plan.md §2-2)
//
// Quest 3/3S에는 아이트래킹이 없다. 머리 전방 레이를 쓴다(Port Plan §3-2a).
//
// 설계상 중요한 점 세 가지
// ----------------------
// 1) **정밀 레이캐스트가 아니라 원뿔 판정이다.** 캐릭터는 얇아서 정확한 조준을 요구하면
//    못 쓴다. 기본 12°. 구현은 SphereCast이고, 반경은 거리에 비례해 환산한다
//    (반경 = tan(원뿔각) x 거리) — 그래야 멀든 가깝든 각도 관대함이 일정하다.
//    판정 대상은 **캐릭터의 캡슐 콜라이더**(Char 레이어)다. 피벗 위치를 몰라도 된다.
// 2) **`Camera.main`을 그대로 믿지 않는다.** `CenterEyeAnchor`를 이름으로 먼저 찾는다
//    (Kickoff Guide §4-28 / MRLazyFollowHUD.ResolveEye와 같은 패턴).
// 3) **대상은 `MRCharacterWorldRoot.CurrentCharacter`다.**
//    `MRSpineCharacterController.Instances`가 아니다 — 그건 Spine 2D 프로토타입 전용이다(§4-14).
//
// palm-up 판정
// -----------
// 이 채널은 palm-up 자세일 때만 조준을 평가한다(Port Plan §2-1 진리표).
// 손바닥 법선은 관절에서 직접 구한다 — 검지·새끼 너클과 손목이 만드는 평면의 법선이다.
// **부호 규약은 SDK 버전·손 방향에 따라 뒤집힐 수 있으므로 실기 검증이 필요하다.**
// 뒤집혀 있으면 `invertPalmNormal`을 켜라. `logPalmDiagnostics`로 실측값을 볼 수 있다.

using UnityEngine;
using Oculus.Interaction.Input;

public class MRGazeProvider : MonoBehaviour, IMRAimProvider
{
    [Header("대상")]
    [Tooltip("비우면 씬에서 찾는다.")]
    [SerializeField] private MRCharacterWorldRoot characterRoot;

    [Header("조준")]
    [Tooltip("이 각도(도) 안에 캐릭터가 들어오면 조준한 것으로 본다. 정밀 조준을 요구하지 않는다.")]
    [SerializeField] private float coneAngleDegrees = 12f;

    [Tooltip("이 거리(m)를 넘으면 조준으로 치지 않는다.")]
    [SerializeField] private float maxDistance = 8f;

    [Tooltip("캐릭터 콜라이더가 있는 레이어. Nothing/Everything이면 런타임에 'Char'로 좁힌다.")]
    [SerializeField] private LayerMask characterLayers;

    [Tooltip("캐릭터가 아주 가까울 때의 최소 조준 반경(m). 거리에 비례한 반경이 0으로 수렴하는 것을 막는다.")]
    [SerializeField] private float minAimRadius = 0.05f;

    [Header("palm-up 게이트")]
    [Tooltip("이 채널을 여는 손. 비주력 손 palm-up이 기본 규약이다(§4-6).")]
    [SerializeField] private MRHandSide side = MRHandSide.Left;

    [Tooltip("손바닥 법선과 월드 up의 각도가 이 값(도) 안이면 palm-up으로 본다.")]
    [SerializeField] private float palmUpAngleDegrees = 50f;

    [Tooltip("손바닥 법선 부호가 반대로 나오면 켠다. 실기에서 확인할 것.")]
    [SerializeField] private bool invertPalmNormal;

    [Tooltip("palm-up 실측값을 로그로 찍는다. 임계값을 맞춘 뒤 끌 것.")]
    [SerializeField] private bool logPalmDiagnostics;

    [Tooltip("조준 각도 실측값을 로그로 찍는다. 시선 채널이 캐릭터를 못 잡을 때 켠다.")]
    [SerializeField] private bool logAimDiagnostics;

    private Transform _eye;
    private Hand _hand;
    private float _diagTimer;
    private float _aimDiagTimer;

    public bool IsChannelActive { get; private set; }
    public bool IsPressed { get; private set; }
    public MRAimResult Aim { get; private set; }
    public MRHandSide Side => side;
    public Vector3 PressPoint { get; private set; }

    private void Awake()
    {
        characterLayers = MRCharacterBounds.ResolveCharacterMask(characterLayers, this);
    }

    private void Update()
    {
        ResolveHand();

        // ① 핀치 판정과 ② 관절 포즈 조회를 분리한다 — 포즈 조회 실패가 핀치 판정까지
        //    망가뜨리지 않게 하기 위해서다(§4-19).
        IsPressed = ReadPinch();
        IsChannelActive = ReadPalmUp();
        PressPoint = ReadHandPoint();

        Aim = IsChannelActive ? EvaluateAim() : MRAimResult.None;
    }

    // ---------------------------------------------------------
    // 조준
    // ---------------------------------------------------------
    private MRAimResult EvaluateAim()
    {
        Transform eye = ResolveEye();
        if (eye == null) return MRAimResult.None;

        GameObject character = ResolveCharacter();
        if (character == null)
        {
            // 캐릭터가 없어도 "빈 공간 조준"은 유효하다 — 시스템 메뉴 경로가 살아 있어야 한다.
            return new MRAimResult
            {
                valid = true,
                onCharacter = false,
                point = eye.position + eye.forward * 2f
            };
        }

        // 판정 대상은 캐릭터의 **캡슐 콜라이더**다(Char 레이어).
        // 피벗이 발밑인지 허리인지, 렌더러가 어디까지인지 알 필요가 없어진다.
        //
        // 원뿔 12°(Port Plan §3-2a)는 그대로 유지하되, SphereCast가 각도가 아니라
        // 반경을 받으므로 **거리에 따라 환산**한다. 그래야 멀든 가깝든 각도 관대함이 일정하다.
        //   반경 = tan(원뿔각) × 캐릭터까지의 거리
        //   1 m → 0.21 m,  2 m → 0.42 m,  4 m → 0.85 m
        float distance = maxDistance;
        if (MRCharacterBounds.TryGet(characterRoot, out Bounds bounds))
        {
            distance = Vector3.Distance(eye.position, bounds.center);
        }

        float radius = Mathf.Tan(coneAngleDegrees * Mathf.Deg2Rad) * distance;
        if (radius < minAimRadius) radius = minAimRadius;

        bool hasHit = Physics.SphereCast(eye.position, radius, eye.forward,
                                         out RaycastHit hit, maxDistance, characterLayers);

        // Char 레이어에는 캐릭터만 있지만, 서브 캐릭터가 생길 수 있으므로 대상을 확인한다.
        bool onCharacter = hasHit && hit.collider.transform.IsChildOf(character.transform);

        LogAim(distance, radius, hasHit, hit, onCharacter);

        Vector3 point = eye.position + eye.forward * 2f;
        if (onCharacter) point = hit.point;

        return new MRAimResult
        {
            valid = true,
            onCharacter = onCharacter,
            point = point
        };
    }

    // ---------------------------------------------------------
    // 손
    // ---------------------------------------------------------
    private bool ReadPinch()
    {
        if (_hand == null || !_hand.IsTrackedDataValid) return false;
        return _hand.GetFingerIsPinching(HandFinger.Index);
    }

    private Vector3 ReadHandPoint()
    {
        if (_hand == null || !_hand.IsTrackedDataValid) return PressPoint;
        if (_hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip)) return tip.position;
        return PressPoint;
    }

    /// <summary>손바닥이 위를 향하는가. 관절 3개로 평면 법선을 만든다.</summary>
    private bool ReadPalmUp()
    {
        if (_hand == null || !_hand.IsTrackedDataValid) return false;

        if (!_hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist)) return false;
        if (!_hand.GetJointPose(HandJointId.HandIndex1, out Pose index)) return false;
        if (!_hand.GetJointPose(HandJointId.HandPinky1, out Pose pinky)) return false;

        Vector3 a = index.position - wrist.position;
        Vector3 b = pinky.position - wrist.position;
        if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) return false;

        Vector3 normal = Vector3.Cross(a, b).normalized;

        // 손 방향에 따라 외적 부호가 뒤집힌다. 왼손 기준으로 맞추고, 실기에서 어긋나면
        // invertPalmNormal로 뒤집는다.
        if (side == MRHandSide.Right) normal = -normal;
        if (invertPalmNormal) normal = -normal;

        float angle = Vector3.Angle(normal, Vector3.up);

        if (logPalmDiagnostics)
        {
            _diagTimer -= Time.unscaledDeltaTime;
            if (_diagTimer <= 0f)
            {
                _diagTimer = 0.5f;
                Debug.Log($"[MRGaze] palm 법선과 up의 각도 = {angle:F1}도 " +
                          $"(임계 {palmUpAngleDegrees}도, {(angle <= palmUpAngleDegrees ? "열림" : "닫힘")})");
            }
        }

        return angle <= palmUpAngleDegrees;
    }

    /// <summary>
    /// 조준 계측. SphereCast가 무엇을 맞혔는지 그대로 찍는다.
    ///
    /// 이 로그가 "맞힌 것 없음"인데 눈으로는 캐릭터를 보고 있다면, 원인은 조준 방식이 아니라
    /// **판정 부피**다 — 캡슐이 몸 전체를 감싸지 못하는 것이다. 그때는 반경을 키우지 말고
    /// 캡슐의 center/height를 고칠 것. (반경을 키우면 옆 빈 공간까지 캐릭터로 먹는다.)
    /// </summary>
    private void LogAim(float distance, float radius, bool hasHit, RaycastHit hit, bool onCharacter)
    {
        if (!logAimDiagnostics) return;

        _aimDiagTimer -= Time.unscaledDeltaTime;
        if (_aimDiagTimer > 0f) return;
        _aimDiagTimer = 0.5f;

        string what = "맞힌 것 없음";
        if (hasHit) what = $"'{hit.collider.name}' ({hit.distance:F2} m)";

        Debug.Log($"[MRGaze] 조준: 거리 {distance:F2} m, 반경 {radius:F2} m " +
                  $"(원뿔 {coneAngleDegrees}도) → {what}, 캐릭터={onCharacter}");
    }

    /// <summary>`Hand`는 씬에 정적으로 존재하지 않는다 — 런타임에 찾는다(§4-19).</summary>
    private void ResolveHand()
    {
        if (_hand != null) return;

        Handedness want = side == MRHandSide.Left ? Handedness.Left : Handedness.Right;

        Hand[] hands = FindObjectsByType<Hand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i].Handedness != want) continue;
            _hand = hands[i];
            return;
        }
    }

    private GameObject ResolveCharacter()
    {
        if (characterRoot == null) characterRoot = FindFirstObjectByType<MRCharacterWorldRoot>();
        if (characterRoot == null) return null;
        return characterRoot.CurrentCharacter;
    }

    /// <summary>MRLazyFollowHUD.ResolveEye와 같은 패턴 — 이름으로 CenterEyeAnchor를 먼저 찾는다.</summary>
    private Transform ResolveEye()
    {
        if (_eye != null) return _eye;

        var byName = GameObject.Find("CenterEyeAnchor");
        if (byName != null) { _eye = byName.transform; return _eye; }

        if (Camera.main != null) { _eye = Camera.main.transform; return _eye; }

        var any = FindFirstObjectByType<Camera>();
        if (any != null) _eye = any.transform;
        return _eye;
    }
}
