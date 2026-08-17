// 월드 스페이스 UI 패널을 손으로 잡아 옮기는 컴포넌트.
// 메타 홈 화면의 패널 조작 방식을 따른다 — 패널 **바깥 테두리(프레임)**를 주먹으로 쥐어(grab)
// 옮기고, 멀리서는 레이로 그 테두리를 겨눠 잡는다. 패널 안쪽(내용물/버튼)은 UI 상호작용
// 전용이라 잡히지 않는다.
//
// 왜 ISDK Grabbable/HandGrabInteractable을 안 쓰는가
// -----------------------------------------------
// 버튼 클릭과 같은 포인터 이벤트 경로를 공유하면 §4-15(UIResizeHandler가 ISDK 드래그를
// 가로채 리사이즈로 오발동)와 같은 충돌이 재발할 위험이 있다. 손 관절을 직접 읽어
// UI 이벤트 시스템과 완전히 분리된 입력 경로를 쓴다.
//
// 왜 OVRInput이 아니라 Oculus.Interaction.Input.Hand인가
// ----------------------------------------------------
// 순수 핸드트래킹에서 OVRInput 트리거 축은 항상 0이다 (Kickoff Guide §4-19).
//
// 왜 안쪽이 아니라 "바깥" 테두리인가
// -------------------------------
// 안쪽 테두리로 하면 패널 가장자리에 붙은 버튼(닫기 버튼 등)과 잡기 영역이 겹친다.
// 바깥 패딩(패널 rect 밖)으로 빼면 UI 영역과 물리적으로 분리돼 충돌이 원천적으로 없다.
// 그래도 겹치는 경우(가장자리 밖으로 삐져나온 버튼)를 대비해 uiPriority 옵션으로
// 해당 지점에 Selectable이 있으면 잡기를 포기하도록 한다.
//
// 사용
// ----
// 옮길 패널에 붙이고 panelRect에 그 패널의 RectTransform을 지정한다.
// MRBalloonWorldFollow와 함께 쓸 때는 continuousFollow=false로 둔다.

using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.UI;

public class MRPinchDraggable : MonoBehaviour
{
    public enum GraspMode
    {
        GrabOnly,   // 주먹 쥐기만 (메타 홈 방식)
        PinchOnly,  // 엄지+검지 핀치만
        Either,     // 둘 중 아무거나
    }

    [Tooltip("옮길 대상. 비워두면 자기 자신.")]
    [SerializeField] private Transform target;

    [Tooltip("잡기 영역 계산 기준이 되는 패널. 비워두면 자기 RectTransform.")]
    [SerializeField] private RectTransform panelRect;

    [Header("잡기 방식")]
    [SerializeField] private GraspMode graspMode = GraspMode.GrabOnly;

    [Tooltip("핀치 판정 임계값 (Pinch 모드에서만)")]
    [SerializeField] private float pinchThreshold = 0.5f;

    [Tooltip("주먹 쥠 판정 — 손가락 끝이 손목에 이만큼(m) 가까워지면 쥔 것으로 본다.")]
    [SerializeField] private float grabCurlDistance = 0.09f;

    [Header("잡기 영역 — 패널 '바깥' 테두리")]
    [Tooltip("패널 바깥으로 이만큼(m) 두께의 프레임이 잡기 영역이 된다. " +
             "메타 홈처럼 패널 테두리를 감싸는 띠 모양.")]
    [SerializeField] private float outerPadding = 0.06f;

    [Tooltip("패널 안쪽으로도 이만큼(m)까지는 잡기를 허용한다. 0이면 완전히 바깥만. " +
             "테두리 그래픽이 두꺼운 패널에서 약간 안쪽까지 잡히게 하고 싶을 때 쓴다.")]
    [SerializeField] private float innerBleed = 0.01f;

    [Tooltip("패널 표면으로부터 앞뒤 허용 거리(m) — 근접(손) 잡기에만 적용.")]
    [SerializeField] private float depthMargin = 0.06f;

    [Header("원거리(레이) 잡기")]
    [SerializeField] private bool enableRayGrasp = true;
    [SerializeField] private float rayMaxDistance = 5f;

    [Header("UI 우선")]
    [Tooltip("잡으려는 지점에 버튼 등 Selectable이 있으면 잡기를 포기하고 UI에 양보한다.")]
    [SerializeField] private bool uiPriority = true;

    [Header("회전")]
    [Tooltip("Z축(롤) 회전을 0으로 고정한다. 손목을 기울여도 패널이 갸우뚱하지 않는다.")]
    [SerializeField] private bool lockRollRotation = true;

    [Header("Hand 참조 (비워두면 런타임에 자동 탐색)")]
    [SerializeField] private Hand leftHand;
    [SerializeField] private Hand rightHand;

