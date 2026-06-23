using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Jukebox UI 코드 빌드용 공용 팩토리. JukeboxView(메인)와 JukeboxEnvironmentView(SFX 팝업)가
/// 동일한 헬퍼로 위젯을 만들어 룩앤필을 통일한다. (SkillView의 빌드 헬퍼와 같은 패턴)
/// 둥근 모서리 스프라이트는 인자로 받아 프리팹에 직렬화되게 한다.
/// </summary>
public static class JukeboxUi
{
    // ── 다크 팔레트 ────────────────────────────────────────────────────────────
    public static readonly Color RootBg = new Color(0.086f, 0.098f, 0.125f, 1f);
    public static readonly Color HeaderBg = new Color(0.125f, 0.141f, 0.173f, 1f);
    public static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    public static readonly Color PanelBg2 = new Color(0.153f, 0.169f, 0.204f, 1f);
    public static readonly Color RowBg = new Color(0.118f, 0.133f, 0.165f, 1f);
    public static readonly Color ButtonBg = new Color(0.22f, 0.25f, 0.31f, 1f); // 헤더 위에서도 보이는 둥근 버튼 배경
    public static readonly Color AccentBlue = new Color(0.243f, 0.325f, 0.502f, 1f);
    public static readonly Color AccentBlueHi = new Color(0.306f, 0.404f, 0.608f, 1f);
    public static readonly Color FillColor = new Color(0.36f, 0.49f, 0.74f, 1f);
    public static readonly Color Orange = new Color(0.95f, 0.55f, 0.15f, 1f); // 재생 게이지

