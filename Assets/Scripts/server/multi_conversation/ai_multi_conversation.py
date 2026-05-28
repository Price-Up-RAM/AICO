'''
ai_multi_conversation.py
Multi-Conversation 메인 루프 (AgentEvent 기반)

Stateless 구조로 context_data를 통해 대화 상태를 관리합니다.
다중 AI 캐릭터 대화를 지원합니다.
'''
# 타입 및 이벤트 상수
from ai_vl_agent_types import (
    AgentEvent, ThinkEntry, append_think_log,
    EVENT_KIND_MULTI_REPLY, EVENT_KIND_WAITING_USER,
    EVENT_KIND_CONVERSATION_START, EVENT_KIND_CONVERSATION_END,
    EVENT_KIND_AI_TRIGGERED, EVENT_KIND_ERROR,
    PHASE_FLOW, PHASE_GENERATE, PHASE_INPUT, PHASE_TRANSITION
)

# LLM 호출
from ai_multi_llm import (
    generate_reply,
    generate_greeting
)

# 프롬프트, 캐릭터 정보, 표시 이름
from ai_multi_prompts import (
    get_participant_display_name,
    SUPPORTED_CHARACTERS,
    get_character_name
)

# Flow 분석 함수들
from ai_multi_flow import (
    get_next_speaker_with_agent,
    process_flow_decision,
    analyze_target_speaker_from_message
)


# ============================================================================
# 메인 대화 루프
# ============================================================================

def conversation_run_loop(query, participants=None, lang='ko',
                          server_type='Local', api_key=None, history=None,
                          memory_multi=None, ai_trigger_situation=None):
    '''Multi-Conversation 메인 루프 - AgentEvent 생성'''
    think_log = []
    history = history or []
    memory_multi = memory_multi or []
    participants = participants or ['sensei', 'arona', 'plana']
    
    append_think_log(think_log, PHASE_INPUT, f'대화 시작: participants={participants}')
    
    # 2. AI 트리거 상황 처리
    if ai_trigger_situation:
        yield from handle_ai_trigger(
            think_log, lang, server_type, api_key,
            participants, history, ai_trigger_situation,
            memory_multi
        )
        return
    
    # 3. 대화 시작 (첫 AI 인사)
    if not query and not history:
        yield from handle_conversation_start(
            think_log, lang, server_type, api_key,
            participants, history
        )
        return
    
    # 4. 빈 쿼리 처리
    if not query or not query.strip():
        yield AgentEvent(
            kind=EVENT_KIND_WAITING_USER,
            message='대기 중...',
            think_log=think_log,
            data={}
        )
        return
    
    # 5. 사용자 입력 처리
    append_think_log(think_log, PHASE_INPUT, f'사용자 입력: "{query[:30]}..."')
    
    # 히스토리에 사용자 발화 추가
    history.append({
        'speaker': 'sensei',
        'listener': None,
        'content': query
    })
    
    # 6. 다음 화자 결정
    next_speaker, reason = get_next_speaker_with_agent(
        query=query,
        current_speaker='sensei',
        participants=participants,
        memory_multi=memory_multi,
        context=None,
        lang=lang,
        server_type=server_type,
        api_key=api_key
    )
    
    append_think_log(think_log, PHASE_FLOW, f'다음 화자: {next_speaker} ({reason})')
    
    # sensei 턴이면 대기 상태
    if next_speaker.lower() == 'sensei':
        yield AgentEvent(
            kind=EVENT_KIND_WAITING_USER,
            message='입력을 기다리는 중입니다.',
            think_log=think_log,
            data={}
        )
        return
    
    # 7. AI 응답 생성
    yield from handle_ai_response(
        think_log, lang, server_type, api_key,
        participants, history, query,
        next_speaker, memory_multi
    )


# ============================================================================
# 핸들러 함수들
# ============================================================================

def handle_conversation_start(think_log, lang, server_type,
                              api_key, participants, history):
    '''대화 시작 처리'''
    append_think_log(think_log, PHASE_TRANSITION, '대화 시작')
    
    # 첫 AI 캐릭터가 인사
    first_ai = None
    for p in participants:
        if p.lower() != 'sensei':
            first_ai = p
            break
    
    if not first_ai:
        first_ai = 'arona'
    
    # 인사말 생성
    greeting = generate_greeting(
        char_name=first_ai,
        situation='대화 시작',
        lang=lang,
        server_type=server_type,
        api_key=api_key
    )
    
    char_display = get_participant_display_name(first_ai, lang)
    
    reply_list = [{
        'answer_ko': greeting if lang == 'ko' else greeting,
        'answer_jp': greeting if lang in ['ja', 'jp'] else greeting,
        'answer_en': greeting if lang == 'en' else greeting
    }]
    
    yield AgentEvent(
        kind=EVENT_KIND_CONVERSATION_START,
        message=greeting,
        think_log=think_log,
        data={
            'speaker': first_ai,
            'listener': 'sensei',
            'reply_list': reply_list,
            'char_display': char_display
        }
    )
    
    # Unity가 기대하는 final 이벤트 전송
    yield AgentEvent(
        kind=EVENT_KIND_WAITING_USER,
        message='대화 시작 완료',
        think_log=think_log,
        data={
            'speaker': first_ai,
            'next_speaker': 'sensei',
            'reasoning': '대화 시작'
        }
    )


