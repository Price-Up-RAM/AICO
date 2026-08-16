// 손으로 잡아서(핀치) 오브젝트를 자유롭게 옮기는 기능만 담당하는 최소 컴포넌트.
//
// MRFloatingPanel.cs의 드래그 로직(핀치 반경 판정, 카메라 기준 거리 유지)을 재사용하되,
// Open/Close/소환 책임은 지지 않는다 — MRBalloonWorldFollow처럼 이미 초기 배치를
// 담당하는 컴포넌트가 붙은 오브젝트에 나란히 붙여서 "사용자가 잡아서 옮기는" 기능만 추가한다.
//
// 실기 확인(Quest 3S, 2026-08-11): OVRInput.Axis1D.Primary/SecondaryIndexTrigger는
// 컨트롤러가 연결되지 않은 순수 핸드트래킹 상태(activeController=Hands, connected=False/False)
// 에서는 항상 0을 반환한다 — 핀치를 해도 절대 안 움직인다는 게 이 프로젝트 실기에서 확인된
// 사실이다. MRFloatingPanel도 같은 방식을 쓰고 있어서 순수 핸드트래킹으로는 똑같이
// 안 먹혔을 가능성이 크다(§4-18 참고). 그래서 이 컴포넌트는 MRSpatialAnchorEditor.cs가
// 이미 쓰고 있는 확인된 패턴 — Interaction SDK의 Oculus.Interaction.Input.Hand로
// GetFingerIsPinching(HandFinger.Index) / GetJointPose(HandJointId.HandIndexTip, ...) —
// 를 1순위로 쓰고, Hand를 못 찾거나 트래킹이 안 잡힐 때만 OVRInput(컨트롤러)로 폴백한다.
//
// Hand 컴포넌트를 인스펙터에 미리 드래그해둘 수 없는 이유
// --------------------------------------------------
// 이 씬(SampleSceneKAI-MR.unity)에는 정적으로 배치된 Hand/OVRHand 컴포넌트가 하나도 없다
// (Meta Building Block 카메라 리그가 런타임에 상호작용 리그를 동적으로 구성하는 것으로 보임).
// 그래서 에디터에서 미리 참조를 걸 수 없고, 런타임에 FindObjectsByType<Hand>()로 찾아
// Handedness로 좌/우를 매칭한다(최초 1회, 실패하면 몇 초 뒤 재시도).
//
// 왜 중심 반경이 아니라 "테두리"만 잡히게 하는가
// ------------------------------------------
// panelRect를 지정하면 판정이 중심 반경(grabRadius)이 아니라 패널의 테두리(가장자리)
// 프레임으로 바뀐다 — 마치 창문 프레임을 잡는 것처럼, 안쪽(내용물/버튼)을 집으면
// 드래그가 안 걸리고 테두리를 집었을 때만 이동이 시작된다. 안쪽까지 반경으로 잡히게 하면
// 버튼을 누르려고 손을 갖다 대는 것과 패널을 옮기려고 잡는 것을 구분할 수 없어서
// 포크(클릭) 상호작용과 서로 오작동을 일으킨다 — 원래 UIResizeHandler가 테두리만
// 리사이즈로 반응하게 만들었던 것과 같은 이유(§4-15)다. panelRect를 비워두면
// 기존처럼 중심 반경(grabRadius) 판정으로 동작한다(패널이 아닌 3D 오브젝트용).

using Oculus.Interaction.Input;
using UnityEngine;

public class MRPinchDraggable : MonoBehaviour
{
    [Tooltip("옮길 대상. 비워두면 자기 자신.")]
    [SerializeField] private Transform target;

    [Header("판정 모드 A: 사각 패널 테두리 (UI 패널 권장)")]
    [Tooltip("지정하면 이 RectTransform의 테두리 프레임 안에서만 핀치가 잡힌다. " +
             "비워두면 아래 grabRadius로 중심 반경 판정을 쓴다.")]
    [SerializeField] private RectTransform panelRect;

    [Tooltip("테두리 프레임의 두께(m). 이 두께 안쪽(가장자리)에서만 잡힌다 — 안쪽 내용물/버튼 영역은 제외.")]
    [SerializeField] private float edgeThickness = 0.03f;

    [Tooltip("패널 바깥으로 얼마나 여유를 두고 잡을 수 있게 할지(m).")]
    [SerializeField] private float outwardMargin = 0.02f;

    [Tooltip("패널 표면으로부터 앞뒤 허용 거리(m) — 너무 멀리서 핀치하면 무시한다.")]
    [SerializeField] private float depthMargin = 0.05f;

