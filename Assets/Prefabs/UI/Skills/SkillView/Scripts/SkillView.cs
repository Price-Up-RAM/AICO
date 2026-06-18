using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skills UI. 어두운 테마의 스킬 편집 패널을 코드로 직접 구성한다.
/// 다른 UI들과 동일하게 Show / Hide / Refresh 진입점을 제공한다.
/// 스크롤(휠 포함)은 UGUI 기본 컴포넌트(ScrollRect / TMP_InputField)로만 처리하므로
/// 프로젝트 내 다른 스크립트에 의존하지 않는다.
///
/// 구성 요소
///  1. 헤더 (타이틀 + 닫기 버튼)
///  2. 셀렉터 행 : 스킬 드롭다운 + Refresh 버튼 + 언어 드롭다운
///  3. 태그 영역 : Unity / Local / Python 등 둥근 태그 (가변 리스트, 가로 스크롤)
///  4. CRUD 행   : 저장 / 되돌리기(reload)
///  5. 입력 영역 : 멀티라인 입력 + 우측 세로 스크롤바
/// </summary>
public class SkillView : MonoBehaviour
{
    // ── 다크 팔레트 (CharacterDetail 프리팹에서 추출) ──────────────────────────
    private static readonly Color RootBg = new Color(0.086f, 0.098f, 0.125f, 1f);
    private static readonly Color HeaderBg = new Color(0.125f, 0.141f, 0.173f, 1f);
    private static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    private static readonly Color PanelBg2 = new Color(0.153f, 0.169f, 0.204f, 1f);
    private static readonly Color InputBg = new Color(0.047f, 0.055f, 0.071f, 1f);
    private static readonly Color TagBg = new Color(0.235f, 0.255f, 0.298f, 1f);
    private static readonly Color AccentBlue = new Color(0.243f, 0.325f, 0.502f, 1f);
    private static readonly Color AccentBlueHi = new Color(0.306f, 0.404f, 0.608f, 1f);
    private static readonly Color Border = new Color(0.290f, 0.322f, 0.376f, 1f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);

    // 태그 카테고리별 색상. 일치하지 않으면 TagBg 사용.
    private static readonly Dictionary<string, Color> TagColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
    {
        { "unity", new Color(0.24f, 0.32f, 0.50f, 1f) },
        { "local", new Color(0.27f, 0.45f, 0.34f, 1f) },
        { "python", new Color(0.55f, 0.45f, 0.20f, 1f) },
    };

    [Serializable]
    public class SkillEntry
    {
        public string id;
        public string displayName;
        public List<string> tags = new List<string>();
        public string content = string.Empty;
    }

