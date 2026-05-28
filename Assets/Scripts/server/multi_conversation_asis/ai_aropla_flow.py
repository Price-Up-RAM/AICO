from threading import Lock
from typing import List, Dict, Tuple
import time

import state
from ai_singleton import check_llm, get_llm
import prompt_char

generation_lock = Lock()

def load_model(is_use_cuda=False):
    # 아로프라 흐름 결정을 위한 모델 로딩
    get_llm()

def get_character_info(lang='en'):
    """아로나와 프라나의 상세한 캐릭터 정보를 가져오기"""
    arona_info = prompt_char.get_char_info_from_json('arona', lang)
    plana_info = prompt_char.get_char_info_from_json('plana', lang)
    
    return f"""## 아로나 (Arona) 캐릭터 정보
{arona_info}

## 프라나 (Plana) 캐릭터 정보  
{plana_info}"""

def get_aropla_prompt(memory_list: List[Dict] = None, query: str = "", final_response: str = "", lang: str = 'en', current_speaker: str = None, query_speaker: str = None) -> str:
    """아로프라 다음 발화자 결정 프롬프트 - 완전한 대화 흐름 포함"""
    
    # 현재 발화자 결정 (파라미터 우선, 없으면 memory_list에서 추출)
    if not current_speaker and memory_list:
        current_speaker = memory_list[-1].get('speaker', 'unknown')
    
    # 쿼리 발화자 기본값 설정
    if not query_speaker:
        query_speaker = "sensei"  # 기본값
    
    # 최근 대화 내역 구성 (과거 메모리 + 현재 쿼리 + AI 응답)
    conversation_history = ""
    
    # 1. 과거 메모리 (최대 4턴)
    if memory_list:
        for turn in memory_list[-4:]:
            speaker = turn.get('speaker', 'unknown')
            
            # 언어별 메시지 선택
            message = ""
            if lang == 'ko':
                message = turn.get('messageKo') or turn.get('message', turn.get('content', ''))
            elif lang in ['ja', 'jp']:
                message = turn.get('messageJp') or turn.get('message', turn.get('content', ''))
            elif lang == 'en':
                message = turn.get('messageEn') or turn.get('message', turn.get('content', ''))
            else:
                message = turn.get('message', turn.get('content', ''))
            
            if message:
                conversation_history += f"{speaker}: {message}\n"
    
    # 2. 현재 쿼리 추가 (사용자 발언만 - AI끼리 대화 시에는 제외)
    if query and query_speaker not in ["arona", "plana"]:
        conversation_history += f"{query_speaker}: {query}\n"
    else:
        print("### query is None or query_speaker is arona or plana", query_speaker, ":", query)
    
    # 3. AI 응답 추가 (방금 생성된 응답)
    if final_response and current_speaker:
        conversation_history += f"{current_speaker}: {final_response}\n"
    
    # 언어별 프롬프트 구성
    if lang == 'ko':
        system_content = f"""3명이 참여하는 대화에서 다음에 말할 사람을 자연스럽게 결정해주세요.

참여자:
- sensei (선생님): 사용자
- arona (아로나): 활발하고 적극적인 AI  
- plana (프라나): 차분하고 신중한 AI

최근 대화:
{conversation_history.strip() if conversation_history.strip() else "(대화 시작)"}

위 대화 흐름과 문맥을 고려하여, 누가 다음에 말하는 것이 가장 자연스러울지 결정해주세요.
(방금 말한 {current_speaker}는 제외)

결과 형식:
next_speaker: [arona/plana/sensei]
reason: [간단한 이유]"""

    elif lang in ['ja', 'jp']:
        system_content = f"""3名で行う対話で次に話す人を自然に決めてください。

参加者:
- sensei (先生): ユーザー
- arona (アロナ): 活発で積極的なAI
- plana (プラナ): 落ち着いて慎重なAI

最近の対話:
{conversation_history.strip() if conversation_history.strip() else "(対話開始)"}

上記の会話の流れと文脈を考慮して、次に誰が話すのが最も自然か決めてください。
(直前に話した{current_speaker}は除外)

結果形式:
next_speaker: [arona/plana/sensei]
reason: [簡単な理由]"""

    else:  # English
        system_content = f"""Decide who should speak next naturally in this 3-person conversation.

Participants:
- sensei: User
- arona: Active and energetic AI
- plana: Calm and thoughtful AI

Recent conversation:
{conversation_history.strip() if conversation_history.strip() else "(Conversation start)"}

Based on the conversation flow and context above, decide who would most naturally speak next.
(Exclude {current_speaker} who just spoke)

Format:
next_speaker: [arona/plana/sensei]
reason: [brief reason]"""

    # 언어별 사용자 요청
    user_request = "결정해주세요." if lang == 'ko' else "決定してください。" if lang in ['ja', 'jp'] else "Decide please."
    
    prompt = f"""<|im_start|>system
{system_content}<|im_end|>
<|im_start|>user
{user_request} /no_think<|im_end|>
<|im_start|>assistant
next_speaker: """
    
    print(f'### aropla_flow_prompt', prompt)
    return prompt

