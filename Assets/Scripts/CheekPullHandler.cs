using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// 트릭컬 스타일 볼당기기 핸들러
// Character_Ball_L / Character_Ball_R 본(또는 그 하위 콜라이더)에 부착한다.
// 마우스로 콜라이더를 잡아 본을 최대치까지 당기고, 놓으면 0.2초 탄성 복귀한다.
[RequireComponent(typeof(Collider))]
public class CheekPullHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("당길 본")]
    public Transform cheekBone;  // 실제로 움직일 볼 본. 비우면 자기 자신 Transform 사용

    [Header("당기기 파라미터")]
    public float maxPullDistance = 0.06f;  // 본 기준 로컬 최대 이동 거리(미터)
    public float returnDuration = 0.2f;    // 놓았을 때 복귀 시간(초)
    public float followSpeed = 30f;        // 마우스 추종 부드러움(값이 클수록 즉각적)

    [Header("반응 연출")]
    public string requiredFeatureTag = "볼당기기";  // CharAttributes.featureTags에 이 태그가 있어야 동작
    public string pullEmotion = "><";              // 당기는 동안 표정
    public string releaseEmotion = "idle";          // 놓았을 때 복귀 표정
    public string animatorBoolName = "isCheekPull"; // 애니메이터 bool(없으면 무시)

    private Vector3 restLocalPos;      // 본의 원래 로컬 위치(기준점)
    private Vector3 currentOffset;     // 현재 적용 중인 로컬 오프셋
    private Vector3 targetOffset;      // 마우스가 지시하는 목표 로컬 오프셋
    private bool isPulling = false;    // 현재 당기는 중인지
    private Camera pullCamera;         // 레이 계산에 쓸 카메라
    private Animator charAnimator;     // 표정/모션용 애니메이터
    private GameObject charRoot;       // 표정 제어 대상 캐릭터 루트
    private Coroutine returnRoutine;   // 복귀 코루틴 핸들

    private void Start()
    {
        // 움직일 본이 지정되지 않았으면 자기 자신을 사용
        if (cheekBone == null)
        {
            cheekBone = transform;
        }

        // 본의 원래 로컬 위치 저장
        restLocalPos = cheekBone.localPosition;

        // 캐릭터 루트와 애니메이터 확보
        CharAttributes attributes = GetComponentInParent<CharAttributes>();
        if (attributes != null)
        {
            charRoot = attributes.gameObject;
            charAnimator = attributes.GetComponent<Animator>();
        }

        // 카메라 확보(월드 카메라 우선, 없으면 메인 카메라)
        if (CanvasManager.Instance != null && CanvasManager.Instance.canvasChar != null)
        {
            pullCamera = CanvasManager.Instance.canvasChar.worldCamera;
        }
        if (pullCamera == null)
        {
            pullCamera = Camera.main;
        }

        // 태그 게이팅: 지원 캐릭터가 아니면 비활성화
        if (!IsFeatureAllowed(attributes))
        {
            // 지원 태그가 없으면 볼당기기 자체를 끔
            enabled = false;
            Collider selfCollider = GetComponent<Collider>();
            if (selfCollider != null)
            {
                selfCollider.enabled = false;
            }
        }
    }

    // featureTags에 요구 태그가 있는지 확인
    private bool IsFeatureAllowed(CharAttributes attributes)
    {
        // 요구 태그가 비어 있으면 항상 허용
        if (string.IsNullOrEmpty(requiredFeatureTag))
        {
            return true;
        }

        // 속성이 없으면 불허
        if (attributes == null)
        {
            return false;
        }

        // 태그 목록에 포함되어 있으면 허용
        if (attributes.featureTags != null && attributes.featureTags.Contains(requiredFeatureTag))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // 드래그 시작: 볼 잡기
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 좌클릭이 아니면 무시
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        isPulling = true;

        // 진행 중인 복귀 코루틴 중단
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        // 다른 로직(걷기 등)과 충돌하지 않게 드래그 상태 표시
        if (StatusManager.Instance != null)
        {
            StatusManager.Instance.IsDragging = true;
        }

        // 당기는 동안 표정 변경
        if (EmotionManager.Instance != null && charRoot != null)
        {
            EmotionManager.Instance.ShowEmotion(pullEmotion, charRoot);
        }

        // 애니메이터 bool 켜기(파라미터가 있을 때만)
        SetAnimatorPull(true);
    }

    // 드래그 중: 마우스 위치로 목표 오프셋 갱신
    public void OnDrag(PointerEventData eventData)
    {
        // 당기는 중이 아니면 무시
        if (!isPulling)
        {
            return;
        }
        if (pullCamera == null)
        {
            return;
        }

        // 본의 현재 기준 월드 위치(머리 이동을 반영)
        Vector3 restWorldPos = cheekBone.parent.TransformPoint(restLocalPos);

        // 카메라를 바라보는 평면에 마우스 레이를 투영
        Ray ray = pullCamera.ScreenPointToRay(eventData.position);
        Plane dragPlane = new Plane(-pullCamera.transform.forward, restWorldPos);

        float enter = 0f;
        if (dragPlane.Raycast(ray, out enter))
        {
            // 평면 위 마우스 지점
            Vector3 worldPoint = ray.GetPoint(enter);

            // 기준점 대비 월드 오프셋을 본의 부모 로컬 공간으로 변환
            Vector3 worldOffset = worldPoint - restWorldPos;
            Vector3 localOffset = cheekBone.parent.InverseTransformVector(worldOffset);

            // 최대 이동 거리로 제한
            if (localOffset.magnitude > maxPullDistance)
            {
                localOffset = localOffset.normalized * maxPullDistance;
            }

            targetOffset = localOffset;
        }
    }

    // 드래그 종료: 탄성 복귀 시작
    public void OnEndDrag(PointerEventData eventData)
    {
        // 좌클릭이 아니면 무시
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        isPulling = false;
        targetOffset = Vector3.zero;

        // 드래그 상태 해제
        if (StatusManager.Instance != null)
        {
            StatusManager.Instance.IsDragging = false;
        }

        // 표정 원복
        if (EmotionManager.Instance != null && charRoot != null)
        {
            EmotionManager.Instance.ShowEmotion(releaseEmotion, charRoot);
        }

        // 애니메이터 bool 끄기
        SetAnimatorPull(false);

        // 탄성 복귀 코루틴 시작
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
        }
        returnRoutine = StartCoroutine(ReturnRoutine());
    }

    // 애니메이터가 움직인 뒤 본 위치를 덮어써서 당김을 반영
    private void LateUpdate()
    {
        // 당기는 중에는 목표로 부드럽게 추종
        if (isPulling)
        {
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, Time.deltaTime * followSpeed);
        }

        // 최종 로컬 위치 적용
        cheekBone.localPosition = restLocalPos + currentOffset;
    }

    // 0.2초에 걸쳐 탄성(오버슈트) 복귀
    private IEnumerator ReturnRoutine()
    {
        Vector3 startOffset = currentOffset;  // 복귀 시작 오프셋
        float elapsed = 0f;                   // 경과 시간

        // 복귀 시간이 0 이하이면 즉시 복귀
        if (returnDuration <= 0f)
        {
            currentOffset = Vector3.zero;
            cheekBone.localPosition = restLocalPos;
            returnRoutine = null;
            yield break;
        }

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnDuration);

            // ease-out-back: 0에 도달할 때 살짝 반대로 튕겼다가 안착
            float eased = EaseOutBack(t);
            currentOffset = Vector3.LerpUnclamped(startOffset, Vector3.zero, eased);

            yield return null;
        }

        // 정확히 원위치로 마감
        currentOffset = Vector3.zero;
        cheekBone.localPosition = restLocalPos;
        returnRoutine = null;
    }

    // 오버슈트가 있는 ease-out-back 곡선
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;   // 오버슈트 강도
        float c3 = c1 + 1f;    // 보정 계수

        float p = t - 1f;
        return 1f + c3 * (p * p * p) + c1 * (p * p);
    }

    // 애니메이터 bool 안전하게 설정
    private void SetAnimatorPull(bool value)
    {
        // 애니메이터가 없으면 무시
        if (charAnimator == null)
        {
            return;
        }
        if (string.IsNullOrEmpty(animatorBoolName))
        {
            return;
        }

        // 파라미터 존재 여부 확인 후 설정
        foreach (AnimatorControllerParameter param in charAnimator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Bool && param.name == animatorBoolName)
            {
                charAnimator.SetBool(animatorBoolName, value);
                return;
            }
        }
    }
}