    [Header("Style")]
    [Tooltip("비워두면 TMP 기본 폰트를 사용한다. 다른 UI와 맞추려면 프로젝트 기본 폰트를 지정.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(520f, 640f);
    [Tooltip("둥근 모서리용 9-slice 스프라이트. 베이크 시 빌트인 UISprite가 지정된다.")]
    [SerializeField] private Sprite panelSprite;

    [Header("Data")]
    [SerializeField] private List<SkillEntry> skills = new List<SkillEntry>();
    [SerializeField]
    private List<string> languages = new List<string> { "한국어", "영어", "일본어" };

    // 외부 연동용 이벤트. 구독자가 없어도 단독으로 동작한다.
    public event Action<SkillEntry> SkillSelected;
    public event Action<SkillEntry> SaveRequested;
    public event Action RefreshRequested;
    public event Action<string> LanguageChanged;

    private bool built;
    private int selectedIndex;
    private Sprite roundedSprite;

    private TMP_Dropdown skillDropdown;
    private TMP_Dropdown languageDropdown;
    private RectTransform tagContent;
    private TMP_InputField contentInput;
    private readonly List<GameObject> tagPills = new List<GameObject>();

    private SkillEntry Current =>
        (selectedIndex >= 0 && selectedIndex < skills.Count) ? skills[selectedIndex] : null;

    private void Awake()
    {
        EnsureSampleData();
        if (HasBakedHierarchy())
        {
            // 프리팹에 UI가 이미 구워져 있으면 다시 만들지 않고 기존 자식에 연결만 한다.
            BindExisting();
        }
        else
        {
            Build();
        }
        Refresh();
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    public void Show()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetSkills(IEnumerable<SkillEntry> entries)
    {
        skills = entries != null ? new List<SkillEntry>(entries) : new List<SkillEntry>();
        selectedIndex = skills.Count > 0 ? 0 : -1;
        Refresh();
    }

    public void Refresh()
    {
        if (!built)
        {
            return;
        }

        RefreshSkillOptions();
        RefreshLanguageOptions();
        RefreshTags();
        RefreshContent();
    }

    public void AddTagToCurrent(string tag)
    {
        SkillEntry current = Current;
        if (current == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        current.tags.Add(tag.Trim());
        RefreshTags();
    }

    // ── 데이터 갱신 ───────────────────────────────────────────────────────────
    private void RefreshSkillOptions()
    {
        if (skillDropdown == null)
        {
            return;
        }

        List<string> names = new List<string>();
        for (int i = 0; i < skills.Count; i++)
        {
            names.Add(string.IsNullOrEmpty(skills[i].displayName) ? "Skill " + (i + 1) : skills[i].displayName);
        }

        skillDropdown.onValueChanged.RemoveListener(OnSkillValueChanged);
        skillDropdown.ClearOptions();
        skillDropdown.AddOptions(names);
        skillDropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, names.Count - 1)));
        skillDropdown.onValueChanged.AddListener(OnSkillValueChanged);
    }

    private void RefreshLanguageOptions()
    {
        if (languageDropdown == null || languageDropdown.options.Count == languages.Count)
        {
            return;
        }

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(languages);
    }

    private void RefreshTags()
    {
        if (tagContent == null)
        {
            return;
        }

        // 베이크된 프리팹에는 태그 pill 자식이 이미 들어있을 수 있으므로
        // 리스트뿐 아니라 실제 자식까지 모두 제거한 뒤 다시 만든다.
        for (int i = tagContent.childCount - 1; i >= 0; i--)
        {
            Destroy(tagContent.GetChild(i).gameObject);
        }
        tagPills.Clear();

        SkillEntry current = Current;
        if (current == null)
        {
            return;
        }

        for (int i = 0; i < current.tags.Count; i++)
        {
            tagPills.Add(CreateTagPill(tagContent, current.tags[i]));
        }
    }

    private void RefreshContent()
    {
        if (contentInput == null)
        {
            return;
        }

        SkillEntry current = Current;
        contentInput.SetTextWithoutNotify(current != null ? current.content : string.Empty);
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────────────
    private void OnSkillValueChanged(int index)
    {
        selectedIndex = index;
        RefreshTags();
        RefreshContent();
        SkillSelected?.Invoke(Current);
    }

    private void OnLanguageValueChanged(int index)
    {
        string code = "ko";
        if (index >= 0 && index < languages.Count)
        {
            string label = languages[index];
            if (label == "영어") code = "en";
            else if (label == "일본어") code = "ja";
        }

        LanguageChanged?.Invoke(code);
    }

    private void OnRefreshClicked()
    {
        RefreshRequested?.Invoke();
        RefreshTags();
        RefreshContent();
    }

    private void OnSaveClicked()
    {
        SkillEntry current = Current;
        if (current == null)
        {
            return;
        }

        current.content = contentInput != null ? contentInput.text : current.content;
        SaveRequested?.Invoke(current);
    }

    private void OnReloadClicked()
    {
        RefreshTags();
        RefreshContent();
    }

    private void RemoveTag(string tag, GameObject pill)
    {
        SkillEntry current = Current;
        if (current != null)
        {
            current.tags.Remove(tag);
        }

        tagPills.Remove(pill);
        if (pill != null)
        {
            Destroy(pill);
        }
    }

    // ── 베이크된 프리팹 연결 ───────────────────────────────────────────────────
    // 에디터에서 Build()로 한 번 구워진 프리팹을 런타임에 다시 만들지 않고,
    // 이미 존재하는 자식들에 참조와 이벤트만 연결한다.
    private bool HasBakedHierarchy()
    {
        return FindDeepChild(transform, "InputArea") != null;
    }

    private void BindExisting()
    {
        built = true;

        skillDropdown = FindComponent<TMP_Dropdown>("SkillDropdown");
        languageDropdown = FindComponent<TMP_Dropdown>("LanguageDropdown");
        contentInput = FindComponent<TMP_InputField>("InputArea");

        Transform tagArea = FindDeepChild(transform, "TagArea");
        if (tagArea != null)
        {
            tagContent = FindDeepChild(tagArea, "Content") as RectTransform;
        }

        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(OnLanguageValueChanged);
            languageDropdown.onValueChanged.AddListener(OnLanguageValueChanged);
        }

        BindButton("CloseButton", Hide);
        BindButton("RefreshButton", OnRefreshClicked);
        BindButton("SaveButton", OnSaveClicked);
        BindButton("ReloadButton", OnReloadClicked);
    }

    private void BindButton(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = FindComponent<Button>(name);
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

    private T FindComponent<T>(string name) where T : Component
    {
        Transform t = FindDeepChild(transform, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static Transform FindDeepChild(Transform parent, string name)
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

#if UNITY_EDITOR
    /// <summary>에디터 베이크 전용. 전체 UI 계층을 코드로 생성해 프리팹에 구워 넣을 때 호출한다.</summary>
    public void EditorBuild(Sprite roundedSpriteAsset = null)
    {
        if (roundedSpriteAsset != null)
        {
            panelSprite = roundedSpriteAsset;
        }
        EnsureSampleData();
        Build();
        Refresh();
    }
#endif

    // ── UI 구성 ──────────────────────────────────────────────────────────────
    private void Build()
    {
        if (built)
        {
            return;
        }
        built = true;

        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        rootRect.sizeDelta = panelSize;

        Image rootBg = GetOrAdd<Image>(gameObject);
        ApplyRounded(rootBg, RootBg);

        VerticalLayoutGroup rootLayout = GetOrAdd<VerticalLayoutGroup>(gameObject);
        rootLayout.padding = new RectOffset(16, 16, 16, 16);
        rootLayout.spacing = 12f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        BuildHeader(transform);
        BuildSelectorRow(transform);
        BuildTagArea(transform);
        BuildCrudRow(transform);
        BuildInputArea(transform);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = CreatePanel("Header", parent, HeaderBg);
        Layout(header, minH: 44f, prefH: 44f);
        HorizontalLayoutGroup layout = AddRow(header, 8f, padLeft: 12, padRight: 8);
        layout.childForceExpandHeight = true;

        TextMeshProUGUI title = CreateText("Title", header.transform, "스킬 관리", 20, TextWhite, TextAlignmentOptions.MidlineLeft);
        Layout(title.gameObject, flexW: 1f);

        Button close = CreateButton("CloseButton", header.transform, "×", HeaderBg, 24);
        Layout(close.gameObject, prefW: 32f, minW: 32f);
        close.onClick.AddListener(Hide);
    }

    private void BuildSelectorRow(Transform parent)
    {
        GameObject row = CreateUIObject("SelectorRow", parent);
        Layout(row, minH: 40f, prefH: 40f);
        HorizontalLayoutGroup layout = AddRow(row, 8f);
        layout.childForceExpandHeight = true;

        skillDropdown = CreateDropdown("SkillDropdown", row.transform);
        Layout(skillDropdown.gameObject, flexW: 1f);

        Button refresh = CreateButton("RefreshButton", row.transform, "⟳", PanelBg2, 18);
        Layout(refresh.gameObject, prefW: 40f, minW: 40f);
        refresh.onClick.AddListener(OnRefreshClicked);

        languageDropdown = CreateDropdown("LanguageDropdown", row.transform);
        Layout(languageDropdown.gameObject, prefW: 120f, minW: 120f);
        languageDropdown.onValueChanged.AddListener(OnLanguageValueChanged);
    }

    private void BuildTagArea(Transform parent)
    {
        GameObject area = CreatePanel("TagArea", parent, PanelBg);
        Layout(area, minH: 52f, prefH: 52f);

        // 가로 스크롤 (태그가 패널 폭을 넘으면 휠/드래그로 스크롤). ScrollRect 자체가 휠을 처리.
        ScrollRect scroll = area.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        GameObject viewport = CreateUIObject("Viewport", area.transform);
        SetStretch(viewport, new Vector4(10f, 8f, 10f, 8f));
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup tagLayout = content.AddComponent<HorizontalLayoutGroup>();
        tagLayout.spacing = 8f;
        tagLayout.childAlignment = TextAnchor.MiddleLeft;
        tagLayout.childControlWidth = true;
        tagLayout.childControlHeight = true;
        tagLayout.childForceExpandWidth = false;
        tagLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        tagContent = contentRect;
    }

    private void BuildCrudRow(Transform parent)
    {
        GameObject row = CreateUIObject("CrudRow", parent);
        Layout(row, minH: 40f, prefH: 40f);
        HorizontalLayoutGroup layout = AddRow(row, 8f);
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleRight;

        GameObject spacer = CreateUIObject("Spacer", row.transform);
        Layout(spacer, flexW: 1f);

        Button save = CreateButton("SaveButton", row.transform, "저장", AccentBlue, 16);
        Layout(save.gameObject, prefW: 100f, minW: 80f);
        save.onClick.AddListener(OnSaveClicked);

        Button reload = CreateButton("ReloadButton", row.transform, "되돌리기", PanelBg2, 16);
        Layout(reload.gameObject, prefW: 100f, minW: 80f);
        reload.onClick.AddListener(OnReloadClicked);
    }

    private void BuildInputArea(Transform parent)
    {
        GameObject area = CreatePanel("InputArea", parent, InputBg);
        Layout(area, minH: 140f, flexH: 1f);

        contentInput = area.AddComponent<TMP_InputField>();

        GameObject textArea = CreateUIObject("Text Area", area.transform);
        SetStretch(textArea, new Vector4(12f, 10f, 22f, 10f)); // 우측 22 : 스크롤바 공간
        textArea.AddComponent<RectMask2D>();
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();

        TextMeshProUGUI placeholder = CreateText("Placeholder", textArea.transform, "스킬 내용을 입력하세요...", 15, TextMuted, TextAlignmentOptions.TopLeft);
        SetStretch(placeholder.gameObject, Vector4.zero);
        placeholder.enableWordWrapping = true;

        TextMeshProUGUI text = CreateText("Text", textArea.transform, string.Empty, 15, TextWhite, TextAlignmentOptions.TopLeft);
        SetStretch(text.gameObject, Vector4.zero);
        text.enableWordWrapping = true;

        Scrollbar scrollbar = CreateVerticalScrollbar("Scrollbar", area.transform);

        contentInput.textViewport = textAreaRect;
        contentInput.textComponent = text;
        contentInput.placeholder = placeholder;
        contentInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        contentInput.contentType = TMP_InputField.ContentType.Standard;
        contentInput.richText = false;
        contentInput.verticalScrollbar = scrollbar;
        contentInput.targetGraphic = area.GetComponent<Image>();
    }

    // ── 팩토리 헬퍼 ───────────────────────────────────────────────────────────
    private GameObject CreateTagPill(Transform parent, string tag)
    {
        Color bg = TagColors.TryGetValue(tag, out Color mapped) ? mapped : TagBg;

        GameObject pill = CreatePanel("Tag", parent, bg);
        HorizontalLayoutGroup layout = pill.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 8, 4, 4);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = pill.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        Layout(pill, minH: 30f, prefH: 30f);

        CreateText("Label", pill.transform, tag, 14, TextWhite, TextAlignmentOptions.Center);

        Button remove = CreateButton("Remove", pill.transform, "×", new Color(0f, 0f, 0f, 0.2f), 14);
        Layout(remove.gameObject, prefW: 18f, minW: 18f, prefH: 18f, minH: 18f);
        remove.onClick.AddListener(() => RemoveTag(tag, pill));

        return pill;
    }

    private TMP_Dropdown CreateDropdown(string name, Transform parent)
    {
        GameObject root = CreatePanel(name, parent, PanelBg2);
        TMP_Dropdown dropdown = root.AddComponent<TMP_Dropdown>();

        TextMeshProUGUI label = CreateText("Label", root.transform, string.Empty, 15, TextWhite, TextAlignmentOptions.MidlineLeft);
        SetStretch(label.gameObject, new Vector4(10f, 2f, 26f, 2f));

        GameObject arrow = CreatePanel("Arrow", root.transform, TextMuted);
        RectTransform arrowRect = arrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-10f, 0f);
        arrowRect.sizeDelta = new Vector2(12f, 12f);

        // Template (리스트). ScrollRect 가 휠/드래그 스크롤을 처리.
        GameObject template = CreatePanel("Template", root.transform, PanelBg);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, 160f);

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
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 30f);
        Toggle itemToggle = item.AddComponent<Toggle>();

        GameObject itemBackground = CreateUIObject("Item Background", item.transform);
        SetStretch(itemBackground, Vector4.zero);
        Image itemBackgroundImg = itemBackground.AddComponent<Image>();
        itemBackgroundImg.color = new Color(0f, 0f, 0f, 0f);

        GameObject itemCheckmark = CreatePanel("Item Checkmark", item.transform, AccentBlueHi);
        RectTransform checkRect = itemCheckmark.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0f, 0.5f);
        checkRect.anchorMax = new Vector2(0f, 0.5f);
        checkRect.pivot = new Vector2(0f, 0.5f);
        checkRect.anchoredPosition = new Vector2(10f, 0f);
        checkRect.sizeDelta = new Vector2(10f, 10f);

