using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Jukebox Downloader UI. 유튜브를 키워드로 검색해 결과(썸네일+제목)를 목록으로 보여주고,
/// 각 행의 "받기" 버튼으로 mp3 다운로드를 요청한다. 서버 연동은 JukeboxDownloaderClient가 담당한다.
///
/// SkillView와 동일한 방법론:
///  - 프리팹은 EditorBuild()로 코드에서 구워지고(#if UNITY_EDITOR), 런타임은 BindExisting()으로 연결만 한다.
///  - 다크 팔레트/팩토리 헬퍼는 SkillView 것을 그대로 따른다.
///
/// 구성 요소 (레이아웃 그룹 없이 고정 앵커 배치 — SkillView.prefab 방식)
///  0. Handler   : 패널 전체를 덮는 투명 드래그 표면 (JukeboxView.prefab 방식, 빈 곳을 잡으면 창 이동)
///  1. 헤더      : 고정 높이 full-width 바. 제목 + 개수 라벨 + ×닫기
///  2. 검색 행   : 필터 토글(정사각형 ^/v) + 검색 입력(TMP) + 검색 버튼 (Enter로도 검색)
///  3. 필터 행   : 정렬 + 기간 + 길이(짧음/중간/김 — 서버가 카테고리만 지원) + 개수(5/10/20).
///                토글로 통째로 접을 수 있고, 접으면 결과 영역이 위로 늘어난다.
///  4. 결과 영역 : 우측 세로 스크롤 리스트 (행 = 썸네일 + 제목/채널·길이·조회수 + 받기).
///                진행률/완료는 받기 버튼 라벨에 표시. 행 hover 시 JukeboxDownloaderTooltip으로
///                전체 제목/상세를 보여준다. 결과가 없으면 안내 라벨.
/// </summary>
public class JukeboxDownloaderView : MonoBehaviour
{
    // ── 다크 팔레트 (SkillView와 동일) ─────────────────────────────────────────
    private static readonly Color RootBg = new Color(0.086f, 0.098f, 0.125f, 1f);
    private static readonly Color HeaderBg = new Color(0.125f, 0.141f, 0.173f, 1f);
    private static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    private static readonly Color PanelBg2 = new Color(0.153f, 0.169f, 0.204f, 1f);
    private static readonly Color InputBg = new Color(0.047f, 0.055f, 0.071f, 1f);
    private static readonly Color AccentBlue = new Color(0.243f, 0.325f, 0.502f, 1f);
    private static readonly Color AccentBlueHi = new Color(0.306f, 0.404f, 0.608f, 1f);
    private static readonly Color Border = new Color(0.290f, 0.322f, 0.376f, 1f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);

    // 검색 결과 항목 (서버 /youtube/search 응답과 매핑)
    [Serializable]
    public class Track
    {
        public string videoId;
        public string title;
        public string url;
        public string channel;
        public string durationStr;
        public string viewsStr;
        public string thumbnailHq; // video_id 기반 결정적 URL (우선)
        public string thumbnail;   // 유튜브 서명 URL (fallback)
    }

    // 검색 요청 파라미터 (클라이언트가 /youtube/search 쿼리로 변환)
    [Serializable]
    public class SearchParams
    {
        public string query;
        public string sort;        // "relevance" | "views" | "date"
        public string period;      // "" | "today" | "week" | "month" | "year"
        public string duration;    // "" | "short" | "medium" | "long"
        public int limit = 15;
    }

    // 외부(클라이언트) 연동 이벤트
    public event Action<SearchParams> SearchRequested;
    // (트랙, 상태표시콜백) — 클라이언트가 다운로드 진행률을 setStatus로 흘려보낸다.
    public event Action<Track, Action<string>> DownloadRequested;

