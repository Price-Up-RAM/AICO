using TMPro;
using UnityEngine;
using UnityEngine.UI;

// MemoryArchive UI 공통 팩토리/팔레트. MissionUi/AIStatusUi 계보를 그대로 잇는다 (필요한 부분만 발췌).
public static class MemoryArchiveUi
{
    private static readonly string[] TranslatableMessagePrefixes =
    {
        "요약 요청 생성 실패: ",
        "요약 응답을 읽지 못했습니다: ",
        "학습 요청 생성 실패: ",
        "학습 응답을 읽지 못했습니다: "
    };

    // 다크 팔레트 (MissionUi/AIStatusUi와 동일 값)
    public static readonly Color RootBg = new Color(0.086f, 0.098f, 0.125f, 1f);
    public static readonly Color HeaderBg = new Color(0.125f, 0.141f, 0.173f, 1f);
    public static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    public static readonly Color PanelBg2 = new Color(0.153f, 0.169f, 0.204f, 1f);
    public static readonly Color Accent = new Color(0.243f, 0.325f, 0.502f, 1f);
    public static readonly Color AccentGreen = new Color(0.35f, 0.78f, 0.45f, 1f);
    public static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    public static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);
    public static readonly Color ScrollTrack = new Color(0f, 0f, 0f, 0.30f);
    public static readonly Color ScrollHandle = new Color(0.30f, 0.33f, 0.39f, 1f);

    // 베이크 시 빌트인 9-slice 스프라이트/폰트를 외부(에디터 빌더)에서 주입
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

    public static string Translate(string text)
    {
        if (string.IsNullOrEmpty(text) || SettingManager.Instance == null ||
            SettingManager.Instance.settings == null ||
            string.IsNullOrEmpty(SettingManager.Instance.settings.ui_language))
        {
            return text;
        }

        return Translate(text, SettingManager.Instance.settings.ui_language);
    }

    public static string Translate(string text, string targetLang)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(targetLang))
        {
            return text;
        }

        string translated = LanguageData.Translate(text, targetLang);
        if (!string.Equals(translated, text, System.StringComparison.Ordinal))
        {
            return translated;
        }

        // 예외 메시지가 뒤에 붙는 오류문은 고정 접두사만 번역하고 원문 상세를 보존한다.
        foreach (string prefix in TranslatableMessagePrefixes)
        {
            if (!text.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            string translatedPrefix = LanguageData.Translate(prefix, targetLang);
            return translatedPrefix + text.Substring(prefix.Length);
        }

        return translated;
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