def analyze_target_speaker_from_message(message: str, current_speaker: str = "sensei", lang: str = 'en', memory_list=None) -> Tuple[str, str]:
    """사용자 메시지를 분석하여 누구에게 말하고 있는지 판단 (답변 전 - 메시지 대상 분석)"""
    
    start_time = time.time()
    memory_info = f" (메모리: {len(memory_list) if memory_list else 0}턴)" if memory_list else ""
    print(f"[AI Agent - Target Analysis] 시작: '{message[:30]}...' ({lang}){memory_info}")
    
    # 간단한 메시지 처리 제거 - AI 모델 판단으로 일관성 유지
    
    llm = get_llm()
    if not llm:
        return None, "AI 모델이 로드되지 않음"
    
    char_info_start = time.time()
    character_info = get_character_info(lang)
    char_info_time = time.time() - char_info_start
    print(f"[AI Agent - Target Analysis] 📋 캐릭터 정보 로드 완료 ({char_info_time:.2f}s)")
    
    # 과거 대화 내용 처리 (prompt_llm 방식 참조)
    memory_context = ""
    if memory_list:
        memory_start = time.time()
        recent_memory = memory_list[-5:]  # 최근 5턴만 사용
        memory_lines = []
        
        for m in recent_memory:
            # 언어별 메시지 선택 로직
            selected_message = ""
            if lang == 'ko':
                selected_message = m.get('messageKo') or m.get('message', '')
            elif lang in ['ja', 'jp']:
                selected_message = m.get('messageJp') or m.get('message', '')
            elif lang == 'en':
                selected_message = m.get('messageEn') or m.get('message', '')
            else:
                selected_message = m.get('message', '')  # fallback
            
            if not selected_message:  # 빈 메시지는 스킵
                continue
                
            if m.get('role') == 'user':
                speaker_name = "선생님" if lang == 'ko' else ("先生" if lang in ['ja', 'jp'] else "Sensei")
                memory_lines.append(f"{speaker_name}: {selected_message}")
            # elif m.get('speaker') == 'character':
            else:
                char_speaker = m.get('speaker', 'character')  # 캐릭터명이 있으면 사용. 멀티는 speaker에 캐릭터 이름이 들어감.
                memory_lines.append(f"{char_speaker}: {selected_message}")
        
        if memory_lines:
            memory_header = "최근 대화:" if lang == 'ko' else ("最近の会話:" if lang in ['ja', 'jp'] else "Recent conversation:")
            memory_context = f"\n\n{memory_header}\n" + "\n".join(memory_lines)
        
        memory_time = time.time() - memory_start
        print(f"[AI Agent - Target Analysis] 과거 대화 로드 완료 ({len(recent_memory)}턴, {memory_time:.2f}s)")
    else:
        print(f"[AI Agent - Target Analysis] 과거 대화 없음 - 현재 메시지만 분석")
    
    # 언어별 프롬프트 - 메시지 대상 분석 (목적 명확화)
    if lang == 'ko':
        system_text = f"""사용자 메시지를 보고 누구에게 말하고 있는지 빠르게 판단하세요.

판단 기준:
- 특정 이름 호출: "아로나", "프라나" 등
- 특정 캐릭터 언급: "선배", "후배", "프라나쨩" 등  
- 성격 기반 요청: 활발한 것 → 아로나, 차분한 것 → 프라나
- 과거 대화 맥락: 최근에 누구와 대화했는지, 대화 흐름 고려
- 명확하지 않으면: arona (기본 선택)"""
        
        user_text = f"""{memory_context}

현재 메시지: "{message}"

과거 대화 맥락과 현재 메시지를 종합하여, 사용자가 누구에게 말하고 있나요?
target_speaker: [arona/plana]
reason: [짧은 이유]"""
        
    elif lang == 'ja' or lang == 'jp':
        system_text = f"""ユーザーメッセージを見て誰に話しかけているか素早く判断してください。

判断基準:
- 特定の名前呼び出し: "アロナ", "プラナ"など
- 特定キャラクター言及: "先輩", "後輩", "プラナちゃん"など
- 性格ベース依頼: 活発なもの → アロナ、落ち着いたもの → プラナ  
- 過去の会話文脈: 最近誰と話していたか、会話の流れを考慮
- 明確でなければ: arona (基本選択)"""
        
        user_text = f"""{memory_context}

現在のメッセージ: "{message}"

過去の会話文脈と現在のメッセージを総合して、ユーザーは誰に話しかけていますか？
target_speaker: [arona/plana]
reason: [短い理由]"""
        
    else:  # English
        system_text = f"""Analyze the user message to determine who they are addressing.

Judgment criteria:
- Specific name calls: "Arona", "Plana", etc.
- Character references: "senior", "junior", "Plana-chan", etc.
- Personality-based requests: energetic things → Arona, calm things → Plana
- Past conversation context: Consider who they've been talking to recently, conversation flow
- If unclear: arona (default choice)"""
        
        user_text = f"""{memory_context}

Current message: "{message}"

Based on past conversation context and current message, who is the user addressing?
target_speaker: [arona/plana]
reason: [brief reason]"""
        
        # AI 모델을 사용한 지능적 분석
    prompt_start = time.time()
    prompt = f"""<|im_start|>system
{system_text}<|im_end|>
<|im_start|>user
{user_text} /no_think<|im_end|>
<|im_start|>assistant
"""
    prompt_time = time.time() - prompt_start
    print(f"[AI Agent - Target Analysis] 프롬프트 생성 완료 ({prompt_time:.2f}s)")
    
    with generation_lock:
        ai_start = time.time()
        print(f"[AI Agent - Target Analysis] 🤖 AI 추론 시작...")
        
        state_config = {
            'max_new_tokens': 30,  # 대폭 단축: 간단한 결과만 필요
            'temperature': 0.1,    # 더 확정적으로
            'repetition_penalty': 1.1  # 반복 억제
        }
        
        # 스트리밍으로 변경하여 중간 과정 확인 (로그 최소화)
        output = ""
        for partial_output in llm.generate_with_streaming(prompt, state_config):
            output = partial_output
            # 로그 출력 최소화 - 최종 결과만
        
        ai_time = time.time() - ai_start
        print(f"[AI Agent - Target Analysis] 🤖 AI 추론 완료 ({ai_time:.2f}s)")
        
        # 결과 파싱
        parse_start = time.time()
        lines = output.strip().split('\n')
        target = "arona"  # 기본값 (명확하지 않으면 arona로 설정)
        reason = "AI 분석 결과"
        
        print(f"[AI Agent - Target Analysis] 🔍 파싱 시작...")
        for line in lines:
            if line.startswith("target_speaker:"):
                target = line.split(":", 1)[1].strip()
                print(f"[AI Agent - Target Analysis] 대상 발견: {target}")
            elif line.startswith("reason:"):
                reason = line.split(":", 1)[1].strip()
                print(f"[AI Agent - Target Analysis] 이유: {reason}")
        
        parse_time = time.time() - parse_start
        total_time = time.time() - start_time
        
        print(f"[AI Agent - Target Analysis] 🔍 파싱 완료 ({parse_time:.2f}s)")
        print(f"[AI Agent - Target Analysis] ✅ 전체 완료 ({total_time:.2f}s): {target}")
        
        # 항상 명확한 대상 반환 (arona 또는 plana)
        if target not in ["arona", "plana"]:
            target = "arona"  # 잘못된 응답이면 arona로 기본 설정
            reason = f"잘못된 응답으로 기본 선택 - {reason}"
        
        return target, f"AI 분석: {reason}"

