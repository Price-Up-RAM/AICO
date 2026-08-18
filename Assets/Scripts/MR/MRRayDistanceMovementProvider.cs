// 직선 레이로 패널을 잡아 옮길 때의 이동 방식.
//
// 왜 MoveFromTargetProvider로는 부족한가 (2026-08-15 실기)
// -----------------------------------------------------
// MoveFromTargetProvider는 패널을 **손의 이동량과 1:1**로 옮긴다. 그래서 멀리 있는 패널을
// 레이로 잡으면, 레이는 크게 휘두르는데 패널은 손이 움직인 만큼(수십 cm)만 따라와
// "레이 이동의 절반만 움직이는" 느낌이 든다 — 레이 끝을 따라오지 않기 때문이다.
//
// 이 프로바이더의 규칙
// -------------------
//  · 각도(rho, phi): 패널을 **항상 레이 위에** 둔다. 레이를 돌린 만큼 패널이 정확히 따라온다(1:1).
//  · 거리(r): 손을 앞뒤로 움직인 양에 비례 배율을 곱한다.
//        배율 = (잡은 순간 카메라~패널 거리) / (잡은 순간 카메라~손 거리)
//    멀리 있는 패널일수록 배율이 커져 손을 조금만 당겨도 앞으로 훅 오고,
//    가까이 있는 패널은 배율이 1에 가까워 세밀하게 조절된다.
//
// 사용: 패널의 GrabFrame에 붙이고 각 Bar_*의 RayInteractable._movementProvider에 연결한다.
// (Tools → MR → 8 이 자동으로 처리)

using Oculus.Interaction;
using UnityEngine;

public class MRRayDistanceMovementProvider : MonoBehaviour, IMovementProvider
{
    [Tooltip("잡기 대상이 되는 Bar들의 RayInteractable. 비워두면 자식에서 전부 찾는다.")]
    [SerializeField] private RayInteractable[] rayInteractables;

    [Tooltip("거리 배율의 상한. 너무 크면 아주 멀리 있는 패널이 손 떨림에도 확 튄다.")]
    [SerializeField] private float maxDistanceMultiplier = 12f;

    [Tooltip("카메라에 이보다 가까이는 오지 않는다(m).")]
    [SerializeField] private float minDistanceFromCamera = 0.25f;

    private void Awake()
    {
        if (rayInteractables == null || rayInteractables.Length == 0)
        {
            rayInteractables = GetComponentsInChildren<RayInteractable>(true);
        }
    }

    [Tooltip("레이 잡기가 왜 안 되는지 추적할 때 켠다.")]
    [SerializeField] private bool verboseLog = false;

    public IMovement CreateMovement()
    {
        RayInteractor ray = FindActiveRay();

        if (verboseLog)
        {
            int count = rayInteractables != null ? rayInteractables.Length : 0;
            Debug.Log($"[MRRayDistance] '{name}' CreateMovement 호출됨. " +
                      $"RayInteractable {count}개, 선택 중인 RayInteractor={(ray != null ? "찾음" : "없음")}");
        }

        return new RayDistanceMovement(ray, Camera.main,
                                       maxDistanceMultiplier, minDistanceFromCamera, verboseLog, name);
    }

    /// <summary>지금 이 패널을 선택하고 있는 RayInteractor를 찾는다.</summary>
    private RayInteractor FindActiveRay()
    {
        // Awake 시점에 비어 있었을 수 있으므로(런타임 생성 등) 매번 비었으면 다시 찾는다.
        if (rayInteractables == null || rayInteractables.Length == 0)
        {
            rayInteractables = GetComponentsInChildren<RayInteractable>(true);
        }
        if (rayInteractables == null) return null;

        foreach (var interactable in rayInteractables)
        {
            if (interactable == null) continue;
            foreach (var view in interactable.SelectingInteractorViews)
            {
                if (view is RayInteractor ray) return ray;
            }
        }

        // CreateMovement가 "선택 확정" 직전에 불릴 수 있어 SelectingInteractorViews가
        // 아직 비어 있는 경우가 있다. 그때는 호버 중인 인터랙터라도 집는다.
        foreach (var interactable in rayInteractables)
        {
            if (interactable == null) continue;
            foreach (var view in interactable.InteractorViews)
            {
                if (view is RayInteractor ray) return ray;
            }
        }

        return null;
    }

