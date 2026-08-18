// 메타 시스템 알림처럼 "반쯤 고정된" HUD 배치.
//
// 동작
// ----
// 시야 정면 기준 위치를 목표로 삼되, 카메라가 조금 움직이는 정도로는 따라오지 않는다.
// 목표에서 각도로 deadzoneAngle 이상 벗어나야 추종을 시작하고, 다시 settleAngle 안으로
// 들어오면 그 자리에 멈춘다(히스테리시스). 추종 중에는 스무딩이 걸려 부드럽게 미끄러진다.
//
// 왜 이렇게 하는가
// ---------------
// 완전 고정(월드 고정)이면 고개를 돌렸을 때 시야에서 사라지고, 완전 추종(헤드락)이면
// 시선을 조금만 움직여도 같이 흔들려 멀미가 난다. 둘 사이의 절충이 메타 알림 방식이며,
// STT 결과처럼 "필요할 때 눈에 들어오되 거슬리지 않아야 하는" 정보창에 적합하다.
//
// 사용
// ----
// Image_NoticeBalloon 등 상단/하단 고정 정보창에 붙인다.
// MRBalloonWorldFollow(캐릭터 추종)와 동시에 쓰지 않는다 — 둘 다 위치를 쓰므로 충돌한다.

using UnityEngine;

public class MRLazyFollowHUD : MonoBehaviour
{
    [Header("배치")]
    [Tooltip("카메라로부터의 거리(m).")]
    [SerializeField] private float distance = 1.2f;

    [Tooltip("시선 높이 대비 상하 오프셋(m). 음수면 아래쪽(메타 알림처럼).")]
    [SerializeField] private float verticalOffset = -0.35f;

    [Tooltip("켜면 고개를 위아래로 들어도 높이가 따라온다. 끄면 수평 방향만 따라간다.")]
    [SerializeField] private bool followPitch = false;

    [Header("지연 추종")]
    [Tooltip("이 각도(도) 이상 벗어나야 따라오기 시작한다. 작을수록 민감.")]
    [SerializeField] private float deadzoneAngle = 18f;

    [Tooltip("추종 중 이 각도(도) 안으로 들어오면 멈춘다. deadzone보다 작아야 한다.")]
    [SerializeField] private float settleAngle = 3f;

    [Tooltip("따라오는 속도. 클수록 빠르게 붙는다.")]
    [SerializeField] private float followSpeed = 4f;

    [Tooltip("회전(빌보드) 추종 속도.")]
    [SerializeField] private float rotationSpeed = 6f;

    private Transform _eye;
    private bool _following;
    private bool _placed;

    private void OnEnable()
    {
        _placed = false;
        _following = false;
    }

    private void LateUpdate()
    {
        Transform eye = ResolveEye();
        if (eye == null) return;

        Vector3 desired = DesiredPosition(eye);

        // 처음 켜질 때는 스무딩 없이 바로 제자리에 놓는다.
        if (!_placed)
        {
            transform.position = desired;
            FaceEye(eye, instant: true);
            _placed = true;
            return;
        }

        // 현재 위치와 목표 위치가 눈 기준으로 몇 도 벌어져 있는지 잰다.
        Vector3 toCurrent = transform.position - eye.position;
        Vector3 toDesired = desired - eye.position;
        float angle = Vector3.Angle(toCurrent, toDesired);

        if (!_following)
        {
            if (angle > deadzoneAngle) _following = true;
        }
        else if (angle < settleAngle)
        {
            _following = false;
        }

        if (_following)
        {
            transform.position = Vector3.Lerp(transform.position, desired, Smooth(followSpeed));
        }

        // 회전은 항상 부드럽게 사용자를 향한다 — 위치가 멈춰 있어도 비스듬히 보이면
        // 읽기 어렵기 때문이다.
        FaceEye(eye, instant: false);
    }

    private Vector3 DesiredPosition(Transform eye)
    {
        Vector3 forward = eye.forward;
        if (!followPitch)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
        }

        Vector3 pos = eye.position + forward * distance;
        pos.y = (followPitch ? pos.y : eye.position.y) + verticalOffset;
        return pos;
    }

    private void FaceEye(Transform eye, bool instant)
    {
        // 캔버스 정면은 -Z 관례(§4-20)이므로 눈에서 패널로 향하는 방향을 forward로 준다.
        Vector3 dir = transform.position - eye.position;
        if (!followPitch) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = instant
            ? target
            : Quaternion.Slerp(transform.rotation, target, Smooth(rotationSpeed));
    }

    /// <summary>빌보드 기준이 되는 "눈" 트랜스폼.
    /// Camera.main은 OVR 리그에서 좌안 카메라가 잡히는 경우가 있어 CenterEyeAnchor를 우선한다
    /// (§4-20 계열 함정 — 패널이 살짝 비뚤어져 보인다).</summary>
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

    /// <summary>프레임레이트에 무관한 지수 스무딩 계수.</summary>
    private static float Smooth(float speed)
    {
        if (speed <= 0f) return 1f;
        return 1f - Mathf.Exp(-speed * Time.deltaTime);
    }
}