def process_flow_decision(memory_list: List[Dict] = None, query: str = "", final_response: str = "", current_speaker: str = None, query_speaker: str = None, lang: str = 'en', max_ai_consecutive: int = 6) -> Tuple[str, str]:
    """대화 흐름을 분석하여 다음 발화자를 결정 (답변 후 - 다음 발화자 결정)"""
    
    start_time = time.time()
    memory_list = memory_list or []
    print(f"[AI Agent - Flow Decision] 시작: {len(memory_list)}턴 분석 ({lang})")
    print(f"[AI Agent - Flow Decision] 현재 발화자: {current_speaker}")
    print(f"[AI Agent - Flow Decision] 쿼리: '{query}', 응답: '{final_response}'")
    
    
    llm = get_llm()
    if not llm:
        print(f"[AI Agent - Flow Decision] ❌ AI 모델 미로드 - 기본값 반환")
        return "sensei", "AI 모델이 로드되지 않음 - 선생님께 턴 넘김"
    
    # AI 모델을 사용한 고급 결정
    with generation_lock:
        prompt_start = time.time()
        prompt = get_aropla_prompt(memory_list, query, final_response, lang, current_speaker, query_speaker)
        prompt_time = time.time() - prompt_start
        print(f"[AI Agent - Flow Decision] 프롬프트 생성 완료 ({prompt_time:.2f}s)")
        
        ai_start = time.time()
        print(f"[AI Agent - Flow Decision] 🤖 AI 추론 시작...")
        
        state_config = {
            'max_new_tokens': 50,   # 대폭 단축: 간단한 결과만 필요
            'temperature': 0.1,     # 더 확정적으로
            'repetition_penalty': 1.1  # 반복 억제
        }
        
        # 스트리밍으로 변경하여 중간 과정 확인 (로그 최소화)
        output = ""
        for partial_output in llm.generate_with_streaming(prompt, state_config):
            output = partial_output
            # 로그 출력 최소화 - 최종 결과만
        
        ai_time = time.time() - ai_start
        print(f"[AI Agent - Flow Decision] 🤖 AI 추론 완료 ({ai_time:.2f}s)")
        
        # 결과 파싱
        parse_start = time.time()
        lines = output.strip().split('\n')
        next_speaker = "sensei"  # 기본값을 sensei로 변경
        reason = "AI 모델 결정"
        
        print(f"[AI Agent - Flow Decision] 🔍 파싱 시작...")
        for line in lines:
            if line.startswith("next_speaker:"):
                next_speaker = line.split(":", 1)[1].strip()
                print(f"[AI Agent - Flow Decision] 다음 발화자: {next_speaker}")
            elif line.startswith("reason:"):
                reason = line.split(":", 1)[1].strip()
                print(f"[AI Agent - Flow Decision] 이유: {reason}")
        
        parse_time = time.time() - parse_start
        total_time = time.time() - start_time
        
        print(f"[AI Agent - Flow Decision] 🔍 파싱 완료 ({parse_time:.2f}s)")
        
        # 현재 발화자와 동일한 발화자 선택 방지 로직
        if next_speaker == current_speaker:
            original_speaker = next_speaker
            next_speaker = "sensei"
            reason = f"동일 발화자 방지: {original_speaker} → sensei 자동 변경"
            print(f"[AI Agent - Flow Decision] ⚠️ 동일 발화자 감지! '{original_speaker}' → 'sensei'로 변경")
            print(f"[AI Agent - Flow Decision] 📝 변경 이유: {reason}")
        
        # AI 연속 대화 방지 설정 (max_ai_consecutive회까지, AI Agent 결정 후 검증)
        # 1. 이번 next_speaker도 유저가 아님 (AI 선택됨)
        # 2. 실제 연속 AI 턴 수가 N회 이상
        if next_speaker != 'sensei':  # 이번에도 AI 선택
            
            # 현재 query_speaker가 user인지 확인 (현재 턴이 user 턴인지)
            current_is_user = (query_speaker == 'sensei')  # query_speaker가 sensei면 user 턴
            
            if current_is_user:
                # 현재 턴이 user 턴이면 연속 대화가 아니므로 방지 불필요
                print(f"[AI Agent - Flow Decision] ✅ 현재 턴이 user 턴이므로 연속 대화 방지 불필요")
            elif len(memory_list) >= max_ai_consecutive:  # 과거 기록이 충분한 경우에만 확인
                
                recent_turns = memory_list[-max_ai_consecutive:]  # 과거 N회 확인
                
                # 과거 N회의 role을 추출 (디버깅 및 관리 용이성을 위해 풀어서 작성)
                all_roles = []
                for entry in recent_turns:
                    role = entry.get('role')
                    all_roles.append(role)
                
                # 과거 N회가 전부 user가 아닌지 확인 (모든 턴이 assistant여야 함)
                all_non_user = True
                for role in all_roles:
                    if role == 'user':
                        all_non_user = False
                        break
                
                if all_non_user:  # 과거 N회 모두 non-user + 현재도 AI 선택 = 연속 대화
                    original_next = next_speaker
                    next_speaker = "sensei"
                    reason = f"AI 연속 방지: {original_next} → sensei 강제 변경 (AI {max_ai_consecutive}턴 연속)"
                    print(f"[AI Agent - Flow Decision] AI 연속 감지! '{original_next}' → 'sensei'로 변경")
                    print(f"[AI Agent - Flow Decision] 📝 연속 방지 이유: {reason}")
                    print(f"[AI Agent - Flow Decision] 🔍 과거 {max_ai_consecutive}턴 roles: {all_roles}")
        
        print(f"[AI Agent - Flow Decision] ✅ 전체 완료 ({total_time:.2f}s): {next_speaker}")
        
        return next_speaker, reason

