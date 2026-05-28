using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 중앙 Bus(미디에이터) 기반 멀티 대화 오케스트레이터.
// 이 매니저는 (From → To, Text) 형태의 메시지를 큐잉하고, 수신/응답생성/발화/후속 라우팅을 일관 제어합니다.
// LLM이 is_multichat=true일 때 다음 화자(next)를 스스로 판단하면, 그 결정을 그대로 따릅니다.
// 외부 시스템(STT/LLM/TTS/UI)은 AgentHooks를 통해 연결하며, 외부 라이브러리는 사용하지 않습니다.
public class MultiChatManager : MonoBehaviour
{
    // 대화 구성 타입. 활성 참여자 "집합"만 결정하며, 실제 라우팅은 항상 버스가 수행합니다.
    public enum MultiChatType { TeaParty, Shittim }

    // 참여자 식별자. 요청에 따라 Arona/Plana를 포함합니다. 필요 시 자유롭게 추가 가능합니다.
    public enum AgentId { Player, Mika, Nagisa, Seia, Arona, Plana }

    // 지향성 메시지. From이 To에게 Text를 보냅니다. Meta는 라우팅 힌트나 상황 파라미터를 담는 확장 슬롯입니다.
    // 메시지는 중앙 버스 큐에 쌓였다가 순차적으로 처리됩니다.
    public class DirectedMessage
    {
        public AgentId From;
        public AgentId To;
        public string Text;
        public Dictionary<string, object> Meta;

        public DirectedMessage(AgentId from, AgentId to, string text, Dictionary<string, object> meta = null)
        {
            From = from;
            To = to;
            Text = text;
            Meta = meta ?? new Dictionary<string, object>();
        }
    }

    // 한 턴의 처리 컨텍스트. OnReceive/GenerateDecision/Speak 등이 공유하는 정보 컨테이너입니다.
    // Meta에는 is_multichat, routeTo 등 정책/힌트를 넣어 전달합니다.
    public class TurnContext
    {
        public AgentId From;
        public AgentId To;
        public string Utterance;
        public Dictionary<string, object> Meta = new();
    }

    // LLM이 내려주는 의사결정 결과 컨테이너입니다.
    // Text는 실제 사용자에게 들려줄 대사, Next는 다음 화자 후보(없으면 버스 기본 규칙 적용)입니다.
    public class LlmDecision
    {
        public string Text;
        public AgentId? Next;
        public string Raw; // 디버깅용 원문 보관
    }

    // 각 에이전트(캐릭터/플레이어)에 연결되는 훅입니다.
    // 수신 표시, LLM 호출, 다음 대상 결정, 음성 재생, 사후처리까지의 진입점을 제공합니다.
    public class AgentHooks
    {
        // 수신 훅: 대화창/표정 등 UI 반영 지점입니다. 서버 호출이 필요 없다면 가볍게 끝낼 수 있습니다.
        // 긴 연산이 필요한 경우에도 여기서는 표시만 하고, 실제 생성은 GenerateDecision에서 수행하세요.
        public Action<TurnContext> OnReceive;

        // 발화 훅: TTS로 음성을 재생합니다. 재생 길이(초)를 반환해 인터럽트 제어에 사용합니다.
        // 실제 재생기는 이 반환값에 맞춰 조정하거나, 반대로 재생기에서 길이를 알려주도록 어댑터를 구성하세요.
        public Func<TurnContext, float> Speak;

        // 발화 종료 훅: 효과음/표정 복귀 등 후처리를 담당합니다. 실패해도 라우팅에는 영향을 주지 않습니다.
        public Action<TurnContext> OnAfterSpeak;

        // 권장 경로: LLM이 대사와 next를 함께 결정해 반환합니다(JSON 등 구조화 응답을 그대로 해석).
        // 서버 어댑터에서 HTTP 응답을 파싱해 LlmDecision으로 변환해 주입하는 방식이 안전합니다.
        public Func<TurnContext, LlmDecision> GenerateDecision;

        // 하위 호환: 문자열만 반환할 때 사용합니다. [NEXT:Name] 또는 간단 JSON에서 next를 추출합니다.
        // 프로젝트 이관 중이거나 신속한 임시 대응 시에만 사용하고, 장기적으로는 GenerateDecision으로 통합하세요.
        public Func<TurnContext, string> GenerateReply;

        // 자기 발화 인터럽트 금지 여부입니다. true면 자신의 발화는 본인으로 끊지 않습니다(유저 인터럽트 제외).
        public bool PreventSelfInterrupt = true;
    }

    [Header("Config")]
    [SerializeField] public MultiChatType multiChatType = MultiChatType.TeaParty;

