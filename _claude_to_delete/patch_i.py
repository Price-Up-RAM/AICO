# -*- coding: utf-8 -*-
# 주크박스 수정 — '호출했다'와 '재생된다'를 구분한다 (Kickoff Guide 4-58)
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


# ===== I1. ExecuteJukeboxPlay 전면 교체 =====
patch("ApiAgentFunctionManager.cs",
"""        // 곡 이름이 없으면 재개. 이미 재생 중이면 그대로 둔다.
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
        onComplete?.Invoke(false, $"'{trackName}'에 해당하는 곡을 찾지 못했습니다. 재생 가능한 곡: {available}");""",
"""        if (jukebox.Tracks.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[AgentFunc/jukebox] 재생 목록이 비어 있다");
            onComplete?.Invoke(false, "재생할 수 있는 곡이 없습니다.");
            return;
        }

        // 곡 이름이 없는 경우. 서버가 parameters를 비워 보내는 것이 실측됐다.
        if (string.IsNullOrEmpty(trackName))
        {
            if (jukebox.IsPlaying)
            {
                UnityEngine.Debug.Log($"[AgentFunc/jukebox] 이미 재생 중 - 그대로 둔다 ({jukebox.CurrentTrackName})");
                onComplete?.Invoke(true, $"이미 {jukebox.CurrentTrackName}을(를) 재생 중입니다.");
                return;
            }

            // MRJukebox는 _currentIndex 초기값이 -1이고 playOnAwake가 꺼져 있다.
            // 그 상태에서 Resume()은 두 분기 모두 실패해 조용히 아무것도 하지 않는다.
            // 선택된 곡이 없으면 첫 곡부터 재생해야 한다.
            if (jukebox.CurrentIndex < 0)
            {
                UnityEngine.Debug.Log("[AgentFunc/jukebox] 선택된 곡 없음 - 첫 곡부터 재생");
                jukebox.PlayTrack(0);
            }
            else
            {
                jukebox.Resume();
            }

            ReportJukeboxPlayResult(jukebox, "재개", onComplete);
            return;
        }

        // 1순위: 곡 이름 부분 일치
        int index = FindJukeboxTrackIndex(jukebox, trackName);
        if (index >= 0)
        {
            jukebox.PlayTrack(index);
            ReportJukeboxPlayResult(jukebox, $"이름 매칭 '{trackName}' → index={index}", onComplete);
            return;
        }

        // 2순위: 태그 부분 일치 (MRJukebox.PlayByTag는 매칭 곡 중 랜덤 1개를 고른다)
        jukebox.PlayByTag(trackName);
        if (jukebox.IsPlaying)
        {
            ReportJukeboxPlayResult(jukebox, $"태그 매칭 '{trackName}'", onComplete);
            return;
        }

        // 실패 — 어떤 곡이 있는지 함께 돌려준다.
        // JukeboxView가 비활성이면 StreamingAssets/다운로드 곡이 아직 로드되지 않아 목록이 짧다.
        string available = BuildJukeboxTrackNames(jukebox);
        UnityEngine.Debug.LogWarning($"[AgentFunc/jukebox] '{trackName}' 매칭 실패 | 보유 {jukebox.Tracks.Count}곡=[{available}]");
        onComplete?.Invoke(false, $"'{trackName}'에 해당하는 곡을 찾지 못했습니다. 재생 가능한 곡: {available}");""",
"I1. ExecuteJukeboxPlay 사후 검증")

# ===== I2. 결과 검증 헬퍼 =====
patch("ApiAgentFunctionManager.cs",
"""    private void ExecuteJukeboxStop(Action<bool, string> onComplete)""",
"""    // 재생 명령을 부른 뒤 '실제로 소리가 나는지'를 확인해서 성공/실패를 가른다.
    // 호출했다는 것과 재생된다는 것은 다른 사실이다 (Kickoff Guide 4-58).
    // 이 검증이 없어서 Resume()이 조용히 무시된 경우에도 성공을 반환했다 (2026-08-25 실측).
    private void ReportJukeboxPlayResult(MRJukebox jukebox, string how, Action<bool, string> onComplete)
    {
        string trackName = jukebox.CurrentTrackName;
        bool playing = jukebox.IsPlaying;

        UnityEngine.Debug.Log($"[AgentFunc/jukebox] {how} | isPlaying={playing} index={jukebox.CurrentIndex} track='{trackName}' 보유={jukebox.Tracks.Count}곡");

        if (!playing || string.IsNullOrEmpty(trackName))
        {
            onComplete?.Invoke(false, "음악 재생에 실패했습니다.");
            return;
        }

        onComplete?.Invoke(true, $"{trackName}을(를) 재생합니다.");
    }

    private void ExecuteJukeboxStop(Action<bool, string> onComplete)""",
"I2. ReportJukeboxPlayResult 헬퍼")

# ===== I3. next 도 사후 검증 =====
patch("ApiAgentFunctionManager.cs",
"""        jukebox.PlayNext();
        UnityEngine.Debug.Log($"[AgentFunc/jukebox] 다음 곡: {jukebox.CurrentTrackName}");
        onComplete?.Invoke(true, $"다음 곡 {jukebox.CurrentTrackName}을(를) 재생합니다.");""",
"""        jukebox.PlayNext();
        ReportJukeboxPlayResult(jukebox, "다음 곡", onComplete);""",
"I3. next 사후 검증")

# ===== I4. stop 도 사후 검증 =====
patch("ApiAgentFunctionManager.cs",
"""        jukebox.StopPlayback();
        UnityEngine.Debug.Log("[AgentFunc/jukebox] 정지");
        onComplete?.Invoke(true, "음악을 정지했습니다.");""",
"""        jukebox.StopPlayback();
        UnityEngine.Debug.Log($"[AgentFunc/jukebox] 정지 | isPlaying={jukebox.IsPlaying}");
        if (jukebox.IsPlaying)
        {
            onComplete?.Invoke(false, "음악 정지에 실패했습니다.");
            return;
        }
        onComplete?.Invoke(true, "음악을 정지했습니다.");""",
"I4. stop 사후 검증")

sys.exit(0 if ok else 1)
