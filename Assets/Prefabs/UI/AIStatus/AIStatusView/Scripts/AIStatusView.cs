using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AI 서버/하드웨어 현황 패널. 헤더(lite/full 토글 + 새로고침 + 닫기) + 세로 스크롤 Body.
/// Body: 서버 카드 / GPU 섹션 / 시스템 카드 / 벤치 카드(full) / 모델(드롭다운+리로드+토큰테스트).
/// 이중 모드: 베이크된 계층이 있으면 BindExisting, 없으면 BuildHierarchy. (SkillView/Mission 방법론)
/// 설계: Assets/Prefabs/UI/AIStatus/AISTATUS_Design.md
/// </summary>
public class AIStatusView : MonoBehaviour
{
    // 외부(AIStatusClient) 연동 이벤트
    public event System.Action RefreshRequested;
    public event System.Action<bool> ModeChanged;         // true=full
    public event System.Action<string> ReloadModelRequested; // 드롭다운 선택 모델 gguf 로드
    public event System.Action TokenTestRequested;        // 현재 로드 모델 토큰 속도 측정

    [Header("Style")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(560f, 640f);
    [SerializeField] private float headerHeight = 52f;
    [SerializeField] private Sprite panelSprite;

    [Header("Bound (자동 채움, 인스펙터 등록 가능)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button modeButton;
    [SerializeField] private TextMeshProUGUI serverModelText;
    [SerializeField] private TextMeshProUGUI serverHealthText;
    [SerializeField] private TextMeshProUGUI slotsText;
    [SerializeField] private TextMeshProUGUI ctxText;
    [SerializeField] private Transform gpuList;
    [SerializeField] private AIStatusRow deviceTemplate;
    [SerializeField] private TextMeshProUGUI ramText;
    [SerializeField] private TextMeshProUGUI cpuText;
    [SerializeField] private RectTransform ramGaugeFill;
    [SerializeField] private RectTransform cpuGaugeFill;
    [SerializeField] private RectTransform vramGaugeFill;
    [SerializeField] private GameObject benchCard;
    [SerializeField] private TextMeshProUGUI benchTokText;
    [SerializeField] private TextMeshProUGUI benchPromptText;
    [SerializeField] private TextMeshProUGUI benchElapsedText;
    [SerializeField] private TMP_Dropdown modelDropdown;
    [SerializeField] private Button reloadButton;
    [SerializeField] private Button tokenTestButton;
    [SerializeField] private TextMeshProUGUI modelResultText;

    private bool built;
    private bool fullMode; // 사용자가 토글한 요청 모드(true=full)
    private bool modelsPopulated;
    private AIStatusData.AIStatusSnapshot snapshot;

    private readonly List<AIStatusRow> gpuRows = new List<AIStatusRow>();
    private readonly List<string> modelFileNames = new List<string>(); // 드롭다운 index → gguf 파일명

    private void Awake()
    {
        ApplyStyleOverrides();
        EnsureBuilt();
    }

    private void OnEnable()
    {
        // 모델 드롭다운(ModelDataLocal)을 채우고, 진입 시 재조회 트리거.
        PopulateModels();
        RefreshRequested?.Invoke();
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public bool FullMode => fullMode;

    public void SetStatus(AIStatusData.AIStatusSnapshot s)
    {
        snapshot = s;
        Refresh();
    }

    public void Refresh()
    {
        if (!built)
        {
            return;
        }

        RefreshServer();
        RefreshGpu();
        RefreshSystem();
        RefreshBenchmark();
    }

    // 모델 리로드/토큰테스트 결과 문구(AIStatusClient가 호출).
    public void SetModelResult(string text)
    {
        if (modelResultText != null)
        {
            modelResultText.text = text;
        }
    }

#if UNITY_EDITOR
    // 에디터 베이크 전용: 전체 UI를 코드로 생성해 프리팹에 굽는다.
    public void EditorBuild(Sprite roundedSprite = null, TMP_FontAsset fontAsset = null)
    {
        if (roundedSprite != null) panelSprite = roundedSprite;
        if (fontAsset != null) font = fontAsset;
        ApplyStyleOverrides();
        BuildHierarchy();

        // GPU 리스트 클론은 굽지 않는다(런타임 동적 생성). 비활성 템플릿에 미리보기만 세팅.
        if (deviceTemplate != null)
        {
            deviceTemplate.SetupGpu(new AIStatusData.GpuDevice
            {
                index = 0, name = "GPU 0", vramTotalGb = 24f, vramFreeGb = 18f, vramUsedMb = 6000f,
                utilPercent = 40f, tempC = 62f
            });
        }

        // 드롭다운 옵션은 정적(ModelDataLocal)이라 베이크 시점에 채워둔다.
        PopulateModels();

        // 서버/시스템/벤치 카드는 샘플 스냅샷으로 모양 확인(full → 벤치 노출).
        SetStatus(BuildSampleSnapshot());
    }

    private static AIStatusData.AIStatusSnapshot BuildSampleSnapshot()
    {
        AIStatusData.AIStatusSnapshot s = new AIStatusData.AIStatusSnapshot();
        s.ok = true;
        s.level = "full";
        s.llm.running = true;
        s.llm.modelName = "Qwen3.5-9B-Q4_K_M.gguf";
        s.llm.health = "ok";
        s.llm.slotsTotal = 1;
        s.llm.slotsProcessing = 0;
        s.llm.nCtx = 32768;
        s.system.available = true;
        s.system.ramTotalGb = 32f;
        s.system.ramAvailableGb = 18f;
        s.system.ramPercent = 44f;
        s.system.cpuLogical = 16;
        s.system.cpuPhysical = 8;
        s.system.cpuPercent = 22f;
        s.benchmark.available = true;
        s.benchmark.predictedPerSecond = 48f;
        s.benchmark.promptPerSecond = 520f;
        s.benchmark.elapsedSec = 0.67f;
        s.benchmark.predictedN = 32;
        return s;
    }
#endif

    private void ApplyStyleOverrides()
    {
        AIStatusUi.RoundedSpriteOverride = panelSprite;
        AIStatusUi.FontOverride = font;
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
        return AIStatusUi.FindDeepChild(transform, "Body") != null;
    }

    // ── 모델 드롭다운(ModelDataLocal) ──────────────────────────────────────────
    // ModelDataLocal의 모델 전부를 드롭다운 옵션으로. 현재 선택(SettingManager)을 기본값으로.
    private void PopulateModels()
    {
        if (modelDropdown == null)
        {
            return;
        }

        modelFileNames.Clear();
        List<string> labels = new List<string>();

        string currentId = null;
        if (SettingManager.Instance != null && SettingManager.Instance.settings != null)
        {
            currentId = SettingManager.Instance.settings.model_name_Local; // 현재 선택 모델(Id)
        }

        int currentIndex = 0;
        int idx = 0;
        foreach (ModelDataLocal.ModelOption opt in ModelDataLocal.ModelOptions)
        {
            if (opt.FileInfos == null || opt.FileInfos.Count == 0)
            {
                continue;
            }

            labels.Add(opt.DisplayName);
            modelFileNames.Add(opt.FileInfos[0].FileName); // 서버가 인식하는 gguf 이름
            if (!string.IsNullOrEmpty(currentId) && opt.Id == currentId)
            {
                currentIndex = idx;
            }

            idx++;
        }

        modelDropdown.ClearOptions();
        modelDropdown.AddOptions(labels);
        if (labels.Count > 0)
        {
            modelDropdown.SetValueWithoutNotify(Mathf.Clamp(currentIndex, 0, labels.Count - 1));
            modelDropdown.RefreshShownValue();
        }

        modelsPopulated = true;
    }

    private string SelectedModelFileName()
    {
        if (modelDropdown == null || modelFileNames.Count == 0)
        {
            return "";
        }

        int i = Mathf.Clamp(modelDropdown.value, 0, modelFileNames.Count - 1);
        return modelFileNames[i];
    }

    private void OnReloadClicked()
    {
        string fileName = SelectedModelFileName();
        if (string.IsNullOrEmpty(fileName))
        {
            return;
        }

        SetModelResult("로딩 중...");
        ReloadModelRequested?.Invoke(fileName);
    }

    private void OnTokenTestClicked()
    {
        SetModelResult("토큰 테스트 중...");
        TokenTestRequested?.Invoke();
    }

    // ── 데이터 갱신 ───────────────────────────────────────────────────────────
    private void RefreshServer()
    {
        AIStatusData.LlmServer s = snapshot != null ? snapshot.llm : null;
        bool running = s != null && s.running;

        if (serverModelText != null)
        {
            serverModelText.text = running ? (string.IsNullOrEmpty(s.modelName) ? "(unknown)" : s.modelName) : "미연결";
        }

        if (serverHealthText != null)
        {
            serverHealthText.text = running ? (s.HealthOk ? "OK" : (string.IsNullOrEmpty(s.health) ? "-" : s.health)) : "offline";
            serverHealthText.color = (running && s.HealthOk) ? AIStatusUi.StatusOk : AIStatusUi.StatusBad;
        }

        if (slotsText != null)
        {
            slotsText.text = running ? (s.slotsProcessing + " / " + s.slotsTotal + " busy") : "-";
        }

        if (ctxText != null)
        {
            ctxText.text = (running && s.nCtx > 0) ? s.nCtx.ToString() : "-";
        }
    }

    private void RefreshGpu()
    {
        if (gpuList == null || deviceTemplate == null)
        {
            return;
        }

        for (int i = gpuRows.Count - 1; i >= 0; i--)
        {
            if (gpuRows[i] != null)
            {
                Destroy(gpuRows[i].gameObject);
            }
        }

        gpuRows.Clear();

        if (!Application.isPlaying || snapshot == null || snapshot.gpus == null)
        {
            return; // 에디터(비플레이)에선 클론을 만들지 않음(템플릿 미리보기만)
        }

        for (int i = 0; i < snapshot.gpus.Count; i++)
        {
            AIStatusRow row = Instantiate(deviceTemplate, gpuList);
            row.gameObject.name = "Gpu_" + i;
            row.gameObject.SetActive(true);
            row.SetupGpu(snapshot.gpus[i]);
            gpuRows.Add(row);
        }
    }

    private void RefreshSystem()
    {
        AIStatusData.SystemInfo sys = snapshot != null ? snapshot.system : null;
        bool ok = sys != null && sys.available;

        if (ramText != null)
        {
            ramText.text = ok ? string.Format("{0:0.0}/{1:0.0} GB ({2:0}%)", sys.ramAvailableGb, sys.ramTotalGb, sys.ramPercent) : "psutil 미설치";
        }

        AIStatusUi.SetGauge(ramGaugeFill, ok ? sys.ramPercent / 100f : 0f);

        if (cpuText != null)
        {
            cpuText.text = ok ? string.Format("{0:0}%  ({1}C/{2}T)", sys.cpuPercent, sys.cpuPhysical, sys.cpuLogical) : "-";
        }

        AIStatusUi.SetGauge(cpuGaugeFill, ok ? sys.cpuPercent / 100f : 0f);

        // VRAM 사용률(첫 GPU)
        float vramRatio = 0f;
        if (snapshot != null && snapshot.gpus != null && snapshot.gpus.Count > 0)
        {
            AIStatusData.GpuDevice g = snapshot.gpus[0];
            if (g.vramTotalGb > 0f)
            {
                vramRatio = 1f - (g.vramFreeGb / g.vramTotalGb);
            }
        }

        AIStatusUi.SetGauge(vramGaugeFill, vramRatio);
    }

    private void RefreshBenchmark()
    {
        bool show = snapshot != null && snapshot.HasFull;
        if (benchCard != null)
        {
            benchCard.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        AIStatusData.Benchmark b = snapshot.benchmark;
        bool ok = b != null && b.available;
        if (benchTokText != null) benchTokText.text = ok ? string.Format("{0:0.0} tok/s", b.predictedPerSecond) : "-";
        if (benchPromptText != null) benchPromptText.text = ok ? string.Format("{0:0.0} tok/s", b.promptPerSecond) : "-";
        if (benchElapsedText != null) benchElapsedText.text = ok ? string.Format("{0:0.00}s / {1} tok", b.elapsedSec, b.predictedN) : "-";
    }

    // ── 모드 토글 ─────────────────────────────────────────────────────────────
    private void OnModeToggle()
    {
        fullMode = !fullMode;
        UpdateModeVisual();
        ModeChanged?.Invoke(fullMode);
        RefreshRequested?.Invoke();
    }

    private void OnRefreshClicked()
    {
        RefreshRequested?.Invoke();
    }

    private void UpdateModeVisual()
    {
        if (modeButton == null)
        {
            return;
        }

        Image img = modeButton.GetComponent<Image>();
        if (img != null)
        {
            AIStatusUi.ApplyRounded(img, fullMode ? AIStatusUi.Accent : AIStatusUi.PanelBg2);
        }

        TMP_Text label = modeButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = fullMode ? "full" : "lite";
        }
    }

    // ── 베이크된 프리팹 연결 ───────────────────────────────────────────────────
    private void BindExisting()
    {
        built = true;

        titleText = titleText != null ? titleText : AIStatusUi.FindComponent<TMP_Text>(transform, "TitleText");
        modeButton = modeButton != null ? modeButton : AIStatusUi.FindComponent<Button>(transform, "ModeButton");
        gpuList = gpuList != null ? gpuList : AIStatusUi.FindDeepChild(transform, "GpuList");
        benchCard = benchCard != null ? benchCard : SafeGameObject(AIStatusUi.FindDeepChild(transform, "BenchCard"));
        modelDropdown = modelDropdown != null ? modelDropdown : AIStatusUi.FindComponent<TMP_Dropdown>(transform, "ModelDropdown");
        modelResultText = modelResultText != null ? modelResultText : AIStatusUi.FindComponent<TextMeshProUGUI>(transform, "ModelResult");

        if (deviceTemplate == null)
        {
            Transform t = AIStatusUi.FindDeepChild(transform, "DeviceTemplate");
            if (t != null) deviceTemplate = t.GetComponent<AIStatusRow>();
        }

        if (deviceTemplate != null)
        {
            deviceTemplate.BindExisting();
            deviceTemplate.gameObject.SetActive(false);
        }

        BindButton("ModeButton", OnModeToggle);
        BindButton("RefreshButton", OnRefreshClicked);
        BindButton("CloseButton", Hide);
        BindButton("ReloadButton", OnReloadClicked);
        BindButton("TokenTestButton", OnTokenTestClicked);
        UpdateModeVisual();
    }

    private static GameObject SafeGameObject(Transform t)
    {
        return t != null ? t.gameObject : null;
    }

    private void BindButton(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = AIStatusUi.FindComponent<Button>(transform, name);
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

        Image rootBg = AIStatusUi.GetOrAdd<Image>(gameObject);
        AIStatusUi.ApplyRounded(rootBg, AIStatusUi.RootBg);

        VerticalLayoutGroup rootLayout = AIStatusUi.GetOrAdd<VerticalLayoutGroup>(gameObject);
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
        GameObject header = AIStatusUi.CreateUIObject("Header", parent);
        // 고정 높이 + flexibleHeight=0 : 헤더가 남는 세로공간을 먹지 않도록(HLG의 forceExpand 보고를 덮어씀).
        AIStatusUi.Layout(header, minH: headerHeight, prefH: headerHeight, flexH: 0f);
        AIStatusUi.AddRow(header, 8f).childForceExpandHeight = true;

        titleText = AIStatusUi.CreateText("TitleText", header.transform, "AI 상태", 20f, AIStatusUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        AIStatusUi.Layout(titleText.gameObject, flexW: 1f);

        modeButton = AIStatusUi.CreateButton("ModeButton", header.transform, "lite", AIStatusUi.PanelBg2, 14f);
        AIStatusUi.Layout(modeButton.gameObject, prefW: 60f, minW: 60f);
        modeButton.onClick.AddListener(OnModeToggle);

        Button refresh = AIStatusUi.CreateButton("RefreshButton", header.transform, "↻", AIStatusUi.PanelBg2, 18f);
        AIStatusUi.Layout(refresh.gameObject, prefW: 40f, minW: 40f);
        refresh.onClick.AddListener(OnRefreshClicked);

        Button close = AIStatusUi.CreateButton("CloseButton", header.transform, "×", AIStatusUi.HeaderBg, 24f);
        AIStatusUi.Layout(close.gameObject, prefW: 40f, minW: 40f);
        close.onClick.AddListener(Hide);

        UpdateModeVisual();
    }

    private void BuildBody(Transform parent)
    {
        GameObject body = AIStatusUi.CreatePanel("Body", parent, AIStatusUi.PanelBg);
        AIStatusUi.Layout(body, flexH: 1f); // Body만 남는 세로공간을 채운다.

        ScrollRect scroll = body.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        GameObject viewport = AIStatusUi.CreateUIObject("Viewport", body.transform);
        AIStatusUi.SetStretch(viewport, new Vector4(8f, 8f, 22f, 8f)); // 우측 22px: 스크롤바 전용 레인
        viewport.AddComponent<RectMask2D>();

        GameObject content = AIStatusUi.CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup contentLayout = AIStatusUi.AddColumn(content, 10f, new RectOffset(4, 4, 4, 4));
        contentLayout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;

        BuildScrollbar(body.transform, scroll);

        BuildServerCard(content.transform);
        BuildGpuSection(content.transform);
        BuildSystemCard(content.transform);
        BuildBenchCard(content.transform);
        BuildModelSection(content.transform);
    }

    private void BuildScrollbar(Transform parent, ScrollRect scroll)
    {
        GameObject sb = AIStatusUi.CreatePanel("Scrollbar", parent, new Color(0f, 0f, 0f, 0.30f));
        RectTransform sbRect = sb.GetComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(1f, 0f);
        sbRect.anchorMax = new Vector2(1f, 1f);
        sbRect.pivot = new Vector2(1f, 0.5f);
        sbRect.sizeDelta = new Vector2(12f, -16f);
        sbRect.anchoredPosition = new Vector2(-5f, 0f);

        Scrollbar scrollbar = sb.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = AIStatusUi.CreateUIObject("Sliding Area", sb.transform);
        AIStatusUi.SetStretch(slidingArea, new Vector4(1f, 1f, 1f, 1f));

        GameObject handle = AIStatusUi.CreatePanel("Handle", slidingArea.transform, AIStatusUi.GaugeBorder);
        AIStatusUi.SetStretch(handle, Vector4.zero);

        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    private GameObject BuildCard(string name, Transform parent, string headerLabel)
    {
        GameObject card = AIStatusUi.CreatePanel(name, parent, AIStatusUi.PanelBg2);
        VerticalLayoutGroup col = AIStatusUi.AddColumn(card, 4f, new RectOffset(12, 12, 10, 10));
        col.childForceExpandWidth = true;
        ContentSizeFitter f = card.AddComponent<ContentSizeFitter>();
        f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI header = AIStatusUi.CreateText("Header", card.transform, headerLabel, 15f, AIStatusUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        AIStatusUi.Layout(header.gameObject, minH: 22f, prefH: 22f);
        return card;
    }

    private void BuildServerCard(Transform parent)
    {
        GameObject card = BuildCard("ServerCard", parent, "서버");
        AIStatusUi.CreateKvRow("ModelRow", card.transform, "모델", out serverModelText);
        AIStatusUi.CreateKvRow("HealthRow", card.transform, "헬스", out serverHealthText);
        AIStatusUi.CreateKvRow("SlotsRow", card.transform, "슬롯", out slotsText);
        AIStatusUi.CreateKvRow("CtxRow", card.transform, "컨텍스트", out ctxText);
    }

    private void BuildGpuSection(Transform parent)
    {
        TextMeshProUGUI header = AIStatusUi.CreateText("GpuHeader", parent, "GPU", 15f, AIStatusUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        AIStatusUi.Layout(header.gameObject, minH: 22f, prefH: 22f);

        GameObject list = AIStatusUi.CreateUIObject("GpuList", parent);
        VerticalLayoutGroup col = AIStatusUi.AddColumn(list, 6f);
        col.childForceExpandWidth = true;
        ContentSizeFitter f = list.AddComponent<ContentSizeFitter>();
        f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        gpuList = list.transform;

        deviceTemplate = BuildRowTemplate("DeviceTemplate", list.transform);
    }

    private void BuildSystemCard(Transform parent)
    {
        GameObject card = BuildCard("SystemCard", parent, "시스템");
        BuildGaugeRow(card.transform, "RAM", out ramGaugeFill, out ramText);
        BuildGaugeRow(card.transform, "CPU", out cpuGaugeFill, out cpuText);
        TextMeshProUGUI ignore;
        BuildGaugeRow(card.transform, "VRAM", out vramGaugeFill, out ignore);
    }

    private void BuildBenchCard(Transform parent)
    {
        benchCard = BuildCard("BenchCard", parent, "벤치마크 (full)");
        AIStatusUi.CreateKvRow("TokRow", benchCard.transform, "생성 속도", out benchTokText);
        AIStatusUi.CreateKvRow("PromptRow", benchCard.transform, "프롬프트 속도", out benchPromptText);
        AIStatusUi.CreateKvRow("ElapsedRow", benchCard.transform, "소요/토큰", out benchElapsedText);
        benchCard.SetActive(false);
    }

    // 모델 카드: 드롭다운(ModelDataLocal) + [리로드] + [토큰테스트] + 결과 라인.
    private void BuildModelSection(Transform parent)
    {
        GameObject card = BuildCard("ModelCard", parent, "모델");

        GameObject ddRow = AIStatusUi.CreateUIObject("ModelDropdownRow", card.transform);
        AIStatusUi.Layout(ddRow, minH: 34f, prefH: 34f, flexH: 0f);
        AIStatusUi.AddRow(ddRow, 8f).childForceExpandHeight = true;
        modelDropdown = AIStatusUi.CreateDropdown("ModelDropdown", ddRow.transform);
        AIStatusUi.Layout(modelDropdown.gameObject, flexW: 1f, minH: 30f, prefH: 30f);

        GameObject btnRow = AIStatusUi.CreateUIObject("ModelButtonRow", card.transform);
        AIStatusUi.Layout(btnRow, minH: 34f, prefH: 34f, flexH: 0f);
        AIStatusUi.AddRow(btnRow, 8f).childForceExpandHeight = true;

        reloadButton = AIStatusUi.CreateButton("ReloadButton", btnRow.transform, "리로드", AIStatusUi.Accent, 14f);
        AIStatusUi.Layout(reloadButton.gameObject, flexW: 1f, minH: 30f, prefH: 30f);
        reloadButton.onClick.AddListener(OnReloadClicked);

        tokenTestButton = AIStatusUi.CreateButton("TokenTestButton", btnRow.transform, "토큰테스트", AIStatusUi.PanelBg2, 14f);
        AIStatusUi.Layout(tokenTestButton.gameObject, flexW: 1f, minH: 30f, prefH: 30f);
        tokenTestButton.onClick.AddListener(OnTokenTestClicked);

        modelResultText = AIStatusUi.CreateText("ModelResult", card.transform, "-", 12f, AIStatusUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        AIStatusUi.Layout(modelResultText.gameObject, minH: 18f, prefH: 18f);
    }

    // 게이지 한 줄: 좌 라벨 + 게이지(flexW) + 우 값. out으로 Fill/값 텍스트를 돌려준다.
    private void BuildGaugeRow(Transform parent, string label, out RectTransform fill, out TextMeshProUGUI valueText)
    {
        GameObject row = AIStatusUi.CreateUIObject(label + "Row", parent);
        AIStatusUi.Layout(row, minH: 22f, prefH: 22f);
        AIStatusUi.AddRow(row, 8f).childForceExpandHeight = true;

        TextMeshProUGUI key = AIStatusUi.CreateText("Key", row.transform, label, 13f, AIStatusUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        AIStatusUi.Layout(key.gameObject, prefW: 54f, minW: 54f);

        GameObject gauge = AIStatusUi.CreateGauge(label + "Gauge", row.transform, out fill, AIStatusUi.GaugeFill);
        AIStatusUi.Layout(gauge, flexW: 1f, minH: 12f, prefH: 12f);

        valueText = AIStatusUi.CreateText("Value", row.transform, "-", 12f, AIStatusUi.TextWhite,
            TextAlignmentOptions.MidlineRight);
        AIStatusUi.Layout(valueText.gameObject, prefW: 160f, minW: 120f);
    }

    // GPU 행 템플릿(비활성). 런타임에 Instantiate로 클론한다.
    private AIStatusRow BuildRowTemplate(string name, Transform parent)
    {
        GameObject rowObj = AIStatusUi.CreatePanel(name, parent, AIStatusUi.PanelBg2);
        AIStatusUi.Layout(rowObj, minH: 80f, prefH: 80f);
        VerticalLayoutGroup col = AIStatusUi.AddColumn(rowObj, 3f, new RectOffset(10, 10, 8, 8));
        col.childForceExpandWidth = true;

        GameObject titleRow = AIStatusUi.CreateUIObject("TitleRow", rowObj.transform);
        AIStatusUi.Layout(titleRow, minH: 22f, prefH: 22f);
        AIStatusUi.AddRow(titleRow, 8f).childForceExpandHeight = true;

        TextMeshProUGUI title = AIStatusUi.CreateText("Title", titleRow.transform, "-", 15f, AIStatusUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        AIStatusUi.Layout(title.gameObject, flexW: 1f);

        GameObject badge = AIStatusUi.CreatePanel("Badge", titleRow.transform, AIStatusUi.Accent);
        AIStatusUi.Layout(badge, prefW: 76f, minW: 76f, minH: 20f, prefH: 20f);
        TextMeshProUGUI badgeText = AIStatusUi.CreateText("BadgeText", badge.transform, "-", 12f, AIStatusUi.TextWhite,
            TextAlignmentOptions.Center);
        AIStatusUi.SetStretch(badgeText.gameObject, Vector4.zero);

        GameObject gauge = AIStatusUi.CreateGauge("Gauge", rowObj.transform, out _, AIStatusUi.GaugeFill);
        AIStatusUi.Layout(gauge, minH: 12f, prefH: 12f);

        TextMeshProUGUI line1 = AIStatusUi.CreateText("Line1", rowObj.transform, "-", 12f, AIStatusUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        AIStatusUi.Layout(line1.gameObject, minH: 16f, prefH: 16f);

        TextMeshProUGUI line2 = AIStatusUi.CreateText("Line2", rowObj.transform, "-", 12f, AIStatusUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        AIStatusUi.Layout(line2.gameObject, minH: 16f, prefH: 16f);

        AIStatusRow row = rowObj.AddComponent<AIStatusRow>();
        row.BindExisting();
        rowObj.SetActive(false);
        return row;
    }
}
