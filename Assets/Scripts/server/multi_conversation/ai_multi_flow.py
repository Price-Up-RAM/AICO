'''
ai_multi_flow.py - 대화 흐름 관리 (ASIS3 ai_aropla_flow 기반 재구현)

다음 화자/청자 결정, AI 트리거 상황 판단 등 대화 흐름을 관리합니다.
'''
import time
from threading import Lock

from ai_singleton import get_llm
from ai_multi_llm import analyze_target_speaker, decide_flow, analyze_target_listener
from ai_multi_prompts import get_character_info, get_all_characters_info

generation_lock = Lock()

# AI 연속 대화 허용 최대 횟수 (기본값)
DEFAULT_MAX_AI_CONSECUTIVE = 10

def load_model(is_use_cuda=False):
    get_llm()


def get_character_info_text(lang='en'):
    '''아로나와 플라나의 상세한 캐릭터 정보 반환'''
    arona_info = get_character_info('arona', lang)
    plana_info = get_character_info('plana', lang)
    
    arona_name = arona_info.get('name', 'Arona') if arona_info else 'Arona'
    plana_name = plana_info.get('name', 'Plana') if plana_info else 'Plana'
    
    return f"""## {arona_name} 캐릭터 정보
{str(arona_info)}

## {plana_name} 캐릭터 정보
{str(plana_info)}"""

def analyze_target_speaker_from_message(message, current_speaker='sensei', lang='en',
                                       memory_multi=None, server_type='Local', api_key=None):
    '''사용자 메시지를 분석하여 누구에게 말하고 있는지 판단 (답변 전 - 메시지 대상 분석)'''
    start_time = time.time()
    memory_info = f" (메모리: {len(memory_multi) if memory_multi else 0}턴)" if memory_multi else ""
    print(f"[Target Analysis] 시작: '{message[:30]}...' ({lang}){memory_info}")
    
    try:
        result = analyze_target_speaker(
            message=message,
            memory_multi=memory_multi,
            lang=lang,
            server_type=server_type,
            api_key=api_key
        )
        
        target = result.get('target')
        reason = result.get('reason', 'AI 분석')
        
        total_time = time.time() - start_time
        print(f"[Target Analysis] 완료 ({total_time:.2f}s): {target} - {reason}")
        
        return target, f"AI 분석: {reason}"
    
    except Exception as e:
        print(f"[Target Analysis] 오류: {e}")
        return 'arona', 'AI 모델 오류 - 기본값 선택'


# 대화 흐름 결정 (다음 발화자 결정)
def process_flow_decision(memory_multi=None, query='', final_response='',
                         current_speaker=None, query_speaker=None, lang='en',
                         max_ai_consecutive=DEFAULT_MAX_AI_CONSECUTIVE, server_type='Local', api_key=None):
    '''대화 흐름을 분석하여 다음 발화자를 결정 (답변 후 - 다음 발화자 결정)'''
    start_time = time.time()
    memory_multi = memory_multi or []
    print(f"[Flow Decision] 시작: {len(memory_multi)}턴 분석 ({lang})")
    print(f"[Flow Decision] 현재 발화자: {current_speaker}")
    print(f"[Flow Decision] 쿼리: '{query}', 응답: '{final_response}'")
    
    try:
        result = decide_flow(
            memory_multi=memory_multi,
            query=query,
            final_response=final_response,
            current_speaker=current_speaker,
            query_speaker=query_speaker,
            lang=lang,
            server_type=server_type,
            api_key=api_key
        )
        
        next_speaker = result.get('next_speaker', 'sensei')
        reason = result.get('reason', 'AI 모델 결정')
        
        print(f"[Flow Decision] AI 추론 결과: {next_speaker}")
        
        # 현재 발화자와 동일한 발화자 선택 방지 로직
        if next_speaker == current_speaker:
            original = next_speaker
            next_speaker = 'sensei'
            reason = f"동일 발화자 방지: {original} → sensei 자동 변경"
            print(f"[Flow Decision] ⚠️ 동일 발화자 감지! '{original}' → 'sensei'로 변경")
        
        # AI 연속 대화 방지 설정
        if next_speaker != 'sensei':
            current_is_user = (query_speaker == 'sensei')
            
            if current_is_user:
                print(f"[Flow Decision] ✅ 현재 턴이 user 턴이므로 연속 대화 방지 불필요")
            elif len(memory_multi) >= max_ai_consecutive:
                recent_turns = memory_multi[-max_ai_consecutive:]
                
                # 같은 캐릭터가 연속으로 말하는지 확인
                speaker_sequence = []
                for entry in recent_turns:
                    if entry.get('role') == 'user':
                        speaker_sequence.append('sensei')
                    else:
                        char_name = entry.get('character_name', 'unknown')
                        speaker_sequence.append(char_name)
                
                # user가 한 번도 없고, 모두 같은 캐릭터인 경우만 방지
                has_user = 'sensei' in speaker_sequence
                all_same_character = len(set(speaker_sequence)) == 1 and not has_user
                
                if all_same_character:
                    original = next_speaker
                    next_speaker = 'sensei'
                    reason = f"AI 연속 방지: {original} → sensei 강제 변경 (같은 캐릭터 {max_ai_consecutive}턴 연속)"
                    print(f"[Flow Decision] 같은 캐릭터 연속 감지! '{original}' → 'sensei'로 변경")
                    print(f"[Flow Decision] 🔍 과거 {max_ai_consecutive}턴 speakers: {speaker_sequence}")
        
        total_time = time.time() - start_time
        print(f"[Flow Decision] ✅ 전체 완료 ({total_time:.2f}s): {next_speaker}")
        
        return next_speaker, reason
    
    except Exception as e:
        print(f"[Flow Decision] 오류: {e}")
        return 'sensei', 'AI 모델 오류 - 선생님께 턴 넘김'

