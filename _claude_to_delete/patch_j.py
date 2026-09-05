# -*- coding: utf-8 -*-
# J1: MRJukebox가 StreamingAssets/bgm + 다운로드 폴더를 직접 로드
# J2: MRTextInputGuard 키보드 계측
# J3: 라우터 합성 이벤트 ai_info 누락 NRE 노이즈 제거
import sys, os

ROOT = os.path.expanduser("~/mnt/UnityProject--AICO/Assets/Scripts")
ok = True


def eol_of(p):
    b = open(p, "rb").read()
    crlf = b.count(b"\r\n")
    if crlf > (b.count(b"\n") - crlf):
        return "\r\n"
    return "\n"


def patch(path, old, new, label):
    global ok
    p = os.path.join(ROOT, path)
    e = eol_of(p)
    data = open(p, "rb").read()
    o = old.replace("\n", e).encode("utf-8")
    n = new.replace("\n", e).encode("utf-8")
    c = data.count(o)
    if c != 1:
        print("FAIL %s : 앵커 %d회 매치" % (label, c))
        ok = False
        return
    open(p, "wb").write(data.replace(o, n))
    print("OK   %s" % label)


# ===== J1a. MRJukebox using 추가 =====
patch("MR/MRJukebox.cs",
"""using System.Collections.Generic;
using UnityEngine;""",
"""using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;""",
"J1a. MRJukebox using")

# ===== J1b. Start()에서 외부 곡 로딩 시작 =====
patch("MR/MRJukebox.cs",
"""    private void Start()
    {
        if (playlist == null || playlist.Count == 0)
        {
            Debug.LogWarning("[MRJukebox] 재생 목록(playlist)이 비어있습니다.");
            return;
        }""",
"""    private void Start()
    {
        // 외부 곡(StreamingAssets/bgm, 다운로드 폴더)을 여기서 직접 로드한다.
        // 원래는 JukeboxView.LoadCustom()이 유일한 로더였는데, JukeboxView GameObject가
        // 씬에 비활성으로 저장돼 있어 Start()가 돌지 않는다. 그래서 UI를 한 번도 안 열면
        // 음성 명령에서 씬 playlist(4곡)만 보였다 (2026-08-25 실측: 보유=4곡).
        // JukeboxView도 HasTrack()으로 중복을 거르므로 나중에 UI를 열어도 곡이 겹치지 않는다.
        StartCoroutine(LoadExternalTracks());

        if (playlist == null || playlist.Count == 0)
        {
            Debug.LogWarning("[MRJukebox] 재생 목록(playlist)이 비어있습니다.");
            return;
        }""",
"J1b. Start 외부 로딩")

# ===== J1c. 외부 로딩 구현 =====
patch("MR/MRJukebox.cs",
"""    /// 런타임에 트랙 추가(custom: StreamingAssets에서 로드한 클립 등). 추가된 인덱스 반환.""",
"""    // ==========================================
    // 외부 곡 로딩 (Phase 5)
    // ==========================================

    /// StreamingAssets/bgm 과 다운로드 폴더의 음원을 playlist에 추가한다.
    private IEnumerator LoadExternalTracks()
    {
        int before = playlist.Count;

        yield return LoadExternalDir(Path.Combine(Application.streamingAssetsPath, JukeboxCatalog.CustomFolder), JukeboxCatalog.CustomTag);
        yield return LoadExternalDir(JukeboxCatalog.DownloadDir, JukeboxCatalog.DownloadTag);

        Debug.Log($"[MRJukebox] 외부 곡 로딩 완료: {before}곡 → {playlist.Count}곡");
    }

    private IEnumerator LoadExternalDir(string dir, string tag)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            Debug.Log($"[MRJukebox] 외부 폴더 없음 - 건너뜀: {dir}");
            yield break;
        }

        string[] files = Directory.GetFiles(dir);
        for (int i = 0; i < files.Length; i++)
        {
            string full = files[i];
            string trackName = Path.GetFileNameWithoutExtension(full);
            if (HasExternalTrack(trackName, tag))
            {
                continue;
            }
            yield return LoadExternalFile(full, tag);
        }
    }

    // 같은 이름 + 같은 태그면 이미 등록된 것으로 본다 (JukeboxView.HasTrack과 같은 규칙).
    private bool HasExternalTrack(string trackName, string tag)
    {
        for (int i = 0; i < playlist.Count; i++)
        {
            if (playlist[i].trackName == trackName
                && playlist[i].tags != null
                && playlist[i].tags.Contains(tag))
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerator LoadExternalFile(string full, string tag)
    {
        string ext = Path.GetExtension(full).ToLowerInvariant();
        AudioType type;
        if (ext == ".wav")
        {
            type = AudioType.WAV;
        }
        else if (ext == ".mp3")
        {
            type = AudioType.MPEG;
        }
        else if (ext == ".ogg")
        {
            type = AudioType.OGGVORBIS;
        }
        else
        {
            yield break;
        }

        string url = new Uri(full).AbsoluteUri;
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, type))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[MRJukebox] 외부 곡 로드 실패: {full} ({req.error})");
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
            {
                Debug.LogWarning($"[MRJukebox] 외부 곡 클립이 비어 있다: {full}");
                yield break;
            }

            AddTrack(clip, Path.GetFileNameWithoutExtension(full), tag);
        }
    }

    /// 런타임에 트랙 추가(custom: StreamingAssets에서 로드한 클립 등). 추가된 인덱스 반환.""",
"J1c. 외부 로딩 구현")

