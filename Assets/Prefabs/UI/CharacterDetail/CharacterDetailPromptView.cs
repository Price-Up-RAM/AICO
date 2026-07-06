using System;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDetailPromptView : MonoBehaviour
{
    private const float CollapsedPromptHeight = 50f;
    private const float ExpandedPromptHeight = 172f;
    private const float PromptInputWidth = 570f;
    private const float PromptInputHeight = 110f;
    private const float PromptInputX = 14f;
    private const float PromptInputY = -48f;
    private const float PromptTextPaddingLeft = 12f;
    private const float PromptTextPaddingRight = 32f;
    private const float PromptTextPaddingVertical = 8f;
    private const float PromptScrollbarWidth = 12f;

    private RectTransform promptArea;
    private Button toggleButton;
    private Image toggleImage;
    private TextMeshProUGUI toggleText;
    private TMP_Dropdown languageDropdown;
    private Button copyButton;
    private Button resetButton;
    private Button saveButton;
    private TMP_InputField inputField;
    private Sprite collapsedSprite;
    private Sprite expandedSprite;

    private Action toggleRequested;
    private Action languageChanged;
    private Action resetRequested;
    private Action saveRequested;

    public string Text => inputField != null ? inputField.text : string.Empty;

    public void Configure(
        RectTransform promptArea,
        Button toggleButton,
        Image toggleImage,
        TextMeshProUGUI toggleText,
        TMP_Dropdown languageDropdown,
        Button copyButton,
        Button resetButton,
        Button saveButton,
        TMP_InputField inputField,
        Sprite collapsedSprite,
        Sprite expandedSprite)
    {
        this.promptArea = promptArea;
        this.toggleButton = toggleButton;
        this.toggleImage = toggleImage;
        this.toggleText = toggleText;
        this.languageDropdown = languageDropdown;
        this.copyButton = copyButton;
        this.resetButton = resetButton;
        this.saveButton = saveButton;
        this.inputField = inputField;
        this.collapsedSprite = collapsedSprite;
        this.expandedSprite = expandedSprite;

        NormalizeInputFieldLayout();
    }

    public void BindEvents(
        Action onToggleRequested,
        Action onLanguageChanged,
        Action onResetRequested,
        Action onSaveRequested)
    {
        UnbindEvents();

        toggleRequested = onToggleRequested;
        languageChanged = onLanguageChanged;
        resetRequested = onResetRequested;
        saveRequested = onSaveRequested;

        if (toggleButton != null) toggleButton.onClick.AddListener(HandleToggleClicked);
        if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(HandleLanguageChanged);
        if (copyButton != null) copyButton.onClick.AddListener(CopyToClipboard);
        if (resetButton != null) resetButton.onClick.AddListener(HandleResetClicked);
        if (saveButton != null) saveButton.onClick.AddListener(HandleSaveClicked);
    }

    public void UnbindEvents()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(HandleToggleClicked);
        if (languageDropdown != null) languageDropdown.onValueChanged.RemoveListener(HandleLanguageChanged);
        if (copyButton != null) copyButton.onClick.RemoveListener(CopyToClipboard);
        if (resetButton != null) resetButton.onClick.RemoveListener(HandleResetClicked);
        if (saveButton != null) saveButton.onClick.RemoveListener(HandleSaveClicked);

        toggleRequested = null;
        languageChanged = null;
        resetRequested = null;
        saveRequested = null;
    }

    public void SetExpanded(bool expanded)
    {
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

        if (inputField != null)
        {
            inputField.gameObject.SetActive(expanded);
            NormalizeInputFieldLayout();
            if (expanded)
            {
                StartCoroutine(ResetScrollToTopNextFrame());
            }
        }

        if (toggleImage != null)
        {
            Sprite targetSprite = expanded ? expandedSprite : collapsedSprite;
            if (targetSprite != null)
            {
                toggleImage.sprite = targetSprite;
            }
        }

        if (toggleText != null)
        {
            toggleText.text = expanded ? "V" : ">";
        }
    }

    public void SetTextWithoutNotify(string prompt, bool resetScroll = true)
    {
        if (inputField == null)
        {
            return;
        }

        inputField.SetTextWithoutNotify(FormatPromptForDisplay(prompt));

        if (resetScroll)
        {
            ResetScrollToTop();
        }
    }

    public string GetSelectedLanguage()
    {
        if (languageDropdown == null || languageDropdown.options == null || languageDropdown.options.Count == 0)
        {
            return "ko";
        }

        string selected = languageDropdown.options[languageDropdown.value].text;
        if (selected == "한국어") return "ko";
        if (selected == "일본어") return "ja";
        if (selected == "영어") return "en";
        return "ko";
    }

    private void NormalizeInputFieldLayout()
    {
        if (inputField == null) return;

        RectTransform inputRect = inputField.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 1f);
        inputRect.anchorMax = new Vector2(0f, 1f);
        inputRect.pivot = new Vector2(0f, 1f);
        inputRect.anchoredPosition = new Vector2(PromptInputX, PromptInputY);
        inputRect.sizeDelta = new Vector2(PromptInputWidth, PromptInputHeight);

        RectTransform viewport = EnsureTextViewport(inputRect);
        inputField.textViewport = viewport;

        if (inputField.textComponent != null)
        {
            StretchTextRect(inputField.textComponent.rectTransform);
            inputField.textComponent.maskable = true;
            AddScrollHandler(inputField.textComponent.gameObject);
        }

        if (inputField.placeholder != null)
        {
            StretchTextRect(inputField.placeholder.rectTransform);
            MaskableGraphic graphic = inputField.placeholder.GetComponent<MaskableGraphic>();
            if (graphic != null) graphic.maskable = true;
            AddScrollHandler(inputField.placeholder.gameObject);
        }

        Scrollbar scrollbar = inputField.verticalScrollbar;
        if (scrollbar == null)
        {
            Transform scrollbarTransform = inputField.transform.Find("Vertical Scrollbar");
            scrollbar = scrollbarTransform != null ? scrollbarTransform.GetComponent<Scrollbar>() : null;
            inputField.verticalScrollbar = scrollbar;
        }

        if (scrollbar != null)
        {
            RectTransform scrollbarRect = scrollbar.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.anchoredPosition = new Vector2(-6f, 0f);
            scrollbarRect.sizeDelta = new Vector2(PromptScrollbarWidth, -10f);
            scrollbar.direction = Scrollbar.Direction.TopToBottom;
            AddScrollHandler(scrollbar.gameObject);
        }

        inputField.scrollSensitivity = 16f;
        AddScrollHandler(viewport.gameObject);
    }

    private RectTransform EnsureTextViewport(RectTransform inputRect)
    {
        Transform textArea = inputField.transform.Find("Text Area");
        if (textArea == null)
        {
            GameObject textAreaObject = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textAreaObject.layer = inputField.gameObject.layer;
            textAreaObject.transform.SetParent(inputField.transform, false);
            textArea = textAreaObject.transform;
        }

        RectTransform viewport = textArea.GetComponent<RectTransform>();
        viewport.SetSiblingIndex(0);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0f, 1f);
        viewport.offsetMin = new Vector2(PromptTextPaddingLeft, PromptTextPaddingVertical);
        viewport.offsetMax = new Vector2(-PromptTextPaddingRight, -PromptTextPaddingVertical);

        if (textArea.GetComponent<RectMask2D>() == null)
        {
            textArea.gameObject.AddComponent<RectMask2D>();
        }

        if (inputField.textComponent != null && inputField.textComponent.transform.parent != textArea)
        {
            inputField.textComponent.transform.SetParent(textArea, false);
        }

        if (inputField.placeholder != null && inputField.placeholder.transform.parent != textArea)
        {
            inputField.placeholder.transform.SetParent(textArea, false);
        }

        return viewport != null ? viewport : inputRect;
    }

    private static void StretchTextRect(RectTransform textRect)
    {
        if (textRect == null) return;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void AddScrollHandler(GameObject target)
    {
        if (target == null) return;

        CharacterDetailPromptScroll handler = target.GetComponent<CharacterDetailPromptScroll>();
        if (handler == null)
        {
            handler = target.AddComponent<CharacterDetailPromptScroll>();
        }

        handler.SetInputField(inputField);
    }

    private void ResetScrollToTop()
    {
        if (inputField == null) return;

        inputField.DeactivateInputField();
        inputField.ForceLabelUpdate();

        if (inputField.verticalScrollbar != null)
        {
            inputField.verticalScrollbar.value = 0f;
        }

        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator ResetScrollToTopNextFrame()
    {
        yield return null;
        ResetScrollToTop();
    }

    private string FormatPromptForDisplay(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return prompt;
        }

        string trimmed = prompt.Trim();
        if (!LooksLikeJson(trimmed))
        {
            return prompt;
        }

        try
        {
            JToken token = JToken.Parse(trimmed);
            if (token.Type == JTokenType.String)
            {
                string nestedJson = token.Value<string>();
                if (!string.IsNullOrWhiteSpace(nestedJson) && LooksLikeJson(nestedJson.Trim()))
                {
                    token = JToken.Parse(nestedJson);
                }
            }

            return token.ToString(Formatting.Indented);
        }
        catch (JsonException)
        {
            return prompt;
        }
    }

    private static bool LooksLikeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        char first = value[0];
        char last = value[value.Length - 1];
        return (first == '{' && last == '}') || (first == '[' && last == ']');
    }

    private void CopyToClipboard()
    {
        if (inputField != null)
        {
            GUIUtility.systemCopyBuffer = inputField.text;
        }
    }

    private void HandleToggleClicked()
    {
        toggleRequested?.Invoke();
    }

    private void HandleLanguageChanged(int _)
    {
        languageChanged?.Invoke();
    }

    private void HandleResetClicked()
    {
        resetRequested?.Invoke();
    }

    private void HandleSaveClicked()
    {
        saveRequested?.Invoke();
    }
}
