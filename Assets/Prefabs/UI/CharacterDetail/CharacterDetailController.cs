using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class CharacterDetailController : MonoBehaviour
{
    private const string NoVoiceOptionText = "없음";
    private static readonly Color ButtonActiveBlue = new Color(0.306f, 0.404f, 0.608f, 1f);
    private static readonly Color ButtonInactiveBlack = new Color(0.047f, 0.055f, 0.071f, 1f);
    private static readonly Color NegativeTagRed = new Color(0.557f, 0.184f, 0.227f, 1f);  // 네거티브 태그 칩 배경
    private static readonly Dictionary<string, string> VoiceRefIdByLabel = BuildVoiceRefIdMap();
    private static readonly Dictionary<string, string> LegacyVoiceRefIdByValue = BuildLegacyVoiceRefIdMap();
    private static readonly List<string> VoiceOptionLabels = BuildVoiceOptionLabels();
    private const float CollapsedStatsY = -334f;
    private const float CollapsedAlarmY = -374f;
    private const float ExpandedStatsY = -444f;
    private const float ExpandedAlarmY = -484f;
    private const float CollapsedAlarmListY = -416f;
    private const float ExpandedAlarmListY = -526f;
    private const float CollapsedCustomVoiceY = -374f;
    private const float ExpandedCustomVoiceY = -484f;
    private const int GeneratedAlarmChoiceCount = 3;

    [Header("Root")]
    [SerializeField] private Button hideButton;
    [SerializeField] private ScrollRect infoScrollRect;
    [SerializeField] private RectTransform infoContent;

    [Header("Character")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI portraitPlaceholderText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI sourceText;

    [Header("Clothes Selector")]
    [SerializeField] private Button clothesLeftButton;
    [SerializeField] private Button clothesRightButton;
    [SerializeField] private TextMeshProUGUI clothesText;

    [Header("Affinity")]
    [SerializeField] private TextMeshProUGUI affinityLevelText;
    [SerializeField] private TextMeshProUGUI affinityValueText;
    [SerializeField] private TextMeshProUGUI affinityLabelText;
    [SerializeField] private Image affinityBarFill;
    [SerializeField] private Image affinityBarFillMax;
    [SerializeField] private Button affinityButton;
    [SerializeField] private AffinityRewardModalView affinityRewardModal;

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
    [Header("Character Alarm Voice")]
    [SerializeField] private Toggle customAlarmVoiceToggle;
    [SerializeField] private TextMeshProUGUI customAlarmVoiceToggleText;
    [SerializeField] private Button alarmSamplePlayButton;
    [SerializeField] private Button alarmGenerateButton;
    [SerializeField] private Button alarmGeneratedPlayButton;
    [SerializeField] private RectTransform alarmVoiceListRoot;
    [SerializeField] private RectTransform alarmVoiceListContent;
    [SerializeField] private CharacterAlarmVoiceCatalog characterAlarmVoiceCatalog;
    [SerializeField] private CharacterPomodoroVoiceCatalog characterPomodoroVoiceCatalog;

    [Header("Custom Voice Navigation")]
    [SerializeField] private RectTransform customVoiceSection;
    [SerializeField] private TextMeshProUGUI alarmVoiceSummaryText;
    [SerializeField] private TextMeshProUGUI pomodoroVoiceSummaryText;
    [SerializeField] private Button alarmVoiceOpenButton;
    [SerializeField] private Button pomodoroVoiceOpenButton;

    [Header("Options")]
    [SerializeField] private Sprite promptCollapsedSprite;
    [SerializeField] private Sprite promptExpandedSprite;

    private ChangeCharInfo currentCharInfo;
    private ChangeCharClothesInfo currentClothesInfo;
    private bool promptExpanded;
    private string currentCharacterId;
    private CharacterDetailStateManager subscribedStateManager;
    private int promptRequestVersion;
    private bool isSampleVoiceRequesting;
    private bool isAlarmVoiceRequesting;
    private bool suppressAlarmToggleCallback;
    private AudioSource alarmPreviewAudioSource;
    private readonly List<GameObject> alarmVoiceListRows = new List<GameObject>();
    private readonly List<string> pendingAlarmMessages = new List<string>();
    private readonly Dictionary<int, byte[]> pendingAlarmWavData =
        new Dictionary<int, byte[]>();
    private readonly Dictionary<int, AudioClip> pendingAlarmClips =
        new Dictionary<int, AudioClip>();
    private string pendingAlarmCharacterName;
    private string alarmVoiceNoticeMessage;
    private Color? defaultTagBackgroundColor;  // 태그 칩 프리팹 원색 (슬롯 재사용 시 복원용, 최초 1회 캡처)

    // UI 언어 번역 헬퍼 — CharacterDetail 도메인 테이블 우선, 미등록 문구는 전역 LanguageData 폴백
    // (도메인 클래스가 ui_language 부재/레거시 settings를 자체 방어하므로 여기서 추가 가드 불필요)
    private static string T(string text)
    {
        return LanguageDataCharacterDetail.Translate(text);
    }

    private void Awake()
    {
        AutoBindMissingReferences();
        PopulateVoiceDropdownOptions();
        EnsurePromptView();
        RegisterEvents();
        SetPromptExpanded(false);
        if (characterAlarmVoiceCatalog == null)
        {
            characterAlarmVoiceCatalog = CharacterAlarmVoiceCatalog.LoadDefault();
        }
        if (characterPomodoroVoiceCatalog == null)
        {
            characterPomodoroVoiceCatalog = CharacterPomodoroVoiceCatalog.LoadDefault();
        }
    }

    private void Start()
    {
        subscribedStateManager = CharacterDetailStateManager.Instance;
        if (subscribedStateManager != null)
        {
            subscribedStateManager.StateChanged += OnStateChanged;
        }
        CharacterAlarmVoiceRepository.Changed += OnCharacterAlarmVoiceChanged;
        CharacterPomodoroVoiceRepository.Changed += OnCharacterAlarmVoiceChanged;
    }

    private void OnDestroy()
    {
        UnregisterEvents();
        if (subscribedStateManager != null)
        {
            subscribedStateManager.StateChanged -= OnStateChanged;
            subscribedStateManager = null;
        }
        CharacterAlarmVoiceRepository.Changed -= OnCharacterAlarmVoiceChanged;
        CharacterPomodoroVoiceRepository.Changed -= OnCharacterAlarmVoiceChanged;
    }

    public void Show(ChangeCharInfo charInfo, ChangeCharClothesInfo clothesInfo = null)
    {
        Debug.Log($"[CharacterDetail][Controller] Show start. char={charInfo?.name} clothes={clothesInfo?.text} object={name} activeBefore={gameObject.activeSelf}");

        string previousCharacterId = currentCharacterId;
        currentCharInfo = charInfo;
        currentClothesInfo = clothesInfo ?? GetDefaultClothes(charInfo);
        currentCharacterId = CharacterDetailStateManager.BuildCharacterId(currentCharInfo, currentClothesInfo);
        if (!string.Equals(
                previousCharacterId,
                currentCharacterId,
                System.StringComparison.Ordinal))
        {
            ClearPendingAlarmCandidates();
        }
        promptRequestVersion++;

        gameObject.SetActive(true);

        TranslateBakedLabels();
        CharacterDetailStateManager stateManager = CharacterDetailStateManager.Instance;
        if (stateManager == null)
        {
            Debug.LogWarning("[CharacterDetail][Controller] CharacterDetailStateManager is unavailable.");
            return;
        }
        ApplyState(stateManager.GetState(currentCharacterId));
        SetPromptExpanded(false);

        if (infoScrollRect != null)
        {
            infoScrollRect.verticalNormalizedPosition = 1f;
        }

        RectTransform rectTransform = transform as RectTransform;
        Debug.Log($"[CharacterDetail][Controller] Show complete. active={gameObject.activeSelf} world={(rectTransform != null ? rectTransform.position.ToString() : transform.position.ToString())} anchored={(rectTransform != null ? rectTransform.anchoredPosition.ToString() : "no rect")}");
    }

    // 베이크된 정적 라벨 일괄 번역 — 미등록 문자열(음성 드롭다운 라벨 등 기능 결합 문자열)은 원문 유지.
    // 스윕 후 동적 필드는 ApplyState가 재작성한다.
    private void TranslateBakedLabels()
    {
        foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
        {
            t.text = T(t.text);
        }
    }

    public void Hide()
    {
        Debug.Log($"[CharacterDetail][Controller] Hide. object={name}");
        promptRequestVersion++;
        if (alarmPreviewAudioSource != null)
        {
            alarmPreviewAudioSource.Stop();
        }
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
        SetY(customVoiceSection, expanded ? ExpandedCustomVoiceY : CollapsedCustomVoiceY);

        if (infoContent != null)
        {
            Vector2 size = infoContent.sizeDelta;
            size.y = expanded ? 720f : 610f;
            infoContent.sizeDelta = size;
        }
    }

    public void SetAffinity(int points)
    {
        int level = AffinityData.LevelFor(points);
        bool isMax = level >= AffinityData.MaxLevel;

        // 가로 배치: [Lv.03] [50/100] — MAX면 Lv.MAX만
        SetText(affinityLevelText, isMax ? "Lv.MAX" : "Lv." + level.ToString("00"));
        SetText(affinityValueText, isMax ? "" : AffinityData.PointsInLevel(points) + "/" + AffinityData.PointsPerLevel);
        SetText(affinityLabelText, T(AffinityData.StageNameFor(level)));

        // 평시 = 연노랑 게이지, MAX = 무지개 게이지
        if (affinityBarFill != null)
        {
            affinityBarFill.gameObject.SetActive(!isMax);
            affinityBarFill.fillAmount = AffinityData.ProgressInLevel(points);
        }

        if (affinityBarFillMax != null)
        {
            affinityBarFillMax.gameObject.SetActive(isMax);
            affinityBarFillMax.fillAmount = 1f;
        }
    }

    private void OpenAffinityRewardModal()
    {
        if (affinityRewardModal == null)
        {
            affinityRewardModal = GetComponentInChildren<AffinityRewardModalView>(true);
        }

        if (affinityRewardModal == null)
        {
            Debug.LogWarning("[CharacterDetail][Affinity] AffinityRewardModalView not found. Run Tools/CharacterDetail/Setup All.");
            return;
        }

        affinityRewardModal.Open(currentCharacterId);
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

        if (clothesLeftButton != null)
        {
            clothesLeftButton.onClick.AddListener(OnClickClothesLeft);
        }

        if (clothesRightButton != null)
        {
            clothesRightButton.onClick.AddListener(OnClickClothesRight);
        }

        if (voiceDropdown != null)
        {
            voiceDropdown.onValueChanged.AddListener(OnVoiceChanged);
        }

        if (voiceSamplePlayButton != null)
        {
            voiceSamplePlayButton.onClick.AddListener(OnSampleVoicePlayClicked);
        }

        if (affinityButton != null)
        {
            affinityButton.onClick.AddListener(OpenAffinityRewardModal);
        }

        if (alarmVoiceOpenButton != null)
        {
            alarmVoiceOpenButton.onClick.AddListener(OpenCharacterVoiceAlarm);
        }

        if (pomodoroVoiceOpenButton != null)
        {
            pomodoroVoiceOpenButton.onClick.AddListener(OpenCharacterVoicePomodoro);
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

        if (clothesLeftButton != null)
        {
            clothesLeftButton.onClick.RemoveListener(OnClickClothesLeft);
        }

        if (clothesRightButton != null)
        {
            clothesRightButton.onClick.RemoveListener(OnClickClothesRight);
        }

        if (voiceDropdown != null)
        {
            voiceDropdown.onValueChanged.RemoveListener(OnVoiceChanged);
        }

        if (voiceSamplePlayButton != null)
        {
            voiceSamplePlayButton.onClick.RemoveListener(OnSampleVoicePlayClicked);
        }

        if (affinityButton != null)
        {
            affinityButton.onClick.RemoveListener(OpenAffinityRewardModal);
        }

        if (alarmVoiceOpenButton != null)
        {
            alarmVoiceOpenButton.onClick.RemoveListener(OpenCharacterVoiceAlarm);
        }

        if (pomodoroVoiceOpenButton != null)
        {
            pomodoroVoiceOpenButton.onClick.RemoveListener(OpenCharacterVoicePomodoro);
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

    private void OnCharacterAlarmVoiceChanged(string characterName)
    {
        if (string.Equals(
                characterName,
                GetAlarmCharacterName(),
                System.StringComparison.OrdinalIgnoreCase))
        {
            RefreshCustomVoiceSummary();
        }
    }

    private void ApplyState(CharacterDetailState state)
    {
        if (state == null) return;

        string displayName = currentCharInfo != null ? currentCharInfo.name : T("캐릭터 이름");

        SetText(nameText, displayName);
        SetText(sourceText, T("출전") + " : " + T(state.source));

        RefreshClothesSelector();
        RefreshPortrait();

        // 기능 태그는 의상 단위 (character_database.json 의상 엔트리의 bool 4종 + tagSpecials)
        RefreshFeatureTags(CharacterFeatureTags.BuildDisplayTags(currentClothesInfo));

        RefreshPrompt();

        string normalizedVoiceId = NormalizeVoiceRefId(state.voiceId);
        ApplyVoiceDropdownValue(normalizedVoiceId);
        if (state.voiceId != normalizedVoiceId && !string.IsNullOrEmpty(currentCharacterId))
        {
            CharacterDetailStateManager.Instance?.SetVoice(currentCharacterId, normalizedVoiceId);
        }

        int conversationCount = GetConversationCount();
        int costumeCount = currentCharInfo != null && currentCharInfo.clothesList != null ? currentCharInfo.clothesList.Count : 0;

        SetText(conversationCountText, T("대화횟수") + " : " + conversationCount);
        SetText(costumeCountText, T("복장 수") + " : " + costumeCount);
        SetAffinity(state.affinityPoints);
        RefreshCustomVoiceSummary();
    }

    private void RefreshPrompt(bool isOrigin = false)
    {
        string lang = GetSelectedPromptLanguage();
        string charCode = GetPromptCharacterCode();

        if (promptView != null)
        {
            promptView.SetTextWithoutNotify(isOrigin ? T("초기화 중...") : T("로딩 중..."));
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

        GameObject managerHost = GameManager.Instance != null
            ? GameManager.Instance.gameObject
            : UIManager.Instance != null
                ? UIManager.Instance.gameObject
                : null;
        if (managerHost == null)
        {
            Debug.LogWarning("[CharacterDetail][Prompt] CharacterPromptManager host is unavailable.");
            return null;
        }

        CharacterPromptManager created = managerHost.GetComponent<CharacterPromptManager>();
        if (created == null)
        {
            created = managerHost.AddComponent<CharacterPromptManager>();
        }
        Debug.Log("[CharacterDetail][Prompt] CharacterPromptManager was attached to the existing manager host.");
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

    private void RefreshFeatureTags(List<CharacterFeatureTags.DisplayTag> displayTags)
    {
        if (featureTagContainer == null || displayTags == null)
        {
            return;
        }

        // 태그가 하나도 없으면 "사용가능 기능 태그" 라벨도 함께 숨긴다 (JSON 미등재 캐릭터)
        GameObject label = FindObject("FeatureTagsLabelText");
        SetActive(label, displayTags.Count > 0);

        for (int i = 0; i < featureTagContainer.childCount; i++)
        {
            Transform existing = featureTagContainer.GetChild(i);
            bool shouldShow = i < displayTags.Count;
            existing.gameObject.SetActive(shouldShow);

            if (shouldShow)
            {
                TextMeshProUGUI text = existing.GetComponentInChildren<TextMeshProUGUI>(true);
                SetText(text, T(displayTags[i].text));
                ApplyTagBackground(existing, displayTags[i].isNegative);
            }
        }
    }

    // 의상 셀렉터 표시 갱신 — 목록은 카드와 동일한 selectable 의상(LoadDatabase에서 필터 완료)
    private void RefreshClothesSelector()
    {
        int clothesCount = currentCharInfo != null && currentCharInfo.clothesList != null ? currentCharInfo.clothesList.Count : 0;
        SetText(clothesText, currentClothesInfo != null ? currentClothesInfo.text : "-");

        // 의상이 하나뿐이면 좌우 버튼 비활성화
        bool hasMultiple = clothesCount > 1;
        SetButtonInteractable(clothesLeftButton, hasMultiple);
        SetButtonInteractable(clothesRightButton, hasMultiple);
    }

    private void OnClickClothesLeft()
    {
        CycleClothes(-1);
    }

    private void OnClickClothesRight()
    {
        CycleClothes(1);
    }

    // 현재 의상에서 direction만큼 순환 이동 후 Show 재호출 — 초상화/태그/친밀도/프롬프트가 의상 기준으로 전체 갱신된다
    private void CycleClothes(int direction)
    {
        if (currentCharInfo == null || currentCharInfo.clothesList == null || currentCharInfo.clothesList.Count <= 1)
        {
            return;
        }

        int count = currentCharInfo.clothesList.Count;
        int index = currentCharInfo.clothesList.IndexOf(currentClothesInfo);
        if (index < 0)
        {
            index = 0;
        }

        index = (index + direction + count) % count;
        Show(currentCharInfo, currentCharInfo.clothesList[index]);
    }

    // 태그 칩 배경색 적용 — 네거티브는 붉은색, 일반은 프리팹 원색 복원 (슬롯이 인덱스 재사용되므로 매번 지정)
    private void ApplyTagBackground(Transform slot, bool isNegative)
    {
        Image background = slot.GetComponent<Image>();
        if (background == null)
        {
            background = slot.GetComponentInChildren<Image>(true);
        }
        if (background == null)
        {
            return;
        }

        if (defaultTagBackgroundColor.HasValue == false)
        {
            defaultTagBackgroundColor = background.color;
        }
        background.color = isNegative ? NegativeTagRed : defaultTagBackgroundColor.Value;
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
            CharacterDetailStateManager.Instance?.SetVoice(currentCharacterId, voiceId);
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

    private void OpenCharacterVoiceAlarm()
    {
        CharacterVoiceViewLauncher.ShowAlarm(
            GetAlarmCharacterName(),
            GetSelectedAlarmVoiceRefId(),
            GetAlarmLanguage(),
            GetAlarmVoiceSpeedPercent(),
            characterAlarmVoiceCatalog);
    }

    private void OpenCharacterVoicePomodoro()
    {
        CharacterVoiceViewLauncher.ShowPomodoro(
            GetAlarmCharacterName(),
            GetSelectedAlarmVoiceRefId(),
            GetAlarmLanguage(),
            GetAlarmVoiceSpeedPercent(),
            characterPomodoroVoiceCatalog);
    }

    private void RefreshCustomVoiceSummary()
    {
        string characterName = GetAlarmCharacterName();
        int defaultCount = 0;
        int generatedCount = 0;
        List<CharacterAlarmPlaybackCandidate> candidates =
            CharacterAlarmVoiceRepository.GetDisplayCandidates(
                characterName,
                characterAlarmVoiceCatalog);
        for (int i = 0; i < candidates.Count; i++)
        {
            CharacterAlarmPlaybackCandidate candidate = candidates[i];
            if (candidate == null) continue;
            if (candidate.isGenerated) generatedCount++;
            else defaultCount++;
        }

        SetText(
            alarmVoiceSummaryText,
            T("기본") + " " + defaultCount + " · " + T("생성") + " " + generatedCount);
        int pomodoroDefaultCount = 0;
        int pomodoroGeneratedCount = 0;
        List<CharacterPomodoroPlaybackCandidate> pomodoroCandidates =
            CharacterPomodoroVoiceRepository.GetDisplayCandidates(
                characterName,
                characterPomodoroVoiceCatalog);
        for (int i = 0; i < pomodoroCandidates.Count; i++)
        {
            CharacterPomodoroPlaybackCandidate candidate = pomodoroCandidates[i];
            if (candidate == null) continue;
            if (candidate.isGenerated) pomodoroGeneratedCount++;
            else pomodoroDefaultCount++;
        }
        SetText(
            pomodoroVoiceSummaryText,
            T("기본") + " " + pomodoroDefaultCount + " · " +
            T("생성") + " " + pomodoroGeneratedCount);
    }

    private void OnCustomAlarmVoiceToggleChanged(bool enabled)
    {
        if (suppressAlarmToggleCallback)
        {
            return;
        }

        string characterName = GetAlarmCharacterName();
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return;
        }

        CharacterAlarmVoiceRepository.SetCustomAlarmVoiceEnabled(characterName, enabled);
        RefreshAlarmVoiceUi();
    }

    private void OnAlarmSamplePlayClicked()
    {
        string characterName = GetAlarmCharacterName();
        List<CharacterAlarmPlaybackCandidate> candidates =
            CharacterAlarmVoiceRepository.GetDisplayCandidates(characterName, characterAlarmVoiceCatalog);
        candidates.RemoveAll(item =>
            item == null ||
            item.isGenerated ||
            !item.enabled ||
            item.audioClip == null ||
            string.IsNullOrWhiteSpace(item.message));

        if (candidates.Count > 0)
        {
            PlayAlarmCandidate(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        }
    }

    private void OnAlarmGenerateClicked()
    {
        if (!isAlarmVoiceRequesting)
        {
            StartCoroutine(RequestGeneratedAlarmVoice());
        }
    }

    private void OnAlarmGeneratedPlayClicked()
    {
        string characterName = GetAlarmCharacterName();
        List<CharacterAlarmPlaybackCandidate> candidates =
            CharacterAlarmVoiceRepository.GetDisplayCandidates(characterName, characterAlarmVoiceCatalog);
        candidates.RemoveAll(item =>
            item == null ||
            !item.isGenerated ||
            string.IsNullOrWhiteSpace(item.audioFilePath) ||
            !File.Exists(item.audioFilePath) ||
            string.IsNullOrWhiteSpace(item.message));

        if (candidates.Count > 0)
        {
            PlayAlarmCandidate(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
        }
    }

    private IEnumerator RequestGeneratedAlarmVoice()
    {
        string characterName = GetAlarmCharacterName();
        if (string.IsNullOrWhiteSpace(characterName))
        {
            yield break;
        }

        ClearPendingAlarmCandidates();
        alarmVoiceNoticeMessage = string.Empty;
        isAlarmVoiceRequesting = true;
        RefreshAlarmVoiceUi();

        string baseUrl = null;
        yield return ResolveSampleVoiceBaseUrl(url => baseUrl = url);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            FinishAlarmVoiceRequest("[CharacterDetail][AlarmVoice] Server base URL is empty.");
            yield break;
        }

        string language = GetAlarmLanguage();
        WWWForm form = new WWWForm();
        form.AddField("character_name", characterName);
        form.AddField("lang", language);
        form.AddField("num_alarms", GeneratedAlarmChoiceCount);

        using (UnityWebRequest messageRequest =
               UnityWebRequest.Post(baseUrl.TrimEnd('/') + "/agent/alarm/make", form))
        {
            yield return messageRequest.SendWebRequest();
            if (messageRequest.result != UnityWebRequest.Result.Success)
            {
                FinishAlarmVoiceRequest(
                    $"[CharacterDetail][AlarmVoice] Message generation failed. error={messageRequest.error}");
                yield break;
            }

            List<string> responseMessages = null;
            try
            {
                JObject response = JObject.Parse(messageRequest.downloadHandler.text);
                JToken messageList = response["alarm_messages"];
                if (string.Equals(
                        response.Value<string>("status"),
                        "success",
                        System.StringComparison.OrdinalIgnoreCase) &&
                    messageList != null &&
                    messageList.Type == JTokenType.Array)
                {
                    responseMessages = new List<string>();
                    foreach (JToken item in messageList.Children())
                    {
                        if (item.Type != JTokenType.String)
                        {
                            continue;
                        }

                        string candidate = item.Value<string>();
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            candidate = candidate.Trim();
                            if (!responseMessages.Contains(candidate))
                            {
                                responseMessages.Add(candidate);
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CharacterDetail][AlarmVoice] Invalid agent response: {e.Message}");
            }

            if (responseMessages == null || responseMessages.Count == 0)
            {
                FinishAlarmVoiceRequest(
                    "[CharacterDetail][AlarmVoice] Response does not contain a non-empty alarm_messages JSON array.");
                yield break;
            }

            for (int i = 0;
                 i < responseMessages.Count &&
                 pendingAlarmMessages.Count < GeneratedAlarmChoiceCount;
                 i++)
            {
                pendingAlarmMessages.Add(responseMessages[i]);
            }
        }

        if (pendingAlarmMessages.Count == 0)
        {
            FinishAlarmVoiceRequest(
                "[CharacterDetail][AlarmVoice] Generated alarm message list has no valid entry.");
            yield break;
        }

        pendingAlarmCharacterName = characterName;
        alarmVoiceNoticeMessage = string.Empty;
        isAlarmVoiceRequesting = false;
        RefreshAlarmVoiceUi();
    }

    private void OnPendingAlarmSampleClicked(int candidateIndex)
    {
        if (!isAlarmVoiceRequesting)
        {
            StartCoroutine(RequestPendingAlarmVoice(candidateIndex, false));
        }
    }

    private void OnPendingAlarmUseClicked(int candidateIndex)
    {
        if (!isAlarmVoiceRequesting)
        {
            StartCoroutine(RequestPendingAlarmVoice(candidateIndex, true));
        }
    }

    private IEnumerator RequestPendingAlarmVoice(int candidateIndex, bool saveAfterRequest)
    {
        if (candidateIndex < 0 || candidateIndex >= pendingAlarmMessages.Count)
        {
            yield break;
        }

        string characterName = GetAlarmCharacterName();
        if (string.IsNullOrWhiteSpace(characterName) ||
            !string.Equals(
                characterName,
                pendingAlarmCharacterName,
                System.StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        alarmVoiceNoticeMessage = string.Empty;
        string alarmMessage = pendingAlarmMessages[candidateIndex];
        if (pendingAlarmWavData.TryGetValue(candidateIndex, out byte[] cachedWav) &&
            pendingAlarmClips.TryGetValue(candidateIndex, out AudioClip cachedClip) &&
            cachedWav != null &&
            cachedWav.Length > 0 &&
            cachedClip != null)
        {
            if (saveAfterRequest)
            {
                SavePendingAlarmCandidate(
                    characterName,
                    alarmMessage,
                    cachedWav,
                    GetSelectedAlarmVoiceRefId(),
                    GetAlarmLanguage());
            }
            else
            {
                PlayAlarmPreview(alarmMessage, cachedClip);
            }
            yield break;
        }

        isAlarmVoiceRequesting = true;
        RefreshAlarmVoiceUi();
        int requestVersion = promptRequestVersion;

        string baseUrl = null;
        yield return ResolveSampleVoiceBaseUrl(url => baseUrl = url);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            FinishAlarmVoiceRequest("[CharacterDetail][AlarmVoice] Server base URL is empty.");
            yield break;
        }

        string language = GetAlarmLanguage();
        string refId = GetSelectedAlarmVoiceRefId();
        Dictionary<string, string> requestData = new Dictionary<string, string>
        {
            { "text", alarmMessage },
            { "char", characterName },
            { "lang", language },
            { "speed", GetAlarmVoiceSpeedPercent() },
            { "chatIdx", "-1" }
        };
        if (!string.IsNullOrWhiteSpace(refId))
        {
            requestData["ref_id"] = refId;
        }

        string ttsUrl = baseUrl.TrimEnd('/') + "/getSound";
        using (UnityWebRequest ttsRequest = new UnityWebRequest(ttsUrl, "POST"))
        {
            byte[] requestBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestData));
            ttsRequest.uploadHandler = new UploadHandlerRaw(requestBody);
            ttsRequest.downloadHandler = new DownloadHandlerAudioClip(ttsUrl, AudioType.WAV);
            ttsRequest.SetRequestHeader("Content-Type", "application/json");

            yield return ttsRequest.SendWebRequest();
            if (ttsRequest.result != UnityWebRequest.Result.Success)
            {
                FinishAlarmVoiceRequest(
                    $"[CharacterDetail][AlarmVoice] TTS generation failed. error={ttsRequest.error}");
                yield break;
            }

            if (requestVersion != promptRequestVersion ||
                candidateIndex >= pendingAlarmMessages.Count ||
                !string.Equals(
                    pendingAlarmMessages[candidateIndex],
                    alarmMessage,
                    System.StringComparison.Ordinal))
            {
                FinishAlarmVoiceRequest(null);
                yield break;
            }

            byte[] wavData = ttsRequest.downloadHandler.data;
            AudioClip clip = DownloadHandlerAudioClip.GetContent(ttsRequest);
            if (wavData == null || wavData.Length == 0 || clip == null)
            {
                FinishAlarmVoiceRequest(
                    "[CharacterDetail][AlarmVoice] TTS response did not contain valid WAV audio.");
                yield break;
            }

            pendingAlarmWavData[candidateIndex] = wavData;
            pendingAlarmClips[candidateIndex] = clip;

            if (saveAfterRequest)
            {
                if (!SavePendingAlarmCandidate(
                        characterName,
                        alarmMessage,
                        wavData,
                        refId,
                        language))
                {
                    yield break;
                }
            }
            else
            {
                alarmVoiceNoticeMessage = string.Empty;
                PlayAlarmPreview(alarmMessage, clip);
            }
        }

        isAlarmVoiceRequesting = false;
        RefreshAlarmVoiceUi();
    }

    private bool SavePendingAlarmCandidate(
        string characterName,
        string alarmMessage,
        byte[] wavData,
        string refId,
        string language)
    {
        CharacterAlarmVoiceRecord saved = CharacterAlarmVoiceRepository.AddGeneratedAlarm(
            characterName,
            alarmMessage,
            wavData,
            refId,
            language);
        if (saved == null)
        {
            FinishAlarmVoiceRequest("[CharacterDetail][AlarmVoice] Generated WAV save failed.");
            return false;
        }

        ClearPendingAlarmCandidates();
        alarmVoiceNoticeMessage = string.Empty;
        isAlarmVoiceRequesting = false;
        RefreshAlarmVoiceUi();
        return true;
    }

    private void ClearPendingAlarmCandidates()
    {
        foreach (KeyValuePair<int, AudioClip> item in pendingAlarmClips)
        {
            AudioClip clip = item.Value;
            if (clip == null)
            {
                continue;
            }

            if (alarmPreviewAudioSource != null && alarmPreviewAudioSource.clip == clip)
            {
                alarmPreviewAudioSource.Stop();
                alarmPreviewAudioSource.clip = null;
            }

            Destroy(clip);
        }

        pendingAlarmMessages.Clear();
        pendingAlarmWavData.Clear();
        pendingAlarmClips.Clear();
        pendingAlarmCharacterName = string.Empty;
    }

    private void FinishAlarmVoiceRequest(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            Debug.LogError(error);
            alarmVoiceNoticeMessage = "알람 음성 생성에 실패했습니다. 다시 시도해주세요.";
        }

        isAlarmVoiceRequesting = false;
        RefreshAlarmVoiceUi();
    }

    private void RefreshAlarmVoiceUi()
    {
        string characterName = GetAlarmCharacterName();
        if (string.IsNullOrWhiteSpace(characterName))
        {
            return;
        }

        CharacterAlarmVoiceMetadata metadata = CharacterAlarmVoiceRepository.Load(characterName);
        if (customAlarmVoiceToggle != null)
        {
            suppressAlarmToggleCallback = true;
            customAlarmVoiceToggle.SetIsOnWithoutNotify(metadata.customAlarmVoiceEnabled);
            suppressAlarmToggleCallback = false;
        }

        if (customAlarmVoiceToggleText != null)
        {
            customAlarmVoiceToggleText.text = T("커스텀 알람음성") + " : " +
                                              T(metadata.customAlarmVoiceEnabled ? "사용중" : "사용안함");
        }

        RebuildAlarmVoiceList(characterName);
        RefreshAlarmVoiceButtonState();
    }

    private void RefreshAlarmVoiceButtonState()
    {
        string characterName = GetAlarmCharacterName();
        List<CharacterAlarmPlaybackCandidate> candidates =
            CharacterAlarmVoiceRepository.GetDisplayCandidates(characterName, characterAlarmVoiceCatalog);
        bool hasDefault = candidates.Exists(item =>
            item != null &&
            !item.isGenerated &&
            item.enabled &&
            item.audioClip != null &&
            !string.IsNullOrWhiteSpace(item.message));
        bool hasGenerated = candidates.Exists(item =>
            item != null &&
            item.isGenerated &&
            !string.IsNullOrWhiteSpace(item.audioFilePath) &&
            File.Exists(item.audioFilePath) &&
            !string.IsNullOrWhiteSpace(item.message));

        if (alarmSamplePlayButton != null)
        {
            alarmSamplePlayButton.interactable = !isAlarmVoiceRequesting && hasDefault;
        }

        if (alarmGenerateButton != null)
        {
            alarmGenerateButton.interactable = !isAlarmVoiceRequesting &&
                                                !string.IsNullOrWhiteSpace(characterName);
        }

        if (alarmGeneratedPlayButton != null)
        {
            alarmGeneratedPlayButton.interactable = !isAlarmVoiceRequesting && hasGenerated;
        }
    }

    private void RebuildAlarmVoiceList(string characterName)
    {
        if (alarmVoiceListContent == null)
        {
            return;
        }

        for (int i = 0; i < alarmVoiceListRows.Count; i++)
        {
            if (alarmVoiceListRows[i] != null)
            {
                alarmVoiceListRows[i].SetActive(false);
                Destroy(alarmVoiceListRows[i]);
            }
        }
        alarmVoiceListRows.Clear();

        if (!string.IsNullOrWhiteSpace(alarmVoiceNoticeMessage))
        {
            alarmVoiceListRows.Add(CreateAlarmVoiceNoticeRow(alarmVoiceNoticeMessage));
        }

        if (pendingAlarmMessages.Count > 0 &&
            string.Equals(
                pendingAlarmCharacterName,
                characterName,
                System.StringComparison.OrdinalIgnoreCase))
        {
            alarmVoiceListRows.Add(CreateAlarmCandidateHeader());
            for (int i = 0; i < pendingAlarmMessages.Count; i++)
            {
                alarmVoiceListRows.Add(CreateAlarmCandidateRow(i, pendingAlarmMessages[i]));
            }
        }

        List<CharacterAlarmPlaybackCandidate> candidates =
            CharacterAlarmVoiceRepository.GetDisplayCandidates(characterName, characterAlarmVoiceCatalog);
        for (int i = 0; i < candidates.Count; i++)
        {
            alarmVoiceListRows.Add(CreateAlarmVoiceListRow(characterName, candidates[i]));
        }
    }

    private GameObject CreateAlarmVoiceNoticeRow(string message)
    {
        GameObject notice = MemoryArchiveUi.CreatePanel(
            "AlarmVoiceNotice",
            alarmVoiceListContent,
            new Color(0.24f, 0.105f, 0.12f, 1f));
        MemoryArchiveUi.Layout(notice, minH: 48f, prefH: 48f);
        TextMeshProUGUI text = MemoryArchiveUi.CreateText(
            "NoticeText",
            notice.transform,
            T(message),
            13f,
            new Color(1f, 0.76f, 0.78f, 1f),
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.SetStretch(text.gameObject, new Vector4(10f, 0f, 10f, 0f));
        ApplyAlarmListFont(text);
        return notice;
    }

    private GameObject CreateAlarmCandidateHeader()
    {
        GameObject header = MemoryArchiveUi.CreatePanel(
            "AlarmCandidateHeader",
            alarmVoiceListContent,
            new Color(0.075f, 0.09f, 0.12f, 1f));
        MemoryArchiveUi.Layout(header, minH: 34f, prefH: 34f);
        TextMeshProUGUI text = MemoryArchiveUi.CreateText(
            "Text",
            header.transform,
            T("알람 후보") + " · " + T("샘플을 듣고 사용할 대사를 선택하세요."),
            13f,
            MemoryArchiveUi.TextMuted,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.SetStretch(text.gameObject, new Vector4(8f, 0f, 8f, 0f));
        ApplyAlarmListFont(text);
        return header;
    }

    private GameObject CreateAlarmCandidateRow(int candidateIndex, string message)
    {
        GameObject row = MemoryArchiveUi.CreatePanel(
            "AlarmCandidateRow_" + (candidateIndex + 1),
            alarmVoiceListContent,
            MemoryArchiveUi.PanelBg2);
        MemoryArchiveUi.Layout(row, minH: 78f, prefH: 78f);
        VerticalLayoutGroup column = MemoryArchiveUi.AddColumn(
            row,
            5f,
            new RectOffset(8, 8, 7, 7));
        column.childForceExpandWidth = true;

        TextMeshProUGUI messageText = MemoryArchiveUi.CreateText(
            "MessageText",
            row.transform,
            T("후보") + (candidateIndex + 1) + "  " + message,
            14f,
            MemoryArchiveUi.TextWhite,
            TextAlignmentOptions.MidlineLeft);
        MemoryArchiveUi.Layout(messageText.gameObject, minH: 28f, prefH: 28f);
        ApplyAlarmListFont(messageText);

        GameObject buttonRow = MemoryArchiveUi.CreateUIObject("ButtonRow", row.transform);
        MemoryArchiveUi.Layout(buttonRow, minH: 28f, prefH: 28f);
        HorizontalLayoutGroup buttons = MemoryArchiveUi.AddRow(buttonRow, 6f);
        buttons.childForceExpandHeight = true;

        string sampleLabel = pendingAlarmClips.ContainsKey(candidateIndex)
            ? T("다시 듣기")
            : T("샘플 듣기");
        Button sampleButton = MemoryArchiveUi.CreateButton(
            "SampleButton",
            buttonRow.transform,
            sampleLabel,
            MemoryArchiveUi.PanelBg,
            12f);
        MemoryArchiveUi.Layout(
            sampleButton.gameObject,
            minH: 26f,
            prefH: 26f,
            flexW: 1f);
        ApplyAlarmListFont(sampleButton.GetComponentInChildren<TextMeshProUGUI>(true));
        sampleButton.interactable = !isAlarmVoiceRequesting;
        sampleButton.onClick.AddListener(
            () => OnPendingAlarmSampleClicked(candidateIndex));

        Button useButton = MemoryArchiveUi.CreateButton(
            "UseButton",
            buttonRow.transform,
            T("사용하기"),
            MemoryArchiveUi.Accent,
            12f);
        MemoryArchiveUi.Layout(
            useButton.gameObject,
            minH: 26f,
            prefH: 26f,
            flexW: 1f);
        ApplyAlarmListFont(useButton.GetComponentInChildren<TextMeshProUGUI>(true));
        useButton.interactable = !isAlarmVoiceRequesting;
        useButton.onClick.AddListener(
            () => OnPendingAlarmUseClicked(candidateIndex));

        return row;
    }

    private void ApplyAlarmListFont(TextMeshProUGUI text)
    {
        if (text != null && customAlarmVoiceToggleText != null)
        {
            text.font = customAlarmVoiceToggleText.font;
        }
    }

    private GameObject CreateAlarmVoiceListRow(
        string characterName,
        CharacterAlarmPlaybackCandidate candidate)
    {
        GameObject row = new GameObject(
            "AlarmVoiceRow_" + candidate.id,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        row.layer = gameObject.layer;
        row.transform.SetParent(alarmVoiceListContent, false);

        Image background = row.GetComponent<Image>();
        background.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = new Color(0.11f, 0.13f, 0.17f, 1f);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredHeight = 38f;

        TextMeshProUGUI messageText = CreateAlarmRowText(row.transform);
        messageText.text = candidate.label + "  " + candidate.message;
        LayoutElement messageLayout = messageText.gameObject.AddComponent<LayoutElement>();
        messageLayout.flexibleWidth = 1f;
        messageLayout.minWidth = 190f;

        Button playButton = CreateAlarmRowButton(row.transform, T("듣기"), 58f);
        playButton.interactable =
            !string.IsNullOrWhiteSpace(candidate.message) &&
            (candidate.audioClip != null ||
             (!string.IsNullOrWhiteSpace(candidate.audioFilePath) && File.Exists(candidate.audioFilePath)));
        playButton.onClick.AddListener(() => PlayAlarmCandidate(candidate));

        if (candidate.isGenerated)
        {
            Button enabledButton = CreateAlarmRowButton(
                row.transform,
                T(candidate.enabled ? "사용중" : "사용안함"),
                78f);
            enabledButton.onClick.AddListener(() =>
            {
                CharacterAlarmVoiceRepository.SetGeneratedAlarmEnabled(
                    characterName,
                    candidate.id,
                    !candidate.enabled);
                RefreshAlarmVoiceUi();
            });
        }
        else
        {
            TextMeshProUGUI typeText = CreateAlarmRowText(row.transform);
            typeText.text = T("기본");
            typeText.alignment = TextAlignmentOptions.Center;
            LayoutElement typeLayout = typeText.gameObject.AddComponent<LayoutElement>();
            typeLayout.preferredWidth = 78f;
        }

        return row;
    }

    private TextMeshProUGUI CreateAlarmRowText(Transform parent)
    {
        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = customAlarmVoiceToggleText != null ? customAlarmVoiceToggleText.font : null;
        text.fontSize = 14f;
        text.color = new Color(0.9f, 0.92f, 0.96f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateAlarmRowButton(Transform parent, string label, float width)
    {
        GameObject buttonObject = new GameObject(
            "Button_" + label,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.layer = gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = ButtonActiveBlue;

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;

        TextMeshProUGUI text = CreateAlarmRowText(buttonObject.transform);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        text.text = label;

        return buttonObject.GetComponent<Button>();
    }

    private void PlayAlarmCandidate(CharacterAlarmPlaybackCandidate candidate)
    {
        if (candidate == null || string.IsNullOrWhiteSpace(candidate.message))
        {
            return;
        }

        if (candidate.audioClip != null)
        {
            PlayAlarmPreview(candidate.message, candidate.audioClip);
        }
        else if (!string.IsNullOrWhiteSpace(candidate.audioFilePath))
        {
            StartCoroutine(LoadAndPlayAlarmPreview(candidate));
        }
    }

    private IEnumerator LoadAndPlayAlarmPreview(CharacterAlarmPlaybackCandidate candidate)
    {
        if (!File.Exists(candidate.audioFilePath))
        {
            yield break;
        }

        string fileUri = new System.Uri(candidate.audioFilePath).AbsoluteUri;
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.WAV))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                PlayAlarmPreview(candidate.message, DownloadHandlerAudioClip.GetContent(request));
            }
            else
            {
                Debug.LogError(
                    $"[CharacterDetail][AlarmVoice] Preview load failed. path={candidate.audioFilePath}, error={request.error}");
            }
        }
    }

    private void PlayAlarmPreview(string message, AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (alarmPreviewAudioSource == null)
        {
            alarmPreviewAudioSource = gameObject.AddComponent<AudioSource>();
            alarmPreviewAudioSource.playOnAwake = false;
        }

        alarmPreviewAudioSource.Stop();
        alarmPreviewAudioSource.clip = clip;
        alarmPreviewAudioSource.loop = false;
        alarmPreviewAudioSource.volume = GetMasterVolume();
        alarmPreviewAudioSource.Play();

        if (AnswerBalloonSimpleManager.Instance != null)
        {
            AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleForSeconds(
                message,
                clip.length + 0.5f);
        }
    }

    private string GetAlarmCharacterName()
    {
        if (currentClothesInfo != null &&
            !string.IsNullOrWhiteSpace(currentClothesInfo.charAttr_nickname))
        {
            return currentClothesInfo.charAttr_nickname.Trim();
        }

        if (currentCharInfo != null && !string.IsNullOrWhiteSpace(currentCharInfo.name))
        {
            return currentCharInfo.name.Trim();
        }

        if (CharManager.Instance != null)
        {
            GameObject currentCharacter = CharManager.Instance.GetCurrentCharacter();
            if (currentCharacter != null)
            {
                return CharManager.Instance.GetNickname(currentCharacter);
            }
        }

        return string.Empty;
    }

    private string GetAlarmLanguage()
    {
        string language = null;
        if (SettingManager.Instance != null && SettingManager.Instance.settings != null)
        {
            language = SettingManager.Instance.settings.sound_language;
        }

        if (language == "jp")
        {
            return "ja";
        }

        return string.IsNullOrWhiteSpace(language) ? "ko" : language;
    }

    private string GetAlarmVoiceSpeedPercent()
    {
        if (SettingManager.Instance != null && SettingManager.Instance.settings != null)
        {
            return SettingManager.Instance.settings.sound_speedMaster.ToString();
        }

        return "100";
    }

    private string GetSelectedAlarmVoiceRefId()
    {
        if (voiceDropdown == null ||
            voiceDropdown.options == null ||
            voiceDropdown.value < 0 ||
            voiceDropdown.value >= voiceDropdown.options.Count)
        {
            return string.Empty;
        }

        return GetVoiceRefIdFromLabel(voiceDropdown.options[voiceDropdown.value].text);
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
        clothesLeftButton = clothesLeftButton != null ? clothesLeftButton : FindComponent<Button>("ClothesLeftButton");
        clothesRightButton = clothesRightButton != null ? clothesRightButton : FindComponent<Button>("ClothesRightButton");
        clothesText = clothesText != null ? clothesText : FindComponent<TextMeshProUGUI>("ClothesText");
        affinityLevelText = affinityLevelText != null ? affinityLevelText : FindComponent<TextMeshProUGUI>("AffinityLevelText");
        affinityValueText = affinityValueText != null ? affinityValueText : FindComponent<TextMeshProUGUI>("AffinityValueText");
        affinityLabelText = affinityLabelText != null ? affinityLabelText : FindComponent<TextMeshProUGUI>("AffinityLabelText");
        affinityBarFill = affinityBarFill != null ? affinityBarFill : FindComponent<Image>("AffinityBarFill");
        affinityBarFillMax = affinityBarFillMax != null ? affinityBarFillMax : FindComponent<Image>("AffinityBarFillMax");
        affinityButton = affinityButton != null ? affinityButton : FindComponent<Button>("AffinityContainer");
        affinityRewardModal = affinityRewardModal != null ? affinityRewardModal : GetComponentInChildren<AffinityRewardModalView>(true);
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
        customAlarmVoiceToggle = customAlarmVoiceToggle != null
            ? customAlarmVoiceToggle
            : FindComponent<Toggle>("CustomAlarmVoiceToggle");
        customAlarmVoiceToggleText = customAlarmVoiceToggleText != null
            ? customAlarmVoiceToggleText
            : FindComponent<TextMeshProUGUI>("CustomAlarmVoiceToggle_Text");
        alarmSamplePlayButton = alarmSamplePlayButton != null
            ? alarmSamplePlayButton
            : FindComponent<Button>("AlarmSamplePlayButton");
        alarmGenerateButton = alarmGenerateButton != null
            ? alarmGenerateButton
            : FindComponent<Button>("AlarmGenerateButton");
        alarmGeneratedPlayButton = alarmGeneratedPlayButton != null
            ? alarmGeneratedPlayButton
            : FindComponent<Button>("AlarmGeneratedPlayButton");
        customVoiceSection = customVoiceSection != null
            ? customVoiceSection
            : FindRect("CustomVoiceSection");
        alarmVoiceSummaryText = alarmVoiceSummaryText != null
            ? alarmVoiceSummaryText
            : FindComponent<TextMeshProUGUI>("AlarmVoiceSummaryText");
        pomodoroVoiceSummaryText = pomodoroVoiceSummaryText != null
            ? pomodoroVoiceSummaryText
            : FindComponent<TextMeshProUGUI>("PomodoroVoiceSummaryText");
        alarmVoiceOpenButton = alarmVoiceOpenButton != null
            ? alarmVoiceOpenButton
            : FindComponent<Button>("AlarmVoiceOpenButton");
        pomodoroVoiceOpenButton = pomodoroVoiceOpenButton != null
            ? pomodoroVoiceOpenButton
            : FindComponent<Button>("PomodoroVoiceOpenButton");
    }

    private void EnsureAlarmVoiceRuntimeUi()
    {
        if (customAlarmVoiceToggle == null)
        {
            RectTransform legacyLabel = FindRect("DefaultAlarmVoiceLabelText");
            if (legacyLabel != null && legacyLabel.parent != null)
            {
                Transform originalParent = legacyLabel.parent;
                GameObject toggleObject = CreateAlarmRuntimeObject(
                    "CustomAlarmVoiceToggle",
                    originalParent);
                RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
                CopyRectTransform(legacyLabel, toggleRect);

                Image background = toggleObject.AddComponent<Image>();
                background.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
                background.type = Image.Type.Sliced;
                background.color = ButtonInactiveBlack;

                customAlarmVoiceToggle = toggleObject.AddComponent<Toggle>();
                customAlarmVoiceToggle.targetGraphic = background;
                customAlarmVoiceToggle.graphic = null;

                legacyLabel.SetParent(toggleRect, false);
                legacyLabel.name = "CustomAlarmVoiceToggle_Text";
                legacyLabel.anchorMin = Vector2.zero;
                legacyLabel.anchorMax = Vector2.one;
                legacyLabel.pivot = new Vector2(0.5f, 0.5f);
                legacyLabel.offsetMin = Vector2.zero;
                legacyLabel.offsetMax = Vector2.zero;
                customAlarmVoiceToggleText = legacyLabel.GetComponent<TextMeshProUGUI>();
                if (customAlarmVoiceToggleText != null)
                {
                    customAlarmVoiceToggleText.alignment = TextAlignmentOptions.Center;
                    customAlarmVoiceToggleText.raycastTarget = false;
                }
            }
        }

        if (alarmVoiceListRoot != null && alarmVoiceListContent != null)
        {
            return;
        }

        Transform parent = infoContent != null ? infoContent : transform;
        GameObject scrollObject = CreateAlarmRuntimeObject("AlarmVoiceListScroll", parent);
        alarmVoiceListRoot = scrollObject.GetComponent<RectTransform>();
        SetTopLeftRect(
            alarmVoiceListRoot,
            new Vector2(0f, CollapsedAlarmListY),
            new Vector2(440f, 170f));

        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        scrollBackground.type = Image.Type.Sliced;
        scrollBackground.color = new Color(0.055f, 0.065f, 0.085f, 0.95f);

        ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = CreateAlarmRuntimeObject(
            "AlarmVoiceListViewport",
            scrollObject.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        StretchRect(viewportRect, 4f, 4f, 4f, 4f);
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.005f);
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateAlarmRuntimeObject(
            "AlarmVoiceListContent",
            viewportObject.transform);
        alarmVoiceListContent = contentObject.GetComponent<RectTransform>();
        alarmVoiceListContent.anchorMin = new Vector2(0f, 1f);
        alarmVoiceListContent.anchorMax = new Vector2(1f, 1f);
        alarmVoiceListContent.pivot = new Vector2(0.5f, 1f);
        alarmVoiceListContent.anchoredPosition = Vector2.zero;
        alarmVoiceListContent.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = alarmVoiceListContent;
    }

    private GameObject CreateAlarmRuntimeObject(string objectName, Transform parent)
    {
        GameObject result = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer));
        result.layer = gameObject.layer;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform destination)
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.pivot = source.pivot;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void StretchRect(
        RectTransform rect,
        float left,
        float right,
        float bottom,
        float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
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
