using UnityEngine;
using Oculus.Interaction.Surfaces;

/// <summary>
/// `Tools → MR → 5`가 만든 `HandInteraction` 자식(손 상호작용 판정 면)의 크기·스케일을
/// 패널의 **현재** rect 기준으로 다시 계산한다.
///
/// 왜 필요한가 (Kickoff Guide §4-35)
/// --------------------------------
/// Tools → MR → 5는 **에디터 실행 시점의** rect와 lossyScale을 읽어 `BoundsClipper.Size`를
/// 미터 단위로 구워 넣는다. 크기가 고정된 패널은 그래도 되지만, DevionGames 위젯은
/// **항목 수에 따라 런타임에 크기가 생긴다.** 씬에 저장된 값이 이렇다:
///
///   Context Menu / Context Menu Sub   160 × 4     ← 높이가 사실상 0
///   Radial Menu Action / Characters   0 × 0
///
/// 즉 열 때마다 판정 면이 어긋난다. `MRGrabFrameFitter`가 잡기 띠에 대해 하는 일을
/// 판정 면에 대해 하는 컴포넌트다.
///
/// `OnEnable`만으로는 부족하다 — 위젯은 `OnEnable` 시점에 아직 항목이 없다.
/// **항목을 채운 뒤 `Fit()`을 한 번 더 불러야 한다.**
/// (`DevionGames.UIWidgets`는 Assembly-CSharp을 참조할 수 없으므로(§4-12) 직접 부르지 못한다.
///  `MRPointerBridge`처럼 브리지 asmdef를 통해 내보내거나, `refitInterval`로 폴링한다.)
/// </summary>
[ExecuteAlways]
public class MRHandInteractionFitter : MonoBehaviour
{
    // Tools → MR → 5(MRWorldUIInteraction)와 **같은 값**이어야 한다.
    // 저쪽이 바뀌면 여기도 바꿀 것.
    private const string InteractionChildName = "HandInteraction";
    private const float SurfaceDepth = 0.02f;
    private const float GraspBandPadding = 0f;

    [Tooltip("기준이 되는 패널의 RectTransform. 비워두면 자기 자신에서 찾는다.")]
    [SerializeField] private RectTransform panelRect;

    [Tooltip("rect가 이 값(초)마다 바뀌었는지 확인해 자동으로 다시 맞춘다. " +
             "0이면 폴링하지 않고 OnEnable과 수동 Fit()에만 의존한다.")]
    [SerializeField] private float refitInterval = 0.25f;

    [Tooltip("크기가 0인 rect에는 맞추지 않는다. 항목이 채워지기 전 상태이기 때문이다.")]
    [SerializeField] private float minValidSize = 1f;

    [Tooltip("(임시) 판정 면 크기를 로그로 남긴다. 원인 확인 후 끄거나 지운다.")]
    [SerializeField] private bool logDiagnostics = true;

    private Transform _interactionChild;
    private BoundsClipper _clipper;

    private Vector2 _lastRectSize = new Vector2(-1f, -1f);
    private Vector3 _lastLossyScale = Vector3.zero;
    private float _timer;

    private void OnEnable()
    {
        Fit();
    }

    private void Update()
    {
        if (refitInterval <= 0f) return;

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;

        _timer = refitInterval;

        if (!HasChanged()) return;

        Fit();
    }

    private bool HasChanged()
    {
        if (!Resolve()) return false;

        Vector2 size = panelRect.rect.size;
        Vector3 lossy = panelRect.lossyScale;

        if (size == _lastRectSize && lossy == _lastLossyScale) return false;

        return true;
    }

    /// <summary>현재 rect·스케일 기준으로 판정 면을 다시 맞춘다.
    /// 위젯 항목을 채운 직후에 외부에서 불러야 한다.</summary>
    public void Fit()
    {
        if (!Resolve()) return;

        Vector2 size = panelRect.rect.size;

        // 항목이 아직 없어 크기가 0인 상태에서 맞추면, 손이 닿을 수 없는 판정 면이
        // 그대로 굳는다(§4-21에서 BoundsClipper가 0.00072m로 구워졌던 것과 같은 사고).
        if (size.x < minValidSize || size.y < minValidSize) return;

        Vector3 lossy = panelRect.lossyScale;
        if (Mathf.Abs(lossy.x) < 1e-9f || Mathf.Abs(lossy.y) < 1e-9f) return;

        // 캔버스 스케일을 상쇄해 world scale = 1로 되돌린다.
        // ISDK의 Poke 판정 거리는 월드 미터 기준이라, 0.001 스케일 밑에 두면 1000배로 왜곡된다.
        _interactionChild.localRotation = Quaternion.identity;
        _interactionChild.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);

