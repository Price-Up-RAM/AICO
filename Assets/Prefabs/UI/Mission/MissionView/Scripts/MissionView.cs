using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미션(업적) 메인 패널. 좌측 카테고리 탭 + 우측 카드 리스트.
/// 이중 모드: 베이크된 계층이 있으면 BindExisting, 없으면 Build. (SkillView 방법론)
/// 설계: Assets/Prefabs/UI/Mission/MISSION_Design.md
/// </summary>
public class MissionView : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(760f, 520f);
    [Tooltip("상단 헤더 줄 높이(px). 아래 카드 영역은 나머지를 자동으로 채움.")]
    [SerializeField] private float headerHeight = 40f;
    [SerializeField] private Sprite panelSprite;

    [Header("Reward Icons (비우면 텍스트 폴백)")]
    [SerializeField] private Sprite goldIcon;
    [SerializeField] private Sprite item1Icon;
    [SerializeField] private Sprite item2Icon;
    [SerializeField] private Sprite item3Icon;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stampClip;

    [Header("Test")]
    [Tooltip("켜면 카드를 꾸욱 눌러 진행도를 +1 (테스트용). 출시 전 끄거나 MissionTestPoke 제거.")]
    [SerializeField] private bool enableTestPoke = true;

    private bool built;
    private MissionTab selectedCategory = MissionTab.Onboarding;
    private bool hideCompleted; // 켜면 달성(완료) 미션 숨김

    [Header("Bound (자동 채움, 인스펙터 등록 가능)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button hideDoneButton;
    [SerializeField] private Transform tabColumn;
    [SerializeField] private MissionTabButton tabTemplate;
    [SerializeField] private RectTransform cardContent;
    [SerializeField] private MissionCardRow cardTemplate;

    private readonly List<MissionTabButton> tabButtons = new List<MissionTabButton>();
    private readonly List<MissionCardRow> visibleRows = new List<MissionCardRow>();
    private readonly List<string> orderedIds = new List<string>(); // 현재 탭 표시 순서(진입 시 고정)
    private MissionCardRow openDrawerRow;

    private string Lang =>
        (Application.isPlaying && MissionList.Instance != null) ? MissionList.Instance.Language : "ko";

    private void Awake()
    {
        ApplyStyleOverrides();
        EnsureBuilt();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && MissionList.Instance != null)
        {
            MissionList.Instance.MissionsChanged += RefreshAll;
        }

        RebuildOrder(); // 진입(표시) 시점 → 정렬 갱신
        RefreshAll();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && MissionList.Instance != null)
        {
            MissionList.Instance.MissionsChanged -= RefreshAll;
        }
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    public void Show()
    {
        gameObject.SetActive(true);
        RebuildOrder(); // 창 표시 시점 → 정렬 갱신
        RefreshAll();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SelectCategory(MissionTab category)
    {
        selectedCategory = category;
        openDrawerRow = null;
        RebuildOrder(); // 탭 이동 시점 → 정렬 갱신
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (!built)
        {
            return;
        }

        RefreshTabs();
        RefreshCards();
    }

#if UNITY_EDITOR
    // 에디터 베이크 전용: 전체 UI를 코드로 생성해 프리팹에 굽는다.
    public void EditorBuild(Sprite roundedSprite = null, TMP_FontAsset fontAsset = null)
    {
        if (roundedSprite != null) panelSprite = roundedSprite;
        if (fontAsset != null) font = fontAsset;
        ApplyStyleOverrides();
        BuildHierarchy();

        // 탭/카드 클론은 굽지 않는다(런타임에 동적 생성). 템플릿만 미리보기로 세팅(비활성 유지).
        RefreshTabs();

        if (tabTemplate != null)
        {
            tabTemplate.Setup(MissionTab.Onboarding, CategoryLabel(MissionTab.Onboarding), "0/0", true, null);
        }

        // 비활성 카드 템플릿에 미리보기 데이터 1건(에디터에서 모양 확인용, 비활성 유지).
        // 에디터(비플레이)에선 MissionList 인스턴스가 없으므로 샘플 MissionInfo를 즉석 생성.
        if (cardTemplate != null)
        {
            MissionInfo sample = new MissionInfo
            {
                id = "OB0001",
                tab = MissionTab.Onboarding,
                type = MissionType.OneTime,
                title = new LocalizedText("아이코를 처음 만나기", "Meet Aiko for the first time", "アイコと初めて出会う"),
            };
            sample.tiers.Add(new MissionTier(1, new MissionReward(50)));
            cardTemplate.Setup(sample, "ko", null, null);
        }
    }
#endif

    private void ApplyStyleOverrides()
    {
        MissionUi.RoundedSpriteOverride = panelSprite;
        MissionUi.FontOverride = font;
        MissionUi.GoldIcon = goldIcon;
        MissionUi.Item1Icon = item1Icon;
        MissionUi.Item2Icon = item2Icon;
        MissionUi.Item3Icon = item3Icon;
    }

    private void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        if (HasBakedHierarchy())
        {
            BindExisting();
        }
        else
        {
            BuildHierarchy();
        }
    }

    private bool HasBakedHierarchy()
    {
        return MissionUi.FindDeepChild(transform, "CardScroll") != null;
    }

    // ── 데이터 갱신 ───────────────────────────────────────────────────────────
    // 탭은 미션 데이터(info.tab)에서 동적 수집해 템플릿을 클론한다.
    private void RefreshTabs()
    {
        if (tabColumn == null || tabTemplate == null)
        {
            return;
        }

        for (int i = tabButtons.Count - 1; i >= 0; i--)
        {
            if (tabButtons[i] != null)
            {
                Destroy(tabButtons[i].gameObject);
            }
        }

        tabButtons.Clear();

        if (!Application.isPlaying || MissionList.Instance == null)
        {
            return; // 에디터: 템플릿만(런타임에 탭 생성)
        }

        List<MissionTab> tabs = GetTabsInOrder();
        if (tabs.Count > 0 && !tabs.Contains(selectedCategory))
        {
            selectedCategory = tabs[0];
        }

        for (int i = 0; i < tabs.Count; i++)
        {
            MissionTab tab = tabs[i];
            MissionTabButton button = Instantiate(tabTemplate, tabColumn);
            button.gameObject.name = "Tab_" + tab;
            button.gameObject.SetActive(true);
            MissionList.Instance.GetTabCounts(tab, out int done, out int total);
            button.Setup(tab, CategoryLabel(tab), done + "/" + total, tab == selectedCategory, SelectCategory);
            tabButtons.Add(button);
        }
    }

    // 미션 목록에서 등장 순서대로 중복 없는 탭 목록을 만든다.
    private List<MissionTab> GetTabsInOrder()
    {
        List<MissionTab> tabs = new List<MissionTab>();
        IReadOnlyList<MissionInfo> all = MissionList.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (!tabs.Contains(all[i].tab))
            {
                tabs.Add(all[i].tab);
            }
        }

        return tabs;
    }

    private void RefreshCards()
    {
        if (cardContent == null || cardTemplate == null)
        {
            return;
        }

        for (int i = visibleRows.Count - 1; i >= 0; i--)
        {
            if (visibleRows[i] != null)
            {
                Destroy(visibleRows[i].gameObject);
            }
        }

        visibleRows.Clear();
        openDrawerRow = null;

        if (!Application.isPlaying || MissionList.Instance == null)
        {
            return; // 에디터(비플레이)에선 카드 클론을 만들지 않음(템플릿 미리보기만)
        }

        // 표시 순서는 RebuildOrder()가 진입 시점(Show/탭 이동 등)에만 계산한다.
        // 여기서는 그 순서대로 렌더만 → 보는 중 달성해도 자리에서 도장만 찍히고 즉시 안 내려감.
        for (int i = 0; i < orderedIds.Count; i++)
        {
            MissionInfo info = MissionList.Instance.GetById(orderedIds[i]);
            if (info == null || info.tab != selectedCategory)
            {
                continue;
            }

            MissionCardRow row = Instantiate(cardTemplate, cardContent);
            row.gameObject.name = "Card_" + info.id;
            row.gameObject.SetActive(true);
            row.Setup(info, Lang, OnClaimClicked, OnDrawerOpened);

            // 테스트 훅: 런타임에만 부착(프리팹 비포함, 분리 용이)
            if (enableTestPoke && row.GetComponent<MissionTestPoke>() == null)
            {
                row.gameObject.AddComponent<MissionTestPoke>();
            }

            visibleRows.Add(row);
        }
    }

    // 표시 순서 계산: 미달성 먼저, 달성(완료)은 아래로. hideCompleted면 완료 제외.
    // 진입 시점(Show/탭 이동/필터 토글/리셋)에만 호출 → 달성 즉시 재정렬하지 않음.
    private void RebuildOrder()
    {
        orderedIds.Clear();
        if (!Application.isPlaying || MissionList.Instance == null)
        {
            return;
        }

        List<MissionInfo> defs = MissionList.Instance.GetByTab(selectedCategory);
        for (int i = 0; i < defs.Count; i++)
        {
            if (!defs[i].AllDone)
            {
                orderedIds.Add(defs[i].id);
            }
        }

        if (!hideCompleted)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].AllDone)
                {
                    orderedIds.Add(defs[i].id);
                }
            }
        }
    }

    private void OnClaimClicked(string id)
    {
        if (!Application.isPlaying || MissionList.Instance == null)
        {
            return;
        }

        bool ok = MissionList.Instance.ClaimReward(id);
        if (!ok)
        {
            return;
        }

        PlayStampSound();

        bool nowAllDone = MissionList.Instance.IsCompleted(id);
        // MissionsChanged 이벤트로 이미 RefreshAll이 돌아 카드가 재생성됨 → 새 카드에서 도장 연출.
        MissionCardRow row = FindRow(id);
        if (row != null)
        {
            row.PlayClaimEffect(nowAllDone);
        }
    }

    private void OnToggleHideDone()
    {
        hideCompleted = !hideCompleted;
        UpdateHideDoneVisual();
        RebuildOrder(); // 필터 토글 시점 → 정렬/필터 갱신
        RefreshAll();
    }

    private void UpdateHideDoneVisual()
    {
        if (hideDoneButton == null)
        {
            return;
        }

        Image img = hideDoneButton.GetComponent<Image>();
        if (img != null)
        {
            MissionUi.ApplyRounded(img, hideCompleted ? MissionUi.Accent : MissionUi.PanelBg2);
        }

        TMP_Text label = hideDoneButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = (hideCompleted ? "[v] " : "[ ] ") + "달성 제외";
        }
    }

    private void OnResetClicked()
    {
        if (Application.isPlaying && MissionList.Instance != null)
        {
            MissionList.Instance.ResetAllProgress(); // MissionsChanged → RefreshAll
            RebuildOrder(); // 리셋도 진입처럼 정렬 갱신
            RefreshAll();
        }
    }

    private void OnDrawerOpened(MissionCardRow row)
    {
        if (openDrawerRow != null && openDrawerRow != row)
        {
            openDrawerRow.CloseDrawer();
        }

        openDrawerRow = row;
    }

    private MissionCardRow FindRow(string id)
    {
        for (int i = 0; i < visibleRows.Count; i++)
        {
            if (visibleRows[i] != null && visibleRows[i].MissionId == id)
            {
                return visibleRows[i];
            }
        }

        return null;
    }

    private void PlayStampSound()
    {
        if (audioSource != null && stampClip != null)
        {
            audioSource.PlayOneShot(stampClip);
        }
    }

    private static string CategoryLabel(MissionTab category)
    {
        switch (category)
        {
            case MissionTab.Onboarding: return "첫걸음";
            case MissionTab.Conversation: return "대화";
            case MissionTab.Affection: return "교감";
            case MissionTab.Productivity: return "생활";
            case MissionTab.Challenge: return "도전";
            default: return category.ToString();
        }
    }

    // ── 베이크된 프리팹 연결 ───────────────────────────────────────────────────
    private void BindExisting()
    {
        built = true;

        // 인스펙터 등록 우선, 비면 탐색(fallback)
        titleText = titleText != null ? titleText : MissionUi.FindComponent<TMP_Text>(transform, "TitleText");

        // 헤더 높이는 인스펙터 값(headerHeight)을 런타임에도 반영(베이크 후 조절 가능)
        Transform headerT = MissionUi.FindDeepChild(transform, "Header");
        if (headerT != null)
        {
            LayoutElement headerLe = headerT.GetComponent<LayoutElement>();
            if (headerLe != null)
            {
                headerLe.minHeight = headerHeight;
                headerLe.preferredHeight = headerHeight;
            }
        }

        tabColumn = tabColumn != null ? tabColumn : MissionUi.FindDeepChild(transform, "TabColumn");
        cardContent = cardContent != null ? cardContent : MissionUi.FindComponent<RectTransform>(transform, "CardContent");

        if (cardTemplate == null)
        {
            Transform template = MissionUi.FindDeepChild(transform, "CardTemplate");
            if (template != null)
            {
                cardTemplate = template.GetComponent<MissionCardRow>();
            }
        }

        if (cardTemplate != null)
        {
            cardTemplate.BindExisting();
            cardTemplate.gameObject.SetActive(false);
        }

        tabButtons.Clear();
        if (tabTemplate == null)
        {
            Transform tabT = MissionUi.FindDeepChild(transform, "TabTemplate");
            if (tabT != null)
            {
                tabTemplate = tabT.GetComponent<MissionTabButton>();
            }
        }

        if (tabTemplate != null)
        {
            tabTemplate.BindExisting();
            tabTemplate.gameObject.SetActive(false);
        }

        BindButton("CloseButton", Hide);
        BindButton("ResetButton", OnResetClicked);
        hideDoneButton = hideDoneButton != null ? hideDoneButton : MissionUi.FindComponent<Button>(transform, "HideDoneButton");
        BindButton("HideDoneButton", OnToggleHideDone);
        UpdateHideDoneVisual();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void BindButton(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = MissionUi.FindComponent<Button>(transform, name);
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

    // ── UI 구성 ──────────────────────────────────────────────────────────────
    private void BuildHierarchy()
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

        Image rootBg = MissionUi.GetOrAdd<Image>(gameObject);
        MissionUi.ApplyRounded(rootBg, MissionUi.RootBg);

        if (audioSource == null)
        {
            audioSource = MissionUi.GetOrAdd<AudioSource>(gameObject);
            audioSource.playOnAwake = false;
        }

        VerticalLayoutGroup rootLayout = MissionUi.GetOrAdd<VerticalLayoutGroup>(gameObject);
        rootLayout.padding = new RectOffset(14, 14, 14, 14);
        rootLayout.spacing = 10f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        BuildHeader(transform);
        BuildBody(transform);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = MissionUi.CreateUIObject("Header", parent);
        MissionUi.Layout(header, minH: headerHeight, prefH: headerHeight);
        MissionUi.AddRow(header, 8f).childForceExpandHeight = true;

        titleText = MissionUi.CreateText("TitleText", header.transform, "미션", 20f, MissionUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MissionUi.Layout(titleText.gameObject, flexW: 1f);

        // 달성(완료) 미션 숨김 체크박스
        hideDoneButton = MissionUi.CreateButton("HideDoneButton", header.transform, "[ ] 달성 제외", MissionUi.PanelBg2, 13f);
        MissionUi.Layout(hideDoneButton.gameObject, prefW: 104f, minW: 104f);
        hideDoneButton.onClick.AddListener(OnToggleHideDone);
        UpdateHideDoneVisual();

        // 테스트용 초기화 버튼 (미션 진행 상태만 리셋, 인벤토리는 유지)
        Button reset = MissionUi.CreateButton("ResetButton", header.transform, "초기화", MissionUi.PanelBg2, 14f);
        MissionUi.Layout(reset.gameObject, prefW: 64f, minW: 64f);
        reset.onClick.AddListener(OnResetClicked);

        Button close = MissionUi.CreateButton("CloseButton", header.transform, "×", MissionUi.HeaderBg, 24f);
        MissionUi.Layout(close.gameObject, prefW: 40f, minW: 40f);
        close.onClick.AddListener(Hide);
    }

    private void BuildBody(Transform parent)
    {
        GameObject body = MissionUi.CreateUIObject("Body", parent);
        MissionUi.Layout(body, flexH: 1f);
        HorizontalLayoutGroup bodyLayout = MissionUi.AddRow(body, 10f);
        bodyLayout.childForceExpandHeight = true;

        BuildTabColumn(body.transform);
        BuildCardScroll(body.transform);
    }

    private void BuildTabColumn(Transform parent)
    {
        GameObject column = MissionUi.CreateUIObject("TabColumn", parent);
        // 탭:컨텐츠 = 2:8 비율 (고정폭 대신 flexibleWidth로 분배)
        MissionUi.Layout(column, minW: 0f, prefW: 0f, flexW: 2f);
        VerticalLayoutGroup layout = MissionUi.AddColumn(column, 6f);
        layout.childForceExpandHeight = false;
        tabColumn = column.transform;

        tabButtons.Clear();
        tabTemplate = BuildTabTemplate(column.transform); // 비활성 템플릿 1개만 굽고, 런타임에 클론
    }

    private MissionTabButton BuildTabTemplate(Transform parent)
    {
        GameObject tabObject = MissionUi.CreatePanel("TabTemplate", parent, MissionUi.TabBg);
        MissionUi.Layout(tabObject, minH: 50f, prefH: 50f);

        Button button = tabObject.AddComponent<Button>();
        button.targetGraphic = tabObject.GetComponent<Image>();

        TextMeshProUGUI label = MissionUi.CreateText("Label", tabObject.transform, "탭", 16f,
            MissionUi.TextMuted, TextAlignmentOptions.MidlineLeft);
        MissionUi.SetStretch(label.gameObject, new Vector4(12f, 0f, 46f, 0f));   // 우측은 카운트 공간 확보

        TextMeshProUGUI count = MissionUi.CreateText("Count", tabObject.transform, "0/0", 12f, MissionUi.TextMuted,
            TextAlignmentOptions.MidlineRight);
        MissionUi.SetStretch(count.gameObject, new Vector4(12f, 0f, 10f, 0f));   // 탭 우측에 카운트

        MissionTabButton tab = tabObject.AddComponent<MissionTabButton>();
        tab.BindExisting();
        tabObject.SetActive(false);
        return tab;
    }

    private void BuildCardScroll(Transform parent)
    {
        GameObject scrollObject = MissionUi.CreatePanel("CardScroll", parent, MissionUi.PanelBg);
        // 탭:컨텐츠 = 2:8 비율
        MissionUi.Layout(scrollObject, minW: 0f, prefW: 0f, flexW: 8f);

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        GameObject viewport = MissionUi.CreateUIObject("Viewport", scrollObject.transform);
        MissionUi.SetStretch(viewport, new Vector4(8f, 8f, 22f, 8f)); // 우측 22px는 스크롤바 전용 레인(겹침 방지)
        viewport.AddComponent<RectMask2D>();

        GameObject content = MissionUi.CreateUIObject("CardContent", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = MissionUi.AddColumn(content, 8f);
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        cardContent = contentRect;

        // 세로 스크롤바 — 우측 전용 레인(뷰포트와 겹치지 않음). 항상 표시(공간 확보).
        GameObject sb = MissionUi.CreatePanel("Scrollbar", scrollObject.transform, new Color(0f, 0f, 0f, 0.30f));
        RectTransform sbRect = sb.GetComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(1f, 0f);
        sbRect.anchorMax = new Vector2(1f, 1f);
        sbRect.pivot = new Vector2(1f, 0.5f);
        sbRect.sizeDelta = new Vector2(12f, -16f);   // 폭 12, 상하 8씩 여백
        sbRect.anchoredPosition = new Vector2(-5f, 0f);

        Scrollbar scrollbar = sb.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = MissionUi.CreateUIObject("Sliding Area", sb.transform);
        MissionUi.SetStretch(slidingArea, new Vector4(1f, 1f, 1f, 1f));

        GameObject handle = MissionUi.CreatePanel("Handle", slidingArea.transform, MissionUi.GaugeBorder);
        MissionUi.SetStretch(handle, Vector4.zero);

        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent; // 뷰포트 위로 확장하지 않음(레인 고정)

        cardTemplate = BuildCardTemplate(content.transform);
    }

    private MissionCardRow BuildCardTemplate(Transform parent)
    {
        GameObject card = MissionUi.CreatePanel("CardTemplate", parent, MissionUi.PanelBg2);
        MissionUi.Layout(card, minH: 88f, prefH: 88f);
        HorizontalLayoutGroup cardLayout = MissionUi.AddRow(card, 10f, new RectOffset(12, 12, 10, 10));
        cardLayout.childForceExpandHeight = true;
        cardLayout.childAlignment = TextAnchor.MiddleLeft;

        // 좌측 정보
        GameObject info = MissionUi.CreateUIObject("Info", card.transform);
        MissionUi.Layout(info, flexW: 1f);
        MissionUi.AddColumn(info, 6f);

        // 제목 줄: 설명(좌) + 단계 라벨(우)
        GameObject titleRow = MissionUi.CreateUIObject("TitleRow", info.transform);
        MissionUi.Layout(titleRow, minH: 24f, prefH: 24f);
        MissionUi.AddRow(titleRow, 8f).childForceExpandHeight = true;

        TextMeshProUGUI desc = MissionUi.CreateText("Description", titleRow.transform, "미션 설명", 15f, MissionUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MissionUi.Layout(desc.gameObject, flexW: 1f);

        TextMeshProUGUI tierLabel = MissionUi.CreateText("TierLabel", titleRow.transform, "", 12f, MissionUi.TextMuted,
            TextAlignmentOptions.MidlineRight);
        MissionUi.Layout(tierLabel.gameObject, prefW: 52f, minW: 40f);

        GameObject gaugeRow = MissionUi.CreateUIObject("GaugeRow", info.transform);
        MissionUi.Layout(gaugeRow, minH: 20f, prefH: 20f);
        MissionUi.AddRow(gaugeRow, 8f).childForceExpandHeight = true;

        // 게이지(실린더 느낌): 테두리(Frame) → 배경 트랙(Bg) → 너비로 채우는 바(Fill)
        GameObject gaugeFrame = MissionUi.CreatePanel("GaugeFrame", gaugeRow.transform, MissionUi.GaugeBorder);
        MissionUi.Layout(gaugeFrame, flexW: 1f, minH: 18f, prefH: 18f);

        GameObject gaugeBg = MissionUi.CreatePanel("GaugeBg", gaugeFrame.transform, MissionUi.GaugeBg);
        MissionUi.SetStretch(gaugeBg, new Vector4(2f, 2f, 2f, 2f));

        // Fill은 anchor 너비로 채운다(왼쪽 고정, anchorMax.x = 진행률). Filled 타입은 sprite 없으면 무효라 사용하지 않음.
        GameObject gaugeFillObject = MissionUi.CreatePanel("GaugeFill", gaugeBg.transform, MissionUi.GaugeFill);
        RectTransform gaugeFillRect = gaugeFillObject.GetComponent<RectTransform>();
        gaugeFillRect.anchorMin = new Vector2(0f, 0f);
        gaugeFillRect.anchorMax = new Vector2(0f, 1f);   // 시작 0%
        gaugeFillRect.pivot = new Vector2(0f, 0.5f);
        gaugeFillRect.offsetMin = Vector2.zero;
        gaugeFillRect.offsetMax = Vector2.zero;

        TextMeshProUGUI progressLabel = MissionUi.CreateText("ProgressLabel", gaugeRow.transform, "0 / 0", 13f,
            MissionUi.TextMuted, TextAlignmentOptions.MidlineRight);
        MissionUi.Layout(progressLabel.gameObject, prefW: 84f, minW: 64f);

        // 우측 보상 영역
        GameObject rewardArea = MissionUi.CreateUIObject("RewardArea", card.transform);
        MissionUi.Layout(rewardArea, prefW: 118f, minW: 118f);

        // 대표 보상 셀(아이콘 + 우하단 수량). 다중 보상이면 3초마다 페이드로 순환.
        GameObject rewardCell = MissionUi.CreateRewardCell("RewardCell", rewardArea.transform, out _, out _);
        RectTransform cellRect = rewardCell.GetComponent<RectTransform>();
        cellRect.anchorMin = new Vector2(1f, 0.5f);
        cellRect.anchorMax = new Vector2(1f, 0.5f);
        cellRect.pivot = new Vector2(1f, 0.5f);
        cellRect.anchoredPosition = new Vector2(-8f, 0f);
        cellRect.sizeDelta = new Vector2(54f, 54f);
        // 보상 셀 자체가 버튼 → 클릭 시 상세 서랍(카드 전체 클릭=수령과 분리). 클릭이 카드로 버블되지 않음.
        Button cellButton = rewardCell.AddComponent<Button>();
        cellButton.targetGraphic = rewardCell.GetComponent<Image>();

        // 보상 상세 서랍: 우측 정렬, 보상 개수에 따라 가로 가변(ContentSizeFitter), 높이는 보상 셀과 동일(54).
        GameObject drawer = MissionUi.CreatePanel("Drawer", rewardArea.transform, MissionUi.PanelBg);
        RectTransform drawerRect = drawer.GetComponent<RectTransform>();
        drawerRect.anchorMin = new Vector2(1f, 0.5f);
        drawerRect.anchorMax = new Vector2(1f, 0.5f);
        drawerRect.pivot = new Vector2(1f, 0.5f);   // 우측 고정 → 왼쪽으로 펼쳐짐(보상 셀 왼쪽에서)
        drawerRect.anchoredPosition = new Vector2(-68f, 0f); // 대표 보상 셀(우측 54px) 바로 왼쪽 → 원본과 안 겹침
        drawerRect.sizeDelta = new Vector2(0f, 54f); // 높이 54(셀과 동일), 너비는 ContentSizeFitter가 결정
        drawer.AddComponent<CanvasGroup>();

        // 셀은 Drawer 직접 자식. 가로 레이아웃 + 좌우 패딩 + ContentSizeFitter로 폭 가변.
        HorizontalLayoutGroup drawerLayout = MissionUi.AddRow(drawer, 6f, new RectOffset(8, 8, 0, 0));
        drawerLayout.childAlignment = TextAnchor.MiddleRight;
        drawerLayout.childForceExpandHeight = false;
        ContentSizeFitter drawerFitter = drawer.AddComponent<ContentSizeFitter>();
        drawerFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        drawerFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        drawer.SetActive(false);

        // 도장 오버레이 — 우측 '보상 영역' 위에 겹쳐 찍힌다(같은 origin, 큰→작게 스케일).
        // 카드의 HorizontalLayoutGroup이 자식으로 잡아 우측에 얇게 끼우고 버튼을 밀지 않도록 레이아웃에서 제외.
        GameObject stamp = MissionUi.CreatePanel("Stamp", card.transform, MissionUi.StampColor);
        LayoutElement stampLayout = stamp.AddComponent<LayoutElement>();
        stampLayout.ignoreLayout = true;
        RectTransform stampRect = stamp.GetComponent<RectTransform>();
        stampRect.anchorMin = new Vector2(0.5f, 0.5f);
        stampRect.anchorMax = new Vector2(0.5f, 0.5f);
        stampRect.pivot = new Vector2(0.5f, 0.5f);          // 카드 정중앙에 겹쳐 찍힘(보상은 뒤로 가리지 않음)
        stampRect.anchoredPosition = Vector2.zero;
        stampRect.sizeDelta = new Vector2(160f, 60f);
        stamp.AddComponent<CanvasGroup>();
        TextMeshProUGUI stampText = MissionUi.CreateText("StampText", stamp.transform, "달성", 20f, MissionUi.TextWhite,
            TextAlignmentOptions.Center);
        MissionUi.SetStretch(stampText.gameObject, Vector4.zero);
        stamp.SetActive(false);

        MissionCardRow row = card.AddComponent<MissionCardRow>();
        row.BindExisting();
        card.SetActive(false);
        return row;
    }
}
