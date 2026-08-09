using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public sealed class CharacterVoiceSpeechText
{
    public string displayText;
    public string speechText;
    public string speechLanguage;
    public bool translated;
}

public static class CharacterVoiceSpeechTextResolver
{
    private static readonly HashSet<string> SupportedLanguages =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ko",
            "ja",
            "en"
        };

    public static string GetCurrentUiLanguage()
    {
        try
        {
            if (SettingManager.Instance != null &&
                SettingManager.Instance.settings != null)
            {
                return NormalizeLanguage(
                    SettingManager.Instance.settings.ui_language,
                    "ko");
            }
        }
        catch
        {
        }

        return "ko";
    }

    public static string NormalizeLanguage(string value, string fallback = "ko")
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
        if (normalized == "jp")
        {
            normalized = "ja";
        }
        return SupportedLanguages.Contains(normalized)
            ? normalized
            : fallback;
    }

    public static IEnumerator Resolve(
        string baseUrl,
        string displayText,
        string uiLanguage,
        string soundLanguage,
        Action<CharacterVoiceSpeechText> completed)
    {
        string original = displayText ?? string.Empty;
        string normalizedUi = NormalizeLanguage(uiLanguage, "ko");
        string normalizedSound =
            NormalizeLanguage(soundLanguage, normalizedUi);

        CharacterVoiceSpeechText fallback = new CharacterVoiceSpeechText
        {
            displayText = original,
            speechText = original,
            speechLanguage = normalizedUi,
            translated = false
        };

        if (string.IsNullOrWhiteSpace(original) ||
            string.Equals(
                normalizedUi,
                normalizedSound,
                StringComparison.OrdinalIgnoreCase))
        {
            fallback.speechLanguage = normalizedSound;
            completed?.Invoke(fallback);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            completed?.Invoke(fallback);
            yield break;
        }

        Dictionary<string, string> payload = new Dictionary<string, string>
        {
            { "text", original },
            { "source_lang", normalizedUi },
            { "target_lang", normalizedSound }
        };
        string url = baseUrl.TrimEnd('/') + "/translate";
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[CharacterVoice] Translation failed; using UI text for TTS. " +
                    $"code={request.responseCode}, error={request.error}");
                completed?.Invoke(fallback);
                yield break;
            }

            try
            {
                JObject response = JObject.Parse(request.downloadHandler.text);
                string translatedText =
                    response.Value<string>("translated_text");
                if (string.Equals(
                        response.Value<string>("status"),
                        "success",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(translatedText))
                {
                    completed?.Invoke(new CharacterVoiceSpeechText
                    {
                        displayText = original,
                        speechText = translatedText.Trim(),
                        speechLanguage = normalizedSound,
                        translated = true
                    });
                    yield break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[CharacterVoice] Invalid translation response; " +
                    "using UI text for TTS. " + exception.Message);
            }
        }

        completed?.Invoke(fallback);
    }
}