        // 판정 영역 = rect 와 **실제 내용물 경계의 합집합**.
        //
        // rect 중심만 쓰면 피벗이 모서리인 위젯에서 통째로 어긋난다.
        // 실측(2026-08-18) Context Menu: rect=(160,130)이라 rect.center=(80,-65)인데
        // 항목들의 중심은 (80,+65) — 메뉴 높이 하나만큼(130px) 어긋나 있었다.
        // 그래서 레이가 판정 면에는 맞지만 그 자리에 Selectable이 없어
        // "canvas는 잡히는데 target=null"(§4-18 패턴)이 나왔다.
        //
        // 어느 쪽이 '옳은' 영역인지 판단하지 않고 둘 다 덮는다 — 판정 면이 조금 넓은 것은
        // 무해하지만, 어긋나면 아무것도 눌리지 않는다.
        Rect rectPx = panelRect.rect;
        Bounds area = new Bounds(new Vector3(rectPx.center.x, rectPx.center.y, 0f),
                                 new Vector3(rectPx.width, rectPx.height, 0f));

        Bounds contentPx = ComputeContentBounds();
        if (contentPx.size.x > 0.01f && contentPx.size.y > 0.01f)
        {
            area.Encapsulate(contentPx);
        }

        _interactionChild.localPosition = new Vector3(area.center.x, area.center.y, 0f);

        if (_clipper != null)
        {
            float widthM = area.size.x * Mathf.Abs(lossy.x);
            float heightM = area.size.y * Mathf.Abs(lossy.y);

            _clipper.Position = Vector3.zero;
            _clipper.Size = new Vector3(
                widthM + GraspBandPadding * 2f,
                heightM + GraspBandPadding * 2f,
                SurfaceDepth);
        }

        _lastRectSize = size;
        _lastLossyScale = lossy;

        if (logDiagnostics)
        {
            // 항목이 rect 밖으로 나가면 판정 면이 항목을 안 덮는다 —
            // "canvas는 잡히는데 target이 null"(§4-18 패턴)의 원인이 될 수 있다.
            // 자식들의 실제 경계도 함께 찍어 rect와 비교한다.
            Debug.Log($"[MRHandFit] '{panelRect.name}' rect={size} rect.center={rectPx.center} " +
                      $"판정영역 center={area.center} size={area.size} " +
                      $"clipper={( _clipper != null ? _clipper.Size.ToString("F3") : "없음" )} " +
                      $"자식수={panelRect.childCount}");
        }
    }

    /// <summary>자식 RectTransform들의 로컬 경계를 합쳐 반환한다(패널 로컬 px 기준).
    /// rect보다 크면 판정 면이 항목을 못 덮고 있다는 뜻이다.</summary>
    private Bounds ComputeContentBounds()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;

        for (int i = 0; i < panelRect.childCount; i++)
        {
            var child = panelRect.GetChild(i) as RectTransform;
            if (child == null) continue;
            if (!child.gameObject.activeInHierarchy) continue;
            if (child.name == InteractionChildName) continue;

            Vector3 center = panelRect.InverseTransformPoint(child.TransformPoint(child.rect.center));
            Vector3 size = new Vector3(child.rect.width * child.localScale.x,
                                       child.rect.height * child.localScale.y,
                                       0f);

            var b = new Bounds(center, size);
            if (first)
            {
                bounds = b;
                first = false;
                continue;
            }

            bounds.Encapsulate(b);
        }

        return bounds;
    }

    private bool Resolve()
    {
        if (panelRect == null) panelRect = transform as RectTransform;
        if (panelRect == null) return false;

        if (_interactionChild == null)
        {
            _interactionChild = transform.Find(InteractionChildName);
        }

        if (_interactionChild == null) return false;

        if (_clipper == null)
        {
            _clipper = _interactionChild.GetComponent<BoundsClipper>();
        }

        return true;
    }
}