# ===== J2. 키보드 계측 =====
patch("MR/MRTextInputGuard.cs",
"""    private void Awake()
    {
        if (!blockTextInput) return;""",
"""    // 계측(Phase 5): 키보드 입력이 막히는 지점을 가른다.
    // blockTextInput이 꺼져 있어도 다른 이유로 포커스가 안 잡힐 수 있다 —
    // '차단이 꺼져 있다'와 '입력이 된다'는 다른 사실이다 (Kickoff Guide 4-58).
    private TMP_InputField _diagLastFocused;

    private void Start()
    {
        TMP_InputField[] fields = targetFields;
        if (fields == null || fields.Length == 0)
        {
            fields = GetComponentsInChildren<TMP_InputField>(true);
        }

        Debug.Log($"[MRInput/진단] blockTextInput={blockTextInput} | 대상 InputField {fields.Length}개 | 오브젝트='{gameObject.name}'");

        for (int i = 0; i < fields.Length; i++)
        {
            TMP_InputField f = fields[i];
            if (f == null)
            {
                continue;
            }

            Graphic g = f.GetComponent<Graphic>();
            string raycast = "(Graphic없음)";
            if (g != null)
            {
                raycast = g.raycastTarget.ToString();
            }

            Debug.Log($"[MRInput/진단] '{f.gameObject.name}' interactable={f.interactable} readOnly={f.readOnly} raycastTarget={raycast} 활성={f.gameObject.activeInHierarchy} shouldHideSoftKeyboard={f.shouldHideSoftKeyboard} 부모활성={f.transform.parent != null && f.transform.parent.gameObject.activeInHierarchy}");
        }
    }

    private void Update()
    {
        // 포커스가 잡히는 순간만 찍는다 (매 프레임 찍으면 로그가 묻힌다).
        TMP_InputField focused = null;
        TMP_InputField[] fields = targetFields;
        if (fields == null || fields.Length == 0)
        {
            return;
        }

        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i] != null && fields[i].isFocused)
            {
                focused = fields[i];
                break;
            }
        }

        if (focused == _diagLastFocused)
        {
            return;
        }

        _diagLastFocused = focused;
        if (focused != null)
        {
            Debug.Log($"[MRInput/진단] 포커스 획득: '{focused.gameObject.name}' text='{focused.text}'");
        }
        else
        {
            Debug.Log("[MRInput/진단] 포커스 해제");
        }
    }

    private void Awake()
    {
        if (!blockTextInput) return;""",
"J2. 키보드 계측")

# ===== J3. 합성 이벤트에 ai_info 추가 (NRE 노이즈 21건 제거) =====
patch("ApiVlRouterManager.cs",
"""        conversationEvent["query"] = new JObject
        {
            ["origin"] = currentQuery,
            ["text"] = currentQuery
        };""",
"""        conversationEvent["query"] = new JObject
        {
            ["origin"] = currentQuery,
            ["text"] = currentQuery
        };
        // ai_info가 없으면 APIManager.PrepareConversationReplyUiFromRouter가 NRE를 낸다.
        // try/catch 안이라 동작에는 지장이 없지만 catch가 스택 전체를 찍어 로그를 오염시킨다
        // (2026-08-25 실측: 한 세션에 21건). 빈 값으로 채워 조용히 통과시킨다.
        conversationEvent["ai_info"] = new JObject
        {
            ["server_type"] = "",
            ["model"] = "",
            ["prompt"] = "",
            ["lang_used"] = "",
            ["translator"] = "",
            ["time"] = "",
            ["emotion"] = ""
        };""",
"J3. ai_info 채우기")

sys.exit(0 if ok else 1)