    // 인터럽트 정책. 유저 인터럽트와 AI 인터럽트를 분리 제어합니다.
    // 유저가 말하면 즉시 끊길 수 있어야 하지만, AI끼리 서로 끊는 것은 기본적으로 금지하는 구성이 안전합니다.
    [Serializable]
    public class InterruptPolicy
    {
        public bool allowUserInterrupt = true;
        public bool allowAiInterrupt = false;
    }
    public InterruptPolicy interruptPolicy = new InterruptPolicy();

    // 레지스트리/활성 집합/버스 큐/실행 상태 등 내부 필드입니다.
    private readonly Dictionary<AgentId, AgentHooks> _hooks = new();    // 에이전트별 훅 저장소
    private readonly HashSet<AgentId> _active = new();                   // 현재 타입에서 활성화된 참여자 집합
    private readonly Queue<DirectedMessage> _busQueue = new();           // 중앙 버스 메시지 큐

    private Coroutine _busLoop;                                          // 버스 처리 코루틴(단일 인스턴스 보장)
    private bool _isProcessing;                                          // 버스 루프 동작 여부
    private bool _isSpeaking;                                            // 현재 발화 진행 중 여부
    private AgentId _currentSpeaker;                                     // 현재 발화자(디버깅/표시용)
    private bool _interruptRequested;                                     // 인터럽트 요청 플래그
    private AgentId? _forcedNext;                                        // 운영자 강제 라우팅(선택 기능)

    // 타입 구성 적용: 활성 참여자 집합을 재구성합니다.
    // 씬 시작 시 1회 호출하거나, 런타임 전환에도 사용할 수 있습니다(버스는 활성 범위를 넘어가지 않도록 보장).
    public void ConfigureByType(MultiChatType type)
    {
        // 타입 변경 시, 기존 세션을 유지할지 여부는 외부에서 결정하세요.
        // 이 함수는 단지 활성 집합만 갱신하며, 현재 큐나 루프는 건드리지 않습니다.
        multiChatType = type;
        _active.Clear();

        if (type == MultiChatType.TeaParty)
        {
            _active.Add(AgentId.Player);
            _active.Add(AgentId.Mika);
            _active.Add(AgentId.Nagisa);
            _active.Add(AgentId.Seia);
        }
        else // Shittim
        {
            _active.Add(AgentId.Player);
            _active.Add(AgentId.Arona);
            _active.Add(AgentId.Plana);
        }
    }

    // 에이전트 등록: 훅을 레지스트리에 기록합니다.
    // 참여 활성 여부는 타입 구성으로 따로 제어하므로, 훅은 선등록해 두었다가 나중에 활성화해도 됩니다.
    public void RegisterAgent(AgentId id, AgentHooks hooks)
    {
        // 중복 등록 시 마지막 등록이 덮어씁니다. 분할 초기화가 필요하면 별도 팩토리를 두세요.
        _hooks[id] = hooks;
    }

    // 메시지 전송: 중앙 버스에 (From → To, Text) 메시지를 적재합니다.
    // 발화 중 인터럽트 정책을 확인하여 즉시 끊을지, 큐 뒤에 쌓을지를 결정합니다.
    public void Send(AgentId from, AgentId to, string text, Dictionary<string, object> meta = null)
    {
        // 대상 유효성 검사: 타입 전환으로 비활성화되었거나 훅 미등록이면 무시합니다.
        if (!_active.Contains(to) || !_hooks.ContainsKey(to))
        {
            Debug.LogWarning($"[MultiChatManager] Invalid target: {to} (active:{_active.Contains(to)} reg:{_hooks.ContainsKey(to)})");
            return;
        }

        // 유저 인터럽트 허용: AI가 말하는 중이고 보낸이가 Player이며 정책이 허용되면 인터럽트를 요청합니다.
        // 실제 오디오 중지는 TTS 시스템에서 이 신호를 확인해 처리하도록 구현해야 합니다.
        if (_isSpeaking && from == AgentId.Player && interruptPolicy.allowUserInterrupt)
        {
            _interruptRequested = true;
            _busQueue.Enqueue(new DirectedMessage(from, to, text, meta)); // 인터럽트 후 즉시 처리되도록 큐에 적재
        }
        else if (_isSpeaking && from != AgentId.Player && !interruptPolicy.allowAiInterrupt)
        {
            // AI 인터럽트 금지 상태에서는 현재 발화를 존중하고 대기열 뒤에 적재합니다.
            _busQueue.Enqueue(new DirectedMessage(from, to, text, meta));
        }
        else
        {
            // 평상시에는 일반적으로 큐 뒤에 적재합니다. 필요 시 우선순위 큐로 확장할 수 있습니다.
            _busQueue.Enqueue(new DirectedMessage(from, to, text, meta));
        }

        // 버스 루프가 멈춰 있다면 즉시 가동합니다. 단일 코루틴만 동작하게 설계되어 있습니다.
        if (_busLoop == null) _busLoop = StartCoroutine(BusProcessLoop());
    }

