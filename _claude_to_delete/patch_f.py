# -*- coding: utf-8 -*-
# Phase 5 보강 3 — chatIdx 증가 / 구간 계측 / 재생 계측
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


# ===== F1. Whisper 경로에서 chatIdx 증가 =====
# ChatHandler:231과 같은 규칙 — 호출부가 올린다. STTUtil은 :328에서 이미 올리므로 건드리지 않는다.
patch("WhisperSTTManager.cs",
"""        // 대화 시작 — 라우터+스킬 경로 (Phase 5).
        // 말풍선·TTS 출력은 ApiVlRouterManager 내부에서 처리되므로 호출부에 배선이 필요 없다.
        ApiVlRouterManager.Instance.ExecuteVlRouterRun(query, sttLang: response.lang);""",
"""        // chatIdx 증가 — 라우터는 GameManager.chatIdx를 '읽기만' 하므로 호출부가 올려야 한다.
        // 서버 STT 경로(STTUtil.cs)는 자체적으로 올리지만 Whisper 경로에는 그게 없었다.
        // conversation_stream 시절에는 response.chatIdx를 인자로 넘겨 써서 드러나지 않았다.
        GameManager.Instance.chatIdx += 1;
        GameManager.Instance.chatIdxRegenerateCount = 0;

        // 대화 시작 — 라우터+스킬 경로 (Phase 5).
        // 말풍선·TTS 출력은 ApiVlRouterManager 내부에서 처리되므로 호출부에 배선이 필요 없다.
        Debug.Log($"[MRChat/whisper] 라우터 요청 시작 chatIdx={GameManager.Instance.chatIdx} lang={response.lang} len={query.Length}");
        ApiVlRouterManager.Instance.ExecuteVlRouterRun(query, sttLang: response.lang);""",
"F1. Whisper chatIdx 증가 + 시작 계측")

# ===== F2. 라우터 구간 경과 계측 (7-1 C) =====
patch("ApiVlRouterManager.cs",
"""    private string routerActionChannel = "?";""",
"""    private float routerStartTime = 0f;  // 요청 시작 시각. 구간 경과 계측용 (7-1 C)
    private string routerActionChannel = "?";""",
"F2a. routerStartTime 필드")

patch("ApiVlRouterManager.cs",
"""        currentSttLang = sttLang;
        currentOnEvent = onEvent;""",
"""        routerStartTime = Time.realtimeSinceStartup;
        currentSttLang = sttLang;
        currentOnEvent = onEvent;""",
"F2b. 시작 시각 기록")

patch("ApiVlRouterManager.cs",
"""        BeginRouterConversationIfNeeded();
        Debug.Log("[VlRouterRun] data.reply_list 표시 - /conversation_stream 재호출 없음");""",
"""        BeginRouterConversationIfNeeded();
        // 구간 계측: 발화 → 대사 도착까지 몇 초 걸렸는지, 대사가 몇 개로 쪼개져 왔는지 (7-1 C)
        // conversation_stream은 문장마다 스트리밍했지만 라우터는 done에서 전문을 한 번에 준다.
        Debug.Log($"[VlRouterRun/계측] 대사 도착 {Time.realtimeSinceStartup - routerStartTime:F2}s | reply 개수={((JArray)replyList).Count} | chatIdx={currentChatIdx}");""",
"F2c. 대사 도착 계측")

# ===== F3. TTS 요청/수락 구간 계측 =====
patch("TTSManager.cs",
"""            Debug.Log($"[TTS_Flow] 4.TTS수락 seq={seq} → 재생큐 추가");""",
"""            Debug.Log($"[TTS_Flow] 4.TTS수락 seq={seq} → 재생큐 추가");
            // '큐에 넣었다'와 '실제로 소리가 난다'는 다른 사실이다 (Kickoff Guide 4-58).
            // 재생 단계 실측은 VoiceManager.AddToQueue의 [Voice/재생] 로그를 볼 것.""",
"F3. TTS 수락 주석")

# ===== F4. 재생 단계 계측 =====
patch("VoiceManager.cs",
"""    public void AddToQueue(AudioClip clip)
    {
        clipQueue.Enqueue(clip); // 클립을 Queue에 추가""",
"""    public void AddToQueue(AudioClip clip)
    {
        // 재생 계측(Phase 5): 큐 적재와 실제 출력은 다른 사실이다 (Kickoff Guide 4-58).
        // clip이 null이거나 길이 0, 볼륨 0, mute면 '재생됐다'는 로그만 남고 소리는 안 난다.
        string clipInfo = "clip=null";
        if (clip != null)
        {
            clipInfo = $"clip='{clip.name}' {clip.length:F2}s ch={clip.channels} hz={clip.frequency}";
        }
        string srcInfo = "audioSource=null";
        if (audioSource != null)
        {
            srcInfo = $"vol={audioSource.volume:F2} mute={audioSource.mute} enabled={audioSource.enabled} playing={audioSource.isPlaying} listener={AudioListener.volume:F2} pause={AudioListener.pause}";
        }
        Debug.Log($"[Voice/재생] 큐 적재 | {clipInfo} | {srcInfo} | 큐길이={clipQueue.Count}");

        clipQueue.Enqueue(clip); // 클립을 Queue에 추가""",
"F4a. AddToQueue 계측")

patch("VoiceManager.cs",
"""            isQueuePlaying = true;
            audioSource.clip = clipQueue.Dequeue();  // Queue에서 클립을 가져옴
            audioSource.Play();  // AudioSource로 재생 시작""",
"""            isQueuePlaying = true;
            audioSource.clip = clipQueue.Dequeue();  // Queue에서 클립을 가져옴
            audioSource.Play();  // AudioSource로 재생 시작
            // 계측: Play() 직후 isPlaying이 false면 클립/볼륨/리스너 중 하나가 죽어 있다는 뜻
            string playedName = "null";
            if (audioSource.clip != null)
            {
                playedName = audioSource.clip.name;
            }
            Debug.Log($"[Voice/재생] Play 호출 | clip='{playedName}' isPlaying={audioSource.isPlaying} vol={audioSource.volume:F2} listener={AudioListener.volume:F2} | 남은큐={clipQueue.Count}");""",
"F4b. PlayNextClip 계측")

sys.exit(0 if ok else 1)