    [Header("판정 모드 B: 중심 반경 (panelRect 없을 때)")]
    [Tooltip("대상 중심으로부터 이 반경(m) 안에서 핀치하면 드래그를 시작한다.")]
    [SerializeField] private float grabRadius = 0.15f;

    [Header("Hand 참조 (비워두면 런타임에 자동 탐색)")]
    [SerializeField] private Hand leftHand;
    [SerializeField] private Hand rightHand;

    [Header("공통")]
    [Tooltip("핀치 판정 임계값 — Hand를 못 찾아 OVRInput 폴백을 쓸 때만 사용 (트리거 축 기준)")]
    [SerializeField] private float pinchThreshold = 0.5f;

    [Tooltip("Z축(롤) 회전을 0으로 고정한다. 손목을 기울여도 패널이 갸우뚱하지 않아 " +
             "읽기 편하다. 기울임까지 자유롭게 하고 싶으면 끈다.")]
    [SerializeField] private bool lockRollRotation = true;

    private Camera _cam;
    private bool _isDragging;
    private bool _draggingLeft;
    private Vector3 _dragOffset;
    private Quaternion _dragRotationOffset;
    private float _lastHeartbeatTime;
    private float _lastLogTime;
    private float _lastHandSearchTime;

    public bool IsDragging => _isDragging;

    private void Awake()
    {
        _cam = Camera.main;
        if (target == null) target = transform;
        FindHandsIfNeeded();
    }

