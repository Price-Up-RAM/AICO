using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Policy UI (어두운 테마의 약관/정책 열람 패널)
/// <summary>
/// Policy UI. 좌측 문서 탭 + 우측 스크롤 본문 구조의 약관 열람 패널을 코드로 구성한다.
///
/// 문서(개인정보처리방침/이용약관/AI 고지/AI 운영정책)는 언어별 TextAsset(ko/jp/en)으로
/// 프리팹에 직렬화되어 번들된다. 표시 언어는 SettingManager.settings.ui_language를 따르고
/// (없으면 ko), 데모/테스트용으로 SetLanguageOverride를 제공한다.
///
/// 구성 요소
///  1. 헤더        : 타이틀 "Policy" + ×닫기
///  2. 탭 컬럼(좌) : 문서별 탭 버튼 (라벨 = 각 문서 첫 '# ' 행)
///  3. 본문(우)    : ScrollRect + 세로 스크롤바. 문단 단위 TMP 블록을 수동 적층
///                   (장문을 TMP 하나에 넣지 않아 정점 한계를 피한다)
/// </summary>
public class PolicyView : MonoBehaviour
{
    // ── 다크 팔레트 (SkillView와 동일 계열) ─────────────────────────────────
    private static readonly Color RootBg = new Color(0.086f, 0.098f, 0.125f, 1f);
    private static readonly Color HeaderBg = new Color(0.180f, 0.188f, 0.220f, 1f); // SkillView 베이크본 헤더 바 색
    private const float HeaderHeight = 54f;
    private static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    private static readonly Color PanelBg2 = new Color(0.153f, 0.169f, 0.204f, 1f);
    private static readonly Color AccentBlue = new Color(0.243f, 0.325f, 0.502f, 1f);
    private static readonly Color Border = new Color(0.290f, 0.322f, 0.376f, 1f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextBody = new Color(0.82f, 0.84f, 0.88f, 1f);
    private static readonly Color TextHeading = new Color(0.76f, 0.83f, 0.95f, 1f);

    [Serializable]
    public class PolicyDocument
    {
        public string key;      // privacy_policy | terms_of_service | ai_notice | acceptable_use_policy
        public TextAsset ko;
        public TextAsset jp;
        public TextAsset en;

        public TextAsset ForLanguage(string lang)
        {
            if (lang == "jp" && jp != null) return jp;
            if (lang == "en" && en != null) return en;
            return ko;
        }
    }

    [Header("Style")]
    [Tooltip("비워두면 TMP 기본 폰트를 사용한다. 베이크 후 SUIT-Bold가 적용된다.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(920f, 600f);
    [Tooltip("둥근 모서리용 9-slice 스프라이트. 베이크 시 빌트인 UISprite가 지정된다.")]
    [SerializeField] private Sprite panelSprite;

    [Header("Layout")]
    [SerializeField] private float tabColumnWidth = 210f;
    [SerializeField] private float tabHeight = 44f;
    [SerializeField] private float tabSpacing = 6f;
    [Tooltip("Title 아래와 Body 사이 간격(px)")]
    [SerializeField] private float titleBodyGap = 12f;
    [Tooltip("Body 아래 여백(px) — 스크롤 끝 여유")]
    [SerializeField] private float contentBottomPadding = 20f;

    [Header("Documents")]
    [SerializeField] private List<PolicyDocument> documents = new List<PolicyDocument>();

    // SettingManager 등록 순서(ui_language_idx 0:ko, 1:jp, 2:en)와 동일한 순환 순서
    private static readonly string[] LangCycle = { "ko", "jp", "en" };

    private bool built;
    private int selectedIndex;
    private string currentLang; // 패널 표시 언어. Show() 시 설정 ui_language로 리셋(실패 시 en).
    private Sprite roundedSprite;
    private TMP_FontAsset boundFont; // 베이크된 텍스트에서 캡처한 폰트(런타임 생성 블록용)

    private Button languageButton;
    private RectTransform tabColumn;
    private RectTransform contentArea;
    private RectTransform contentRect;   // 스크롤 Content
    private RectTransform viewportRect;
    private ScrollRect scrollRect;
    private readonly List<Button> tabButtons = new List<Button>();

    // 스타일 샘플(에디터에서 상시 표시·조정) + 런타임 매핑 노드(샘플 복제본)
    private TextMeshProUGUI titleSample;
    private TextMeshProUGUI bodySample;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;

    private void Awake()
    {
        if (HasBakedHierarchy())
        {
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
        // 열 때는 항상 설정 UI 언어로 시작. 설정을 읽지 못하면 en.
        currentLang = ResolveSettingsLanguage() ?? "en";
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (gameObject.activeSelf) Hide();
        else Show();
    }

    /// <summary>표시 언어 지정("ko"/"jp"/"en"). 다음 Show()에서 설정 언어로 리셋된다.</summary>
    public void SetLanguageOverride(string lang)
    {
        string norm = NormalizeLang(lang);
        if (norm != null)
        {
            currentLang = norm;
            Refresh();
        }
    }

    /// <summary>표시 언어를 SettingManager 등록 순서(ko→jp→en)로 순환한다.</summary>
    public void CycleLanguage()
    {
        string lang = ActiveLanguage();
        int index = Array.IndexOf(LangCycle, lang);
        currentLang = LangCycle[(index + 1) % LangCycle.Length];
        Refresh();
    }

    public void SelectDocument(int index)
    {
        if (documents.Count == 0)
        {
            return;
        }
        selectedIndex = Mathf.Clamp(index, 0, documents.Count - 1);
        Refresh();
    }

    // ── 언어 해석 ────────────────────────────────────────────────────────────
    private static string NormalizeLang(string lang)
    {
        if (string.IsNullOrEmpty(lang)) return null;
        string v = lang.Trim().ToLowerInvariant();
        if (v == "ko" || v == "en") return v;
        if (v == "jp" || v == "ja") return "jp";
        return null;
    }

    // 설정에서 UI 언어를 읽는다. SettingManager가 없는 씬(데모)에서도 죽지 않도록 전 단계 null-가드.
    private string ResolveSettingsLanguage()
    {
        try
        {
            SettingManager manager = SettingManager.Instance;
            if (manager != null && manager.settings != null)
            {
                return NormalizeLang(manager.settings.ui_language);
            }
        }
        catch (Exception)
        {
            // 설정 접근 실패 → null (호출부가 en 폴백)
        }
        return null;
    }

    private string ActiveLanguage()
    {
        if (currentLang == null)
        {
            currentLang = ResolveSettingsLanguage() ?? "en";
        }
        return currentLang;
    }

    // ── 표시 갱신 ────────────────────────────────────────────────────────────
    private void Refresh()
    {
        if (!built || documents.Count == 0)
        {
            return;
        }
        selectedIndex = Mathf.Clamp(selectedIndex, 0, documents.Count - 1);
        string lang = ActiveLanguage();
        EnsureRuntimeNodes();
        RefreshLanguageButton(lang);
        RefreshTabs(lang);
        RenderDocument(documents[selectedIndex], lang);
    }

    private void RefreshLanguageButton(string lang)
    {
        if (languageButton == null)
        {
            return;
        }
        TextMeshProUGUI label = languageButton.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = lang.ToUpperInvariant();
        }
    }

    private void RefreshTabs(string lang)
    {
        for (int i = 0; i < tabButtons.Count && i < documents.Count; i++)
        {
            Button tab = tabButtons[i];
            if (tab == null)
            {
                continue;
            }
            Image bg = tab.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = (i == selectedIndex) ? AccentBlue : PanelBg2;
            }
            TextMeshProUGUI label = tab.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.text = GetDocumentTitle(documents[i], lang);
            }
        }
    }

    private string GetDocumentTitle(PolicyDocument doc, string lang)
    {
        TextAsset asset = doc != null ? doc.ForLanguage(lang) : null;
        if (asset == null)
        {
            return doc != null ? doc.key : "?";
        }
        string text = asset.text ?? string.Empty;
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r').Trim();
            if (line.StartsWith("# "))
            {
                return line.Substring(2).Trim();
            }
        }
        return doc.key;
    }

