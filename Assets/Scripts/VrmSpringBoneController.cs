using System.Collections;
using UniGLTF.SpringBoneJobs.Blittables;
using UnityEngine;
using UniVRM10;

// VRM(1.0) 캐릭터의 스프링본 런타임 보정 컨트롤러 — 소환 시 Attach()로 부착하면 이후 스스로 동작한다
// 배경 (naost에서 확인된 머리카락 폭주의 원인 두 가지):
// 1) FastSpringBone은 본 길이를 초기화 시점의 lossyScale이 곱해진 월드 단위로 베이크하는데,
//    stiffness/gravity는 SupportsScalingAtRuntime이 꺼져 있으면 스케일 미반영(미터 단위 고정)이라
//    캔버스 거대 스케일 밑에서 조인트 파라미터가 사실상 0으로 죽는다.
// 2) VRoid 출신 가슴 캡슐 콜라이더의 Offset/Tail이 본 세그먼트와 어긋나 머리카락 조인트를
//    상시 침범하면, 복원력이 없는 상태에서 콜라이더 밀어내기만 작동해 머리카락이 폭주한다.
// 프리팹에 에디터의 "Fit head-tail capsule"을 적용해도 플레이 시 원복되는 사례가 있어 코드로 강제 적용한다.
// 동작 세 가지:
// - Awake: 가슴 캡슐 콜라이더를 본 세그먼트에 맞게 재계산 (Vrm10Instance의 버퍼 베이크 전이라 그대로 반영)
// - OnEnable: 결합 버퍼 생성을 기다렸다가 스케일 연동(SupportsScalingAtRuntime) 적용
// - LateUpdate: lossyScale 변화 감시 — 칠윗유 착석/복귀, 설정 크기 변경 등으로 스케일이 바뀌면
//   본 길이를 재베이크(ReconstructSpringBone)하고, 재구축으로 초기화된 스케일 연동을 다시 적용
public class VrmSpringBoneController : MonoBehaviour
{
    // 가슴 캡슐 콜라이더 강제 보정 사용 여부 (실기에서 특정 캐릭터가 이상해지면 끄고 원인 재조사)
    private const bool useColliderFit = true;

    // 스케일 연동 사용 여부 (끄면 거대 스케일에서 조인트 stiffness/gravity 파라미터가 다시 죽는다)
    private const bool useScalingSupport = true;

    // 스케일 변화가 멈춘 뒤 이 시간이 지나면 재베이크 (슬라이더 드래그 중 재구축 연타 방지)
    private const float rescaleStabilitySeconds = 0.15f;

    // 결합 버퍼 생성 대기 한도 (초) — 버퍼가 만들어지지 않을 때 코루틴 무한 대기 방지
    private const float combineWaitTimeoutSeconds = 5f;

    private Vrm10Instance vrm;  // 보정 대상 VRM 인스턴스 (자식 포함 탐색)
    private Vector3 lastLossyScale;  // 마지막으로 확인한 lossyScale
    private float pendingRescaleTime = -1f;  // 스케일 변화를 마지막으로 감지한 시각 (-1 = 재베이크 대기 없음)
    private Coroutine scalingCoroutine;  // 스케일 연동 적용 코루틴 핸들

    // 소환 직후(Instantiate와 같은 프레임, setCharSize 이후) 캐릭터에 컨트롤러를 부착
    // Vrm10Instance가 없는 캐릭터(2D, 자체 리깅 3D)는 아무것도 하지 않는다
    public static void Attach(GameObject charObj)
    {
        if (charObj == null)
        {
            return;
        }
        if (charObj.GetComponentInChildren<Vrm10Instance>() == null)
        {
            return;
        }
        if (charObj.GetComponent<VrmSpringBoneController>() != null)
        {
            return;
        }

        charObj.AddComponent<VrmSpringBoneController>();
    }

    private void Awake()
    {
        vrm = GetComponentInChildren<Vrm10Instance>();
        if (vrm == null)
        {
            enabled = false;
            return;
        }

        lastLossyScale = transform.lossyScale;

        // Vrm10Instance(실행순서 11000)의 버퍼 베이크 전에 콜라이더를 고쳐야 재구축 없이 반영된다
        if (useColliderFit)
        {
            FitChestCapsuleCollider();
        }
    }

    private void OnEnable()
    {
        // 최초 부착 시 스케일 연동 시작.
        // 칠윗유 배치처럼 SetActive(false)로 코루틴이 중단된 경우의 재기동도 겸한다
        if (vrm != null)
        {
            EnsureScalingCoroutine();
        }
    }

