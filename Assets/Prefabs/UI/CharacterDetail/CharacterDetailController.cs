using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailController : MonoBehaviour
{
    private const float CollapsedPromptHeight = 50f;
    private const float ExpandedPromptHeight = 172f;
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
    [SerializeField] private List<string> defaultFeatureTags = new List<string>
    {
        "AI 대화",
        "호감도 보유",
        "감정표현"
    };

    [Header("Voice")]
    [SerializeField] private TMP_Dropdown voiceDropdown;

    [Header("Prompt")]
    [SerializeField] private RectTransform promptArea;
    [SerializeField] private Button promptToggleButton;
    [SerializeField] private Image promptToggleImage;
    [SerializeField] private TextMeshProUGUI promptToggleText;
    [SerializeField] private Sprite promptCollapsedSprite;
    [SerializeField] private Sprite promptExpandedSprite;
    [SerializeField] private TMP_Dropdown promptLanguageDropdown;
    [SerializeField] private Button promptCopyButton;
    [SerializeField] private Button promptResetButton;
    [SerializeField] private TMP_InputField promptInputField;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI conversationCountText;
    [SerializeField] private TextMeshProUGUI costumeCountText;
    [SerializeField] private RectTransform defaultAlarmVoiceLabel;
    [SerializeField] private RectTransform alarmSamplePlayButton;
    [SerializeField] private RectTransform alarmGenerateButton;
    [SerializeField] private RectTransform alarmGeneratedPlayButton;

    [Header("Fallback Metadata")]
    [SerializeField] private string defaultSource = "오리지널";
    [SerializeField] private string defaultForm = "2D";

    private readonly List<GameObject> generatedFeatureTags = new List<GameObject>();
    private ChangeCharInfo currentCharInfo;
    private ChangeCharClothesInfo currentClothesInfo;
    private bool promptExpanded;
    private string originalPromptText = string.Empty;

    private void Awake()
    {
        AutoBindMissingReferences();
        RegisterEvents();
        SetPromptExpanded(false);
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    public void Show(ChangeCharInfo charInfo, ChangeCharClothesInfo clothesInfo = null)
    {
        Debug.Log($"[CharacterDetail][Controller] Show start. char={charInfo?.name} clothes={clothesInfo?.text} object={name} activeBefore={gameObject.activeSelf}");

        currentCharInfo = charInfo;
        currentClothesInfo = clothesInfo ?? GetDefaultClothes(charInfo);

        gameObject.SetActive(true);
        RefreshStaticInfo();
        RefreshPrompt();
        RefreshStats();
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
        gameObject.SetActive(false);
    }

    public void TogglePromptExpanded()
    {
        SetPromptExpanded(!promptExpanded);
    }

    public void SetPromptExpanded(bool expanded)
    {
        promptExpanded = expanded;

        if (promptArea != null)
        {
            Vector2 size = promptArea.sizeDelta;
            size.y = expanded ? ExpandedPromptHeight : CollapsedPromptHeight;
            promptArea.sizeDelta = size;

            LayoutElement layoutElement = promptArea.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredHeight = size.y;
            }
        }

        if (promptInputField != null)
        {
            promptInputField.gameObject.SetActive(expanded);
        }

        if (promptToggleImage != null)
        {
            Sprite targetSprite = expanded ? promptExpandedSprite : promptCollapsedSprite;
            if (targetSprite != null)
            {
                promptToggleImage.sprite = targetSprite;
            }
        }

        if (promptToggleText != null)
        {
            promptToggleText.text = expanded ? "V" : ">";
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

    public void RefreshStaticInfo()
    {
        string displayName = currentCharInfo != null ? currentCharInfo.name : "캐릭터 이름";
        string form = !string.IsNullOrEmpty(currentClothesInfo?.charAttr_type) ? currentClothesInfo.charAttr_type : defaultForm;

        SetText(nameText, displayName);
        SetText(sourceText, "출전 : " + defaultSource);
        SetText(formText, "형태 : " + form);

        bool selectable = currentClothesInfo == null || currentClothesInfo.isSelectable;
        SetActive(statusAvailableTag, selectable);
        SetActive(statusDownloadRequiredTag, !selectable);

        RefreshPortrait();
        RefreshFeatureTags(defaultFeatureTags);
    }

    public void RefreshPrompt()
    {
        string charCode = GetPromptCharacterCode();
        string lang = GetSelectedPromptLanguage();
        string prompt = string.Empty;

        if (!string.IsNullOrEmpty(charCode) && ApiGeminiCharacterDataManager.Instance != null)
        {
            prompt = ApiGeminiCharacterDataManager.Instance.GetCharacterPrompt(charCode, lang);
        }

        originalPromptText = prompt;

        if (promptInputField != null)
        {
            promptInputField.SetTextWithoutNotify(prompt);
        }
    }

    public void RefreshStats()
    {
        int conversationCount = GetConversationCount();
        int costumeCount = currentCharInfo != null && currentCharInfo.clothesList != null ? currentCharInfo.clothesList.Count : 0;

        SetText(conversationCountText, "대화횟수 : " + conversationCount);
        SetText(costumeCountText, "복장 수 : " + costumeCount);
        SetAffection(0, defaultAffectionLabel);
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

        for (int i = 0; i < generatedFeatureTags.Count; i++)
        {
            if (generatedFeatureTags[i] != null)
            {
                Destroy(generatedFeatureTags[i]);
            }
        }
        generatedFeatureTags.Clear();

        for (int i = 0; i < featureTagContainer.childCount; i++)
        {
            featureTagContainer.GetChild(i).gameObject.SetActive(false);
        }

        for (int i = 0; i < featureTags.Count; i++)
        {
            Transform existing = i < featureTagContainer.childCount ? featureTagContainer.GetChild(i) : null;
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                TextMeshProUGUI text = existing.GetComponentInChildren<TextMeshProUGUI>(true);
                SetText(text, featureTags[i]);
            }
        }
    }

    private void RegisterEvents()
    {
        if (hideButton != null)
        {
            hideButton.onClick.AddListener(Hide);
        }

        if (promptToggleButton != null)
        {
            promptToggleButton.onClick.AddListener(TogglePromptExpanded);
        }

        if (promptLanguageDropdown != null)
        {
            promptLanguageDropdown.onValueChanged.AddListener(_ => RefreshPrompt());
        }

        if (promptCopyButton != null)
        {
            promptCopyButton.onClick.AddListener(CopyPrompt);
        }

        if (promptResetButton != null)
        {
            promptResetButton.onClick.AddListener(ResetPrompt);
        }
    }

    private void UnregisterEvents()
    {
        if (hideButton != null)
        {
            hideButton.onClick.RemoveListener(Hide);
        }

        if (promptToggleButton != null)
        {
            promptToggleButton.onClick.RemoveListener(TogglePromptExpanded);
        }

        if (promptLanguageDropdown != null)
        {
            promptLanguageDropdown.onValueChanged.RemoveAllListeners();
        }

        if (promptCopyButton != null)
        {
            promptCopyButton.onClick.RemoveListener(CopyPrompt);
        }

        if (promptResetButton != null)
        {
            promptResetButton.onClick.RemoveListener(ResetPrompt);
        }
    }

    private void CopyPrompt()
    {
        if (promptInputField != null)
        {
            GUIUtility.systemCopyBuffer = promptInputField.text;
        }
    }

    private void ResetPrompt()
    {
        if (promptInputField != null)
        {
            promptInputField.SetTextWithoutNotify(originalPromptText);
        }
    }

    private string GetSelectedPromptLanguage()
    {
        if (promptLanguageDropdown == null || promptLanguageDropdown.options == null || promptLanguageDropdown.options.Count == 0)
        {
            return "ko";
        }

        string selected = promptLanguageDropdown.options[promptLanguageDropdown.value].text;
        if (selected == "한국어") return "ko";
        if (selected == "일본어") return "ja";
        if (selected == "영어") return "en";
        return "ko";
    }

    private string GetPromptCharacterCode()
    {
        if (!string.IsNullOrEmpty(currentClothesInfo?.charAttr_charcode))
        {
            return currentClothesInfo.charAttr_charcode;
        }

        if (!string.IsNullOrEmpty(currentClothesInfo?.name))
        {
            return currentClothesInfo.name;
        }

        return currentCharInfo != null ? currentCharInfo.name : string.Empty;
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
        promptArea = promptArea != null ? promptArea : FindRect("PromptArea");
        promptToggleButton = promptToggleButton != null ? promptToggleButton : FindComponent<Button>("PromptToggleButton");
        promptToggleImage = promptToggleImage != null && promptToggleImage.gameObject.name != "PromptToggleButton_Text" ? promptToggleImage : FindComponent<Image>("PromptToggleButton");
        promptToggleText = promptToggleText != null ? promptToggleText : FindComponent<TextMeshProUGUI>("PromptToggleButton_Text");
        promptLanguageDropdown = promptLanguageDropdown != null ? promptLanguageDropdown : FindComponent<TMP_Dropdown>("PromptLanguageDropdown");
        promptCopyButton = promptCopyButton != null ? promptCopyButton : FindComponent<Button>("PromptCopyButton");
        promptResetButton = promptResetButton != null ? promptResetButton : FindComponent<Button>("PromptResetButton");
        promptInputField = promptInputField != null ? promptInputField : FindComponent<TMP_InputField>("PromptInputField");
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
