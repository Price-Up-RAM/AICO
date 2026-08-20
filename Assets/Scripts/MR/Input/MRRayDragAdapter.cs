// 캐릭터 원격 이동 — 캐릭터를 겨누고 탭 홀드한 채 끌면 따라온다
// (MR_Phase4A_Input_Plan.md §2-5, Port Plan §3-4).
//
// 데스크톱 DragHandler(화면좌표 드래그)의 MR 대체품이다 — MRSceneStripper 167행이
// 이 컴포넌트를 대체 대상으로 명시해 두었다.
//
// ⚠ 움직이는 대상은 캐릭터가 **아니라 픽셀 공간 래퍼**다 (Kickoff Guide §4-1).
// 캐릭터 트랜스폼을 직접 옮기면 픽셀 좌표계가 미터로 해석돼 수백 m 밖으로 날아간다.
// MRCharacterWorldRoot.SetCharacterPosition()이 내부에서 올바른 대상을 고르므로 그것만 부른다.
//
// 표현 계층은 새로 만들지 않는다
// ---------------------------
// 집어 올림 애니메이션은 이미 있다 — Animator의 isPick / BlendPick, StatusManager.IsPicking.
// DragHandler가 하던 것 중 **집기와 직접 관련된 것만** 가져온다. 감정풍선 Destroy,
// isPat 초기화, _animator.speed 복구 같은 뒷정리는 데스크톱 경로가 만든 상태를 되돌리는
// 것이라 MR에는 대응물이 없다.
//
// IsPicking을 세우는 것이 중요하다 — AnswerBalloonManager / ChatBalloonManager /
// PhysicsManager / FallingObject가 전부 이 플래그를 보고 자기 동작을 억제한다.
// 세우지 않으면 드래그 중에 말풍선이 뜬다.

using UnityEngine;

public class MRRayDragAdapter : MonoBehaviour
{
    [Header("참조 — 비우면 씬에서 찾는다")]
    [SerializeField] private MRIntentRouter router;
    [SerializeField] private MRCharacterWorldRoot characterRoot;

    [Header("착지점")]
    [Tooltip("캐릭터를 놓을 수 있는 최소/최대 거리(m).")]
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float maxDistance = 5f;

    [Header("이동 거리 배율 (UI 원격 이동과 동일)")]
    [Tooltip("손을 뻗고 당길 때 이동량의 배율 상한. 멀리 있을수록 커진다.")]
    [SerializeField] private float maxDistanceMultiplier = 12f;
    
    [Tooltip("카메라에 이보다 가까이는 오지 않는다(m).")]
    [SerializeField] private float minDistanceFromCamera = 0.25f;

    [Header("추종")]
    [Tooltip("지수 감쇠 계수(1/s). 클수록 빠르게 붙는다. 8~12 권장.")]
    [SerializeField] private float followSharpness = 10f;

    [Header("착지 링")]
    [SerializeField] private bool showLandingRing = true;
    [SerializeField] private int ringSegments = 48;
    [SerializeField] private float ringLineWidth = 0.006f;
    [SerializeField] private Color ringColor = new Color(0.35f, 0.85f, 1f, 0.9f);

    [Header("들어올림")]
    [Tooltip("집는 순간 캐릭터가 바닥에서 떠오르는 높이(m). 0이면 바닥을 긴다.")]
    [SerializeField] private float liftHeight = 0.25f;

    [Tooltip("켜면 손을 올린 만큼 캐릭터도 더 올라간다. 집을 때의 손 높이가 기준이다.")]
    [SerializeField] private bool followHandHeight = true;

    [Tooltip("손 높이로 추가할 수 있는 최대 높이(m).")]
    [SerializeField] private float maxHandLift = 0.8f;

    [Header("회전 — 손목 롤로 캐릭터 Y를 돌린다")]
    [SerializeField] private bool rotateWithHandRoll = true;

    [Tooltip("손목 롤 1도당 캐릭터 Y 회전 각도. 1이면 1:1.")]
    [SerializeField] private float rollToYawScale = 1f;

    [Tooltip("회전 방향이 반대로 느껴지면 켠다. 기본은 '시계 방향 → Y 음수'다.")]
    [SerializeField] private bool invertRollToYaw = false;