    // 강제 라우팅 지정(선택 기능): 현재 턴 종료 직후, 다음 메시지의 To를 우선 적용합니다.
    // 운영자 UI, 演出 스크립트, QA 테스트 등에서 유용합니다.
    public void ForceNext(AgentId next)
    {
        // 활성 집합에 없는 대상이면 적용되지 않습니다(런타임에서 안전하게 무시).
        _forcedNext = next;
    }

    // 세션 정지: 버스 큐와 상태를 초기화합니다.
    // 실제 오디오 정지는 TTS 재생기에서 따로 처리해야 합니다(중간에 stop을 걸 수 있도록 설계하세요).
    public void StopAll()
    {
        if (_busLoop != null) StopCoroutine(_busLoop);
        _busLoop = null;

        _busQueue.Clear();
        _isProcessing = false;
        _isSpeaking = false;
        _interruptRequested = false;
        _forcedNext = null;
    }

    // 버스 루프: 중앙 큐에서 메시지를 하나씩 꺼내 순차 처리합니다.
    // 순서는 수신 표시 → LLM 생성/결정 → 발화(TTS) → 후속 라우팅 결정 → 다음 메시지 큐잉입니다.
    private IEnumerator BusProcessLoop()
    {
        // 중복 실행 방지. 한 번에 하나의 루프만 동작하도록 보장합니다.
        if (_isProcessing) yield break;
        _isProcessing = true;

        while (_busQueue.Count > 0)
        {
            var msg = _busQueue.Dequeue();

            // 타입 전환/훅 해제 등으로 대상이 무효화되었을 수 있으므로 재확인합니다.
            if (!_active.Contains(msg.To) || !_hooks.ContainsKey(msg.To))
                continue;

            var toHooks = _hooks[msg.To];

            // 컨텍스트 구성: is_multichat 플래그를 자동 세팅합니다(Shittim에서 Arona+Plana 동시 활성 기준).
            var ctxIn = new TurnContext { From = msg.From, To = msg.To, Utterance = msg.Text, Meta = msg.Meta ?? new Dictionary<string, object>() };
            bool isMultiChat = (multiChatType == MultiChatType.Shittim) && _active.Contains(AgentId.Arona) && _active.Contains(AgentId.Plana);
            ctxIn.Meta["is_multichat"] = isMultiChat;

            // 1) 수신 단계: 화면 표시, 표정/이모션, 로그 등을 즉시 반영합니다.
            // 무거운 연산은 여기서 하지 말고, GenerateDecision에서 수행하세요.
            toHooks?.OnReceive?.Invoke(ctxIn);

            // 2) 생성/결정 단계: LLM이 대사와 next를 함께 반환하는 경로를 우선 사용합니다.
            // 하위 호환으로 문자열만 반환할 경우 [NEXT:Name] 태그 또는 간단 JSON에서 next를 파싱합니다.
            LlmDecision decision = null;

            if (toHooks?.GenerateDecision != null)
            {
                decision = toHooks.GenerateDecision(ctxIn);
            }
            else if (toHooks?.GenerateReply != null)
            {
                string raw = toHooks.GenerateReply(ctxIn);
                AgentId? parsedNext; string cleaned;
                TryParseRoutingHint(raw, out parsedNext, out cleaned);
                decision = new LlmDecision { Raw = raw, Text = cleaned, Next = parsedNext };
            }
            else
            {
                // 훅이 전혀 없으면 에코처럼 흘려보냅니다(테스트/디버그용).
                decision = new LlmDecision { Text = msg.Text, Next = null, Raw = msg.Text };
            }

            string reply = decision.Text ?? msg.Text;

            // 3) 발화 단계: TTS가 길이(초)를 반환하면 그만큼 대기합니다.
            // 인터럽트 요청이 들어오면 즉시 빠져나와 다음 메시지로 진행하도록 합니다.
            var speakCtx = new TurnContext { From = msg.To, To = default, Utterance = reply, Meta = ctxIn.Meta };
            _currentSpeaker = msg.To;
            _isSpeaking = true;

            float dur = 0f;
            if (toHooks?.Speak != null)
                dur = toHooks.Speak(speakCtx);

            float t = 0f;
            while (t < dur)
            {
                if (_interruptRequested)
                    break;
                t += Time.deltaTime;
                yield return null;
            }

            _isSpeaking = false;
            _interruptRequested = false;
            toHooks?.OnAfterSpeak?.Invoke(speakCtx);

            // 4) 후속 라우팅: LLM next > 운영자 강제 > 버스 기본 규칙 순으로 결정합니다.
            // 기본 규칙은 "AI가 말했으면 Player에게, Player가 받았으면 보낸이에게"로 설정했습니다.
            AgentId? nextTarget = null;

            if (decision.Next.HasValue && _active.Contains(decision.Next.Value) && _hooks.ContainsKey(decision.Next.Value))
            {
                nextTarget = decision.Next.Value;
            }
            else if (_forcedNext.HasValue && _active.Contains(_forcedNext.Value))
            {
                nextTarget = _forcedNext.Value;
            }
            else
            {
                nextTarget = (msg.To == AgentId.Player) ? msg.From : AgentId.Player;
            }

            _forcedNext = null; // 강제 라우팅은 1회성입니다.

            // 5) 다음 메시지 큐잉: 동일 reply를 payload로 하여 To→nextTarget으로 전파합니다.
            // 필요 시 reply 대신 새로운 텍스트를 만들어 넣는 어댑터를 별도 구현하세요.
            if (nextTarget.HasValue && _active.Contains(nextTarget.Value) && _hooks.ContainsKey(nextTarget.Value))
            {
                _busQueue.Enqueue(new DirectedMessage(msg.To, nextTarget.Value, reply, speakCtx.Meta));
            }
        }

        _isProcessing = false;
        _busLoop = null;
    }

