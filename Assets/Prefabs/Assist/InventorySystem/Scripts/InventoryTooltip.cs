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

        // 배치: 커서 오른쪽-위 (+오프셋), 화면 밖으로 나가지 않게 대략 클램프
        float scale = rootCanvas.scaleFactor;
        float x = Mathf.Min(screenPos.x + 16f, Screen.width - Width * scale);
        float y = Mathf.Min(screenPos.y + 16f, Screen.height - 120f * scale);
        rt.position = new Vector3(x, y, 0f);
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
