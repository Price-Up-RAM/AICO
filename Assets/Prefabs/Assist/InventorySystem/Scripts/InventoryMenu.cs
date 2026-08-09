using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 컨텍스트 메뉴 엔트리 (라벨 + 실행 액션)
public class InventoryMenuEntry
{
    public string label;    // 표시 라벨
    public Action action;   // 클릭 시 실행 (실행 후 메뉴 자동 닫힘)
}

// 인벤토리 우클릭 컨텍스트 메뉴 + 상세 팝업.
// Devion UIWidgets의 ContextMenu(AddMenuItem/ShowAtScreenPosition/바깥 클릭 닫기)를 참고하되
// 서브메뉴 없이 단일 메뉴만 제공하며, 전부 코드로 런타임 생성한다 (베이크 불필요).
// 백드롭(풀스크린 투명) 클릭 = 닫기, 엔트리 클릭 = 액션 실행 후 닫기. 동시에 1개만 열림.
public class InventoryMenu : MonoBehaviour
{
    // ── 다크 팔레트 ──────────────────────────────────────────────
    private static readonly Color PanelBg = new Color(0.09f, 0.09f, 0.11f, 0.98f);   // 메뉴 패널 배경
    private static readonly Color EntryBg = new Color(0.16f, 0.16f, 0.20f, 1f);      // 엔트리 버튼 배경
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);    // 본문 텍스트
    private static readonly Color TextMuted = new Color(0.62f, 0.64f, 0.68f, 1f);    // 상세 본문 텍스트

    private const float MenuWidth = 160f;    // 컨텍스트 메뉴 폭
    private const float DetailWidth = 250f;  // 상세 팝업 폭
    private const float EntryHeight = 30f;   // 엔트리 버튼 높이

    private static InventoryMenu current;    // 현재 열린 메뉴 (동시 1개)

    // 컨텍스트 메뉴 열기 (screenPos = 우클릭 위치, font = 한글 지원 폰트(SUIT) — 뷰의 헤더 폰트를 넘겨받는다)
    public static void Show(Canvas rootCanvas, Vector2 screenPos, List<InventoryMenuEntry> entries, TMP_FontAsset font)
    {
        Close();

        if (rootCanvas == null || entries == null || entries.Count == 0)
        {
            return;
        }

        InventoryMenu menu = BuildBackdrop(rootCanvas);
        RectTransform panel = menu.BuildPanel(MenuWidth);

        foreach (InventoryMenuEntry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.label))
            {
                continue;
            }

            menu.AddMenuItem(panel, entry.label, entry.action, font);
        }

        menu.PlacePanel(rootCanvas, panel, screenPos, MenuWidth, entries.Count * (EntryHeight + 2f) + 12f);
        current = menu;
    }

    // 상세 팝업 열기 (타이틀 + 본문 텍스트)
    public static void ShowDetail(Canvas rootCanvas, Vector2 screenPos, string title, string body, TMP_FontAsset font)
    {
        Close();

        if (rootCanvas == null)
        {
            return;
        }

        InventoryMenu menu = BuildBackdrop(rootCanvas);
        RectTransform panel = menu.BuildPanel(DetailWidth);

        TMP_Text titleText = menu.CreateText(panel, "Title", title, 15f, FontStyles.Bold, TextWhite, font);
        menu.AddLayoutHeight(titleText.gameObject, 24f);

        TMP_Text bodyText = menu.CreateText(panel, "Body", body, 12.5f, FontStyles.Normal, TextMuted, font);
        bodyText.textWrappingMode = TextWrappingModes.Normal;

        menu.PlacePanel(rootCanvas, panel, screenPos, DetailWidth, 140f);
        current = menu;
    }

    // 열린 메뉴 닫기
    public static void Close()
    {
        if (current != null)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    // ── 내부 빌더 ────────────────────────────────────────────────

    // 백드롭(풀스크린 투명 버튼 = 바깥 클릭 닫기) 생성
    private static InventoryMenu BuildBackdrop(Canvas rootCanvas)
    {
        GameObject backdropGo = new GameObject("InventoryMenu", typeof(RectTransform));
        backdropGo.layer = 5; // UI 레이어
        backdropGo.transform.SetParent(rootCanvas.transform, false);

        RectTransform rt = (RectTransform)backdropGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        Image img = backdropGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);  // 투명하지만 레이캐스트는 받는다
        img.raycastTarget = true;

        Button btn = backdropGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(Close);

        return backdropGo.AddComponent<InventoryMenu>();
    }

    // 메뉴/상세 공용 패널 생성 (세로 레이아웃 + 내용 크기 맞춤)
    private RectTransform BuildPanel(float width)
    {
        GameObject panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.layer = 5;
        panelGo.transform.SetParent(transform, false);

        RectTransform rt = (RectTransform)panelGo.transform;
        rt.pivot = new Vector2(0f, 1f);  // 커서에서 오른쪽-아래로 펼침
        rt.sizeDelta = new Vector2(width, 0f);

        Image bg = panelGo.AddComponent<Image>();
        bg.color = PanelBg;
        bg.raycastTarget = true;  // 패널 클릭이 백드롭(닫기)으로 새지 않게

        VerticalLayoutGroup layout = panelGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    // 메뉴 엔트리 추가 (Devion ContextMenu.AddMenuItem 참고: 라벨 + 클릭 시 액션 실행 후 닫기)
    private void AddMenuItem(RectTransform panel, string label, Action action, TMP_FontAsset font)
    {
        GameObject itemGo = new GameObject("Item_" + label, typeof(RectTransform));
        itemGo.layer = 5;
        itemGo.transform.SetParent(panel, false);

        Image bg = itemGo.AddComponent<Image>();
        bg.color = EntryBg;

        Button btn = itemGo.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(() =>
        {
            Close();
            action?.Invoke();
        });

        LayoutElement le = itemGo.AddComponent<LayoutElement>();
        le.preferredHeight = EntryHeight;

        TMP_Text text = CreateText((RectTransform)itemGo.transform, "Label", label, 13f, FontStyles.Bold, TextWhite, font);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 0f);
        textRt.offsetMax = new Vector2(-10f, 0f);
    }

    // 패널을 화면 좌표에 배치 (화면 밖으로 나가지 않게 대략 클램프)
    private void PlacePanel(Canvas rootCanvas, RectTransform panel, Vector2 screenPos, float width, float estimatedHeight)
    {
        float scale = rootCanvas.scaleFactor;
        float x = Mathf.Min(screenPos.x, Screen.width - width * scale);
        float y = Mathf.Max(screenPos.y, estimatedHeight * scale);
        panel.position = new Vector3(x, y, 0f);
    }

    // TMP 텍스트 생성
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

    // 레이아웃 그룹 자식의 높이 지정 헬퍼
    private void AddLayoutHeight(GameObject go, float height)
    {
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }
}
