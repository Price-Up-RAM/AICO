using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// JukeboxDownloader 전용 hover 상세 툴팁 (InventoryTooltip과 동일 방법론의 독립 구현).
/// 결과 행에 마우스를 올리면 잘리지 않은 전체 제목 + 채널/길이/조회수를 보여준다.
/// 전부 코드로 런타임 생성, 동시에 1개. 모든 요소가 raycastTarget=false라
/// 포인터를 절대 가로채지 않는다 (가로채면 enter/exit가 깜빡거림).
/// </summary>
public class JukeboxDownloaderTooltip : MonoBehaviour
{
    // ── 다크 팔레트 (InventoryTooltip과 동일 계열) ─────────────────────────────
    private static readonly Color PanelBg = new Color(0.07f, 0.07f, 0.09f, 0.97f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted = new Color(0.62f, 0.64f, 0.68f, 1f);

    private const float Width = 260f;         // 툴팁 폭 (긴 제목이 줄바꿈으로 다 보이도록)
    private const float CursorOffset = 14f;   // 커서와의 간격

    private static JukeboxDownloaderTooltip current; // 현재 표시 중인 툴팁 (동시 1개)

    private Canvas canvas; // 좌표 변환용 (Overlay/Camera 모드 모두 지원)

    // 툴팁 표시 (screenPos = 포인터 위치, font = 한글 지원 폰트(SUIT-Bold))
    public static void Show(Canvas rootCanvas, Vector2 screenPos, string title, string body, TMP_FontAsset font)
    {
        Hide();

        if (rootCanvas == null || string.IsNullOrEmpty(title))
        {
            return;
        }

        GameObject go = new GameObject("JukeboxDownloaderTooltip", typeof(RectTransform));
        go.layer = 5; // UI 레이어
        go.transform.SetParent(rootCanvas.transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.pivot = new Vector2(0f, 0f); // 커서의 오른쪽-위로 펼침
        rt.sizeDelta = new Vector2(Width, 0f);
        rt.SetAsLastSibling();

        Image bg = go.AddComponent<Image>();
        bg.color = PanelBg;
        bg.raycastTarget = false; // 포인터 가로채기 금지

        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        current = go.AddComponent<JukeboxDownloaderTooltip>();

        // 전체 제목 (줄바꿈 허용 — 목록에서 잘린 제목을 여기서 다 보여준다)
        TMP_Text titleText = current.CreateText(rt, "Title", title, 13.5f, FontStyles.Bold, TextWhite, font);
        titleText.textWrappingMode = TextWrappingModes.Normal;

        // 본문 (채널 / 길이·조회수)
        if (string.IsNullOrEmpty(body) == false)
        {
            TMP_Text bodyText = current.CreateText(rt, "Body", body, 11.5f, FontStyles.Normal, TextMuted, font);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
        }

        // 높이를 즉시 확정해야 첫 프레임 클램프가 정확하다 (ContentSizeFitter는 다음 레이아웃 패스에 계산됨)
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        current.canvas = rootCanvas;
        current.Position(screenPos);
    }

    // 포인터 이동 추적 (RowHover의 OnPointerMove에서 호출)
    public static void Move(Vector2 screenPos)
    {
        if (current != null)
        {
            current.Position(screenPos);
        }
    }

    // 커서 오른쪽-위에 배치. 스크린 좌표를 캔버스 로컬로 변환해
    // Overlay/Camera 렌더 모드와 CanvasScaler 배율에 모두 정확하다.
    private void Position(Vector2 screenPos)
    {
        RectTransform rt = (RectTransform)transform;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out local))
        {
            return;
        }

        local += new Vector2(CursorOffset, CursorOffset); // pivot(0,0)이라 여기서부터 오른쪽-위로 펼쳐진다

        // 캔버스 밖으로 나가지 않게 클램프
        Rect bounds = canvasRect.rect;
        local.x = Mathf.Clamp(local.x, bounds.xMin, bounds.xMax - rt.rect.width);
        local.y = Mathf.Clamp(local.y, bounds.yMin, bounds.yMax - rt.rect.height);
        rt.localPosition = local;
    }

    // 툴팁 숨김
    public static void Hide()
    {
        if (current != null)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    // TMP 텍스트 생성 (raycastTarget=false 고정)
    private TMP_Text CreateText(RectTransform parent, string name, string content, float fontSize, FontStyles style, Color color, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;

        return text;
    }
}

/// <summary>
/// 결과 행에 붙는 hover 감지 컴포넌트 (InventorySlotView의 enter/exit 패턴).
/// 행 생성 시 AddComponent로 부착하고 title/body/font를 채워 쓴다.
/// </summary>
public class JukeboxDownloaderRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [HideInInspector] public string title;
    [HideInInspector] public string body;
    [HideInInspector] public TMP_FontAsset font;

    public void OnPointerEnter(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        JukeboxDownloaderTooltip.Show(canvas != null ? canvas.rootCanvas : null, eventData.position, title, body, font);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        JukeboxDownloaderTooltip.Move(eventData.position); // 커서를 따라다닌다
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        JukeboxDownloaderTooltip.Hide();
    }

    private void OnDisable()
    {
        JukeboxDownloaderTooltip.Hide();
    }
}