    private void LateUpdate()
    {
        if (vrm == null)
        {
            enabled = false;
            return;
        }

        // 스케일 변화 감시 — 변화가 이어지는 동안에는 재베이크 시점을 계속 뒤로 미룬다
        Vector3 currentLossyScale = transform.lossyScale;
        if (ScaleChanged(currentLossyScale, lastLossyScale))
        {
            lastLossyScale = currentLossyScale;
            pendingRescaleTime = Time.unscaledTime;
            return;
        }

        // 변화가 멈춘 뒤 안정화 시간이 지나면 한 번만 재베이크
        if (pendingRescaleTime >= 0f && Time.unscaledTime - pendingRescaleTime >= rescaleStabilitySeconds)
        {
            pendingRescaleTime = -1f;
            vrm.Runtime.SpringBone.ReconstructSpringBone();
            EnsureScalingCoroutine();  // 재구축으로 기본값으로 돌아간 스케일 연동을 다시 적용
        }
    }

    // lossyScale의 상대 변화가 0.1% 이상인지 판정
    private static bool ScaleChanged(Vector3 current, Vector3 last)
    {
        return (current - last).sqrMagnitude > Mathf.Max(last.sqrMagnitude, 1e-12f) * 1e-6f;
    }

    // UpperChest(없으면 Chest) 본의 스프링본 콜라이더를 본 머리→첫 자식 구간의 캡슐로 재계산
    // 에디터 VRM10SpringBoneColliderEditor의 "Fit head-tail capsule" 버튼과 같은 계산식
    private void FitChestCapsuleCollider()
    {
        Transform chest;
        if (!vrm.TryGetBoneTransform(HumanBodyBones.UpperChest, out chest))
        {
            if (!vrm.TryGetBoneTransform(HumanBodyBones.Chest, out chest))
            {
                return;
            }
        }

        if (chest == null || chest.childCount == 0)
        {
            return;
        }

        VRM10SpringBoneCollider[] colliders = chest.GetComponents<VRM10SpringBoneCollider>();
        foreach (VRM10SpringBoneCollider collider in colliders)
        {
            collider.ColliderType = VRM10SpringBoneColliderTypes.Capsule;
            collider.Offset = Vector3.zero;
            collider.Tail = chest.worldToLocalMatrix.MultiplyPoint(chest.GetChild(0).position);
        }

        if (colliders.Length > 0)
        {
            Debug.Log($"[VrmSpringBoneController] 가슴 캡슐 콜라이더 보정 완료: {vrm.name} ({chest.name}, 콜라이더 {colliders.Length}개)");
        }
    }

    // 스케일 연동 적용 코루틴을 (재)기동 — 이미 도는 코루틴이 있으면 중단하고 새로 시작
    private void EnsureScalingCoroutine()
    {
        if (!useScalingSupport)
        {
            return;
        }

        if (scalingCoroutine != null)
        {
            StopCoroutine(scalingCoroutine);
        }
        scalingCoroutine = StartCoroutine(CoEnableScalingSupport());
    }

    // 결합 버퍼가 만들어진 뒤(첫 Process 이후) 스케일 연동을 켠다
    // - SetModelLevel은 결합 버퍼에 모델이 등록된 뒤에만 반영된다 (그 전 호출은 조용히 무시됨)
    // - ReconstructSpringBone이 일어나면 설정이 기본값으로 돌아가므로 재구축 후에도 다시 호출해야 한다
    private IEnumerator CoEnableScalingSupport()
    {
        // Runtime 접근이 첫 호출이면 이 시점의 콜라이더/스케일 값으로 버퍼가 베이크된다
        // (Awake의 콜라이더 보정과 소환 코드의 setCharSize 이후에 도달하는 것이 전제)
        var standalone = vrm.Runtime.SpringBone as Vrm10FastSpringboneRuntimeStandalone;

        float deadline = Time.realtimeSinceStartup + combineWaitTimeoutSeconds;
        if (standalone != null)
        {
            // 결합 버퍼 생성 대기
            while (vrm != null && !standalone.m_bufferCombiner.HasBuffer)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogWarning("[VrmSpringBoneController] 결합 버퍼 대기 타임아웃 — 스케일 연동 적용 실패");
                    scalingCoroutine = null;
                    yield break;
                }
                yield return null;
            }
        }

        // 직전에 큐잉된 재구축 요청이 결합에 반영된 뒤 쓰도록 두 프레임 대기
        // (UpdateType이 Update인 모델에서도 결합(실행순서 11000)보다 먼저 쓰는 일이 없도록 여유 확보)
        yield return null;
        yield return null;

        if (vrm != null)
        {
            vrm.Runtime.SpringBone.SetModelLevel(
                vrm.transform,
                new BlittableModelLevel(supportsScalingAtRuntime: true));
            Debug.Log($"[VrmSpringBoneController] 스프링본 스케일 연동 적용 완료: {vrm.name}");
        }

        scalingCoroutine = null;
    }
}
