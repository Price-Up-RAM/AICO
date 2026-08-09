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

// 캐릭터별 기본/생성 포모도로 대사를 생성·추가·편집·재생하는 독립 화면.
public class CharacterVoicePomodoroView : MonoBehaviour
{
    private const int GeneratedChoiceCount = 3;
    private const float RowLongPressSeconds = 0.65f;
    private static readonly Color EnabledRowColor =
        new Color(0.145f, 0.205f, 0.175f, 1f);
    private static readonly Color DisabledRowColor =
        new Color(0.105f, 0.115f, 0.135f, 1f);
    private const float DisabledRowAlpha = 0.72f;
    private static readonly string[][] RandomPomodoroConcepts =
    {
        new[] { "차분하게 집중을 시작하도록 격려", "落ち着いて集中を始めるよう励ます", "Calmly encourage the user to begin focusing" },
        new[] { "활기차게 집중 시작을 알림", "元気に集中開始を知らせる", "Energetically announce the start of focus time" },
        new[] { "끝에 냥을 붙이는 귀여운 집중 응원", "語尾に「にゃん」を付ける可愛い集中応援", "A cute focus message ending with meow" },
        new[] { "엄격한 선생님처럼 집중을 독려", "厳しい先生のように集中を促す", "Encourage focus like a strict teacher" },
        new[] { "다정한 동료처럼 집중을 응원", "優しい仲間のように集中を応援する", "Support focus like a caring companion" },
        new[] { "게임 퀘스트를 시작하는 느낌", "ゲームのクエストを始めるような雰囲気", "Sound like starting a game quest" },
        new[] { "짧고 사무적인 집중 안내", "短く事務的な集中案内", "A short and professional focus notice" },
        new[] { "과장되고 코믹한 집중 선언", "大げさでコミカルな集中宣言", "An exaggerated comedic focus declaration" },
        new[] { "휴식 종료 후 부드러운 복귀 안내", "休憩後の優しい集中復帰案内", "A gentle return-to-focus message after a break" },
        new[] { "목표 달성을 강조하는 진지한 응원", "目標達成を強調する真剣な応援", "Serious encouragement emphasizing goal completion" }
    };