    // Update가 아니라 LateUpdate인 이유: ChatBalloonManager 같은 원본(데스크톱 공유) 매니저가
    // 자기 Update()에서 매 프레임 anchoredPosition을 캐릭터 기준으로 다시 계산해서 되돌려놓는다
    // (예: ChatBalloonManager.UpdateChatBalloonPosition()). 우리가 같은 Update()에 있으면
    // 실행 순서가 보장되지 않아 핀치로 옮겨도 그 프레임에 원본 스크립트가 다시 덮어써서
    // "잡히는 느낌은 나는데 실제로는 안 움직이는" 현상이 생긴다(실기 확인, 2026-08-11).
    // 모든 Update()가 끝난 뒤 LateUpdate()가 실행되는 걸 이용해 우리가 항상 마지막에
    // 덮어쓰게 한다 — MRBalloonWorldFollow와 같은 이유/패턴이다.
    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || target == null) return;

        FindHandsIfNeeded();

        GetPinchState(true, out bool leftPinch, out Vector3 leftPos, out Quaternion leftRot, out string leftSrc);
        GetPinchState(false, out bool rightPinch, out Vector3 rightPos, out Quaternion rightRot, out string rightSrc);

        if (Time.time - _lastHeartbeatTime > 1f)
        {
            _lastHeartbeatTime = Time.time;
            Debug.Log($"[MRPinchDraggable] '{name}' 살아있음. left(pinch={leftPinch}, src={leftSrc}) " +
                      $"right(pinch={rightPinch}, src={rightSrc}) " +
                      $"leftHand={(leftHand != null)} rightHand={(rightHand != null)}");
        }

        if (!_isDragging)
        {
            bool tryLeft = leftPinch;
            bool tryRight = rightPinch;
            if (!tryLeft && !tryRight) return;

            bool useLeft = tryLeft;
            Vector3 handWorldPos = useLeft ? leftPos : rightPos;

            bool grabbable = IsGrabbable(handWorldPos, out string reason);
            LogPinchAttempt(useLeft, handWorldPos, grabbable, reason);

            if (grabbable)
            {
                _isDragging = true;
                _draggingLeft = useLeft;
                Quaternion handRot = useLeft ? leftRot : rightRot;
                // 오프셋을 "손 기준 로컬 좌표"로 저장한다 — 월드 고정 벡터로 저장하면
                // 손을 회전시켜도 오프셋이 같이 돌지 않아서, 패널이 잡은 지점이 아니라
                // 자기 원점을 축으로 회전해버린다(= 잡은 손에서 떨어져 나가는 것처럼 보임).
                // 실기 확인/사용자 피드백, 2026-08-15.
                _dragOffset = Quaternion.Inverse(handRot) * (target.position - handWorldPos);
                // 잡은 순간의 "손 회전 대비 패널 회전" 상대값을 기억해둔다 — 태블릿을
                // 손에 쥐듯, 이후 손이 회전하는 그대로 패널도 같이 회전하게 하기 위함
                // (예전엔 매 프레임 카메라를 보게 강제로 되돌려서 자연스러운 조작이 어려웠다 —
                // 사용자 피드백, 2026-08-11).
                _dragRotationOffset = Quaternion.Inverse(handRot) * target.rotation;
                Debug.Log($"[MRPinchDraggable] '{name}' 드래그 시작({(useLeft ? "L" : "R")}).");
            }
            return;
        }

        bool stillPinching = _draggingLeft ? leftPinch : rightPinch;
        if (!stillPinching)
        {
            _isDragging = false;
            Debug.Log($"[MRPinchDraggable] '{name}' 드래그 종료. 그 자리에 고정. 최종 pos={target.position}");
            return;
        }

        Vector3 currentHandPos = _draggingLeft ? leftPos : rightPos;
        Quaternion currentHandRot = _draggingLeft ? leftRot : rightRot;

        // 손 위치를 그대로 따라간다(1:1) — 앞뒤로도 자유롭게 밀고 당길 수 있어야 하므로
        // 카메라 기준 고정 반경(예전 MRFloatingPanel 방식)은 쓰지 않는다. 실기 확인
        // (2026-08-11): 고정 반경으로 하면 잡은 순간 거리보다 멀리/가깝게 못 옮겨져서
        // "한 점 기준으로 일정 거리 이상 못 넘어가는" 것처럼 느껴진다.
        Vector3 before = target.position;

        // 회전을 먼저 적용하고, 오프셋도 현재 손 회전으로 돌려서 더한다 — 이래야
        // 잡은 지점이 손에 그대로 붙어 있는 것처럼(태블릿을 쥔 것처럼) 회전한다.
        Quaternion newRot = currentHandRot * _dragRotationOffset;

        if (lockRollRotation)
        {
            // 롤(Z)만 0으로 눌러준다. 오일러 각을 직접 만지면 짐벌 문제로 튀기 쉬우므로,
            // "패널이 보는 방향(forward)은 그대로 두고, 위쪽(up)만 월드 Y로 다시 세운다"는
            // 방식으로 계산한다 — 결과적으로 롤만 제거된다.
            Vector3 fwd = newRot * Vector3.forward;
            if (fwd.sqrMagnitude > 0.0001f)
            {
                newRot = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }

        target.rotation = newRot;
        target.position = currentHandPos + currentHandRot * _dragOffset;

        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[MRPinchDraggable] '{name}' 드래그 중. hand={currentHandPos} " +
                      $"pos {before}→{target.position}");
        }
    }

    private void FindHandsIfNeeded()
    {
        if (leftHand != null && rightHand != null) return;
        if (Time.time - _lastHandSearchTime < 2f) return; // 너무 자주 검색하지 않는다
        _lastHandSearchTime = Time.time;

        Hand[] hands = FindObjectsByType<Hand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var h in hands)
        {
            if (h == null) continue;
            if (leftHand == null && h.Handedness == Handedness.Left) leftHand = h;
            if (rightHand == null && h.Handedness == Handedness.Right) rightHand = h;
        }
    }

    /// <summary>왼손/오른손의 핀치 상태와 (잡을 때 쓸) 월드 위치·회전을 반환한다.
    /// Hand(ISDK 핸드트래킹)를 우선 쓰고, 없거나 트래킹이 안 잡히면 OVRInput 컨트롤러로 폴백한다.</summary>
    private void GetPinchState(bool left, out bool pinching, out Vector3 worldPos, out Quaternion worldRot, out string source)
    {
        Hand hand = left ? leftHand : rightHand;
        OVRInput.Controller ctrl = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        if (hand != null && hand.IsTrackedDataValid)
        {
            // 핀치 여부는 손이 트래킹되는 한 항상 Hand API를 신뢰한다 — 아래 관절 포즈
            // 조회가 실패하더라도 이 값을 OVRInput(항상 0을 주는 컨트롤러 폴백, §4-18)으로
            // 절대 덮어쓰지 않는다. 실기 확인(2026-08-11): 예전에 관절 포즈만 실패해도
            // 핀치 판정 자체가 통째로 폴백으로 넘어가면서 드래그가 아예 안 되는 회귀가 있었다.
            pinching = hand.GetFingerIsPinching(HandFinger.Index);

            // 위치는 손끝(HandIndexTip) 기준 — MRSpatialAnchorEditor.cs가 이미 쓰고 있는
            // 검증된 관절이다. (손목 기준으로 바꿔봤다가 관절 포즈 조회 자체가 실패해서
            // 핀치가 아예 안 먹히는 회귀만 생겨 되돌림 — 2026-08-11.)
            if (hand.GetJointPose(HandJointId.HandIndexTip, out Pose pose))
            {
                worldPos = pose.position;
                worldRot = pose.rotation;
                source = "Hand";
                return;
            }

            // 관절 포즈 조회만 실패한 경우 — 핀치 판정(위에서 이미 구함)은 유지하고
            // 위치·회전만 컨트롤러 값으로 대체한다.
            worldPos = HandWorldPosition(ctrl);
            worldRot = OVRInput.GetLocalControllerRotation(ctrl);
            source = "Hand(pinch)+OVRPos";
            return;
        }

        float axis = OVRInput.Get(left ? OVRInput.Axis1D.PrimaryIndexTrigger : OVRInput.Axis1D.SecondaryIndexTrigger);
        pinching = axis > pinchThreshold;
        worldPos = HandWorldPosition(ctrl);
        worldRot = OVRInput.GetLocalControllerRotation(ctrl);
        source = "OVRInput";
    }

    private void LogPinchAttempt(bool left, Vector3 handWorldPos, bool grabbable, string reason)
    {
        if (Time.time - _lastLogTime < 0.3f) return; // 스팸 방지
        _lastLogTime = Time.time;
        Debug.Log($"[MRPinchDraggable] '{name}' 핀치 감지({(left ? "L" : "R")}) hand={handWorldPos} " +
                  $"targetPos={target.position} grabbable={grabbable} reason={reason} " +
                  $"panelRectSet={(panelRect != null)}");
    }

    private bool IsGrabbable(Vector3 handWorldPos, out string reason)
    {
        if (panelRect == null)
        {
            float d = Vector3.Distance(handWorldPos, target.position);
            reason = $"center-radius d={d:F3} radius={grabRadius:F3}";
            return d < grabRadius;
        }

        // 패널의 로컬 좌표계(캔버스 px 단위)로 변환한다. panelRect.InverseTransformPoint는
        // 스케일까지 포함해서 변환해주므로, m 단위 여유값들을 "미터당 로컬 유닛"으로
        // 환산해서 같은 좌표계에서 비교한다.
        Vector3 local = panelRect.InverseTransformPoint(handWorldPos);
        Rect r = panelRect.rect;

        float lossyX = Mathf.Abs(panelRect.lossyScale.x);
        if (lossyX < 0.0000001f)
        {
            reason = "lossyScale 0";
            return false;
        }
        float unitsPerMeter = 1f / lossyX;

        float edgeUnits = edgeThickness * unitsPerMeter;
        float outUnits = outwardMargin * unitsPerMeter;
        float depthUnits = depthMargin * unitsPerMeter;

        float xMin = r.xMin - outUnits;
        float xMax = r.xMax + outUnits;
        float yMin = r.yMin - outUnits;
        float yMax = r.yMax + outUnits;

        string baseInfo = $"local={local} rect=({r.xMin:F0},{r.yMin:F0})~({r.xMax:F0},{r.yMax:F0}) " +
                           $"edgeUnits={edgeUnits:F1} depthUnits={depthUnits:F1} unitsPerMeter={unitsPerMeter:F1}";

        // 패널 앞뒤로 너무 멀면 무시
        if (Mathf.Abs(local.z) > depthUnits)
        {
            reason = $"depth 초과 {baseInfo}";
            return false;
        }

        // 전체 허용 영역(패널 + 바깥 여유) 밖이면 무시
        if (local.x < xMin || local.x > xMax || local.y < yMin || local.y > yMax)
        {
            reason = $"영역 밖 {baseInfo}";
            return false;
        }

        // 테두리보다 안쪽 깊숙한 "내용물" 영역이면 무시 — 버튼/입력 전용 구역
        bool insideInnerCore =
            local.x > r.xMin + edgeUnits && local.x < r.xMax - edgeUnits &&
            local.y > r.yMin + edgeUnits && local.y < r.yMax - edgeUnits;

        reason = $"{(insideInnerCore ? "안쪽 내용물이라 제외" : "테두리 통과")} {baseInfo}";
        return !insideInnerCore;
    }

    private Vector3 HandWorldPosition(OVRInput.Controller ctrl)
    {
        Vector3 handPos = OVRInput.GetLocalControllerPosition(ctrl);
        Transform rigRoot = _cam.transform.parent != null ? _cam.transform.parent : _cam.transform;
        return rigRoot.TransformPoint(handPos);
    }

}
