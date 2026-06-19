using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// SkillView의 데이터 공급/저장 담당. 서버 통합 카탈로그(/skills/list)를 읽어와
/// SkillView에 주입하고, custom 스킬의 생성/수정(POST)·삭제(DELETE)를 서버에 반영한다.
///
/// 역할 분담
///  - 카탈로그(이름/source/category/description/parameters) : 서버 GET /skills/list
///  - custom 본문(body)                                     : 로컬 ApiAgentFunctionSkillManager
///    (서버 list는 body를 주지 않으므로 본문은 로컬 .md 파일이 진실의 원천)
///  - custom 생성/수정 : 로컬 SaveSkill + 서버 POST /skills/custom (동기화)
///  - custom 삭제      : 로컬 DeleteSkill + 서버 DELETE /skills/custom/&lt;key&gt;
///
/// 서버 미연결 시 로컬 custom만으로 동작한다.
/// </summary>
[RequireComponent(typeof(SkillView))]
public class SkillCatalogClient : MonoBehaviour
{
    [SerializeField] private SkillView view;
    [SerializeField] private string lang = "ko";

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<SkillView>();
        }
    }

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
        ReloadCatalog();
    }

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
    }

    private void OnLanguageChanged(string code)
    {
        lang = string.IsNullOrEmpty(code) ? "ko" : code;
        ReloadCatalog();
    }

    // ── 카탈로그 로드 ─────────────────────────────────────────────────────────
    public void ReloadCatalog()
    {
        ServerManager sm = FindObjectOfType<ServerManager>();
        if (sm == null)
        {
            view.SetSkills(BuildLocalOnly());
            return;
        }

        sm.GetBaseUrl(baseUrl =>
        {
            if (string.IsNullOrEmpty(baseUrl))
            {
                view.SetSkills(BuildLocalOnly());
                return;
            }
            StartCoroutine(GetListCoroutine(baseUrl));
        });
    }

    private IEnumerator GetListCoroutine(string baseUrl)
    {
        string url = baseUrl.TrimEnd('/') + "/skills/list?lang=" + UnityWebRequest.EscapeURL(lang);
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SkillCatalogClient] /skills/list 실패: {req.error} → 로컬 custom만 사용");
                view.SetSkills(BuildLocalOnly());
                yield break;
            }

            List<SkillView.SkillEntry> entries = ParseList(req.downloadHandler.text);
            MergeLocalCustom(entries);
            view.SetSkills(entries);
        }
    }

    private static List<SkillView.SkillEntry> ParseList(string json)
    {
        List<SkillView.SkillEntry> list = new List<SkillView.SkillEntry>();
        if (string.IsNullOrEmpty(json))
        {
            return list;
        }

        JArray arr;
        try
        {
            arr = JObject.Parse(json)["skills"] as JArray;
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
            SkillView.SkillEntry e = new SkillView.SkillEntry
            {
                id = (string)t["name"],
                displayName = (string)t["name"],
                source = (string)t["source"] ?? "custom",
                category = (string)t["category"] ?? string.Empty,
                description = (string)t["description"] ?? string.Empty,
                requireImage = (bool?)t["require_image"] ?? false,
            };

            JArray pj = t["parameters"] as JArray;
            if (pj != null)
            {
                foreach (JToken p in pj)
                {
                    e.parameters.Add(new SkillView.SkillParam
                    {
                        name = (string)p["name"],
                        type = (string)p["type"],
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
                e.content = ExtractBody(mgr.ReadSkillBody(e.id));
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
                content = ExtractBody(mgr.ReadSkillBody(meta.key)),
            });
        }
    }

    private List<SkillView.SkillEntry> BuildLocalOnly()
    {
        List<SkillView.SkillEntry> list = new List<SkillView.SkillEntry>();
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
                content = ExtractBody(mgr.ReadSkillBody(meta.key)),
            });
        }
        return list;
    }

    // ── 저장 / 삭제 ───────────────────────────────────────────────────────────
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
        ServerManager sm = FindObjectOfType<ServerManager>();
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

        ServerManager sm = FindObjectOfType<ServerManager>();
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
    }

    // ── 마크다운 frontmatter/body 분리 ─────────────────────────────────────────
    // 파일 형식: "---\n{frontmatter}\n---\n\n{body}"
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

    private static string ExtractBody(string raw)
    {
        Split(raw, out _, out string body);
        return body;
    }

    private static string ExtractFrontmatter(string raw)
    {
        Split(raw, out string fm, out _);
        return fm;
    }
}
