# -*- coding: utf-8 -*-
# Phase 5 — 주크박스 4종 구현
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


# ===== H1. ExecuteAction 분기 4종 =====
patch("ApiAgentFunctionManager.cs",
"""        else if (functionName == "physical_click")""",
"""        else if (functionName == "jukebox_play")
        {
            // track_name이 비면 현재 곡 재개. 서버가 파라미터를 안 채워 보내는 경우가 실측됐다.
            string trackName = GetParam<string>(parameters, "track_name", "");
            ExecuteJukeboxPlay(trackName, onComplete);
        }
        else if (functionName == "jukebox_stop")
        {
            ExecuteJukeboxStop(onComplete);
        }
        else if (functionName == "jukebox_next")
        {
            ExecuteJukeboxNext(onComplete);
        }
        else if (functionName == "jukebox_get_music_list")
        {
            ExecuteJukeboxGetMusicList(onComplete);
        }
        else if (functionName == "physical_click")""",
"H1. jukebox 분기 4종")

# ===== H2. 헬퍼 =====
patch("ApiAgentFunctionManager.cs",
"""    // 상대 타이머 생성 후 즉시 시작한다.""",
"""    // ===== 주크박스 (Phase 5) =====
    // MRJukebox는 씬의 GameObject 'JukeBox'(활성)에 붙어 있어 Instance가 정상 동작한다.
    // 그래도 비활성 저장으로 바뀔 가능성에 대비해 폴백을 둔다 (AlarmManager가 실제로 그런 상태다).
    private MRJukebox FindJukebox()
    {
        if (MRJukebox.Instance != null)
        {
            return MRJukebox.Instance;
        }

        MRJukebox[] found = Resources.FindObjectsOfTypeAll<MRJukebox>();
        for (int i = 0; i < found.Length; i++)
        {
            MRJukebox item = found[i];
            if (item == null || item.gameObject == null)
            {
                continue;
            }
            if (!item.gameObject.scene.IsValid())
            {
                continue;
            }
            return item;
        }

        return null;
    }

    // 곡 이름 부분 일치 검색 (대소문자 무시). 못 찾으면 -1.
    // JukeboxView.IndexOfTrackName은 정확 일치라 '캠프파이어' 같은 요청을 못 잡는다.
    private int FindJukeboxTrackIndex(MRJukebox jukebox, string trackName)
    {
        if (jukebox == null || string.IsNullOrEmpty(trackName))
        {
            return -1;
        }

        string needle = trackName.ToLower();
        var tracks = jukebox.Tracks;
        for (int i = 0; i < tracks.Count; i++)
        {
            string name = tracks[i].trackName;
            if (string.IsNullOrEmpty(name) && tracks[i].clip != null)
            {
                name = tracks[i].clip.name;
            }
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }
            if (name.ToLower().Contains(needle))
            {
                return i;
            }
        }

        return -1;
    }

    // 현재 로드된 곡 이름을 쉼표로 잇는다. 실패 응답에 담아 서버·캐릭터가 안내할 수 있게 한다.
    private string BuildJukeboxTrackNames(MRJukebox jukebox)
    {
        if (jukebox == null)
        {
            return "";
        }

        List<string> names = new List<string>();
        var tracks = jukebox.Tracks;
        for (int i = 0; i < tracks.Count; i++)
        {
            string name = tracks[i].trackName;
            if (string.IsNullOrEmpty(name) && tracks[i].clip != null)
            {
                name = tracks[i].clip.name;
            }
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        return string.Join(", ", names.ToArray());
    }

    private void ExecuteJukeboxPlay(string trackName, Action<bool, string> onComplete)
    {
        MRJukebox jukebox = FindJukebox();
        if (jukebox == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/jukebox] MRJukebox를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "주크박스를 사용할 수 없습니다.");
            return;
        }

        // 곡 이름이 없으면 재개. 이미 재생 중이면 그대로 둔다.
        if (string.IsNullOrEmpty(trackName))
        {
            if (jukebox.IsPlaying)
            {
                UnityEngine.Debug.Log("[AgentFunc/jukebox] 이미 재생 중 - 그대로 둔다");
                onComplete?.Invoke(true, $"이미 {jukebox.CurrentTrackName}을(를) 재생 중입니다.");
                return;
            }

            jukebox.Resume();
            UnityEngine.Debug.Log($"[AgentFunc/jukebox] 재개: {jukebox.CurrentTrackName}");
            onComplete?.Invoke(true, $"{jukebox.CurrentTrackName} 재생을 시작했습니다.");
            return;
        }

        // 1순위: 곡 이름 부분 일치
        int index = FindJukeboxTrackIndex(jukebox, trackName);
        if (index >= 0)
        {
            jukebox.PlayTrack(index);
            UnityEngine.Debug.Log($"[AgentFunc/jukebox] 이름 매칭 재생: '{trackName}' → index={index} ({jukebox.CurrentTrackName})");
            onComplete?.Invoke(true, $"{jukebox.CurrentTrackName}을(를) 재생합니다.");
            return;
        }

        // 2순위: 태그 부분 일치 (MRJukebox.PlayByTag는 매칭 곡 중 랜덤 1개를 고른다)
        string beforeTrack = jukebox.CurrentTrackName;
        jukebox.PlayByTag(trackName);
        if (jukebox.IsPlaying && jukebox.CurrentTrackName != beforeTrack)
        {
            UnityEngine.Debug.Log($"[AgentFunc/jukebox] 태그 매칭 재생: '{trackName}' → {jukebox.CurrentTrackName}");
            onComplete?.Invoke(true, $"{jukebox.CurrentTrackName}을(를) 재생합니다.");
            return;
        }

        // 실패 — 어떤 곡이 있는지 함께 돌려준다.
        // JukeboxView가 비활성이면 StreamingAssets/다운로드 곡이 아직 로드되지 않아 목록이 짧다.
        string available = BuildJukeboxTrackNames(jukebox);
        UnityEngine.Debug.LogWarning($"[AgentFunc/jukebox] '{trackName}' 매칭 실패 | 보유 {jukebox.Tracks.Count}곡=[{available}]");
        onComplete?.Invoke(false, $"'{trackName}'에 해당하는 곡을 찾지 못했습니다. 재생 가능한 곡: {available}");
    }

    private void ExecuteJukeboxStop(Action<bool, string> onComplete)
    {
        MRJukebox jukebox = FindJukebox();
        if (jukebox == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/jukebox] MRJukebox를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "주크박스를 사용할 수 없습니다.");
            return;
        }

        jukebox.StopPlayback();
        UnityEngine.Debug.Log("[AgentFunc/jukebox] 정지");
        onComplete?.Invoke(true, "음악을 정지했습니다.");
    }

    private void ExecuteJukeboxNext(Action<bool, string> onComplete)
    {
        MRJukebox jukebox = FindJukebox();
        if (jukebox == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/jukebox] MRJukebox를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "주크박스를 사용할 수 없습니다.");
            return;
        }

        if (jukebox.Tracks.Count == 0)
        {
            onComplete?.Invoke(false, "재생할 곡이 없습니다.");
            return;
        }

        jukebox.PlayNext();
        UnityEngine.Debug.Log($"[AgentFunc/jukebox] 다음 곡: {jukebox.CurrentTrackName}");
        onComplete?.Invoke(true, $"다음 곡 {jukebox.CurrentTrackName}을(를) 재생합니다.");
    }

    private void ExecuteJukeboxGetMusicList(Action<bool, string> onComplete)
    {
        MRJukebox jukebox = FindJukebox();
        if (jukebox == null)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/jukebox] MRJukebox를 씬에서 찾지 못했다");
            onComplete?.Invoke(false, "주크박스를 사용할 수 없습니다.");
            return;
        }

        string available = BuildJukeboxTrackNames(jukebox);
        string state = "정지";
        if (jukebox.IsPlaying)
        {
            state = $"재생 중 ({jukebox.CurrentTrackName})";
        }
        else if (jukebox.IsPaused)
        {
            state = $"일시정지 ({jukebox.CurrentTrackName})";
        }

        // JukeboxView가 비활성이면 StreamingAssets/다운로드 곡이 아직 목록에 없다.
        // 목록이 짧게 나오는 이유가 여기라 진단을 남긴다.
        UnityEngine.Debug.Log($"[AgentFunc/jukebox] 목록 조회 {jukebox.Tracks.Count}곡 | 상태={state}");
        onComplete?.Invoke(true, $"곡 {jukebox.Tracks.Count}개: {available} / 현재 {state}");
    }

    // 상대 타이머 생성 후 즉시 시작한다.""",
"H2. 주크박스 헬퍼")

# ===== H3. 레지스트리 등록 =====
patch("ApiAgentFunctionManager.cs",
"""            // Alarm
            F("alarm_set_timer",""",
"""            // Audio - Jukebox
            F("jukebox_play", "audio", "주크박스에서 곡 이름으로 배경음악을 찾아 재생. 이름을 비우면 현재 곡을 재생/재개", false, new JArray {
                P("track_name", "string", false, "재생할 곡 이름 (일부만 입력해도 매칭)")
            }),
            F("jukebox_stop", "audio", "주크박스에서 재생 중인 배경음악을 정지", false),
            F("jukebox_next", "audio", "주크박스의 다음 곡으로 넘어가 재생", false),
            F("jukebox_get_music_list", "audio", "주크박스에 등록된 곡 목록과 현재 재생 상태를 조회", false),

            // Alarm
            F("alarm_set_timer",""",
"H3. 레지스트리 등록")

sys.exit(0 if ok else 1)
