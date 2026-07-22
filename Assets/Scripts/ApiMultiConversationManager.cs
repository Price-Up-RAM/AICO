using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 멀티 참가자 대화 클라이언트 — POST /multi/conversation_stream 소비
// (설계: MY-Little-Jarvis-Plus/plan/todo_multi_conversation.md)
// - 참가자 = 소환 상태 스냅샷(메인 + 소환된 서브 캐릭터). 발화자 판정은 서버(LLM 1콜)가 수행
// - 스트림은 화자 단위 누적 reply — 수신 청크의 speaker 실명을 그대로 {메인닉네임}_multi 메모리에 기록
// - 서버가 query를 채널 이력에 스스로 편입하므로, 전송 memory에는 현재 발화를 포함하지 않는다(스냅샷 방식)
// - 기존 매니저(TTS/말풍선/메모리/캐릭터)는 호출만 한다. 입력을 막는 락 없이 요청 Abort로 인터럽트
public class ApiMultiConversationManager : MonoBehaviour
{
    private static ApiMultiConversationManager instance;
    public static ApiMultiConversationManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ApiMultiConversationManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("ApiMultiConversationManager");
                    instance = go.AddComponent<ApiMultiConversationManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private const int MaxResponders = 1;           // 라운드당 최대 응답 캐릭터 수 (동시엔 1명만 — 복수 반응은 연쇄 재판정)
    private const int MaxAiUtterancesPerTurn = 3;  // 유저 발화 1회당 AI 발화 예산 (연쇄 폭주 방지)
    private const int TtsIdlePollMs = 500;         // 말풍선 정리 전 TTS 유휴 폴링 간격
    private const int TtsIdleTimeoutMs = 30000;    // TTS 유휴 대기 상한
    private const int TtsIdleStableCount = 3;      // 유휴 판정 연속 충족 횟수 (합성 인플라이트 틈새 대비)

    // 화자 1명의 수신 상태 (reply_list는 화자 단위 누적이므로 화자별로 분리 보관)
    private class SpeakerState
    {
        public List<string> replyKo = new List<string>();
        public List<string> replyJa = new List<string>();
        public List<string> replyEn = new List<string>();
        public int ttsSentCount = 0;          // TTS 제출 완료 문장 수 (누적 diff 기준점)
        public bool balloonShown = false;     // 말풍선 최초 표시 여부
        public GameObject thinkingBalloon;    // thinking 이모션 말풍선 (첫 reply에 제거)
        public bool memorySaved = false;      // 발화 확정(메모리 저장) 여부
    }

    // 라운드 처리 결과 — finalized는 발화 확정 순서 유지 (연쇄 스냅샷 갱신용)
    private class RoundResult
    {
        public List<string> responded = new List<string>();
        public List<Conversation> finalized = new List<Conversation>();
        public bool userAddressed = false;
        public bool balloonShown = false;
    }

    private HttpWebRequest currentRequest;    // 진행 중 요청 (인터럽트 Abort 대상)
    private int turnToken = 0;                // 발화 턴 세대 토큰 (인터럽트 후 낡은 콜백 무시)
    private bool isTurnRunning = false;       // 현재 턴 진행 여부 (입력을 막지 않음 — 새 입력은 인터럽트)
    private GameObject turnNoticeBalloon;     // 전송 즉시 띄우는 대기 연출 (첫 서버 이벤트 도착 시 제거)

    // 인터럽트 시 동기 발화 확정용 — 진행 중 라운드의 수신 상태 참조 (스트림 처리 중에만 유효)
    private Dictionary<string, SpeakerState> activeSpeakerStates;
    private string activeSpeaker;
    private string activeMultiNickname;
    private RoundResult activeResult;

