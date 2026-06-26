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
    [SerializeField] private Sprite panelSprite;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stampClip;

    [Header("Test")]
    [Tooltip("켜면 카드를 꾸욱 눌러 진행도를 +1 (테스트용). 출시 전 끄거나 MissionTestPoke 제거.")]
    [SerializeField] private bool enableTestPoke = true;

    // 탭 순서/라벨
    private static readonly MissionCategory[] Categories =
    {
        MissionCategory.Onboarding,
        MissionCategory.Conversation,
        MissionCategory.Affection,
        MissionCategory.Productivity,
        MissionCategory.Challenge,
    };

    private bool built;
    private MissionCategory selectedCategory = MissionCategory.Onboarding;

    private TMP_Text titleText;
    private Transform tabColumn;
    private RectTransform cardContent;
    private MissionCardRow cardTemplate;

    private readonly List<MissionTabButton> tabButtons = new List<MissionTabButton>();
    private readonly List<MissionCardRow> visibleRows = new List<MissionCardRow>();
    private MissionCardRow openDrawerRow;

    private string Lang =>
        (Application.isPlaying && MissionManager.Instance != null) ? MissionManager.Instance.Language : "ko";

    private void Awake()
    {
        ApplyStyleOverrides();
        EnsureBuilt();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && MissionManager.Instance != null)
        {
            MissionManager.Instance.MissionsChanged += RefreshAll;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && MissionManager.Instance != null)
        {
            MissionManager.Instance.MissionsChanged -= RefreshAll;
        }
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    public void Show()
    {
        gameObject.SetActive(true);
        RefreshAll();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SelectCategory(MissionCategory category)
    {
        selectedCategory = category;
        openDrawerRow = null;
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

        // 탭 라벨/선택 상태만 세팅(런타임 진행도 없음). 카드 클론은 굽지 않는다(런타임에 생성).
        RefreshTabs();

        // 비활성 카드 템플릿에 미리보기 데이터 1건(에디터에서 모양 확인용, 비활성 유지).
        List<MissionDef> defs = MissionCatalog.GetByCategory(selectedCategory);
        if (cardTemplate != null && defs.Count > 0)
        {
            cardTemplate.Setup(defs[0], new MissionProgress(defs[0].id), "ko", null, null);
        }
    }
#endif

    private void ApplyStyleOverrides()
    {
        MissionUi.RoundedSpriteOverride = panelSprite;
        MissionUi.FontOverride = font;
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
    private void RefreshTabs()
    {
        for (int i = 0; i < tabButtons.Count && i < Categories.Length; i++)
        {
            MissionCategory cat = Categories[i];
            int done = 0;
            int total = 0;
            if (Application.isPlaying && MissionManager.Instance != null)
            {
                MissionManager.Instance.GetCategoryCounts(cat, out done, out total);
            }
            else
            {
                total = MissionCatalog.GetByCategory(cat).Count;
            }

            tabButtons[i].Setup(cat, CategoryLabel(cat), done + "/" + total, cat == selectedCategory, SelectCategory);
        }
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

        List<MissionDef> defs = MissionCatalog.GetByCategory(selectedCategory);
        for (int i = 0; i < defs.Count; i++)
        {
            MissionDef def = defs[i];
            MissionCardRow row = Instantiate(cardTemplate, cardContent);
            row.gameObject.name = "Card_" + def.id;
            row.gameObject.SetActive(true);
            MissionProgress progress = GetProgress(def.id);
            row.Setup(def, progress, Lang, OnClaimClicked, OnDrawerOpened);

            // 테스트 훅: 런타임에만 부착(프리팹 비포함, 분리 용이)
            if (enableTestPoke && Application.isPlaying && row.GetComponent<MissionTestPoke>() == null)
            {
                row.gameObject.AddComponent<MissionTestPoke>();
            }

            visibleRows.Add(row);
        }
    }

    private MissionProgress GetProgress(string id)
    {
        if (Application.isPlaying && MissionManager.Instance != null)
        {
            return MissionManager.Instance.GetProgress(id);
        }

        return new MissionProgress(id);
    }

    private void OnClaimClicked(string id)
    {
        if (!Application.isPlaying || MissionManager.Instance == null)
        {
            return;
        }

        bool ok = MissionManager.Instance.ClaimReward(id);
        if (!ok)
        {
            return;
        }

        PlayStampSound();

        bool nowAllDone = MissionManager.Instance.IsCompleted(id);
        // MissionsChanged 이벤트로 이미 RefreshAll이 돌아 카드가 재생성됨 → 새 카드에서 도장 연출.
        MissionCardRow row = FindRow(id);
        if (row != null)
        {
            row.PlayClaimEffect(nowAllDone);
        }
    }

    private void OnResetClicked()
    {
        if (Application.isPlaying && MissionManager.Instance != null)
        {
            MissionManager.Instance.ResetAllProgress(); // MissionsChanged → RefreshAll
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

    private static string CategoryLabel(MissionCategory category)
    {
        switch (category)
        {
            case MissionCategory.Onboarding: return "첫걸음";
            case MissionCategory.Conversation: return "대화";
            case MissionCategory.Affection: return "교감";
            case MissionCategory.Productivity: return "생활";
            case MissionCategory.Challenge: return "도전";
            default: return category.ToString();
        }
    }

    // ── 베이크된 프리팹 연결 ───────────────────────────────────────────────────
    private void BindExisting()
    {
        built = true;

        titleText = MissionUi.FindComponent<TMP_Text>(transform, "TitleText");
        tabColumn = MissionUi.FindDeepChild(transform, "TabColumn");
        cardContent = MissionUi.FindComponent<RectTransform>(transform, "CardContent");

        Transform template = MissionUi.FindDeepChild(transform, "CardTemplate");
        if (template != null)
        {
            cardTemplate = template.GetComponent<MissionCardRow>();
            if (cardTemplate != null)
            {
                cardTemplate.BindExisting();
                cardTemplate.gameObject.SetActive(false);
            }
        }

        tabButtons.Clear();
        if (tabColumn != null)
        {
            for (int i = 0; i < tabColumn.childCount; i++)
            {
                MissionTabButton tab = tabColumn.GetChild(i).GetComponent<MissionTabButton>();
                if (tab != null)
                {
                    tab.BindExisting();
                    tabButtons.Add(tab);
                }
            }
        }

        BindButton("CloseButton", Hide);
        BindButton("ResetButton", OnResetClicked);
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
        MissionUi.Layout(header, minH: 44f, prefH: 44f);
        MissionUi.AddRow(header, 8f).childForceExpandHeight = true;

        titleText = MissionUi.CreateText("TitleText", header.transform, "미션", 22f, MissionUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MissionUi.Layout(titleText.gameObject, flexW: 1f);

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
        MissionUi.Layout(column, prefW: 140f, minW: 140f);
        VerticalLayoutGroup layout = MissionUi.AddColumn(column, 6f);
        layout.childForceExpandHeight = false;
        tabColumn = column.transform;

        tabButtons.Clear();
        for (int i = 0; i < Categories.Length; i++)
        {
            MissionTabButton tab = BuildTab(column.transform, Categories[i]);
            tabButtons.Add(tab);
        }
    }

    private MissionTabButton BuildTab(Transform parent, MissionCategory category)
    {
        GameObject tabObject = MissionUi.CreatePanel("Tab_" + category, parent, MissionUi.TabBg);
        MissionUi.Layout(tabObject, minH: 50f, prefH: 50f);

        Button button = tabObject.AddComponent<Button>();
        button.targetGraphic = tabObject.GetComponent<Image>();

        TextMeshProUGUI label = MissionUi.CreateText("Label", tabObject.transform, CategoryLabel(category), 17f,
            MissionUi.TextMuted, TextAlignmentOptions.Left);
        MissionUi.SetStretch(label.gameObject, new Vector4(14f, 4f, 10f, 18f));

        TextMeshProUGUI count = MissionUi.CreateText("Count", tabObject.transform, "0/0", 12f, MissionUi.TextMuted,
            TextAlignmentOptions.BottomLeft);
        MissionUi.SetStretch(count.gameObject, new Vector4(14f, 6f, 10f, 4f));

        MissionTabButton tab = tabObject.AddComponent<MissionTabButton>();
        tab.BindExisting();
        return tab;
    }

    private void BuildCardScroll(Transform parent)
    {
        GameObject scrollObject = MissionUi.CreatePanel("CardScroll", parent, MissionUi.PanelBg);
        MissionUi.Layout(scrollObject, flexW: 1f);

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        GameObject viewport = MissionUi.CreateUIObject("Viewport", scrollObject.transform);
        MissionUi.SetStretch(viewport, new Vector4(8f, 8f, 8f, 8f));
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

        TextMeshProUGUI desc = MissionUi.CreateText("Description", info.transform, "미션 설명", 15f, MissionUi.TextWhite,
            TextAlignmentOptions.TopLeft);
        MissionUi.Layout(desc.gameObject, flexH: 1f, minH: 22f);

        GameObject gaugeRow = MissionUi.CreateUIObject("GaugeRow", info.transform);
        MissionUi.Layout(gaugeRow, minH: 20f, prefH: 20f);
        MissionUi.AddRow(gaugeRow, 8f).childForceExpandHeight = true;

        // 게이지: 테두리(Frame) → 배경(Bg) → 내부 채움 바(Fill)
        GameObject gaugeFrame = MissionUi.CreatePanel("GaugeFrame", gaugeRow.transform, MissionUi.GaugeBorder);
        MissionUi.Layout(gaugeFrame, flexW: 1f, minH: 18f, prefH: 18f);

        GameObject gaugeBg = MissionUi.CreatePanel("GaugeBg", gaugeFrame.transform, MissionUi.GaugeBg);
        MissionUi.SetStretch(gaugeBg, new Vector4(2f, 2f, 2f, 2f));

        GameObject gaugeFillObject = MissionUi.CreateUIObject("GaugeFill", gaugeBg.transform);
        MissionUi.SetStretch(gaugeFillObject, new Vector4(1f, 1f, 1f, 1f));
        Image gaugeFill = gaugeFillObject.AddComponent<Image>();
        gaugeFill.sprite = null;   // 단색 쿼드: fillAmount 0이면 완전히 빈 채로 렌더(9-slice 캡 잔상 방지)
        gaugeFill.color = MissionUi.GaugeFill;
        gaugeFill.type = Image.Type.Filled;
        gaugeFill.fillMethod = Image.FillMethod.Horizontal;
        gaugeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        gaugeFill.fillAmount = 0f;

        TextMeshProUGUI progressLabel = MissionUi.CreateText("ProgressLabel", gaugeRow.transform, "0 / 0", 13f,
            MissionUi.TextMuted, TextAlignmentOptions.MidlineRight);
        MissionUi.Layout(progressLabel.gameObject, prefW: 84f, minW: 64f);

        TextMeshProUGUI tierLabel = MissionUi.CreateText("TierLabel", gaugeRow.transform, "", 12f, MissionUi.TextMuted,
            TextAlignmentOptions.MidlineRight);
        MissionUi.Layout(tierLabel.gameObject, prefW: 44f, minW: 30f);

        // 우측 보상 영역
        GameObject rewardArea = MissionUi.CreateUIObject("RewardArea", card.transform);
        MissionUi.Layout(rewardArea, prefW: 112f, minW: 112f);

        Button chip = MissionUi.CreateButton("RewardChipButton", rewardArea.transform, null, MissionUi.PanelBg, 14f);
        RectTransform chipRect = chip.transform as RectTransform;
        chipRect.anchorMin = new Vector2(1f, 0.5f);
        chipRect.anchorMax = new Vector2(1f, 0.5f);
        chipRect.pivot = new Vector2(1f, 0.5f);
        chipRect.anchoredPosition = new Vector2(0f, 0f);
        chipRect.sizeDelta = new Vector2(100f, 42f);
        TextMeshProUGUI chipText = MissionUi.CreateText("RewardChipText", chip.transform, "받기", 15f,
            MissionUi.TextWhite, TextAlignmentOptions.Center);
        MissionUi.SetStretch(chipText.gameObject, Vector4.zero);

        // 보상 서랍 (오른쪽 칩에서 왼쪽으로 펼쳐짐)
        GameObject drawer = MissionUi.CreatePanel("Drawer", rewardArea.transform, MissionUi.PanelBg);
        RectTransform drawerRect = drawer.GetComponent<RectTransform>();
        drawerRect.anchorMin = new Vector2(1f, 0.5f);
        drawerRect.anchorMax = new Vector2(1f, 0.5f);
        drawerRect.pivot = new Vector2(1f, 0.5f);
        drawerRect.anchoredPosition = new Vector2(-104f, 0f);
        drawerRect.sizeDelta = new Vector2(220f, 34f);
        drawer.AddComponent<CanvasGroup>();

        GameObject drawerContent = MissionUi.CreateUIObject("DrawerContent", drawer.transform);
        MissionUi.SetStretch(drawerContent, new Vector4(6f, 4f, 6f, 4f));
        HorizontalLayoutGroup drawerLayout = MissionUi.AddRow(drawerContent, 6f);
        drawerLayout.childAlignment = TextAnchor.MiddleRight;
        ContentSizeFitter drawerFitter = drawerContent.AddComponent<ContentSizeFitter>();
        drawerFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        drawerFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        drawer.SetActive(false);

        // 도장 오버레이 — 우측 '보상 영역' 위에 겹쳐 찍힌다(같은 origin, 큰→작게 스케일).
        // 카드의 HorizontalLayoutGroup이 자식으로 잡아 우측에 얇게 끼우고 버튼을 밀지 않도록 레이아웃에서 제외.
        GameObject stamp = MissionUi.CreatePanel("Stamp", card.transform, MissionUi.StampColor);
        LayoutElement stampLayout = stamp.AddComponent<LayoutElement>();
        stampLayout.ignoreLayout = true;
        RectTransform stampRect = stamp.GetComponent<RectTransform>();
        stampRect.anchorMin = new Vector2(1f, 0.5f);
        stampRect.anchorMax = new Vector2(1f, 0.5f);
        stampRect.pivot = new Vector2(0.5f, 0.5f);          // 중앙 피벗: 스케일이 보상 중앙을 기준으로 줄어듦
        stampRect.anchoredPosition = new Vector2(-62f, 0f); // 우측 보상 칩 위에 겹침
        stampRect.sizeDelta = new Vector2(104f, 66f);
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