def handle_ai_trigger(think_log, lang, server_type,
                      api_key, participants, history,
                      situation, memory_multi):
    '''AI 트리거 상황 처리'''
    append_think_log(think_log, PHASE_TRANSITION, f'AI 트리거: {situation[:30]}...')
    
    # AI 트리거 판단
    from ai_multi_llm import check_ai_trigger
    trigger_result = check_ai_trigger(
        situation=situation,
        history=history,
        lang=lang,
        server_type=server_type,
        api_key=api_key
    )
    
    if not trigger_result.get('trigger'):
        # 트리거 조건 미충족
        yield AgentEvent(
            kind=EVENT_KIND_WAITING_USER,
            message='대기 중...',
            think_log=think_log,
            data={}
        )
        return
    
    # AI 선발화
    speaker = trigger_result.get('speaker') or participants[0]
    if speaker.lower() == 'sensei':
        speaker = participants[1] if len(participants) > 1 else 'arona'
    
    append_think_log(think_log, PHASE_GENERATE, f'AI 선발화: {speaker}')
    
    # 인사/발화 생성
    greeting = generate_greeting(
        char_name=speaker,
        situation=situation,
        lang=lang,
        server_type=server_type,
        api_key=api_key
    )
    
    char_display = get_participant_display_name(speaker, lang)
    
    reply_list = [{
        'answer_ko': greeting if lang == 'ko' else greeting,
        'answer_jp': greeting if lang in ['ja', 'jp'] else greeting,
        'answer_en': greeting if lang == 'en' else greeting
    }]
    
    yield AgentEvent(
        kind=EVENT_KIND_AI_TRIGGERED,
        message=greeting,
        think_log=think_log,
        data={
            'speaker': speaker,
            'listener': 'sensei',
            'reply_list': reply_list,
            'char_display': char_display,
            'trigger_reason': trigger_result.get('reason', '')
        }
    )
    
    # Unity가 기대하는 final 이벤트 전송
    yield AgentEvent(
        kind=EVENT_KIND_WAITING_USER,
        message='AI 트리거 완료',
        think_log=think_log,
        data={
            'speaker': speaker,
            'next_speaker': 'sensei',
            'reasoning': f"AI 트리거: {trigger_result.get('reason', '')}"
        }
    )


def handle_ai_response(think_log, lang, server_type,
                       api_key, participants, history,
                       last_utterance, speaker, memory_multi):
    '''AI 응답 생성'''
    append_think_log(think_log, PHASE_GENERATE, f'AI 응답 생성: {speaker}')
    
    # 응답 생성
    response = generate_reply(
        char_name=speaker,
        speaker='sensei',
        listener=speaker,
        participants=participants,
        history=history,
        last_utterance=last_utterance,
        lang=lang,
        server_type=server_type,
        api_key=api_key
    )
    
    char_display = get_participant_display_name(speaker, lang)
    
    # 히스토리에 AI 발화 추가
    history.append({
        'speaker': speaker,
        'listener': 'sensei',
        'content': response
    })
    
    reply_list = [{
        'answer_ko': response if lang == 'ko' else response,
        'answer_jp': response if lang in ['ja', 'jp'] else response,
        'answer_en': response if lang == 'en' else response
    }]
    
    # 다음 화자 결정 (연속 AI 대화 지원)
    from ai_multi_flow import DEFAULT_MAX_AI_CONSECUTIVE
    next_speaker, reason = process_flow_decision(
        memory_multi=memory_multi,
        query='',
        final_response=response,
        current_speaker=speaker,
        query_speaker='sensei',
        lang=lang,
        max_ai_consecutive=DEFAULT_MAX_AI_CONSECUTIVE,
        server_type=server_type,
        api_key=api_key
    )
    
    # AI 응답 이벤트 반환
    yield AgentEvent(
        kind=EVENT_KIND_MULTI_REPLY,
        message=response,
        think_log=think_log,
        data={
            'speaker': speaker,
            'listener': 'sensei',
            'reply_list': reply_list,
            'char_display': char_display,
            'next_speaker': next_speaker
        }
    )
    
    # Unity가 기대하는 final 이벤트 전송
    yield AgentEvent(
        kind=EVENT_KIND_WAITING_USER,
        message='대화 완료',
        think_log=think_log,
        data={
            'speaker': speaker,
            'next_speaker': next_speaker,
            'reasoning': reason
        }
    )


if __name__ == '__main__':
    print('=== ai_multi_conversation 테스트 ===')
    print('실제 테스트는 서버에서 실행하세요.')
