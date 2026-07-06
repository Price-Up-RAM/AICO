using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Jukebox 메인 UI(고전 플레이어). BGM 재생/메타데이터는 MRJukebox(싱글톤)에 위임하고
/// 이 뷰는 표시/조작만 한다.
///  - 카테고리 = MRJukebox 트랙의 태그(+custom). 카테고리 선택 시 그 태그의 트랙 목록 표시
///  - 트랙 클릭 → MRJukebox.PlayTrack(index)
///  - 진행 게이지/시간/%, now-playing, 재생/중지/정지, 마스터 볼륨 모두 MRJukebox 상태/제어 호출
///  - custom: StreamingAssets/bgm 의 wav/mp3/ogg 를 로드해 MRJukebox.AddTrack(tag="custom"). 없으면 비움
///  - SFX는 "SFX" 버튼으로 JukeboxEnvironmentView(별도) 팝업
/// </summary>
public class JukeboxView : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Vector2 panelSize = new Vector2(420f, 480f);
    [SerializeField] private Sprite panelSprite;
    [Tooltip("재생 게이지 핸들용 둥근 노브 스프라이트(베이크 시 빌트인 Knob 지정).")]
    [SerializeField] private Sprite knobSprite;

    [Header("Wiring")]
    [SerializeField] private GameObject environmentPrefab;

    [Header("Mode Icons (등록 시 아이콘 표시, 미등록 시 텍스트 폴백)")]
    [SerializeField] private Sprite shuffleSequentialSprite; // 순차재생
    [SerializeField] private Sprite shuffleRandomSprite;     // 랜덤재생
    [SerializeField] private Sprite repeatNoneSprite;        // 반복없음
    [SerializeField] private Sprite repeatAllSprite;         // 전곡반복
    [SerializeField] private Sprite repeatOneSprite;         // 한곡반복

    private bool built;
    private bool runtimeReady;
    private bool listExpanded = true;
    private bool shuffleRandom;                      // false=순차재생, true=랜덤재생
    private RepeatMode repeatMode = RepeatMode.All;  // 한곡반복 / 전곡반복 / 반복없음
    private int currentTagIndex;
    private int lastShownIndex = -2;

    private enum RepeatMode { None, All, One }        // 반복없음 → 전곡반복 → 한곡반복

    private MRJukebox mr;

    private readonly List<string> tags = new List<string>();
    private readonly Dictionary<int, Image> rowImageByTrack = new Dictionary<int, Image>();

    private TextMeshProUGUI nowPlayingText;
    private TextMeshProUGUI timeText;
    private TextMeshProUGUI percentText;
    private TextMeshProUGUI collapseLabel;
    private TextMeshProUGUI shuffleLabel;
    private TextMeshProUGUI repeatLabel;
    private Image shuffleIcon;
    private Image repeatIcon;
    private Slider progressSlider;
    private Slider masterSlider;
    private TMP_Dropdown categoryDropdown;
    private GameObject listPanel;
    private RectTransform listContent;

    private JukeboxEnvironmentView envInstance;

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
    }

    private void Start()
    {
        mr = MRJukebox.Instance != null ? MRJukebox.Instance : FindObjectOfType<MRJukebox>();
        if (mr == null)
        {
            Debug.LogWarning("[Jukebox] MRJukebox를 찾지 못했습니다. BGM 목록이 비어있습니다.");
        }

        WireStaticControls();
        RefreshCategories();
        RebuildList(CurrentTag());
        StartCoroutine(LoadCustom());
        runtimeReady = true;
    }

    private void Update()
    {
        if (!runtimeReady)
        {
            return;
        }

        if (progressSlider != null)
        {
            if (mr != null && mr.CurrentClip != null && mr.ClipLength > 0f)
            {
                float p = Mathf.Clamp01(mr.CurrentTime / mr.ClipLength);
                progressSlider.SetValueWithoutNotify(p); // 재생 위치 반영(시킹 콜백 방지)
                if (percentText != null) percentText.text = Mathf.RoundToInt(p * 100f) + "%";
                if (timeText != null) timeText.text = Fmt(mr.CurrentTime) + " / " + Fmt(mr.ClipLength);
            }
            else
            {
                progressSlider.SetValueWithoutNotify(0f);
                if (percentText != null) percentText.text = "0%";
                if (timeText != null) timeText.text = "00:00 / 00:00";
            }
        }

        // MRJukebox가 자체 모드로 곡을 바꿨을 수도 있으니 동기화.
        int idx = mr != null ? mr.CurrentIndex : -1;
        if (idx != lastShownIndex)
        {
            lastShownIndex = idx;
            UpdateNowPlaying();
            UpdateHighlight();
        }
    }

    private static string Fmt(float seconds)
    {
        int t = Mathf.FloorToInt(Mathf.Max(0f, seconds));
        return (t / 60).ToString("00") + ":" + (t % 60).ToString("00");
    }

    // ── 공개 API ─────────────────────────────────────────────────────────────
    public void Show() { gameObject.SetActive(true); }
    public void Hide() { gameObject.SetActive(false); }

    // 데모/외부 제어: 트랙 이름으로 재생/정지.
    public void PlayTrack(string trackName, bool on = true)
    {
        if (mr == null)
        {
            mr = MRJukebox.Instance != null ? MRJukebox.Instance : FindObjectOfType<MRJukebox>();
        }
        if (mr == null) return;

        if (!on)
        {
            mr.StopPlayback();
            return;
        }
        if (tags.Count == 0) RefreshCategories(); // Start 순서와 무관하게 보장

        int idx = IndexOfTrackName(trackName);
        if (idx >= 0)
        {
            // 해당 곡의 첫 태그 카테고리를 열고 그 태그를 scope로(같은 태그 내 진행).
            JukeboxTrack t = mr.Tracks[idx];
            string tag = (t.tags != null && t.tags.Count > 0) ? t.tags[0] : null;
            if (!string.IsNullOrEmpty(tag))
            {
                SelectCategory(tag); // RebuildList가 scope도 이 목록으로 설정
            }
            mr.PlayTrack(idx);
        }
    }

    // 드롭다운/목록을 지정 태그 카테고리로 전환.
    private void SelectCategory(string tag)
    {
        int ti = tags.IndexOf(tag);
        if (ti < 0) return;
        currentTagIndex = ti;
        if (categoryDropdown != null)
        {
            categoryDropdown.SetValueWithoutNotify(ti);
            categoryDropdown.RefreshShownValue();
        }
        RebuildList(tag);
    }

    public void ToggleList()
    {
        listExpanded = !listExpanded;
        if (listPanel != null) listPanel.SetActive(listExpanded);
        if (collapseLabel != null) collapseLabel.text = listExpanded ? "^" : "v";
    }

    public void ToggleEnvironment()
    {
        if (envInstance == null)
        {
            if (environmentPrefab == null)
            {
                Debug.LogWarning("[Jukebox] environmentPrefab 미설정");
                return;
            }
            Transform parent = transform.parent != null ? transform.parent : transform;
            GameObject go = Instantiate(environmentPrefab, parent);
            envInstance = go.GetComponent<JukeboxEnvironmentView>();
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(panelSize.x * 0.5f + 240f, 0f);
            }
            envInstance.Show();
            return;
        }
        if (envInstance.gameObject.activeSelf) envInstance.Hide();
        else envInstance.Show();
    }

    // ── MRJukebox 연동 헬퍼 ─────────────────────────────────────────────────────
    private int IndexOfTrackName(string trackName)
    {
        if (mr == null || string.IsNullOrEmpty(trackName)) return -1;
        IReadOnlyList<JukeboxTrack> list = mr.Tracks;
        for (int i = 0; i < list.Count; i++)
        {
            string n = !string.IsNullOrEmpty(list[i].trackName) ? list[i].trackName : (list[i].clip != null ? list[i].clip.name : null);
            if (n == trackName) return i;
        }
        return -1;
    }

    private string CurrentTag()
    {
        return (currentTagIndex >= 0 && currentTagIndex < tags.Count) ? tags[currentTagIndex] : null;
    }

    private void RefreshCategories()
    {
        tags.Clear();
        if (mr != null)
        {
            foreach (JukeboxTrack t in mr.Tracks)
            {
                if (t.tags == null) continue;
                foreach (string tag in t.tags)
                {
                    if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag))
                    {
                        tags.Add(tag);
                    }
                }
            }
        }

        if (categoryDropdown != null)
        {
            int keep = Mathf.Clamp(currentTagIndex, 0, Mathf.Max(0, tags.Count - 1));
            categoryDropdown.onValueChanged.RemoveListener(OnCategoryChanged);
            categoryDropdown.ClearOptions();
            categoryDropdown.AddOptions(tags.Count > 0 ? tags : new List<string> { "(없음)" });
            categoryDropdown.SetValueWithoutNotify(keep);
            categoryDropdown.RefreshShownValue();
            categoryDropdown.onValueChanged.AddListener(OnCategoryChanged);
            currentTagIndex = keep;
        }
    }

    private void OnCategoryChanged(int index)
    {
        currentTagIndex = index;
        RebuildList(CurrentTag());
    }

    private List<int> TrackIndicesForTag(string tag)
    {
        List<int> result = new List<int>();
        if (mr == null || string.IsNullOrEmpty(tag)) return result;
        IReadOnlyList<JukeboxTrack> list = mr.Tracks;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].tags != null && list[i].tags.Contains(tag))
            {
                result.Add(i);
            }
        }
        return result;
    }

    private void RebuildList(string tag)
    {
        rowImageByTrack.Clear();
        if (listContent == null) return;
        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Destroy(listContent.GetChild(i).gameObject);
        }
        if (mr == null) return;

        // 반복/다음곡/랜덤 범위 = 지금 보여주는 목록(이 태그)으로 고정.
        mr.SetScope(TrackIndicesForTag(tag));

        foreach (int idx in TrackIndicesForTag(tag))
        {
            JukeboxTrack t = mr.Tracks[idx];
            string name = !string.IsNullOrEmpty(t.trackName) ? t.trackName : (t.clip != null ? t.clip.name : "track");
            GameObject row = BuildTrackRow(name);
            Image img = row.GetComponent<Image>();
            if (img != null) rowImageByTrack[idx] = img;
            Button btn = row.GetComponent<Button>();
            int captured = idx;
            if (btn != null)
            {
                btn.interactable = t.clip != null;
                btn.onClick.AddListener(() =>
                {
                    if (mr != null) mr.PlayTrack(captured); // scope는 이미 이 목록으로 설정됨
                });
            }
        }
        UpdateHighlight();
    }

    private void UpdateNowPlaying()
    {
        if (nowPlayingText == null) return;
        string n = mr != null ? mr.CurrentTrackName : null;
        nowPlayingText.text = string.IsNullOrEmpty(n) ? string.Empty : n;
    }

    private void UpdateHighlight()
    {
        int cur = mr != null ? mr.CurrentIndex : -1;
        foreach (KeyValuePair<int, Image> kv in rowImageByTrack)
        {
            if (kv.Value != null)
            {
                kv.Value.color = kv.Key == cur ? JukeboxUi.AccentBlue : JukeboxUi.RowBg;
            }
        }
    }

    private void OnMasterVolume(float v)
    {
        if (mr != null) mr.Volume = v;
    }

    private void OnPlay()
    {
        if (mr == null) return;
        if (mr.CurrentIndex < 0)
        {
            List<int> idxs = TrackIndicesForTag(CurrentTag());
            if (idxs.Count > 0)
            {
                mr.PlayTrack(idxs[0]); // scope는 현재 목록으로 이미 설정됨
            }
        }
        else
        {
            mr.Resume();
        }
    }

    private void OnPause() { if (mr != null) mr.Pause(); }
    private void OnStop() { if (mr != null) mr.StopPlayback(); }

    // 게이지 드래그 → 재생 위치 이동
    private void OnProgressChanged(float v)
    {
        if (mr != null) mr.SeekNormalized(v);
    }

    // 순차재생 ↔ 랜덤재생 토글
    private void OnShuffleToggle()
    {
        shuffleRandom = !shuffleRandom;
        ApplyPlayMode();
        UpdateModeDisplay();
    }

    // 한곡반복 → 전곡반복 → 반복없음 순환
    private void OnRepeatCycle()
    {
        repeatMode = (RepeatMode)(((int)repeatMode + 1) % 3);
        ApplyPlayMode();
        UpdateModeDisplay();
    }

    // 순차/랜덤 × 반복모드 조합을 MRJukebox 재생모드로 변환.
    private void ApplyPlayMode()
    {
        if (mr == null) return;
        if (repeatMode == RepeatMode.One) mr.SetPlayModeLoopOne();       // 한곡반복
        else if (shuffleRandom) mr.SetPlayModeRandom();                  // 랜덤재생
        else if (repeatMode == RepeatMode.All) mr.SetPlayModeLoopAll();  // 순차 + 전곡반복
        else mr.SetPlayModeSequential();                                // 순차 + 반복없음
    }

    // 등록된 스프라이트가 있으면 아이콘, 없으면 텍스트 폴백으로 현재 모드를 표시.
    private void UpdateModeDisplay()
    {
        Sprite shufSpr = shuffleRandom ? shuffleRandomSprite : shuffleSequentialSprite;
        ApplyModeButton(shuffleIcon, shuffleLabel, shufSpr, shuffleRandom ? "랜덤" : "순차");

        Sprite repSpr = repeatMode == RepeatMode.One ? repeatOneSprite
                      : repeatMode == RepeatMode.All ? repeatAllSprite : repeatNoneSprite;
        string repTxt = repeatMode == RepeatMode.One ? "한곡"
                      : repeatMode == RepeatMode.All ? "전곡" : "없음";
        ApplyModeButton(repeatIcon, repeatLabel, repSpr, repTxt);
    }

    private static void ApplyModeButton(Image icon, TextMeshProUGUI label, Sprite sprite, string fallbackText)
    {
        bool hasIcon = sprite != null;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = hasIcon;
        }
        if (label != null)
        {
            label.enabled = !hasIcon;
            if (!hasIcon) label.text = fallbackText;
        }
    }

    // ── custom 로드 (StreamingAssets/bgm) ───────────────────────────────────────
    private IEnumerator LoadCustom()
    {
        if (mr == null) yield break;
        string dir = Path.Combine(Application.streamingAssetsPath, JukeboxCatalog.CustomFolder);
        if (!Directory.Exists(dir)) yield break;

        bool added = false;
        foreach (string full in Directory.GetFiles(dir))
        {
            string ext = Path.GetExtension(full).ToLowerInvariant();
            AudioType type;
            if (ext == ".wav") type = AudioType.WAV;
            else if (ext == ".mp3") type = AudioType.MPEG;
            else if (ext == ".ogg") type = AudioType.OGGVORBIS;
            else continue;

            string url = new Uri(full).AbsoluteUri;
            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                    if (clip != null)
                    {
                        mr.AddTrack(clip, Path.GetFileNameWithoutExtension(full), JukeboxCatalog.CustomTag);
                        added = true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[Jukebox] custom 로드 실패: {full} ({req.error})");
                }
            }
        }

        if (added)
        {
            RefreshCategories();
            RebuildList(CurrentTag());
        }
    }

    // ── 빌드 / 바인드 ──────────────────────────────────────────────────────────
    private void WireStaticControls()
    {
        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveListener(OnMasterVolume);
            masterSlider.SetValueWithoutNotify(mr != null ? mr.Volume : 0.8f);
            masterSlider.onValueChanged.AddListener(OnMasterVolume);
        }
        if (progressSlider != null)
        {
            progressSlider.onValueChanged.RemoveListener(OnProgressChanged);
            progressSlider.SetValueWithoutNotify(0f);
            progressSlider.onValueChanged.AddListener(OnProgressChanged);
        }

        BindButton("CloseButton", Hide);
        BindButton("EnvButton", ToggleEnvironment);
        BindButton("CollapseButton", ToggleList);
        BindButton("PlayButton", OnPlay);
        BindButton("PauseButton", OnPause);
        BindButton("StopButton", OnStop);
        BindButton("ShuffleButton", OnShuffleToggle);
        BindButton("RepeatButton", OnRepeatCycle);

        // 모드 상태 초기화(MRJukebox 현재 모드 기준).
        if (mr != null)
        {
            shuffleRandom = mr.PlayMode == JukeboxPlayMode.Random;
            repeatMode = mr.PlayMode == JukeboxPlayMode.LoopOne ? RepeatMode.One
                       : mr.PlayMode == JukeboxPlayMode.LoopAll ? RepeatMode.All
                       : RepeatMode.None;
        }

        if (collapseLabel != null) collapseLabel.text = listExpanded ? "^" : "v";
        if (listPanel != null) listPanel.SetActive(listExpanded);
        UpdateModeDisplay();
    }

    private bool HasBakedHierarchy()
    {
        return transform.Find("Header") != null;
    }

    private void BindExisting()
    {
        built = true;
        nowPlayingText = FindComp<TextMeshProUGUI>("NowPlaying");
        timeText = FindComp<TextMeshProUGUI>("TimeText");
        percentText = FindComp<TextMeshProUGUI>("PercentText");
        progressSlider = FindComp<Slider>("ProgressSlider");
        masterSlider = FindComp<Slider>("MasterSlider");
        categoryDropdown = FindComp<TMP_Dropdown>("CategoryDropdown");
        Transform lp = FindTransform("ListPanel");
        listPanel = lp != null ? lp.gameObject : null;
        listContent = FindTransform("BgmList") as RectTransform;
        Transform cl = FindTransform("CollapseLabel");
        collapseLabel = cl != null ? cl.GetComponent<TextMeshProUGUI>() : null;
        Transform sl = FindTransform("ShuffleLabel");
        shuffleLabel = sl != null ? sl.GetComponent<TextMeshProUGUI>() : null;
        Transform rl = FindTransform("RepeatLabel");
        repeatLabel = rl != null ? rl.GetComponent<TextMeshProUGUI>() : null;
        shuffleIcon = FindComp<Image>("ShuffleIcon");
        repeatIcon = FindComp<Image>("RepeatIcon");
    }

    private void BindButton(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = FindComp<Button>(name);
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

    private T FindComp<T>(string name) where T : Component
    {
        Transform t = FindTransform(name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private Transform FindTransform(string name) { return FindDeep(transform, name); }

    private static Transform FindDeep(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

#if UNITY_EDITOR
    public void EditorBuild(Sprite sprite, TMP_FontAsset fontAsset, Sprite knob = null)
    {
        panelSprite = sprite;
        font = fontAsset;
        knobSprite = knob;
        Build();
    }

    public void EditorSetEnvironmentPrefab(GameObject prefab)
    {
        environmentPrefab = prefab;
    }
#endif

    private void Build()
    {
        if (built) return;
        built = true;

        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
        rootRect.sizeDelta = panelSize;

        Image rootBg = gameObject.GetComponent<Image>();
        if (rootBg == null) rootBg = gameObject.AddComponent<Image>();
        JukeboxUi.ApplyRounded(rootBg, panelSprite, JukeboxUi.RootBg);

        VerticalLayoutGroup rootLayout = gameObject.GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null) rootLayout = gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(14, 14, 14, 14);
        rootLayout.spacing = 10f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        ContentSizeFitter rootFitter = gameObject.GetComponent<ContentSizeFitter>();
        if (rootFitter == null) rootFitter = gameObject.AddComponent<ContentSizeFitter>();
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildHeader(transform);
        BuildProgressRow(transform);
        BuildTransportRow(transform);
        BuildVolumeRow(transform);
        BuildCategoryRow(transform);
        BuildList(transform);
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = JukeboxUi.Panel("Header", parent, panelSprite, JukeboxUi.HeaderBg);
        JukeboxUi.Layout(header, minH: 44f, prefH: 44f);
        JukeboxUi.Row(header, 8f, padL: 12, padR: 8);

        nowPlayingText = JukeboxUi.Text("NowPlaying", header.transform, string.Empty, 18, JukeboxUi.TextWhite, TextAlignmentOptions.MidlineLeft, font);
        JukeboxUi.Layout(nowPlayingText.gameObject, flexW: 1f);

        Button env = JukeboxUi.MakeButton("EnvButton", header.transform, "SFX", JukeboxUi.ButtonBg, 14, panelSprite, font);
        env.onClick.AddListener(ToggleEnvironment);
        JukeboxUi.Layout(env.gameObject, prefW: 56f, minW: 56f, prefH: 30f, minH: 30f);

        Button close = JukeboxUi.MakeButton("CloseButton", header.transform, "×", JukeboxUi.ButtonBg, 20, panelSprite, font);
        close.onClick.AddListener(Hide);
        JukeboxUi.Layout(close.gameObject, prefW: 32f, minW: 32f, prefH: 30f, minH: 30f);
    }

    private void BuildProgressRow(Transform parent)
    {
        GameObject row = JukeboxUi.Obj("ProgressRow", parent);
        JukeboxUi.Layout(row, minH: 24f, prefH: 24f);
        HorizontalLayoutGroup hl = JukeboxUi.Row(row, 8f, padL: 4, padR: 4);
        hl.childAlignment = TextAnchor.MiddleCenter;

        // 드래그로 위치 이동 가능한 재생 게이지(오렌지 fill + 둥근 핸들).
        progressSlider = JukeboxUi.MakeSlider("ProgressSlider", row.transform, panelSprite);
        JukeboxUi.Layout(progressSlider.gameObject, flexW: 1f, minW: 120f, prefH: 16f, minH: 16f);
        if (progressSlider.fillRect != null)
        {
            Image fillImg = progressSlider.fillRect.GetComponent<Image>();
            if (fillImg != null) fillImg.color = JukeboxUi.Orange;
        }
        if (progressSlider.handleRect != null)
        {
            Image handleImg = progressSlider.handleRect.GetComponent<Image>();
            if (handleImg != null)
            {
                if (knobSprite != null) handleImg.sprite = knobSprite; // 둥근 노브
                handleImg.type = Image.Type.Simple;
                handleImg.color = JukeboxUi.Orange;
                progressSlider.handleRect.sizeDelta = new Vector2(16f, 16f);
            }
        }

        timeText = JukeboxUi.Text("TimeText", row.transform, "00:00 / 00:00", 12, JukeboxUi.TextMuted, TextAlignmentOptions.MidlineRight, font);
        JukeboxUi.Layout(timeText.gameObject, prefW: 86f, minW: 86f);

        percentText = JukeboxUi.Text("PercentText", row.transform, "0%", 12, JukeboxUi.TextWhite, TextAlignmentOptions.MidlineRight, font);
        JukeboxUi.Layout(percentText.gameObject, prefW: 40f, minW: 40f);
    }

    private void BuildTransportRow(Transform parent)
    {
        GameObject row = JukeboxUi.Panel("TransportRow", parent, panelSprite, JukeboxUi.PanelBg);
        JukeboxUi.Layout(row, minH: 40f, prefH: 40f);
        HorizontalLayoutGroup hl = JukeboxUi.Row(row, 6f, padL: 8, padR: 8);
        hl.childAlignment = TextAnchor.MiddleRight;
        hl.childForceExpandHeight = true;

        // 좌: 재생 컨트롤
        GameObject left = JukeboxUi.Obj("TransportLeft", row.transform);
        HorizontalLayoutGroup leftHl = JukeboxUi.Row(left, 6f);
        leftHl.childForceExpandHeight = true;
        JukeboxUi.Layout(left, flexW: 1f, minH: 30f);

        Button play = JukeboxUi.MakeButton("PlayButton", left.transform, "재생", JukeboxUi.ButtonBg, 14, panelSprite, font);
        play.onClick.AddListener(OnPlay);
        JukeboxUi.Layout(play.gameObject, flexW: 1f, minW: 44f);

        Button pause = JukeboxUi.MakeButton("PauseButton", left.transform, "중지", JukeboxUi.ButtonBg, 14, panelSprite, font);
        pause.onClick.AddListener(OnPause);
        JukeboxUi.Layout(pause.gameObject, flexW: 1f, minW: 44f);

        Button stop = JukeboxUi.MakeButton("StopButton", left.transform, "정지", JukeboxUi.ButtonBg, 14, panelSprite, font);
        stop.onClick.AddListener(OnStop);
        JukeboxUi.Layout(stop.gameObject, flexW: 1f, minW: 44f);

        // 우: 순차/랜덤, 반복 모드
        GameObject right = JukeboxUi.Obj("TransportRight", row.transform);
        HorizontalLayoutGroup rightHl = JukeboxUi.Row(right, 6f);
        rightHl.childAlignment = TextAnchor.MiddleRight;
        rightHl.childForceExpandHeight = true;
        JukeboxUi.Layout(right, minH: 30f);

        Button shuffle = JukeboxUi.MakeButton("ShuffleButton", right.transform, "순차재생", JukeboxUi.ButtonBg, 13, panelSprite, font);
        shuffle.onClick.AddListener(OnShuffleToggle);
        JukeboxUi.Layout(shuffle.gameObject, prefW: 86f, minW: 78f);
        shuffleLabel = shuffle.GetComponentInChildren<TextMeshProUGUI>();
        if (shuffleLabel != null) shuffleLabel.gameObject.name = "ShuffleLabel";

        Button repeat = JukeboxUi.MakeButton("RepeatButton", right.transform, "전곡반복", JukeboxUi.ButtonBg, 13, panelSprite, font);
        repeat.onClick.AddListener(OnRepeatCycle);
        JukeboxUi.Layout(repeat.gameObject, prefW: 86f, minW: 78f);
        repeatLabel = repeat.GetComponentInChildren<TextMeshProUGUI>();
        if (repeatLabel != null) repeatLabel.gameObject.name = "RepeatLabel";
    }

    private void BuildVolumeRow(Transform parent)
    {
        GameObject row = JukeboxUi.Obj("VolumeRow", parent);
        JukeboxUi.Layout(row, minH: 26f, prefH: 26f);
        JukeboxUi.Row(row, 8f, padL: 4, padR: 4);

        TextMeshProUGUI label = JukeboxUi.Text("VolLabel", row.transform, "VOL", 12, JukeboxUi.TextMuted, TextAlignmentOptions.MidlineLeft, font);
        JukeboxUi.Layout(label.gameObject, prefW: 36f, minW: 36f);

        masterSlider = JukeboxUi.MakeSlider("MasterSlider", row.transform, panelSprite);
        JukeboxUi.Layout(masterSlider.gameObject, flexW: 1f, minW: 120f);
    }

    private void BuildCategoryRow(Transform parent)
    {
        GameObject row = JukeboxUi.Obj("CategoryRow", parent);
        JukeboxUi.Layout(row, minH: 44f, prefH: 44f);
        HorizontalLayoutGroup hl = JukeboxUi.Row(row, 8f, padL: 4, padR: 4);
        hl.childForceExpandHeight = true;

        categoryDropdown = JukeboxUi.MakeDropdown("CategoryDropdown", row.transform, panelSprite, font);
        JukeboxUi.Layout(categoryDropdown.gameObject, prefW: 130f, minW: 130f);

        GameObject spacer = JukeboxUi.Obj("Spacer", row.transform);
        JukeboxUi.Layout(spacer, flexW: 1f);

        Button collapse = JukeboxUi.MakeButton("CollapseButton", row.transform, "^", JukeboxUi.ButtonBg, 16, panelSprite, font);
        collapse.onClick.AddListener(ToggleList);
        JukeboxUi.Layout(collapse.gameObject, prefW: 40f, minW: 40f);
        collapseLabel = collapse.GetComponentInChildren<TextMeshProUGUI>();
        if (collapseLabel != null) collapseLabel.gameObject.name = "CollapseLabel";
    }

    private void BuildList(Transform parent)
    {
        GameObject panel = JukeboxUi.Panel("ListPanel", parent, panelSprite, JukeboxUi.PanelBg);
        JukeboxUi.Layout(panel, minH: 120f, prefH: 260f);

        ScrollRect scroll = panel.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        GameObject viewport = JukeboxUi.Obj("Viewport", panel.transform);
        JukeboxUi.Stretch(viewport, new Vector4(8f, 8f, 8f, 8f));
        viewport.AddComponent<RectMask2D>();

        GameObject content = JukeboxUi.Obj("BgmList", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        JukeboxUi.Column(content, 3f);
        AddVerticalFitter(content);

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;

        listContent = contentRect;
        listPanel = panel;
        // 행은 런타임에 MRJukebox 태그 기준으로 채운다(RebuildList).
    }

    private GameObject BuildTrackRow(string display)
    {
        GameObject row = JukeboxUi.Panel("BgmRow", listContent, panelSprite, JukeboxUi.RowBg);
        JukeboxUi.Row(row, 6f, padL: 10, padR: 10);
        JukeboxUi.Layout(row, minH: 28f, prefH: 28f);

        Button btn = row.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = row.GetComponent<Image>();

        TextMeshProUGUI label = JukeboxUi.Text("Label", row.transform, display, 14, JukeboxUi.TextWhite, TextAlignmentOptions.MidlineLeft, font);
        JukeboxUi.Layout(label.gameObject, flexW: 1f, minW: 60f);
        return row;
    }

    private static void AddVerticalFitter(GameObject go)
    {
        ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