    private class RayDistanceMovement : IMovement
    {
        private readonly RayInteractor _ray;
        private readonly Camera _cam;
        private readonly float _maxMultiplier;
        private readonly float _minCamDistance;

        private bool _initialized;
        private float _lengthAlongRay;   // 잡은 순간 레이 원점~패널 거리
        private float _handDistance0;    // 잡은 순간 카메라~손(레이 원점) 거리
        private float _multiplier;       // 손 이동 → 패널 이동 배율

        public Pose Pose { get; private set; } = Pose.identity;
        public bool Stopped => true;

        private readonly bool _verbose;
        private readonly string _owner;

        public RayDistanceMovement(RayInteractor ray, Camera cam, float maxMultiplier, float minCamDistance,
                                   bool verbose = false, string owner = "")
        {
            _ray = ray;
            _cam = cam != null ? cam : Camera.main;
            _maxMultiplier = maxMultiplier;
            _minCamDistance = minCamDistance;
            _verbose = verbose;
            _owner = owner;
        }

        public void StopAndSetPose(Pose source) => Pose = source;

        public void MoveTo(Pose target)
        {
            CaptureGrabState();
            Pose = Compute(target);
        }

        public void UpdateTarget(Pose target)
        {
            Pose = Compute(target);
        }

        public void StopMovement() { }
        public void Tick() { }

        /// <summary>잡은 순간의 거리 관계를 기록한다 — 이후 배율의 기준이 된다.</summary>
        private void CaptureGrabState()
        {
            _initialized = false;
            if (_ray == null || _cam == null) return;

            Vector3 origin = _ray.Origin;
            Vector3 camPos = _cam.transform.position;

            _lengthAlongRay = Vector3.Distance(origin, Pose.position);
            _handDistance0 = Vector3.Distance(camPos, origin);

            float objectDistance0 = Vector3.Distance(camPos, Pose.position);
            _multiplier = _handDistance0 > 0.01f
                ? Mathf.Clamp(objectDistance0 / _handDistance0, 1f, _maxMultiplier)
                : 1f;

            _initialized = true;

            if (_verbose)
            {
                Debug.Log($"[MRRayDistance] '{_owner}' 잡음. 레이원점~패널={_lengthAlongRay:F2}m " +
                          $"카메라~손={_handDistance0:F2}m 카메라~패널={objectDistance0:F2}m " +
                          $"배율={_multiplier:F2}");
            }
        }

        private Pose Compute(Pose fallback)
        {
            // 레이를 못 찾았으면 SDK가 준 기본 동작(손과 1:1)으로 폴백한다.
            if (!_initialized || _ray == null || _cam == null) return fallback;

            Vector3 origin = _ray.Origin;
            Vector3 dir = _ray.Ray.direction.normalized;
            if (dir.sqrMagnitude < 0.0001f) return fallback;

            // 손이 카메라에서 멀어진 만큼(앞으로 뻗은 만큼) 배율을 곱해 패널을 밀어낸다.
            float handDistance = Vector3.Distance(_cam.transform.position, origin);
            float length = _lengthAlongRay + (handDistance - _handDistance0) * _multiplier;

            // 너무 가까이 붙거나 등 뒤로 넘어가지 않게 막는다.
            length = Mathf.Max(length, _minCamDistance);

            Vector3 position = origin + dir * length;

            // 회전은 SDK가 준 값을 그대로 쓴다 — 회전 처리는 MRPanelGrabTransformer 담당.
            return new Pose(position, fallback.rotation);
        }
    }
}
