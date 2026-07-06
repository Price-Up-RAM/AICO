using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CharacterDetailController : MonoBehaviour
{
    private const string NoVoiceOptionText = "없음";
    private static readonly Color ButtonActiveBlue = new Color(0.306f, 0.404f, 0.608f, 1f);
    private static readonly Color ButtonInactiveBlack = new Color(0.047f, 0.055f, 0.071f, 1f);
    private static readonly Dictionary<string, string> VoiceRefIdByLabel = BuildVoiceRefIdMap();
    private static readonly Dictionary<string, string> LegacyVoiceRefIdByValue = BuildLegacyVoiceRefIdMap();
    private static readonly List<string> VoiceOptionLabels = BuildVoiceOptionLabels();
    private const float CollapsedStatsY = -334f;
    private const float CollapsedAlarmY = -374f;
    private const float ExpandedStatsY = -444f;
    private const float ExpandedAlarmY = -484f;

    [Header("Root")]
    [SerializeField] private Button hideButton;
    [SerializeField] private ScrollRect infoScrollRect;
    [SerializeField] private RectTransform infoContent;

    [Header("Character")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI portraitPlaceholderText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI sourceText;
    [SerializeField] private TextMeshProUGUI formText;

    [Header("Status Tags")]
    [SerializeField] private GameObject statusAvailableTag;
    [SerializeField] private GameObject statusDownloadRequiredTag;

    [Header("Affection")]
    [SerializeField] private TextMeshProUGUI affectionValueText;
    [SerializeField] private TextMeshProUGUI affectionLabelText;
    [SerializeField] private RectTransform affectionBarYellow;
    [SerializeField] private RectTransform affectionBarOrange;
    [SerializeField] private RectTransform affectionBarRed;
    [SerializeField] private int maxAffection = 300;
    [SerializeField] private string defaultAffectionLabel = "친밀";

    [Header("Feature Tags")]
    [SerializeField] private Transform featureTagContainer;

    [Header("Voice")]
    [SerializeField] private TMP_Dropdown voiceDropdown;
    [SerializeField] private Button voiceSamplePlayButton;

    [Header("Prompt")]
    [SerializeField] private RectTransform promptArea;
    [SerializeField] private Button promptToggleButton;
    [SerializeField] private Image promptToggleImage;
    [SerializeField] private TextMeshProUGUI promptToggleText;
    [SerializeField] private TMP_Dropdown promptLanguageDropdown;
    [SerializeField] private Button promptCopyButton;
    [SerializeField] private Button promptResetButton;
    [SerializeField] private Button promptSaveButton;
    [SerializeField] private TMP_InputField promptInputField;
    [SerializeField] private CharacterDetailPromptView promptView;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI conversationCountText;
    [SerializeField] private TextMeshProUGUI costumeCountText;
    [SerializeField] private RectTransform defaultAlarmVoiceLabel;
    [SerializeField] private RectTransform alarmSamplePlayButton;
    [SerializeField] private RectTransform alarmGenerateButton;
    [SerializeField] private RectTransform alarmGeneratedPlayButton;

    [Header("Options")]
    [SerializeField] private Sprite promptCollapsedSprite;
    [SerializeField] private Sprite promptExpandedSprite;

    private ChangeCharInfo currentCharInfo;
    private ChangeCharClothesInfo currentClothesInfo;
    private bool promptExpanded;
    private string currentCharacterId;
    private int promptRequestVersion;
    private bool isSampleVoiceRequesting;

    private void Awake()
    {
        AutoBindMissingReferences();
        PopulateVoiceDropdownOptions();
        EnsurePromptView();
        RegisterEvents();
        SetPromptExpanded(false);
    }

    private void Start()
    {
        CharacterDetailStateManager.Instance.StateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        UnregisterEvents();
        if (CharacterDetailStateManager.Instance != null)
        {
            CharacterDetailStateManager.Instance.StateChanged -= OnStateChanged;
        }
    }

    public void Show(ChangeCharInfo charInfo, ChangeCharClothesInfo clothesInfo = null)
    {
        Debug.Log($"[CharacterDetail][Controller] Show start. char={charInfo?.name} clothes={clothesInfo?.text} object={name} activeBefore={gameObject.activeSelf}");

        currentCharInfo = charInfo;
        currentClothesInfo = clothesInfo ?? GetDefaultClothes(charInfo);
        currentCharacterId = CharacterDetailStateManager.BuildCharacterId(currentCharInfo, currentClothesInfo);
        promptRequestVersion++;

        gameObject.SetActive(true);

        ApplyState(CharacterDetailStateManager.Instance.GetState(currentCharacterId));
        SetPromptExpanded(false);

        if (infoScrollRect != null)
        {
            infoScrollRect.verticalNormalizedPosition = 1f;
        }

        RectTransform rectTransform = transform as RectTransform;
        Debug.Log($"[CharacterDetail][Controller] Show complete. active={gameObject.activeSelf} world={(rectTransform != null ? rectTransform.position.ToString() : transform.position.ToString())} anchored={(rectTransform != null ? rectTransform.anchoredPosition.ToString() : "no rect")}");
    }

    public void Hide()
    {
        Debug.Log($"[CharacterDetail][Controller] Hide. object={name}");
        promptRequestVersion++;
        gameObject.SetActive(false);
    }

    public void TogglePromptExpanded()
    {
        SetPromptExpanded(!promptExpanded);
    }

    public void SetPromptExpanded(bool expanded)
    {
        promptExpanded = expanded;
        if (promptView != null)
        {
            promptView.SetExpanded(expanded);
        }

        SetY(conversationCountText, expanded ? ExpandedStatsY : CollapsedStatsY);
        SetY(costumeCountText, expanded ? ExpandedStatsY : CollapsedStatsY);
        SetY(defaultAlarmVoiceLabel, expanded ? ExpandedAlarmY : CollapsedAlarmY);
        SetY(alarmSamplePlayButton, expanded ? ExpandedAlarmY : CollapsedAlarmY);
        SetY(alarmGenerateButton, expanded ? ExpandedAlarmY : CollapsedAlarmY);
        SetY(alarmGeneratedPlayButton, expanded ? ExpandedAlarmY : CollapsedAlarmY);

        if (infoContent != null)
        {
            Vector2 size = infoContent.sizeDelta;
            size.y = expanded ? 530f : 430f;
            infoContent.sizeDelta = size;
        }
    }

    public void SetAffection(int value, string label = null)
    {
        int clamped = Mathf.Clamp(value, 0, maxAffection);
        SetText(affectionValueText, "호감도 " + clamped + "/" + maxAffection);
        SetText(affectionLabelText, string.IsNullOrEmpty(label) ? defaultAffectionLabel : label);

        float yellow = Mathf.Clamp(clamped, 0, 100) / 100f;
        float orange = Mathf.Clamp(clamped - 100, 0, 100) / 100f;
        float red = Mathf.Clamp(clamped - 200, 0, 100) / 100f;

        SetFillWidth(affectionBarYellow, yellow);
        SetFillWidth(affectionBarOrange, orange);
        SetFillWidth(affectionBarRed, red);
    }

    private void EnsurePromptView()
    {
        promptView = promptView != null ? promptView : GetComponent<CharacterDetailPromptView>();
        if (promptView == null)
        {
            promptView = gameObject.AddComponent<CharacterDetailPromptView>();
        }

        promptView.Configure(
            promptArea,
            promptToggleButton,
            promptToggleImage,
            promptToggleText,
            promptLanguageDropdown,
            promptCopyButton,
            promptResetButton,
            promptSaveButton,
            promptInputField,
            promptCollapsedSprite,
            promptExpandedSprite);
    }

    private void RegisterEvents()
    {
        if (hideButton != null)
        {
            hideButton.onClick.AddListener(Hide);
        }

        if (voiceDropdown != null)
        {
            voiceDropdown.onValueChanged.AddListener(OnVoiceChanged);
        }

        if (voiceSamplePlayButton != null)
        {
            voiceSamplePlayButton.onClick.AddListener(OnSampleVoicePlayClicked);
        }

        if (promptView != null)
        {
            promptView.BindEvents(
                TogglePromptExpanded,
                OnPromptLanguageChanged,
                ResetPrompt,
                SavePrompt);
        }
    }

    private void UnregisterEvents()
    {
        if (hideButton != null)
        {
            hideButton.onClick.RemoveListener(Hide);
        }

        if (voiceDropdown != null)
        {
            voiceDropdown.onValueChanged.RemoveListener(OnVoiceChanged);
        }

        if (voiceSamplePlayButton != null)
        {
            voiceSamplePlayButton.onClick.RemoveListener(OnSampleVoicePlayClicked);
        }

        if (promptView != null)
        {
            promptView.UnbindEvents();
        }
    }

    private void OnStateChanged(string characterId, CharacterDetailState state)
    {
        if (characterId != currentCharacterId)
        {
            return;
        }

        ApplyState(state);
    }

    private void ApplyState(CharacterDetailState state)
    {
        if (state == null) return;

        string displayName = currentCharInfo != null ? currentCharInfo.name : "캐릭터 이름";
        string form = !string.IsNullOrEmpty(currentClothesInfo?.charAttr_type) ? currentClothesInfo.charAttr_type : state.form;

        SetText(nameText, displayName);
        SetText(sourceText, "출전 : " + state.source);
        SetText(formText, "형태 : " + form);

        bool selectable = currentClothesInfo == null || currentClothesInfo.isSelectable;
        SetActive(statusAvailableTag, selectable);
        SetActive(statusDownloadRequiredTag, !selectable);

        RefreshPortrait();
        RefreshFeatureTags(state.featureTags);
        RefreshPrompt();

        string normalizedVoiceId = NormalizeVoiceRefId(state.voiceId);
        ApplyVoiceDropdownValue(normalizedVoiceId);
        if (state.voiceId != normalizedVoiceId && !string.IsNullOrEmpty(currentCharacterId))
        {
            CharacterDetailStateManager.Instance.SetVoice(currentCharacterId, normalizedVoiceId);
        }

        int conversationCount = GetConversationCount();
        int costumeCount = currentCharInfo != null && currentCharInfo.clothesList != null ? currentCharInfo.clothesList.Count : 0;

        SetText(conversationCountText, "대화횟수 : " + conversationCount);
        SetText(costumeCountText, "복장 수 : " + costumeCount);
        SetAffection(state.affection, state.affectionLabel);
    }

    private void RefreshPrompt(bool isOrigin = false)
    {
        string lang = GetSelectedPromptLanguage();
        string charCode = GetPromptCharacterCode();

        if (promptView != null)
        {
            promptView.SetTextWithoutNotify(isOrigin ? "초기화 중..." : "로딩 중...");
        }

        if (!string.IsNullOrEmpty(charCode))
        {
            _ = FetchAndApplyPromptAsync(charCode, lang, isOrigin);
        }
    }

    private async System.Threading.Tasks.Task FetchAndApplyPromptAsync(string charCode, string lang, bool isOrigin = false)
    {
        CharacterPromptManager promptManager = EnsureCharacterPromptManager();
        if (promptManager == null)
        {
            Debug.LogError($"[CharacterDetail][Prompt] CharacterPromptManager unavailable. Fetch skipped. charCode={charCode}, lang={lang}, isOrigin={isOrigin}");
            return;
        }

        int requestVersion = ++promptRequestVersion;
        Debug.Log($"[CharacterDetail][Prompt] Fetch requested. charCode={charCode}, lang={lang}, isOrigin={isOrigin}");
        string prompt = await promptManager.FetchPromptAsync(charCode, lang, isOrigin);

        if (requestVersion != promptRequestVersion || charCode != GetPromptCharacterCode() || lang != GetSelectedPromptLanguage())
        {
            Debug.Log($"[CharacterDetail][Prompt] Stale fetch ignored. charCode={charCode}, lang={lang}, isOrigin={isOrigin}");
            return;
        }

        if (promptView != null)
        {
            promptView.SetTextWithoutNotify(prompt);
        }
    }

    private CharacterPromptManager EnsureCharacterPromptManager()
    {
        if (CharacterPromptManager.Instance != null)
        {
            return CharacterPromptManager.Instance;
        }

        CharacterPromptManager existing = FindObjectOfType<CharacterPromptManager>();
        if (existing != null)
        {
            return existing;
        }

        GameObject managerObject = new GameObject("CharacterPromptManager");
        CharacterPromptManager created = managerObject.AddComponent<CharacterPromptManager>();
        Debug.Log("[CharacterDetail][Prompt] CharacterPromptManager was missing and has been created.");
        return created;
    }

    private async void RefreshPortrait()
    {
        if (portraitImage == null || currentClothesInfo == null)
        {
            return;
        }

        Sprite sprite = null;
        string spriteAddress = currentClothesInfo.spriteAddress;

        if (currentClothesInfo.isLocal)
        {
            sprite = ChangeCharManager.Instance != null ? ChangeCharManager.Instance.GetLocalSprite(spriteAddress) : null;
        }
        else if (!string.IsNullOrEmpty(spriteAddress) && AddressableManager.Instance != null)
        {
            sprite = await AddressableManager.Instance.LoadIfExist<Sprite>(spriteAddress);
        }

        if (sprite == null && ChangeCharManager.Instance != null)
        {
            sprite = ChangeCharManager.Instance.fallbackSprite;
        }

        if (sprite != null)
        {
            portraitImage.sprite = sprite;
            portraitImage.preserveAspect = true;
            portraitImage.color = Color.white;
            SetActive(portraitPlaceholderText != null ? portraitPlaceholderText.gameObject : null, false);
        }
        else
        {
            SetActive(portraitPlaceholderText != null ? portraitPlaceholderText.gameObject : null, true);
        }
    }

    private void RefreshFeatureTags(List<string> featureTags)
    {
        if (featureTagContainer == null || featureTags == null)
        {
            return;
        }

        for (int i = 0; i < featureTagContainer.childCount; i++)
        {
            Transform existing = featureTagContainer.GetChild(i);
            bool shouldShow = i < featureTags.Count;
            existing.gameObject.SetActive(shouldShow);

            if (shouldShow)
            {
                TextMeshProUGUI text = existing.GetComponentInChildren<TextMeshProUGUI>(true);
                SetText(text, featureTags[i]);
            }
        }
    }

    private void OnVoiceChanged(int index)
    {
        if (voiceDropdown == null || index < 0 || index >= voiceDropdown.options.Count)
        {
            RefreshVoiceSamplePlayButtonState();
            return;
        }

        string voiceId = GetVoiceRefIdFromLabel(voiceDropdown.options[index].text);
        if (!string.IsNullOrEmpty(currentCharacterId))
        {
            CharacterDetailStateManager.Instance.SetVoice(currentCharacterId, voiceId);
        }

        RefreshVoiceSamplePlayButtonState();
    }

    private void OnSampleVoicePlayClicked()
    {
        if (isSampleVoiceRequesting || voiceDropdown == null || voiceDropdown.options == null || voiceDropdown.options.Count == 0)
        {
            return;
        }

        int selectedIndex = voiceDropdown.value;
        if (selectedIndex < 0 || selectedIndex >= voiceDropdown.options.Count)
        {
            return;
        }

        string refId = GetVoiceRefIdFromLabel(voiceDropdown.options[selectedIndex].text);
        if (string.IsNullOrEmpty(refId))
        {
            Debug.Log("[CharacterDetail][SampleVoice] Voice is not selected.");
            return;
        }

        StartCoroutine(RequestAndPlaySampleVoice(refId));
    }

    private IEnumerator RequestAndPlaySampleVoice(string refId)
    {
        isSampleVoiceRequesting = true;
        RefreshVoiceSamplePlayButtonState();

        string baseUrl = null;
        yield return ResolveSampleVoiceBaseUrl(url => baseUrl = url);

        if (string.IsNullOrEmpty(baseUrl))
        {
            Debug.LogError("[CharacterDetail][SampleVoice] baseUrl is empty. Sample voice request skipped.");
            isSampleVoiceRequesting = false;
            RefreshVoiceSamplePlayButtonState();
            yield break;
        }

        string url = baseUrl.TrimEnd('/') + "/getSampleVoice";
        string jsonData = JsonConvert.SerializeObject(new Dictionary<string, string>
        {
            { "ref_id", refId }
        });

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[CharacterDetail][SampleVoice] Request failed. refId={refId}, url={url}, error={request.error}, code={request.responseCode}");
            }
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                PlaySampleVoiceClip(clip);
            }
        }

        isSampleVoiceRequesting = false;
        RefreshVoiceSamplePlayButtonState();
    }

    private IEnumerator ResolveSampleVoiceBaseUrl(System.Action<string> onComplete)
    {
        if (SettingManager.Instance != null && SettingManager.Instance.settings != null && SettingManager.Instance.settings.isLocalSound)
        {
            onComplete?.Invoke("http://127.0.0.1:5000");
            yield break;
        }

        string baseUrl = string.Empty;
        if (ServerManager.Instance != null)
        {
            bool completed = false;
            ServerManager.Instance.GetBaseUrl(url =>
            {
                baseUrl = url;
                completed = true;
            });

            while (!completed)
            {
                yield return null;
            }
        }

        if (string.IsNullOrEmpty(baseUrl) && ServerManager.Instance != null && SettingManager.Instance != null)
        {
            bool shouldUseDevServer =
                SettingManager.Instance.GetInstallStatus() < 2 ||
                SettingManager.Instance.IsDevSoundEnabled();

            if (shouldUseDevServer)
            {
                bool completed = false;
                ServerManager.Instance.GetServerUrlFromServerId("dev_voice", url =>
                {
                    baseUrl = url;
                    completed = true;
                });

                while (!completed)
                {
                    yield return null;
                }
            }
        }

        onComplete?.Invoke(string.IsNullOrEmpty(baseUrl) ? "http://127.0.0.1:5000" : baseUrl);
    }

    private void PlaySampleVoiceClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[CharacterDetail][SampleVoice] AudioClip is empty.");
            return;
        }

        AudioSource audioSource = VoiceManager.Instance != null ? VoiceManager.Instance.audioSource : null;
        if (audioSource == null)
        {
            audioSource = VoiceManager.Instance.gameObject.AddComponent<AudioSource>();
            VoiceManager.Instance.audioSource = audioSource;
        }

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.volume = GetMasterVolume();
        audioSource.Play();
    }

    private float GetMasterVolume()
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

    private void PopulateVoiceDropdownOptions()
    {
        if (voiceDropdown == null)
        {
            return;
        }

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (string label in VoiceOptionLabels)
        {
            options.Add(new TMP_Dropdown.OptionData(label));
        }

        voiceDropdown.ClearOptions();
        voiceDropdown.AddOptions(options);
        voiceDropdown.SetValueWithoutNotify(0);
        voiceDropdown.RefreshShownValue();
        RefreshVoiceSamplePlayButtonState();
    }

    private void ApplyVoiceDropdownValue(string voiceId)
    {
        if (voiceDropdown == null || voiceDropdown.options == null || voiceDropdown.options.Count == 0)
        {
            return;
        }

        int selectedIndex = 0;
        if (!string.IsNullOrEmpty(voiceId))
        {
            voiceId = NormalizeVoiceRefId(voiceId);
            for (int i = 0; i < voiceDropdown.options.Count; i++)
            {
                if (GetVoiceRefIdFromLabel(voiceDropdown.options[i].text) == voiceId)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        voiceDropdown.SetValueWithoutNotify(selectedIndex);
        voiceDropdown.RefreshShownValue();
        RefreshVoiceSamplePlayButtonState();
    }

    private void RefreshVoiceSamplePlayButtonState()
    {
        bool canPlay = !isSampleVoiceRequesting && HasSelectedVoice();
        SetButtonInteractable(voiceSamplePlayButton, canPlay);
        SetButtonImageColor(voiceSamplePlayButton, canPlay ? ButtonActiveBlue : ButtonInactiveBlack);
    }

    private bool HasSelectedVoice()
    {
        if (voiceDropdown == null || voiceDropdown.options == null || voiceDropdown.options.Count == 0)
        {
            return false;
        }

        int selectedIndex = voiceDropdown.value;
        if (selectedIndex < 0 || selectedIndex >= voiceDropdown.options.Count)
        {
            return false;
        }

        string label = voiceDropdown.options[selectedIndex].text;
        return !string.IsNullOrWhiteSpace(label)
            && label.Trim() != NoVoiceOptionText
            && !string.IsNullOrEmpty(GetVoiceRefIdFromLabel(label));
    }

    private static string GetVoiceRefIdFromLabel(string label)
    {
        return VoiceRefIdByLabel.TryGetValue(label, out string refId) ? refId : string.Empty;
    }

    private static string NormalizeVoiceRefId(string voiceId)
    {
        if (VoiceRefIdByLabel.TryGetValue(voiceId, out string refId))
        {
            return refId;
        }

        if (LegacyVoiceRefIdByValue.TryGetValue(voiceId, out string normalizedRefId))
        {
            return normalizedRefId;
        }

        return voiceId;
    }

    private static List<string> BuildVoiceOptionLabels()
    {
        return new List<string>(VoiceRefIdByLabel.Keys);
    }

    private static Dictionary<string, string> BuildVoiceRefIdMap()
    {
        return new Dictionary<string, string>
        {
            { "없음", "" },
            { "남자1", "man_01" },
            { "남자2", "man_02" },
            { "남자3", "man_03" },
            { "남자4", "man_04" },
            { "남자5", "man_05" },
            { "남자6", "man_06" },
            { "남자7", "man_07" },
            { "남자8", "man_08" },
            { "남자9", "man_09" },
            { "남자10", "man_10" },
            { "남자11", "man_11" },
            { "남자12", "man_12" },
            { "남자13", "man_13" },
            { "남자14", "man_14" },
            { "남자15", "man_15" },
            { "여자1", "woman_01" },
            { "여자2", "woman_02" },
            { "여자3", "woman_03" },
            { "여자4", "woman_04" },
            { "여자5", "woman_05" },
            { "여자6", "woman_06" },
            { "여자7", "woman_07" },
            { "여자8", "woman_08" },
            { "여자9", "woman_09" },
            { "여자10", "woman_10" },
            { "여자11", "woman_11" },
            { "여자12", "woman_12" },
            { "여자13", "woman_13" },
            { "여자14", "woman_14" },
            { "여자15", "woman_15" },
            { "여자16", "woman_16" },
            { "여자17", "woman_17" },
            { "여자18", "woman_18" },
            { "여자19", "woman_19" },
            { "여자20", "woman_20" },
            { "여자21", "woman_21" },
            { "여자22", "woman_22" },
            { "여자23", "woman_23" },
            { "여자24", "woman_24" },
            { "여자25", "woman_25" }
        };
    }

    private static Dictionary<string, string> BuildLegacyVoiceRefIdMap()
    {
        return new Dictionary<string, string>
        {
            { "man1", "man_01" },
            { "man2", "man_02" },
            { "man3", "man_03" },
            { "man4", "man_04" },
            { "man5", "man_05" },
            { "man6", "man_06" },
            { "man7", "man_07" },
            { "man8", "man_08" },
            { "man9", "man_09" },
            { "man10", "man_10" },
            { "man11", "man_11" },
            { "man12", "man_12" },
            { "man13", "man_13" },
            { "man14", "man_14" },
            { "man15", "man_15" },
            { "woman1", "woman_01" },
            { "woman2", "woman_02" },
            { "woman3", "woman_03" },
            { "woman4", "woman_04" },
            { "woman5", "woman_05" },
            { "woman6", "woman_06" },
            { "woman7", "woman_07" },
            { "woman8", "woman_08" },
            { "woman9", "woman_09" },
            { "woman10", "woman_10" },
            { "woman11", "woman_11" },
            { "woman12", "woman_12" },
            { "woman13", "woman_13" },
            { "woman14", "woman_14" },
            { "woman15", "woman_15" },
            { "woman16", "woman_16" },
            { "woman17", "woman_17" },
            { "woman18", "woman_18" },
            { "woman19", "woman_19" },
            { "woman20", "woman_20" },
            { "woman21", "woman_21" },
            { "woman22", "woman_22" },
            { "woman23", "woman_23" },
            { "woman24", "woman_24" },
            { "woman25", "woman_25" }
        };
    }

    private void OnPromptLanguageChanged()
    {
        if (!string.IsNullOrEmpty(currentCharacterId))
        {
            RefreshPrompt();
        }
    }

    private void ResetPrompt()
    {
        string charCode = GetPromptCharacterCode();
        if (!string.IsNullOrEmpty(charCode))
        {
            RefreshPrompt(isOrigin: true);
        }
    }

    private async void SavePrompt()
    {
        string charCode = GetPromptCharacterCode();
        string lang = GetSelectedPromptLanguage();

        if (string.IsNullOrEmpty(charCode) || promptView == null)
        {
            Debug.LogWarning($"[CharacterDetail][Prompt] Save skipped. charCode={charCode}, hasPromptView={promptView != null}");
            return;
        }

        CharacterPromptManager promptManager = EnsureCharacterPromptManager();
        if (promptManager == null)
        {
            Debug.LogError($"[CharacterDetail][Prompt] Save failed. CharacterPromptManager unavailable. charCode={charCode}, lang={lang}");
            return;
        }

        string prompt = promptView.Text;
        bool saved = await promptManager.SavePromptAsync(charCode, lang, prompt);
        if (!saved)
        {
            Debug.LogWarning($"[CharacterDetail][Prompt] Save failed. charCode={charCode}, lang={lang}");
            return;
        }

        Debug.Log($"[CharacterDetail][Prompt] Save complete. charCode={charCode}, lang={lang}");
    }

    private string GetSelectedPromptLanguage()
    {
        return promptView != null ? promptView.GetSelectedLanguage() : "ko";
    }

    private string GetPromptCharacterCode()
    {
        string targetName = string.Empty;

        if (currentClothesInfo != null && !string.IsNullOrEmpty(currentClothesInfo.charAttr_nickname))
        {
            targetName = currentClothesInfo.charAttr_nickname;
        }
        else if (currentCharInfo != null && !string.IsNullOrEmpty(currentCharInfo.name))
        {
            targetName = currentCharInfo.name;
        }

        return targetName.ToLower();
    }

    private int GetConversationCount()
    {
        if (MemoryManager.Instance == null)
        {
            return 0;
        }

        string nickname = GetPromptCharacterCode();
        return MemoryManager.Instance.GetAllConversationMemory(nickname).Count;
    }

    private ChangeCharClothesInfo GetDefaultClothes(ChangeCharInfo charInfo)
    {
        if (charInfo == null || charInfo.clothesList == null || charInfo.clothesList.Count == 0)
        {
            return null;
        }

        return charInfo.clothesList[0];
    }

    private void AutoBindMissingReferences()
    {
        hideButton = hideButton != null ? hideButton : FindComponent<Button>("HideButton");
        infoScrollRect = infoScrollRect != null ? infoScrollRect : GetComponentInChildren<ScrollRect>(true);
        infoContent = infoContent != null ? infoContent : FindRect("InfoContent");
        portraitImage = portraitImage != null ? portraitImage : FindComponent<Image>("PortraitImage");
        portraitPlaceholderText = portraitPlaceholderText != null ? portraitPlaceholderText : FindComponent<TextMeshProUGUI>("PortraitPlaceholderText");
        nameText = nameText != null ? nameText : FindComponent<TextMeshProUGUI>("NameText");
        sourceText = sourceText != null ? sourceText : FindComponent<TextMeshProUGUI>("SourceText");
        formText = formText != null ? formText : FindComponent<TextMeshProUGUI>("FormText");
        statusAvailableTag = statusAvailableTag != null ? statusAvailableTag : FindObject("StatusTag_Available");
        statusDownloadRequiredTag = statusDownloadRequiredTag != null ? statusDownloadRequiredTag : FindObject("StatusTag_DownloadRequired");
        affectionValueText = affectionValueText != null ? affectionValueText : FindComponent<TextMeshProUGUI>("AffectionValueText");
        affectionLabelText = affectionLabelText != null ? affectionLabelText : FindComponent<TextMeshProUGUI>("AffectionLabelText");
        affectionBarYellow = affectionBarYellow != null ? affectionBarYellow : FindRect("AffectionBarFillYellow");
        affectionBarOrange = affectionBarOrange != null ? affectionBarOrange : FindRect("AffectionBarFillOrange");
        affectionBarRed = affectionBarRed != null ? affectionBarRed : FindRect("AffectionBarFillRed");
        featureTagContainer = featureTagContainer != null ? featureTagContainer : FindTransform("FeatureTagContainer");
        voiceDropdown = voiceDropdown != null ? voiceDropdown : FindComponent<TMP_Dropdown>("VoiceDropdown");
        voiceSamplePlayButton = voiceSamplePlayButton != null ? voiceSamplePlayButton : FindComponent<Button>("VoiceSamplePlayButton");
        promptArea = promptArea != null ? promptArea : FindRect("PromptArea");
        promptToggleButton = promptToggleButton != null ? promptToggleButton : FindComponent<Button>("PromptToggleButton");
        promptToggleImage = promptToggleImage != null && promptToggleImage.gameObject.name != "PromptToggleButton_Text" ? promptToggleImage : FindComponent<Image>("PromptToggleButton");
        promptToggleText = promptToggleText != null ? promptToggleText : FindComponent<TextMeshProUGUI>("PromptToggleButton_Text");
        promptLanguageDropdown = promptLanguageDropdown != null ? promptLanguageDropdown : FindComponent<TMP_Dropdown>("PromptLanguageDropdown");
        promptCopyButton = promptCopyButton != null ? promptCopyButton : FindComponent<Button>("PromptCopyButton");
        promptResetButton = promptResetButton != null ? promptResetButton : FindComponent<Button>("PromptResetButton");
        promptSaveButton = promptSaveButton != null ? promptSaveButton : FindComponent<Button>("PromptSaveButton");
        promptInputField = promptInputField != null ? promptInputField : FindComponent<TMP_InputField>("PromptInputField");
        promptView = promptView != null ? promptView : GetComponent<CharacterDetailPromptView>();
        conversationCountText = conversationCountText != null ? conversationCountText : FindComponent<TextMeshProUGUI>("ConversationCountText");
        costumeCountText = costumeCountText != null ? costumeCountText : FindComponent<TextMeshProUGUI>("CostumeCountText");
        defaultAlarmVoiceLabel = defaultAlarmVoiceLabel != null ? defaultAlarmVoiceLabel : FindRect("DefaultAlarmVoiceLabelText");
        alarmSamplePlayButton = alarmSamplePlayButton != null ? alarmSamplePlayButton : FindRect("AlarmSamplePlayButton");
        alarmGenerateButton = alarmGenerateButton != null ? alarmGenerateButton : FindRect("AlarmGenerateButton");
        alarmGeneratedPlayButton = alarmGeneratedPlayButton != null ? alarmGeneratedPlayButton : FindRect("AlarmGeneratedPlayButton");
    }

    private void SetFillWidth(RectTransform target, float ratio)
    {
        if (target == null || target.parent == null)
        {
            return;
        }

        RectTransform parent = target.parent as RectTransform;
        float width = parent != null ? parent.rect.width : 0f;
        Vector2 size = target.sizeDelta;
        size.x = width * Mathf.Clamp01(ratio);
        target.sizeDelta = size;
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private static void SetButtonInteractable(Button target, bool interactable)
    {
        if (target != null)
        {
            target.interactable = interactable;
        }
    }

    private static void SetButtonImageColor(Button target, Color color)
    {
        if (target == null)
        {
            return;
        }

        Image image = target.targetGraphic as Image;
        if (image == null)
        {
            image = target.GetComponent<Image>();
        }

        if (image != null)
        {
            image.color = color;
        }
    }

    private static void SetY(Component target, float y)
    {
        if (target == null)
        {
            return;
        }

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 pos = rect.anchoredPosition;
            pos.y = y;
            rect.anchoredPosition = pos;
        }
    }

    private GameObject FindObject(string objectName)
    {
        Transform found = FindTransform(objectName);
        return found != null ? found.gameObject : null;
    }

    private RectTransform FindRect(string objectName)
    {
        Transform found = FindTransform(objectName);
        return found as RectTransform;
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        Transform found = FindTransform(objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    private Transform FindTransform(string objectName)
    {
        return FindDeepChild(transform, objectName);
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
