// GrabFrame(패널 테두리 잡기 영역)의 크기·스케일을 패널의 현재 상태에 맞춰 다시 계산한다.
//
// 왜 필요한가 (2026-08-15)
// ----------------------
// Tools → MR → 8 은 실행 시점의 패널 스케일을 읽어 GrabFrame의 보정 스케일(1/lossyScale)과
// 바 콜라이더 크기(m 단위)를 **구워서** 저장했다. 그래서 나중에 인스펙터에서 패널 크기를
// 바꾸면(예: 절반으로 축소) 잡기 띠만 옛 크기로 남아 조용히 어긋난다 —
// 실제로 말풍선들을 0.001 → 0.0005로 줄인 뒤 GrabFrame이 1000(옛 값)으로 남아
// 띠 두께가 두 배로 틀어진 사례가 있었다.
//
// 이 컴포넌트가 OnEnable에서 현재 값 기준으로 다시 맞추므로, 패널 크기를 바꿔도
// 툴을 다시 실행할 필요가 없다.
//
// 주의: 바(Bar_*) 오브젝트 자체는 Tools → MR → 8 이 만든다. 이 컴포넌트는 이미 있는
// 바들의 위치/크기만 갱신한다.

using UnityEngine;

[ExecuteAlways]
public class MRGrabFrameFitter : MonoBehaviour
{
    [Tooltip("기준이 되는 패널의 RectTransform. 비워두면 부모에서 찾는다.")]
    [SerializeField] private RectTransform panelRect;

    [Tooltip("패널 가장자리 바깥으로 잡을 수 있는 띠의 두께(m).")]
    [SerializeField] private float bandThickness = 0.06f;

    [Tooltip("콜라이더의 앞뒤 두께(m). 두꺼우면 UI 앞으로 튀어나와 조준을 가린다.")]
    [SerializeField] private float bandDepth = 0.008f;

    [Tooltip("패널 평면보다 뒤로 밀어낼 여유(m). UI가 항상 조준 우선권을 갖게 한다.")]
    [SerializeField] private float extraZOffset = 0.004f;

    private void OnEnable()
    {
        Fit();
    }

    /// <summary>패널의 현재 크기·스케일 기준으로 GrabFrame과 바들을 다시 맞춘다.</summary>
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

        // 피벗이 중앙이 아닐 수 있으므로 rect 중심으로 맞춘다.
        Vector2 centerPx = panelRect.rect.center;
        transform.localPosition = new Vector3(centerPx.x, centerPx.y, 0f);

        float widthM = panelRect.rect.width * Mathf.Abs(lossy.x);
        float heightM = panelRect.rect.height * Mathf.Abs(lossy.y);

        float t = bandThickness;
        float halfW = widthM * 0.5f;
        float halfH = heightM * 0.5f;
        float z = bandDepth * 0.5f + extraZOffset;

        // 상/하는 좌우 모서리까지 덮도록 띠 두께만큼 더 길게.
        FitBar("Bar_Top", new Vector3(0f, halfH + t * 0.5f, z), new Vector3(widthM + t * 2f, t, bandDepth));
        FitBar("Bar_Bottom", new Vector3(0f, -(halfH + t * 0.5f), z), new Vector3(widthM + t * 2f, t, bandDepth));
        FitBar("Bar_Left", new Vector3(-(halfW + t * 0.5f), 0f, z), new Vector3(t, heightM, bandDepth));
        FitBar("Bar_Right", new Vector3(halfW + t * 0.5f, 0f, z), new Vector3(t, heightM, bandDepth));
    }

    private void FitBar(string barName, Vector3 localPos, Vector3 size)
    {
        Transform bar = transform.Find(barName);
        if (bar == null) return;

        bar.localRotation = Quaternion.identity;
        bar.localScale = Vector3.one;
        bar.localPosition = localPos;

        var box = bar.GetComponent<BoxCollider>();
        if (box == null) return;
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = size;
    }
}
