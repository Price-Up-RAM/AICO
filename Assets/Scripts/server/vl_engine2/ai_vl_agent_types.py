'''
ai_vl_agent_types.py
VL 에이전트의 타입 정의
'''
from dataclasses import dataclass, field, asdict
from datetime import datetime


'''
ThinkEntry 예시:
{
    'idx': 1,                                    # 로그 인덱스
    'phase': 'plan',                             # 단계 (goal/signal/observe/plan/act/check/revise/wait)
    'content': '목표: 노란 버튼 클릭',              # 내용
    'timestamp': '2026-01-19T23:50:00.000000'    # 타임스탬프
}
'''
@dataclass
class ThinkEntry:
    idx: int = 0              # 로그 인덱스
    phase: str = ''           # 단계 (goal/signal/observe/plan/act/check/revise/wait)
    content: str = ''         # 내용
    timestamp: str = None     # 타임스탬프 (자동 생성)
    
    def __post_init__(self):
        if self.timestamp is None:
            self.timestamp = datetime.now().isoformat()
    
    def to_dict(self):
        return asdict(self)

def append_think_log(think_log, phase, content):
    '''
    think_log에 새 항목 추가
    
    Args:
        think_log: ThinkEntry 리스트
        phase: 단계
        content: 내용
    
    Returns:
        ThinkEntry: 추가된 항목
    '''
    think_log_idx = len(think_log) + 1
    entry = ThinkEntry(idx=think_log_idx, phase=phase, content=content)
    think_log.append(entry)
    return entry

def restore_think_log(think_log_data):
    result = []
    for entry in think_log_data or []:
        result.append(ThinkEntry(
            idx=entry.get('idx', 0),
            phase=entry.get('phase', 'unknown'),
            content=entry.get('content', ''),
            timestamp=entry.get('timestamp')
        ))
    return result


'''
AgentEvent 예시:
{
    'kind': 'act',                               # 이벤트 종류
    'message': '클릭 실행',                        # 메시지
    'think_log': [...],                          # ThinkEntry 리스트
    'data': {'x': 100, 'y': 200}                 # 추가 데이터
}
'''
@dataclass
class AgentEvent:
    kind: str = ''            # 이벤트 종류 (goal/observe/act/check/done/fail 등)
    message: str = ''         # 메시지
    think_log: list = None     # ThinkEntry 리스트
    data: dict = None          # 추가 데이터
    
    def __post_init__(self):
        if self.think_log is None:
            self.think_log = []
    
    def to_dict(self):
        return {
            'kind': self.kind,
            'message': self.message,
            'think_log': [entry.to_dict() for entry in self.think_log],
            'data': self.data or {}
        }

'''
AgentState 래퍼 클래스 (dict 기반)
동적 필드 추가를 위해 dict를 내부 저장소로 사용

AgentState 예시:
{
    'expected_state': None,      # 예상 시나리오 (identify 행위 우선순위 상)
    'remain_retry_count': 5,    # 남은 재시도 횟수
    'retry_interval': 2.0,       # observation 재시도 간격 (초)
    'identify_fail_count': 0     # 시나리오 식별 실패 카운트 (동적 추가)
}
'''
class AgentState:
    def __init__(self, **kwargs):
        self._state = {
            'expected_state': kwargs.get('expected_state'),
            'remain_retry_count': kwargs.get('remain_retry_count', 5),
            'retry_interval': kwargs.get('retry_interval', 2.0)
        }
        # 추가 필드 처리 (identify_fail_count 등)
        for key, value in kwargs.items():
            if key not in self._state:
                self._state[key] = value
    
    def to_dict(self):
        '''모든 필드를 dict로 반환 (동적 필드 포함)'''
        return dict(self._state)
    
    @classmethod
    def from_dict(cls, data):
        if not data:
            return cls()
        return cls(**data)


# AgentEvent kind 상수 (Unity에 스트리밍되는 이벤트 종류)
EVENT_KIND_GOAL = 'goal'                         # 목표/성공조건 설정 완료
EVENT_KIND_PLAN = 'plan'                         # 다음 행동 계획 수립
EVENT_KIND_OBSERVE = 'observe'                   # 화면 관찰/분석 및 새 화면 요청
EVENT_KIND_ACT = 'act'                           # 함수 실행 (grounding, click 등)
EVENT_KIND_CHECK = 'check'                       # 성공 조건 충족 여부 확인
EVENT_KIND_WAIT = 'wait'                         # 대기 상태 (프레임 요청 쿨다운 등)
EVENT_KIND_REVISE = 'revise'                     # 목표/전략 재수정
EVENT_KIND_DONE = 'done'                         # 작업 성공 완료
EVENT_KIND_FAIL = 'fail'                         # 작업 실패 (취소 포함)
EVENT_KIND_MAX_RETRY_REACHED = 'max_retry_reached'  # 최대 재검증 횟수 도달 (실패 아님, 종료)

# AgentEvent kind - Multi-Conversation 용
EVENT_KIND_MULTI_REPLY = 'multi_reply' # AI 캐릭터 응답
EVENT_KIND_WAITING_USER = 'waiting_user' # 사용자 입력 대기
EVENT_KIND_CONVERSATION_START = 'conversation_start' # 대화 시작
EVENT_KIND_CONVERSATION_END = 'conversation_end' # 대화 종료
EVENT_KIND_AI_TRIGGERED = 'ai_triggered' # AI 트리거 상황 (특정 조건에서 AI가 먼저 발화)
EVENT_KIND_ERROR = 'error' # 에러

# ThinkEntry phase 상수 (사고 로그의 단계 구분)
PHASE_GOAL = 'goal'         # 목표 설정 단계
PHASE_SIGNAL = 'signal'     # 성공 시그널 설정 단계
PHASE_OBSERVE = 'observe'   # 화면 관찰 단계
PHASE_PLAN = 'plan'         # 행동 계획 단계
PHASE_ACT = 'act'           # 행동 실행 단계
PHASE_CHECK = 'check'       # 성공 확인 단계
PHASE_REVISE = 'revise'     # 목표/전략 재수정 단계
PHASE_WAIT = 'wait'         # 대기 단계

# ThinkEntry phase 상수 - Multi-Conversation 용
PHASE_FLOW = 'flow' # 화자 결정 단계
PHASE_GENERATE = 'generate' # 대화 생성 단계
PHASE_INPUT = 'input' # 입력 처리 단계
PHASE_TRANSITION = 'transition' # 상태 전환 단계


if __name__ == '__main__':
    print('=== ai_vl_agent_types 테스트 ===')
    
    entry1 = ThinkEntry(idx=1, phase=PHASE_GOAL, content='목표: 노란 버튼 클릭')
    print(f'ThinkEntry: {entry1.to_dict()}')
    
    event = AgentEvent(
        kind=EVENT_KIND_GOAL,
        message='목표 설정 완료',
        think_log=[entry1],
        data={'target': 'yellow button'}
    )
    print(f'AgentEvent: {event.to_dict()}')