    [Tooltip("레이가 수직에 가까우면 롤의 기준이 사라진다. 이 각도 안쪽이면 회전을 쉰다.")]
    [SerializeField] private float minRollAxisTiltDegrees = 15f;

    [Header("안전")]
    [Tooltip("조준이 이 프레임 수만큼 연속으로 무효면 자동으로 놓는다. " +
             "손 트래킹이 끊겼는데 홀드 종료 이벤트가 안 오는 경우를 대비한다.")]
    [SerializeField] private int dropAfterInvalidFrames = 12;

    [Header("표현")]
    [Tooltip("BlendPick 축이 고르는 pick 클립 개수. Blend_Animation_Controller 기준 4개 " +
             "(2026-08-19 확인). 컨트롤러를 바꾸면 여기도 맞출 것.")]
    [SerializeField] private int pickClipCount = 4;

    [Header("진단")]
    [SerializeField] private bool logDrag = true;

    private bool _dragging;
    private MRRayProvider _dragProvider;
    private Oculus.Interaction.RayInteractor[] _disabledInteractors;
    private int _invalidFrames;
    private Vector3 _targetPosition;
    private float _footY;

    // 이동 대상(래퍼)의 로컬 좌표에서 본 캐릭터 접지점.
    // 캐릭터가 래퍼 안에서 (0,0,-70) 같은 오프셋에 있을 수 있어 래퍼 원점 ≠ 캐릭터 위치다.
    private Vector3 _localGroundOffset;

    // 거리 배율 변수 (UI 이동과 동일)
    private float _lengthAlongRay;   // 잡은 순간 레이 원점~캐릭터 거리
    private float _handDistance0;    // 잡은 순간 카메라~손(레이 원점) 거리
    private float _multiplier;       // 손 이동 → 이동량 배율

    private float _handYAtGrab;

    private bool _hasRollBaseline;
    private float _baselineRoll;
    private float _baselineYaw;
    private LineRenderer _ring;

    private Animator _animator;
    private GameObject _animatorOwner;

    private void OnEnable()
    {
        ResolveRefs();

        if (router == null)
        {
            Debug.LogWarning("[MRRayDrag] MRIntentRouter를 찾지 못했습니다 — 드래그가 동작하지 않습니다.");
            return;
        }

        router.OnCharacterHoldStarted += HandleHoldStarted;
        router.OnCharacterHoldEnded += HandleHoldEnded;
    }

    private void OnDisable()
    {
        if (router != null)
        {
            router.OnCharacterHoldStarted -= HandleHoldStarted;
            router.OnCharacterHoldEnded -= HandleHoldEnded;
        }

        // 구독이 끊긴 채로 잡고 있던 상태가 남으면 캐릭터가 영영 pick 포즈로 굳는다.
        if (_dragging)
        {
            EndDrag("컴포넌트 비활성화");
        }
    }

    private void OnDestroy()
    {
        MRRingRenderer.Dispose(_ring);
    }

    private void Update()
    {
        if (!_dragging) return;

        if (!TryResolveTarget(out Vector3 landing))
        {
            _invalidFrames++;
            if (_invalidFrames >= dropAfterInvalidFrames)
            {
                EndDrag("조준 소실");
            }
            return;
        }

        _invalidFrames = 0;
        _targetPosition = landing;

        Transform moveTarget = characterRoot.CharacterMoveTarget;
        if (moveTarget == null) return;

        // 회전을 **먼저** 건다. 회전이 접지점의 월드 위치를 바꾸므로,
        // 위치 보정은 회전이 끝난 뒤의 접지점을 기준으로 해야 한다.
        if (rotateWithHandRoll)
        {
            ApplyHandRoll(moveTarget);
        }

        // 들어올림 — 링(_targetPosition)은 바닥에 그대로 두고 캐릭터만 띄운다.
        // 내가 집었는데 캐릭터가 바닥을 기면 이상하다.
        Vector3 held = _targetPosition + Vector3.up * CurrentLift();

        // 지수 감쇠 — 프레임레이트가 흔들려도 체감 속도가 같다.
        Vector3 currentGround = moveTarget.TransformPoint(_localGroundOffset);
        float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        Vector3 nextGround = Vector3.Lerp(currentGround, held, k);

        // 접지점을 nextGround로 보내려면 래퍼를 그 차이만큼 옮긴다.
        characterRoot.SetCharacterPosition(moveTarget.position + (nextGround - currentGround));

        UpdateRing();
    }