    public static readonly Color Track = new Color(0.047f, 0.055f, 0.071f, 1f);
    public static readonly Color Border = new Color(0.290f, 0.322f, 0.376f, 1f);
    public static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    public static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);

    public static GameObject Obj(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (go.layer < 0)
        {
            go.layer = 5;
        }
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one;
        return go;
    }

    public static void ApplyRounded(Image image, Sprite sprite, Color color)
    {
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }
        image.color = color;
    }

    public static GameObject Panel(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = Obj(name, parent);
        Image image = go.AddComponent<Image>();
        ApplyRounded(image, sprite, color);
        return go;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        GameObject go = Obj(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        TMP_FontAsset resolved = font != null ? font : TMP_Settings.defaultFontAsset;
        if (resolved != null)
        {
            text.font = resolved;
        }
        return text;
    }

    public static Button MakeButton(string name, Transform parent, string label, Color background, float fontSize, Sprite sprite, TMP_FontAsset font)
    {
        GameObject root = Panel(name, parent, sprite, background);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;

        TextMeshProUGUI text = Text("Text", root.transform, label, fontSize, TextWhite, TextAlignmentOptions.Center, font);
        Stretch(text.gameObject, Vector4.zero);
        return button;
    }

    // 체크박스형 토글 (라디오/멀티 공용)
    public static Toggle MakeToggle(string name, Transform parent, Sprite sprite)
    {
        GameObject root = Obj(name, parent);
        Toggle toggle = root.AddComponent<Toggle>();
        toggle.toggleTransition = Toggle.ToggleTransition.None;

        GameObject bg = Panel("Background", root.transform, sprite, Track);
        Stretch(bg, Vector4.zero);

        GameObject check = Panel("Checkmark", bg.transform, sprite, AccentBlueHi);
        Stretch(check, new Vector4(4f, 4f, 4f, 4f));

        toggle.targetGraphic = bg.GetComponent<Image>();
        toggle.graphic = check.GetComponent<Image>();
        return toggle;
    }

    // 가로 슬라이더 (0~1). Unity DefaultControls와 동일한 앵커/사이즈 구조로 구성해
    // Fill/Handle이 음수 폭으로 찌그러지지 않게 한다.
    public static Slider MakeSlider(string name, Transform parent, Sprite sprite)
    {
        GameObject root = Obj(name, parent);
        Slider slider = root.AddComponent<Slider>();

        // Background (가로 바)
        GameObject background = Panel("Background", root.transform, sprite, Track);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = Vector2.zero;

        // Fill Area
        GameObject fillArea = Obj("Fill Area", root.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.anchoredPosition = new Vector2(-5f, 0f);
        fillAreaRect.sizeDelta = new Vector2(-20f, 0f);

        GameObject fill = Panel("Fill", fillArea.transform, sprite, FillColor);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.sizeDelta = new Vector2(10f, 0f);

        // Handle Slide Area
        GameObject handleArea = Obj("Handle Slide Area", root.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.anchoredPosition = Vector2.zero;
        handleAreaRect.sizeDelta = new Vector2(-20f, 0f);

        GameObject handle = Panel("Handle", handleArea.transform, sprite, TextWhite);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 0f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    // 단일 라인 숫자 입력
    public static TMP_InputField NumberInput(string name, Transform parent, Sprite sprite, TMP_FontAsset font)
    {
        GameObject area = Panel(name, parent, sprite, Track);
        TMP_InputField input = area.AddComponent<TMP_InputField>();

        GameObject textArea = Obj("Text Area", area.transform);
        Stretch(textArea, new Vector4(6f, 2f, 6f, 2f));
        textArea.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = Text("Placeholder", textArea.transform, "0", 13, TextMuted, TextAlignmentOptions.Center, font);
        Stretch(placeholder.gameObject, Vector4.zero);
        TextMeshProUGUI text = Text("Text", textArea.transform, string.Empty, 13, TextWhite, TextAlignmentOptions.Center, font);
        Stretch(text.gameObject, Vector4.zero);

        input.textViewport = textArea.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.targetGraphic = area.GetComponent<Image>();
        return input;
    }

    // TMP_Dropdown (SkillView에서 베이크 검증된 구조와 동일).
    public static TMP_Dropdown MakeDropdown(string name, Transform parent, Sprite sprite, TMP_FontAsset font)
    {
        GameObject root = Panel(name, parent, sprite, PanelBg2);
        TMP_Dropdown dropdown = root.AddComponent<TMP_Dropdown>();

        TextMeshProUGUI label = Text("Label", root.transform, string.Empty, 15, TextWhite, TextAlignmentOptions.MidlineLeft, font);
        Stretch(label.gameObject, new Vector4(10f, 2f, 26f, 2f));

        GameObject arrow = Panel("Arrow", root.transform, sprite, TextMuted);
        RectTransform arrowRect = arrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-10f, 0f);
        arrowRect.sizeDelta = new Vector2(12f, 12f);

        GameObject template = Panel("Template", root.transform, sprite, PanelBg);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, 180f);

        ScrollRect scroll = template.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        GameObject viewport = Obj("Viewport", template.transform);
        Stretch(viewport, Vector4.zero);
        Image viewportImg = viewport.AddComponent<Image>();
        ApplyRounded(viewportImg, sprite, PanelBg);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = Obj("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 30f);

        GameObject item = Obj("Item", content.transform);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 30f);
        Toggle itemToggle = item.AddComponent<Toggle>();

        GameObject itemBackground = Obj("Item Background", item.transform);
        Stretch(itemBackground, Vector4.zero);
        Image itemBackgroundImg = itemBackground.AddComponent<Image>();
        itemBackgroundImg.color = new Color(0f, 0f, 0f, 0f);

        GameObject itemCheckmark = Panel("Item Checkmark", item.transform, sprite, AccentBlueHi);
        RectTransform checkRect = itemCheckmark.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0f, 0.5f);
        checkRect.anchorMax = new Vector2(0f, 0.5f);
        checkRect.pivot = new Vector2(0f, 0.5f);
        checkRect.anchoredPosition = new Vector2(10f, 0f);
        checkRect.sizeDelta = new Vector2(10f, 10f);

        TextMeshProUGUI itemLabel = Text("Item Label", item.transform, "Option", 14, TextWhite, TextAlignmentOptions.MidlineLeft, font);
        Stretch(itemLabel.gameObject, new Vector4(28f, 1f, 10f, 1f));

        itemToggle.targetGraphic = itemBackgroundImg;
        itemToggle.graphic = itemCheckmark.GetComponent<Image>();
        itemToggle.toggleTransition = Toggle.ToggleTransition.None;
        ColorBlock colors = itemToggle.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0f);
        colors.highlightedColor = AccentBlue;
        colors.selectedColor = AccentBlue;
        colors.pressedColor = AccentBlueHi;
        itemToggle.colors = colors;
        itemToggle.isOn = true;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;

        dropdown.template = templateRect;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        dropdown.targetGraphic = root.GetComponent<Image>();

        template.SetActive(false);
        return dropdown;
    }

    public static HorizontalLayoutGroup Row(GameObject go, float spacing, int padL = 0, int padR = 0, int padT = 0, int padB = 0)
    {
        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(padL, padR, padT, padB);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static VerticalLayoutGroup Column(GameObject go, float spacing, int pad = 0)
    {
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static LayoutElement Layout(GameObject go, float minH = -1f, float prefH = -1f, float minW = -1f, float prefW = -1f, float flexW = -1f, float flexH = -1f)
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

    public static void Stretch(GameObject go, Vector4 padding)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding.x, padding.y);   // left, bottom
        rect.offsetMax = new Vector2(-padding.z, -padding.w); // right, top
    }
}
