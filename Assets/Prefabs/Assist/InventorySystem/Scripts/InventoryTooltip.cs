using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인벤토리 hover 미니 툴팁 (상세 팝업의 축소판: 이름 + 수량·분류 + 짧은 설명).
// 전부 코드로 런타임 생성, 동시에 1개. 모든 요소가 raycastTarget=false라
// 포인터를 절대 가로채지 않는다 (가로채면 enter/exit가 깜빡거림).
public class InventoryTooltip : MonoBehaviour
{
    // ── 다크 팔레트 ──────────────────────────────────────────────
    private static readonly Color PanelBg = new Color(0.07f, 0.07f, 0.09f, 0.97f);   // 툴팁 배경
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);    // 타이틀
    private static readonly Color TextMuted = new Color(0.62f, 0.64f, 0.68f, 1f);    // 본문

    private const float Width = 210f;  // 툴팁 폭

    // MR 좌표 변환 진단. 위치가 맞다고 확인되면 꺼도 된다.
    private static bool verboseLog = true;

    private static InventoryTooltip current;  // 현재 표시 중인 툴팁 (동시 1개)

    // 툴팁 표시 (screenPos = 포인터 위치, font = 한글 지원 폰트(SUIT))
    public static void Show(Canvas rootCanvas, Vector2 screenPos, string title, string body, TMP_FontAsset font)
    {
        Hide();

        if (rootCanvas == null || string.IsNullOrEmpty(title))
        {
            return;
        }

        GameObject go = new GameObject("InventoryTooltip", typeof(RectTransform));
        go.layer = 5; // UI 레이어
        go.transform.SetParent(rootCanvas.transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.pivot = new Vector2(0f, 0f);  // 커서의 오른쪽-위로 펼침
        rt.sizeDelta = new Vector2(Width, 0f);
        rt.SetAsLastSibling();

        // 부모 캔버스에 LayoutGroup이 있으면 이 툴팁이 **레이아웃 자식으로 편입된다.**
        //
        // InventoryPanel 루트에는 VerticalLayoutGroup이 붙어 있다. 데스크톱에서는 이 문제가
        // 없었는데, 그때는 패널에 Canvas가 없어서 GetComponentInParent<Canvas>()가 메인 Canvas까지
        // 올라갔고 툴팁이 그쪽에 붙었기 때문이다. MR 전환(Tools 6)이 패널 자체를 루트 캔버스로
        // 만들면서 툴팁이 패널 안쪽 레이아웃에 떨어지게 됐다 —
        // 배경이 패널 폭으로 늘어나고(childForceExpandWidth) 형제들이 위로 밀린다.
        // 게다가 밀린 레이아웃이 커서 아래 슬롯을 바꿔 Enter/Exit이 반복된다(§4-54).
        LayoutElement ignore = go.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;

        Image bg = go.AddComponent<Image>();
        bg.color = PanelBg;
        bg.raycastTarget = false;  // 포인터 가로채기 금지

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

        current = go.AddComponent<InventoryTooltip>();

        // 타이틀
        current.CreateText(rt, "Title", title, 13.5f, FontStyles.Bold, TextWhite, font);

        // 본문 (수량·분류·짧은 설명)
        if (string.IsNullOrEmpty(body) == false)
        {
            TMP_Text bodyText = current.CreateText(rt, "Body", body, 11.5f, FontStyles.Normal, TextMuted, font);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
        }

        PlaceAtPointer(rootCanvas, rt, screenPos);
    }

    // 커서 오른쪽-위에 놓는다.
    //
    // 예전 코드는 `rt.position = new Vector3(화면X, 화면Y, 0)`이었다. 스크린 스페이스 캔버스에서는
    // 캔버스 월드 단위 ≈ 픽셀이라 맞았지만, **월드 스페이스 캔버스에서 position은 미터다** —
    // 화면 좌표 (800, 450)을 넣으면 툴팁이 800 m 밖으로 날아간다 (Kickoff Guide §4-38).
    // 그래서 캔버스 로컬 좌표로 변환해 anchoredPosition에 넣는다. 두 렌더 모드 모두에서 맞다.
    private static void PlaceAtPointer(Canvas rootCanvas, RectTransform rt, Vector2 screenPos)
    {
        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        // 스크린 스페이스 오버레이만 카메라가 null이어야 한다. 나머지는 이벤트 카메라를 쓴다.
        Camera cam = rootCanvas.worldCamera;
        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            cam = null;
        }

        Vector2 local;
        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out local);

        // 캔버스 안에 머물게 클램프 — Screen.width가 아니라 **캔버스 rect** 기준이다.
        Rect area = canvasRect.rect;
        float maxX = area.xMax - Width;
        float maxY = area.yMax - 120f;

        Vector2 target = new Vector2(
            Mathf.Clamp(local.x + 16f, area.xMin, Mathf.Max(area.xMin, maxX)),
            Mathf.Clamp(local.y + 16f, area.yMin, Mathf.Max(area.yMin, maxY)));

        rt.anchoredPosition = target;

        // 지금 값과 판정 근거를 한 줄에 (§7-1 C). MR에서 포인터 좌표가 신뢰 가능한지 여기서 갈린다.
        if (verboseLog)
        {
            Debug.Log($"[MRInv/툴팁] 화면={screenPos} 변환성공={converted} 캔버스로컬={local} → 배치={target} " +
                      $"| 캔버스 rect={area.size} 모드={rootCanvas.renderMode} cam={(cam == null ? "null" : cam.name)}");
        }
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
            text.font = font;  // 한글 지원 폰트 (SUIT-Bold)
        }

        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;

        return text;
    }
}
