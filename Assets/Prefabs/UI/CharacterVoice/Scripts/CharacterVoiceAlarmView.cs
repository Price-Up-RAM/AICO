using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

// 캐릭터별 기본/생성 알람 대사를 조회하고 직접 추가·재합성·미리 듣기 하는 독립 화면.
public class CharacterVoiceAlarmView : MonoBehaviour
{
    private const int GeneratedChoiceCount = 3;
    private const float RowLongPressSeconds = 0.65f;
    private static readonly Color EnabledRowColor =
        new Color(0.145f, 0.205f, 0.175f, 1f);
    private static readonly Color DisabledRowColor =
        new Color(0.105f, 0.115f, 0.135f, 1f);
    private const float DisabledRowAlpha = 0.72f;
    private static readonly string[][] RandomAlarmConcepts =
    {
        new[] { "덤덤한 알람", "淡々としたアラーム", "A calm, matter-of-fact alarm" },
        new[] { "활발하게 알람", "元気で活発なアラーム", "A lively and energetic alarm" },
        new[] { "끝에 냥을 붙인 귀여운 알람", "語尾に「にゃん」を付ける可愛いアラーム", "A cute alarm ending each line with meow" },
        new[] { "다정하게 깨워주는 알람", "優しく起こしてくれるアラーム", "A gentle and caring wake-up alarm" },
        new[] { "엄격한 선생님처럼 재촉하는 알람", "厳しい先生のように急かすアラーム", "A strict teacher-style alarm" },
        new[] { "졸린 목소리로 중얼거리는 알람", "眠そうな声でつぶやくアラーム", "A sleepy and murmuring alarm" },
        new[] { "사무적인 비서처럼 알려주는 알람", "事務的な秘書のように知らせるアラーム", "A professional secretary-style alarm" },
        new[] { "과장되고 코믹한 알람", "大げさでコミカルなアラーム", "An exaggerated and comedic alarm" },
        new[] { "따뜻한 아침 인사 같은 알람", "温かい朝の挨拶のようなアラーム", "A warm morning-greeting alarm" },
        new[] { "긴박한 카운트다운 느낌의 알람", "緊迫したカウントダウン風のアラーム", "An urgent countdown-style alarm" }
    };

    [SerializeField] private Vector2 panelSize = new Vector2(620f, 560f);
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Transform listContent;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private TMP_InputField conceptInput;
    [SerializeField] private TMP_InputField addInput;
    [SerializeField] private TMP_Text characterText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private Button sampleButton;
    [SerializeField] private Button randomConceptButton;
    [SerializeField] private Button generateButton;
    [SerializeField] private Button addButton;

    private bool built;
    private bool busy;
    private string characterName;
    private string refId;
    private string language = "ko";
    private string speed = "100";
    private CharacterAlarmVoiceCatalog catalog;
    private AudioSource previewAudioSource;
    private Coroutine loadingNoticeCoroutine;
    private Coroutine rowLongPressCoroutine;
    private Vector2 rowPointerPosition;
    private bool rowPointerActive;
    private GameObject editingRow;
    private TMP_InputField editingInput;
    private bool suppressEditCommit;
    private GameObject suppressRowClick;
    private readonly List<GameObject> rows = new List<GameObject>();

    private void Awake()
    {
        ApplyStyle();
        EnsureBuilt();
    }

    public void Show(
        string targetCharacterName,
        string selectedRefId,
        string soundLanguage,
        string soundSpeed,
        CharacterAlarmVoiceCatalog alarmCatalog)
    {
        EnsureBuilt();
        ApplyLocalizedStaticText();
        characterName = targetCharacterName != null ? targetCharacterName.Trim() : string.Empty;
        refId = selectedRefId ?? string.Empty;
        language = NormalizeLanguage(soundLanguage);
        speed = string.IsNullOrWhiteSpace(soundSpeed) ? "100" : soundSpeed;
        catalog = alarmCatalog != null ? alarmCatalog : CharacterAlarmVoiceCatalog.LoadDefault();
        busy = false;
        UpdateConceptUiLanguage();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Reload();
    }

    public void Hide()
    {
        StopAllCoroutines();
        loadingNoticeCoroutine = null;
        rowLongPressCoroutine = null;
        rowPointerActive = false;
        suppressRowClick = null;
        ResetEditingState();
        busy = false;
        if (previewAudioSource != null)
        {
            previewAudioSource.Stop();
        }
        gameObject.SetActive(false);
    }

    public void Reload()
    {
        EnsureBuilt();
        CancelRowLongPress();
        ResetEditingState();
        ClearRows();
        List<CharacterAlarmPlaybackCandidate> candidates =
            CharacterAlarmVoiceRepository.GetDisplayCandidates(characterName, catalog);

        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterAlarmPlaybackCandidate candidate = candidates[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.message))
            {
                continue;
            }

            GameObject row = Instantiate(rowTemplate, listContent);
            row.name = "AlarmVoiceRow_" + candidate.id;
            row.SetActive(true);
            BindRow(row, candidate);
            rows.Add(row);
        }

        if (characterText != null)
        {
            characterText.text = string.IsNullOrWhiteSpace(characterName)
                ? T("캐릭터를 선택해주세요.")
                : characterName;
        }
        if (countText != null)
        {
            countText.text = rows.Count.ToString();
        }
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(rows.Count == 0);
        }
        SetNotice("");
        RefreshButtons();
    }

#if UNITY_EDITOR
    public void EditorBuild(Sprite roundedSprite = null, TMP_FontAsset fontAsset = null)
    {
        if (roundedSprite != null) panelSprite = roundedSprite;
        if (fontAsset != null) font = fontAsset;
        ApplyStyle();
        BuildHierarchy();
    }