    // ── 본문 렌더링 (샘플 스타일 매핑) ───────────────────────────────────────
    // Content 아래 구조는 Title/Body 2노드 고정. 스타일(폰트 크기·행간·좌우 여백)은
    // 샘플 노드(TitleSample/BodySample)에서 조정하며, 런타임 노드는 샘플의 복제본이다.

    // 런타임 진입 시 1회: 샘플을 숨기고 샘플 복제본(Title/Body)을 준비한다.
    private void EnsureRuntimeNodes()
    {
        if (titleSample != null && titleSample.gameObject.activeSelf)
        {
            titleSample.gameObject.SetActive(false);
        }
        if (bodySample != null && bodySample.gameObject.activeSelf)
        {
            bodySample.gameObject.SetActive(false);
        }
        if (titleText == null && titleSample != null)
        {
            titleText = InstantiateFromSample(titleSample, "Title");
        }
        if (bodyText == null && bodySample != null)
        {
            bodyText = InstantiateFromSample(bodySample, "Body");
        }
    }

    private TextMeshProUGUI InstantiateFromSample(TextMeshProUGUI sample, string name)
    {
        TextMeshProUGUI instance = Instantiate(sample, contentRect);
        instance.name = name;
        instance.gameObject.SetActive(true);
        return instance;
    }