# 청자 결정 (응답 대상 분석)
def analyze_target_listener_from_message(message, current_speaker='sensei', target_speaker=None,
                                        lang='en', memory_multi=None, server_type='Local', api_key=None):
    '''메시지 분석을 통해 target_speaker가 누구에게 응답해야 하는지 결정'''
    start_time = time.time()
    memory_info = f" (메모리: {len(memory_multi) if memory_multi else 0}턴)" if memory_multi else ""
    print(f"[Listener Analysis] 시작: {target_speaker} 응답 대상 분석 '{message[:30]}...' ({lang}){memory_info}")
    
    try:
        result = analyze_target_listener(
            message=message,
            current_speaker=current_speaker,
            target_speaker=target_speaker,
            memory_multi=memory_multi,
            lang=lang,
            server_type=server_type,
            api_key=api_key
        )
        
        target_listener = result.get('target_listener', 'all')
        reason = result.get('reason', 'AI 분석')
        
        total_time = time.time() - start_time
        print(f"[Listener Analysis] 완료 ({total_time:.2f}s): {target_listener} - {reason}")
        
        return target_listener, f"AI 분석: {reason}"
    
    except Exception as e:
        print(f"[Listener Analysis] 오류: {e}")
        return 'all', 'AI 모델 오류'


# 맥락(규칙) 기반 청자 결정
def determine_target_listener_from_context(current_speaker, target_speaker, message='',
                                          memory_multi=None, lang='en'):
    '''대화 맥락에서 청취자 결정 (발화자와 응답자 관계 기반)'''
    print(f"[Context Listener] 맥락 분석: {current_speaker} -> {target_speaker}")
    
    # 기본 규칙 기반 결정
    if current_speaker == 'sensei':
        # 선생님이 말할 때는 주로 특정 AI에게 말함
        if target_speaker in ['arona', 'plana']:
            return target_speaker, f"선생님 -> {target_speaker} 개별 대화"
        else:
            return 'all', "선생님의 전체 발언"
    
    elif current_speaker in ['arona', 'plana']:
        # AI가 말할 때
        if target_speaker == 'sensei':
            return 'sensei', f"{current_speaker} -> 선생님 개별 응답"
        elif target_speaker in ['arona', 'plana'] and target_speaker != current_speaker:
            return target_speaker, f"{current_speaker} -> {target_speaker} AI끼리 대화"
        else:
            return 'all', f"{current_speaker}의 전체 발언"
    
    # 기본값
    return 'all', "맥락 불분명 - 전체 대화로 설정"

