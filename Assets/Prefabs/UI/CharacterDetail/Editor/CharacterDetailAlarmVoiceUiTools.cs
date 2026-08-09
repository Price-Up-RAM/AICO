using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterDetailAlarmVoiceUiTools
{
    private const string PrefabPath = "Assets/Prefabs/UI/CharacterDetail/CharacterDetail.prefab";

    private sealed class CustomVoiceRefs
    {
        public RectTransform section;
        public TextMeshProUGUI alarmSummary;
        public TextMeshProUGUI pomodoroSummary;
        public Button alarmOpen;
        public Button pomodoroOpen;
    }

    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "[CharacterDetail][AlarmVoiceUI] Prefab bake skipped while Unity is in Play Mode.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            CharacterDetailController controller = root.GetComponent<CharacterDetailController>();
            Transform infoContent = FindDeep(root.transform, "InfoContent");
            if (controller == null || infoContent == null)
            {
                Debug.LogError("[CharacterDetail][AlarmVoiceUI] Controller or InfoContent was not found.");
                return;
            }

            RemoveLegacyAlarmControls(root.transform);
            CustomVoiceRefs refs = BuildCustomVoiceSection(infoContent, root.transform);

            SerializedObject serializedController = new SerializedObject(controller);
            SetRef(serializedController, "customAlarmVoiceToggle", null);
            SetRef(serializedController, "customAlarmVoiceToggleText", null);
            SetRef(serializedController, "alarmSamplePlayButton", null);
            SetRef(serializedController, "alarmGenerateButton", null);
            SetRef(serializedController, "alarmGeneratedPlayButton", null);
            SetRef(serializedController, "alarmVoiceListRoot", null);
            SetRef(serializedController, "alarmVoiceListContent", null);
            SetRef(serializedController, "customVoiceSection", refs.section);
            SetRef(serializedController, "alarmVoiceSummaryText", refs.alarmSummary);
            SetRef(serializedController, "pomodoroVoiceSummaryText", refs.pomodoroSummary);
            SetRef(serializedController, "alarmVoiceOpenButton", refs.alarmOpen);
            SetRef(serializedController, "pomodoroVoiceOpenButton", refs.pomodoroOpen);
            SetRef(
                serializedController,
                "characterAlarmVoiceCatalog",
                Resources.Load<CharacterAlarmVoiceCatalog>(
                    "CharacterAlarmVoiceCatalog"));
            SetRef(
                serializedController,
                "characterPomodoroVoiceCatalog",
                Resources.Load<CharacterPomodoroVoiceCatalog>(
                    "CharacterPomodoroVoiceCatalog"));
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterDetail][CustomVoice] Navigation section baked.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void BatchSetup()
    {
        Setup();
    }

    public static void ValidateBatch()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        CharacterDetailController controller =
            prefab != null ? prefab.GetComponent<CharacterDetailController>() : null;
        CharacterAlarmVoiceCatalog catalog =
            Resources.Load<CharacterAlarmVoiceCatalog>("CharacterAlarmVoiceCatalog");
        CharacterPomodoroVoiceCatalog pomodoroCatalog =
            Resources.Load<CharacterPomodoroVoiceCatalog>(
                "CharacterPomodoroVoiceCatalog");
        if (prefab == null ||
            controller == null ||
            catalog == null ||
            pomodoroCatalog == null)
        {
            throw new System.InvalidOperationException(
                "[CharacterDetail][AlarmVoiceUI] Prefab, controller, or default voice catalog is missing.");
        }

        SerializedObject serializedController = new SerializedObject(controller);
        string[] requiredReferences =
        {
            "customVoiceSection",
            "alarmVoiceSummaryText",
            "pomodoroVoiceSummaryText",
            "alarmVoiceOpenButton",
            "pomodoroVoiceOpenButton",
            "characterAlarmVoiceCatalog",
            "characterPomodoroVoiceCatalog"
        };
        for (int i = 0; i < requiredReferences.Length; i++)
        {
            SerializedProperty property =
                serializedController.FindProperty(requiredReferences[i]);
            if (property == null || property.objectReferenceValue == null)
            {
                throw new System.InvalidOperationException(
                    "[CharacterDetail][AlarmVoiceUI] Missing prefab reference: " +
                    requiredReferences[i]);
            }
        }

        if (FindDeep(prefab.transform, "CustomAlarmVoiceToggle") != null ||
            FindDeep(prefab.transform, "AlarmSamplePlayButton") != null ||
            FindDeep(prefab.transform, "AlarmGenerateButton") != null ||
            FindDeep(prefab.transform, "AlarmGeneratedPlayButton") != null)
        {
            throw new System.InvalidOperationException(
                "[CharacterDetail][CustomVoice] Legacy inline alarm controls remain.");
        }

        Debug.Log("[CharacterDetail][CustomVoice] Validation passed.");
    }

    private static void RemoveLegacyAlarmControls(Transform root)
    {
        string[] legacyNames =
        {
            "CustomVoiceSection",
            "CustomAlarmVoiceToggle",
            "DefaultAlarmVoiceLabelText",
            "AlarmSamplePlayButton",
            "AlarmGenerateButton",
            "AlarmGeneratedPlayButton",
            "AlarmVoiceListScroll"
        };
        for (int i = 0; i < legacyNames.Length; i++)
        {
            Transform target = FindDeep(root, legacyNames[i]);
            if (target != null)
            {
                Object.DestroyImmediate(target.gameObject);
            }
        }
    }

    private static CustomVoiceRefs BuildCustomVoiceSection(
        Transform infoContent,
        Transform prefabRoot)
    {
        TextMeshProUGUI referenceText =
            FindDeep(prefabRoot, "ConversationCountText")?.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = referenceText != null ? referenceText.font : null;
        Color textColor = referenceText != null ? referenceText.color : Color.white;

        GameObject sectionObject = CreateUiObject("CustomVoiceSection", infoContent);
        RectTransform section = sectionObject.GetComponent<RectTransform>();
        SetTopStretch(section, -374f, 156f);

        TextMeshProUGUI title = CreateText(
            "CustomVoiceTitleText",
            section,
            "Custom Voice",
            font,
            18f,
            textColor,
            TextAlignmentOptions.MidlineLeft);
        title.fontStyle = FontStyles.Bold;
        SetTopStretch(title.rectTransform, 0f, 24f);

        GameObject scrollObject = CreateUiObject("AlarmVoiceListScroll", section);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetTopStretch(scrollRectTransform, -26f, 126f);
        Image background = scrollObject.AddComponent<Image>();
        background.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = new Color(0.055f, 0.065f, 0.085f, 0.95f);

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        GameObject viewport = CreateUiObject("Viewport", scrollObject.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, 4f, 24f, 4f, 8f);
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUiObject("CustomVoiceEntryContent", viewport.transform);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup column = contentObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(2, 2, 2, 2);
        column.spacing = 4f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewportRect;
        scroll.content = content;
        BuildCustomVoiceScrollbar(scrollObject.transform, scroll);

        TextMeshProUGUI alarmSummary;
        Button alarmOpen;
        BuildEntryRow(
            content,
            "AlarmVoiceEntry",
            "Alarm",
            font,
            textColor,
            out alarmSummary,
            out alarmOpen);

        TextMeshProUGUI pomodoroSummary;
        Button pomodoroOpen;
        BuildEntryRow(
            content,
            "PomodoroVoiceEntry",
            "Pomodoro",
            font,
            textColor,
            out pomodoroSummary,
            out pomodoroOpen);

        return new CustomVoiceRefs
        {
            section = section,
            alarmSummary = alarmSummary,
            pomodoroSummary = pomodoroSummary,
            alarmOpen = alarmOpen,
            pomodoroOpen = pomodoroOpen
        };
    }

    private static void BuildEntryRow(
        Transform parent,
        string rowName,
        string labelValue,
        TMP_FontAsset font,
        Color textColor,
        out TextMeshProUGUI summary,
        out Button open)
    {
        GameObject row = CreateUiObject(rowName, parent);
        Image background = row.AddComponent<Image>();
        background.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = new Color(0.11f, 0.13f, 0.17f, 1f);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 35f;
        rowLayout.preferredHeight = 35f;
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 8, 4, 4);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        TextMeshProUGUI label = CreateText(
            "LabelText", row.transform, labelValue, font, 14f, textColor,
            TextAlignmentOptions.MidlineLeft);
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.minWidth = 86f;
        labelLayout.preferredWidth = 86f;

        string summaryName =
            labelValue == "Alarm" ? "AlarmVoiceSummaryText" : "PomodoroVoiceSummaryText";
        summary = CreateText(
            summaryName, row.transform, "-", font, 13f, new Color(0.68f, 0.72f, 0.8f, 1f),
            TextAlignmentOptions.MidlineLeft);
        LayoutElement summaryLayout = summary.gameObject.AddComponent<LayoutElement>();
        summaryLayout.flexibleWidth = 1f;

        string buttonName =
            labelValue == "Alarm" ? "AlarmVoiceOpenButton" : "PomodoroVoiceOpenButton";
        open = CreateButton(buttonName, row.transform, "열기", font);
        LayoutElement buttonLayout = open.gameObject.AddComponent<LayoutElement>();
        buttonLayout.minWidth = 64f;
        buttonLayout.preferredWidth = 64f;
    }

    private static void BuildCustomVoiceScrollbar(Transform parent, ScrollRect scroll)
    {
        GameObject scrollbarObject = CreateUiObject("CustomVoiceScrollbar", parent);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-18f, 8f);
        scrollbarRect.offsetMax = new Vector2(-7f, -8f);

        Image track = scrollbarObject.AddComponent<Image>();
        track.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        track.type = Image.Type.Sliced;
        track.color = new Color(0.12f, 0.14f, 0.18f, 1f);

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = CreateUiObject("Sliding Area", scrollbarObject.transform);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        Stretch(slidingRect, 1f, 1f, 1f, 1f);

        GameObject handleObject = CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        Image handle = handleObject.AddComponent<Image>();
        handle.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        handle.type = Image.Type.Sliced;
        handle.color = new Color(0.31f, 0.40f, 0.61f, 1f);

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string value,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject obj = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        TMP_FontAsset font)
    {
        GameObject obj = CreateUiObject(objectName, parent);
        Image image = obj.AddComponent<Image>();
        image.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.306f, 0.404f, 0.608f, 1f);
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI text = CreateText(
            objectName + "_Text", obj.transform, label, font, 12f, Color.white,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    private static Toggle BuildCustomToggle(Transform root)
    {
        Transform target = FindDeep(root, "CustomAlarmVoiceToggle") ??
                           FindDeep(root, "DefaultAlarmVoiceLabelText");
        if (target == null)
        {
            Debug.LogError("[CharacterDetail][AlarmVoiceUI] DefaultAlarmVoiceLabelText was not found.");
            return null;
        }

        target.name = "CustomAlarmVoiceToggle";
        TextMeshProUGUI label = target.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            label = target.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        TMP_FontAsset font = label != null ? label.font : null;
        Color textColor = label != null ? label.color : Color.white;
        if (label != null && label.transform == target)
        {
            Object.DestroyImmediate(label);
        }

        Image background = target.GetComponent<Image>();
        if (background == null)
        {
            background = target.gameObject.AddComponent<Image>();
        }
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = new Color(0.047f, 0.055f, 0.071f, 1f);
        background.raycastTarget = true;

        Toggle toggle = target.GetComponent<Toggle>();
        if (toggle == null)
        {
            toggle = target.gameObject.AddComponent<Toggle>();
        }
        toggle.targetGraphic = background;
        toggle.graphic = null;
        toggle.transition = Selectable.Transition.ColorTint;
        toggle.isOn = false;

        Transform oldLabel = target.Find("CustomAlarmVoiceToggle_Text");
        if (oldLabel != null)
        {
            Object.DestroyImmediate(oldLabel.gameObject);
        }

        GameObject labelObject = CreateUiObject("CustomAlarmVoiceToggle_Text", target);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI toggleLabel = labelObject.AddComponent<TextMeshProUGUI>();
        toggleLabel.text = "커스텀 알람음성 : 사용안함";
        toggleLabel.font = font;
        toggleLabel.fontSize = 14f;
        toggleLabel.color = textColor;
        toggleLabel.alignment = TextAlignmentOptions.Center;
        toggleLabel.raycastTarget = false;

        RectTransform rect = target as RectTransform;
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(130f, 34f);
        }

        return toggle;
    }

    private static ScrollRect BuildAlarmListScroll(Transform infoContent, out RectTransform contentRect)
    {
        Transform existing = FindDeep(infoContent, "AlarmVoiceListScroll");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject scrollObject = CreateUiObject("AlarmVoiceListScroll", infoContent);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        SetTopLeft(scrollRectTransform, new Vector2(0f, -416f), new Vector2(440f, 170f));

        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scrollBackground.type = Image.Type.Sliced;
        scrollBackground.color = new Color(0.055f, 0.065f, 0.085f, 0.95f);

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;

        GameObject viewportObject = CreateUiObject("AlarmVoiceListViewport", scrollObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect, 4f, 16f, 4f, 4f);
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.005f);
        viewportImage.raycastTarget = true;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUiObject("AlarmVoiceListContent", viewportObject.transform);
        contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject scrollbarObject = CreateUiObject("AlarmVoiceListScrollbar", scrollObject.transform);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.offsetMin = new Vector2(-12f, 4f);
        scrollbarRect.offsetMax = new Vector2(-2f, -4f);
        Image scrollbarBackground = scrollbarObject.AddComponent<Image>();
        scrollbarBackground.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scrollbarBackground.type = Image.Type.Sliced;
        scrollbarBackground.color = new Color(0.12f, 0.14f, 0.18f, 1f);

        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = CreateUiObject("Sliding Area", scrollbarObject.transform);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        Stretch(slidingRect, 2f, 2f, 2f, 2f);

        GameObject handleObject = CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        Image handleImage = handleObject.AddComponent<Image>();
        handleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        handleImage.type = Image.Type.Sliced;
        handleImage.color = new Color(0.31f, 0.40f, 0.61f, 1f);

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scrollRect.verticalScrollbarSpacing = -3f;

        return scrollRect;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        result.layer = 5;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetTopStretch(RectTransform rect, float y, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static void Stretch(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetRef(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
        else
        {
            Debug.LogWarning("[CharacterDetail][AlarmVoiceUI] Missing serialized field: " + propertyName);
        }
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