        TextMeshProUGUI itemLabel = CreateText("Item Label", item.transform, "Option", 14, TextWhite, TextAlignmentOptions.MidlineLeft);
        SetStretch(itemLabel.gameObject, new Vector4(28f, 1f, 10f, 1f));

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

    private Scrollbar CreateVerticalScrollbar(string name, Transform parent)
    {
        GameObject root = CreatePanel(name, parent, new Color(0f, 0f, 0f, 0.25f));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(10f, -16f);
        rect.anchoredPosition = new Vector2(-6f, 0f);

        Scrollbar scrollbar = root.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = CreateUIObject("Sliding Area", root.transform);
        SetStretch(slidingArea, new Vector4(1f, 1f, 1f, 1f));

        GameObject handle = CreatePanel("Handle", slidingArea.transform, Border);
        SetStretch(handle, Vector4.zero);

        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        return scrollbar;
    }

    private Button CreateButton(string name, Transform parent, string label, Color background, float fontSize)
    {
        GameObject root = CreatePanel(name, parent, background);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText("Text", root.transform, label, fontSize, TextWhite, TextAlignmentOptions.Center);
        SetStretch(text.gameObject, Vector4.zero);
        return button;
    }

    private GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        Image image = go.AddComponent<Image>();
        ApplyRounded(image, color);
        return go;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        TMP_FontAsset resolved = ResolveFont();
        if (resolved != null)
        {
            text.font = resolved;
        }
        return text;
    }

    private GameObject CreateUIObject(string name, Transform parent)
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

    private void ApplyRounded(Image image, Color color)
    {
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
    }

    private Sprite GetRoundedSprite()
    {
        if (panelSprite != null)
        {
            return panelSprite;
        }
        if (roundedSprite == null)
        {
            roundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        }
        return roundedSprite;
    }

    private TMP_FontAsset ResolveFont()
    {
        if (font != null)
        {
            return font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    private static HorizontalLayoutGroup AddRow(GameObject go, float spacing, int padLeft = 0, int padRight = 0)
    {
        HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(padLeft, padRight, 0, 0);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static LayoutElement Layout(GameObject go, float minH = -1f, float prefH = -1f, float minW = -1f, float prefW = -1f, float flexW = -1f, float flexH = -1f)
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

    private static void SetStretch(GameObject go, Vector4 padding)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding.x, padding.y);   // left, bottom
        rect.offsetMax = new Vector2(-padding.z, -padding.w); // right, top
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
        {
            component = go.AddComponent<T>();
        }
        return component;
    }

    private void EnsureSampleData()
    {
        if (skills != null && skills.Count > 0)
        {
            return;
        }

        skills = new List<SkillEntry>
        {
            new SkillEntry
            {
                id = "screenshot",
                displayName = "스크린샷 분석",
                tags = new List<string> { "Unity", "Local" },
                content = "화면을 캡처해 분석하는 스킬입니다."
            },
            new SkillEntry
            {
                id = "web_search",
                displayName = "웹 검색",
                tags = new List<string> { "Python", "Local" },
                content = "웹에서 정보를 검색하는 스킬입니다."
            },
        };
        selectedIndex = 0;
    }
}