def analyze_target_listener_from_message(message: str, current_speaker: str = "sensei", target_speaker: str = None, lang: str = 'en', memory_list=None) -> Tuple[str, str]:
    """메시지 분석을 통해 target_speaker가 누구에게 응답해야 하는지 결정"""
    
    start_time = time.time()
    memory_info = f" (메모리: {len(memory_list) if memory_list else 0}턴)" if memory_list else ""
    print(f"[AI Agent - Response Target] 시작: {target_speaker} 응답 대상 분석 '{message[:30]}...' ({lang}){memory_info}")
    
    llm = get_llm()
    if not llm:
        return "all", "AI 모델이 로드되지 않음"
    
    # 과거 대화 내용 처리
    memory_context = ""
    if memory_list:
        memory_start = time.time()
        recent_memory = memory_list[-5:]  # 최근 5턴만 사용
        memory_lines = []
        
        for m in recent_memory:
            # 언어별 메시지 선택 로직
            selected_message = ""
            if lang == 'ko':
                selected_message = m.get('messageKo') or m.get('message', '')
            elif lang in ['ja', 'jp']:
                selected_message = m.get('messageJp') or m.get('message', '')
            elif lang == 'en':
                selected_message = m.get('messageEn') or m.get('message', '')
            else:
                selected_message = m.get('message', '')  # fallback
            
            if not selected_message:  # 빈 메시지는 스킵
                continue
                
            if m.get('role') == 'user':
                speaker_name = "선생님" if lang == 'ko' else ("先生" if lang in ['ja', 'jp'] else "Sensei")
                memory_lines.append(f"{speaker_name}: {selected_message}")
            else:
                char_speaker = m.get('speaker', 'character')  # 캐릭터명 사용
                memory_lines.append(f"{char_speaker}: {selected_message}")
        
        if memory_lines:
            memory_header = "최근 대화:" if lang == 'ko' else ("最近の会話:" if lang in ['ja', 'jp'] else "Recent conversation:")
            memory_context = f"\n\n{memory_header}\n" + "\n".join(memory_lines)
        
        memory_time = time.time() - memory_start
        print(f"[AI Agent - Response Target] 과거 대화 로드 완료 ({len(recent_memory)}턴, {memory_time:.2f}s)")
    
    # target_speaker 정보 처리
    target_speaker = target_speaker or "unknown"
    
    # 언어별 프롬프트 - 응답 대상 분석 (target_speaker 관점)
    if lang == 'ko':
        system_text = f"""대화 상황을 분석하여 {target_speaker}가 응답할 때 누구에게 말해야 하는지 판단하세요.

상황 분석:
- {current_speaker}가 메시지를 말했습니다
- {target_speaker}가 응답할 예정입니다
- {target_speaker}는 누구에게 응답해야 할까요?

판단 기준:
- 개별 대화: {current_speaker}가 {target_speaker}에게 직접 말했다면 → {current_speaker}에게 응답
- 간접 질문: "{target_speaker}야, 프라나는 어떻게 생각해?" → 프라나에게 질문하도록 유도
- 전체 질문: 모든 사람이 들어도 되는 일반적 내용 → all (전체)
- 불분명한 경우: all (전체) 선택"""
        
        user_text = f"""{memory_context}

현재 상황:
- 발화자: {current_speaker}
- 응답자: {target_speaker} (응답할 예정)
- 메시지: "{message}"

{target_speaker}가 응답할 때 누구에게 말해야 하나요?
target_listener: [sensei/arona/plana/all]
reason: [짧은 이유]"""
        
    elif lang == 'ja' or lang == 'jp':
        system_text = f"""会話状況を分析して{target_speaker}が応答する時に誰に話すべきかを判断してください。

状況分析:
- {current_speaker}がメッセージを話しました
- {target_speaker}が応答する予定です
- {target_speaker}は誰に応答すべきでしょうか？

判断基準:
- 個別会話: {current_speaker}が{target_speaker}に直接話した場合 → {current_speaker}に応答
- 間接質問: "{target_speaker}、プラナはどう思う？" → プラナに質問するよう誘導
- 全体質問: 皆が聞いても良い一般的内容 → all (全体)
- 不明な場合: all (全体) 選択"""
        
        user_text = f"""{memory_context}

現在の状況:
- 発話者: {current_speaker}
- 応答者: {target_speaker} (応答予定)
- メッセージ: "{message}"

{target_speaker}が応答する時に誰に話すべきですか？
target_listener: [sensei/arona/plana/all]
reason: [短い理由]"""
        
    else:  # English
        system_text = f"""Analyze the conversation situation to determine who {target_speaker} should respond to.

Situation Analysis:
- {current_speaker} spoke the message
- {target_speaker} will respond
- Who should {target_speaker} respond to?

Judgment criteria:
- Individual conversation: If {current_speaker} spoke directly to {target_speaker} → respond to {current_speaker}
- Indirect question: "{target_speaker}, what does Plana think?" → guide to ask Plana
- General question: General content everyone can hear → all (everyone)
- Unclear cases: all (everyone)"""
        
        user_text = f"""{memory_context}

Current situation:
- Speaker: {current_speaker}
- Responder: {target_speaker} (will respond)
- Message: "{message}"

Who should {target_speaker} respond to?
target_listener: [sensei/arona/plana/all]
reason: [brief reason]"""
    
    # AI 모델을 사용한 분석
    prompt_start = time.time()
    prompt = f"""<|im_start|>system
{system_text}<|im_end|>
<|im_start|>user
{user_text} /no_think<|im_end|>
<|im_start|>assistant
"""
    prompt_time = time.time() - prompt_start
    print(f"[AI Agent - Response Target] 프롬프트 생성 완료 ({prompt_time:.2f}s)")
    
    with generation_lock:
        ai_start = time.time()
        print(f"[AI Agent - Response Target] 🤖 AI 추론 시작...")
        
        state_config = {
            'max_new_tokens': 30,
            'temperature': 0.1,
            'repetition_penalty': 1.1
        }
        
        output = ""
        for partial_output in llm.generate_with_streaming(prompt, state_config):
            output = partial_output
        
        ai_time = time.time() - ai_start
        print(f"[AI Agent - Response Target] 🤖 AI 추론 완료 ({ai_time:.2f}s)")
        
        # 결과 파싱
        parse_start = time.time()
        lines = output.strip().split('\n')
        target_listener = "all"  # 기본값 (전체)
        reason = "AI 분석 결과"
        
        print(f"[AI Agent - Response Target] 🔍 파싱 시작...")
        for line in lines:
            if line.startswith("target_listener:"):
                target_listener = line.split(":", 1)[1].strip()
                print(f"[AI Agent - Response Target] 응답 대상 발견: {target_listener}")
            elif line.startswith("reason:"):
                reason = line.split(":", 1)[1].strip()
                print(f"[AI Agent - Response Target] 이유: {reason}")
        
        parse_time = time.time() - parse_start
        total_time = time.time() - start_time
        
        print(f"[AI Agent - Response Target] 🔍 파싱 완료 ({parse_time:.2f}s)")
        print(f"[AI Agent - Response Target] ✅ 전체 완료 ({total_time:.2f}s): {target_listener}")
        
        # 유효한 청취자만 반환
        valid_listeners = ["sensei", "arona", "plana", "all"]
        if target_listener not in valid_listeners:
            target_listener = "all"  # 잘못된 응답이면 전체로 기본 설정
            reason = f"잘못된 응답으로 기본 선택 - {reason}"
        
        return target_listener, f"AI 분석: {reason}"

