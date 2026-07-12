using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 미션 UI 공통 팩토리/팔레트. SkillView.cs의 헬퍼를 차용해 다크 테마로 통일.
public static class MissionUi
{
    // 다크 팔레트 (SkillView에서 추출)
    public static readonly Color RootBg = new Color(0.086f, 0.098f, 0.125f, 1f);
    public static readonly Color HeaderBg = new Color(0.125f, 0.141f, 0.173f, 1f);
    public static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    public static readonly Color PanelBg2 = new Color(0.153f, 0.169f, 0.204f, 1f);
    public static readonly Color TabBg = new Color(0.118f, 0.133f, 0.165f, 1f);
    public static readonly Color TabSelected = new Color(0.243f, 0.325f, 0.502f, 1f);
    public static readonly Color GaugeBorder = new Color(0.30f, 0.33f, 0.39f, 1f);
    public static readonly Color GaugeBg = new Color(0.07f, 0.08f, 0.1f, 1f);
    public static readonly Color GaugeFill = new Color(0.35f, 0.78f, 0.45f, 1f);
    public static readonly Color Accent = new Color(0.243f, 0.325f, 0.502f, 1f);
    public static readonly Color Gold = new Color(0.85f, 0.7f, 0.28f, 1f);
    public static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    public static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);
    public static readonly Color StampColor = new Color(0.85f, 0.27f, 0.27f, 1f);

    // 베이크 시 빌트인 9-slice 스프라이트를 외부(에디터 빌더)에서 주입.
    public static Sprite RoundedSpriteOverride;

    public static TMP_FontAsset FontOverride;

    // 보상 아이콘(외부 입력). 보상은 gold 단일. null이면 텍스트 폴백.
    public static Sprite GoldIcon;

    // 보상 셀에 골드 아이콘+수량을 채운다. 아이콘 없으면 텍스트 폴백(G50). (CardRow와 Drawer가 공용)
    public static void ApplyRewardCell(Image icon, TMP_Text amount, int value)
    {
        Sprite sprite = GoldIcon;
        if (icon != null)
        {
            icon.enabled = sprite != null;
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = Color.white;
            }
        }

        if (amount != null)
        {
            if (sprite != null)
            {
                amount.text = value.ToString();
                amount.alignment = TextAlignmentOptions.BottomRight;
                amount.fontSize = 13f;
            }
            else
            {
                amount.text = "G" + value;
                amount.alignment = TextAlignmentOptions.Center;
                amount.fontSize = 15f;
            }
        }
    }

    // 보상 셀: 사각 테두리 + 흰 배경(유지) + [Content(페이드 대상): 아이콘 + 우하단 수량].
    // 배경은 그대로 두고 Content만 페이드인/아웃하면 빈 칸 없이 보상이 바뀐다.
    public static GameObject CreateRewardCell(string name, Transform parent, out Image icon, out TMP_Text amount)
    {
        GameObject cell = CreatePanel(name, parent, GaugeBorder); // 테두리
        GameObject bg = CreatePanel("Bg", cell.transform, Color.white);
        SetStretch(bg, new Vector4(2f, 2f, 2f, 2f));

        GameObject content = CreateUIObject("Content", bg.transform); // 페이드 대상(아이콘+수량)
        SetStretch(content, Vector4.zero);
        content.AddComponent<CanvasGroup>();

        GameObject iconGo = CreateUIObject("Icon", content.transform);
        SetStretch(iconGo, new Vector4(5f, 5f, 5f, 7f));
        icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        amount = CreateText("Amount", content.transform, string.Empty, 13f, new Color(0.1f, 0.1f, 0.12f, 1f),
            TextAlignmentOptions.BottomRight);
        SetStretch(amount.gameObject, new Vector4(2f, 2f, 4f, 2f));
        amount.fontStyle = FontStyles.Bold;
        return cell;
    }

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
        text.enableWordWrapping = true;
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
}
