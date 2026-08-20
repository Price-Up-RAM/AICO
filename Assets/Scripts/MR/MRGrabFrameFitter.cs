// GrabFrame(패널 잡기 영역)의 크기·스케일을 패널의 현재 상태에 맞춰 다시 계산한다.
//
// 왜 필요한가 (2026-08-15)
// ----------------------
// Tools → MR → 8 은 실행 시점의 패널 스케일을 읽어 GrabFrame의 보정 스케일(1/lossyScale)과
// 콜라이더 크기(m 단위)를 **구워서** 저장했다. 그래서 나중에 인스펙터에서 패널 크기를
// 바꾸면 잡기 영역만 옛 크기로 남아 조용히 어긋난다 (Kickoff Guide §4-26).
// 이 컴포넌트가 OnEnable에서 현재 값 기준으로 다시 맞춘다.
//
// 테두리 4바 → 뒤판 1개 (2026-08-18 결정)
// --------------------------------------
// 예전에는 Bar_Top/Bottom/Left/Right 4개를 테두리 모양으로 배치했다. 4개였던 이유는
// 기능이 아니라 SDK 제약이다 — ColliderSurface가 콜라이더를 **하나만** 참조해서,
// 직선 레이로 잡으려면 바마다 자기 ColliderSurface + RayInteractable이 필요했다(§4-23).
//
// 패널은 어차피 평면이고 잡기 영역은 그 뒤에 있으므로, **캔버스보다 조금 큰 얇은 판 하나**로
// 대체할 수 있다. 오브젝트가 1/4로 줄고 모서리 틈도 사라진다.
//
// 다만 근접 grab은 Rigidbody 기준으로 자식 콜라이더를 전부 훑으므로(§4-23), 판을 얕게 두면
// **패널 정면 전체가 grab 근접 영역**이 되어 버튼을 포크하려는 손과 경쟁한다(§4-15 계열).
// 그래서 판의 앞면을 패널 평면보다 backClearance(기본 3cm)만큼 **확실히 뒤로** 민다.
// 직선 레이 grab은 빈 공간을 통과해 닿으므로 이 깊이에 영향받지 않는다.

// 항목이 rect 밖으로 뻗는 위젯 (2026-08-18)
// ------------------------------------
// RadialMenu는 항목을 **원형으로 뻗어서** 배치하므로 자식들이 자기 rect 밖에 있다.
// rect만 보고 판을 만들면 잡기 영역이 가운데 일부만 덮는다 — 실측: 버튼(포크)은 눌리는데
// 잡기가 안 되는 비대칭. 판정 면(MRHandInteractionFitter)은 이미 자식 경계를 합쳐서
// 쓰고 있었고, 잡기 판만 안 하고 있었다. 같은 계산을 여기에도 넣는다 (§4-47).

using UnityEngine;

[ExecuteAlways]
public class MRGrabFrameFitter : MonoBehaviour
{
    // Tools → MR → 8이 만드는 자식 이름. 저쪽과 반드시 같아야 한다.
    private const string PlateName = "GrabPlate";

    // 예전 테두리 바들. 남아 있으면 꺼서 중복 판정을 막는다.
    private static readonly string[] LegacyBarNames =
    {
        "Bar_Top", "Bar_Bottom", "Bar_Left", "Bar_Right"
    };

    [Tooltip("기준이 되는 패널의 RectTransform. 비워두면 부모에서 찾는다.")]
    [SerializeField] private RectTransform panelRect;

    [Tooltip("패널 가장자리 바깥으로 판이 더 나가는 양(m). 여기가 예전 '잡기 띠'에 해당한다.")]
    [SerializeField] private float edgeMargin = 0.06f;

    [Tooltip("판의 앞뒤 두께(m). 너무 얇으면 근접 grab이 안 걸린다.")]
    [SerializeField] private float plateDepth = 0.04f;

    [Tooltip("판의 **앞면**을 패널 평면보다 뒤로 미는 거리(m). " +
             "이 값이 작으면 버튼을 포크하려는 손이 grab 근접 영역에 들어간다.")]
    [SerializeField] private float backClearance = 0.03f;

    [Tooltip("이 주기(초)마다 크기가 바뀌었는지 확인해 다시 맞춘다. " +
             "RadialMenu처럼 열 때 항목이 생기는 위젯에 필요하다. 0이면 폴링하지 않는다.")]
    [SerializeField] private float refitInterval = 0.25f;

    // 판정 면(MRHandInteractionFitter)이 만드는 자식 이름. 잡기 영역 계산에서 제외한다.
    private const string InteractionChildName = "HandInteraction";

    private Vector2 _lastArea = new Vector2(-1f, -1f);
    private Vector3 _lastLossy = Vector3.zero;
    private float _timer;

    private void OnEnable()
    {
        _lastArea = new Vector2(-1f, -1f);
        Fit();
    }