    // =========================================================
    // 드래그 시작 / 종료
    // =========================================================
    private void HandleHoldStarted(MRRayProvider provider)
    {
        if (_dragging) return;

        ResolveRefs();
        if (characterRoot == null)
        {
            Debug.LogWarning("[MRRayDrag] MRCharacterWorldRoot가 없어 드래그를 시작할 수 없습니다.");
            return;
        }
        if (characterRoot.CurrentCharacter == null)
        {
            Debug.LogWarning("[MRRayDrag] 캐릭터가 아직 스폰되지 않았습니다.");
            return;
        }

        _dragProvider = provider;
        _dragging = true;
        // 캐릭터를 잡았을 때 ISDK 레이 인터랙터를 비활성화하여 UI가 동시에 잡히는 것을 방지
        var interactors = FindObjectsOfType<Oculus.Interaction.RayInteractor>(false);
        var disabledList = new System.Collections.Generic.List<Oculus.Interaction.RayInteractor>();
        foreach(var r in interactors)
        {
            if (r.enabled)
            {
                r.Unselect();
                r.enabled = false;
                disabledList.Add(r);
            }
        }
        _disabledInteractors = disabledList.ToArray();
        _invalidFrames = 0;

        _hasRollBaseline = false;

        // 래퍼 원점과 캐릭터 접지점의 어긋남을 여기서 한 번 잰다.
        //
        // 실기에서 캐릭터가 래퍼 안 로컬 (0,0,-70)에 있었다(2026-08-19). 그대로 두면
        // 착지 링은 래퍼가 갈 자리를, 캐릭터는 거기서 70만큼 뒤를 간다 —
        // 보이는 곳과 놓이는 곳이 달라진다.**
        // 로컬 좌표로 잡아두면 회전을 걸어도 오프셋이 같이 돈다.
        if (!CaptureGrabState())
        {
            Debug.LogWarning("[MRRayDrag] 캐릭터 경계를 잴 수 없어 드래그를 시작하지 않습니다.");
            _dragging = false;
            _dragProvider = null;
            return;
        }

        // 손 높이 기준점. followHandHeight가 이 값과의 차이만큼 캐릭터를 더 올린다.
        _handYAtGrab = provider.PressPoint.y;

        BeginPickPose();

        if (logDrag)
        {
            Debug.Log($"[MRRayDrag] 드래그 시작 — 발 높이 {_footY:0.00} m, 래퍼 오프셋 {_localGroundOffset}");
        }
    }

    private void HandleHoldEnded()
    {
        if (!_dragging) return;

        EndDrag("홀드 종료");
    }

    private void EndDrag(string reason)
    {
        _dragging = false;
        _dragProvider = null;
        _invalidFrames = 0;
        _hasRollBaseline = false;

        if (_disabledInteractors != null)
        {
            foreach (var r in _disabledInteractors)
            {
                if (r != null) r.enabled = true;
            }
            _disabledInteractors = null;
        }

        // 띄워둔 캐릭터를 링 자리(바닥)에 내려놓는다. 안 하면 공중에 뜬 채로 남는다.
        DropToGround();

        EndPickPose();
        HideRing();

        if (logDrag)
        {
            Debug.Log($"[MRRayDrag] 드래그 종료 — {reason}");
        }
    }

    // =========================================================
    // 착지점 (거리 배율 + 수직 낙하)
    // =========================================================
    private bool TryResolveTarget(out Vector3 landing)
    {
        landing = Vector3.zero;

        if (_dragProvider == null || characterRoot == null) return false;

        Ray ray;
        if (!_dragProvider.TryGetRay(out ray)) return false;

        Vector3 origin = ray.origin;
        Vector3 dir = ray.direction;
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();

        Camera cam = Camera.main;
        if (cam == null) return false;

        // UI 이동과 같은 공식: 손을 움직인 양 * 배율
        float handDistance = Vector3.Distance(cam.transform.position, origin);
        float length = _lengthAlongRay + (handDistance - _handDistance0) * _multiplier;

        // 너무 가깝거나 멀어지지 않게 제한
        length = Mathf.Max(length, minDistanceFromCamera);
        length = Mathf.Clamp(length, minDistance, maxDistance);

        // 레이 방향으로 계산된 위치
        Vector3 point = origin + dir * length;
        
        // 캐릭터는 항상 바닥에 놓여야 하므로 위치의 y만 고정한다.
        landing = new Vector3(point.x, _footY, point.z);
        return true;
    }