    [Header("Style")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(420f, 520f);
    [SerializeField] private Sprite panelSprite;

    // 고정 앵커 배치 상수 (SkillView.prefab처럼 레이아웃 그룹 없이 배치)
    private const float HeaderHeight = 36f;
    private const float Margin = 10f;
    private const float RowSpacing = 6f;
    private const float SearchRowHeight = 30f;
    private const float FilterRowHeight = 26f;
    private const float SearchButtonWidth = 60f;

    private bool built;
    private Sprite roundedSprite;
    private TMP_FontAsset boundFont;

    private TMP_InputField searchInput;
    private Button searchButton;
    private TMP_Dropdown sortDropdown;
    private TMP_Dropdown periodDropdown;
    private TMP_Dropdown durationDropdown;
    private TMP_Dropdown limitDropdown;
    private RectTransform resultsContent;
    private GameObject emptyLabel;

    // 필터 행 접기/펴기 (검색 행 왼쪽 정사각형 버튼으로 토글)
    private GameObject filterRow;
    private RectTransform resultsRect;
    private TextMeshProUGUI filterToggleLabel;
    private bool filterVisible = true;

    private readonly List<Track> results = new List<Track>();

    // 검색 잠금(중복 검색 방지). 전송 시 잠그고, 완료+최소쿨다운 또는 하드캡에서 해제.
    private bool searchLocked;
    private bool searchCompleted;
    private const float SearchCooldownSeconds = 5f;   // 전송 후 최소 잠금(재검색 금지)
    private const float SearchMaxLockSeconds = 30f;   // 응답 지연 대비 하드 캡

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
        RefreshEmptyState();
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

    // 검색 결과를 목록으로 렌더. 클라이언트가 서버 응답을 파싱해 호출한다.
    public void SetResults(IEnumerable<Track> tracks)
    {
        searchCompleted = true;   // 검색 사이클 종료 신호(잠금 해제 조건)
        results.Clear();
        if (tracks != null)
        {
            results.AddRange(tracks);
        }
        RebuildRows();
        RefreshEmptyState();
    }

    // 검색 중/오류 등 목록 상단 상태를 비우고 안내만 보여주고 싶을 때.
    public void ClearResults()
    {
        searchCompleted = true;   // 실패/빈 결과도 검색 사이클 종료로 간주
        results.Clear();
        RebuildRows();
        RefreshEmptyState();
    }

    // 데모/외부 트리거용: 검색어를 채우고 즉시 검색한다.
    public void SearchFor(string query)
    {
        if (searchInput != null)
        {
            searchInput.SetTextWithoutNotify(query ?? string.Empty);
        }
        TriggerSearch();
    }

    // ── 검색 트리거 ───────────────────────────────────────────────────────────
    // TMP_InputField.onSubmit(UnityEvent<string>)용 — 문자열 인자를 받는다.
    private void OnSearchSubmit(string _)
    {
        TriggerSearch();
    }

    // 버튼 onClick / BindButton용 — 무인자.
    // 규칙: 드롭다운 변경으로는 절대 검색하지 않고, 이 진입점(버튼/Enter)에서만 검색한다.
    private void TriggerSearch()
    {
        if (searchLocked)              // 검색 중/쿨다운이면 무시(추가 검색 금지)
        {
            return;
        }
        if (searchInput == null)
        {
            return;
        }
        string query = (searchInput.text ?? string.Empty).Trim();
        if (query.Length == 0)          // 입력이 비어 있으면 검색하지 않음
        {
            return;
        }
        LockSearch();
        SearchRequested?.Invoke(GatherParams(query));
    }

    // 검색 전송 시 잠금: 버튼 비활성(비활성 느낌) + 코루틴으로 해제 시점 관리.
    private void LockSearch()
    {
        searchLocked = true;
        searchCompleted = false;
        SetInteractable(searchButton, false);
        SetButtonLabel(searchButton, "검색 중…");
        StartCoroutine(SearchLockRoutine());
    }

    // 해제 조건: (완료 && 최소 5초 경과) 또는 30초 하드캡 도달.
    private IEnumerator SearchLockRoutine()
    {
        float start = Time.unscaledTime;
        while (true)
        {
            float elapsed = Time.unscaledTime - start;
            if (elapsed >= SearchMaxLockSeconds)
            {
                break;
            }
            if (searchCompleted && elapsed >= SearchCooldownSeconds)
            {
                break;
            }
            yield return null;
        }
        searchLocked = false;
        SetInteractable(searchButton, true);
        SetButtonLabel(searchButton, "검색");
    }

    private SearchParams GatherParams(string query)
    {
        SearchParams p = new SearchParams { query = query };

        // 정렬: 0:관련성 1:조회수 2:최신
        int sortIdx = sortDropdown != null ? sortDropdown.value : 0;
        p.sort = sortIdx == 1 ? "views" : (sortIdx == 2 ? "date" : "relevance");

        // 기간: 0:전체 1:오늘 2:이번주 3:이번달 4:올해
        int periodIdx = periodDropdown != null ? periodDropdown.value : 0;
        switch (periodIdx)
        {
            case 1: p.period = "today"; break;
            case 2: p.period = "week"; break;
            case 3: p.period = "month"; break;
            case 4: p.period = "year"; break;
            default: p.period = ""; break;
        }

        // 길이: 0:전체 1:짧음 2:중간 3:김 — 서버가 카테고리(short|medium|long)로만 받는다.
        int durIdx = durationDropdown != null ? durationDropdown.value : 0;
        switch (durIdx)
        {
            case 1: p.duration = "short"; break;
            case 2: p.duration = "medium"; break;
            case 3: p.duration = "long"; break;
            default: p.duration = ""; break;
        }

        // 검색 개수: 0:5 1:10 2:20
        int limitIdx = limitDropdown != null ? limitDropdown.value : 1;
        p.limit = limitIdx == 0 ? 5 : (limitIdx == 2 ? 20 : 10);

        return p;
    }

    // ── 결과 행 렌더 ──────────────────────────────────────────────────────────
    private void RebuildRows()
    {
        if (resultsContent == null)
        {
            return;
        }
        for (int i = resultsContent.childCount - 1; i >= 0; i--)
        {
            Destroy(resultsContent.GetChild(i).gameObject);
        }
        for (int i = 0; i < results.Count; i++)
        {
            CreateResultRow(results[i], i);
        }
    }

    // 한 행: 썸네일 + (제목 / 채널·길이·조회수) + 상태 + 받기 버튼.
    private void CreateResultRow(Track track, int index)
    {
        GameObject root = CreatePanel("Row_" + index, resultsContent, PanelBg2);
        Layout(root, minH: 56f, prefH: 56f);
        HorizontalLayoutGroup row = AddRow(root, 8f, 6, 6);
        row.childAlignment = TextAnchor.MiddleLeft;

        // 썸네일 (16:9)
        GameObject thumbGo = CreateUIObject("Thumbnail", root.transform);
        RawImage thumb = thumbGo.AddComponent<RawImage>();
        thumb.color = InputBg; // 로드 전 배경
        Layout(thumbGo, prefW: 80f, minW: 80f, prefH: 45f, minH: 45f);

        // 정보 (제목 + 메타)
        GameObject info = CreateUIObject("Info", root.transform);
        Layout(info, flexW: 1f);
        VerticalLayoutGroup infoLayout = info.AddComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 2f;
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;
        infoLayout.childAlignment = TextAnchor.MiddleLeft;

        // 제목/메타는 Truncate로 자른다 (SUIT-Bold에 '…' 글리프가 없어 Ellipsis는 □로 깨짐).
        // 전체 내용은 hover 툴팁이 보여준다.
        TextMeshProUGUI title = CreateText("Title", info.transform, track.title ?? string.Empty, 13, TextWhite, TextAlignmentOptions.MidlineLeft);
        title.overflowMode = TextOverflowModes.Truncate;
        title.enableWordWrapping = false;
        Layout(title.gameObject, minH: 18f, prefH: 18f);

        string meta = BuildMeta(track);
        TextMeshProUGUI metaLabel = CreateText("Meta", info.transform, meta, 11, TextMuted, TextAlignmentOptions.MidlineLeft);
        metaLabel.overflowMode = TextOverflowModes.Truncate;
        metaLabel.enableWordWrapping = false;
        Layout(metaLabel.gameObject, minH: 14f, prefH: 14f);

        // 받기 버튼 — 별도 상태 칸 대신 버튼 라벨에 진행률/완료를 표시해 제목 공간을 넓힌다.
        Button download = CreateButton("DownloadButton", root.transform, "받기", AccentBlue, 12);
        Layout(download.gameObject, prefW: 52f, minW: 52f, prefH: 26f, minH: 26f);

        Track captured = track;
        download.onClick.AddListener(() =>
        {
            SetInteractable(download, false);
            SetButtonLabel(download, "요청…");
            DownloadRequested?.Invoke(captured, s =>
            {
                SetButtonLabel(download, s);
                if (s == "실패" || s == "서버없음")
                {
                    SetInteractable(download, true); // 실패 시 재시도 허용
                }
            });
        });

        // hover 상세 툴팁 (전체 제목 + 채널/길이/조회수)
        JukeboxDownloaderRowHover hover = root.AddComponent<JukeboxDownloaderRowHover>();
        hover.title = track.title ?? string.Empty;
        hover.body = BuildTooltipBody(track);
        hover.font = ResolveFont();

        // 썸네일 비동기 로드 (URL만 있으면 Unity가 직접 받아온다; 프록시 불필요)
        string thumbUrl = !string.IsNullOrEmpty(track.thumbnailHq) ? track.thumbnailHq : track.thumbnail;
        if (!string.IsNullOrEmpty(thumbUrl) && isActiveAndEnabled)
        {
            StartCoroutine(LoadThumbnail(thumbUrl, thumb));
        }
    }

    private static string BuildMeta(Track t)
    {
        List<string> parts = new List<string>();
        if (!string.IsNullOrEmpty(t.channel)) parts.Add(t.channel);
        if (!string.IsNullOrEmpty(t.durationStr)) parts.Add(t.durationStr);
        if (!string.IsNullOrEmpty(t.viewsStr)) parts.Add(t.viewsStr);
        return string.Join("  ·  ", parts);
    }

    // hover 툴팁 본문: 채널 / 길이·조회수를 줄 단위로.
    private static string BuildTooltipBody(Track t)
    {
        List<string> lines = new List<string>();
        if (!string.IsNullOrEmpty(t.channel)) lines.Add(t.channel);
        List<string> stats = new List<string>();
        if (!string.IsNullOrEmpty(t.durationStr)) stats.Add(t.durationStr);
        if (!string.IsNullOrEmpty(t.viewsStr)) stats.Add(t.viewsStr);
        if (stats.Count > 0) lines.Add(string.Join("  ·  ", stats));
        return string.Join("\n", lines);
    }

    private IEnumerator LoadThumbnail(string url, RawImage target)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success && target != null)
            {
                target.texture = DownloadHandlerTexture.GetContent(req);
                target.color = Color.white;
            }
        }
    }

    private void RefreshEmptyState()
    {
        SetActive(emptyLabel, results.Count == 0);
    }

    // ── 베이크된 프리팹 연결 ───────────────────────────────────────────────────
    private bool HasBakedHierarchy()
    {
        return FindDeepChild(transform, "ResultsContent") != null;
    }

    private void BindExisting()
    {
        built = true;

        searchInput = FindComponent<TMP_InputField>("SearchInput");
        searchButton = FindComponent<Button>("SearchButton");
        sortDropdown = FindComponent<TMP_Dropdown>("SortDropdown");
        periodDropdown = FindComponent<TMP_Dropdown>("PeriodDropdown");
        durationDropdown = FindComponent<TMP_Dropdown>("DurationDropdown");
        limitDropdown = FindComponent<TMP_Dropdown>("LimitDropdown");
        resultsContent = FindComponent<RectTransform>("ResultsContent");
        resultsRect = FindComponent<RectTransform>("Results");
        Transform empty = FindDeepChild(transform, "EmptyLabel");
        emptyLabel = empty != null ? empty.gameObject : null;

        Transform filter = FindDeepChild(transform, "FilterRow");
        filterRow = filter != null ? filter.gameObject : null;
        filterVisible = filterRow == null || filterRow.activeSelf;
        Transform toggle = FindDeepChild(transform, "FilterToggleButton");
        filterToggleLabel = toggle != null ? toggle.GetComponentInChildren<TextMeshProUGUI>(true) : null;

        TextMeshProUGUI titleText = FindComponent<TextMeshProUGUI>("HeaderTitleText");
        if (titleText != null) boundFont = titleText.font;

        if (searchInput != null)
        {
            searchInput.onSubmit.RemoveListener(OnSearchSubmit);
            searchInput.onSubmit.AddListener(OnSearchSubmit);
        }
        BindButton("SearchButton", TriggerSearch);
        BindButton("CloseButton", Hide);
        BindButton("FilterToggleButton", ToggleFilterRow);
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
    // 에디터 베이크 전용: 전체 UI 계층을 코드로 생성해 프리팹에 굽는다.
    public void EditorBuild(Sprite roundedSpriteAsset, TMP_FontAsset fontAsset)
    {
        if (roundedSpriteAsset != null) panelSprite = roundedSpriteAsset;
        if (fontAsset != null) font = fontAsset;
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

        // SkillView.prefab처럼 루트/헤더에 레이아웃 그룹을 쓰지 않는다.
        // 모든 정적 요소는 고정 앵커로 배치 (동적 리스트 행 내부만 레이아웃 그룹 사용).
        BuildHandler(transform);
        BuildHeader(transform);
        BuildSearchRow(transform);
        BuildFilterRow(transform);
        BuildResults(transform);

        // 필터는 접힌 상태가 기본값. 베이크 시 이 상태(FilterRow 비활성 + Results 확장)로 구워져
        // 런타임에 위치 재조정이 필요 없다.
        SetFilterVisible(false);
    }

    // JukeboxView.prefab의 Handler: 패널 전체를 덮는 투명 드래그 표면.
    // 첫 자식이라 다른 컨트롤 뒤에 깔리고, 빈 곳을 잡으면 창이 끌린다.
    private void BuildHandler(Transform parent)
    {
        GameObject handler = CreateUIObject("Handler", parent);
        SetStretch(handler, Vector4.zero);
        Image img = handler.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        handler.AddComponent<DragUIHandler>();
    }

    // SkillView.prefab 헤더 방식: 고정 높이의 full-width 바를 상단에 앵커, 자식도 명시 앵커.
    private void BuildHeader(Transform parent)
    {
        GameObject header = CreatePanel("Header", parent, HeaderBg);
        TopStretch(header, 0f, 0f, 0f, HeaderHeight);

        TextMeshProUGUI title = CreateText("HeaderTitleText", header.transform, "Jukebox Downloader", 15, TextWhite, TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(12f, 0f);
        titleRect.offsetMax = new Vector2(-38f, 0f);

        Button close = CreateButton("CloseButton", header.transform, "×", HeaderBg, 18);
        AnchorRight(close.gameObject, -6f, 0f, 24f, 24f);
        close.onClick.AddListener(Hide);
    }

    private void BuildSearchRow(Transform parent)
    {
        float top = HeaderHeight + 8f;

        // 필터 행 표시/숨김 토글 (정사각형, 검색 행 왼쪽). 라벨은 JukeboxView의 접기 표기(^/v)를 따른다.
        Button filterToggle = CreateButton("FilterToggleButton", parent, "^", PanelBg2, 14);
        RectTransform toggleRect = filterToggle.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 1f);
        toggleRect.anchorMax = new Vector2(0f, 1f);
        toggleRect.pivot = new Vector2(0f, 1f);
        toggleRect.anchoredPosition = new Vector2(Margin, -top);
        toggleRect.sizeDelta = new Vector2(SearchRowHeight, SearchRowHeight);
        filterToggleLabel = filterToggle.GetComponentInChildren<TextMeshProUGUI>();
        filterToggle.onClick.AddListener(ToggleFilterRow);

        searchInput = CreateSingleLineInput("SearchInput", parent, "검색어를 입력하고 Enter");
        TopStretch(searchInput.gameObject, Margin + SearchRowHeight + RowSpacing, Margin + SearchButtonWidth + RowSpacing, top, SearchRowHeight);
        searchInput.onSubmit.AddListener(OnSearchSubmit);

        searchButton = CreateButton("SearchButton", parent, "검색", AccentBlue, 14);
        RectTransform buttonRect = searchButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-Margin, -top);
        buttonRect.sizeDelta = new Vector2(SearchButtonWidth, SearchRowHeight);
        searchButton.onClick.AddListener(TriggerSearch);
    }

    private void BuildFilterRow(Transform parent)
    {
        float top = HeaderHeight + 8f + SearchRowHeight + RowSpacing;

        // 컨테이너 하나로 묶어 토글 시 통째로 켜고 끈다.
        filterRow = CreateUIObject("FilterRow", parent);
        TopStretch(filterRow, Margin, Margin, top, FilterRowHeight);

        // 서버는 duration을 short|medium|long 카테고리로만 받는다 (정확한 시간 필터 불가).
        sortDropdown = CreateDropdown("SortDropdown", filterRow.transform);
        AnchorFraction(sortDropdown.gameObject, 0f, 0.30f, 0f, 3f, 0f, FilterRowHeight);
        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<string> { "관련성순", "조회수순", "최신순" });

        periodDropdown = CreateDropdown("PeriodDropdown", filterRow.transform);
        AnchorFraction(periodDropdown.gameObject, 0.30f, 0.58f, 3f, 3f, 0f, FilterRowHeight);
        periodDropdown.ClearOptions();
        periodDropdown.AddOptions(new List<string> { "전체 기간", "오늘", "이번주", "이번달", "올해" });

        durationDropdown = CreateDropdown("DurationDropdown", filterRow.transform);
        AnchorFraction(durationDropdown.gameObject, 0.58f, 0.82f, 3f, 3f, 0f, FilterRowHeight);
        durationDropdown.ClearOptions();
        durationDropdown.AddOptions(new List<string> { "길이 전체", "짧음", "중간", "김" });

        // 검색 개수 (서버 limit 1~30 지원 확인됨 — 5/10/20만 노출)
        limitDropdown = CreateDropdown("LimitDropdown", filterRow.transform);
        AnchorFraction(limitDropdown.gameObject, 0.82f, 1f, 3f, 0f, 0f, FilterRowHeight);
        limitDropdown.ClearOptions();
        limitDropdown.AddOptions(new List<string> { "5", "10", "20" });
        limitDropdown.SetValueWithoutNotify(1); // 기본 10 (서버 기본값과 동일)
    }

    // 필터 행 접기/펴기: 결과 영역 top도 함께 당긴다.
    private void ToggleFilterRow()
    {
        SetFilterVisible(!filterVisible);
    }

    private void SetFilterVisible(bool visible)
    {
        filterVisible = visible;
        SetActive(filterRow, visible);
        if (resultsRect != null)
        {
            float top = HeaderHeight + 8f + SearchRowHeight + RowSpacing
                        + (visible ? FilterRowHeight + RowSpacing : 0f);
            resultsRect.offsetMax = new Vector2(-Margin, -top);
        }
        if (filterToggleLabel != null)
        {
            filterToggleLabel.text = visible ? "^" : "v";
        }
    }

    // 결과 = 우측 세로 스크롤바를 가진 세로 리스트. 필터 행 아래부터 패널 바닥까지 스트레치.
    private void BuildResults(Transform parent)
    {
        float top = HeaderHeight + 8f + SearchRowHeight + RowSpacing + FilterRowHeight + RowSpacing;

        GameObject area = CreatePanel("Results", parent, PanelBg);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = Vector2.one;
        areaRect.pivot = new Vector2(0.5f, 0.5f);
        areaRect.offsetMin = new Vector2(Margin, Margin);
        areaRect.offsetMax = new Vector2(-Margin, -top);
        resultsRect = areaRect;

        ScrollRect scroll = area.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        GameObject viewport = CreateUIObject("Viewport", area.transform);
        SetStretch(viewport, new Vector4(8f, 8f, 16f, 8f)); // 우측 16: 스크롤바 공간
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUIObject("ResultsContent", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup listLayout = content.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 6f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateVerticalScrollbar("ResultsScrollbar", area.transform);

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.verticalScrollbar = scrollbar;
        resultsContent = contentRect;

        // 빈 상태 안내 (결과 없을 때만 표시). 결과 영역 중앙에 오버레이.
        TextMeshProUGUI empty = CreateText("EmptyLabel", area.transform, "검색 결과가 여기에 표시됩니다.", 13, TextMuted, TextAlignmentOptions.Center);
        SetStretch(empty.gameObject, new Vector4(12f, 12f, 12f, 12f));
        emptyLabel = empty.gameObject;
    }

    // ── 팩토리 헬퍼 (SkillView와 동일) ─────────────────────────────────────────
    private TMP_InputField CreateSingleLineInput(string name, Transform parent, string placeholderText)
    {
        GameObject area = CreatePanel(name, parent, InputBg);
        TMP_InputField input = area.AddComponent<TMP_InputField>();

        GameObject textArea = CreateUIObject("Text Area", area.transform);
        SetStretch(textArea, new Vector4(10f, 4f, 10f, 4f));
        textArea.AddComponent<RectMask2D>();
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();

        TextMeshProUGUI placeholder = CreateText("Placeholder", textArea.transform, placeholderText, 14, TextMuted, TextAlignmentOptions.MidlineLeft);
        SetStretch(placeholder.gameObject, Vector4.zero);

        TextMeshProUGUI text = CreateText("Text", textArea.transform, string.Empty, 14, TextWhite, TextAlignmentOptions.MidlineLeft);
        SetStretch(text.gameObject, Vector4.zero);

        input.textViewport = textAreaRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.richText = false;
        input.targetGraphic = area.GetComponent<Image>();
        return input;
    }

    private TMP_Dropdown CreateDropdown(string name, Transform parent)
    {
        GameObject root = CreatePanel(name, parent, PanelBg2);
        TMP_Dropdown dropdown = root.AddComponent<TMP_Dropdown>();

        TextMeshProUGUI label = CreateText("Label", root.transform, string.Empty, 13, TextWhite, TextAlignmentOptions.MidlineLeft);
        SetStretch(label.gameObject, new Vector4(8f, 2f, 22f, 2f));

        GameObject arrow = CreatePanel("Arrow", root.transform, TextMuted);
        RectTransform arrowRect = arrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-10f, 0f);
        arrowRect.sizeDelta = new Vector2(12f, 12f);

        GameObject template = CreatePanel("Template", root.transform, PanelBg);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, 2f);
        templateRect.sizeDelta = new Vector2(0f, 150f);

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

        TextMeshProUGUI itemLabel = CreateText("Item Label", item.transform, "Option", 13, TextWhite, TextAlignmentOptions.MidlineLeft);
        SetStretch(itemLabel.gameObject, new Vector4(24f, 1f, 4f, 1f));
        itemLabel.textWrappingMode = TextWrappingModes.NoWrap; // 좁은 드롭다운에서 "20"이 세로로 꺾이는 것 방지
        itemLabel.overflowMode = TextOverflowModes.Overflow;

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
        scrollbar.direction = Scrollbar.Direction.TopToBottom;

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
        if (font != null) return font;
        if (boundFont != null) return boundFont;
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
        rect.offsetMin = new Vector2(padding.x, padding.y);
        rect.offsetMax = new Vector2(-padding.z, -padding.w);
    }

    // 상단 기준 가로 스트레치 바: 패널 top에서 top만큼 내려온 위치에 height 높이로 배치.
    private static RectTransform TopStretch(GameObject go, float left, float right, float top, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -(top + height));
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    // 가로 구간(xMin~xMax 비율)에 상단 고정 배치. 필터 드롭다운 3분할용.
    private static void AnchorFraction(GameObject go, float xMin, float xMax, float left, float right, float top, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, 1f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -(top + height));
        rect.offsetMax = new Vector2(-right, -top);
    }

    // 우측 중앙 기준 고정 크기 배치 (헤더의 개수 라벨/닫기 버튼).
    private static void AnchorRight(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
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

    private static void SetInteractable(Button button, bool on)
    {
        if (button != null) button.interactable = on;
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        TextMeshProUGUI t = button.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.text = label;
    }

    private static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }

    private static void SetText(TextMeshProUGUI t, string v)
    {
        if (t != null) t.text = v;
    }
}