    // 멀티 대화 활성 조건: 메인 캐릭터 + 소환된 서브 캐릭터 1명 이상 (= AI 참가자 2명 이상)
    public static bool IsActive()
    {
        if (CharManager.Instance == null || SubCharManager.Instance == null) return false;
        if (CharManager.Instance.GetCurrentCharacter() == null) return false;
        if (SubCharManager.Instance.subCharsContainer == null) return false;

        // 소환된(파괴 예약 아닌) 서브 캐릭터가 하나라도 있으면 활성
        foreach (Transform child in SubCharManager.Instance.subCharsContainer.transform)
        {
            if (child != null && child.gameObject != null && child.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    // 유저 발화 진입점 (ChatHandler에서 호출) — 진행 중 턴이 있으면 인터럽트 후 새 턴 시작
    public void SendUserMessage(string input)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(input.Trim())) return;

        // 유저 발화 항상 우선: 진행 중이면 즉시 중단 (isProcessing식 입력 드랍 금지)
        if (isTurnRunning) InterruptCurrentTurn();

        turnToken += 1;
        _ = RunTurnAsync(input, turnToken);
    }

    // 턴 시작 시 띄운 대기 연출 제거 (첫 서버 이벤트 도착/인터럽트/종결 시)
    private void ClearTurnNoticeBalloon()
    {
        if (turnNoticeBalloon != null)
        {
            Destroy(turnNoticeBalloon);
            turnNoticeBalloon = null;
        }
    }

    // 진행 중 턴 중단 — 표시/재생분 동기 확정 후 요청 Abort + TTS 세션 취소
    private void InterruptCurrentTurn()
    {
        Debug.Log("[Multi] Interrupt: aborting current turn");
        ClearTurnNoticeBalloon();

        // 이미 표시/재생된 발화를 채널 메모리에 확정 (절단 발화는 표시된 문장까지만 기록)
        // — 새 턴의 유저 발화 저장보다 먼저 수행해 채널 파일의 시간 순서를 보존
        FinalizeSpeaker(activeSpeaker, activeSpeakerStates, activeMultiNickname, activeResult);

        try { currentRequest?.Abort(); } catch (Exception e) { Debug.LogWarning($"[Multi] Abort failed: {e.Message}"); }
        currentRequest = null;
        TTSManager.Instance.CancelTtsSession();
    }

    // 발화 턴 본체: 유저 발화 처리 → (예산 내) AI 발화 재판정 연쇄 → 종결
    private async Task RunTurnAsync(string input, int token)
    {
        isTurnRunning = true;

        // 참가자/메모리 컨텍스트 스냅샷 (소환 상태 = 참가자)
        GameObject mainGo = CharManager.Instance != null ? CharManager.Instance.GetCurrentCharacter() : null;
        if (mainGo == null)
        {
            Debug.LogWarning("[Multi] main character not found — turn cancelled");
            isTurnRunning = false;
            return;
        }
        string mainNickname = CharManager.Instance.GetNickname(mainGo);
        string multiNickname = mainNickname + "_multi";  // 채널 메모리 파일 키: conversation_memory_{메인}_multi.json
        Dictionary<string, GameObject> speakerObjects = BuildParticipantObjects(mainGo, mainNickname);
        string participantsJson = BuildParticipantsJson(speakerObjects, mainNickname);

        // 전송 즉시 대기 연출 — 판정이 끝나기 전에도 반응이 보이도록 메인 캐릭터에 로딩 표시 (1:1 관례)
        ClearTurnNoticeBalloon();
        if (EmotionBalloonManager.Instance != null)
        {
            turnNoticeBalloon = EmotionBalloonManager.Instance.ShowEmotionBalloon(mainGo, "Time");
        }

        // 전송용 이력 스냅샷 — 현재 발화 저장 "이전"에 확보 (서버가 query를 스스로 이력에 편입하므로
        //  전송 memory에 현재 발화가 포함되면 프롬프트에 이중 등장함)
        List<Conversation> channelSnapshot = MemoryManager.Instance.GetAllConversationMemory(multiNickname);

        // 유저 발화를 실명(sensei)으로 채널 메모리 파일에 기록 (영속화 — 스냅샷에는 미포함)
        MemoryManager.Instance.SaveConversationMemory("sensei", "user", input, input, input, input, multiNickname);

        // chatIdx 세대 갱신 (ChatHandler가 chatIdx를 이미 +1 함) + 말풍선/TTS 검증 관례 준수
        string chatIdx = GameManager.Instance.chatIdx.ToString();
        GameManager.Instance.chatIdxSuccess = chatIdx;
        GameManager.Instance.chatIdxBalloon = GameManager.Instance.chatIdx;

        // 턴 단위 TTS 세션 (연쇄 라운드에 걸쳐 1세션 유지 — 라운드마다 새로 열면 직전 잔여 음성이 flush됨)
        TTSManager.Instance.StartTtsSession(GameManager.Instance.chatIdx);
        int ttsSession = TTSManager.Instance.GetSessionId();

        // 클릭 타겟이 서브 캐릭터면 char 지정 (서버가 판정 스킵하고 확정 응답)
        string charTarget = null;
        if (CharManager.Instance.activeCharacter != null && CharManager.Instance.activeCharacter != mainGo)
        {
            charTarget = CharManager.Instance.GetNickname(CharManager.Instance.activeCharacter);
        }

        string query = input;             // 이번 라운드에 처리할 발화
        string querySpeaker = "sensei";   // 발화자 (연쇄 라운드에서는 직전 AI 실명)
        int utterancesUsed = 0;           // 사용한 AI 발화 예산
        bool anyBalloonShown = false;     // 종결 시 말풍선 정리 여부

        try
        {
            // 발화 예산 내 라운드 반복 — 종료 3조건: 예산 소진 / 응답자 없음(침묵) / 유저 대상 발화
            while (utterancesUsed < MaxAiUtterancesPerTurn)
            {
                string memoryJson = JsonConvert.SerializeObject(channelSnapshot);
                RoundResult round = await ProcessUtteranceAsync(
                    query, querySpeaker, charTarget, participantsJson, memoryJson, multiNickname,
                    chatIdx, ttsSession, mainNickname, speakerObjects, token);

                if (round == null || token != turnToken) return;  // 인터럽트/실패 — 조용히 종료
                anyBalloonShown = anyBalloonShown || round.balloonShown;

                if (round.responded.Count == 0 || round.finalized.Count == 0) break;  // 침묵 = 정상 종결
                utterancesUsed += round.responded.Count;
                if (round.userAddressed) break;                                       // 유저 대상 발화 = 유저 턴

                // 연쇄 스냅샷 갱신: 이번 라운드의 query 편입 + 확정 발화 중 마지막을 제외하고 편입
                //  (마지막 발화는 다음 라운드의 query가 되고, 서버가 이력에 편입함)
                channelSnapshot.Add(BuildConversationEntry(querySpeaker, query, query, query, query));
                for (int i = 0; i < round.finalized.Count - 1; i++) channelSnapshot.Add(round.finalized[i]);

                Conversation last = round.finalized[round.finalized.Count - 1];
                query = last.message;
                querySpeaker = last.speaker;
                charTarget = null;  // char 지정은 첫 라운드만
            }
        }
        finally
        {
            if (token == turnToken)
            {
                // 턴 종결: 대기 연출 제거 + TTS 스트림 종료 스탬프 + 잔여 음성 재생 완료 후 말풍선 정리
                ClearTurnNoticeBalloon();
                TTSManager.Instance.MarkStreamCompleted(ttsSession);
                if (anyBalloonShown) _ = HideBalloonsWhenTtsIdleAsync(ttsSession, speakerObjects, mainNickname, token);
                currentRequest = null;
                isTurnRunning = false;
            }
        }
    }

    // 발화 1건 처리: /multi/conversation_stream 호출 → thinking/reply/final 스트림 소비
    private async Task<RoundResult> ProcessUtteranceAsync(
        string query, string querySpeaker, string charTarget, string participantsJson, string memoryJson,
        string multiNickname, string chatIdx, int ttsSession, string mainNickname,
        Dictionary<string, GameObject> speakerObjects, int token)
    {
        // baseUrl 획득 (콜백 → Task 변환 관례)
        var urlTcs = new TaskCompletionSource<string>();
        ServerManager.Instance.GetBaseUrl((url) => urlTcs.TrySetResult(url));
        string baseUrl = await urlTcs.Task;
        if (token != turnToken) return null;
        string apiUrl = baseUrl + "/multi/conversation_stream";

        // 요청 폼 조립 — wire 키는 기존 /conversation_stream과 완전 동일(1:1 파서 재사용 계약) + 멀티 전용 필드
        var settings = SettingManager.Instance.settings;
        string guidelineJson = UIUserCardManager.Instance.GetGuidelineListJson();
        string situationJson = UIChatSituationManager.Instance.GetCurUIChatSituationInfoJson();
        var requestData = new Dictionary<string, string>
        {
            { "query", query },
            { "query_speaker", querySpeaker },
            { "participants", participantsJson },
            { "memory", memoryJson },
            { "chatIdx", chatIdx },
            { "player", settings.player_name ?? "sensei" },
            { "ai_language", settings.ai_language ?? "auto" },
            { "ai_language_out", settings.ui_language ?? "ko" },
            { "sound_language", settings.sound_language ?? "ja" },
            { "ai_emotion", settings.ai_emotion ?? "off" },
            { "guideline_list", guidelineJson },
            { "situation", situationJson },
            { "max_responders", MaxResponders.ToString() },
        };
        if (!string.IsNullOrEmpty(charTarget)) requestData["char"] = charTarget;

        var result = new RoundResult();
        var speakerStates = new Dictionary<string, SpeakerState>();  // 이번 라운드의 화자별 수신 상태
        string currentSpeaker = null;                                // 현재 발화 중 화자 (전환 감지용)

        // 인터럽트 동기 확정용 참조 등록
        activeSpeakerStates = speakerStates;
        activeSpeaker = null;
        activeMultiNickname = multiNickname;
        activeResult = result;

        try
        {
            // multipart/form-data 수동 조립 + 전송 (기존 스트리밍 클라이언트 관례)
            string boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
            request.Method = "POST";
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            currentRequest = request;

            using (MemoryStream memStream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(memStream, new UTF8Encoding(false), 1024, true))
                {
                    foreach (var entry in requestData)
                    {
                        writer.WriteLine($"--{boundary}");
                        writer.WriteLine($"Content-Disposition: form-data; name=\"{entry.Key}\"");
                        writer.WriteLine();
                        writer.WriteLine(entry.Value);
                    }
                    writer.WriteLine($"--{boundary}--");
                    writer.Flush();
                }
                request.ContentLength = memStream.Length;
                using (Stream requestStream = await request.GetRequestStreamAsync())
                {
                    memStream.Seek(0, SeekOrigin.Begin);
                    await memStream.CopyToAsync(requestStream);
                }
            }

            // 라인 단위 스트림 소비 (ping 무시, chat_idx 세대 검증)
            using (WebResponse response = await request.GetResponseAsync())
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(responseStream))
            {
                Debug.Log($"[Multi] stream opened (chat_idx={chatIdx}, speaker={querySpeaker})");
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (token != turnToken)
                    {
                        // 인터럽트됨 — 잔여 표시분 확정은 InterruptCurrentTurn이 수행, 여기선 정리만
                        ClearThinkingBalloons(speakerStates);
                        return null;
                    }
                    if (string.IsNullOrEmpty(line)) continue;

                    JObject data;
                    try { data = JObject.Parse(line); }
                    catch (JsonReaderException) { continue; }

                    string type = data["type"]?.ToString() ?? "unknown";
                    if (type == "ping") continue;  // keep-alive — 무시

                    string lineChatIdx = data["chat_idx"]?.ToString() ?? "";
                    if (lineChatIdx != chatIdx) continue;  // 낡은 세대 청크 폐기

                    // 첫 서버 이벤트 도착 — 전송 시 띄운 대기 연출 제거
                    ClearTurnNoticeBalloon();

                    switch (type)
                    {
                        case "thinking":
                            {
                                string speaker = data["speaker"]?.ToString() ?? "";
                                // 화자 전환 통지 → 직전 화자 발화 확정(메모리 저장)
                                FinalizeSpeaker(currentSpeaker, speakerStates, multiNickname, result);
                                currentSpeaker = speaker;
                                activeSpeaker = speaker;
                                Debug.Log($"[Multi] thinking: speaker={speaker}");
                                SpeakerState st = GetOrCreateState(speakerStates, speaker);
                                // 응답 준비 중 표시 — 응답 예정 캐릭터 머리 위 로딩 이모션 (아로프라의 대기 연출 방식)
                                if (speakerObjects.TryGetValue(speaker, out GameObject speakerGo) && speakerGo != null)
                                {
                                    st.thinkingBalloon = EmotionBalloonManager.Instance.ShowEmotionBalloon(speakerGo, "Time");
                                }
                                // 다음 발화자 힌트 — 메인 머리 위에 응답 예정 캐릭터 아이콘 (아로프라 연출의 일반화,
                                //  CharacterIconCatalog에 아이콘 미등록이면 생략)
                                if (speaker != mainNickname && CharacterIconCatalog.GetIcon(speaker) != null
                                    && speakerObjects.TryGetValue(mainNickname, out GameObject mainHintGo) && mainHintGo != null)
                                {
                                    EmotionBalloonManager.Instance.SetEmotionBalloonForTarget(mainHintGo, speaker, 3f);
                                }
                                break;
                            }
                        case "reply":
                            {
                                string speaker = data["speaker"]?.ToString() ?? "";
                                currentSpeaker = speaker;
                                activeSpeaker = speaker;
                                HandleReplyChunk(data, speaker, speakerStates, speakerObjects, mainNickname, chatIdx, ttsSession);
                                result.balloonShown = true;
                                break;
                            }
                        case "final":
                            {
                                // 마지막 화자 확정 + 종결 정보 수집
                                FinalizeSpeaker(currentSpeaker, speakerStates, multiNickname, result);
                                currentSpeaker = null;
                                activeSpeaker = null;
                                result.userAddressed = data["user_addressed"]?.ToObject<bool>() ?? false;
                                var responded = data["responded"] as JArray;
                                if (responded != null)
                                {
                                    foreach (var name in responded)
                                    {
                                        string n = name?.ToString();
                                        if (!string.IsNullOrEmpty(n) && !result.responded.Contains(n)) result.responded.Add(n);
                                    }
                                }
                                Debug.Log($"[Multi] final: responded=[{string.Join(",", result.responded)}] " +
                                          $"fallback={data["fallback"]} reason={data["reason"]} user_addressed={result.userAddressed}");
                                break;
                            }
                        case "error":
                            {
                                Debug.LogWarning($"[Multi] server error: {data["message"]}");
                                GameObject mainGo = speakerObjects.ContainsKey(mainNickname) ? speakerObjects[mainNickname] : null;
                                if (mainGo != null) EmotionBalloonManager.Instance.ShowEmotionBalloonForSec(mainGo, "No", 2f);
                                break;
                            }
                    }
                }
            }

