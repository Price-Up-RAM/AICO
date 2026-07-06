using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AIStatus UI 공통 팩토리/팔레트. MissionUi/SkillView 계보를 그대로 잇고,
// 상태 색(Ok/Warn/Bad)과 게이지(막대)·KV(라벨:값) 헬퍼를 추가한다.
public static class AIStatusUi
{
    // 다크 팔레트 (MissionUi와 동일 값)
    public static readonly Color RootBg = new Color(0.086f, 0.098f, 0.125f, 1f);
    public static readonly Color HeaderBg = new Color(0.125f, 0.141f, 0.173f, 1f);
    public static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    public static readonly Color PanelBg2 = new Color(0.153f, 0.169f, 0.204f, 1f);
    public static readonly Color GaugeBorder = new Color(0.30f, 0.33f, 0.39f, 1f);
    public static readonly Color GaugeBg = new Color(0.07f, 0.08f, 0.1f, 1f);
    public static readonly Color GaugeFill = new Color(0.35f, 0.78f, 0.45f, 1f);
    public static readonly Color Accent = new Color(0.243f, 0.325f, 0.502f, 1f);
    public static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    public static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);

    // AIStatus 고유 상태 색
    public static readonly Color StatusOk = new Color(0.35f, 0.78f, 0.45f, 1f);
    public static readonly Color StatusWarn = new Color(0.85f, 0.7f, 0.28f, 1f);
    public static readonly Color StatusBad = new Color(0.80f, 0.30f, 0.30f, 1f);

    // 베이크 시 빌트인 9-slice 스프라이트/폰트를 외부(에디터 빌더)에서 주입.
    public static Sprite RoundedSpriteOverride;
    public static TMP_FontAsset FontOverride;

    public static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        go.layer = uiLayer >= 0 ? uiLayer : 5;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one;
        return go;
    }

    public static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        Image image = go.AddComponent<Image>();
        ApplyRounded(image, color);
        return go;
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        if (FontOverride != null)
        {
            text.font = FontOverride;
        }
        else if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return text;
    }

    public static Button CreateButton(string name, Transform parent, string label, Color background, float fontSize)
    {
        GameObject root = CreatePanel(name, parent, background);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI text = CreateText("Text", root.transform, label, fontSize, TextWhite, TextAlignmentOptions.Center);
            SetStretch(text.gameObject, Vector4.zero);
        }

        return button;
    }

    public static void ApplyRounded(Image image, Color color)
    {
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
    }

    public static Sprite GetRoundedSprite()
    {
        if (RoundedSpriteOverride != null)
        {
            return RoundedSpriteOverride;
        }

        return Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
    }

    public static void SetStretch(GameObject go, Vector4 padding)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding.x, padding.y);   // left, bottom
        rect.offsetMax = new Vector2(-padding.z, -padding.w); // right, top
    }

    public static LayoutElement Layout(GameObject go, float minH = -1f, float prefH = -1f, float minW = -1f,
        float prefW = -1f, float flexW = -1f, float flexH = -1f)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = go.AddComponent<LayoutElement>();
        }

        if (minH >= 0f) element.minHeight = minH;
        if (prefH >= 0f) element.preferredHeight = prefH;
        if (minW >= 0f) element.minWidth = minW;
        if (prefW >= 0f) element.preferredWidth = prefW;
        if (flexW >= 0f) element.flexibleWidth = flexW;
        if (flexH >= 0f) element.flexibleHeight = flexH;
        return element;
    }

    public static HorizontalLayoutGroup AddRow(GameObject go, float spacing, RectOffset padding = null)
    {
        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static VerticalLayoutGroup AddColumn(GameObject go, float spacing, RectOffset padding = null)
    {
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
        {
            component = go.AddComponent<T>();
        }

        return component;
    }

    public static Transform FindDeepChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }

            Transform found = FindDeepChild(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public static T FindComponent<T>(Transform root, string name) where T : Component
    {
        Transform t = FindDeepChild(root, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    // 게이지(막대): 테두리(Frame) → 배경 트랙(Bg) → 너비로 채우는 Fill(anchorMax.x=진행률).
    public static GameObject CreateGauge(string name, Transform parent, out RectTransform fillRect, Color fillColor)
    {
        GameObject frame = CreatePanel(name, parent, GaugeBorder);

        GameObject bg = CreatePanel("Bg", frame.transform, GaugeBg);
        SetStretch(bg, new Vector4(2f, 2f, 2f, 2f));

        GameObject fill = CreatePanel("Fill", bg.transform, fillColor);
        fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);   // 시작 0%
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        return frame;
    }

    // 게이지 채움 비율(0~1) 반영. Fill의 anchorMax.x만 조절.
    public static void SetGauge(RectTransform fill, float ratio01)
    {
        if (fill == null)
        {
            return;
        }

        float p = Mathf.Clamp01(ratio01);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(p, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }

    // 라벨:값 한 줄(좌 라벨 flexW:1 + 우 값 MidlineRight). out으로 값 텍스트를 돌려준다.
    public static GameObject CreateKvRow(string name, Transform parent, string key, out TextMeshProUGUI valueText)
    {
        GameObject row = CreateUIObject(name, parent);
        Layout(row, minH: 22f, prefH: 22f);
        AddRow(row, 8f).childForceExpandHeight = true;

        TextMeshProUGUI k = CreateText("Key", row.transform, key, 14f, TextMuted, TextAlignmentOptions.MidlineLeft);
        Layout(k.gameObject, prefW: 96f, minW: 72f);

        valueText = CreateText("Value", row.transform, "-", 14f, TextWhite, TextAlignmentOptions.MidlineRight);
        Layout(valueText.gameObject, flexW: 1f);
        return row;
    }

    // TMP_Dropdown을 코드로 완전 조립(Template/Viewport(Mask)/Content/Item(Toggle)). SkillView 관용구 기반.
    public static TMP_Dropdown CreateDropdown(string name, Transform parent)
    {
        GameObject root = CreatePanel(name, parent, PanelBg2);
        TMP_Dropdown dropdown = root.AddComponent<TMP_Dropdown>();

        TextMeshProUGUI label = CreateText("Label", root.transform, "", 14f, TextWhite, TextAlignmentOptions.MidlineLeft);
        SetStretch(label.gameObject, new Vector4(10f, 2f, 26f, 2f));

        // Template (펼침 리스트). 아래로 펼침(하단 anchor, pivot top).
        GameObject template = CreatePanel("Template", root.transform, PanelBg);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, 200f);

        ScrollRect scroll = template.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        GameObject viewport = CreateUIObject("Viewport", template.transform);
        SetStretch(viewport, Vector4.zero);
        Image viewportImg = viewport.AddComponent<Image>();
        ApplyRounded(viewportImg, PanelBg);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 30f);

        GameObject item = CreateUIObject("Item", content.transform);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 30f);

        Toggle itemToggle = item.AddComponent<Toggle>();
        itemToggle.toggleTransition = Toggle.ToggleTransition.None;

        GameObject itemBg = CreatePanel("Item Background", item.transform, PanelBg2);
        SetStretch(itemBg, Vector4.zero);
        itemToggle.targetGraphic = itemBg.GetComponent<Image>();

        GameObject itemChk = CreatePanel("Item Checkmark", item.transform, Accent);
        SetStretch(itemChk, Vector4.zero);
        itemToggle.graphic = itemChk.GetComponent<Image>();

        TextMeshProUGUI itemLabel = CreateText("Item Label", item.transform, "Option", 14f, TextWhite,
            TextAlignmentOptions.MidlineLeft);
        SetStretch(itemLabel.gameObject, new Vector4(10f, 1f, 10f, 1f));

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;

        dropdown.template = templateRect;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        dropdown.targetGraphic = root.GetComponent<Image>();

        template.SetActive(false);
        return dropdown;
    }
}
