'''
ai_vl_agent_types_addon.py
20 Questions Game용 EVENT_KIND 확장

기존 ai_vl_agent_types.py의 AgentEvent, ThinkEntry를 재사용하고
20Q 게임 전용 EVENT_KIND와 PHASE를 추가 정의합니다.
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# 기존 타입 재사용
from ai_vl_agent_types import (
    AgentEvent, ThinkEntry, AgentState,
    append_think_log, restore_think_log
)


# ============================================================================
# 20 Questions Game EVENT_KIND 확장
# ============================================================================

# 게임 시작
EVENT_KIND_GAME_START = 'game_start'

# 질문에 대한 답변 (예/아니오/모르겠어요)
EVENT_KIND_GAME_ANSWER = 'game_answer'

# AI의 추측 시도
EVENT_KIND_AI_GUESS = 'ai_guess'

# 추측 결과 (정답/오답)
EVENT_KIND_GUESS_RESULT = 'guess_result'

# 일상 대화 (게임 무관 발화)
EVENT_KIND_CASUAL_CHAT = 'casual_chat'

# 대기 상태 (사용자 입력 대기)
# waiting_for 종류: 'restart', 'continue_or_giveup', None (일반 질문 대기)
EVENT_KIND_WAITING_ANSWER = 'waiting_answer'

# 게임 종료 (정답 맞춤/포기/한도 도달)
EVENT_KIND_GAME_OVER = 'game_over'

# 규칙 안내 (질문 형식이 아닌 발화)
EVENT_KIND_GUIDE_QUESTION = 'guide_question'


# ============================================================================
# 20 Questions Game PHASE 확장
# ============================================================================

# 의도 분류 단계
PHASE_CLASSIFY = 'classify'

# 답변 생성 단계
PHASE_ANSWER = 'answer'

# 추측 판정 단계
PHASE_JUDGE = 'judge'

# 일상 대화 단계
PHASE_CHAT = 'chat'


# ============================================================================
# 20 Questions Game 전용 ContextData 구조
# ============================================================================

def create_20q_context_data(
    secret: str = None,
    theme_key: str = None,
    question_count: int = 0,
    max_questions: int = 20,
    waiting_for: str = None,
    game_status: str = 'playing',
    game_result: str = None,
    history_secret_list: list = None
) -> dict:
    '''
    20 Questions Game용 context_data 생성
    
    Args:
        secret: 비밀 정답
        theme_key: 테마 키 (animal, fruit 등)
        question_count: 현재 질문 횟수
        max_questions: 최대 질문 횟수
        waiting_for: 대기 상태 ('restart', 'continue_or_giveup', None)
        game_status: 게임 상태 ('game_start', 'playing', 'game_over')
        game_result: 게임 결과 (None, 'user_won', 'ai_won', 'user_gave_up', 'max_reached')
        history_secret_list: 이미 사용된 정답 리스트
    
    Returns:
        dict: context_data 구조
    '''
    return {
        'data_type': 'game_20q_question',
        'value': {
            'secret': secret,
            'theme_key': theme_key,
            'question_count': question_count,
            'max_questions': max_questions,
            'waiting_for': waiting_for,
            'game_status': game_status,
            'game_result': game_result,
            'history_secret_list': history_secret_list or []
        }
    }


def restore_20q_context(context_data: dict) -> dict:
    '''
    context_data에서 20Q 게임 상태 복원
    
    Args:
        context_data: 클라이언트에서 전달받은 context_data
    
    Returns:
        dict: value 딕셔너리 (secret, question_count 등)
    '''
    if not context_data:
        return {
            'secret': None,
            'theme_key': None,
            'question_count': 0,
            'max_questions': 20,
            'waiting_for': None,
            'game_status': 'playing',
            'game_result': None,
            'history_secret_list': []
        }
    
    # data_type 검증
    if context_data.get('data_type') != 'game_20q_question':
        return {
            'secret': None,
            'theme_key': None,
            'question_count': 0,
            'max_questions': 20,
            'waiting_for': None,
            'game_status': 'playing',
            'game_result': None,
            'history_secret_list': []
        }
    
    value = context_data.get('value', {})
    return {
        'secret': value.get('secret'),
        'theme_key': value.get('theme_key'),
        'question_count': value.get('question_count', 0),
        'max_questions': value.get('max_questions', 20),
        'waiting_for': value.get('waiting_for'),
        'game_status': value.get('game_status', 'playing'),
        'game_result': value.get('game_result'),
        'history_secret_list': value.get('history_secret_list', [])
    }


# ============================================================================
# 20 Questions Game 전용 AgentEvent 헬퍼
# ============================================================================

def create_game_event(
    kind: str,
    message: str,
    think_log: list = None,
    reply_list: list = None,
    context_data: dict = None,
    **kwargs
) -> AgentEvent:
    '''
    20Q 게임용 AgentEvent 생성 헬퍼
    
    Args:
        kind: 이벤트 종류 (EVENT_KIND_*)
        message: 메시지
        think_log: ThinkEntry 리스트
        reply_list: 다국어 답변 리스트 [{'answer_ko': ..., 'answer_jp': ..., 'answer_en': ...}]
        context_data: 게임 상태 context_data
        **kwargs: 추가 data 필드
    
    Returns:
        AgentEvent: 생성된 이벤트
    '''
    data = kwargs.copy()
    
    if reply_list:
        data['reply_list'] = reply_list
    
    if context_data:
        data['context_data'] = context_data
    
    return AgentEvent(
        kind=kind,
        message=message,
        think_log=think_log or [],
        data=data
    )


if __name__ == '__main__':
    print('=== ai_vl_agent_types_addon (20Q) 테스트 ===')
    
    # context_data 생성 테스트
    ctx = create_20q_context_data(
        secret='고양이',
        theme_key='animal',
        question_count=5,
        waiting_for=None
    )
    print(f'context_data: {ctx}')
    
    # 복원 테스트
    restored = restore_20q_context(ctx)
    print(f'restored: {restored}')
    
    # 이벤트 생성 테스트
    event = create_game_event(
        kind=EVENT_KIND_GAME_ANSWER,
        message='네, 맞아요!',
        reply_list=[{
            'answer_ko': '네, 맞아요!',
            'answer_jp': 'はい、そうです！',
            'answer_en': 'Yes, that\'s right!'
        }],
        context_data=ctx
    )
    print(f'event: {event.to_dict()}')
