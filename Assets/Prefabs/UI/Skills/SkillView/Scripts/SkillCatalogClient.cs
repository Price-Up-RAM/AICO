using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

// SkillView의 데이터 공급 및 서버 연동 담당
[RequireComponent(typeof(SkillView))]
public class SkillCatalogClient : MonoBehaviour
{
    [SerializeField] private SkillView view;
    [SerializeField] private string lang = "";

    // 초기화 시 SkillView 컴포넌트 가져오기
    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<SkillView>();
        }
    }

    // 활성화 시 이벤트 리스너 등록 및 카탈로그 갱신
    private void OnEnable()
    {
        if (view == null)
        {
            return;
        }
        view.RefreshRequested += ReloadCatalog;
        view.LanguageChanged += OnLanguageChanged;
        view.SaveRequested += OnSaveRequested;
        view.DeleteRequested += OnDeleteRequested;
        view.RecommendRequested += OnRecommendRequested;
        view.ToggleEnabledRequested += OnToggleEnabledRequested;
        ReloadCatalog();
    }

    // 비활성화 시 이벤트 리스너 해제
    private void OnDisable()
    {
        if (view == null)
        {
            return;
        }
        view.RefreshRequested -= ReloadCatalog;
        view.LanguageChanged -= OnLanguageChanged;
        view.SaveRequested -= OnSaveRequested;
        view.DeleteRequested -= OnDeleteRequested;
        view.RecommendRequested -= OnRecommendRequested;
        view.ToggleEnabledRequested -= OnToggleEnabledRequested;
    }

    // 언어 변경 시 카탈로그 재로딩
    private void OnLanguageChanged(string code)
    {
        lang = code;
        ReloadCatalog();
    }

    // ── 카탈로그 로드 ─────────────────────────────────────────────────────────
    // 서버에서 카탈로그 목록 갱신 시작
    public void ReloadCatalog()
    {
        ServerManager sm = GetServerManager();
        if (sm == null)
        {
            view.SetSkills(BuildLocalOnly(true));
            return;
        }

        sm.GetBaseUrl(baseUrl =>
        {
            if (!this.isActiveAndEnabled) return;

            if (string.IsNullOrEmpty(baseUrl))
            {
                view.SetSkills(BuildLocalOnly(true));
                return;
            }
            StartCoroutine(GetListCoroutine(baseUrl));
        });
    }

    // 비동기로 서버에서 스킬 목록 가져오기
    private IEnumerator GetListCoroutine(string baseUrl)
    {
        string url = baseUrl.TrimEnd('/') + "/skills/list";
        if (!string.IsNullOrEmpty(lang))
        {
            url += "?lang=" + UnityWebRequest.EscapeURL(lang);
        }
        Debug.Log($"[SkillCatalogClient] GET {url}");
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SkillCatalogClient] /skills/list 실패({req.responseCode}): {req.error} -> 로컬 custom만 사용");
                view.SetSkills(BuildLocalOnly(true));
                yield break;
            }

            string jsonText = req.downloadHandler.text;
            SaveToolLog(jsonText);
            List<SkillView.SkillEntry> entries = ParseList(jsonText);
            MergeLocalCustom(entries);
            Debug.Log($"[SkillCatalogClient] /skills/list loaded: {entries.Count}");
            view.SetSkills(entries);
        }
    }

    // 서버에서 받은 json을 예쁘게 포맷팅하여 파일로 저장
    private void SaveToolLog(string json)
    {
        try
        {
            string formattedJson = json;
            if (!string.IsNullOrEmpty(json))
            {
                // JToken으로 파싱 후 들여쓰기가 적용된 문자열로 변환 (유니코드도 읽기 편하게 변환됨)
                JToken parsedJson = JToken.Parse(json);
                formattedJson = parsedJson.ToString(Newtonsoft.Json.Formatting.Indented);
            }

            string path = System.IO.Path.Combine(Application.persistentDataPath, "skills.json");
            System.IO.File.WriteAllText(path, formattedJson, System.Text.Encoding.UTF8);
            Debug.Log($"[SkillCatalogClient] Saved formatted skills to {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SkillCatalogClient] Failed to save skills.json: {e.Message}");
        }
    }

    // 서버 응답 JSON을 SkillEntry 리스트로 파싱
    private static List<SkillView.SkillEntry> ParseList(string json)
    {
        List<SkillView.SkillEntry> list = new List<SkillView.SkillEntry>();
        if (string.IsNullOrEmpty(json))
        {
            return list;
        }

        JArray arr = null;
        try
        {
            JToken root = JToken.Parse(json);
            arr = root as JArray;
            if (arr == null)
            {
                JObject obj = root as JObject;
                arr = obj != null ? obj["skills"] as JArray : null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SkillCatalogClient] 카탈로그 파싱 실패: {e.Message}");
            return list;
        }
        if (arr == null)
        {
            return list;
        }

        foreach (JToken t in arr)
        {
            string name = (string)t["name"];
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            // 서버가 모든 커스텀 JSON 데이터를 description에 담아서 줌
            // 에디터 화면용(lang 없음)으로 불렀으므로 다국어 딕셔너리 원본 JSON 문자열이 들어있음
            string rawDesc = (string)t["description"] ?? string.Empty;
            string parsedContent = FormatSkillDescription(rawDesc);

            // description이 JSON 문자열 형태라면, 전체 JSON을 보기 좋게 포맷팅해서 본문으로 사용
            string source = (string)t["source"] ?? "custom";
            bool isRegistry = string.Equals(source, "server", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(source, "unity", System.StringComparison.OrdinalIgnoreCase);
            SkillView.SkillEntry e = new SkillView.SkillEntry
            {
                id = name,
                displayName = name,
                source = source,
                category = (string)t["category"] ?? string.Empty,
                description = rawDesc,
                requireImage = (bool?)t["require_image"] ?? false,
                content = parsedContent,
                // custom/official 공존 플래그 (구 서버 호환: 없으면 source로 추론)
                isCustom = (bool?)t["is_custom"] ?? string.Equals(source, "custom", System.StringComparison.OrdinalIgnoreCase),
                isOfficial = (bool?)t["is_official"] ?? isRegistry,
                isEnabled = (bool?)t["is_enabled"] ?? true,
            };

            JArray pj = t["parameters"] as JArray;
            if (pj != null)
            {
                foreach (JToken p in pj)
                {
                    e.parameters.Add(new SkillView.SkillParam
                    {
                        name = (string)p["name"] ?? string.Empty,
                        type = (string)p["type"] ?? string.Empty,
                        required = (bool?)p["required"] ?? false,
                        description = (string)p["description"] ?? string.Empty,
                    });
                }
            }

            list.Add(e);
        }
        return list;
    }

    // 서버 custom 엔트리에 로컬 본문을 채우고, 로컬에만 있는 custom은 추가한다.
    private void MergeLocalCustom(List<SkillView.SkillEntry> entries)
    {
        ApiAgentFunctionSkillManager mgr = ApiAgentFunctionSkillManager.Instance;
        if (mgr == null)
        {
            return;
        }

        HashSet<string> known = new HashSet<string>();
        foreach (SkillView.SkillEntry e in entries)
        {
            if (e.IsEditable && !string.IsNullOrEmpty(e.id))
            {
                string localBody = ExtractBody(mgr.ReadSkillBody(e.id));
                if (!string.IsNullOrEmpty(localBody))
                {
                    e.content = localBody;
                }
                known.Add(e.id);
            }
        }

        foreach (SkillMetadata meta in mgr.GetAllSkills())
        {
            if (string.IsNullOrEmpty(meta.key) || known.Contains(meta.key))
            {
                continue;
            }
            entries.Add(new SkillView.SkillEntry
            {
                id = meta.key,
                displayName = meta.key,
                source = "custom",
                category = "Skill",
                isCustom = true,
                content = ExtractBody(mgr.ReadSkillBody(meta.key)),
            });
        }
    }

    // 서버 접속 실패 시 로컬 custom 스킬만으로 목록 구성
    private List<SkillView.SkillEntry> BuildLocalOnly(bool showConnectionError = false)
    {
        List<SkillView.SkillEntry> list = new List<SkillView.SkillEntry>();

        if (showConnectionError)
        {
            list.Add(new SkillView.SkillEntry
            {
                id = "server_error",
                displayName = "Connection Error",
                source = "server",
                category = "Error",
                description = "Check Local Server Status"
            });
        }

        ApiAgentFunctionSkillManager mgr = ApiAgentFunctionSkillManager.Instance;
        if (mgr == null)
        {
            return list;
        }
        foreach (SkillMetadata meta in mgr.GetAllSkills())
        {
            list.Add(new SkillView.SkillEntry
            {
                id = meta.key,
                displayName = meta.key,
                source = "custom",
                category = "Skill",
                isCustom = true,
                content = ExtractBody(mgr.ReadSkillBody(meta.key)),
            });
        }
        return list;
    }

    // ── 저장 / 삭제 ───────────────────────────────────────────────────────────
    // 스킬 저장 요청 처리 (로컬 저장 및 서버 POST)
    private void OnSaveRequested(SkillView.SkillEntry entry)
    {
        if (entry == null || !entry.IsEditable || string.IsNullOrEmpty(entry.id))
        {
            return;
        }

        // 1) 로컬 저장 (기존 frontmatter 보존, 본문만 교체)
        ApiAgentFunctionSkillManager mgr = ApiAgentFunctionSkillManager.Instance;
        if (mgr != null)
        {
            string existing = mgr.ReadSkillBody(entry.id);
            string frontmatter = ExtractFrontmatter(existing);
            if (string.IsNullOrEmpty(frontmatter))
            {
                frontmatter = $"name: {entry.displayName}\nrequire_vl: {entry.requireImage.ToString().ToLowerInvariant()}";
            }
            mgr.SaveSkill(entry.id, frontmatter, entry.content);
        }

        // 2) 서버 동기화 POST
        ServerManager sm = GetServerManager();
        if (sm == null)
        {
            return;
        }
        sm.GetBaseUrl(baseUrl =>
        {
            if (!string.IsNullOrEmpty(baseUrl))
            {
                StartCoroutine(PostCustomCoroutine(baseUrl, entry));
            }
        });
    }

    // 서버에 custom 스킬 생성/수정 요청
    private IEnumerator PostCustomCoroutine(string baseUrl, SkillView.SkillEntry entry)
    {
        JObject body = new JObject
        {
            ["key"] = entry.id,
            ["name"] = string.IsNullOrEmpty(entry.displayName) ? entry.id : entry.displayName,
            ["body"] = entry.content ?? string.Empty,
            ["lang"] = lang,
            ["require_image"] = entry.requireImage,
            ["overwrite"] = true,
        };

        string url = baseUrl.TrimEnd('/') + "/skills/custom";
        byte[] raw = Encoding.UTF8.GetBytes(body.ToString());
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SkillCatalogClient] POST /skills/custom 실패({req.responseCode}): {req.error}");
            }
            else
            {
                Debug.Log($"[SkillCatalogClient] custom 저장 동기화 완료: {entry.id}");
            }
        }
    }

    // 스킬 삭제 요청 처리 (로컬 삭제 및 서버 DELETE)
    private void OnRecommendRequested(string text, int token)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ServerManager sm = GetServerManager();
        if (sm == null)
        {
            return;
        }

        sm.GetBaseUrl(baseUrl =>
        {
            if (!string.IsNullOrEmpty(baseUrl))
            {
                StartCoroutine(PostRecommendCoroutine(baseUrl, text, token));
            }
        });
    }

    private IEnumerator PostRecommendCoroutine(string baseUrl, string text, int token)
    {
        JObject body = new JObject
        {
            ["text"] = text ?? string.Empty,
            ["lang"] = string.IsNullOrEmpty(lang) ? "ko" : lang,
        };

        string url = baseUrl.TrimEnd('/') + "/skills/recommend";
        byte[] raw = Encoding.UTF8.GetBytes(body.ToString());
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SkillCatalogClient] POST /skills/recommend 실패({req.responseCode}): {req.error}");
                yield break;
            }

            SkillView.SkillEntry recommended = ExtractRecommendedSkill(req.downloadHandler.text);
            view.ApplyRecommendedSkill(recommended, token);
        }
    }

    private static SkillView.SkillEntry ExtractRecommendedSkill(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new SkillView.SkillEntry();
        }

        try
        {
            JObject root = JObject.Parse(json);
            JToken skill = root["skill"];
            if (skill != null)
            {
                string name = (string)skill["name"] ?? string.Empty;
                string description = ExtractRecommendedDescription(json);
                string displayName = string.IsNullOrEmpty(name) ? ExtractDisplayNameFromDescription(description) : name;

                SkillView.SkillEntry entry = new SkillView.SkillEntry
                {
                    id = name,
                    displayName = displayName,
                    source = (string)skill["source"] ?? "custom",
                    category = (string)skill["category"] ?? "Skill",
                    description = description,
                    content = description,
                    requireImage = (bool?)skill["require_image"] ?? false,
                };

                JArray pj = skill["parameters"] as JArray;
                if (pj != null)
                {
                    foreach (JToken p in pj)
                    {
                        entry.parameters.Add(new SkillView.SkillParam
                        {
                            name = (string)p["name"] ?? string.Empty,
                            type = (string)p["type"] ?? string.Empty,
                            required = (bool?)p["required"] ?? false,
                            description = (string)p["description"] ?? string.Empty,
                        });
                    }
                }

                return entry;
            }
        }
        catch
        {
            // Fall back to showing whatever the server returned.
        }

        return new SkillView.SkillEntry
        {
            source = "custom",
            category = "Skill",
            description = FormatSkillDescription(json),
            content = FormatSkillDescription(json),
        };
    }

    private static string ExtractRecommendedDescription(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return string.Empty;
        }

        try
        {
            JObject root = JObject.Parse(json);
            JToken description = root["skill"]?["description"];
            if (description != null)
            {
                return description.Type == JTokenType.String
                    ? FormatSkillDescription((string)description)
                    : description.ToString(Newtonsoft.Json.Formatting.Indented);
            }

            JToken skill = root["skill"];
            if (skill != null)
            {
                return skill.ToString(Newtonsoft.Json.Formatting.Indented);
            }
        }
        catch
        {
            // If the server returns raw text, show it in the editor as-is.
        }

        return json;
    }

    private static string ExtractDisplayNameFromDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        try
        {
            JObject obj = JObject.Parse(description);
            JToken name = obj["name"];
            if (name == null)
            {
                return string.Empty;
            }
            if (name.Type == JTokenType.String)
            {
                return (string)name ?? string.Empty;
            }
            if (name.Type == JTokenType.Object)
            {
                return (string)name["ko"]
                    ?? (string)name["en"]
                    ?? (string)name["ja"]
                    ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string FormatSkillDescription(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        string trimmed = text.Trim();
        if ((!trimmed.StartsWith("{") || !trimmed.EndsWith("}")) &&
            (!trimmed.StartsWith("[") || !trimmed.EndsWith("]")))
        {
            return text;
        }

        try
        {
            JToken parsed = JToken.Parse(trimmed);
            return parsed.ToString(Newtonsoft.Json.Formatting.Indented);
        }
        catch
        {
            return text;
        }
    }

    private void OnDeleteRequested(SkillView.SkillEntry entry)
    {
        if (entry == null || !entry.IsEditable || string.IsNullOrEmpty(entry.id))
        {
            return;
        }

        ApiAgentFunctionSkillManager mgr = ApiAgentFunctionSkillManager.Instance;
        if (mgr != null)
        {
            mgr.DeleteSkill(entry.id);
        }

        ServerManager sm = GetServerManager();
        if (sm == null)
        {
            return;
        }
        sm.GetBaseUrl(baseUrl =>
        {
            if (!string.IsNullOrEmpty(baseUrl))
            {
                StartCoroutine(DeleteCustomCoroutine(baseUrl, entry.id));
            }
        });
    }

    // 서버에 custom 스킬 삭제 요청. 성공 시 목록을 재조회해 원본(official)을 다시 노출한다.
    private IEnumerator DeleteCustomCoroutine(string baseUrl, string key)
    {
        string url = baseUrl.TrimEnd('/') + "/skills/custom/" + UnityWebRequest.EscapeURL(key);
        using (UnityWebRequest req = UnityWebRequest.Delete(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success && req.responseCode != 404)
            {
                Debug.LogWarning($"[SkillCatalogClient] DELETE /skills/custom 실패({req.responseCode}): {req.error}");
            }
            else
            {
                Debug.Log($"[SkillCatalogClient] custom 삭제 동기화 완료: {key}");
            }
        }

        // custom 오버레이를 지웠으니 서버에서 다시 받아 원본을 official 태그로 표시한다.
        if (this.isActiveAndEnabled)
        {
            ReloadCatalog();
        }
    }

    // ── on/off 토글 ───────────────────────────────────────────────────────────
    // 스킬 활성/비활성 상태를 서버에 반영 (모든 source 대상)
    private void OnToggleEnabledRequested(SkillView.SkillEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id))
        {
            return;
        }

        ServerManager sm = GetServerManager();
        if (sm == null)
        {
            return;
        }
        sm.GetBaseUrl(baseUrl =>
        {
            if (!string.IsNullOrEmpty(baseUrl))
            {
                StartCoroutine(PutEnabledCoroutine(baseUrl, entry.id, entry.isEnabled));
            }
        });
    }

    private IEnumerator PutEnabledCoroutine(string baseUrl, string key, bool enabled)
    {
        JObject body = new JObject
        {
            ["key"] = key,
            ["enabled"] = enabled,
        };

        string url = baseUrl.TrimEnd('/') + "/skills/enabled";
        byte[] raw = Encoding.UTF8.GetBytes(body.ToString());
        using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
        {
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SkillCatalogClient] PUT /skills/enabled 실패({req.responseCode}): {req.error}");
            }
            else
            {
                Debug.Log($"[SkillCatalogClient] on/off 동기화 완료: {key} enabled={enabled}");
            }
        }
    }

    // ── 마크다운 frontmatter/body 분리 ─────────────────────────────────────────
    // 파일 형식: "---\n{frontmatter}\n---\n\n{body}"
    // 마크다운 파일에서 frontmatter와 본문 분리
    private static void Split(string raw, out string frontmatter, out string body)
    {
        frontmatter = string.Empty;
        body = raw ?? string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        string s = raw.Replace("\r\n", "\n");
        if (!s.StartsWith("---\n"))
        {
            body = raw;
            return;
        }

        int end = s.IndexOf("\n---", 4);
        if (end < 0)
        {
            body = raw;
            return;
        }

        frontmatter = s.Substring(4, end - 4).Trim();
        int bodyStart = end + 4;
        body = bodyStart < s.Length ? s.Substring(bodyStart).TrimStart('\n') : string.Empty;
    }

    // 마크다운 파일에서 본문만 추출
    private static string ExtractBody(string raw)
    {
        Split(raw, out _, out string body);
        return body;
    }

    // 마크다운 파일에서 frontmatter만 추출
    private static string ExtractFrontmatter(string raw)
    {
        Split(raw, out string fm, out _);
        return fm;
    }

    // ServerManager 싱글톤 인스턴스 가져오기
    private static ServerManager GetServerManager()
    {
        return ServerManager.Instance != null ? ServerManager.Instance : UnityEngine.Object.FindObjectOfType<ServerManager>();
    }
}
