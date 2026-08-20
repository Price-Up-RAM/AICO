// 캐릭터 조준 표시 — 발밑 링 (MR_Phase4A_Input_Plan.md §8-1b)
//
// 왜 필요한가
// ----------
// 진리표(Port Plan §2-1)의 캐릭터 행 4칸은 "캐릭터를 조준한 상태"가 전제인데,
// 사용자에게 그 상태를 알려주는 표시가 하나도 없었다. 그래서 2026-08-19 실기에서
// **캐릭터 4칸을 검증조차 하지 못했다.** 손 레이는 `MRRayProvider`가 선을 그리지만
// 시선(palm-up) 채널은 선이 없으므로, 두 채널을 한 번에 덮는 표시가 필요하다.
//
// 설계 결정 세 가지
// ---------------
// 1) **외부 자산에 의존하지 않는다.** 링은 LineRenderer(loop)로 원을 그린다.
//    메시·머티리얼·프리팹을 새로 만들 필요가 없다.
// 2) **캐릭터의 자식으로 넣지 않는다.** 캐릭터는 픽셀 공간 래퍼(1/120) 안에 있어서
//    자식으로 넣는 순간 반경 1 m가 120 m가 된다 (Kickoff Guide §4-1).
//    링은 **월드 최상위**에 두고 월드 좌표로 그린다.
// 3) **판정 부피를 그대로 그린다.** 링은 캐릭터의 **콜라이더 경계**(MRCharacterBounds)로
//    만든다 — 조준 판정이 보는 것과 **같은 값**이다. '보이는 곳 = 잡히는 곳'이라
//    조준이 안 될 때 링을 보면 원인이 바로 드러난다.
//    링이 몸과 안 맞아 보이면 그건 링의 버그가 아니라 **캡슐이 몸과 안 맞는다는 신고**다.
//    그때 고칠 것은 표시가 아니라 캡슐의 center/height다.
//
// 비용: 콜라이더 목록 수집은 MRCharacterBounds가 0.5초 주기로 캐시하고, bounds 합산과
// 정점 갱신은 **조준 중일 때만** 돈다. 조준하지 않으면 아무 일도 하지 않는다.

using UnityEngine;

public class MRAimHighlight : MonoBehaviour
{
    [Header("공급자 — 비우면 씬에서 찾는다")]
    [SerializeField] private MRGazeProvider gazeProvider;
    [SerializeField] private MRRayProvider rayProvider;

    [Header("대상")]
    [SerializeField] private MRCharacterWorldRoot characterRoot;

    [Header("링")]
    [Tooltip("원을 몇 개의 선분으로 그릴지.")]
    [SerializeField] private int segments = 48;

    [Tooltip("선 굵기(m).")]
    [SerializeField] private float lineWidth = 0.006f;

    [Tooltip("판정 반경에 더할 여유(m). 0으로 두면 캡슐 굵기를 그대로 보여준다.")]
    [SerializeField] private float radiusPadding = 0f;

    [Tooltip("판정 부피 아래쪽에서 살짝 띄운다(m). 0이면 바닥과 z-fighting이 날 수 있다.")]
    [SerializeField] private float groundOffset = 0.01f;

    [SerializeField] private Color ringColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    private LineRenderer _ring;
    private GameObject _ringObject;

    private void OnDisable()
    {
        if (_ring != null) _ring.enabled = false;
    }

    private void OnDestroy()
    {
        // 링은 최상위 오브젝트라 이 컴포넌트가 사라져도 씬에 남는다. 같이 치운다.
        if (_ringObject != null) Destroy(_ringObject);
    }

    private void LateUpdate()
    {
        ResolveRefs();

        if (!IsAimingAtCharacter())
        {
            HideRing();
            return;
        }

        if (!TryGetGroundCircle(out Vector3 center, out float radius))
        {
            HideRing();
            return;
        }

        EnsureRing();
        if (_ring == null) return;

        BuildCircle(center, radius);
        _ring.enabled = true;
    }

    private void HideRing()
    {
        if (_ring == null) return;
        _ring.enabled = false;
    }

    /// <summary>두 채널 중 **어느 쪽이든** 캐릭터를 조준하면 표시한다.
    /// 채널이 닫혀 있으면(palm-up이 아니거나 손 트래킹이 없으면) 공급자가 Aim을
    /// None으로 두므로 valid가 false다.</summary>
    private bool IsAimingAtCharacter()
    {
        if (gazeProvider != null)
        {
            MRAimResult gaze = gazeProvider.Aim;
            if (gaze.valid && gaze.onCharacter) return true;
        }

        if (rayProvider != null)
        {
            MRAimResult ray = rayProvider.Aim;
            if (ray.valid && ray.onCharacter) return true;
        }

        return false;
    }

    // ---------------------------------------------------------
    // 캐릭터 경계 — 위치를 가정하지 않고 실제로 잰다
    // ---------------------------------------------------------
    private bool TryGetGroundCircle(out Vector3 center, out float radius)
    {
        center = Vector3.zero;
        radius = 0f;

        // 경계 계산은 MRCharacterBounds 한 곳에만 둔다 — 조준 판정(MRGazeProvider·MRRayProvider)과
        // 같은 값을 봐야 "표시는 되는데 판정은 안 되는" 비대칭이 생기지 않는다(§4-47).
        if (!MRCharacterBounds.TryGet(characterRoot, out Bounds bounds)) return false;

        center = new Vector3(bounds.center.x, bounds.min.y + groundOffset, bounds.center.z);
        radius = MRCharacterBounds.GetHorizontalRadius(bounds) + radiusPadding;

        // 경계가 사실상 0이면(로딩 중 등) 그리지 않는다 — 점 하나가 찍히는 것보다 낫다.
        if (radius < 0.02f) return false;

        return true;
    }

    private void BuildCircle(Vector3 center, float radius)
    {
        if (segments < 8) segments = 8;

        if (_ring.positionCount != segments) _ring.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            _ring.SetPosition(i, center + offset);
        }
    }

    private void EnsureRing()
    {
        if (_ring != null) return;

        _ringObject = new GameObject("MRAimRing");

        // ⚠ 최상위에 둔다. 캐릭터(픽셀 공간 래퍼 1/120)나 스케일이 걸린 부모 밑에 두면
        //    반경과 선 굵기가 그 배율만큼 왜곡된다 (Kickoff Guide §4-1).
        _ringObject.transform.SetParent(null);

        _ring = _ringObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace = true;
        _ring.loop = true;
        _ring.startWidth = lineWidth;
        _ring.endWidth = lineWidth;
        _ring.startColor = ringColor;
        _ring.endColor = ringColor;
        _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _ring.receiveShadows = false;
        _ring.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null) _ring.material = new Material(shader);
    }

    private void ResolveRefs()
    {
        if (gazeProvider == null) gazeProvider = FindFirstObjectByType<MRGazeProvider>();
        if (rayProvider == null) rayProvider = FindFirstObjectByType<MRRayProvider>();
        if (characterRoot == null) characterRoot = FindFirstObjectByType<MRCharacterWorldRoot>();
    }
}
