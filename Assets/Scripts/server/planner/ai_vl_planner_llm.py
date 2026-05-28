'''
ai_vl_planner_llm.py
VL Planner LLM 추론 함수들
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from ai_singleton import get_llm
from ai_vl_agent_functions import (
    FUNC_VL_TARGET_FIND, FUNC_REQUEST_FRAME, FUNC_REQUEST_CLICK,
    FUNC_REQUEST_PLAY_SFX_ALERT, FUNC_REQUEST_DANCE, FUNC_REQUEST_SCREENSHOT
)


# LLM 호출 공통 함수
def call_llm(prompt, max_tokens=256):
    '''
    텍스트 생성용 LLM 호출 (VL 모델 사용)
    '''
    llm = get_llm(require_vl=True)
    
    output = ''
    try:
        for out in llm.generate_with_streaming(prompt):
            output = out
    except:
        output = llm.generate(prompt, {'max_new_tokens': max_tokens, 'temperature': 0.3})
    
    # think 태그 제거
    if '</think>' in output:
        _, output = output.split('</think>', 1)
    
    return output.strip()


# Goal과 Success Signal 추론
def ai_vl_planner_infer_goal_and_success(query, memory=None, lang='ko'):
    from ai_vl_planner_prompts import get_goal_signal_prompt, parse_goal_signal_response
    
    prompt_body = get_goal_signal_prompt(query, memory, lang)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>user
Analyze. /no_think<|im_end|>
<|im_start|>assistant
goal:'''
    
    print(f'\n[LLM] infer_goal_and_success')
    print(f'  prompt: {full_prompt[:200]}...')
    
    output = call_llm(full_prompt, max_tokens=128)
    print(f'  output: {output}')
    
    goal, success_signal, final_action = parse_goal_signal_response('goal:' + output)
    
    print(f'  parsed: goal={goal}, signal={success_signal}, final_action={final_action}')
    
    return goal, success_signal, final_action


# 다음 스텝 결정
def ai_vl_planner_decide_next_step(goal_text, success_signal_text, frame_summary, last_function_results):
    from ai_vl_planner_prompts import get_decide_next_step_prompt, parse_decide_response
    
    prompt_body = get_decide_next_step_prompt(goal_text, success_signal_text, frame_summary, last_function_results)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>user
Decide next action. /no_think<|im_end|>
<|im_start|>assistant
action:'''
    
    print(f'\n[LLM] decide_next_step')
    print(f'  goal: {goal_text}')
    print(f'  last_results: {str(last_function_results)[:100]}...')
    
    output = call_llm(full_prompt, max_tokens=128)
    print(f'  output: {output}')
    
    action, reason, extra_args = parse_decide_response('action:' + output)
    
    print(f'  parsed: action={action}, extra_args={extra_args}')
    
    # action 파싱해서 구조화
    action_upper = action.upper()
    
    if 'DONE' in action_upper:
        return {'action': 'DONE', 'reason': reason}
    elif 'FAIL' in action_upper:
        return {'action': 'FAIL', 'reason': reason}
    elif 'WAIT' in action_upper:
        return {'action': 'WAIT', 'seconds': 1.0, 'reason': reason}
    elif 'CLICK' in action_upper or 'REQUEST_CLICK' in action_upper:
        # 마지막 결과에서 좌표 추출
        if last_function_results and isinstance(last_function_results, dict):
            x = last_function_results.get('x')
            y = last_function_results.get('y')
            if x is not None and y is not None:
                return {
                    'action': 'CALL_FUNCTION',
                    'function': FUNC_REQUEST_CLICK,
                    'args': {'x': x, 'y': y},
                    'reason': reason
                }
        return {'action': 'FAIL', 'reason': 'No coordinates for click'}
    elif 'GROUNDING' in action_upper:
        keyword = extra_args.get('keyword', '')
        target = keyword if keyword else (goal_text.split()[0] if goal_text else 'target')
        return {
            'action': 'CALL_FUNCTION',
            'function': FUNC_VL_TARGET_FIND,
            'args': {'target_text': target},
            'reason': reason
        }
    elif 'FRAME' in action_upper:
        return {
            'action': 'CALL_FUNCTION',
            'function': FUNC_REQUEST_FRAME,
            'args': {'purpose': reason},
            'reason': reason
        }
    else:
        # 기본: grounding
        keyword = extra_args.get('keyword', '')
        target = keyword if keyword else (goal_text.split()[0] if goal_text else 'target')
        return {
            'action': 'CALL_FUNCTION',
            'function': FUNC_VL_TARGET_FIND,
            'args': {'target_text': target},
            'reason': reason
        }


# 성공 여부 체크
def ai_vl_planner_check_success(success_signal_text, frame_summary_text, last_function_results):
    from ai_vl_planner_prompts import get_check_success_prompt, parse_check_response
    
    prompt_body = get_check_success_prompt(success_signal_text, frame_summary_text, last_function_results)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>user
Determine success. /no_think<|im_end|>
<|im_start|>assistant
is_done:'''
    
    print(f'\n[LLM] check_success')
    print(f'  success_signal: {success_signal_text}')
    print(f'  last_results: {str(last_function_results)[:100]}...')
    
    output = call_llm(full_prompt, max_tokens=64)
    print(f'  output: {output}')
    
    is_done, reason = parse_check_response('is_done:' + output)
    
    return is_done, reason


if __name__ == '__main__':
    print('=== ai_vl_planner_llm 테스트 ===')
    print('LLM 테스트는 서버에서 실행하세요')