def determine_target_listener_from_context(
    current_speaker: str, 
    target_speaker: str, 
    message: str = "",
    memory_list: List[Dict] = None, 
    lang: str = 'en'
) -> Tuple[str, str]:
    """대화 맥락에서 청취자 결정 (발화자와 응답자 관계 기반)"""
    
    print(f"[AI Agent - Context Listener] 맥락 분석: {current_speaker} -> {target_speaker}")
    
    # 기본 규칙 기반 결정
    if current_speaker == "sensei":
        # 선생님이 말할 때는 주로 특정 AI에게 말함 (명시적 target_speaker 있으면 그대로)
        if target_speaker in ["arona", "plana"]:
            return target_speaker, f"선생님 -> {target_speaker} 개별 대화"
        else:
            return "all", "선생님의 전체 발언"
    
    elif current_speaker in ["arona", "plana"]:
        # AI가 말할 때
        if target_speaker == "sensei":
            return "sensei", f"{current_speaker} -> 선생님 개별 응답"
        elif target_speaker in ["arona", "plana"] and target_speaker != current_speaker:
            return target_speaker, f"{current_speaker} -> {target_speaker} AI끼리 대화"
        else:
            return "all", f"{current_speaker}의 전체 발언"
    
    # 기본값
    return "all", "맥락 불분명 - 전체 대화로 설정"