    // 래퍼 로컬 좌표에서 본 접지점을 잡아두고, 손 이동 배율을 계산한다.
    private bool CaptureGrabState()
    {
        Transform moveTarget = characterRoot.CharacterMoveTarget;
        if (moveTarget == null) return false;

        Bounds bounds;
        if (!MRCharacterBounds.TryGet(characterRoot, out bounds)) return false;

        Vector3 ground = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        // 발 높이는 시작 시점에 한 번만 고정한다.
        _footY = bounds.min.y;
        _localGroundOffset = moveTarget.InverseTransformPoint(ground);

        // 거리 배율 계산
        Ray ray;
        Camera cam = Camera.main;
        if (cam != null && _dragProvider != null && _dragProvider.TryGetRay(out ray))
        {
            Vector3 origin = ray.origin;
            Vector3 camPos = cam.transform.position;
            
            // 패널 이동과 동일한 공식 적용. 캐릭터의 기준은 방금 구한 ground 위치.
            _lengthAlongRay = Vector3.Distance(origin, ground);
            
            // 홀로그램을 잡은 경우, 진짜 캐릭터가 허공에서 잡힌 것으로 간주하여
            // 사용자가 원하는 초기 거리(약 2m)로 덮어씌운다.
            if (characterRoot.CurrentHologram != null && characterRoot.CurrentHologram.activeInHierarchy)
            {
                _lengthAlongRay = 2.0f;
            }

            _handDistance0 = Vector3.Distance(camPos, origin);

            float objectDistance0 = _lengthAlongRay; // 홀로그램 덮어쓰기 반영
            _multiplier = _handDistance0 > 0.01f
                ? Mathf.Clamp(objectDistance0 / _handDistance0, 1f, maxDistanceMultiplier)
                : 1f;
        }

        return true;
    }

    // 지금 프레임의 들어올림 높이.
    private float CurrentLift()
    {
        float lift = liftHeight;

        if (followHandHeight && _dragProvider != null)
        {
            float handRise = _dragProvider.PressPoint.y - _handYAtGrab;
            lift += Mathf.Clamp(handRise, 0f, maxHandLift);
        }

        return lift;
    }

    // 놓는 순간 바닥에 안착시킨다. 링이 가리키던 자리다.
    private void DropToGround()
    {
        if (characterRoot == null) return;

        Transform moveTarget = characterRoot.CharacterMoveTarget;
        if (moveTarget == null) return;

        Vector3 currentGround = moveTarget.TransformPoint(_localGroundOffset);
        Vector3 ground = new Vector3(currentGround.x, _footY, currentGround.z);

        characterRoot.SetCharacterPosition(moveTarget.position + (ground - currentGround));
    }