    private void Update()
    {
        if (refitInterval <= 0f) return;

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;

        _timer = refitInterval;
        Fit();
    }

    /// <summary>패널의 현재 크기·스케일 기준으로 잡기 판을 다시 맞춘다.</summary>
    public void Fit()
    {
        if (panelRect == null) panelRect = transform.parent as RectTransform;
        if (panelRect == null) return;

        Vector3 lossy = panelRect.lossyScale;
        if (Mathf.Abs(lossy.x) < 1e-9f || Mathf.Abs(lossy.y) < 1e-9f) return;

        // 캔버스 스케일을 상쇄해 world scale = 1로 만든다 — 그래야 콜라이더 크기를
        // 미터 단위로 직접 지정할 수 있다.
        transform.localRotation = Quaternion.identity;
        transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);

        // 잡기 영역 = 패널 rect ∪ 자식들의 실제 경계.
        // RadialMenu처럼 항목이 rect 밖으로 뻗는 위젯을 위해서다.
        Rect rectPx = panelRect.rect;
        Bounds area = new Bounds(new Vector3(rectPx.center.x, rectPx.center.y, 0f),
                                 new Vector3(rectPx.width, rectPx.height, 0f));

        Bounds contentPx = ComputeContentBounds();
        if (contentPx.size.x > 0.01f && contentPx.size.y > 0.01f) area.Encapsulate(contentPx);

        // 아직 항목이 없어 영역이 사실상 0이면 맞추지 않는다 — 손이 닿을 수 없는 판이 굳는다.
        if (area.size.x < 1f || area.size.y < 1f) return;

        // 피벗이 중앙이 아닐 수 있으므로 영역 중심으로 맞춘다.
        transform.localPosition = new Vector3(area.center.x, area.center.y, 0f);

        float widthM = area.size.x * Mathf.Abs(lossy.x);
        float heightM = area.size.y * Mathf.Abs(lossy.y);

        Vector2 areaSize = new Vector2(area.size.x, area.size.y);
        if (areaSize == _lastArea && lossy == _lastLossy) return;
        _lastArea = areaSize;
        _lastLossy = lossy;

        DisableLegacyBars();

        Transform plate = transform.Find(PlateName);
        if (plate == null) return;

        plate.localRotation = Quaternion.identity;
        plate.localScale = Vector3.one;

        // 판의 앞면이 패널 평면보다 backClearance 만큼 뒤에 오도록 중심을 잡는다.
        float centerZ = backClearance + plateDepth * 0.5f;
        plate.localPosition = new Vector3(0f, 0f, centerZ);

        var box = plate.GetComponent<BoxCollider>();
        if (box == null) return;

        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(
            widthM + edgeMargin * 2f,
            heightM + edgeMargin * 2f,
            plateDepth);
    }

    /// <summary>패널의 자식 RectTransform들이 실제로 차지하는 경계(패널 로컬 px).
    /// 자기 자신(GrabFrame)과 판정 면(HandInteraction)은 제외한다 — 그것들은 결과가 아니라 원인이다.</summary>
    private Bounds ComputeContentBounds()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;

        for (int i = 0; i < panelRect.childCount; i++)
        {
            var child = panelRect.GetChild(i) as RectTransform;
            if (child == null) continue;
            if (child == transform) continue;                       // GrabFrame 자신
            if (child.name == InteractionChildName) continue;       // 판정 면
            if (!child.gameObject.activeInHierarchy) continue;

            Vector3 center = panelRect.InverseTransformPoint(child.TransformPoint(child.rect.center));
            Vector3 size = new Vector3(child.rect.width * child.localScale.x,
                                       child.rect.height * child.localScale.y,
                                       0f);

            var b = new Bounds(center, size);
            if (first) { bounds = b; first = false; continue; }

            bounds.Encapsulate(b);
        }

        return bounds;
    }

    /// <summary>예전 테두리 바가 남아 있으면 끈다.
    /// 판과 같이 켜져 있으면 같은 자리에서 두 번 판정되어 grab이 튄다.
    /// 삭제는 Tools → MR → 8이 담당한다 — [ExecuteAlways]에서 오브젝트를 지우는 것은 위험하다.</summary>
    private void DisableLegacyBars()
    {
        for (int i = 0; i < LegacyBarNames.Length; i++)
        {
            Transform bar = transform.Find(LegacyBarNames[i]);
            if (bar == null) continue;
            if (!bar.gameObject.activeSelf) continue;

            bar.gameObject.SetActive(false);
            Debug.Log($"[MRGrabFrameFitter] '{panelRect.name}'의 옛 잡기 바 '{LegacyBarNames[i]}'를 껐습니다. " +
                      "Tools → MR → 8을 다시 실행하면 정리됩니다.", this);
        }
    }
}