#endif

    private void BindRow(GameObject row, CharacterAlarmPlaybackCandidate candidate)
    {
        TMP_Text message = MemoryArchiveUi.FindComponent<TMP_Text>(row.transform, "AlarmMessageText");
        TMP_InputField editInput =
            MemoryArchiveUi.FindComponent<TMP_InputField>(row.transform, "EditInput");
        Button delete = MemoryArchiveUi.FindComponent<Button>(row.transform, "DeleteButton");
        Button regenerate = MemoryArchiveUi.FindComponent<Button>(row.transform, "RegenerateButton");
        Button play = MemoryArchiveUi.FindComponent<Button>(row.transform, "PlayButton");

        if (message != null)
        {
            // 기본1/후보1/생성1과 같은 내부 라벨은 표시하지 않고 대사만 보여준다.
            message.text = candidate.message;
        }
        if (editInput != null)
        {
            editInput.SetTextWithoutNotify(candidate.message);
            editInput.onEndEdit.RemoveAllListeners();
            editInput.onEndEdit.AddListener(value => OnRowEditFinished(row, candidate, value));
            editInput.gameObject.SetActive(false);
        }
        if (delete != null)
        {
            delete.gameObject.SetActive(false);
            delete.onClick.RemoveAllListeners();
            delete.onClick.AddListener(() => DeleteAlarm(row, candidate));
            AddEventTrigger(
                delete.gameObject,
                EventTriggerType.PointerDown,
                _ => suppressEditCommit = true,
                false);
            AddScrollForwarding(delete.gameObject);
        }
        if (regenerate != null)
        {
            regenerate.interactable = !busy;
            regenerate.onClick.RemoveAllListeners();
            regenerate.onClick.AddListener(() =>
            {
                if (!busy)
                {
                    StartCoroutine(RegenerateVoice(candidate));
                }
            });
        }
        if (play != null)
        {
            play.interactable = !busy && HasPlayableAudio(candidate);
            play.onClick.RemoveAllListeners();
            play.onClick.AddListener(() => PlayCandidate(candidate));
        }
        ApplyRowActiveVisual(row, candidate.enabled);
        ConfigureLongPress(row, candidate);
    }

    private void ConfigureLongPress(
        GameObject row,
        CharacterAlarmPlaybackCandidate candidate)
    {
        AddEventTrigger(
            row,
            EventTriggerType.PointerDown,
            data =>
            {
                PointerEventData pointer = data as PointerEventData;
                if (pointer == null)
                {
                    return;
                }
                BeginRowLongPress(
                    row,
                    candidate,
                    pointer.position);
            },
            true);
        AddEventTrigger(row, EventTriggerType.Drag, data =>
        {
            if (data is PointerEventData pointer)
            {
                rowPointerPosition = pointer.position;
            }
        }, false);
        AddEventTrigger(row, EventTriggerType.PointerUp, _ => CancelRowLongPress(), false);
        AddEventTrigger(row, EventTriggerType.PointerExit, _ => CancelRowLongPress(), false);
        AddEventTrigger(
            row,
            EventTriggerType.PointerClick,
            data => OnRowClicked(row, candidate, data),
            false);
        AddScrollForwarding(row);
    }

    private void BeginRowLongPress(
        GameObject row,
        CharacterAlarmPlaybackCandidate candidate,
        Vector2 pointerPosition)
    {
        if (busy)
        {
            return;
        }

        CancelRowLongPress();
        rowPointerPosition = pointerPosition;
        rowPointerActive = true;
        rowLongPressCoroutine = StartCoroutine(
            WaitForRowLongPress(row, candidate, pointerPosition));
    }

    private IEnumerator WaitForRowLongPress(
        GameObject row,
        CharacterAlarmPlaybackCandidate candidate,
        Vector2 pointerPosition)
    {
        float elapsed = 0f;
        while (elapsed < RowLongPressSeconds)
        {
            if (!rowPointerActive ||
                Vector2.Distance(pointerPosition, rowPointerPosition) > 14f)
            {
                rowPointerActive = false;
                rowLongPressCoroutine = null;
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        rowPointerActive = false;
        rowLongPressCoroutine = null;
        suppressRowClick = row;
        EnterRowEditMode(row, candidate);
    }

    private void OnRowClicked(
        GameObject row,
        CharacterAlarmPlaybackCandidate candidate,
        BaseEventData eventData)
    {
        PointerEventData pointer = eventData as PointerEventData;
        if (pointer != null && pointer.button != PointerEventData.InputButton.Left)
        {
            return;
        }
        if (IsRowControlClick(row, pointer))
        {
            return;
        }
        if (suppressRowClick == row)
        {
            suppressRowClick = null;
            return;
        }
        suppressRowClick = null;
        if (busy || editingRow == row || candidate == null)
        {
            return;
        }

        bool enabled = !candidate.enabled;
        bool saved = candidate.isGenerated &&
                     candidate.generatedRecord != null
            ? CharacterAlarmVoiceRepository.SetGeneratedAlarmEnabled(
                characterName,
                candidate.generatedRecord.id,
                enabled)
            : CharacterAlarmVoiceRepository.SetDefaultAlarmEnabled(
                characterName,
                candidate.id,
                enabled);
        if (!saved)
        {
            SetNotice(T("알람 사용 상태를 저장하지 못했습니다."));
            return;
        }

        candidate.enabled = enabled;
        ApplyRowActiveVisual(row, enabled);
        SetNotice(enabled
            ? T("이 알람 음성을 사용합니다.")
            : T("이 알람 음성을 사용하지 않습니다."));
    }

    private static bool IsRowControlClick(
        GameObject row,
        PointerEventData pointer)
    {
        if (row == null || pointer == null)
        {
            return false;
        }

        GameObject target = pointer.pointerPress ??
                            pointer.rawPointerPress ??
                            pointer.pointerCurrentRaycast.gameObject;
        Transform current = target != null ? target.transform : null;
        while (current != null && current != row.transform)
        {
            if (current.GetComponent<Button>() != null ||
                current.GetComponent<TMP_InputField>() != null)
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    private static void ApplyRowActiveVisual(GameObject row, bool enabled)
    {
        if (row == null)
        {
            return;
        }

        CanvasGroup canvasGroup = MemoryArchiveUi.GetOrAdd<CanvasGroup>(row);
        canvasGroup.alpha = enabled ? 1f : DisabledRowAlpha;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        Image background = row.GetComponent<Image>();
        if (background != null)
        {
            background.color = enabled ? EnabledRowColor : DisabledRowColor;
        }
        TMP_Text message = MemoryArchiveUi.FindComponent<TMP_Text>(
            row.transform,
            "AlarmMessageText");
        if (message != null)
        {
            message.color = enabled
                ? MemoryArchiveUi.TextWhite
                : MemoryArchiveUi.TextMuted;
        }
        TMP_InputField input = MemoryArchiveUi.FindComponent<TMP_InputField>(
            row.transform,
            "EditInput");
        if (input != null && input.textComponent != null)
        {
            input.textComponent.color = enabled
                ? MemoryArchiveUi.TextWhite
                : MemoryArchiveUi.TextMuted;
        }
    }

    private void CancelRowLongPress()
    {
        rowPointerActive = false;
        if (rowLongPressCoroutine == null)
        {
            return;
        }

        StopCoroutine(rowLongPressCoroutine);
        rowLongPressCoroutine = null;
    }

    private void EnterRowEditMode(
        GameObject row,
        CharacterAlarmPlaybackCandidate candidate)
    {
        if (busy || row == null || candidate == null)
        {
            return;
        }
        ResetEditingState();
        TMP_Text message = MemoryArchiveUi.FindComponent<TMP_Text>(
            row.transform,
            "AlarmMessageText");
        TMP_InputField input = MemoryArchiveUi.FindComponent<TMP_InputField>(
            row.transform,
            "EditInput");
        Button delete = MemoryArchiveUi.FindComponent<Button>(
            row.transform,
            "DeleteButton");
        if (input == null || delete == null)
        {
            return;
        }

        editingRow = row;
        editingInput = input;
        suppressEditCommit = false;
        input.SetTextWithoutNotify(candidate.message);
        if (message != null) message.gameObject.SetActive(false);
        delete.gameObject.SetActive(true);
        input.gameObject.SetActive(true);
        input.Select();
        input.ActivateInputField();
        input.MoveTextEnd(false);
    }

    private void OnRowEditFinished(
        GameObject row,
        CharacterAlarmPlaybackCandidate candidate,
        string value)
    {
        StartCoroutine(CommitRowEditAfterCurrentClick(
            row,
            candidate,
            value));
    }

    private IEnumerator CommitRowEditAfterCurrentClick(
        GameObject row,
        CharacterAlarmPlaybackCandidate candidate,
        string value)
    {
        // Button.OnPointerDown이 InputField 포커스를 먼저 빼도
        // 같은 클릭의 Button.onClick이 끝날 때까지 편집 UI를 유지한다.
        yield return null;
        if (row == null ||
            suppressEditCommit ||
            editingRow != row ||
            candidate == null)
        {
            yield break;
        }

        string updatedMessage = value != null ? value.Trim() : string.Empty;
        suppressEditCommit = true;
        ExitRowEditVisual(row);
        editingRow = null;
        editingInput = null;
        suppressEditCommit = false;
        if (string.IsNullOrWhiteSpace(updatedMessage))
        {
            SetNotice(T("알람 대사는 비워둘 수 없습니다."));
            yield break;
        }
        if (updatedMessage == candidate.message)
        {
            yield break;
        }

        UpdateEditedAlarm(candidate, updatedMessage);
    }

    private void UpdateEditedAlarm(
        CharacterAlarmPlaybackCandidate candidate,
        string updatedMessage)
    {
        bool saved = candidate != null &&
                     candidate.isGenerated &&
                     candidate.generatedRecord != null
            ? CharacterAlarmVoiceRepository.UpdateGeneratedAlarmMessage(
                characterName,
                candidate.generatedRecord.id,
                updatedMessage)
            : candidate != null &&
              CharacterAlarmVoiceRepository.UpdateDefaultAlarmMessage(
                  characterName,
                  candidate.id,
                  updatedMessage);
        if (!saved)
        {
            SetNotice(T("수정한 알람 대사를 저장하지 못했습니다."));
            return;
        }

        SetNotice(T("알람 대사를 수정했습니다. 음성은 재생성으로 갱신할 수 있습니다."));
        ReloadKeepingNotice();
    }

    private void DeleteAlarm(
        GameObject row,
        CharacterAlarmPlaybackCandidate candidate)
    {
        if (busy || candidate == null)
        {
            return;
        }
        suppressRowClick = row;
        suppressEditCommit = true;
        CancelRowLongPress();
        bool deleted = candidate.isGenerated && candidate.generatedRecord != null
            ? CharacterAlarmVoiceRepository.DeleteGeneratedAlarm(
                characterName,
                candidate.generatedRecord.id)
            : CharacterAlarmVoiceRepository.SetDefaultAlarmHidden(
                characterName,
                candidate.id,
                true);
        editingRow = null;
        editingInput = null;
        suppressEditCommit = false;
        SetNotice(deleted
            ? T("알람을 삭제했습니다.")
            : T("알람을 삭제하지 못했습니다."));
        ReloadKeepingNotice();
    }

    private void ResetEditingState()
    {
        if (editingRow != null)
        {
            suppressEditCommit = true;
            ExitRowEditVisual(editingRow);
        }
        editingRow = null;
        editingInput = null;
        suppressEditCommit = false;
    }

    private static void ExitRowEditVisual(GameObject row)
    {
        if (row == null)
        {
            return;
        }

        TMP_Text message = MemoryArchiveUi.FindComponent<TMP_Text>(
            row.transform,
            "AlarmMessageText");
        TMP_InputField input = MemoryArchiveUi.FindComponent<TMP_InputField>(
            row.transform,
            "EditInput");
        Button delete = MemoryArchiveUi.FindComponent<Button>(
            row.transform,
            "DeleteButton");
        if (input != null)
        {
            input.DeactivateInputField();
            input.gameObject.SetActive(false);
        }
        if (delete != null) delete.gameObject.SetActive(false);
        if (message != null) message.gameObject.SetActive(true);
    }

    private static void AddEventTrigger(
        GameObject target,
        EventTriggerType type,
        UnityEngine.Events.UnityAction<BaseEventData> action,
        bool clearExisting)
    {
        if (target == null || action == null)
        {
            return;
        }

        EventTrigger trigger = MemoryArchiveUi.GetOrAdd<EventTrigger>(target);
        if (trigger.triggers == null)
        {
            trigger.triggers = new List<EventTrigger.Entry>();
        }
        if (clearExisting)
        {
            trigger.triggers.Clear();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void AddScrollForwarding(GameObject source)
    {
        AddEventTrigger(
            source,
            EventTriggerType.Scroll,
            data => ForwardToParentScroll(source, data, EventTriggerType.Scroll),
            false);
        AddEventTrigger(
            source,
            EventTriggerType.BeginDrag,
            data =>
            {
                CancelRowLongPress();
                ForwardToParentScroll(source, data, EventTriggerType.BeginDrag);
            },
            false);
        AddEventTrigger(
            source,
            EventTriggerType.Drag,
            data =>
            {
                CancelRowLongPress();
                ForwardToParentScroll(source, data, EventTriggerType.Drag);
            },
            false);
        AddEventTrigger(
            source,
            EventTriggerType.EndDrag,
            data => ForwardToParentScroll(source, data, EventTriggerType.EndDrag),
            false);
    }

    private static void ForwardToParentScroll(
        GameObject source,
        BaseEventData data,
        EventTriggerType eventType)
    {
        PointerEventData pointer = data as PointerEventData;
        ScrollRect parentScroll = source != null
            ? source.GetComponentInParent<ScrollRect>()
            : null;
        if (pointer == null || parentScroll == null)
        {
            return;
        }

        switch (eventType)
        {
            case EventTriggerType.Scroll:
                parentScroll.OnScroll(pointer);
                break;
            case EventTriggerType.BeginDrag:
                parentScroll.OnBeginDrag(pointer);
                break;
            case EventTriggerType.Drag:
                parentScroll.OnDrag(pointer);
                break;
            case EventTriggerType.EndDrag:
                parentScroll.OnEndDrag(pointer);
                break;
        }
    }

    private void OnAddClicked()
    {
        if (busy) return;
        if (addInput == null || string.IsNullOrWhiteSpace(addInput.text))
        {
            SetNotice(T("추가할 알람 대사를 입력해주세요."));
            return;
        }
        string message = addInput.text.Trim();
        addInput.SetTextWithoutNotify("");
        StartCoroutine(SynthesizeAndSave(new List<string> { message }));
    }

    private void OnSampleClicked()
    {
        if (!busy && !string.IsNullOrWhiteSpace(characterName))
        {
            StartCoroutine(RequestAndPlayVoiceSample());
        }
    }

    private void OnGenerateClicked()
    {
        if (!busy && !string.IsNullOrWhiteSpace(characterName))
        {
            StartCoroutine(RequestAlarmCandidates());
        }
    }

    private void OnRandomConceptClicked()
    {
        if (busy || conceptInput == null || RandomAlarmConcepts.Length == 0)
        {
            return;
        }

        string uiLanguage =
            CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage();
        UpdateConceptUiLanguage(uiLanguage);
        int index = Random.Range(0, RandomAlarmConcepts.Length);
        conceptInput.SetTextWithoutNotify(
            GetLocalizedConcept(RandomAlarmConcepts[index], uiLanguage));
    }

    private IEnumerator RequestAlarmCandidates()
    {
        language = GetCurrentSoundLanguage();
        string uiLanguage =
            CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage();
        UpdateConceptUiLanguage(uiLanguage);
        SetBusy(true, "");
        StartLoadingNotice(T("알람 대사를 생성 중입니다"));
        string baseUrl = null;
        yield return ResolveVoiceBaseUrl(value => baseUrl = value);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Fail(T("알람 대사 생성 서버에 연결할 수 없습니다."));
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddField("character_name", characterName);
        form.AddField("lang", uiLanguage);
        form.AddField("num_alarms", GeneratedChoiceCount);
        form.AddField(
            "custom_request",
            conceptInput != null ? conceptInput.text.Trim() : string.Empty);
        form.AddField("player_name", GetPlayerName());
        using (UnityWebRequest request =
               UnityWebRequest.Post(baseUrl.TrimEnd('/') + "/agent/alarm/make", form))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[CharacterVoiceAlarm] alarm maker failed. code={request.responseCode}, error={request.error}");
                Fail(T("알람 대사 생성에 실패했습니다. 다시 시도해주세요."));
                yield break;
            }

            List<string> candidates = ParseAlarmMessages(request.downloadHandler.text);
            if (candidates.Count == 0)
            {
                Fail(T("알람 대사 생성 결과가 올바른 JSON 리스트가 아닙니다."));
                yield break;
            }

            SetBusy(false, "");
            CharacterVoiceViewLauncher.ShowAlarmConfirm(
                candidates,
                characterName,
                refId,
                language,
                speed,
                OnCandidatesConfirmed);
        }
    }

    private static List<string> ParseAlarmMessages(string json)
    {
        List<string> result = new List<string>();
        try
        {
            JObject response = JObject.Parse(json);
            JToken list = response["alarm_messages"];
            if (!string.Equals(
                    response.Value<string>("status"),
                    "success",
                    System.StringComparison.OrdinalIgnoreCase) ||
                list == null ||
                list.Type != JTokenType.Array)
            {
                return result;
            }

            foreach (JToken token in list.Children())
            {
                if (token.Type != JTokenType.String) continue;
                string value = token.Value<string>();
                if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value.Trim()))
                {
                    result.Add(value.Trim());
                }
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError("[CharacterVoiceAlarm] invalid alarm maker JSON: " + exception.Message);
        }
        return result;
    }

    private void OnCandidatesConfirmed(
        List<CharacterVoiceAlarmConfirmView.PreparedAlarm> selected)
    {
        if (selected == null || selected.Count == 0)
        {
            return;
        }

        int saved = 0;
        int failed = 0;
        foreach (CharacterVoiceAlarmConfirmView.PreparedAlarm alarm in selected)
        {
            if (alarm != null &&
                alarm.wav != null &&
                alarm.wav.Length > 0 &&
                CharacterAlarmVoiceRepository.AddGeneratedAlarm(
                    characterName,
                    alarm.text,
                    alarm.wav,
                    refId,
                    alarm.language) != null)
            {
                saved++;
            }
            else
            {
                failed++;
            }
        }

        SetNotice(failed == 0
            ? string.Format(T("{0}개의 알람 음성을 추가했습니다."), saved)
            : string.Format(T("{0}개 추가, {1}개 저장 실패"), saved, failed));
        ReloadKeepingNotice();
    }

    private IEnumerator SynthesizeAndSave(List<string> messages)
    {
        SetBusy(true, T("선택한 알람 음성을 생성 중입니다."));
        int saved = 0;
        int failed = 0;
        for (int i = 0; i < messages.Count; i++)
        {
            string message = messages[i];
            byte[] wav = null;
            string speechLanguage = null;
            yield return RequestTts(message, (value, usedLanguage) =>
            {
                wav = value;
                speechLanguage = usedLanguage;
            });
            if (wav != null &&
                CharacterAlarmVoiceRepository.AddGeneratedAlarm(
                    characterName,
                    message,
                    wav,
                    refId,
                    speechLanguage) != null)
            {
                saved++;
            }
            else
            {
                failed++;
            }
        }

        SetBusy(false, failed == 0
            ? string.Format(T("{0}개의 알람 음성을 저장했습니다."), saved)
            : string.Format(T("{0}개 저장, {1}개 생성 실패"), saved, failed));
        ReloadKeepingNotice();
    }

    private IEnumerator RegenerateVoice(CharacterAlarmPlaybackCandidate candidate)
    {
        SetBusy(true, T("알람 음성을 다시 생성 중입니다."));
        byte[] wav = null;
        string speechLanguage = null;
        yield return RequestTts(candidate.message, (value, usedLanguage) =>
        {
            wav = value;
            speechLanguage = usedLanguage;
        });
        if (wav == null)
        {
            Fail(T("알람 음성 재생성에 실패했습니다."));
            yield break;
        }

        bool saved;
        if (candidate.isGenerated && candidate.generatedRecord != null)
        {
            saved = CharacterAlarmVoiceRepository.ReplaceGeneratedAlarmAudio(
                characterName,
                candidate.generatedRecord.id,
                wav,
                refId,
                speechLanguage);
        }
        else
        {
            CharacterAlarmVoiceRecord replacement =
                CharacterAlarmVoiceRepository.AddGeneratedAlarm(
                    characterName,
                    candidate.message,
                    wav,
                    refId,
                    speechLanguage);
            bool activationSaved =
                replacement != null &&
                (candidate.enabled ||
                 CharacterAlarmVoiceRepository.SetGeneratedAlarmEnabled(
                     characterName,
                     replacement.id,
                     false));
            saved = activationSaved &&
                    CharacterAlarmVoiceRepository.SetDefaultAlarmHidden(
                        characterName,
                        candidate.id,
                        true);
            if (!saved && replacement != null)
            {
                CharacterAlarmVoiceRepository.DeleteGeneratedAlarm(
                    characterName,
                    replacement.id);
            }
        }

        if (!saved)
        {
            Fail(T("알람 음성을 저장하지 못했습니다."));
            yield break;
        }

        SetBusy(false, T("알람 음성을 다시 생성했습니다."));
        ReloadKeepingNotice();
    }

    private IEnumerator RequestTts(
        string message,
        System.Action<byte[], string> completed)
    {
        string baseUrl = null;
        yield return ResolveVoiceBaseUrl(value => baseUrl = value);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            completed?.Invoke(null, null);
            yield break;
        }

        CharacterVoiceSpeechText speechText = null;
        yield return CharacterVoiceSpeechTextResolver.Resolve(
            baseUrl,
            message,
            CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage(),
            GetCurrentSoundLanguage(),
            value => speechText = value);
        if (speechText == null)
        {
            completed?.Invoke(null, null);
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
            request.uploadHandler =
                new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestData)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success ||
                request.downloadHandler.data == null ||
                request.downloadHandler.data.Length == 0)
            {
                Debug.LogError(
                    $"[CharacterVoiceAlarm] TTS failed. code={request.responseCode}, error={request.error}");
                completed?.Invoke(null, speechText.speechLanguage);
            }
            else
            {
                completed?.Invoke(
                    request.downloadHandler.data,
                    speechText.speechLanguage);
            }
        }
    }

    private IEnumerator RequestAndPlayVoiceSample()
    {
        SetBusy(true, T("샘플 음성을 불러오는 중입니다."));
        string baseUrl = null;
        yield return ResolveVoiceBaseUrl(value => baseUrl = value);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Fail(T("샘플 음성 서버에 연결할 수 없습니다."));
            yield break;
        }

        bool useSelectedReference = HasVoiceSelection();
        string url = baseUrl.TrimEnd('/') +
                     (useSelectedReference ? "/getSampleVoice" : "/getSound");
        Dictionary<string, string> requestData;
        string sampleDisplayText = null;
        if (useSelectedReference)
        {
            requestData = new Dictionary<string, string> { { "ref_id", refId } };
        }
        else
        {
            string uiLanguage =
                CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage();
            sampleDisplayText = GetSampleMessage(uiLanguage);
            CharacterVoiceSpeechText speechText = null;
            yield return CharacterVoiceSpeechTextResolver.Resolve(
                baseUrl,
                sampleDisplayText,
                uiLanguage,
                GetCurrentSoundLanguage(),
                value => speechText = value);
            if (speechText == null)
            {
                Fail(T("샘플 음성을 불러오지 못했습니다."));
                yield break;
            }
            requestData = new Dictionary<string, string>
            {
                { "text", speechText.speechText },
                { "char", characterName },
                { "lang", speechText.speechLanguage },
                { "speed", speed },
                { "chatIdx", "-1" }
            };
        }
        string body = JsonConvert.SerializeObject(requestData);
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail(T("샘플 음성을 불러오지 못했습니다."));
                yield break;
            }

            PlayClip(
                DownloadHandlerAudioClip.GetContent(request),
                sampleDisplayText);
        }
        SetBusy(false, "");
    }

    private void PlayCandidate(CharacterAlarmPlaybackCandidate candidate)
    {
        if (candidate.audioClip != null)
        {
            PlayClip(candidate.audioClip, candidate.message);
        }
        else if (!string.IsNullOrWhiteSpace(candidate.audioFilePath) &&
                 File.Exists(candidate.audioFilePath))
        {
            StartCoroutine(LoadAndPlay(candidate.audioFilePath, candidate.message));
        }
    }

    private IEnumerator LoadAndPlay(string path, string message)
    {
        using (UnityWebRequest request =
               UnityWebRequestMultimedia.GetAudioClip(new System.Uri(path).AbsoluteUri, AudioType.WAV))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                PlayClip(DownloadHandlerAudioClip.GetContent(request), message);
            }
            else
            {
                SetNotice(T("저장된 알람 음성을 재생하지 못했습니다."));
            }
        }
    }

    private void PlayClip(AudioClip clip, string message)
    {
        if (clip == null) return;
        if (previewAudioSource == null)
        {
            previewAudioSource = gameObject.AddComponent<AudioSource>();
            previewAudioSource.playOnAwake = false;
        }
        previewAudioSource.Stop();
        previewAudioSource.clip = clip;
        previewAudioSource.loop = false;
        previewAudioSource.volume = GetMasterVolume();
        previewAudioSource.Play();

        if (!string.IsNullOrWhiteSpace(message) && AnswerBalloonSimpleManager.Instance != null)
        {
            AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleForSeconds(
                message, clip.length + 0.5f);
        }
    }

    private static bool HasPlayableAudio(CharacterAlarmPlaybackCandidate candidate)
    {
        return candidate != null &&
               (candidate.audioClip != null ||
                (!string.IsNullOrWhiteSpace(candidate.audioFilePath) &&
                 File.Exists(candidate.audioFilePath)));
    }

    private bool HasVoiceSelection()
    {
        return !string.IsNullOrWhiteSpace(refId);
    }

    private IEnumerator ResolveVoiceBaseUrl(System.Action<string> completed)
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

    private void SetBusy(bool value, string notice)
    {
        StopLoadingNotice();
        busy = value;
        SetNotice(notice);
        RefreshButtons();
    }

    private void Fail(string message)
    {
        StopLoadingNotice();
        busy = false;
        SetNotice(message);
        RefreshButtons();
    }

    private void ReloadKeepingNotice()
    {
        string message = noticeText != null ? noticeText.text : string.Empty;
        Reload();
        SetNotice(message);
    }

    private void RefreshButtons()
    {
        bool hasCharacter = !string.IsNullOrWhiteSpace(characterName);
        if (sampleButton != null) sampleButton.interactable = !busy && hasCharacter;
        if (randomConceptButton != null)
        {
            randomConceptButton.interactable = !busy;
        }
        if (generateButton != null)
        {
            generateButton.interactable = !busy && hasCharacter;
        }
        if (addButton != null)
        {
            addButton.interactable = !busy && hasCharacter;
        }
        foreach (GameObject row in rows)
        {
            if (row == null) continue;
            Button regenerate = MemoryArchiveUi.FindComponent<Button>(row.transform, "RegenerateButton");
            if (regenerate != null) regenerate.interactable = !busy;
        }
    }

    private void SetNotice(string value)
    {
        if (noticeText != null) noticeText.text = value ?? string.Empty;
    }

    private void StartLoadingNotice(string baseMessage)
    {
        StopLoadingNotice();
        loadingNoticeCoroutine = StartCoroutine(AnimateLoadingNotice(baseMessage));
    }

    private void StopLoadingNotice()
    {
        if (loadingNoticeCoroutine == null)
        {
            return;
        }

        StopCoroutine(loadingNoticeCoroutine);
        loadingNoticeCoroutine = null;
    }

    private IEnumerator AnimateLoadingNotice(string baseMessage)
    {
        int dotCount = 1;
        while (busy)
        {
            SetNotice((baseMessage ?? string.Empty) + new string('.', dotCount));
            dotCount = dotCount == 3 ? 1 : dotCount + 1;
            yield return new WaitForSecondsRealtime(0.4f);
        }
        loadingNoticeCoroutine = null;
    }

    private void EnsureBuilt()
    {
        if (built) return;
        if (MemoryArchiveUi.FindDeepChild(transform, "AlarmVoiceBody") != null)
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
            : MemoryArchiveUi.FindDeepChild(transform, "AlarmVoiceContent");
        Transform template = rowTemplate != null
            ? rowTemplate.transform
            : MemoryArchiveUi.FindDeepChild(transform, "AlarmVoiceRowTemplate");
        rowTemplate = template != null ? template.gameObject : null;
        conceptInput = conceptInput != null
            ? conceptInput
            : MemoryArchiveUi.FindComponent<TMP_InputField>(transform, "ConceptInput");
        addInput = addInput != null
            ? addInput
            : MemoryArchiveUi.FindComponent<TMP_InputField>(transform, "AddInput");
        characterText = characterText != null
            ? characterText
            : MemoryArchiveUi.FindComponent<TMP_Text>(transform, "CharacterText");
        countText = countText != null
            ? countText
            : MemoryArchiveUi.FindComponent<TMP_Text>(transform, "CountText");
        emptyText = emptyText != null
            ? emptyText
            : MemoryArchiveUi.FindComponent<TMP_Text>(transform, "EmptyText");
        noticeText = noticeText != null
            ? noticeText
            : MemoryArchiveUi.FindComponent<TMP_Text>(transform, "NoticeText");
        sampleButton = sampleButton != null
            ? sampleButton
            : MemoryArchiveUi.FindComponent<Button>(transform, "AlarmSamplePlayButton");
        randomConceptButton = randomConceptButton != null
            ? randomConceptButton
            : MemoryArchiveUi.FindComponent<Button>(transform, "RandomConceptButton");
        generateButton = generateButton != null
            ? generateButton
            : MemoryArchiveUi.FindComponent<Button>(transform, "AlarmGenerateButton");
        addButton = addButton != null
            ? addButton
            : MemoryArchiveUi.FindComponent<Button>(transform, "AddButton");
        if (rowTemplate != null) rowTemplate.SetActive(false);
        BindButton("CloseButton", Hide);
        BindButton("AlarmSamplePlayButton", OnSampleClicked);
        BindButton("RandomConceptButton", OnRandomConceptClicked);
        BindButton("AlarmGenerateButton", OnGenerateClicked);
        BindButton("AddButton", OnAddClicked);
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

        BuildHeader();
        BuildConceptRow();
        BuildBody();
        BuildDirectAddRow();
        noticeText = MemoryArchiveUi.CreateText(
            "NoticeText", transform, "", 12f, MemoryArchiveUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.Layout(noticeText.gameObject, minH: 20f, prefH: 20f, flexH: 0f);
    }

    private void BuildHeader()
    {
        GameObject header = MemoryArchiveUi.CreateUIObject("Header", transform);
        MemoryArchiveUi.Layout(header, minH: 52f, prefH: 52f, flexH: 0f);
        MemoryArchiveUi.AddRow(header, 8f).childForceExpandHeight = true;
        TMP_Text title = MemoryArchiveUi.CreateText(
            "TitleText", header.transform, "Alarm Voice", 20f, MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.Layout(title.gameObject, minW: 180f, flexW: 1f);
        characterText = MemoryArchiveUi.CreateText(
            "CharacterText", header.transform, "", 13f, MemoryArchiveUi.TextMuted,
            TextAlignmentOptions.MidlineRight);
        characterText.overflowMode = TextOverflowModes.Ellipsis;
        MemoryArchiveUi.Layout(characterText.gameObject, minW: 100f, prefW: 100f);
        sampleButton = MemoryArchiveUi.CreateButton(
            "AlarmSamplePlayButton", header.transform, "샘플 듣기", MemoryArchiveUi.PanelBg2, 12f);
        MemoryArchiveUi.Layout(sampleButton.gameObject, minW: 78f, prefW: 78f);
        sampleButton.onClick.AddListener(OnSampleClicked);
        Button close = MemoryArchiveUi.CreateButton(
            "CloseButton", header.transform, "×", MemoryArchiveUi.HeaderBg, 24f);
        MemoryArchiveUi.Layout(close.gameObject, minW: 40f, prefW: 40f);
        close.onClick.AddListener(Hide);
    }

    private void BuildConceptRow()
    {
        GameObject row = MemoryArchiveUi.CreatePanel("ConceptRow", transform, MemoryArchiveUi.PanelBg);
        MemoryArchiveUi.Layout(row, minH: 46f, prefH: 46f, flexH: 0f);
        MemoryArchiveUi.AddRow(row, 6f, new RectOffset(5, 5, 5, 5)).childForceExpandHeight = true;
        conceptInput = CreateInput("ConceptInput", row.transform, "컨셉을 적어주세요");
        MemoryArchiveUi.Layout(conceptInput.gameObject, flexW: 1f);
        randomConceptButton = MemoryArchiveUi.CreateButton(
            "RandomConceptButton", row.transform, "랜덤", MemoryArchiveUi.PanelBg2, 13f);
        MemoryArchiveUi.Layout(randomConceptButton.gameObject, minW: 62f, prefW: 62f);
        randomConceptButton.onClick.AddListener(OnRandomConceptClicked);
        generateButton = MemoryArchiveUi.CreateButton(
            "AlarmGenerateButton", row.transform, "생성", MemoryArchiveUi.Accent, 13f);
        MemoryArchiveUi.Layout(generateButton.gameObject, minW: 62f, prefW: 62f);
        generateButton.onClick.AddListener(OnGenerateClicked);
    }

    private void BuildDirectAddRow()
    {
        GameObject row = MemoryArchiveUi.CreatePanel("DirectAddRow", transform, MemoryArchiveUi.PanelBg);
        MemoryArchiveUi.Layout(row, minH: 46f, prefH: 46f, flexH: 0f);
        MemoryArchiveUi.AddRow(row, 6f, new RectOffset(5, 5, 5, 5)).childForceExpandHeight = true;
        addInput = CreateInput("AddInput", row.transform, "직접 알람 대사 추가");
        MemoryArchiveUi.Layout(addInput.gameObject, flexW: 1f);
        addButton = MemoryArchiveUi.CreateButton(
            "AddButton", row.transform, "추가", MemoryArchiveUi.Accent, 13f);
        MemoryArchiveUi.Layout(addButton.gameObject, minW: 62f, prefW: 62f);
        addButton.onClick.AddListener(OnAddClicked);
    }

    private void BuildBody()
    {
        GameObject body = MemoryArchiveUi.CreatePanel(
            "AlarmVoiceBody", transform, MemoryArchiveUi.PanelBg);
        MemoryArchiveUi.Layout(body, flexH: 1f);
        ScrollRect scroll = body.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;
        GameObject viewport = MemoryArchiveUi.CreateUIObject("Viewport", body.transform);
        MemoryArchiveUi.SetStretch(viewport, new Vector4(4f, 4f, 18f, 4f));
        viewport.AddComponent<RectMask2D>();
        GameObject content = MemoryArchiveUi.CreateUIObject("AlarmVoiceContent", viewport.transform);
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
        rowTemplate = BuildRowTemplate(listContent);
        emptyText = MemoryArchiveUi.CreateText(
            "EmptyText", body.transform, "저장되거나 매핑된 알람 대사가 없습니다.", 14f,
            MemoryArchiveUi.TextMuted, TextAlignmentOptions.Center);
        MemoryArchiveUi.SetStretch(emptyText.gameObject, new Vector4(36f, 36f, 36f, 36f));
    }

    private GameObject BuildRowTemplate(Transform parent)
    {
        GameObject row = MemoryArchiveUi.CreatePanel(
            "AlarmVoiceRowTemplate", parent, MemoryArchiveUi.PanelBg2);
        row.AddComponent<CanvasGroup>();
        MemoryArchiveUi.Layout(row, minH: 66f, prefH: 66f, flexH: 0f);
        HorizontalLayoutGroup layout = MemoryArchiveUi.AddRow(
            row, 6f, new RectOffset(8, 8, 8, 8));
        layout.childForceExpandHeight = true;
        Button delete = MemoryArchiveUi.CreateButton(
            "DeleteButton", row.transform, "", new Color(0.62f, 0.08f, 0.11f, 1f), 12f);
        MemoryArchiveUi.Layout(delete.gameObject, minW: 44f, prefW: 44f);
        BuildTrashIcon(delete.transform);
        delete.gameObject.SetActive(false);
        TMP_Text message = MemoryArchiveUi.CreateText(
            "AlarmMessageText", row.transform, "", 13f, MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        message.textWrappingMode = TextWrappingModes.Normal;
        message.overflowMode = TextOverflowModes.Ellipsis;
        MemoryArchiveUi.Layout(message.gameObject, flexW: 1f);
        TMP_InputField editInput = CreateInput("EditInput", row.transform, "");
        MemoryArchiveUi.Layout(editInput.gameObject, flexW: 1f);
        editInput.gameObject.SetActive(false);
        Button regenerate = MemoryArchiveUi.CreateButton(
            "RegenerateButton", row.transform, "재생성", MemoryArchiveUi.PanelBg, 12f);
        MemoryArchiveUi.Layout(regenerate.gameObject, minW: 68f, prefW: 68f);
        Button play = MemoryArchiveUi.CreateButton(
            "PlayButton", row.transform, "듣기", MemoryArchiveUi.Accent, 12f);
        MemoryArchiveUi.Layout(play.gameObject, minW: 58f, prefW: 58f);
        row.SetActive(false);
        return row;
    }

    private static void BuildTrashIcon(Transform parent)
    {
        GameObject icon = MemoryArchiveUi.CreateUIObject("TrashIcon", parent);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(18f, 20f);
        iconRect.anchoredPosition = Vector2.zero;

        CreateTrashIconPart(
            "Body", icon.transform, new Vector2(0f, -2f), new Vector2(12f, 13f));
        CreateTrashIconPart(
            "Lid", icon.transform, new Vector2(0f, 6f), new Vector2(16f, 3f));
        CreateTrashIconPart(
            "Handle", icon.transform, new Vector2(0f, 9f), new Vector2(7f, 2f));
    }

    private static void CreateTrashIconPart(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size)
    {
        GameObject part = MemoryArchiveUi.CreateUIObject(name, parent);
        RectTransform rect = part.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = part.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static TMP_InputField CreateInput(string name, Transform parent, string placeholderValue)
    {
        GameObject root = MemoryArchiveUi.CreatePanel(name, parent, MemoryArchiveUi.PanelBg);
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        GameObject area = MemoryArchiveUi.CreateUIObject("Text Area", root.transform);
        MemoryArchiveUi.SetStretch(area, new Vector4(8f, 2f, 8f, 2f));
        area.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = MemoryArchiveUi.CreateText(
            "Placeholder", area.transform, placeholderValue, 13f, MemoryArchiveUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.SetStretch(placeholder.gameObject, Vector4.zero);
        TextMeshProUGUI text = MemoryArchiveUi.CreateText(
            "Text", area.transform, "", 13f, MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.SetStretch(text.gameObject, Vector4.zero);
        input.textViewport = area.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
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
        GameObject handle = MemoryArchiveUi.CreatePanel("Handle", area.transform, MemoryArchiveUi.ScrollHandle);
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
        if (string.Equals(value, "jp", System.StringComparison.OrdinalIgnoreCase))
        {
            return "ja";
        }
        return string.IsNullOrWhiteSpace(value) ? "ko" : value;
    }

    private string GetCurrentSoundLanguage()
    {
        try
        {
            if (SettingManager.Instance != null &&
                SettingManager.Instance.settings != null &&
                !string.IsNullOrWhiteSpace(SettingManager.Instance.settings.sound_language))
            {
                return NormalizeLanguage(SettingManager.Instance.settings.sound_language);
            }
        }
        catch
        {
            // 설정 매니저 초기화 전에는 CharacterDetail에서 전달받은 값을 유지한다.
        }

        return NormalizeLanguage(language);
    }

    private static string GetPlayerName()
    {
        try
        {
            if (SettingManager.Instance != null &&
                SettingManager.Instance.settings != null &&
                !string.IsNullOrWhiteSpace(SettingManager.Instance.settings.player_name))
            {
                return SettingManager.Instance.settings.player_name.Trim();
            }
        }
        catch
        {
            // 설정 매니저 초기화 전에는 서버 기본값을 사용한다.
        }

        return "선생님";
    }

    private void UpdateConceptUiLanguage(string targetLanguage = null)
    {
        if (conceptInput == null || conceptInput.placeholder == null)
        {
            return;
        }

        TMP_Text placeholder = conceptInput.placeholder as TMP_Text;
        if (placeholder == null)
        {
            return;
        }

        placeholder.text = LanguageDataCharacterVoiceAlarm.Translate(
            "컨셉을 적어주세요",
            targetLanguage ??
            CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage());
    }

    private static string GetLocalizedConcept(string[] concept, string targetLanguage)
    {
        if (concept == null || concept.Length < 3)
        {
            return string.Empty;
        }

        switch (NormalizeLanguage(targetLanguage))
        {
            case "ja":
                return concept[1];
            case "en":
                return concept[2];
            default:
                return concept[0];
        }
    }

    private static string GetSampleMessage(string targetLanguage)
    {
        return LanguageDataCharacterVoiceAlarm.Translate(
            "시간이 되었습니다.",
            targetLanguage);
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
        SetLocalizedText(transform, "TitleText", "Alarm Voice");
        SetLocalizedButtonLabel(
            transform,
            "AlarmSamplePlayButton",
            "샘플 듣기");
        SetLocalizedButtonLabel(transform, "RandomConceptButton", "랜덤");
        SetLocalizedButtonLabel(transform, "AlarmGenerateButton", "생성");
        SetLocalizedButtonLabel(transform, "AddButton", "추가");
        SetLocalizedInputPlaceholder(conceptInput, "컨셉을 적어주세요");
        SetLocalizedInputPlaceholder(addInput, "직접 알람 대사 추가");
        if (emptyText != null)
        {
            emptyText.text = T("저장되거나 매핑된 알람 대사가 없습니다.");
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

    private static void SetLocalizedText(
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

    private static void SetLocalizedButtonLabel(
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

    private static void SetLocalizedInputPlaceholder(
        TMP_InputField input,
        string source)
    {
        TMP_Text placeholder = input != null
            ? input.placeholder as TMP_Text
            : null;
        if (placeholder != null)
        {
            placeholder.text = T(source);
        }
    }

    private static string T(string value)
    {
        return LanguageDataCharacterVoiceAlarm.Translate(value);
    }
}
