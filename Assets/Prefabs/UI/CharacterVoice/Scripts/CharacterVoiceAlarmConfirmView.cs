using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// ai_alarm_maker가 반환한 대사 중 실제로 음성화할 항목을 한 번에 확인하는 창.
public class CharacterVoiceAlarmConfirmView : MonoBehaviour
{
    public sealed class PreparedAlarm
    {
        public string text;
        public byte[] wav;
        public string language;
    }

    private sealed class Candidate
    {
        public string text;
        public bool selected = true;
        public byte[] wav;
        public AudioClip clip;
        public string speechLanguage;
        public Button selectButton;
        public Image selectIcon;
        public Button regenerateButton;
        public Button playButton;
    }

    [SerializeField] private Vector2 panelSize = new Vector2(500f, 520f);
    [SerializeField] protected bool pomodoroMode;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite checkmarkSprite;
    [SerializeField] private Transform listContent;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private Button confirmButton;

    private bool built;
    private bool busy;
    private string characterName;
    private string refId;
    private string language = "ko";
    private string speed = "100";
    private Action<List<PreparedAlarm>> confirmed;
    private AudioSource previewAudioSource;
    private readonly List<GameObject> rows = new List<GameObject>();
    private readonly List<Candidate> candidates = new List<Candidate>();

    protected virtual void Awake()
    {
        ApplyStyle();
        EnsureBuilt();
    }

    public void Open(
        List<string> values,
        string targetCharacterName,
        string selectedRefId,
        string soundLanguage,
        string soundSpeed,
        Action<List<PreparedAlarm>> onConfirmed)
    {
        EnsureBuilt();
        ApplyLocalizedStaticText();
        StopAllCoroutines();
        characterName = targetCharacterName ?? string.Empty;
        refId = selectedRefId ?? string.Empty;
        language = NormalizeLanguage(soundLanguage);
        speed = string.IsNullOrWhiteSpace(soundSpeed) ? "100" : soundSpeed;
        confirmed = onConfirmed;
        busy = false;
        ReleaseCandidateClips();
        candidates.Clear();
        foreach (string value in values ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(new Candidate { text = value.Trim(), selected = true });
            }
        }