    // =========================================================
    // 손목 롤 → 캐릭터 Y 회전
    // =========================================================
    private void ApplyHandRoll(Transform moveTarget)
    {
        Pose pose;
        if (_dragProvider == null || !_dragProvider.TryGetPose(out pose))
        {
            _hasRollBaseline = false;
            return;
        }

        float roll;
        if (!TryComputeRoll(pose, out roll))
        {
            // 롤을 못 재는 자세다. 기준을 버려서, 다시 잴 수 있게 됐을 때 튀지 않게 한다.
            _hasRollBaseline = false;
            return;
        }

        if (!_hasRollBaseline)
        {
            _hasRollBaseline = true;
            _baselineRoll = roll;
            _baselineYaw = moveTarget.eulerAngles.y;
            return;
        }

        float delta = Mathf.DeltaAngle(_baselineRoll, roll) * rollToYawScale;
        if (invertRollToYaw)
        {
            delta = -delta;
        }

        // 시계 방향으로 돌리면 Y가 음수 방향으로 간다.
        float yaw = _baselineYaw - delta;

        // 래퍼는 항등 회전으로 생성되고 MR 캐릭터는 똑바로 서 있으므로 Y만 준다.
        moveTarget.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // 레이 축을 중심으로 손이 얼마나 돌아갔는가.
    //
    // 기준은 "월드 up을 레이 축에 수직인 평면에 투영한 방향"이다.
    // 레이가 수직에 가까우면 그 투영이 0으로 수렴해 각이 미친 듯이 튄다 — 그때는 실패로 돌린다.
    private bool TryComputeRoll(Pose pose, out float roll)
    {
        roll = 0f;

        Vector3 axis = pose.rotation * Vector3.forward;
        if (axis.sqrMagnitude < 0.000001f) return false;
        axis.Normalize();

        float tilt = Vector3.Angle(axis, Vector3.up);
        if (tilt < minRollAxisTiltDegrees) return false;
        if (tilt > 180f - minRollAxisTiltDegrees) return false;

        Vector3 reference = Vector3.ProjectOnPlane(Vector3.up, axis);
        if (reference.sqrMagnitude < 0.000001f) return false;

        Vector3 handUp = Vector3.ProjectOnPlane(pose.rotation * Vector3.up, axis);
        if (handUp.sqrMagnitude < 0.000001f) return false;

        roll = Vector3.SignedAngle(reference.normalized, handUp.normalized, axis);
        return true;
    }

    // =========================================================
    // 표현 — 기존 Animator 파라미터를 재사용한다
    // =========================================================
    private void BeginPickPose()
    {
        Animator animator = ResolveAnimator();
        if (animator != null)
        {
            // BlendPick은 pick 클립을 고르는 블렌드 축이다. 파라미터가 없는 캐릭터도 있으므로
            // 존재를 확인하고 넣는다 — DragHandler는 예외를 잡아 처리하고 있었다.
            if (HasParameter(animator, "BlendPick"))
            {
                animator.SetFloat("BlendPick", Random.Range(0, pickClipCount));
            }
            if (HasParameter(animator, "isPick"))
            {
                animator.SetBool("isPick", true);
            }
        }

        StatusManager.Instance.IsPicking = true;
    }

    private void EndPickPose()
    {
        Animator animator = ResolveAnimator();
        if (animator != null && HasParameter(animator, "isPick"))
        {
            animator.SetBool("isPick", false);
        }

        StatusManager.Instance.IsPicking = false;
    }

    private Animator ResolveAnimator()
    {
        GameObject character = null;
        if (characterRoot != null)
        {
            character = characterRoot.CurrentCharacter;
        }
        if (character == null) return null;

        // 캐릭터가 교체되면 캐시를 버린다.
        if (_animatorOwner != character)
        {
            _animatorOwner = character;
            _animator = character.GetComponentInChildren<Animator>(true);
        }

        return _animator;
    }

    private bool HasParameter(Animator animator, string parameterName)
    {
        if (animator == null) return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName) return true;
        }

        return false;
    }

    // =========================================================
    // 착지 링
    // =========================================================
    private void UpdateRing()
    {
        if (!showLandingRing)
        {
            HideRing();
            return;
        }

        Bounds bounds;
        if (!MRCharacterBounds.TryGet(characterRoot, out bounds))
        {
            HideRing();
            return;
        }

        float radius = MRCharacterBounds.GetHorizontalRadius(bounds);
        if (radius < 0.02f)
        {
            HideRing();
            return;
        }

        if (_ring == null)
        {
            _ring = MRRingRenderer.Create("MRDragLandingRing", ringLineWidth, ringColor);
        }

        MRRingRenderer.BuildCircle(_ring, _targetPosition, radius, ringSegments);
        _ring.enabled = true;
    }

    private void HideRing()
    {
        if (_ring == null) return;

        _ring.enabled = false;
    }

    private void ResolveRefs()
    {
        if (router == null) router = FindFirstObjectByType<MRIntentRouter>();
        if (characterRoot == null) characterRoot = FindFirstObjectByType<MRCharacterWorldRoot>();
    }
}