    [SerializeField] private Vector2 panelSize = new Vector2(760f, 560f);
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
    private CharacterPomodoroVoiceCatalog catalog;
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
        CharacterPomodoroVoiceCatalog pomodoroCatalog)
    {
        EnsureBuilt();
        ApplyLocalizedStaticText();
        characterName = targetCharacterName != null ? targetCharacterName.Trim() : string.Empty;
        refId = selectedRefId ?? string.Empty;
        language = NormalizeLanguage(soundLanguage);
        speed = string.IsNullOrWhiteSpace(soundSpeed) ? "100" : soundSpeed;
        catalog = pomodoroCatalog != null
            ? pomodoroCatalog
            : CharacterPomodoroVoiceCatalog.LoadDefault();
        busy = false;
        UpdateConceptPlaceholder();
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
        if (previewAudioSource != null) previewAudioSource.Stop();
        gameObject.SetActive(false);
    }

    public void Reload()
    {
        EnsureBuilt();
        CancelRowLongPress();
        ResetEditingState();
        ClearRows();
        List<CharacterPomodoroPlaybackCandidate> candidates =
            CharacterPomodoroVoiceRepository.GetDisplayCandidates(characterName, catalog);
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterPomodoroPlaybackCandidate candidate = candidates[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.message)) continue;
            GameObject row = Instantiate(rowTemplate, listContent);
            row.name = "PomodoroVoiceRow_" + candidate.id;
            row.SetActive(true);
            BindRow(row, candidate);
            rows.Add(row);
        }

        if (characterText != null) characterText.text = characterName;
        if (countText != null) countText.text = rows.Count.ToString();
        if (emptyText != null) emptyText.gameObject.SetActive(rows.Count == 0);
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

    private void BindRow(
        GameObject row,
        CharacterPomodoroPlaybackCandidate candidate)
    {
        TMP_Text message = MemoryArchiveUi.FindComponent<TMP_Text>(
            row.transform, "PomodoroMessageText");
        TMP_InputField editInput = MemoryArchiveUi.FindComponent<TMP_InputField>(
            row.transform, "EditInput");
        Button delete = MemoryArchiveUi.FindComponent<Button>(
            row.transform, "DeleteButton");
        Button regenerate = MemoryArchiveUi.FindComponent<Button>(
            row.transform, "RegenerateButton");
        Button play = MemoryArchiveUi.FindComponent<Button>(
            row.transform, "PlayButton");
        TMP_Dropdown situationDropdown =
            MemoryArchiveUi.FindComponent<TMP_Dropdown>(
                row.transform,
                "SituationDropdown");

        if (message != null) message.text = candidate.message;
        if (editInput != null)
        {
            editInput.SetTextWithoutNotify(candidate.message);
            editInput.onEndEdit.RemoveAllListeners();
            editInput.onEndEdit.AddListener(
                value => OnRowEditFinished(row, candidate, value));
            editInput.gameObject.SetActive(false);
        }
        if (delete != null)
        {
            delete.gameObject.SetActive(false);
            delete.onClick.RemoveAllListeners();
            delete.onClick.AddListener(() => DeleteDialogue(row, candidate));
            AddEventTrigger(
                delete.gameObject,
                EventTriggerType.PointerDown,
                _ => suppressEditCommit = true,
                false);
            AddScrollForwarding(delete.gameObject);
        }
        if (regenerate != null)
        {
            regenerate.onClick.RemoveAllListeners();
            regenerate.onClick.AddListener(
                () =>
                {
                    if (!busy) StartCoroutine(RegenerateVoice(candidate));
                });
        }
        ConfigureSituationDropdown(situationDropdown, candidate);
        if (play != null)
        {
            play.interactable = !busy && HasPlayableAudio(candidate);
            play.onClick.RemoveAllListeners();
            play.onClick.AddListener(() => PlayCandidate(candidate));
        }
        ApplyRowActiveVisual(row, candidate.enabled);
        ConfigureLongPress(row, candidate);
    }

    private void ConfigureSituationDropdown(
        TMP_Dropdown dropdown,
        CharacterPomodoroPlaybackCandidate candidate)
    {
        if (dropdown == null || candidate == null)
        {
            return;
        }

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>
        {
            T("아무때나"),
            T("준비"),
            T("집중"),
            T("휴식")
        });
        int value = Mathf.Clamp(
            (int)candidate.situation,
            (int)PomodoroVoiceSituation.Anytime,
            (int)PomodoroVoiceSituation.Break);
        dropdown.SetValueWithoutNotify(value);
        dropdown.RefreshShownValue();
        dropdown.interactable = !busy;
        dropdown.onValueChanged.AddListener(
            selected => OnSituationChanged(dropdown, candidate, selected));
    }

    private void OnSituationChanged(
        TMP_Dropdown dropdown,
        CharacterPomodoroPlaybackCandidate candidate,
        int selectedIndex)
    {
        if (busy || dropdown == null || candidate == null)
        {
            return;
        }

        PomodoroVoiceSituation previous = candidate.situation;
        PomodoroVoiceSituation selected =
            (PomodoroVoiceSituation)Mathf.Clamp(
                selectedIndex,
                (int)PomodoroVoiceSituation.Anytime,
                (int)PomodoroVoiceSituation.Break);
        if (selected == previous)
        {
            return;
        }

        bool saved = candidate.isGenerated &&
                     candidate.generatedRecord != null
            ? CharacterPomodoroVoiceRepository
                .UpdateGeneratedDialogueSituation(
                    characterName,
                    candidate.generatedRecord.id,
                    selected)
            : CharacterPomodoroVoiceRepository
                .UpdateDefaultDialogueSituation(
                    characterName,
                    candidate.id,
                    selected);
        if (saved)
        {
            candidate.situation = selected;
            if (candidate.generatedRecord != null)
            {
                candidate.generatedRecord.situation = selected;
            }
            SetNotice("");
            return;
        }

        dropdown.SetValueWithoutNotify((int)previous);
        dropdown.RefreshShownValue();
        SetNotice(T("포모도로 대사 상황을 저장하지 못했습니다."));
    }

    private void OnAddClicked()
    {
        if (busy) return;
        if (addInput == null || string.IsNullOrWhiteSpace(addInput.text))
        {
            SetNotice(T("추가할 포모도로 대사를 입력해주세요."));
            return;
        }
        string message = addInput.text.Trim();
        addInput.SetTextWithoutNotify("");
        StartCoroutine(SynthesizeAndSave(new List<string> { message }));
    }

    private void OnGenerateClicked()
    {
        if (!busy && !string.IsNullOrWhiteSpace(characterName))
        {
            StartCoroutine(RequestPomodoroCandidates());
        }
    }

    private void OnRandomConceptClicked()
    {
        if (busy || conceptInput == null) return;
        string uiLanguage =
            CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage();
        UpdateConceptPlaceholder(uiLanguage);
        int index = Random.Range(0, RandomPomodoroConcepts.Length);
        conceptInput.SetTextWithoutNotify(
            GetLocalizedConcept(RandomPomodoroConcepts[index], uiLanguage));
    }

    private void OnSampleClicked()
    {
        if (!busy && !string.IsNullOrWhiteSpace(characterName))
        {
            StartCoroutine(RequestAndPlayVoiceSample());
        }
    }

    private IEnumerator RequestPomodoroCandidates()
    {
        language = GetCurrentSoundLanguage();
        string uiLanguage =
            CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage();
        UpdateConceptPlaceholder(uiLanguage);
        SetBusy(true, "");
        StartLoadingNotice(T("포모도로 대사를 생성 중입니다"));
        string baseUrl = null;
        yield return ResolveVoiceBaseUrl(value => baseUrl = value);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            Fail(T("포모도로 대사 생성 서버에 연결할 수 없습니다."));
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddField("character_name", characterName);
        form.AddField("lang", uiLanguage);
        form.AddField("num_dialogues", GeneratedChoiceCount);
        form.AddField(
            "custom_request",
            conceptInput != null ? conceptInput.text.Trim() : string.Empty);
        form.AddField("player_name", GetPlayerName());
        using (UnityWebRequest request = UnityWebRequest.Post(
                   baseUrl.TrimEnd('/') + "/agent/pomodoro/make",
                   form))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[CharacterVoicePomodoro] maker failed. code={request.responseCode}, error={request.error}");
                Fail(T("포모도로 대사 생성에 실패했습니다. 다시 시도해주세요."));
                yield break;
            }

            List<string> candidates = ParseDialogues(request.downloadHandler.text);
            if (candidates.Count == 0)
            {
                Fail(T("포모도로 대사 생성 결과가 올바른 JSON 리스트가 아닙니다."));
                yield break;
            }

            SetBusy(false, "");
            CharacterVoiceViewLauncher.ShowPomodoroConfirm(
                candidates,
                characterName,
                refId,
                language,
                speed,
                OnCandidatesConfirmed);
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

        bool hasReference = !string.IsNullOrWhiteSpace(refId);
        string url = baseUrl.TrimEnd('/') +
                     (hasReference ? "/getSampleVoice" : "/getSound");
        Dictionary<string, string> requestData;
        string sampleDisplayText = null;
        if (hasReference)
        {
            requestData =
                new Dictionary<string, string> { { "ref_id", refId } };
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
            if (request.result == UnityWebRequest.Result.Success)
            {
                PlayClip(
                    DownloadHandlerAudioClip.GetContent(request),
                    sampleDisplayText);
            }
            else
            {
                Fail(T("샘플 음성을 불러오지 못했습니다."));
                yield break;
            }
        }
        SetBusy(false, "");
    }

    private static List<string> ParseDialogues(string json)
    {
        List<string> result = new List<string>();
        try
        {
            JObject response = JObject.Parse(json);
            if (!string.Equals(
                    response.Value<string>("status"),
                    "success",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
            JToken list = response["dialogues"];
            if (list == null || list.Type != JTokenType.Array) return result;
            foreach (JToken item in list)
            {
                string value = item.Type == JTokenType.String
                    ? item.Value<string>()
                    : null;
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value.Trim());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[CharacterVoicePomodoro] invalid JSON: " + e.Message);
        }
        return result;
    }

    private void OnCandidatesConfirmed(
        List<CharacterVoiceAlarmConfirmView.PreparedAlarm> selected)
    {
        if (selected == null || selected.Count == 0) return;
        int saved = 0;
        int failed = 0;
        foreach (CharacterVoiceAlarmConfirmView.PreparedAlarm dialogue in selected)
        {
            if (dialogue != null &&
                dialogue.wav != null &&
                CharacterPomodoroVoiceRepository.AddGeneratedDialogue(
                    characterName,
                    dialogue.text,
                    dialogue.wav,
                    refId,
                    dialogue.language) != null)
            {
                saved++;
            }
            else
            {
                failed++;
            }
        }
        SetNotice(failed == 0
            ? string.Format(T("{0}개의 포모도로 음성을 추가했습니다."), saved)
            : string.Format(T("{0}개 추가, {1}개 저장 실패"), saved, failed));
        ReloadKeepingNotice();
    }

    private IEnumerator SynthesizeAndSave(List<string> messages)
    {
        SetBusy(true, T("포모도로 음성을 생성 중입니다."));
        int saved = 0;
        int failed = 0;
        foreach (string message in messages)
        {
            byte[] wav = null;
            string speechLanguage = null;
            yield return RequestTts(message, (value, usedLanguage) =>
            {
                wav = value;
                speechLanguage = usedLanguage;
            });
            if (wav != null &&
                CharacterPomodoroVoiceRepository.AddGeneratedDialogue(
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
            ? string.Format(T("{0}개의 포모도로 음성을 저장했습니다."), saved)
            : string.Format(T("{0}개 저장, {1}개 생성 실패"), saved, failed));
        ReloadKeepingNotice();
    }

    private IEnumerator RegenerateVoice(CharacterPomodoroPlaybackCandidate candidate)
    {
        SetBusy(true, T("포모도로 음성을 다시 생성 중입니다."));
        byte[] wav = null;
        string speechLanguage = null;
        yield return RequestTts(candidate.message, (value, usedLanguage) =>
        {
            wav = value;
            speechLanguage = usedLanguage;
        });
        if (wav == null)
        {
            Fail(T("포모도로 음성 재생성에 실패했습니다."));
            yield break;
        }

        bool saved;
        if (candidate.isGenerated && candidate.generatedRecord != null)
        {
            saved = CharacterPomodoroVoiceRepository.ReplaceGeneratedDialogue(
                characterName,
                candidate.generatedRecord.id,
                candidate.message,
                wav,
                refId,
                speechLanguage);
        }
        else
        {
            CharacterPomodoroVoiceRecord replacement =
                CharacterPomodoroVoiceRepository.AddGeneratedDialogue(
                    characterName,
                    candidate.message,
                    wav,
                    refId,
                    speechLanguage,
                    candidate.situation);
            bool activationSaved =
                replacement != null &&
                (candidate.enabled ||
                 CharacterPomodoroVoiceRepository.SetGeneratedDialogueEnabled(
                     characterName,
                     replacement.id,
                     false));
            saved = activationSaved &&
                    CharacterPomodoroVoiceRepository.SetDefaultDialogueHidden(
                        characterName, candidate.id, true);
            if (!saved && replacement != null)
            {
                CharacterPomodoroVoiceRepository.DeleteGeneratedDialogue(
                    characterName, replacement.id);
            }
        }
        if (!saved)
        {
            Fail(T("포모도로 음성을 저장하지 못했습니다."));
            yield break;
        }
        SetBusy(false, T("포모도로 음성을 다시 생성했습니다."));
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
        if (!string.IsNullOrWhiteSpace(refId)) requestData["ref_id"] = refId;
        string url = baseUrl.TrimEnd('/') + "/getSound";
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestData)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            byte[] data = request.result == UnityWebRequest.Result.Success
                ? request.downloadHandler.data
                : null;
            if (data == null || data.Length == 0)
            {
                Debug.LogError(
                    $"[CharacterVoicePomodoro] TTS failed. code={request.responseCode}, error={request.error}");
                completed?.Invoke(null, speechText.speechLanguage);
            }
            else
            {
                completed?.Invoke(data, speechText.speechLanguage);
            }
        }
    }

    private void ConfigureLongPress(
        GameObject row,
        CharacterPomodoroPlaybackCandidate candidate)
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
                CancelRowLongPress();
                rowPointerPosition = pointer.position;
                rowPointerActive = true;
                rowLongPressCoroutine = StartCoroutine(WaitForLongPress(
                    row,
                    candidate,
                    pointer.position));
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

    private IEnumerator WaitForLongPress(
        GameObject row,
        CharacterPomodoroPlaybackCandidate candidate,
        Vector2 startPosition)
    {
        float elapsed = 0f;
        while (elapsed < RowLongPressSeconds)
        {
            if (busy || !rowPointerActive ||
                Vector2.Distance(startPosition, rowPointerPosition) > 14f)
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
        EnterEditMode(row, candidate);
    }

    private void OnRowClicked(
        GameObject row,
        CharacterPomodoroPlaybackCandidate candidate,
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
            ? CharacterPomodoroVoiceRepository.SetGeneratedDialogueEnabled(
                characterName,
                candidate.generatedRecord.id,
                enabled)
            : CharacterPomodoroVoiceRepository.SetDefaultDialogueEnabled(
                characterName,
                candidate.id,
                enabled);
        if (!saved)
        {
            SetNotice(T("포모도로 사용 상태를 저장하지 못했습니다."));
            return;
        }

        candidate.enabled = enabled;
        ApplyRowActiveVisual(row, enabled);
        SetNotice(enabled
            ? T("이 포모도로 음성을 사용합니다.")
            : T("이 포모도로 음성을 사용하지 않습니다."));
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
                current.GetComponent<TMP_InputField>() != null ||
                current.GetComponent<TMP_Dropdown>() != null)
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
            "PomodoroMessageText");
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

    private void EnterEditMode(
        GameObject row,
        CharacterPomodoroPlaybackCandidate candidate)
    {
        if (busy || row == null || candidate == null) return;
        ResetEditingState();
        TMP_Text message = MemoryArchiveUi.FindComponent<TMP_Text>(
            row.transform, "PomodoroMessageText");
        TMP_InputField input = MemoryArchiveUi.FindComponent<TMP_InputField>(
            row.transform, "EditInput");
        Button delete = MemoryArchiveUi.FindComponent<Button>(
            row.transform, "DeleteButton");
        if (input == null || delete == null) return;
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
        CharacterPomodoroPlaybackCandidate candidate,
        string value)
    {
        StartCoroutine(CommitRowEditAfterCurrentClick(
            row,
            candidate,
            value));
    }

    private IEnumerator CommitRowEditAfterCurrentClick(
        GameObject row,
        CharacterPomodoroPlaybackCandidate candidate,
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
        string message = value != null ? value.Trim() : string.Empty;
        suppressEditCommit = true;
        ExitEditVisual(row);
        editingRow = null;
        editingInput = null;
        suppressEditCommit = false;
        if (string.IsNullOrWhiteSpace(message))
        {
            SetNotice(T("포모도로 대사는 비워둘 수 없습니다."));
            yield break;
        }
        if (message != candidate.message)
        {
            UpdateEditedDialogue(candidate, message);
        }
    }

    private void UpdateEditedDialogue(
        CharacterPomodoroPlaybackCandidate candidate,
        string message)
    {
        bool saved = candidate != null &&
                     candidate.isGenerated &&
                     candidate.generatedRecord != null
            ? CharacterPomodoroVoiceRepository.UpdateGeneratedDialogueMessage(
                characterName,
                candidate.generatedRecord.id,
                message)
            : candidate != null &&
              CharacterPomodoroVoiceRepository.UpdateDefaultDialogueMessage(
                  characterName,
                  candidate.id,
                  message);
        if (!saved)
        {
            SetNotice(T("수정한 포모도로 대사를 저장하지 못했습니다."));
            return;
        }
        SetNotice(T("포모도로 대사를 수정했습니다. 음성은 재생성으로 갱신할 수 있습니다."));
        ReloadKeepingNotice();
    }

    private void DeleteDialogue(
        GameObject row,
        CharacterPomodoroPlaybackCandidate candidate)
    {
        if (busy || candidate == null) return;
        suppressRowClick = row;
        suppressEditCommit = true;
        bool deleted = candidate.isGenerated && candidate.generatedRecord != null
            ? CharacterPomodoroVoiceRepository.DeleteGeneratedDialogue(
                characterName, candidate.generatedRecord.id)
            : CharacterPomodoroVoiceRepository.SetDefaultDialogueHidden(
                characterName, candidate.id, true);
        editingRow = null;
        editingInput = null;
        suppressEditCommit = false;
        SetNotice(deleted
            ? T("포모도로 대사를 삭제했습니다.")
            : T("포모도로 대사를 삭제하지 못했습니다."));
        ReloadKeepingNotice();
    }

    private void PlayCandidate(CharacterPomodoroPlaybackCandidate candidate)
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
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(
                   new System.Uri(path).AbsoluteUri, AudioType.WAV))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                PlayClip(DownloadHandlerAudioClip.GetContent(request), message);
            }
            else
            {
                SetNotice(T("저장된 포모도로 음성을 재생하지 못했습니다."));
            }
        }
    }

    private static bool HasPlayableAudio(CharacterPomodoroPlaybackCandidate candidate)
    {
        return candidate != null &&
               (candidate.audioClip != null ||
                (!string.IsNullOrWhiteSpace(candidate.audioFilePath) &&
                 File.Exists(candidate.audioFilePath)));
    }

    private void CancelRowLongPress()
    {
        rowPointerActive = false;
        if (rowLongPressCoroutine == null) return;
        StopCoroutine(rowLongPressCoroutine);
        rowLongPressCoroutine = null;
    }

    private void ResetEditingState()
    {
        if (editingRow != null)
        {
            suppressEditCommit = true;
            ExitEditVisual(editingRow);
        }
        editingRow = null;
        editingInput = null;
        suppressEditCommit = false;
    }

    private static void ExitEditVisual(GameObject row)
    {
        if (row == null) return;
        TMP_Text message = MemoryArchiveUi.FindComponent<TMP_Text>(
            row.transform, "PomodoroMessageText");
        TMP_InputField input = MemoryArchiveUi.FindComponent<TMP_InputField>(
            row.transform, "EditInput");
        Button delete = MemoryArchiveUi.FindComponent<Button>(
            row.transform, "DeleteButton");
        if (input != null)
        {
            input.DeactivateInputField();
            input.gameObject.SetActive(false);
        }
        if (delete != null) delete.gameObject.SetActive(false);
        if (message != null) message.gameObject.SetActive(true);
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
        completed?.Invoke(string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:5000" : baseUrl);
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
        try
        {
            previewAudioSource.volume = SettingManager.Instance.settings.sound_volumeMaster / 100f;
        }
        catch
        {
            previewAudioSource.volume = 1f;
        }
        previewAudioSource.Play();
        if (!string.IsNullOrWhiteSpace(message) &&
            AnswerBalloonSimpleManager.Instance != null)
        {
            AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleForSeconds(
                message, clip.length + 0.5f);
        }
    }

    private void RefreshButtons()
    {
        bool hasCharacter = !string.IsNullOrWhiteSpace(characterName);
        if (sampleButton != null)
        {
            sampleButton.interactable = !busy && hasCharacter;
        }
        if (randomConceptButton != null) randomConceptButton.interactable = !busy;
        if (generateButton != null) generateButton.interactable = !busy && hasCharacter;
        if (addButton != null) addButton.interactable = !busy && hasCharacter;
        foreach (GameObject row in rows)
        {
            if (row == null) continue;
            Button regenerate = MemoryArchiveUi.FindComponent<Button>(
                row.transform, "RegenerateButton");
            if (regenerate != null) regenerate.interactable = !busy;
            TMP_Dropdown situationDropdown =
                MemoryArchiveUi.FindComponent<TMP_Dropdown>(
                    row.transform,
                    "SituationDropdown");
            if (situationDropdown != null)
            {
                situationDropdown.interactable = !busy;
            }
        }
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
        string value = noticeText != null ? noticeText.text : string.Empty;
        Reload();
        SetNotice(value);
    }

    private void SetNotice(string value)
    {
        if (noticeText != null) noticeText.text = value ?? string.Empty;
    }

    private void StartLoadingNotice(string value)
    {
        StopLoadingNotice();
        loadingNoticeCoroutine = StartCoroutine(AnimateLoadingNotice(value));
    }

    private void StopLoadingNotice()
    {
        if (loadingNoticeCoroutine == null) return;
        StopCoroutine(loadingNoticeCoroutine);
        loadingNoticeCoroutine = null;
    }

    private IEnumerator AnimateLoadingNotice(string value)
    {
        int dots = 1;
        while (busy)
        {
            SetNotice((value ?? string.Empty) + new string('.', dots));
            dots = dots == 3 ? 1 : dots + 1;
            yield return new WaitForSecondsRealtime(0.4f);
        }
        loadingNoticeCoroutine = null;
    }

    private void EnsureBuilt()
    {
        if (built) return;
        if (MemoryArchiveUi.FindDeepChild(transform, "PomodoroVoiceBody") != null)
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
            : MemoryArchiveUi.FindDeepChild(transform, "PomodoroVoiceContent");
        Transform template = rowTemplate != null
            ? rowTemplate.transform
            : MemoryArchiveUi.FindDeepChild(transform, "PomodoroVoiceRowTemplate");
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
            : MemoryArchiveUi.FindComponent<Button>(transform, "PomodoroSamplePlayButton");
        randomConceptButton = randomConceptButton != null
            ? randomConceptButton
            : MemoryArchiveUi.FindComponent<Button>(transform, "RandomConceptButton");
        generateButton = generateButton != null
            ? generateButton
            : MemoryArchiveUi.FindComponent<Button>(transform, "PomodoroGenerateButton");
        addButton = addButton != null
            ? addButton
            : MemoryArchiveUi.FindComponent<Button>(transform, "AddButton");
        if (rowTemplate != null) rowTemplate.SetActive(false);
        BindButton("CloseButton", Hide);
        BindButton("PomodoroSamplePlayButton", OnSampleClicked);
        BindButton("RandomConceptButton", OnRandomConceptClicked);
        BindButton("PomodoroGenerateButton", OnGenerateClicked);
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
            "TitleText", header.transform, "Pomodoro Voice", 20f, MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.Layout(title.gameObject, minW: 180f, flexW: 1f);
        characterText = MemoryArchiveUi.CreateText(
            "CharacterText", header.transform, "", 13f, MemoryArchiveUi.TextMuted,
            TextAlignmentOptions.MidlineRight);
        characterText.overflowMode = TextOverflowModes.Ellipsis;
        MemoryArchiveUi.Layout(characterText.gameObject, minW: 100f, prefW: 100f);
        sampleButton = MemoryArchiveUi.CreateButton(
            "PomodoroSamplePlayButton", header.transform, "샘플 듣기", MemoryArchiveUi.PanelBg2, 12f);
        MemoryArchiveUi.Layout(sampleButton.gameObject, minW: 78f, prefW: 78f);
        sampleButton.onClick.AddListener(OnSampleClicked);
        Button close = MemoryArchiveUi.CreateButton(
            "CloseButton", header.transform, "×", MemoryArchiveUi.HeaderBg, 24f);
        MemoryArchiveUi.Layout(close.gameObject, minW: 40f, prefW: 40f);
        close.onClick.AddListener(Hide);
    }

    private void BuildConceptRow()
    {
        GameObject row = MemoryArchiveUi.CreatePanel(
            "ConceptRow", transform, MemoryArchiveUi.PanelBg);
        MemoryArchiveUi.Layout(row, minH: 46f, prefH: 46f, flexH: 0f);
        MemoryArchiveUi.AddRow(
            row, 6f, new RectOffset(5, 5, 5, 5)).childForceExpandHeight = true;
        conceptInput = CreateInput("ConceptInput", row.transform, "컨셉을 적어주세요");
        MemoryArchiveUi.Layout(conceptInput.gameObject, flexW: 1f);
        randomConceptButton = MemoryArchiveUi.CreateButton(
            "RandomConceptButton", row.transform, "랜덤", MemoryArchiveUi.PanelBg2, 13f);
        MemoryArchiveUi.Layout(randomConceptButton.gameObject, minW: 62f, prefW: 62f);
        randomConceptButton.onClick.AddListener(OnRandomConceptClicked);
        generateButton = MemoryArchiveUi.CreateButton(
            "PomodoroGenerateButton", row.transform, "생성", MemoryArchiveUi.Accent, 13f);
        MemoryArchiveUi.Layout(generateButton.gameObject, minW: 62f, prefW: 62f);
        generateButton.onClick.AddListener(OnGenerateClicked);
    }

    private void BuildDirectAddRow()
    {
        GameObject row = MemoryArchiveUi.CreatePanel(
            "DirectAddRow", transform, MemoryArchiveUi.PanelBg);
        MemoryArchiveUi.Layout(row, minH: 46f, prefH: 46f, flexH: 0f);
        MemoryArchiveUi.AddRow(row, 6f, new RectOffset(5, 5, 5, 5)).childForceExpandHeight = true;
        addInput = CreateInput("AddInput", row.transform, "직접 Pomodoro 대사 추가");
        MemoryArchiveUi.Layout(addInput.gameObject, flexW: 1f);
        addButton = MemoryArchiveUi.CreateButton(
            "AddButton", row.transform, "추가", MemoryArchiveUi.Accent, 13f);
        MemoryArchiveUi.Layout(addButton.gameObject, minW: 62f, prefW: 62f);
        addButton.onClick.AddListener(OnAddClicked);
    }

    private void BuildBody()
    {
        GameObject body = MemoryArchiveUi.CreatePanel(
            "PomodoroVoiceBody", transform, MemoryArchiveUi.PanelBg);
        MemoryArchiveUi.Layout(body, flexH: 1f);
        ScrollRect scroll = body.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;
        GameObject viewport = MemoryArchiveUi.CreateUIObject("Viewport", body.transform);
        MemoryArchiveUi.SetStretch(viewport, new Vector4(4f, 4f, 18f, 4f));
        viewport.AddComponent<RectMask2D>();
        GameObject content = MemoryArchiveUi.CreateUIObject("PomodoroVoiceContent", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        MemoryArchiveUi.AddColumn(
            content, 6f, new RectOffset(2, 2, 2, 2)).childForceExpandWidth = true;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        BuildScrollbar(body.transform, scroll);
        listContent = content.transform;

        rowTemplate = MemoryArchiveUi.CreatePanel(
            "PomodoroVoiceRowTemplate", content.transform, MemoryArchiveUi.PanelBg2);
        rowTemplate.AddComponent<CanvasGroup>();
        MemoryArchiveUi.Layout(rowTemplate, minH: 72f, prefH: 72f, flexH: 0f);
        HorizontalLayoutGroup layout = MemoryArchiveUi.AddRow(
            rowTemplate, 6f, new RectOffset(8, 8, 8, 8));
        layout.childForceExpandHeight = true;
        Button delete = MemoryArchiveUi.CreateButton(
            "DeleteButton", rowTemplate.transform, "",
            new Color(0.62f, 0.08f, 0.11f, 1f), 12f);
        MemoryArchiveUi.Layout(delete.gameObject, minW: 44f, prefW: 44f);
        BuildTrashIcon(delete.transform);
        delete.gameObject.SetActive(false);
        TMP_Text message = MemoryArchiveUi.CreateText(
            "PomodoroMessageText", rowTemplate.transform, "", 13f, MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        message.textWrappingMode = TextWrappingModes.Normal;
        message.overflowMode = TextOverflowModes.Ellipsis;
        MemoryArchiveUi.Layout(message.gameObject, minW: 300f, flexW: 1f);
        TMP_InputField editInput = CreateInput("EditInput", rowTemplate.transform, "");
        MemoryArchiveUi.Layout(editInput.gameObject, minW: 300f, flexW: 1f);
        editInput.gameObject.SetActive(false);
        TMP_Dropdown situationDropdown = CreateSituationDropdown(
            "SituationDropdown",
            rowTemplate.transform);
        MemoryArchiveUi.Layout(
            situationDropdown.gameObject,
            minW: 108f,
            prefW: 108f,
            flexW: 0f);
        Button regenerate = MemoryArchiveUi.CreateButton(
            "RegenerateButton", rowTemplate.transform, "재생성", MemoryArchiveUi.PanelBg, 12f);
        MemoryArchiveUi.Layout(regenerate.gameObject, minW: 68f, prefW: 68f);
        Button play = MemoryArchiveUi.CreateButton(
            "PlayButton", rowTemplate.transform, "듣기", MemoryArchiveUi.Accent, 12f);
        MemoryArchiveUi.Layout(play.gameObject, minW: 58f, prefW: 58f);
        rowTemplate.SetActive(false);

        emptyText = MemoryArchiveUi.CreateText(
            "EmptyText", body.transform, "저장된 Pomodoro 대사가 없습니다.", 14f,
            MemoryArchiveUi.TextMuted, TextAlignmentOptions.Center);
        MemoryArchiveUi.SetStretch(
            emptyText.gameObject, new Vector4(36f, 36f, 36f, 36f));
    }

    private static TMP_Dropdown CreateSituationDropdown(
        string name,
        Transform parent)
    {
        GameObject root = MemoryArchiveUi.CreatePanel(
            name,
            parent,
            MemoryArchiveUi.PanelBg);
        TMP_Dropdown dropdown = root.AddComponent<TMP_Dropdown>();

        TextMeshProUGUI label = MemoryArchiveUi.CreateText(
            "Label",
            root.transform,
            "아무때나",
            12f,
            MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        label.overflowMode = TextOverflowModes.Ellipsis;
        MemoryArchiveUi.SetStretch(
            label.gameObject,
            new Vector4(9f, 2f, 24f, 2f));

        TextMeshProUGUI arrow = MemoryArchiveUi.CreateText(
            "Arrow",
            root.transform,
            "▼",
            10f,
            MemoryArchiveUi.TextMuted,
            TextAlignmentOptions.Center);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-8f, 0f);
        arrowRect.sizeDelta = new Vector2(12f, 16f);

        GameObject template = MemoryArchiveUi.CreatePanel(
            "Template",
            root.transform,
            MemoryArchiveUi.PanelBg);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -2f);
        templateRect.sizeDelta = new Vector2(0f, 120f);

        ScrollRect scroll = template.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        GameObject viewport = MemoryArchiveUi.CreatePanel(
            "Viewport",
            template.transform,
            MemoryArchiveUi.PanelBg);
        MemoryArchiveUi.SetStretch(viewport, Vector4.zero);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = MemoryArchiveUi.CreateUIObject(
            "Content",
            viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 28f);

        GameObject item = MemoryArchiveUi.CreateUIObject(
            "Item",
            content.transform);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 28f);
        Toggle itemToggle = item.AddComponent<Toggle>();
        itemToggle.toggleTransition = Toggle.ToggleTransition.None;

        GameObject itemBackground = MemoryArchiveUi.CreatePanel(
            "Item Background",
            item.transform,
            MemoryArchiveUi.PanelBg2);
        MemoryArchiveUi.SetStretch(itemBackground, Vector4.zero);
        itemToggle.targetGraphic = itemBackground.GetComponent<Image>();

        GameObject itemCheckmark = MemoryArchiveUi.CreatePanel(
            "Item Checkmark",
            item.transform,
            MemoryArchiveUi.Accent);
        RectTransform checkRect = itemCheckmark.GetComponent<RectTransform>();
        checkRect.anchorMin = checkRect.anchorMax = new Vector2(0f, 0.5f);
        checkRect.pivot = new Vector2(0f, 0.5f);
        checkRect.anchoredPosition = new Vector2(9f, 0f);
        checkRect.sizeDelta = new Vector2(8f, 16f);
        itemToggle.graphic = itemCheckmark.GetComponent<Image>();

        TextMeshProUGUI itemLabel = MemoryArchiveUi.CreateText(
            "Item Label",
            item.transform,
            "아무때나",
            12f,
            MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.SetStretch(
            itemLabel.gameObject,
            new Vector4(23f, 1f, 8f, 1f));

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        dropdown.template = templateRect;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        dropdown.targetGraphic = root.GetComponent<Image>();
        dropdown.AddOptions(new List<string>
        {
            "아무때나",
            "준비",
            "집중",
            "휴식"
        });
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
        template.SetActive(false);
        return dropdown;
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

    private static void BuildTrashIcon(Transform parent)
    {
        GameObject icon = MemoryArchiveUi.CreateUIObject("TrashIcon", parent);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(18f, 20f);
        rect.anchoredPosition = Vector2.zero;
        CreateTrashPart("Body", icon.transform, new Vector2(0f, -2f), new Vector2(12f, 13f));
        CreateTrashPart("Lid", icon.transform, new Vector2(0f, 6f), new Vector2(16f, 3f));
        CreateTrashPart("Handle", icon.transform, new Vector2(0f, 9f), new Vector2(7f, 2f));
    }

    private static void CreateTrashPart(
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

    private static void AddEventTrigger(
        GameObject target,
        EventTriggerType type,
        UnityEngine.Events.UnityAction<BaseEventData> action,
        bool clear)
    {
        EventTrigger trigger = MemoryArchiveUi.GetOrAdd<EventTrigger>(target);
        if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
        if (clear) trigger.triggers.Clear();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void AddScrollForwarding(GameObject source)
    {
        AddEventTrigger(
            source, EventTriggerType.Scroll,
            data => ForwardToScroll(source, data, EventTriggerType.Scroll), false);
        AddEventTrigger(
            source, EventTriggerType.BeginDrag,
            data =>
            {
                CancelRowLongPress();
                ForwardToScroll(source, data, EventTriggerType.BeginDrag);
            }, false);
        AddEventTrigger(
            source, EventTriggerType.Drag,
            data =>
            {
                CancelRowLongPress();
                ForwardToScroll(source, data, EventTriggerType.Drag);
            }, false);
        AddEventTrigger(
            source, EventTriggerType.EndDrag,
            data => ForwardToScroll(source, data, EventTriggerType.EndDrag), false);
    }

    private static void ForwardToScroll(
        GameObject source,
        BaseEventData data,
        EventTriggerType type)
    {
        PointerEventData pointer = data as PointerEventData;
        ScrollRect scroll = source != null
            ? source.GetComponentInParent<ScrollRect>()
            : null;
        if (pointer == null || scroll == null) return;
        switch (type)
        {
            case EventTriggerType.Scroll: scroll.OnScroll(pointer); break;
            case EventTriggerType.BeginDrag: scroll.OnBeginDrag(pointer); break;
            case EventTriggerType.Drag: scroll.OnDrag(pointer); break;
            case EventTriggerType.EndDrag: scroll.OnEndDrag(pointer); break;
        }
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

    private string GetCurrentSoundLanguage()
    {
        try
        {
            if (SettingManager.Instance != null &&
                SettingManager.Instance.settings != null &&
                !string.IsNullOrWhiteSpace(
                    SettingManager.Instance.settings.sound_language))
            {
                return NormalizeLanguage(
                    SettingManager.Instance.settings.sound_language);
            }
        }
        catch
        {
        }
        return NormalizeLanguage(language);
    }

    private static string NormalizeLanguage(string value)
    {
        if (string.Equals(
                value, "jp", System.StringComparison.OrdinalIgnoreCase))
        {
            return "ja";
        }
        return string.IsNullOrWhiteSpace(value) ? "ko" : value;
    }

    private void UpdateConceptPlaceholder(string targetLanguage = null)
    {
        TMP_Text placeholder = conceptInput != null
            ? conceptInput.placeholder as TMP_Text
            : null;
        if (placeholder == null) return;
        placeholder.text = LanguageDataCharacterVoicePomodoro.Translate(
            "컨셉을 적어주세요",
            targetLanguage ??
            CharacterVoiceSpeechTextResolver.GetCurrentUiLanguage());
    }

    private static string GetLocalizedConcept(string[] values, string targetLanguage)
    {
        if (values == null || values.Length < 3) return string.Empty;
        switch (NormalizeLanguage(targetLanguage))
        {
            case "ja": return values[1];
            case "en": return values[2];
            default: return values[0];
        }
    }

    private static string GetSampleMessage(string targetLanguage)
    {
        return LanguageDataCharacterVoicePomodoro.Translate(
            "집중할 시간이에요.",
            targetLanguage);
    }

    private static string GetPlayerName()
    {
        try
        {
            string value = SettingManager.Instance.settings.player_name;
            return string.IsNullOrWhiteSpace(value) ? "선생님" : value.Trim();
        }
        catch
        {
            return "선생님";
        }
    }

    private void ApplyLocalizedStaticText()
    {
        SetLocalizedText(transform, "TitleText", "Pomodoro Voice");
        SetLocalizedButtonLabel(
            transform,
            "PomodoroSamplePlayButton",
            "샘플 듣기");
        SetLocalizedButtonLabel(transform, "RandomConceptButton", "랜덤");
        SetLocalizedButtonLabel(transform, "PomodoroGenerateButton", "생성");
        SetLocalizedButtonLabel(transform, "AddButton", "추가");
        SetLocalizedInputPlaceholder(conceptInput, "컨셉을 적어주세요");
        SetLocalizedInputPlaceholder(addInput, "직접 Pomodoro 대사 추가");
        if (emptyText != null)
        {
            emptyText.text = T("저장된 Pomodoro 대사가 없습니다.");
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
        return LanguageDataCharacterVoicePomodoro.Translate(value);
    }
}
