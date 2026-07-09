using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// JukeboxDownloaderView의 서버 연동 담당 (SkillCatalogClient와 동일 패턴).
///  - 검색   : GET  /youtube/search   -> view.SetResults(...)
///  - 다운로드 : POST /youtube/download -> job_id -> GET /youtube/progress/<id> 폴링
/// 서버 주소는 ServerManager.GetBaseUrl(콜백)로 얻는다.
/// </summary>
[RequireComponent(typeof(JukeboxDownloaderView))]
public class JukeboxDownloaderClient : MonoBehaviour
{
    [SerializeField] private JukeboxDownloaderView view;
    [Tooltip("진행률 폴링 간격(초)")]
    [SerializeField] private float pollInterval = 0.4f;

    private void Awake()
    {
        if (view == null)
        {
            view = GetComponent<JukeboxDownloaderView>();
        }
    }

    private void OnEnable()
    {
        if (view == null)
        {
            return;
        }
        view.SearchRequested += OnSearchRequested;
        view.DownloadRequested += OnDownloadRequested;
    }

    private void OnDisable()
    {
        if (view == null)
        {
            return;
        }
        view.SearchRequested -= OnSearchRequested;
        view.DownloadRequested -= OnDownloadRequested;
    }

    // ── 검색 ──────────────────────────────────────────────────────────────────
    private void OnSearchRequested(JukeboxDownloaderView.SearchParams p)
    {
        ServerManager sm = GetServerManager();
        if (sm == null)
        {
            Debug.LogWarning("[JukeboxDownloaderClient] ServerManager 없음");
            view.ClearResults();
            return;
        }
        sm.GetBaseUrl(baseUrl =>
        {
            if (!this.isActiveAndEnabled) return;
            if (string.IsNullOrEmpty(baseUrl))
            {
                view.ClearResults();
                return;
            }
            StartCoroutine(SearchCoroutine(baseUrl, p));
        });
    }

    private IEnumerator SearchCoroutine(string baseUrl, JukeboxDownloaderView.SearchParams p)
    {
        StringBuilder url = new StringBuilder(baseUrl.TrimEnd('/'));
        url.Append("/youtube/search?q=").Append(UnityWebRequest.EscapeURL(p.query));
        url.Append("&limit=").Append(Mathf.Clamp(p.limit, 1, 30));
        if (!string.IsNullOrEmpty(p.sort) && p.sort != "relevance") url.Append("&sort=").Append(p.sort);
        if (!string.IsNullOrEmpty(p.period)) url.Append("&period=").Append(p.period);
        if (!string.IsNullOrEmpty(p.duration)) url.Append("&duration=").Append(p.duration);

        Debug.Log($"[JukeboxDownloaderClient] GET {url}");
        using (UnityWebRequest req = UnityWebRequest.Get(url.ToString()))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JukeboxDownloaderClient] /youtube/search 실패({req.responseCode}): {req.error}");
                view.ClearResults();
                yield break;
            }
            view.SetResults(ParseResults(req.downloadHandler.text));
        }
    }

    private static List<JukeboxDownloaderView.Track> ParseResults(string json)
    {
        List<JukeboxDownloaderView.Track> list = new List<JukeboxDownloaderView.Track>();
        if (string.IsNullOrEmpty(json))
        {
            return list;
        }

        JArray arr;
        try
        {
            JToken root = JToken.Parse(json);
            arr = (root as JObject)?["results"] as JArray ?? root as JArray;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JukeboxDownloaderClient] 검색 결과 파싱 실패: {e.Message}");
            return list;
        }
        if (arr == null)
        {
            return list;
        }

        foreach (JToken t in arr)
        {
            string id = (string)t["video_id"];
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }
            list.Add(new JukeboxDownloaderView.Track
            {
                videoId = id,
                title = (string)t["title"] ?? string.Empty,
                url = (string)t["url"] ?? ("https://www.youtube.com/watch?v=" + id),
                channel = (string)t["channel"] ?? string.Empty,
                durationStr = (string)t["duration_str"] ?? string.Empty,
                viewsStr = (string)t["views_str"] ?? string.Empty,
                thumbnailHq = (string)t["thumbnail_hq"] ?? string.Empty,
                thumbnail = (string)t["thumbnail"] ?? string.Empty,
            });
        }
        return list;
    }

    // ── 다운로드 + 진행률 폴링 ─────────────────────────────────────────────────
    private void OnDownloadRequested(JukeboxDownloaderView.Track track, Action<string> setStatus)
    {
        ServerManager sm = GetServerManager();
        if (sm == null || track == null)
        {
            setStatus?.Invoke("서버없음");
            return;
        }
        sm.GetBaseUrl(baseUrl =>
        {
            if (!this.isActiveAndEnabled) return;
            if (string.IsNullOrEmpty(baseUrl))
            {
                setStatus?.Invoke("서버없음");
                return;
            }
            StartCoroutine(DownloadCoroutine(baseUrl, track, setStatus));
        });
    }

    private IEnumerator DownloadCoroutine(string baseUrl, JukeboxDownloaderView.Track track, Action<string> setStatus)
    {
        string root = baseUrl.TrimEnd('/');

        // 1) POST /youtube/download
        JObject body = new JObject { ["url"] = track.url };
        byte[] raw = Encoding.UTF8.GetBytes(body.ToString());
        string jobId = null;
        using (UnityWebRequest req = new UnityWebRequest(root + "/youtube/download", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(raw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JukeboxDownloaderClient] POST /youtube/download 실패({req.responseCode}): {req.error}");
                setStatus?.Invoke("실패");
                yield break;
            }
            try
            {
                jobId = (string)JObject.Parse(req.downloadHandler.text)["job_id"];
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JukeboxDownloaderClient] job_id 파싱 실패: {e.Message}");
            }
        }

        if (string.IsNullOrEmpty(jobId))
        {
            setStatus?.Invoke("실패");
            yield break;
        }

        // 2) GET /youtube/progress/<job_id> 폴링
        string progressUrl = root + "/youtube/progress/" + jobId;
        WaitForSeconds wait = new WaitForSeconds(pollInterval);
        while (true)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(progressUrl))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    setStatus?.Invoke("실패");
                    yield break;
                }

                JObject job;
                try
                {
                    job = JObject.Parse(req.downloadHandler.text);
                }
                catch
                {
                    setStatus?.Invoke("실패");
                    yield break;
                }

                string status = (string)job["status"] ?? string.Empty;
                switch (status)
                {
                    case "completed":
                        setStatus?.Invoke("완료");
                        yield break;
                    case "error":
                        setStatus?.Invoke("실패");
                        yield break;
                    case "converting":
                        setStatus?.Invoke("변환…");
                        break;
                    case "downloading":
                        float percent = (float?)job["percent"] ?? 0f;
                        setStatus?.Invoke($"{percent:0}%");
                        break;
                    default:
                        setStatus?.Invoke("대기…");
                        break;
                }
            }
            yield return wait;
        }
    }

    private static ServerManager GetServerManager()
    {
        return ServerManager.Instance != null ? ServerManager.Instance : UnityEngine.Object.FindObjectOfType<ServerManager>();
    }
}
