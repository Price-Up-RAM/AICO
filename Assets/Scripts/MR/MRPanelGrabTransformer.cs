// ISDK Grabbable이 잡고 있는 동안 패널을 어떻게 움직일지 정의하는 커스텀 트랜스포머.
//
// 왜 커스텀인가
// ------------
// 빌딩 블록 기본 구성은 트랜스포머가 null이라 손을 1:1로 따라간다(6-DOF 자유 회전).
// 패널 UI로는 두 가지가 안 맞는다:
//   1) 손목을 기울이면 패널이 같이 갸우뚱해서 읽기 어렵다.
//   2) 손 트래킹 원본값을 그대로 쓰면 미세하게 떨린다.
// 그래서 이 트랜스포머는 "잡는 동안 카메라를 바라보게(빌보드) + 스무딩"을 적용하고,
// 손을 떼면 그 자세 그대로 고정한다(EndTransform에서 아무것도 되돌리지 않는다).
//
// 사용
// ----
// 패널 루트(Grabbable이 있는 오브젝트)에 붙이고, Grabbable._oneGrabTransformer에 연결한다.
// Tools → MR → 8 이 자동으로 붙이고 배선한다.

using Oculus.Interaction;
using UnityEngine;

public class MRPanelGrabTransformer : MonoBehaviour, ITransformer
{
    public enum BillboardMode
    {
        None,       // 회전 안 함 — 잡기 전 자세 유지
        YAxisOnly,  // 수평으로만 사용자를 향함 (권장 — 어지럽지 않다)
        Full,       // 상하까지 완전히 사용자를 향함
    }

    [Header("잡는 동안 회전")]
    [Tooltip("잡고 있는 동안 카메라를 바라보게 한다. 손을 떼면 그 자세 그대로 고정된다. " +
             "Full은 상하까지(XY) 완전히 사용자를 향한다 — 손의 회전은 무시하고 " +
             "잡은 지점을 앵커로 삼아 돈다.")]
    [SerializeField] private BillboardMode billboardWhileGrabbed = BillboardMode.Full;

    [Header("스무딩")]
    [Tooltip("위치 추종 속도. 클수록 손에 딱 붙고, 작을수록 부드럽지만 늦게 따라온다.")]
    [SerializeField] private float positionSmoothing = 14f;

    [Tooltip("회전 추종 속도.")]
    [SerializeField] private float rotationSmoothing = 10f;

    [Tooltip("끄면 스무딩 없이 손을 1:1로 따라간다(디버그용).")]
    [SerializeField] private bool enableSmoothing = true;

    private IGrabbable _grabbable;
    private Transform _eye;

    // 잡은 지점을 패널 로컬로 기억한다 — 회전을 빌보드로 덮어써도
    // "잡은 지점이 손에 온다"를 항상 정확히 만족시키기 위함.
    // (손 기준 오프셋으로 저장하면 적용 회전과 오프셋 회전이 어긋나 패널이 손에서 떨어진다.)
    private Vector3 _grabPointLocal;

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
        _eye = ResolveEye();
    }

    /// <summary>빌보드 기준이 되는 "눈" 트랜스폼.
    ///
    /// Camera.main을 그냥 쓰면 OVR 리그에서 좌안 카메라가 잡히는 경우가 있어 패널이
    /// 살짝 왼쪽을 향한 것처럼 비뚤어진다(실기 확인 2026-08-15). CenterEyeAnchor를
    /// 우선적으로 찾는다.</summary>
    private static Transform ResolveEye()
    {
        var byName = GameObject.Find("CenterEyeAnchor");
        if (byName != null) return byName.transform;

        if (Camera.main != null) return Camera.main.transform;

        var any = Object.FindFirstObjectByType<Camera>();
        return any != null ? any.transform : null;
    }

    public void BeginTransform()
    {
        if (_grabbable == null || _grabbable.GrabPoints.Count == 0) return;
        if (_eye == null) _eye = ResolveEye();

        Transform target = _grabbable.Transform;
        Pose grabPose = _grabbable.GrabPoints[0];

        _grabPointLocal = target.InverseTransformPoint(grabPose.position);
    }

    public void UpdateTransform()
    {
        if (_grabbable == null || _grabbable.GrabPoints.Count == 0) return;
        if (_eye == null) _eye = ResolveEye();

        Transform target = _grabbable.Transform;
        Pose grabPose = _grabbable.GrabPoints[0];

        // ---- 목표 회전 ----
        Quaternion desiredRot = target.rotation;
        if (billboardWhileGrabbed != BillboardMode.None && _eye != null)
        {
            // 캔버스 정면은 -Z 관례(§4-20)이므로, 눈에서 패널로 향하는 방향을
            // forward로 잡으면 패널 앞면이 사용자를 향한다.
            Vector3 dir = target.position - _eye.position;
            if (billboardWhileGrabbed == BillboardMode.YAxisOnly) dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        // ---- 회전 먼저 적용 ----
        // 순서가 중요하다: 아래 위치 역산이 TransformPoint를 쓰는데, 그게 방금 적용한
        // 회전을 반영해야 "잡은 지점이 손에 정확히 온다"가 성립한다.
        target.rotation = enableSmoothing
            ? Quaternion.Slerp(target.rotation, desiredRot, SmoothFactor(rotationSmoothing))
            : desiredRot;

        // ---- 위치: 잡은 지점이 손에 오도록 역산 ----
        Vector3 desiredPos = target.position + (grabPose.position - target.TransformPoint(_grabPointLocal));

        target.position = enableSmoothing
            ? Vector3.Lerp(target.position, desiredPos, SmoothFactor(positionSmoothing))
            : desiredPos;
    }

    /// <summary>손을 떼는 순간 — 아무것도 하지 않는다. 마지막 자세 그대로 공중에 고정된다.</summary>
    public void EndTransform() { }

    /// <summary>프레임레이트에 무관한 지수 스무딩 계수. Quest 프레임 변동에도 감이 일정하다.</summary>
    private static float SmoothFactor(float speed)
    {
        if (speed <= 0f) return 1f;
        return 1f - Mathf.Exp(-speed * Time.deltaTime);
    }
}