            // 스트림이 final 없이 끝난 경우의 안전망 — 마지막 화자 확정
            FinalizeSpeaker(currentSpeaker, speakerStates, multiNickname, result);
            ClearThinkingBalloons(speakerStates);
            return result;
        }
        catch (WebException e)
        {
            // 인터럽트 Abort는 정상 경로 (RequestCanceled), 그 외는 경고
            if (e.Status != WebExceptionStatus.RequestCanceled)
            {
                Debug.LogWarning($"[Multi] request failed: {e.Message}");
            }
            // 표시/재생된 문장까지는 확정 (memorySaved 플래그로 중복 저장 방지)
            FinalizeSpeaker(currentSpeaker, speakerStates, multiNickname, result);
            ClearThinkingBalloons(speakerStates);
            return token == turnToken ? result : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Multi] stream error: {e.Message}");
            FinalizeSpeaker(currentSpeaker, speakerStates, multiNickname, result);
            ClearThinkingBalloons(speakerStates);
            return token == turnToken ? result : null;
        }
        finally
        {
            // 인터럽트 동기 확정용 참조 해제
            activeSpeakerStates = null;
            activeSpeaker = null;
            activeResult = null;
        }
    }

    // reply 청크 처리 — 화자 단위 누적 reply_list 재조립 + 신규 문장 TTS + 말풍선 갱신
    private void HandleReplyChunk(
        JObject data, string speaker, Dictionary<string, SpeakerState> speakerStates,
        Dictionary<string, GameObject> speakerObjects, string mainNickname, string chatIdx, int ttsSession)
    {
        SpeakerState st = GetOrCreateState(speakerStates, speaker);

        // 첫 응답 도착 — thinking 말풍선 제거
        if (st.thinkingBalloon != null)
        {
            Destroy(st.thinkingBalloon);
            st.thinkingBalloon = null;
        }

        // 누적 전체 재조립 (서버가 해당 화자의 전체 목록을 매번 재전송)
        st.replyKo.Clear(); st.replyJa.Clear(); st.replyEn.Clear();
        var replyList = data["reply_list"] as JArray;
        if (replyList == null) return;
        foreach (var reply in replyList)
        {
            st.replyKo.Add(reply["answer_ko"]?.ToString() ?? "");
            st.replyJa.Add(reply["answer_ja"]?.ToString() ?? "");
            st.replyEn.Add(reply["answer_en"]?.ToString() ?? "");
        }

        speakerObjects.TryGetValue(speaker, out GameObject speakerGo);
        if (speakerGo == null)
        {
            // 스트림 중 디스폰된 화자 — 표시/TTS는 드랍 (메모리 확정은 FinalizeSpeaker에서 수행)
            Debug.LogWarning($"[Multi] speaker object not found: {speaker} (display skipped)");
            return;
        }

        // 첫 응답 도착 로그 (진단용 — 화자당 1회)
        if (!st.balloonShown)
        {
            Debug.Log($"[Multi] first reply: speaker={speaker} isMain={speaker == mainNickname}");
        }

        // 발화 캐릭터 입모션 교체
        StatusManager.Instance.ClearSpeakingCharacters();
        StatusManager.Instance.AddSpeakingCharacter(speakerGo);

        // 말풍선 갱신 — 아로프라 방식: 화자별로 메인은 AnswerBalloon, 서브는 SubAnswerBalloon을 해당 캐릭터 머리 위에 표시
        string joinKo = JoinNonEmpty(st.replyKo);
        string joinJa = JoinNonEmpty(st.replyJa);
        string joinEn = JoinNonEmpty(st.replyEn);

        // 언어 슬롯 폴백 — 비어 있는 언어는 대표 텍스트로 채움 (아로프라 ShowPlanaMessage 관례:
        //  ui_language가 미채움 언어를 가리켜도 빈 말풍선이 되지 않도록)
        string displayText = PickDisplayText(joinKo, joinJa, joinEn);
        string displayKo = !string.IsNullOrEmpty(joinKo) ? joinKo : displayText;
        string displayJa = !string.IsNullOrEmpty(joinJa) ? joinJa : displayText;
        string displayEn = !string.IsNullOrEmpty(joinEn) ? joinEn : displayText;

        if (speaker == mainNickname)
        {
            AnswerBalloonManager.Instance.ShowAnswerBalloonInf();
            AnswerBalloonManager.Instance.ModifyAnswerBalloonTextInfo(displayKo, displayJa, displayEn);
            AnswerBalloonManager.Instance.ModifyAnswerBalloonText();
            st.balloonShown = true;
        }
        else
        {
            bool isFirstBalloon = !st.balloonShown;  // 프로브 1회 조건
            SubAnswerBalloonManager.Instance.ModifyAnswerBalloonTextInfo(displayKo, displayJa, displayEn);
            SubAnswerBalloonManager.Instance.ModifyAnswerBalloonText();
            RectTransform speakerRect = speakerGo.GetComponent<RectTransform>();
            if (speakerRect != null)
            {
                SubAnswerBalloonManager.Instance.ShowAnswerBalloonInfAtCharacter(speakerRect);
            }
            else
            {
                SubAnswerBalloonManager.Instance.ShowAnswerBalloonInf();
            }
            st.balloonShown = true;

            // 말풍선 실체 프로브 (진단용 — 화자당 1회): 활성 여부/실제 좌표/텍스트 길이
            //  GameObject.Find는 활성 오브젝트만 찾으므로 activeInHierarchy 판별을 겸함
            if (isFirstBalloon)
            {
                GameObject balloonProbe = GameObject.Find("Image_SubAnswerBalloon");
                RectTransform probeRect = balloonProbe != null ? balloonProbe.GetComponent<RectTransform>() : null;
                Debug.Log($"[Multi] balloon probe: activeInHierarchy={balloonProbe != null}, " +
                          $"balloonPos={(probeRect != null ? probeRect.anchoredPosition.ToString() : "-")}, " +
                          $"charPos={(speakerRect != null ? speakerRect.anchoredPosition.ToString() : "null-rect")}, " +
                          $"uiLang={SettingManager.Instance.settings.ui_language}, lenKo/Ja/En={joinKo.Length}/{joinJa.Length}/{joinEn.Length}");
            }
        }

        // 신규 확정 문장만 TTS 제출 (제출 순서 = seq = 재생 순서 → 화자 직렬화 자동 보장)
        string soundLang = SettingManager.Instance.settings.sound_language ?? "ja";
        for (int i = st.ttsSentCount; i < replyList.Count; i++)
        {
            // 세션 선점 검사 — 낡은 세션이면 제출 생략
            if (ttsSession != TTSManager.Instance.GetSessionId()) break;
            string voiceText = soundLang == "ko" ? st.replyKo[i] : (soundLang == "en" ? st.replyEn[i] : st.replyJa[i]);
            if (!string.IsNullOrEmpty(voiceText))
            {
                bool isSubCharacter = speaker != mainNickname;  // 메인이 아니면 반드시 서브로 (음성 큐/wav 충돌 방지)
                TTSManager.Instance.RequestTTS(voiceText, chatIdx, soundLang, speaker, isSubCharacter);
            }
        }
        st.ttsSentCount = Math.Max(st.ttsSentCount, replyList.Count);  // 축소 청크 방어 (기준점 역행 금지)
    }

    // 화자 발화 확정 — 채널 메모리에 실명 speaker + role=assistant로 1회 저장 (중복 호출 무해)
    private void FinalizeSpeaker(string speaker, Dictionary<string, SpeakerState> speakerStates, string multiNickname, RoundResult result)
    {
        if (string.IsNullOrEmpty(speaker) || speakerStates == null || result == null) return;
        if (!speakerStates.TryGetValue(speaker, out SpeakerState st)) return;
        if (st.memorySaved) return;

        string joinKo = JoinNonEmpty(st.replyKo);
        string joinJa = JoinNonEmpty(st.replyJa);
        string joinEn = JoinNonEmpty(st.replyEn);
        string display = PickDisplayText(joinKo, joinJa, joinEn);
        if (string.IsNullOrEmpty(display)) return;

        MemoryManager.Instance.SaveConversationMemory(speaker, "assistant", display, joinKo, joinJa, joinEn, multiNickname);
        st.memorySaved = true;

        // 연쇄 스냅샷/재호출용 확정 발화 기록 (순서 유지)
        result.finalized.Add(BuildConversationEntry(speaker, display, joinKo, joinJa, joinEn));
    }

    // 전송 스냅샷용 Conversation 항목 생성 (파일 저장용 아님 — 서버 memory 필드 전용)
    private Conversation BuildConversationEntry(string speaker, string message, string messageKo, string messageJa, string messageEn)
    {
        return new Conversation
        {
            speaker = speaker,
            role = speaker == "sensei" ? "user" : "assistant",
            type = "conversation",
            message = message,
            message_trans = message,
            messageKo = messageKo,
            messageJa = messageJa,
            messageEn = messageEn,
            timestamp = "",
        };
    }

    // 참가자 스냅샷 — 닉네임 → GameObject (메인 + subCharsContainer 자식 순회)
    private Dictionary<string, GameObject> BuildParticipantObjects(GameObject mainGo, string mainNickname)
    {
        var map = new Dictionary<string, GameObject>();
        if (!string.IsNullOrEmpty(mainNickname)) map[mainNickname] = mainGo;

        if (SubCharManager.Instance != null && SubCharManager.Instance.subCharsContainer != null)
        {
            foreach (Transform child in SubCharManager.Instance.subCharsContainer.transform)
            {
                if (child == null || child.gameObject == null || !child.gameObject.activeInHierarchy) continue;
                string nickname = SubCharManager.Instance.GetNickname(child.gameObject);
                if (!string.IsNullOrEmpty(nickname) && !map.ContainsKey(nickname)) map[nickname] = child.gameObject;
            }
        }
        return map;
    }

    // participants JSON 조립 — sensei + AI 실명 목록 (메인에 is_main)
    private string BuildParticipantsJson(Dictionary<string, GameObject> speakerObjects, string mainNickname)
    {
        var participants = new List<Dictionary<string, object>>();
        string playerName = SettingManager.Instance.settings.player_name ?? "sensei";
        participants.Add(new Dictionary<string, object> { { "name", "sensei" }, { "type", "user" }, { "display_name", playerName } });

        foreach (var pair in speakerObjects)
        {
            participants.Add(new Dictionary<string, object>
            {
                { "name", pair.Key },
                { "type", "ai" },
                { "display_name", pair.Key },
                { "character_file", pair.Key },
                { "is_main", pair.Key == mainNickname },
            });
        }
        return JsonConvert.SerializeObject(participants);
    }

    // 화자별 수신 상태 획득/생성
    private SpeakerState GetOrCreateState(Dictionary<string, SpeakerState> speakerStates, string speaker)
    {
        if (!speakerStates.TryGetValue(speaker, out SpeakerState st))
        {
            st = new SpeakerState();
            speakerStates[speaker] = st;
        }
        return st;
    }

    // 남은 thinking 말풍선 정리 (에러/조기 종료 경로 포함)
    private void ClearThinkingBalloons(Dictionary<string, SpeakerState> speakerStates)
    {
        foreach (var st in speakerStates.Values)
        {
            if (st.thinkingBalloon != null)
            {
                Destroy(st.thinkingBalloon);
                st.thinkingBalloon = null;
            }
        }
    }

    // 잔여 TTS(합성/큐/재생)가 소진된 뒤 말풍선을 음성 종료 후 자연 닫힘으로 전환
    //  (즉시 닫으면 아직 합성 중인 문장이 말풍선 없이 재생됨 — 공개 API 폴링으로 유휴 대기)
    private async Task HideBalloonsWhenTtsIdleAsync(int ttsSession, Dictionary<string, GameObject> speakerObjects, string mainNickname, int token)
    {
        int waited = 0;       // 누적 대기 시간
        int stableCount = 0;  // 연속 유휴 확인 횟수

        while (waited < TtsIdleTimeoutMs)
        {
            await Task.Delay(TtsIdlePollMs);
            waited += TtsIdlePollMs;

            // 새 턴/새 세션이 시작됐으면 말풍선 소유권이 넘어감 — 정리 중단
            if (token != turnToken || ttsSession != TTSManager.Instance.GetSessionId()) return;

            bool mainBusy = VoiceManager.Instance != null && VoiceManager.Instance.IsPlaybackBusy();
            bool subBusy = SubVoiceManager.Instance != null && SubVoiceManager.Instance.IsAnyPlaying();
            if (mainBusy || subBusy)
            {
                stableCount = 0;
                continue;
            }

            // 합성 인플라이트(요청됨·아직 큐 미도착) 틈새 대비 — 연속 유휴 확인 후 정리
            stableCount += 1;
            if (stableCount >= TtsIdleStableCount) break;
        }

        if (token != turnToken) return;
        HideAllBalloonsAfterAudio(speakerObjects, mainNickname);
    }

    // 턴 종결 시 표시된 말풍선을 음성 종료 후 자연 닫힘으로 전환 (아로프라 방식 — 메인/서브 말풍선 각 1개)
    private void HideAllBalloonsAfterAudio(Dictionary<string, GameObject> speakerObjects, string mainNickname)
    {
        AnswerBalloonManager.Instance.HideAnswerBalloonAfterAudio();
        SubAnswerBalloonManager.Instance.HideAnswerBalloonAfterAudio();
    }

    // 비어있지 않은 문장만 공백으로 연결
    private string JoinNonEmpty(List<string> sentences)
    {
        var filtered = new List<string>();
        foreach (string s in sentences)
        {
            if (!string.IsNullOrEmpty(s)) filtered.Add(s);
        }
        return string.Join(" ", filtered);
    }

    // ui_language 기준 표시 텍스트 선택 (비어 있으면 ko → ja → en 순 폴백)
    private string PickDisplayText(string joinKo, string joinJa, string joinEn)
    {
        string uiLanguage = SettingManager.Instance.settings.ui_language ?? "ko";
        string primary = uiLanguage == "ja" ? joinJa : (uiLanguage == "en" ? joinEn : joinKo);
        if (!string.IsNullOrEmpty(primary)) return primary;
        if (!string.IsNullOrEmpty(joinKo)) return joinKo;
        if (!string.IsNullOrEmpty(joinJa)) return joinJa;
        return joinEn ?? "";
    }
}
