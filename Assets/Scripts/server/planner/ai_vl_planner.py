'''
ai_vl_planner.py
VL Planner 에이전트 루프 (범용 Stateless 버전)
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import time

from ai_vl_agent_types import (
    ThinkEntry, AgentEvent,
    EVENT_KIND_GOAL, EVENT_KIND_OBSERVE, EVENT_KIND_PLAN, EVENT_KIND_ACT,
    EVENT_KIND_CHECK, EVENT_KIND_WAIT, EVENT_KIND_DONE, EVENT_KIND_FAIL,
    EVENT_KIND_MAX_RETRY_REACHED,
    PHASE_GOAL, PHASE_SIGNAL, PHASE_OBSERVE, PHASE_PLAN, PHASE_ACT, PHASE_CHECK, PHASE_WAIT
)
from ai_vl_agent_functions import (
    call_vl_function, is_final_capable,
    FUNC_VL_TARGET_FIND, FUNC_REQUEST_FRAME, FUNC_REQUEST_CLICK,
    FUNC_REQUEST_PLAY_SFX_ALERT, FUNC_REQUEST_DANCE, FUNC_REQUEST_SCREENSHOT
)
from ai_vl_planner_llm import (
    ai_vl_planner_infer_goal_and_success,
    ai_vl_planner_decide_next_step,
    ai_vl_planner_check_success
)


# Think Log 추가
def append_think_log(think_log, think_log_idx_holder, phase, content):
    '''
    think_log에 새 항목 추가
    think_log_idx_holder: [idx] 형태의 리스트 (mutable reference)
    '''
    think_log_idx_holder[0] += 1
    entry = ThinkEntry(idx=think_log_idx_holder[0], phase=phase, content=content)
    think_log.append(entry)
    return entry


# think_log에서 goal 추출
def extract_goal_from_think_log(think_log_list):
    for entry in think_log_list:
        if entry.phase == PHASE_GOAL and entry.content.startswith('Goal:'):
            return entry.content.replace('Goal:', '').strip()
    return ''


# think_log에서 success_signal 추출
def extract_success_from_think_log(think_log_list):
    for entry in think_log_list:
        if entry.phase == PHASE_SIGNAL and entry.content.startswith('Success Signal:'):
            return entry.content.replace('Success Signal:', '').strip()
    return ''


# think_log에서 original_query 추출
def extract_query_from_think_log(think_log_list):
    for entry in think_log_list:
        if entry.phase == PHASE_GOAL and entry.content.startswith('사용자 요청:'):
            content = entry.content.replace('사용자 요청:', '').strip()
            return content.strip('"').strip('"').strip()
    return ''


# think_log에서 마지막 grounding keyword 추출
def extract_last_grounding_keyword(think_log_list):
    for entry in reversed(think_log_list):
        if entry.phase == PHASE_ACT and entry.content.startswith('target:'):
            return entry.content.replace('target:', '').strip()
    return ''


# think_log에서 final_action 추출
def extract_final_action_from_think_log(think_log_list):
    for entry in think_log_list:
        if entry.phase == PHASE_SIGNAL and entry.content.startswith('Final Action:'):
            return entry.content.replace('Final Action:', '').strip()
    return ''


# think_log dict 리스트를 ThinkEntry 리스트로 변환
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


# VL Planner 메인 루프 (Stateless)
def ai_vl_planner_run_loop(
    query=None,           # 사용자 쿼리 (첫 요청 시 필수)
    memory=None,          # 대화 기록 (첫 요청 시 사용)
    initial_frame=None,   # 현재 화면 캡처 경로
    resume_think_log=None,# 재요청 시 이전 think_log (Stateless 핵심)
    retry_count=0,        # 현재까지 재요청 횟수 (Unity가 관리)
    max_retry=5,          # 최대 재요청 허용 횟수
    is_canceled=False,    # 취소 요청 여부
    max_iters=5           # 루프 내 최대 반복 횟수
):
    # 로컬 상태 변수
    think_log = []
    think_log_idx_holder = [0]  # mutable reference
    goal_text = ''
    success_signal = ''
    final_action = ''  # LLM이 지정한 최종 action (optional)
    original_query = ''
    current_grounding_keyword = ''
    
    # ========================================
    # 취소 요청 처리
    # ========================================
    if is_canceled:
        yield AgentEvent(
            kind=EVENT_KIND_FAIL,
            message='작업 취소됨',
            think_log=[],
            data={'reason': 'user_canceled'}
        )
        return
    
    # ========================================
    # 최대 재검증 횟수 초과 체크
    # ========================================
    if retry_count >= max_retry:
        yield AgentEvent(
            kind=EVENT_KIND_MAX_RETRY_REACHED,
            message=f'최대 재검증 횟수({max_retry}) 도달',
            think_log=restore_think_log(resume_think_log) if resume_think_log else [],
            data={
                'reason': 'max_retry_reached',
                'retry_count': retry_count,
                'max_retry': max_retry
            }
        )
        return
    
    # 초기화
    current_frame = initial_frame
    last_function_results = None
    
    # ========================================
    # think_log가 있으면 재요청 (Stateless 복원)
    # ========================================
    if resume_think_log and len(resume_think_log) > 0:
        # 재요청: think_log 복원
        think_log = restore_think_log(resume_think_log)
        think_log_idx_holder[0] = think_log[-1].idx if think_log else 0
        
        # goal, success_signal, final_action 추출
        goal_text = extract_goal_from_think_log(think_log)
        success_signal = extract_success_from_think_log(think_log)
        final_action = extract_final_action_from_think_log(think_log)
        
        # original_query, current_grounding_keyword 복원
        original_query = extract_query_from_think_log(think_log)
        current_grounding_keyword = extract_last_grounding_keyword(think_log)
        
        append_think_log(think_log, think_log_idx_holder, PHASE_OBSERVE, '--- 재요청: 새 프레임 수신 ---')
        append_think_log(think_log, think_log_idx_holder, PHASE_OBSERVE, f'프레임: {current_frame}')
        
        print(f'[VL Planner] 재요청 - goal: {goal_text}, keyword: {current_grounding_keyword}, think_log: {len(think_log)}개')
        
        # OBSERVE 이벤트
        yield AgentEvent(
            kind=EVENT_KIND_OBSERVE,
            message='새 프레임 수신 (재요청)',
            think_log=think_log.copy(),
            data={'frame_path': current_frame, 'is_resumed': True}
        )
        
        # 바로 성공 체크로 (클릭 후 화면 확인)
        frame_summary = f'프레임: {current_frame}' if current_frame else '프레임 없음'
        append_think_log(think_log, think_log_idx_holder, PHASE_CHECK, '클릭 결과 확인 중...')
        
        is_done, check_reason = ai_vl_planner_check_success(
            success_signal, frame_summary, last_function_results
        )
        
        append_think_log(think_log, think_log_idx_holder, PHASE_CHECK, f'성공 여부: {is_done} - {check_reason}')
        
        yield AgentEvent(
            kind=EVENT_KIND_CHECK,
            message='성공 조건 확인',
            think_log=think_log.copy(),
            data={'is_done': is_done, 'reason': check_reason}
        )
        
        if is_done:
            reply = {
                'answer_ko': '클릭 완료! 작업이 성공적으로 끝났어요, 선생님!',
                'answer_jp': 'クリック完了！作業が正常に終わりました、先生！',
                'answer_en': 'Click done! Task completed successfully, Sensei!'
            }
            yield AgentEvent(
                kind=EVENT_KIND_DONE,
                message='작업 완료',
                think_log=think_log.copy(),
                data={'goal': goal_text, 'success_signal': success_signal, 'reply_list': [reply]}
            )
            return
        else:
            # 아직 성공 안 됨 - 루프 계속
            append_think_log(think_log, think_log_idx_holder, PHASE_PLAN, '성공 조건 미충족 - 추가 액션 필요')
    else:
        # ========================================
        # 첫 요청: Goal/Success 설정 (LLM 호출, memory 사용)
        # ========================================
        think_log = []
        think_log_idx_holder[0] = 0
        
        append_think_log(think_log, think_log_idx_holder, PHASE_GOAL, f'사용자 요청: "{query}"')
        append_think_log(think_log, think_log_idx_holder, PHASE_GOAL, 'Goal과 Success Signal 추론 중...')
        
        goal_text, success_signal, final_action = ai_vl_planner_infer_goal_and_success(query, memory, lang='ko')
        original_query = query
        current_grounding_keyword = ''
        
        append_think_log(think_log, think_log_idx_holder, PHASE_GOAL, f'Goal: {goal_text}')
        append_think_log(think_log, think_log_idx_holder, PHASE_SIGNAL, f'Success Signal: {success_signal}')
        if final_action:
            append_think_log(think_log, think_log_idx_holder, PHASE_SIGNAL, f'Final Action: {final_action}')
        
        yield AgentEvent(
            kind=EVENT_KIND_GOAL,
            message='목표 설정 완료',
            think_log=think_log.copy(),
            data={
                'query': query,
                'goal': goal_text,
                'success_signal': success_signal,
                'final_action': final_action,
                'has_memory': bool(memory),
                'has_frame': bool(initial_frame)
            }
        )
    
    # ========================================
    # 메인 루프
    # ========================================
    for iteration in range(max_iters):
        append_think_log(think_log, think_log_idx_holder, PHASE_OBSERVE, f'--- Iteration {iteration + 1} ---')
        
        # 현재 프레임 상태 요약
        frame_summary = f'프레임: {current_frame}' if current_frame else '프레임 없음'
        append_think_log(think_log, think_log_idx_holder, PHASE_OBSERVE, frame_summary)
        
        yield AgentEvent(
            kind=EVENT_KIND_OBSERVE,
            message=f'화면 관찰 (iter {iteration + 1})',
            think_log=think_log.copy(),
            data={'frame_path': current_frame, 'iteration': iteration + 1}
        )
        
        # ========================================
        # 다음 행동 결정 (LLM 호출)
        # ========================================
        append_think_log(think_log, think_log_idx_holder, PHASE_PLAN, '다음 행동 결정 중...')
        
        next_step = ai_vl_planner_decide_next_step(
            goal_text=goal_text,
            success_signal_text=success_signal,
            frame_summary=frame_summary,
            last_function_results=last_function_results
        )
        
        action = next_step.get('action', 'FAIL')
        reason = next_step.get('reason', '')
        append_think_log(think_log, think_log_idx_holder, PHASE_PLAN, f'결정: {action} - {reason}')
        
        yield AgentEvent(
            kind=EVENT_KIND_PLAN,
            message=f'행동 결정: {action}',
            think_log=think_log.copy(),
            data={'next_step': next_step}
        )
        
        # ========================================
        # 행동 실행
        # ========================================
        if action == 'DONE':
            reply = {
                'answer_ko': '작업이 성공적으로 완료되었어요, 선생님!',
                'answer_jp': '作業が正常に完了しました、先生！',
                'answer_en': 'Task completed successfully, Sensei!'
            }
            yield AgentEvent(
                kind=EVENT_KIND_DONE,
                message='작업 완료',
                think_log=think_log.copy(),
                data={'reason': reason, 'final_result': last_function_results, 'reply_list': [reply]}
            )
            return
        
        elif action == 'FAIL':
            yield AgentEvent(
                kind=EVENT_KIND_FAIL,
                message='작업 실패',
                think_log=think_log.copy(),
                data={'reason': reason, 'final_result': last_function_results}
            )
            return
        
        elif action == 'WAIT':
            seconds = next_step.get('seconds', 1.0)
            append_think_log(think_log, think_log_idx_holder, PHASE_WAIT, f'{seconds}초 대기')
            time.sleep(seconds)
            
            yield AgentEvent(
                kind=EVENT_KIND_WAIT,
                message=f'{seconds}초 대기',
                think_log=think_log.copy(),
                data={'seconds': seconds}
            )
            continue
        
        elif action == 'CALL_FUNCTION':
            func_name = next_step.get('function', '')
            func_args = next_step.get('args', {})
            
            append_think_log(think_log, think_log_idx_holder, PHASE_ACT, f'{func_name} 호출')
            
            # ========================================
            # VL Function 실행 (서버 내부)
            # ========================================
            if func_name == FUNC_VL_TARGET_FIND:
                # Keyword Fallback 체인
                llm_keyword = func_args.get('target_text', '')
                
                if llm_keyword and llm_keyword != goal_text.split()[0]:
                    target_text = llm_keyword
                    current_grounding_keyword = llm_keyword
                elif current_grounding_keyword:
                    target_text = current_grounding_keyword
                else:
                    target_text = original_query if original_query else goal_text
                    current_grounding_keyword = target_text
                
                # 실제 grounding 호출
                actual_args = {'target_text': target_text}
                last_function_results = call_vl_function(
                    func_name, actual_args, frame=current_frame
                )
                append_think_log(think_log, think_log_idx_holder, PHASE_ACT, f'target: {target_text}')
                append_think_log(think_log, think_log_idx_holder, PHASE_ACT, f'결과: {str(last_function_results)[:100]}')
                
                yield AgentEvent(
                    kind=EVENT_KIND_ACT,
                    message=f'{func_name} 실행 완료',
                    think_log=think_log.copy(),
                    data={
                        'function': func_name,
                        'args': actual_args,
                        'result': last_function_results
                    }
                )
            
            # ========================================
            # Unity 요청 (클라이언트 액션)
            # ========================================
            elif func_name == FUNC_REQUEST_CLICK:
                x = func_args.get('x', 0)
                y = func_args.get('y', 0)
                
                append_think_log(think_log, think_log_idx_holder, PHASE_ACT, f'클릭 요청: ({x}, {y})')
                
                # 최종 action 판단:
                # 1. final_action이 지정되어 있고, 현재 action이 그것과 일치하면 → 최종
                # 2. final_action이 없으면 → fallback: is_final_capable + ONE_SHOT 체크
                is_one_shot = success_signal.strip().upper().startswith('[ONE_SHOT]')
                
                # final_action 체크
                is_final_by_signal = (final_action and func_name == final_action)
                is_final_by_fallback = (not final_action and is_final_capable(func_name) and is_one_shot)
                
                should_terminate = is_final_by_signal or is_final_by_fallback
                
                if should_terminate and is_one_shot:
                    # ONE_SHOT + 최종 action: 클릭 완료 = 성공
                    reason_msg = f'[ONE_SHOT] final_action={final_action}' if is_final_by_signal else '[ONE_SHOT] fallback'
                    append_think_log(think_log, think_log_idx_holder, PHASE_CHECK, f'{reason_msg} → 즉시 성공 처리')
                    
                    reply = {
                        'answer_ko': '작업이 종료되었어요, 선생님.',
                        'answer_jp': '作業が終了しました、先生。',
                        'answer_en': 'Task completed, Sensei.'
                    }
                    yield AgentEvent(
                        kind=EVENT_KIND_DONE,
                        message='작업 완료 (ONE_SHOT)',
                        think_log=think_log.copy(),
                        data={
                            'action': 'click',
                            'x': x, 'y': y,
                            'goal': goal_text,
                            'success_signal': success_signal,
                            'final_action': final_action,
                            'is_one_shot': True,
                            'reply_list': [reply]
                        }
                    )
                    return
                else:
                    # VERIFY: 화면 변화 확인 필요 → request_observation
                    append_think_log(think_log, think_log_idx_holder, PHASE_ACT, '[VERIFY] 새 프레임 대기 (Unity에게 request_observation 전송)')
                    
                    yield AgentEvent(
                        kind=EVENT_KIND_OBSERVE,
                        message='화면 관찰 요청',
                        think_log=think_log.copy(),
                        data={
                            'reason': 'click_result_verification',
                            'action': 'click',
                            'x': x, 'y': y,
                            'goal': goal_text,
                            'success_signal': success_signal,
                            'retry_count': retry_count + 1,
                            'max_retry': max_retry
                        }
                    )
                    return  # 스트리밍 종료, Unity의 재요청 대기
            
            elif func_name == FUNC_REQUEST_FRAME:
                append_think_log(think_log, think_log_idx_holder, PHASE_ACT, '새 프레임 요청')
                
                yield AgentEvent(
                    kind=EVENT_KIND_OBSERVE,
                    message='화면 관찰 요청',
                    think_log=think_log.copy(),
                    data={
                        'reason': 'frame_request',
                        'goal': goal_text,
                        'success_signal': success_signal,
                        'retry_count': retry_count + 1,
                        'max_retry': max_retry
                    }
                )
                return
            
            elif func_name == FUNC_REQUEST_PLAY_SFX_ALERT:
                append_think_log(think_log, think_log_idx_holder, PHASE_ACT, '알림 효과음 재생 요청')
                
                reply = {
                    'answer_ko': '확인이 필요한 사항이 생겼어요, 선생님.',
                    'answer_jp': '確認が必要なことがあります、先生。',
                    'answer_en': 'Something needs your attention, Sensei.'
                }
                yield AgentEvent(
                    kind=EVENT_KIND_DONE,
                    message='알림 완료 (ONE_SHOT)',
                    think_log=think_log.copy(),
                    data={
                        'action': 'sfx_alert',
                        'goal': goal_text,
                        'success_signal': success_signal,
                        'is_one_shot': True,
                        'reply_list': [reply]
                    }
                )
                return
            
            elif func_name == FUNC_REQUEST_DANCE:
                dance_type = func_args.get('dance_type', 'default')
                append_think_log(think_log, think_log_idx_holder, PHASE_ACT, f'춤 요청: {dance_type}')
                
                reply = {
                    'answer_ko': '알겠어요! 춤을 출게요, 선생님!',
                    'answer_jp': 'わかりました！踊りますね、先生！',
                    'answer_en': 'Got it! I will dance, Sensei!'
                }
                yield AgentEvent(
                    kind=EVENT_KIND_DONE,
                    message='춤 요청 완료 (ONE_SHOT)',
                    think_log=think_log.copy(),
                    data={
                        'action': 'dance',
                        'dance_type': dance_type,
                        'goal': goal_text,
                        'success_signal': success_signal,
                        'is_one_shot': True,
                        'reply_list': [reply]
                    }
                )
                return
            
            elif func_name == FUNC_REQUEST_SCREENSHOT:
                append_think_log(think_log, think_log_idx_holder, PHASE_ACT, '스크린샷 요청')
                
                reply = {
                    'answer_ko': '스크린샷을 저장하고 클립보드에 복사했어요, 선생님.',
                    'answer_jp': 'スクリーンショットを保存してクリップボードにコピーしました、先生。',
                    'answer_en': 'Screenshot saved and copied to clipboard, Sensei.'
                }
                yield AgentEvent(
                    kind=EVENT_KIND_DONE,
                    message='스크린샷 요청 완료 (ONE_SHOT)',
                    think_log=think_log.copy(),
                    data={
                        'action': 'screenshot',
                        'goal': goal_text,
                        'success_signal': success_signal,
                        'is_one_shot': True,
                        'reply_list': [reply]
                    }
                )
                return
    
    # max_iters 초과
    append_think_log(think_log, think_log_idx_holder, PHASE_CHECK, f'최대 반복 횟수({max_iters}) 도달')
    yield AgentEvent(
        kind=EVENT_KIND_MAX_RETRY_REACHED,
        message=f'최대 반복 횟수({max_iters}) 도달',
        think_log=think_log.copy(),
        data={
            'reason': 'max_iters_exceeded',
            'max_iters': max_iters,
            'retry_count': retry_count,
            'max_retry': max_retry
        }
    )


if __name__ == '__main__':
    print('=== ai_vl_planner 테스트 ===')
    print('서버에서 실행하세요: python server_interface_vl_planner_impl.py')
