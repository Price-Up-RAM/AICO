using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// JukeboxDownloaderView의 서버 연동 담당 (SkillCatalogClient와 동일 패턴).
///  - 검색   : GET  /youtube/search   -> view.SetResults(...)
///  - 다운로드 : POST /youtube/download -> job_id -> GET /youtube/progress/<id> 폴링
///  - 수집   : 완료 시 GET /youtube/file/<id> 로 mp3를 받아
///             persistentDataPath/Jukebox/download 에 저장 (주크박스 download 카테고리로 노출)
/// 서버 주소는 ServerManager.GetBaseUrl(콜백)로 얻는다.
/// </summary>
[RequireComponent(typeof(JukeboxDownloaderView))]
public class JukeboxDownloaderClient : MonoBehaviour
{
    [SerializeField] private JukeboxDownloaderView view;
    [Tooltip("진행률 폴링 간격(초)")]
    [SerializeField] private float pollInterval = 0.4f;

    // 진행 중인 다운로드의 상태 콜백. 창이 SetActive(false)로 닫히면 코루틴이 죽어
    // 버튼이 진행률 라벨인 채 영구 비활성으로 남으므로, OnDisable에서 "실패"로 되돌린다.
    private readonly List<Action<string>> activeStatusCallbacks = new List<Action<string>>();

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
        // 비활성화로 코루틴이 중단된 다운로드 행을 재시도 가능한 상태로 되돌린다.
        foreach (Action<string> cb in activeStatusCallbacks)
        {
            cb?.Invoke("실패");
        }
        activeStatusCallbacks.Clear();

        if (view == null)
        {
            return;
        }
        view.SearchRequested -= OnSearchRequested;
        view.DownloadRequested -= OnDownloadRequested;
    }

    // 종료 상태("완료"/"실패"/"서버없음")를 표시하며 OnDisable 복구 목록에서 제거한다.
    private void SetTerminal(Action<string> setStatus, string label)
    {
        activeStatusCallbacks.Remove(setStatus);
        setStatus?.Invoke(label);
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
        activeStatusCallbacks.Add(setStatus);
        sm.GetBaseUrl(baseUrl =>
        {
            if (!this.isActiveAndEnabled) return; // OnDisable가 이미 "실패" 처리함
            if (string.IsNullOrEmpty(baseUrl))
            {
                SetTerminal(setStatus, "서버없음");
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
                SetTerminal(setStatus, "실패");
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
            SetTerminal(setStatus, "실패");
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
                    SetTerminal(setStatus, "실패");
                    yield break;
                }

                JObject job;
                try
                {
                    job = JObject.Parse(req.downloadHandler.text);
                }
                catch
                {
                    SetTerminal(setStatus, "실패");
                    yield break;
                }

                string status = (string)job["status"] ?? string.Empty;
                switch (status)
                {
                    case "completed":
                        setStatus?.Invoke("저장…");
                        yield return SaveToDownloadFolder(root, jobId, track, setStatus);
                        yield break;
                    case "error":
                        SetTerminal(setStatus, "실패");
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

    // ── 완료 파일 수집: 서버 → persistentDataPath/Jukebox/download ─────────────
    private IEnumerator SaveToDownloadFolder(string root, string jobId, JukeboxDownloaderView.Track track, Action<string> setStatus)
    {
        string dir = JukeboxCatalog.DownloadDir;
        string finalPath = Path.Combine(dir, BuildFileName(track));
        // 임시 파일은 job별로 이름을 달리해, 같은 곡을 동시에 받아도 서로 충돌하지 않는다.
        // 성공 시에만 최종 이름으로 옮기므로 잘린 mp3가 목록에 노출되지 않는다.
        string partPath = finalPath + "." + jobId + ".part";

        UnityWebRequest req = UnityWebRequest.Get(root + "/youtube/file/" + jobId);
        try
        {
            Directory.CreateDirectory(dir);
            // DownloadHandlerFile은 생성자에서 파일을 연다 — 실패(잠김 등) 시 여기서 던진다.
            req.downloadHandler = new DownloadHandlerFile(partPath) { removeFileOnAbort = true };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JukeboxDownloaderClient] 저장 준비 실패: {e.Message}");
            req.Dispose();
            TryDelete(partPath);
            SetTerminal(setStatus, "실패");
            yield break;
        }

        using (req)
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[JukeboxDownloaderClient] GET /youtube/file 실패({req.responseCode}): {req.error}");
                TryDelete(partPath);
                SetTerminal(setStatus, "실패");
                yield break;
            }
        }

        try
        {
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            File.Move(partPath, finalPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JukeboxDownloaderClient] 파일 저장 실패: {e.Message}");
            TryDelete(partPath);
            SetTerminal(setStatus, "실패");
            yield break;
        }

        Debug.Log($"[JukeboxDownloaderClient] 저장 완료: {finalPath}");

        // 주크박스 패널이 열려 있으면 재시작 없이 download 카테고리에 바로 반영.
        // 닫혀 있으면(FindObjectOfType는 비활성 제외) JukeboxView.OnEnable 재스캔이 처리한다.
        JukeboxView jukebox = UnityEngine.Object.FindObjectOfType<JukeboxView>();
        if (jukebox != null)
        {
            jukebox.AddDownloadedTrack(finalPath);
        }

        SetTerminal(setStatus, "완료");
    }

    // 파일명은 서버 것(restrictfilenames로 한글이 깨짐) 대신 검색 결과의 유니코드 제목으로 짓는다.
    // videoId를 붙여 다른 곡끼리의 충돌을 막고, 같은 곡 재다운로드는 같은 이름을 덮어쓴다.
    private static string BuildFileName(JukeboxDownloaderView.Track track)
    {
        string title = SanitizeFileName(track != null ? track.title : null);
        string id = SanitizeFileName(track != null ? track.videoId : null);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 8);
        }
        return string.IsNullOrEmpty(title) ? id + ".mp3" : $"{title} [{id}].mp3";
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? ' ' : c);
        }
        string cleaned = sb.ToString().Trim().TrimEnd('.');
        if (cleaned.Length > 60)
        {
            cleaned = cleaned.Substring(0, 60).Trim().TrimEnd('.');
        }
        return cleaned;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // 임시 파일 정리 실패는 치명적이지 않다 (.part는 로드 대상에서 제외됨)
        }
    }

    private static ServerManager GetServerManager()
    {
        return ServerManager.Instance != null ? ServerManager.Instance : UnityEngine.Object.FindObjectOfType<ServerManager>();
    }
}