    [Header("디버그")]
    [SerializeField] private bool verboseLog = false;

    private bool _isDragging;
    private bool _draggingLeft;
    private bool _draggingViaRay;
    private Vector3 _grabPointLocal;      // 잡은 지점(패널 로컬) — 회전 보정 후에도 손에 붙어 있게 하는 기준
    private Quaternion _dragRotationOffset;
    private float _rayGrabDistance;       // 레이로 잡았을 때 유지할 거리
    private bool _wasGraspingLeft;
    private bool _wasGraspingRight;

    private float _lastHandSearchTime;
    private float _lastLogTime;
    private readonly List<Selectable> _selectableCache = new List<Selectable>();
    private float _lastSelectableRefresh;

    public bool IsDragging => _isDragging;

    private void Awake()
    {
        if (target == null) target = transform;
        if (panelRect == null) panelRect = transform as RectTransform;
        FindHandsIfNeeded();
    }

    // LateUpdate인 이유: ChatBalloonManager 등 원본(데스크톱 공유) 매니저가 자기 Update()에서
    // 매 프레임 anchoredPosition을 다시 계산해 덮어쓴다. 같은 Update() 단계에 있으면 실행 순서가
    // 보장되지 않아 "잡히는 느낌은 나는데 안 움직이는" 현상이 생긴다 (실기 확인, 2026-08-11).
    private void LateUpdate()
    {
        if (target == null) return;
        FindHandsIfNeeded();

        ResolveHandState(true, out HandState left);
        ResolveHandState(false, out HandState right);

        if (!_isDragging)
        {
            TryBeginDrag(left, right);
            return;
        }

        HandState cur = _draggingLeft ? left : right;
        if (!cur.valid || !cur.grasping)
        {
            _isDragging = false;
            if (verboseLog) Debug.Log($"[MRPinchDraggable] '{name}' 놓음. pos={target.position}");
            return;
        }

        ApplyDrag(cur);
    }

    // =====================================================================
    // 손 상태
    // =====================================================================
    private struct HandState
    {
        public bool valid;
        public bool grasping;
        public Vector3 graspPoint;    // 근접 잡기 기준점 (핀치=검지끝, 그랩=손바닥 근처)
        public Vector3 anchorPos;     // 드래그를 따라갈 기준 위치
        public Quaternion anchorRot;
        public bool hasRay;
        public Vector3 rayOrigin;
        public Vector3 rayDir;
    }

    private void ResolveHandState(bool isLeft, out HandState state)
    {
        state = default;
        Hand hand = isLeft ? leftHand : rightHand;
        if (hand == null || !hand.IsTrackedDataValid) return;

        state.valid = true;

        bool pinching = hand.GetFingerIsPinching(HandFinger.Index);
        bool grabbing = IsFistClosed(hand);

        switch (graspMode)
        {
            case GraspMode.GrabOnly: state.grasping = grabbing; break;
            case GraspMode.PinchOnly: state.grasping = pinching; break;
            default: state.grasping = grabbing || pinching; break;
        }

        // 기준점: 손바닥(중지 첫 마디)이 주먹 쥘 때 가장 안정적이다.
        // 핀치 전용 모드에서는 검지 끝이 직관적이다.
        HandJointId anchorJoint = graspMode == GraspMode.PinchOnly
            ? HandJointId.HandIndexTip
            : HandJointId.HandMiddle1;

        if (hand.GetJointPose(anchorJoint, out Pose p))
        {
            state.graspPoint = p.position;
            state.anchorPos = p.position;
            state.anchorRot = p.rotation;
        }
        else if (hand.GetJointPose(HandJointId.HandWristRoot, out Pose wp))
        {
            state.graspPoint = wp.position;
            state.anchorPos = wp.position;
            state.anchorRot = wp.rotation;
        }
        else
        {
            state.valid = false;
            return;
        }

        if (enableRayGrasp && hand.GetPointerPose(out Pose pointer))
        {
            state.hasRay = true;
            state.rayOrigin = pointer.position;
            state.rayDir = pointer.rotation * Vector3.forward;
        }
    }

    /// <summary>주먹을 쥐었는지 — 손가락 끝들이 손목 쪽으로 말려 들어왔는지로 판정한다.
    /// HandGrabAPI는 씬에 정적으로 존재하지 않을 수 있어(리그가 런타임 생성) 관절만으로 계산한다.
    ///
    /// 검지를 반드시 포함해 **네 손가락 전부** 말려야 주먹으로 본다.
    /// 실기 확인(2026-08-15): 중지·약지·소지만 보면 "버튼 누르려고 검지만 편 손"이
    /// 주먹으로 오인된다 — 가리키는 손도 나머지 세 손가락은 말려 있기 때문이다.</summary>
    private bool IsFistClosed(Hand hand)
    {
        if (!hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist)) return false;