if __name__ == "__main__":
    # 테스트용 대화 기록 (다국어)
    test_conversations = {
        'ko': [
            {"speaker": "sensei", "content": "안녕하세요, 오늘 날씨가 좋네요"},
            {"speaker": "arona", "content": "네! 선생님! 정말 좋은 날씨에요~"},
            {"speaker": "plana", "content": "맑은 날씨가 기분을 좋게 만드는군요."},
            {"speaker": "arona", "content": "그러면 오늘은 뭐할까요 프라나쨩?"}
        ],
        'ja': [
            {"speaker": "sensei", "content": "こんにちは、今日はいい天気ですね"},
            {"speaker": "arona", "content": "はい！先生！本当にいい天気です〜"},
            {"speaker": "plana", "content": "晴れた天気は気分を良くしてくれますね。"},
            {"speaker": "arona", "content": "それじゃあ今日は何しましょうかプラナちゃん？"}
        ],
        'en': [
            {"speaker": "sensei", "content": "Hello, it's such a nice weather today"},
            {"speaker": "arona", "content": "Yes! Sensei! It's really beautiful weather~"},
            {"speaker": "plana", "content": "Clear weather does lift one's spirits."},
            {"speaker": "arona", "content": "So what should we do today, Plana-chan?"}
        ]
    }
    
    # 메시지 분석 테스트 (다국어)
    test_messages = {
        'ko': [
            "차분한 의견이 듣고 싶어",
            "신나는 놀이 할까?", 
            "논리적으로 설명해줘",
            "재미있는 이야기 해줘",
            "프라나쨩 아로나를 어떻게 생각해?",
            "아로나? 프라나쨩은 뭐하고 있어?",
            "결국 선배인 너가 이겼구나..."
        ],
        'ja': [
            "落ち着いた意見が聞きたいな",
            "楽しい遊びをしない？",
            "論理的に説明してくれる？",
            "面白い話をして",
            "プラナちゃん、アロナをどう思ってる？",
            "アロナ？プラナちゃんは何してるの？",
            "結局先輩のあなたが勝ったのね..."
        ],
        'en': [
            "I'd like to hear a calm opinion",
            "Want to play something fun?",
            "Can you explain it logically?", 
            "Tell me an interesting story",
            "Plana-chan, what do you think of Arona?",
            "Arona? What is Plana-chan doing?",
            "In the end, you, the senior, won..."
        ]
    }
    
    # AI 모델 테스트 (모델이 로드된 경우)
    if True:  # 필요시 True로 변경
        # state.set_use_gpu_percent(8)
        # state.model_name = 'Qwen2.5-14B-Instruct-1M-Q4_K_M.gguf'
        load_model(is_use_cuda=True)
        
        # 대화 흐름 결정 테스트 (다국어)
        for lang in ['ko', 'ja', 'jp', 'en']:  # ja와 jp 둘 다 테스트
            lang_key = 'ja' if lang == 'jp' else lang  # jp는 ja 데이터 사용
            
            # test_conversations를 memory_list 형식에 맞게 변환
            memory_format = []
            for entry in test_conversations[lang_key]:
                memory_format.append({
                    "speaker": entry["speaker"],
                    "message": entry["content"]
                })
            
            print(f"\n=== {lang.upper()} 흐름 결정 테스트 ===")
            test_query = "안녕하세요" if lang == 'ko' else "こんにちは" if lang in ['ja', 'jp'] else "Hello"
            test_response = "반가워요!" if lang == 'ko' else "嬉しいです！" if lang in ['ja', 'jp'] else "Nice to meet you!"
            
            next_speaker, reason = process_flow_decision(
                memory_format, 
                query=test_query,
                final_response=test_response,
                current_speaker="arona",
                lang=lang
            )
            print(f"AI 흐름 결정 ({lang}) - 다음 발화자: {next_speaker}")
            print(f"AI 흐름 결정 ({lang}) - 이유: {reason}")
        
        # 메시지 분석 테스트 (다국어) - 메모리 있는 경우와 없는 경우 테스트
        print(f"\n=== 메시지 분석 테스트 ===")
        
        # 테스트용 메모리 생성 (prompt_llm 방식 참조)
        test_memory = [
            {'speaker': 'character', 'character_name': 'arona', 'message': '선생님, 안녕하세요! 오늘 날씨가 좋네요~'},
            {'speaker': 'player', 'message': '아로나, 너 정말 활발하구나'},
            {'speaker': 'character', 'character_name': 'arona', 'message': '헤헤, 맞아요! 저는 항상 밝고 활기찬 편이에요!'},
            {'speaker': 'character', 'character_name': 'plana', 'message': '선생님, 프라나도 여기 있어요. 조용히 지켜보고 있었습니다.'},
            {'speaker': 'player', 'message': '프라나는 정말 차분하네'}
        ]
        
        for lang in ['ko', 'ja', 'jp', 'en']:  # ja와 jp 둘 다 테스트
            lang_key = 'ja' if lang == 'jp' else lang  # jp는 ja 데이터 사용
            print(f"\n--- {lang.upper()} 메시지 분석 (메모리 없음) ---")
            for msg in test_messages[lang_key][:2]:  # 처음 2개만
                target, reasoning = analyze_target_speaker_from_message(msg, "sensei", lang=lang, memory_list=None)
                print(f"메시지 분석 ({lang}) - '{msg}' -> {target}")
                print(f"  이유: {reasoning}")
                
            print(f"\n--- {lang.upper()} 메시지 분석 (메모리 있음) ---")  
            for msg in test_messages[lang_key][:2]:  # 처음 2개만
                target, reasoning = analyze_target_speaker_from_message(msg, "sensei", lang=lang, memory_list=test_memory)
                print(f"메시지 분석 ({lang}) - '{msg}' -> {target}")
                print(f"  이유: {reasoning}")
        
        import ai_singleton
        ai_singleton.release()
    else:
        print("AI 모델이 로드되지 않음 - 테스트 건너뜀")