    // 라우팅 힌트 파서: LLM 문자열에서 next 후보를 추출합니다.
    // 지원 형식 1) "[NEXT:Plana] 대사..." 2) 간단 JSON {"text":"...", "next":"Arona"}
    // 외부 라이브러리를 쓰지 않기 위해 매우 단순한 문자열 파서로 구현했습니다(엄격한 JSON 파서는 아님).
    private void TryParseRoutingHint(string raw, out AgentId? next, out string cleaned)
    {
        next = null;
        cleaned = raw ?? "";

        if (string.IsNullOrEmpty(raw))
            return;

        // 형식 1: [NEXT:Name] 프리픽스 태그
        if (raw.StartsWith("[NEXT:", StringComparison.OrdinalIgnoreCase))
        {
            int close = raw.IndexOf(']');
            if (close > 6)
            {
                string name = raw.Substring(6, close - 6).Trim().TrimEnd(':').Trim();
                if (TryMapAgent(name, out var id)) next = id;

                cleaned = raw.Substring(close + 1).TrimStart();
                if (!string.IsNullOrEmpty(cleaned) && cleaned[0] == ':') cleaned = cleaned.Substring(1).TrimStart();
                return;
            }
        }

        // 형식 2: 매우 단순한 JSON 키값 추출(공백/순서 무관, 큰따옴표만 가정)
        int nIdx = raw.IndexOf("\"next\"", StringComparison.OrdinalIgnoreCase);
        if (nIdx >= 0)
        {
            int colon = raw.IndexOf(':', nIdx);
            int q1 = raw.IndexOf('"', colon + 1);
            int q2 = (q1 >= 0) ? raw.IndexOf('"', q1 + 1) : -1;
            if (q1 >= 0 && q2 > q1)
            {
                string name = raw.Substring(q1 + 1, q2 - q1 - 1).Trim();
                if (TryMapAgent(name, out var id)) next = id;
            }
        }

        int tIdx = raw.IndexOf("\"text\"", StringComparison.OrdinalIgnoreCase);
        if (tIdx >= 0)
        {
            int colon = raw.IndexOf(':', tIdx);
            int q1 = raw.IndexOf('"', colon + 1);
            int q2 = (q1 >= 0) ? raw.IndexOf('"', q1 + 1) : -1;
            if (q1 >= 0 && q2 > q1)
            {
                cleaned = raw.Substring(q1 + 1, q2 - q1 - 1);
                return;
            }
        }
        // 둘 다 실패하면 원문 전체를 텍스트로 사용합니다.
    }