        HandJointId[] tips =
        {
            HandJointId.HandIndexTip,
            HandJointId.HandMiddleTip,
            HandJointId.HandRingTip,
            HandJointId.HandPinkyTip,
        };

        int counted = 0;
        foreach (var tip in tips)
        {
            if (!hand.GetJointPose(tip, out Pose tp)) continue;
            counted++;
            if (Vector3.Distance(tp.position, wrist.position) >= grabCurlDistance) return false;
        }

        return counted == tips.Length;
    }

    // =====================================================================
    // 잡기 시작
    // =====================================================================
    private void TryBeginDrag(HandState left, HandState right)
    {
        // 상승 엣지만 인정한다 — 이미 쥔 채로 영역에 들어오는 건 무시하고,
        // 영역 안에서 "쥐는 순간"에만 잡힌다. 실기 확인(2026-08-15): 이게 없으면
        // 주먹 쥔 채 지나가기만 해도 패널이 딸려온다.
        bool leftRising = left.valid && left.grasping && !_wasGraspingLeft;
        bool rightRising = right.valid && right.grasping && !_wasGraspingRight;

        _wasGraspingLeft = left.valid && left.grasping;
        _wasGraspingRight = right.valid && right.grasping;

        if (leftRising && TryBeginWith(left, true)) return;
        if (rightRising) TryBeginWith(right, false);
    }

    private bool TryBeginWith(HandState h, bool isLeft)
    {
        if (!h.valid || !h.grasping) return false;

        // 1) 근접 — 손이 테두리 띠 안에 들어와 있는가
        if (IsInGraspBand(h.graspPoint, true, out string nearReason))
        {
            if (uiPriority && IsOverSelectable(h.graspPoint))
            {
                Log($"근접 잡기 포기 — UI 우선 (버튼 위)");
                return false;
            }
            BeginDrag(h, isLeft, false);
            Log($"근접 잡기 시작({(isLeft ? "L" : "R")}) {nearReason}");
            return true;
        }

        // 2) 원거리 — 레이가 테두리 띠를 맞히는가
        if (enableRayGrasp && h.hasRay &&
            RaycastPanelPlane(h.rayOrigin, h.rayDir, out Vector3 hit, out float dist) &&
            dist <= rayMaxDistance &&
            IsInGraspBand(hit, false, out string rayReason))
        {
            if (uiPriority && IsOverSelectable(hit))
            {
                Log($"레이 잡기 포기 — UI 우선 (버튼 위)");
                return false;
            }
            BeginDrag(h, isLeft, true, dist);
            Log($"레이 잡기 시작({(isLeft ? "L" : "R")}) dist={dist:F2}m {rayReason}");
            return true;
        }

        return false;
    }

    private void BeginDrag(HandState h, bool isLeft, bool viaRay, float rayDist = 0f)
    {
        _isDragging = true;
        _draggingLeft = isLeft;
        _draggingViaRay = viaRay;
        _rayGrabDistance = rayDist;

        // 잡은 지점을 **패널 로컬 좌표**로 기억한다.
        // 이전에는 "손 기준 오프셋"으로 저장했는데, 롤 고정 때문에 실제 적용 회전이
        // 손 회전과 달라지면 오프셋만 손 회전으로 돌아가 위치가 어긋났다
        // (Y축으로 돌리면 패널이 손에서 떨어져 나가는 현상 — 실기 확인 2026-08-15).
        // 패널 로컬로 저장하면 회전을 어떻게 보정하든 "잡은 지점이 손에 온다"를
        // 항상 정확히 만족시킬 수 있다.
        Vector3 anchorWorld = viaRay ? h.rayOrigin + h.rayDir * rayDist : h.anchorPos;
        _grabPointLocal = target.InverseTransformPoint(anchorWorld);

        _dragRotationOffset = Quaternion.Inverse(h.anchorRot) * target.rotation;
    }

    private void ApplyDrag(HandState h)
    {
        Quaternion newRot = h.anchorRot * _dragRotationOffset;

        if (lockRollRotation)
        {
            // 롤(Z)만 제거 — 오일러를 직접 만지면 짐벌로 튀므로, forward는 유지하고
            // up만 월드 Y로 다시 세운다.
            Vector3 fwd = newRot * Vector3.forward;
            if (fwd.sqrMagnitude > 0.0001f) newRot = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // 회전을 먼저 확정한 뒤, 그 회전 기준으로 "잡은 지점이 손에 정확히 오도록"
        // 위치를 역산한다. 순서가 중요하다 — TransformPoint가 방금 적용한 회전을 쓴다.
        target.rotation = newRot;

        Vector3 handWorld = (_draggingViaRay && h.hasRay)
            ? h.rayOrigin + h.rayDir * _rayGrabDistance
            : h.anchorPos;

        target.position += handWorld - target.TransformPoint(_grabPointLocal);
    }

    // =====================================================================
    // 영역 판정
    // =====================================================================

    /// <summary>패널 평면과 레이의 교점을 구한다.</summary>
    private bool RaycastPanelPlane(Vector3 origin, Vector3 dir, out Vector3 hit, out float distance)
    {
        hit = Vector3.zero;
        distance = 0f;
        if (panelRect == null) return false;

        // 캔버스 정면은 -Z 관례 (Kickoff Guide §4-20)
        Plane plane = new Plane(-panelRect.forward, panelRect.position);
        Ray ray = new Ray(origin, dir);

        if (!plane.Raycast(ray, out float enter))
        {
            // 뒷면에서 들어오는 경우도 허용한다 (패널 뒤에서 겨눌 수 있게)
            Plane back = new Plane(panelRect.forward, panelRect.position);
            if (!back.Raycast(ray, out enter)) return false;
        }

        distance = enter;
        hit = ray.GetPoint(enter);
        return true;
    }

    /// <summary>월드 점이 "패널 바깥 테두리 띠" 안에 있는가.</summary>
    private bool IsInGraspBand(Vector3 worldPoint, bool checkDepth, out string reason)
    {
        reason = "";
        if (panelRect == null) return false;

        Vector3 local = panelRect.InverseTransformPoint(worldPoint);
        Rect r = panelRect.rect;

        float lossy = Mathf.Abs(panelRect.lossyScale.x);
        if (lossy < 1e-7f) { reason = "lossyScale 0"; return false; }
        float unitsPerMeter = 1f / lossy;

        float outUnits = outerPadding * unitsPerMeter;
        float inUnits = innerBleed * unitsPerMeter;

        if (checkDepth)
        {
            float depthUnits = depthMargin * unitsPerMeter;
            if (Mathf.Abs(local.z) > depthUnits) { reason = "깊이 초과"; return false; }
        }

        // 바깥 경계 = rect + outerPadding
        bool insideOuter =
            local.x >= r.xMin - outUnits && local.x <= r.xMax + outUnits &&
            local.y >= r.yMin - outUnits && local.y <= r.yMax + outUnits;
        if (!insideOuter) { reason = "띠 바깥"; return false; }

        // 안쪽 경계 = rect - innerBleed. 이 안이면 UI 영역이므로 잡기 금지.
        bool insideInner =
            local.x > r.xMin + inUnits && local.x < r.xMax - inUnits &&
            local.y > r.yMin + inUnits && local.y < r.yMax - inUnits;
        if (insideInner) { reason = "패널 내부(UI 영역)"; return false; }

        reason = $"띠 안 local=({local.x:F0},{local.y:F0})";
        return true;
    }

    /// <summary>해당 월드 지점이 활성 Selectable(버튼 등) 위인가 — UI에 양보하기 위한 검사.</summary>
    private bool IsOverSelectable(Vector3 worldPoint)
    {
        RefreshSelectableCache();

        foreach (var s in _selectableCache)
        {
            if (s == null || !s.isActiveAndEnabled || !s.interactable) continue;

            RectTransform rt = s.transform as RectTransform;
            if (rt == null) continue;

            Vector3 local = rt.InverseTransformPoint(worldPoint);
            if (rt.rect.Contains(new Vector2(local.x, local.y))) return true;
        }
        return false;
    }

    private void RefreshSelectableCache()
    {
        // 자식이 런타임에 생기고 사라질 수 있으므로 주기적으로 갱신한다.
        if (Time.time - _lastSelectableRefresh < 1f && _selectableCache.Count > 0) return;
        _lastSelectableRefresh = Time.time;

        _selectableCache.Clear();
        GetComponentsInChildren(true, _selectableCache);
    }

    // =====================================================================
    private void FindHandsIfNeeded()
    {
        if (leftHand != null && rightHand != null) return;
        if (Time.time - _lastHandSearchTime < 2f) return;
        _lastHandSearchTime = Time.time;

        Hand[] hands = FindObjectsByType<Hand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var h in hands)
        {
            if (h == null) continue;
            if (leftHand == null && h.Handedness == Handedness.Left) leftHand = h;
            if (rightHand == null && h.Handedness == Handedness.Right) rightHand = h;
        }
    }

    private void Log(string msg)
    {
        if (!verboseLog) return;
        if (Time.time - _lastLogTime < 0.3f) return;
        _lastLogTime = Time.time;
        Debug.Log($"[MRPinchDraggable] '{name}' {msg}");
    }
}