    private void RenderDocument(PolicyDocument doc, string lang)
    {
        if (contentRect == null || titleText == null || bodyText == null)
        {
            return;
        }

        TextAsset asset = doc != null ? doc.ForLanguage(lang) : null;
        string text = asset != null ? (asset.text ?? string.Empty) : "(문서 없음: " + (doc != null ? doc.key : "?") + ")";
        text = text.Replace("\r\n", "\n");

        string title;
        string body = SplitTitleBody(text, out title);
        titleText.text = title;
        bodyText.text = body;

        // 상단 여백은 샘플의 배치 위치를 그대로 따른다 (샘플을 움직이면 문서도 움직임).
        float topPad = titleSample != null ? -titleSample.rectTransform.anchoredPosition.y : 14f;

        RectTransform titleRect = titleText.rectTransform;
        float titleHeight = titleText.GetPreferredValues(title, NodeTextWidth(titleRect), 0f).y + 2f;
        titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, -topPad);
        titleRect.sizeDelta = new Vector2(titleRect.sizeDelta.x, titleHeight);

        float bodyTop = topPad + titleHeight + titleBodyGap;
        RectTransform bodyRect = bodyText.rectTransform;
        float bodyHeight = bodyText.GetPreferredValues(body, NodeTextWidth(bodyRect), 0f).y + 4f;
        bodyRect.anchoredPosition = new Vector2(bodyRect.anchoredPosition.x, -bodyTop);
        bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, bodyHeight);

        // 문서 길이에 맞춰 스크롤 영역 갱신 + 맨 위로
        contentRect.sizeDelta = new Vector2(0f, bodyTop + bodyHeight + contentBottomPadding);
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    // 노드의 실제 텍스트 폭 = Content 폭 - 노드 좌우 오프셋(샘플에서 조정)
    private float NodeTextWidth(RectTransform node)
    {
        float areaWidth = (contentRect != null && contentRect.rect.width > 1f)
            ? contentRect.rect.width
            : panelSize.x - 12f * 2f - tabColumnWidth - 8f;
        return Mathf.Max(50f, areaWidth - node.offsetMin.x + node.offsetMax.x);
    }

    // 첫 '# ' 행 = 제목. 나머지는 본문으로, '## ' 절 제목만 리치 태그로 강조한다.
    // (본문 TMP는 richText=on — 문서 파일 안에 '<' 문자를 직접 쓰지 말 것)
    private static string SplitTitleBody(string text, out string title)
    {
        title = string.Empty;
        bool titleFound = false;
        StringBuilder body = new StringBuilder();

        foreach (string rawLine in text.Split('\n'))
        {
            string trimmed = rawLine.TrimEnd('\r').Trim();

            if (!titleFound && trimmed.StartsWith("# "))
            {
                title = trimmed.Substring(2).Trim();
                titleFound = true;
                continue;
            }

            if (trimmed.StartsWith("## "))
            {
                body.Append("<size=125%><color=#").Append(ColorUtility.ToHtmlStringRGB(TextHeading))
                    .Append(">").Append(trimmed.Substring(3).Trim()).Append("</color></size>\n");
            }
            else
            {
                body.Append(trimmed).Append('\n');
            }
        }

        return body.ToString().Trim('\n');
    }

    // ── 베이크된 프리팹 연결 ───────────────────────────────────────────────────
    private bool HasBakedHierarchy()
    {
        return FindDeepChild(transform, "ContentArea") != null;
    }

    private void BindExisting()
    {
        built = true;

        tabColumn = FindDeepChild(transform, "TabColumn") as RectTransform;
        contentArea = FindDeepChild(transform, "ContentArea") as RectTransform;
        viewportRect = FindDeepChild(transform, "Viewport") as RectTransform;
        contentRect = FindDeepChild(transform, "Content") as RectTransform;
        titleSample = FindComponent<TextMeshProUGUI>("TitleSample");
        bodySample = FindComponent<TextMeshProUGUI>("BodySample");
        titleText = FindComponent<TextMeshProUGUI>("Title");
        bodyText = FindComponent<TextMeshProUGUI>("Body");

        TextMeshProUGUI headerTitle = FindComponent<TextMeshProUGUI>("HeaderTitleText");
        if (headerTitle != null)
        {
            boundFont = headerTitle.font;
        }

        Button close = FindComponent<Button>("CloseButton");
        if (close != null)
        {
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(Hide);
        }

        languageButton = FindComponent<Button>("LanguageButton");
        if (languageButton != null)
        {
            languageButton.onClick.RemoveAllListeners();
            languageButton.onClick.AddListener(CycleLanguage);
        }

        if (contentArea != null)
        {
            scrollRect = contentArea.GetComponent<ScrollRect>();
        }

        tabButtons.Clear();
        for (int i = 0; i < documents.Count; i++)
        {
            Button tab = FindComponent<Button>("TabButton_" + documents[i].key);
            if (tab != null)
            {
                int index = i;
                tab.onClick.RemoveAllListeners();
                tab.onClick.AddListener(() => SelectDocument(index));
            }
            tabButtons.Add(tab);
        }
    }

    private T FindComponent<T>(string name) where T : Component
    {
        Transform found = FindDeepChild(transform, name);
        return found != null ? found.GetComponent<T>() : null;
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
    // 에디터 베이크 전용 (전체 UI 계층을 코드로 생성해 프리팹에 굽기)
    public void EditorBuild(Sprite roundedSpriteAsset, List<PolicyDocument> docs)
    {
        if (roundedSpriteAsset != null)
        {
            panelSprite = roundedSpriteAsset;
        }
        if (docs != null)
        {
            documents = docs;
        }
        // 베이크에서는 문서를 매핑하지 않는다 — 샘플(TitleSample/BodySample)이 보이는 상태로 굽는다.
        // 문서 매핑은 런타임 Refresh(EnsureRuntimeNodes)에서 수행된다.
        Build();
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

        BuildHeader();
        BuildTabColumn();
        BuildContentArea();
        BuildTabs();
    }

    private void BuildHeader()
    {
        // 가장자리까지 꽉 차는 타이틀 바 (SkillView 베이크본과 동일 구조). 헤더 드래그로 패널을 이동한다.
        GameObject header = CreatePanel("Header", transform, HeaderBg);
        header.GetComponent<Image>().raycastTarget = true;
        header.AddComponent<DragUIHandler>();
        RectTransform rt = header.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -HeaderHeight);
        rt.offsetMax = new Vector2(0f, 0f);

        TextMeshProUGUI title = CreateText("HeaderTitleText", header.transform, "Policy", 18f, TextWhite, TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(16f, 0f);
        titleRect.offsetMax = new Vector2(-52f, 0f);

        Button close = CreateButton("CloseButton", header.transform, "×", HeaderBg, 24f);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.sizeDelta = new Vector2(32f, 32f);
        closeRect.anchoredPosition = new Vector2(-10f, 0f);
        close.onClick.AddListener(Hide);

        // 언어 전환 버튼 (닫기 왼쪽). 클릭 시 ko→jp→en 순환.
        languageButton = CreateButton("LanguageButton", header.transform, ActiveLanguage().ToUpperInvariant(), PanelBg2, 14f);
        RectTransform langRect = languageButton.GetComponent<RectTransform>();
        langRect.anchorMin = new Vector2(1f, 0.5f);
        langRect.anchorMax = new Vector2(1f, 0.5f);
        langRect.pivot = new Vector2(1f, 0.5f);
        langRect.sizeDelta = new Vector2(44f, 32f);
        langRect.anchoredPosition = new Vector2(-50f, 0f);
        languageButton.onClick.AddListener(CycleLanguage);
    }

    private void BuildTabColumn()
    {
        GameObject column = CreateUIObject("TabColumn", transform);
        tabColumn = column.GetComponent<RectTransform>();
        tabColumn.anchorMin = new Vector2(0f, 0f);
        tabColumn.anchorMax = new Vector2(0f, 1f);
        tabColumn.pivot = new Vector2(0f, 0.5f);
        tabColumn.offsetMin = new Vector2(12f, 12f);
        tabColumn.offsetMax = new Vector2(12f + tabColumnWidth, -(HeaderHeight + 10f));
    }

    private void BuildContentArea()
    {
        GameObject area = CreatePanel("ContentArea", transform, PanelBg);
        contentArea = area.GetComponent<RectTransform>();
        contentArea.anchorMin = new Vector2(0f, 0f);
        contentArea.anchorMax = new Vector2(1f, 1f);
        contentArea.offsetMin = new Vector2(12f + tabColumnWidth + 8f, 12f);
        contentArea.offsetMax = new Vector2(-12f, -(HeaderHeight + 10f));

        GameObject viewport = CreateUIObject("Viewport", area.transform);
        SetStretch(viewport, new Vector4(0f, 0f, 0f, 0f));
        viewportRect = viewport.GetComponent<RectTransform>();
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUIObject("Content", viewport.transform);
        contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(0f, 0f);
        contentRect.offsetMax = new Vector2(0f, 0f);
        contentRect.sizeDelta = new Vector2(0f, 100f);

        scrollRect = area.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        Scrollbar scrollbar = CreateVerticalScrollbar("PolicyScrollbar", area.transform);
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        CreateSamples();
    }

    // 스타일 샘플. 에디터에서 이 두 노드로 폰트 크기·행간·여백을 확인·조정한다.
    // 런타임에는 숨겨지고, 문서는 이 샘플의 복제본(Title/Body)으로 표시된다.
    private void CreateSamples()
    {
        titleSample = CreateText("TitleSample", contentRect, "약관 제목 샘플", 22f, TextWhite, TextAlignmentOptions.TopLeft);
        titleSample.richText = false;
        RectTransform titleRect = titleSample.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(16f, 0f);   // 좌측 여백
        titleRect.offsetMax = new Vector2(-28f, 0f);  // 우측 여백(스크롤바 자리)
        titleRect.anchoredPosition = new Vector2(0f, -14f); // 상단 여백
        titleRect.sizeDelta = new Vector2(titleRect.sizeDelta.x, 32f);

        bodySample = CreateText("BodySample", contentRect, SampleBodyText, 14f, TextBody, TextAlignmentOptions.TopLeft);
        bodySample.richText = true; // '## ' 절 제목 강조 태그 렌더링용
        bodySample.lineSpacing = 4f;
        RectTransform bodyRect = bodySample.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.offsetMin = new Vector2(16f, 0f);
        bodyRect.offsetMax = new Vector2(-28f, 0f);
        bodyRect.anchoredPosition = new Vector2(0f, -(14f + 32f + titleBodyGap));
        bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, 220f);

        contentRect.sizeDelta = new Vector2(0f, 320f);
    }

    private const string SampleBodyText =
        "<size=125%><color=#C2D3F2>제1조 (절 제목 샘플)</color></size>\n" +
        "본문 샘플 문단입니다. 이 노드의 폰트 크기, 행간(Line Spacing), 좌우 오프셋을 조정하면 런타임 문서도 동일한 스타일로 표시됩니다.\n" +
        "\n" +
        "- 불릿 샘플 항목 1\n" +
        "- 불릿 샘플 항목 2\n" +
        "\n" +
        "빈 줄은 문단 간격으로 그대로 반영됩니다.";

    private void BuildTabs()
    {
        tabButtons.Clear();
        if (tabColumn == null)
        {
            return;
        }

        string lang = ActiveLanguage();
        for (int i = 0; i < documents.Count; i++)
        {
            Button tab = CreateButton("TabButton_" + documents[i].key, tabColumn, GetDocumentTitle(documents[i], lang), PanelBg2, 15f);
            RectTransform rt = tab.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(0f, -i * (tabHeight + tabSpacing));
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, tabHeight);

            TextMeshProUGUI label = tab.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.offsetMin = new Vector2(12f, 0f);
                label.rectTransform.offsetMax = new Vector2(-4f, 0f);
            }

            int index = i;
            tab.onClick.AddListener(() => SelectDocument(index));
            tabButtons.Add(tab);
        }
    }

    // ── 위젯 생성 헬퍼 (SkillView와 동일 패턴) ───────────────────────────────
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
        // ScrollRect의 verticalNormalizedPosition은 1=맨 위 — BottomToTop이어야 핸들 위치가 일치한다
        // (TopToBottom은 TMP_InputField 연동용 방향이라 ScrollRect에 쓰면 핸들이 반전됨).
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
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
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
        if (boundFont != null)
        {
            return boundFont;
        }
        return TMP_Settings.defaultFontAsset;
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
}