# 메인 함수
def get_next_speaker_with_agent(query, current_speaker, participants, memory_multi=None,
                                context=None, lang='en', server_type='Local', api_key=None):
    '''AI Agent를 이용한 다음 발화자 결정 (ASIS3 호환 래퍼)'''
    total_start = time.time()
    print(f"\n[Speaker Agent] === 발화자 결정 프로세스 시작 ===")
    print(f"[Speaker Agent] 입력: '{query[:50]}...', 현재: {current_speaker} ({lang})")
    
    # AI 모델 로딩 확인
    model_start = time.time()
    try:
        from ai_singleton import get_llm
        llm = get_llm()
        if not llm:
            print(f"[Speaker Agent] AI 모델 로딩 중...")
            load_model(is_use_cuda=True)
    except:
        pass
    model_time = time.time() - model_start
    print(f"[Speaker Agent] AI 모델 준비 완료 ({model_time:.2f}s)")
    
    # 1단계: 메시지에서 명시적 타겟 분석
    print(f"\n[Speaker Agent] 1단계: 명시적 타겟 분석")
    stage1_start = time.time()
    target_speaker, reason = analyze_target_speaker_from_message(
        query, current_speaker, lang, memory_multi, server_type, api_key
    )
    stage1_time = time.time() - stage1_start
    print(f"[Speaker Agent] 1단계 완료 ({stage1_time:.2f}s): {target_speaker}")
    
    if target_speaker and target_speaker != 'both':
        # 명시적 타겟이 있으면 해당 캐릭터 선택
        total_time = time.time() - total_start
        print(f"[Speaker Agent] 명시적 타겟 발견! 총 소요시간: {total_time:.2f}s")
        return target_speaker, f"명시적 타겟: {reason}"
    
    # 2단계: 일반적인 대화 흐름 결정
    print(f"\n[Speaker Agent] 2단계: 대화 흐름 분석")
    stage2_start = time.time()
    memory_multi = memory_multi or []
    
    next_speaker, flow_reason = process_flow_decision(
        memory_multi=memory_multi, 
        query=query, 
        final_response='',
        current_speaker=current_speaker, 
        query_speaker=current_speaker,
        lang=lang,
        server_type=server_type, 
        api_key=api_key
    )
    stage2_time = time.time() - stage2_start
    print(f"[Speaker Agent] 2단계 완료 ({stage2_time:.2f}s): {next_speaker}")
    
    total_time = time.time() - total_start
    print(f"[Speaker Agent] === 발화자 결정 완료 === (총 {total_time:.2f}s)")
    print(f"[Speaker Agent] 최종 결과: {current_speaker} → {next_speaker}")
    
    return next_speaker, f"흐름 결정: {flow_reason}"


if __name__ == '__main__':
    print('=== ai_multi_flow 테스트 ===')
    
    # 테스트용 메모리 생성
    test_memory = [
        {
            'speaker': 'character',
            'character_name': 'arona',
            'message': '선생님, 안녕하세요!',
            'role': 'assistant',
            'messageKo': '선생님, 안녕하세요!',
            'messageJp': '先生、こんにちは！',
            'messageEn': 'Hello, Sensei!'
        },
        {
            'speaker': 'player',
            'message': '안녕, 아로나',
            'role': 'user',
            'messageKo': '안녕, 아로나',
            'messageJp': 'こんにちは、アロナ',
            'messageEn': 'Hello, Arona'
        }
    ]
    
    print('\n--- 명시적 타겟 분석 테스트 ---')
    target, reason = analyze_target_speaker_from_message(
        message='프라나는 어떻게 생각해?',
        current_speaker='sensei',
        lang='ko',
        memory_multi=test_memory
    )
    print(f'결과: {target} - {reason}')
    
    print('\n--- 대화 흐름 결정 테스트 ---')
    next_speaker, reason = process_flow_decision(
        memory_multi=test_memory,
        query='',
        final_response='네, 선생님! 도와드릴게요!',
        current_speaker='arona',
        query_speaker='sensei',
        lang='ko'
    )
    print(f'결과: {next_speaker} - {reason}')
    
    print('\n--- 청자 결정 테스트 ---')
    listener, reason = analyze_target_listener_from_message(
        message='프라나는 어떻게 생각해?',
        current_speaker='sensei',
        target_speaker='arona',
        lang='ko',
        memory_multi=test_memory
    )
    print(f'결과: {listener} - {reason}')
    
    print('\n--- 맥락 기반 청자 결정 테스트 ---')
    listener, reason = determine_target_listener_from_context(
        current_speaker='sensei',
        target_speaker='arona',
        lang='ko'
    )
    print(f'결과: {listener} - {reason}')