    // 문자열을 AgentId로 매핑합니다. 오타 보정으로 "prana"를 "Plana"에 매핑합니다.
    // 추가 캐릭터가 생기면 여기만 보강하면 됩니다.
    private bool TryMapAgent(string name, out AgentId id)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "player": id = AgentId.Player; return true;
            case "mika":   id = AgentId.Mika;   return true;
            case "nagisa": id = AgentId.Nagisa; return true;
            case "seia":   id = AgentId.Seia;   return true;
            case "arona":  id = AgentId.Arona;  return true;
            case "plana":
            case "prana":  id = AgentId.Plana;  return true; // 오타 보정
            default: id = default; return false;
        }
    }

    // 샘플 초기화: 테스트용 훅들을 간단히 바인딩합니다.
    // 실제 프로젝트에서는 LLM/TTS/UI 어댑터로 교체하세요(서버 호출→LlmDecision 변환).
    public void InitializeSample(Action<string> logOut = null)
    {
        // 간단한 TTS 더미. 실제로는 Narration(sceneId, text) 등에서 길이를 받아 반환하도록 연결하세요.
        float TTS(string who, string text)
        {
            // 1자≈0.04초로 근사. 문장 길이에 따라 0.8~6초 범위로 제한합니다.
            return Mathf.Clamp(text.Length * 0.04f, 0.8f, 6f);
        }

        // 공용 훅 팩토리. GenerateDecision만 갈아 끼우면 나머지는 그대로 재사용됩니다.
        AgentHooks MakeLLM(string name, Func<TurnContext, LlmDecision> llm)
        {
            return new AgentHooks
            {
                GenerateDecision = llm,
                OnReceive = (ctx) => logOut?.Invoke($"[{name}::OnReceive] {ctx.From} → {ctx.To} : {ctx.Utterance}"),
                Speak = (ctx) =>
                {
                    float d = TTS(name, ctx.Utterance);
                    logOut?.Invoke($"[{name}::Speak] ~{Mathf.RoundToInt(d * 1000)} ms");
                    return d;
                },
                OnAfterSpeak = (ctx) => logOut?.Invoke($"[{name}::AfterSpeak]")
            };
        }

        // Player: LLM/발화 없음(버스 기본 규칙만 사용). 필요 시 STT 텍스트를 바로 Send로 쏘세요.
        _hooks[AgentId.Player] = new AgentHooks { };

        // Shittim용 예시: Arona와 Plana가 is_multichat=true일 때 next를 스스로 결정합니다.
        RegisterAgent(AgentId.Arona, MakeLLM("Arona", (ctx) =>
        {
            bool isMulti = ctx.Meta.TryGetValue("is_multichat", out var v) && v is bool b && b;
            if (isMulti && ctx.Utterance.Contains("플라나"))
                return new LlmDecision { Text = "플라나가 더 적합해 보여요.", Next = AgentId.Plana };
            return new LlmDecision { Text = "제 의견을 먼저 말씀드릴게요.", Next = AgentId.Player };
        }));

        RegisterAgent(AgentId.Plana, MakeLLM("Plana", (ctx) =>
        {
            bool isMulti = ctx.Meta.TryGetValue("is_multichat", out var v) && v is bool b && b;
            return new LlmDecision { Text = "데이터를 다시 분석해 봤어요.", Next = isMulti ? AgentId.Arona : AgentId.Player };
        }));

        // TeaParty용 간단 훅: 문자열만 반환하는 하위 호환 경로를 예시로 보여드립니다.
        _hooks[AgentId.Mika] = new AgentHooks
        {
            GenerateReply = (ctx) => "[NEXT:Player] 미카 답변입니다.",
            Speak = (ctx) => TTS("Mika", ctx.Utterance),
            OnReceive = (ctx) => logOut?.Invoke("[Mika::OnReceive]"),
            OnAfterSpeak = (ctx) => { }
        };
        _hooks[AgentId.Nagisa] = new AgentHooks
        {
            GenerateReply = (ctx) => "나기사 답변입니다.", // next 지정 없음 → 기본 규칙
            Speak = (ctx) => TTS("Nagisa", ctx.Utterance)
        };
        _hooks[AgentId.Seia] = new AgentHooks
        {
            GenerateReply = (ctx) => "{\"text\":\"세이아 답변입니다.\", \"next\":\"Player\"}",
            Speak = (ctx) => TTS("Seia", ctx.Utterance)
        };

        // 인스펙터의 현재 설정을 적용합니다.
        ConfigureByType(multiChatType);
    }

    // 테스트/툴용 헬퍼: Player가 특정 대상에게 한 줄 메시지를 보냅니다.
    // 버튼 이벤트나 단축키에서 간단히 연결하기 좋습니다.
    public void PlayerSayTo(AgentId to, string text, Dictionary<string, object> meta = null)
    {
        Send(AgentId.Player, to, text, meta);
    }

    // 외부 표시/디버깅용 상태 Getter입니다.
    public bool IsProcessing => _isProcessing;
    public bool IsSpeaking => _isSpeaking;
    public AgentId CurrentSpeaker => _currentSpeaker;
}
