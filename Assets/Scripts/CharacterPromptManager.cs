using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class CharacterPromptManager : MonoBehaviour
{
    public static CharacterPromptManager Instance { get; private set; }

    public bool IsResetFlagActive { get; private set; }

    private readonly Dictionary<string, string> promptCache = new Dictionary<string, string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async Task<string> FetchPromptAsync(string charName, string lang = "ko", bool isOrigin = false)
    {
        charName = NormalizeCharName(charName);
        lang = NormalizeLanguage(lang);

        string cacheKey = BuildCacheKey(charName, lang, isOrigin);
        if (promptCache.TryGetValue(cacheKey, out string cachedPrompt))
        {
            Debug.Log($"[CharacterPromptManager] Play-session cache hit. charName={charName}, lang={lang}, isOrigin={isOrigin}");
            IsResetFlagActive = isOrigin;
            return cachedPrompt;
        }

        try
        {
            string baseUrl = await ResolveBaseUrlAsync();
            string url = $"{baseUrl}/prompt/char/?char_name={Uri.EscapeDataString(charName)}&lang={Uri.EscapeDataString(lang)}&is_origin={isOrigin.ToString().ToLowerInvariant()}";
            Debug.Log($"[CharacterPromptManager] API GET /prompt/char requested. charName={charName}, lang={lang}, isOrigin={isOrigin}, url={url}");

            string jsonResponse = await SendGetRequestAsync(url);
            Debug.Log($"[CharacterPromptManager] API GET /prompt/char succeeded. charName={charName}, lang={lang}, isOrigin={isOrigin}, bytes={(string.IsNullOrEmpty(jsonResponse) ? 0 : Encoding.UTF8.GetByteCount(jsonResponse))}");

            if (!string.IsNullOrEmpty(jsonResponse))
            {
                IsResetFlagActive = isOrigin;
                promptCache[cacheKey] = jsonResponse;
                return jsonResponse;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CharacterPromptManager] API GET /prompt/char failed: {e.Message}. Trying local prompt folders.");
        }

        string fallbackPrompt = LoadFromProjectPromptFolderFallback(charName, lang, isOrigin);
        if (!string.IsNullOrEmpty(fallbackPrompt))
        {
            IsResetFlagActive = isOrigin;
            promptCache[cacheKey] = fallbackPrompt;
            return fallbackPrompt;
        }

        fallbackPrompt = LoadFromStreamingAssetsPromptFallback(charName, lang, isOrigin);
        if (!string.IsNullOrEmpty(fallbackPrompt))
        {
            IsResetFlagActive = isOrigin;
            promptCache[cacheKey] = fallbackPrompt;
            return fallbackPrompt;
        }

        return "프롬프트가 존재하지 않습니다.";
    }

    public async Task<bool> SavePromptAsync(string charName, string lang, string promptData)
    {
        charName = NormalizeCharName(charName);
        lang = NormalizeLanguage(lang);

        try
        {
            if (IsResetFlagActive)
            {
                bool deleted = await DeletePromptAsync(charName, lang);
                IsResetFlagActive = false;

                if (deleted)
                {
                    CachePrompt(charName, lang, promptData, isOrigin: false);
                    CachePrompt(charName, lang, promptData, isOrigin: true);
                    return true;
                }
            }

            if (!TryParseAndValidatePromptData(promptData, out JObject parsedData))
            {
                return false;
            }

            string baseUrl = await ResolveBaseUrlAsync();
            string url = $"{baseUrl}/prompt/char/";
            Debug.Log($"[CharacterPromptManager] API POST /prompt/char requested. charName={charName}, lang={lang}, url={url}");

            JObject body = new JObject
            {
                { "char_name", charName },
                { "lang", lang },
                { "data", parsedData }
            };

            await SendPostRequestAsync(url, body.ToString(Formatting.None));
            Debug.Log($"[CharacterPromptManager] API POST /prompt/char succeeded. charName={charName}, lang={lang}");

            IsResetFlagActive = false;
            CachePrompt(charName, lang, promptData, isOrigin: false);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterPromptManager] Prompt save failed: {e.Message}");
            return false;
        }
    }

    public async Task<bool> DeletePromptAsync(string charName, string lang)
    {
        charName = NormalizeCharName(charName);
        lang = NormalizeLanguage(lang);

        try
        {
            string baseUrl = await ResolveBaseUrlAsync();
            string url = $"{baseUrl}/prompt/char/?char_name={Uri.EscapeDataString(charName)}&lang={Uri.EscapeDataString(lang)}";
            Debug.Log($"[CharacterPromptManager] API DELETE /prompt/char requested. charName={charName}, lang={lang}, url={url}");

            await SendDeleteRequestAsync(url);
            Debug.Log($"[CharacterPromptManager] API DELETE /prompt/char succeeded. charName={charName}, lang={lang}");

            RemoveCachedPrompt(charName, lang);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterPromptManager] Prompt delete failed: {e.Message}");
            return false;
        }
    }

    private async Task<string> ResolveBaseUrlAsync()
    {
        var tcs = new TaskCompletionSource<string>();
        ServerManager.Instance.GetBaseUrl(urlResult => tcs.SetResult(urlResult));
        string baseUrl = await tcs.Task;
        return string.IsNullOrEmpty(baseUrl) ? "http://127.0.0.1:5000" : baseUrl;
    }

    private string LoadFromProjectPromptFolderFallback(string charName, string lang, bool isOrigin)
    {
        string projectRoot = GetProjectRootPath();
        if (string.IsNullOrEmpty(projectRoot))
        {
            return string.Empty;
        }

        return LoadFromPromptRoot(Path.Combine(projectRoot, "prompt"), charName, lang, isOrigin, "project prompt");
    }

    private string LoadFromStreamingAssetsPromptFallback(string charName, string lang, bool isOrigin)
    {
        return LoadFromPromptRoot(Path.Combine(Application.streamingAssetsPath, "prompt"), charName, lang, isOrigin, "StreamingAssets prompt");
    }

    private string LoadFromPromptRoot(string promptRoot, string charName, string lang, bool isOrigin, string label)
    {
        if (string.IsNullOrEmpty(promptRoot) || !Directory.Exists(promptRoot))
        {
            return string.Empty;
        }

        List<string> candidates = BuildPromptFallbackPaths(promptRoot, charName, lang, isOrigin);
        for (int i = 0; i < candidates.Count; i++)
        {
            string filePath = candidates[i];
            if (!File.Exists(filePath))
            {
                continue;
            }

            Debug.Log($"[CharacterPromptManager] Loaded prompt from {label}: {filePath}");
            return File.ReadAllText(filePath);
        }

        return string.Empty;
    }

    private List<string> BuildPromptFallbackPaths(string promptRoot, string charName, string lang, bool isOrigin)
    {
        string originName = NormalizeOriginCharName(charName);
        var paths = new List<string>();

        if (!isOrigin)
        {
            paths.Add(Path.Combine(promptRoot, "custom", lang, $"{originName}_custom.json"));
        }

        paths.Add(Path.Combine(promptRoot, lang, $"{originName}.json"));
        paths.Add(Path.Combine(promptRoot, $"{originName}.json"));

        if (lang != "en")
        {
            if (!isOrigin)
            {
                paths.Add(Path.Combine(promptRoot, "custom", "en", $"{originName}_custom.json"));
            }

            paths.Add(Path.Combine(promptRoot, "en", $"{originName}.json"));
        }

        return paths;
    }

    private string GetProjectRootPath()
    {
        try
        {
#if UNITY_EDITOR
            return Directory.GetParent(Application.dataPath)?.FullName;
#else
            string dataDirectory = Application.dataPath;
            return Directory.Exists(dataDirectory) ? Directory.GetParent(dataDirectory)?.FullName : Application.persistentDataPath;
#endif
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CharacterPromptManager] Failed to resolve project root: {e.Message}");
            return string.Empty;
        }
    }

    private string ConvertMarkdownToJson(string markdownText)
    {
        try
        {
            JObject root = new JObject();
            JObject currentSection = null;

            using (StringReader reader = new StringReader(markdownText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("### "))
                    {
                        string sectionName = line.Substring(4).Trim();
                        currentSection = new JObject();
                        root[sectionName] = currentSection;
                    }
                    else if (line.StartsWith("- ") && currentSection != null)
                    {
                        string content = line.Substring(2).Trim();
                        int colonIndex = content.IndexOf(':');
                        if (colonIndex >= 0)
                        {
                            string key = content.Substring(0, colonIndex).Trim();
                            string value = content.Substring(colonIndex + 1).Trim();
                            currentSection[key] = value;
                        }
                    }
                }
            }

            return root.ToString(Formatting.None);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterPromptManager] Markdown to JSON conversion failed: {e.Message}");
            return "{}";
        }
    }

    private bool TryParseAndValidatePromptData(string promptData, out JObject parsedData)
    {
        parsedData = null;
        if (string.IsNullOrWhiteSpace(promptData)) return false;

        string trimmedData = promptData.Trim();
        if (trimmedData.StartsWith("{") || trimmedData.StartsWith("["))
        {
            try
            {
                parsedData = JObject.Parse(trimmedData);
                return true;
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[CharacterPromptManager] Invalid JSON. Save canceled. Detail: {ex.Message}");
                return false;
            }
        }

        try
        {
            string convertedJson = ConvertMarkdownToJson(promptData);
            parsedData = JObject.Parse(convertedJson);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CharacterPromptManager] Markdown parsing failed. Save canceled. Detail: {ex.Message}");
            return false;
        }
    }

    private string NormalizeLanguage(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return "en";
        if (lang == "jp") return "ja";
        if (lang == "한국어") return "ko";
        if (lang == "영어") return "en";
        if (lang == "일본어") return "ja";
        return lang;
    }

    private string NormalizeCharName(string charName)
    {
        return string.IsNullOrWhiteSpace(charName) ? string.Empty : charName.Trim().ToLowerInvariant();
    }

    private string NormalizeOriginCharName(string charName)
    {
        string normalized = NormalizeCharName(charName);
        return normalized.EndsWith("_custom", StringComparison.Ordinal) ? normalized.Substring(0, normalized.Length - "_custom".Length) : normalized;
    }

    private string BuildCacheKey(string charName, string lang, bool isOrigin)
    {
        return $"{NormalizeOriginCharName(charName)}|{NormalizeLanguage(lang)}|{isOrigin}";
    }

    private void CachePrompt(string charName, string lang, string promptData, bool isOrigin)
    {
        if (!string.IsNullOrEmpty(promptData))
        {
            promptCache[BuildCacheKey(charName, lang, isOrigin)] = promptData;
        }
    }

    private void RemoveCachedPrompt(string charName, string lang)
    {
        promptCache.Remove(BuildCacheKey(charName, lang, false));
        promptCache.Remove(BuildCacheKey(charName, lang, true));
    }

    private async Task<string> SendGetRequestAsync(string url)
    {
        System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
        request.Method = "GET";

        using (System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)await request.GetResponseAsync())
        using (Stream stream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream))
        {
            return await reader.ReadToEndAsync();
        }
    }

    private async Task SendPostRequestAsync(string url, string jsonBody)
    {
        System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
        request.Method = "POST";
        request.ContentType = "application/json";

        byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
        using (Stream dataStream = await request.GetRequestStreamAsync())
        {
            await dataStream.WriteAsync(byteArray, 0, byteArray.Length);
        }

        using ((System.Net.HttpWebResponse)await request.GetResponseAsync())
        {
        }
    }

    private async Task SendDeleteRequestAsync(string url)
    {
        System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
        request.Method = "DELETE";

        using ((System.Net.HttpWebResponse)await request.GetResponseAsync())
        {
        }
    }
}