        RebuildRows();
        if (noticeText != null)
        {
            noticeText.text = candidates.Count > 0
                ? T(pomodoroMode
                    ? "포모도로 후보 음성을 준비합니다."
                    : "알람 후보 음성을 준비합니다.")
                : T(pomodoroMode
                    ? "사용할 수 있는 포모도로 대사가 없습니다."
                    : "사용할 수 있는 알람 대사가 없습니다.");
        }
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(candidates.Count == 0);
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshButtons();
        if (candidates.Count > 0)
        {
            StartCoroutine(PrepareAllVoices());
        }
    }

    public void Hide()
    {
        StopAllCoroutines();
        busy = false;
        if (previewAudioSource != null)
        {
            previewAudioSource.Stop();
        }
        ReleaseCandidateClips();
        confirmed = null;
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public void EditorBuild(
        Sprite roundedSprite = null,
        TMP_FontAsset fontAsset = null,
        bool usePomodoroMode = false,
        Sprite selectionCheckmarkSprite = null)
    {
        pomodoroMode = usePomodoroMode;
        if (roundedSprite != null) panelSprite = roundedSprite;
        if (fontAsset != null) font = fontAsset;
        if (selectionCheckmarkSprite != null)
        {
            checkmarkSprite = selectionCheckmarkSprite;
        }
        ApplyStyle();
        BuildHierarchy();
    }
#endif

    private void OnConfirmClicked()
    {
        List<PreparedAlarm> selected = new List<PreparedAlarm>();
        foreach (Candidate candidate in candidates)
        {
            if (candidate.selected)
            {
                if (candidate.wav == null || candidate.wav.Length == 0)
                {
                    if (noticeText != null)
                    {
                        noticeText.text = T("선택한 대사의 음성을 먼저 준비해주세요.");
                    }
                    return;
                }
                selected.Add(new PreparedAlarm
                {
                    text = candidate.text,
                    wav = candidate.wav,
                    language = candidate.speechLanguage
                });
            }
        }

        if (selected.Count == 0)
        {
            if (noticeText != null)
            {
                noticeText.text = T("하나 이상의 대사를 선택해주세요.");
            }
            return;
        }

        Action<List<PreparedAlarm>> callback = confirmed;
        confirmed = null;
        ReleaseCandidateClips();
        gameObject.SetActive(false);
        callback?.Invoke(selected);
    }

    private void RebuildRows()
    {
        ClearRows();
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            GameObject row = Instantiate(rowTemplate, listContent);
            row.name = "AlarmCandidateRow_" + i;
            row.SetActive(true);
            candidate.selectButton = MemoryArchiveUi.FindComponent<Button>(row.transform, "SelectButton");
            candidate.selectIcon =
                MemoryArchiveUi.FindComponent<Image>(
                    row.transform,
                    "SelectionCheckmarkImage");
            candidate.regenerateButton =
                MemoryArchiveUi.FindComponent<Button>(row.transform, "RegenerateButton");
            candidate.playButton = MemoryArchiveUi.FindComponent<Button>(row.transform, "PlayButton");
            TMP_Text candidateText = MemoryArchiveUi.FindComponent<TMP_Text>(row.transform, "CandidateText");
            if (candidateText != null)
            {
                candidateText.text = candidate.text;
            }
            UpdateSelect(candidate);
            if (candidate.selectButton != null)
            {
                candidate.selectButton.onClick.RemoveAllListeners();
                candidate.selectButton.onClick.AddListener(() =>
                {
                    candidate.selected = !candidate.selected;
                    UpdateSelect(candidate);
                    UpdateSelectedCount();
                    RefreshButtons();
                });
            }
            if (candidate.regenerateButton != null)
            {
                candidate.regenerateButton.onClick.RemoveAllListeners();
                candidate.regenerateButton.onClick.AddListener(
                    () => StartCoroutine(RegenerateVoice(candidate)));
            }
            if (candidate.playButton != null)
            {
                candidate.playButton.onClick.RemoveAllListeners();
                candidate.playButton.onClick.AddListener(() => PlayCandidate(candidate));
            }
            rows.Add(row);
        }

        UpdateSelectedCount();
        RefreshButtons();
    }

    private void UpdateSelectedCount()
    {
        int selected = 0;
        foreach (Candidate candidate in candidates)
        {
            if (candidate.selected) selected++;
        }
        if (countText != null)
        {
            countText.text = selected + "/" + candidates.Count;
        }
    }

    private static void UpdateSelect(Candidate candidate)
    {
        if (candidate.selectButton == null) return;
        Image image = candidate.selectButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = candidate.selected ? MemoryArchiveUi.AccentGreen : MemoryArchiveUi.PanelBg;
        }
        if (candidate.selectIcon != null)
        {
            candidate.selectIcon.gameObject.SetActive(candidate.selected);
        }
    }

    private IEnumerator PrepareAllVoices()
    {
        busy = true;
        RefreshButtons();
        int failed = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (noticeText != null)
            {
                noticeText.text = string.Format(
                    T(pomodoroMode
                        ? "포모도로 후보 음성을 준비 중입니다. ({0}/{1})"
                        : "알람 후보 음성을 준비 중입니다. ({0}/{1})"),
                    i + 1,
                    candidates.Count);
            }

            bool success = false;
            yield return RequestVoice(candidates[i], value => success = value);
            if (!success)
            {
                failed++;
            }
        }

        busy = false;
        if (noticeText != null)
        {
            noticeText.text = failed == 0
                ? string.Empty
                : string.Format(T("{0}개의 음성을 준비하지 못했습니다. 재생성을 눌러주세요."), failed);
        }
        RefreshButtons();
    }

    private IEnumerator RegenerateVoice(Candidate candidate)
    {
        if (busy || candidate == null)
        {
            yield break;
        }

        busy = true;
        if (noticeText != null)
        {
            noticeText.text = T("후보 음성을 다시 생성 중입니다.");
        }
        RefreshButtons();

        bool success = false;
        yield return RequestVoice(candidate, value => success = value);

        busy = false;
        if (noticeText != null)
        {
            noticeText.text = success
                ? T("후보 음성을 다시 생성했습니다.")
                : T("후보 음성 재생성에 실패했습니다.");
        }
        RefreshButtons();
    }

    private IEnumerator RequestVoice(Candidate candidate, Action<bool> completed)
    {
        string baseUrl = null;
        yield return ResolveVoiceBaseUrl(value => baseUrl = value);
        if (candidate == null || string.IsNullOrWhiteSpace(baseUrl))
        {
            completed?.Invoke(false);
            yield break;
        }

        CharacterVoiceSpeechText speechText = null;
        yield return CharacterVoiceSpeechTextResolver.Resolve(
            baseUrl,
            candidate.text,
            CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage(),
            language,
            value => speechText = value);
        if (speechText == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        Dictionary<string, string> requestData = new Dictionary<string, string>
        {
            { "text", speechText.speechText },
            { "char", characterName },
            { "lang", speechText.speechLanguage },
            { "speed", speed },
            { "chatIdx", "-1" }
        };
        if (!string.IsNullOrWhiteSpace(refId))
        {
            requestData["ref_id"] = refId;
        }

        string url = baseUrl.TrimEnd('/') + "/getSound";
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestData)));
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            AudioClip clip = request.result == UnityWebRequest.Result.Success
                ? DownloadHandlerAudioClip.GetContent(request)
                : null;
            byte[] wav = request.downloadHandler != null
                ? request.downloadHandler.data
                : null;
            if (clip == null || wav == null || wav.Length == 0)
            {
                Debug.LogError(
                    $"[CharacterVoiceAlarmConfirm] TTS failed. code={request.responseCode}, error={request.error}");
                completed?.Invoke(false);
                yield break;
            }

            if (candidate.clip != null && candidate.clip != clip)
            {
                Destroy(candidate.clip);
            }
            candidate.clip = clip;
            candidate.wav = wav;
            candidate.speechLanguage = speechText.speechLanguage;
            completed?.Invoke(true);
        }
    }

    private void PlayCandidate(Candidate candidate)
    {
        if (busy || candidate == null || candidate.clip == null)
        {
            return;
        }

        if (previewAudioSource == null)
        {
            previewAudioSource = gameObject.AddComponent<AudioSource>();
            previewAudioSource.playOnAwake = false;
        }
        previewAudioSource.Stop();
        previewAudioSource.clip = candidate.clip;
        previewAudioSource.loop = false;
        previewAudioSource.volume = GetMasterVolume();
        previewAudioSource.Play();

        if (AnswerBalloonSimpleManager.Instance != null)
        {
            AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleForSeconds(
                candidate.text,
                candidate.clip.length + 0.5f);
        }
    }

    private void RefreshButtons()
    {
        bool hasSelected = false;
        bool allSelectedReady = true;
        foreach (Candidate candidate in candidates)
        {
            if (candidate.selectButton != null)
            {
                candidate.selectButton.interactable = !busy;
            }
            if (candidate.regenerateButton != null)
            {
                candidate.regenerateButton.interactable = !busy;
            }
            if (candidate.playButton != null)
            {
                candidate.playButton.interactable = !busy && candidate.clip != null;
            }
            if (candidate.selected)
            {
                hasSelected = true;
                if (candidate.wav == null || candidate.wav.Length == 0)
                {
                    allSelectedReady = false;
                }
            }
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = !busy && hasSelected && allSelectedReady;
        }
    }

    private IEnumerator ResolveVoiceBaseUrl(Action<string> completed)
    {
        if (SettingManager.Instance != null &&
            SettingManager.Instance.settings != null &&
            SettingManager.Instance.settings.isLocalSound)
        {
            completed?.Invoke("http://127.0.0.1:5000");
            yield break;
        }

        string baseUrl = string.Empty;
        if (ServerManager.Instance != null)
        {
            bool done = false;
            ServerManager.Instance.GetBaseUrl(url =>
            {
                baseUrl = url;
                done = true;
            });
            while (!done) yield return null;
        }

        if (string.IsNullOrWhiteSpace(baseUrl) &&
            ServerManager.Instance != null &&
            SettingManager.Instance != null &&
            (SettingManager.Instance.GetInstallStatus() < 2 ||
             SettingManager.Instance.IsDevSoundEnabled()))
        {
            bool done = false;
            ServerManager.Instance.GetServerUrlFromServerId("dev_voice", url =>
            {
                baseUrl = url;
                done = true;
            });
            while (!done) yield return null;
        }

        completed?.Invoke(
            string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:5000" : baseUrl);
    }

    private void EnsureBuilt()
    {
        if (built) return;
        if (MemoryArchiveUi.FindDeepChild(transform, "AlarmConfirmBody") != null)
        {
            BindExisting();
        }
        else
        {
            BuildHierarchy();
        }
    }

    private void BindExisting()
    {
        built = true;
        listContent = listContent != null
            ? listContent
            : MemoryArchiveUi.FindDeepChild(transform, "AlarmCandidateContent");
        Transform template = rowTemplate != null
            ? rowTemplate.transform
            : MemoryArchiveUi.FindDeepChild(transform, "AlarmCandidateRowTemplate");
        rowTemplate = template != null ? template.gameObject : null;
        countText = countText != null
            ? countText
            : MemoryArchiveUi.FindComponent<TMP_Text>(transform, "CountText");
        emptyText = emptyText != null
            ? emptyText
            : MemoryArchiveUi.FindComponent<TMP_Text>(transform, "EmptyText");
        noticeText = noticeText != null
            ? noticeText
            : MemoryArchiveUi.FindComponent<TMP_Text>(transform, "NoticeText");
        confirmButton = confirmButton != null
            ? confirmButton
            : MemoryArchiveUi.FindComponent<Button>(transform, "ConfirmButton");
        if (rowTemplate != null) rowTemplate.SetActive(false);
        BindButton("CloseButton", Hide);
        BindButton("CancelButton", Hide);
        BindButton("ConfirmButton", OnConfirmClicked);
    }

    private void BuildHierarchy()
    {
        if (built) return;
        built = true;

        RectTransform rect = transform as RectTransform;
        if (rect != null) rect.sizeDelta = panelSize;
        MemoryArchiveUi.ApplyRounded(MemoryArchiveUi.GetOrAdd<Image>(gameObject), MemoryArchiveUi.RootBg);
        VerticalLayoutGroup root = MemoryArchiveUi.GetOrAdd<VerticalLayoutGroup>(gameObject);
        root.padding = new RectOffset(10, 10, 10, 8);
        root.spacing = 8f;
        root.childControlWidth = true;
        root.childControlHeight = true;
        root.childForceExpandWidth = true;
        root.childForceExpandHeight = false;

        GameObject header = MemoryArchiveUi.CreateUIObject("Header", transform);
        MemoryArchiveUi.Layout(header, minH: 52f, prefH: 52f, flexH: 0f);
        MemoryArchiveUi.AddRow(header, 8f).childForceExpandHeight = true;
        TMP_Text title = MemoryArchiveUi.CreateText(
            "TitleText",
            header.transform,
            pomodoroMode ? "포모도로 음성 추가 확인" : "알람 음성 추가 확인",
            19f,
            MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.Layout(title.gameObject, flexW: 1f);

        GameObject badge = MemoryArchiveUi.CreatePanel("CountBadge", header.transform, MemoryArchiveUi.Accent);
        MemoryArchiveUi.Layout(badge, minW: 54f, prefW: 54f, minH: 28f, prefH: 28f);
        countText = MemoryArchiveUi.CreateText(
            "CountText", badge.transform, "0/0", 12f, MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.Center);
        MemoryArchiveUi.SetStretch(countText.gameObject, Vector4.zero);

        Button close = MemoryArchiveUi.CreateButton(
            "CloseButton", header.transform, "×", MemoryArchiveUi.HeaderBg, 24f);
        MemoryArchiveUi.Layout(close.gameObject, minW: 40f, prefW: 40f);
        close.onClick.AddListener(Hide);

        TMP_Text description = MemoryArchiveUi.CreateText(
            "DescriptionText",
            transform,
            pomodoroMode
                ? "후보 음성을 듣고 추가할 포모도로 대사를 선택해주세요."
                : "후보 음성을 듣고 추가할 알람 대사를 선택해주세요.",
            13f,
            MemoryArchiveUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.Layout(description.gameObject, minH: 24f, prefH: 24f, flexH: 0f);

        BuildBody();

        noticeText = MemoryArchiveUi.CreateText(
            "NoticeText", transform, "", 12f, MemoryArchiveUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.Layout(noticeText.gameObject, minH: 18f, prefH: 18f, flexH: 0f);

        GameObject buttons = MemoryArchiveUi.CreateUIObject("ButtonRow", transform);
        MemoryArchiveUi.Layout(buttons, minH: 38f, prefH: 38f, flexH: 0f);
        HorizontalLayoutGroup buttonLayout = MemoryArchiveUi.AddRow(buttons, 8f);
        buttonLayout.childForceExpandHeight = true;
        buttonLayout.childForceExpandWidth = true;
        Button cancel = MemoryArchiveUi.CreateButton(
            "CancelButton", buttons.transform, "취소", MemoryArchiveUi.PanelBg2, 13f);
        MemoryArchiveUi.Layout(cancel.gameObject, flexW: 1f);
        cancel.onClick.AddListener(Hide);
        confirmButton = MemoryArchiveUi.CreateButton(
            "ConfirmButton", buttons.transform, "선택 항목 추가", MemoryArchiveUi.Accent, 13f);
        MemoryArchiveUi.Layout(confirmButton.gameObject, flexW: 1f);
        confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    private void BuildBody()
    {
        GameObject body = MemoryArchiveUi.CreatePanel(
            "AlarmConfirmBody", transform, MemoryArchiveUi.PanelBg);
        MemoryArchiveUi.Layout(body, flexH: 1f);
        ScrollRect scroll = body.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        GameObject viewport = MemoryArchiveUi.CreateUIObject("Viewport", body.transform);
        MemoryArchiveUi.SetStretch(viewport, new Vector4(4f, 4f, 18f, 4f));
        viewport.AddComponent<RectMask2D>();
        GameObject content = MemoryArchiveUi.CreateUIObject("AlarmCandidateContent", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        MemoryArchiveUi.AddColumn(content, 6f, new RectOffset(2, 2, 2, 2)).childForceExpandWidth = true;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        BuildScrollbar(body.transform, scroll);
        listContent = content.transform;

        rowTemplate = MemoryArchiveUi.CreatePanel(
            "AlarmCandidateRowTemplate", listContent, MemoryArchiveUi.PanelBg2);
        MemoryArchiveUi.Layout(rowTemplate, minH: 64f, prefH: 64f, flexH: 0f);
        HorizontalLayoutGroup row = MemoryArchiveUi.AddRow(
            rowTemplate, 8f, new RectOffset(8, 8, 8, 8));
        row.childForceExpandHeight = true;
        Button select = MemoryArchiveUi.CreateButton(
            "SelectButton", rowTemplate.transform, "", MemoryArchiveUi.AccentGreen, 16f);
        MemoryArchiveUi.Layout(select.gameObject, minW: 44f, prefW: 44f);
        TMP_Text selectLabel =
            select.GetComponentInChildren<TMP_Text>(true);
        if (selectLabel != null)
        {
            selectLabel.gameObject.SetActive(false);
        }
        GameObject checkmark = MemoryArchiveUi.CreateUIObject(
            "SelectionCheckmarkImage",
            select.transform);
        RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.15f, 0.15f);
        checkmarkRect.anchorMax = new Vector2(0.85f, 0.85f);
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;
        Image checkmarkImage = checkmark.AddComponent<Image>();
        checkmarkImage.sprite = checkmarkSprite;
        checkmarkImage.color = Color.white;
        checkmarkImage.preserveAspect = true;
        checkmarkImage.raycastTarget = false;
        TMP_Text text = MemoryArchiveUi.CreateText(
            "CandidateText", rowTemplate.transform, "-", 13f, MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        MemoryArchiveUi.Layout(text.gameObject, flexW: 1f);
        Button regenerate = MemoryArchiveUi.CreateButton(
            "RegenerateButton", rowTemplate.transform, "재생성", MemoryArchiveUi.PanelBg, 12f);
        MemoryArchiveUi.Layout(regenerate.gameObject, minW: 62f, prefW: 62f);
        Button play = MemoryArchiveUi.CreateButton(
            "PlayButton", rowTemplate.transform, "듣기", MemoryArchiveUi.Accent, 12f);
        MemoryArchiveUi.Layout(play.gameObject, minW: 52f, prefW: 52f);
        rowTemplate.SetActive(false);

        emptyText = MemoryArchiveUi.CreateText(
            "EmptyText",
            body.transform,
            pomodoroMode
                ? "사용할 수 있는 포모도로 대사가 없습니다."
                : "사용할 수 있는 알람 대사가 없습니다.",
            14f,
            MemoryArchiveUi.TextMuted, TextAlignmentOptions.Center);
        MemoryArchiveUi.SetStretch(emptyText.gameObject, new Vector4(36f, 36f, 36f, 36f));
    }

    private static void BuildScrollbar(Transform parent, ScrollRect scroll)
    {
        GameObject root = MemoryArchiveUi.CreatePanel("Scrollbar", parent, MemoryArchiveUi.ScrollTrack);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(12f, -16f);
        rect.anchoredPosition = new Vector2(-5f, 0f);
        Scrollbar scrollbar = root.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        GameObject area = MemoryArchiveUi.CreateUIObject("Sliding Area", root.transform);
        MemoryArchiveUi.SetStretch(area, new Vector4(1f, 1f, 1f, 1f));
        GameObject handle = MemoryArchiveUi.CreatePanel(
            "Handle", area.transform, MemoryArchiveUi.ScrollHandle);
        MemoryArchiveUi.SetStretch(handle, Vector4.zero);
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    private void ClearRows()
    {
        foreach (GameObject row in rows)
        {
            if (row != null) Destroy(row);
        }
        rows.Clear();
    }

    private void ReleaseCandidateClips()
    {
        foreach (Candidate candidate in candidates)
        {
            if (candidate != null && candidate.clip != null)
            {
                Destroy(candidate.clip);
                candidate.clip = null;
            }
        }
    }

    private void BindButton(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = MemoryArchiveUi.FindComponent<Button>(transform, name);
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ApplyStyle()
    {
        if (panelSprite != null) MemoryArchiveUi.RoundedSpriteOverride = panelSprite;
        if (font != null) MemoryArchiveUi.FontOverride = font;
    }

    private static string NormalizeLanguage(string value)
    {
        if (string.Equals(value, "jp", StringComparison.OrdinalIgnoreCase))
        {
            return "ja";
        }
        return string.IsNullOrWhiteSpace(value) ? "ko" : value;
    }

    private static float GetMasterVolume()
    {
        try
        {
            return SettingManager.Instance.settings.sound_volumeMaster / 100f;
        }
        catch
        {
            return 1f;
        }
    }

    private void ApplyLocalizedStaticText()
    {
        SetLocalizedText(
            transform,
            "TitleText",
            pomodoroMode
                ? "포모도로 음성 추가 확인"
                : "알람 음성 추가 확인");
        SetLocalizedText(
            transform,
            "DescriptionText",
            pomodoroMode
                ? "후보 음성을 듣고 추가할 포모도로 대사를 선택해주세요."
                : "후보 음성을 듣고 추가할 알람 대사를 선택해주세요.");
        SetLocalizedButtonLabel(transform, "CancelButton", "취소");
        SetLocalizedButtonLabel(transform, "ConfirmButton", "선택 항목 추가");
        if (emptyText != null)
        {
            emptyText.text = T(
                pomodoroMode
                    ? "사용할 수 있는 포모도로 대사가 없습니다."
                    : "사용할 수 있는 알람 대사가 없습니다.");
        }
        if (rowTemplate != null)
        {
            SetLocalizedButtonLabel(
                rowTemplate.transform,
                "RegenerateButton",
                "재생성");
            SetLocalizedButtonLabel(rowTemplate.transform, "PlayButton", "듣기");
        }
    }

    private void SetLocalizedText(
        Transform root,
        string objectName,
        string source)
    {
        TMP_Text label = MemoryArchiveUi.FindComponent<TMP_Text>(
            root,
            objectName);
        if (label != null)
        {
            label.text = T(source);
        }
    }

    private void SetLocalizedButtonLabel(
        Transform root,
        string objectName,
        string source)
    {
        Button button = MemoryArchiveUi.FindComponent<Button>(
            root,
            objectName);
        TMP_Text label = button != null
            ? button.GetComponentInChildren<TMP_Text>(true)
            : null;
        if (label != null)
        {
            label.text = T(source);
        }
    }

    private string T(string value)
    {
        return pomodoroMode
            ? LanguageDataCharacterVoicePomodoro.Translate(value)
            : LanguageDataCharacterVoiceAlarm.Translate(value);
    }
}
