'''
ai_vl_planner_prompts.py
VL Planner 프롬프트 템플릿
- Agent instructions: English
- Few-shot examples: Multilingual (ko, en, ja)
- Function descriptions: Dynamic from ai_vl_agent_functions.py
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from ai_vl_agent_functions import (
    get_function_list_for_goal_and_success_signal,
    get_all_functions_list,
    get_final_capable_functions,
    FUNC_REQUEST_CLICK, FUNC_REQUEST_DANCE, FUNC_REQUEST_SCREENSHOT, FUNC_REQUEST_PLAY_SFX_ALERT
)


# ========================================
# Goal/Success Signal 추론 프롬프트
# ========================================

PROMPT_GOAL_SIGNAL = '''You are an expert at analyzing user requests to set goals and success conditions.

User Request: "{query}"

Previous Conversation:
{memory}

**Directly executable functions:**
{function_list}

**Rules:**
- goal: The user's desired final outcome in one sentence
- success_signal: A condition to verify goal achievement
  - [ONE_SHOT]: Success cannot be verified from screen, complete after final action
  - [VERIFY]: Success CAN be verified from screen, check after action
- final_action: The function that completes this goal (optional)

**Output Format:**
goal: <goal>
success_signal: [ONE_SHOT or VERIFY] <success condition>
final_action: <function_name or empty>

{examples}'''

# Few-shot 예시 (언어별)
EXAMPLES_GOAL_SIGNAL_KO = '''**Example 1 (Simple click request):**
Request: "클릭해줘"
goal: Find and click the target on screen
success_signal: [ONE_SHOT] Click action completed
final_action: function_request_click

**Example 2 (Result verification needed):**
Request: "라면 사줘"
goal: Click yellow button to purchase ramen
success_signal: [VERIFY] Ramen purchase complete screen appears
final_action: function_request_click

**Example 3 (Dance request):**
Request: "춤춰줘"
goal: Make character dance
success_signal: [ONE_SHOT] Dance action triggered
final_action: function_request_dance'''

EXAMPLES_GOAL_SIGNAL_EN = '''**Example 1 (Simple click request):**
Request: "Click it"
goal: Find and click the target on screen
success_signal: [ONE_SHOT] Click action completed
final_action: function_request_click

**Example 2 (Result verification needed):**
Request: "Buy ramen"
goal: Click yellow button to purchase ramen
success_signal: [VERIFY] Ramen purchase complete screen appears
final_action: function_request_click

**Example 3 (Dance request):**
Request: "Dance for me"
goal: Make character dance
success_signal: [ONE_SHOT] Dance action triggered
final_action: function_request_dance'''

EXAMPLES_GOAL_SIGNAL_JA = '''**Example 1 (Simple click request):**
Request: "クリックして"
goal: Find and click the target on screen
success_signal: [ONE_SHOT] Click action completed
final_action: function_request_click

**Example 2 (Result verification needed):**
Request: "ラーメン買って"
goal: Click yellow button to purchase ramen
success_signal: [VERIFY] Ramen purchase complete screen appears
final_action: function_request_click

**Example 3 (Dance request):**
Request: "踊って"
goal: Make character dance
success_signal: [ONE_SHOT] Dance action triggered
final_action: function_request_dance'''


def get_goal_signal_prompt(query, memory=None, lang='en'):
    if lang == 'ko':
        examples = EXAMPLES_GOAL_SIGNAL_KO
    elif lang == 'ja':
        examples = EXAMPLES_GOAL_SIGNAL_JA
    else:
        examples = EXAMPLES_GOAL_SIGNAL_EN
    
    memory_text = ''
    if memory:
        for entry in memory[-8:]:  # 최근 8개만
            role = entry.get('role', 'user')
            message = entry.get('message', '')
            memory_text += f'[{role}] {message}\n'
    
    function_list = get_function_list_for_goal_and_success_signal(lang)
    
    return PROMPT_GOAL_SIGNAL.format(
        query=query,
        memory=memory_text if memory_text else '(none)',
        function_list=function_list,
        examples=examples
    )


def parse_goal_signal_response(response):
    goal = ''
    success_signal = ''
    final_action = ''  # 최종 action (optional)
    
    for line in response.split('\n'):
        line = line.strip()
        if line.lower().startswith('goal:'):
            goal = line.split(':', 1)[1].strip()
        elif line.lower().startswith('success_signal:'):
            success_signal = line.split(':', 1)[1].strip()
        elif line.lower().startswith('final_action:'):
            final_action = line.split(':', 1)[1].strip()
    
    return goal, success_signal, final_action


# ========================================
# 다음 행동 결정 프롬프트
# ========================================

PROMPT_DECIDE_NEXT_STEP = '''You are a VL Agent. Analyze the current situation and decide the next action.

Goal: {goal}
Success Condition: {success_signal}

Current Frame Summary:
{frame_summary}

Previous Action Result:
{last_results}

**Available Actions:**
{available_actions}
- WAIT : Wait for screen update
- DONE : Task completed
- FAIL : Task failed

**Output Format:**
action: <action>
keyword: <target to find - required for grounding, use concise noun phrase>
reason: <reason>

**Example 1 (grounding needed):**
action: CALL_FUNCTION(function_vl_grounding)
keyword: yellow button
reason: Need to find the location of the yellow button to click

**Example 2 (perform click):**
action: CALL_FUNCTION(function_request_click)
keyword:
reason: Click at the coordinates found by grounding'''


def get_decide_next_step_prompt(goal, success_signal, frame_summary, last_results, lang='en'):
    # 동적으로 function 목록 가져오기
    available_actions = get_all_functions_list(lang)
    
    return PROMPT_DECIDE_NEXT_STEP.format(
        goal=goal or '(none)',
        success_signal=success_signal or '(none)',
        frame_summary=frame_summary or '(none)',
        last_results=str(last_results) if last_results else '(none)',
        available_actions=available_actions
    )


def parse_decide_response(response):
    action = 'WAIT'
    reason = ''
    extra_args = {}
    
    for line in response.split('\n'):
        line = line.strip()
        if line.lower().startswith('action:'):
            action = line.split(':', 1)[1].strip()
        elif line.lower().startswith('keyword:'):
            keyword = line.split(':', 1)[1].strip()
            if keyword:
                extra_args['keyword'] = keyword
        elif line.lower().startswith('reason:'):
            reason = line.split(':', 1)[1].strip()
    
    return action, reason, extra_args


# ========================================
# 성공 체크 프롬프트
# ========================================

PROMPT_CHECK_SUCCESS = '''Determine if the success condition has been met.

Success Condition: {success_signal}

Current Frame Summary:
{frame_summary}

Recent Action Result:
{last_results}

**Output Format:**
is_done: true or false
reason: <reason for judgment>'''


def get_check_success_prompt(success_signal, frame_summary, last_results):
    return PROMPT_CHECK_SUCCESS.format(
        success_signal=success_signal or '(none)',
        frame_summary=frame_summary or '(none)',
        last_results=str(last_results) if last_results else '(none)'
    )


def parse_check_response(response):
    is_done = False
    reason = ''
    
    for line in response.split('\n'):
        line = line.strip()
        if line.lower().startswith('is_done:'):
            value = line.split(':', 1)[1].strip().lower()
            is_done = value in ['true', 'yes', '1']
        elif line.lower().startswith('reason:'):
            reason = line.split(':', 1)[1].strip()
    
    return is_done, reason


if __name__ == '__main__':
    print('=== Planner Prompts Test ===')
    
    print('\n--- Goal/Signal Prompt (ko) ---')
    prompt = get_goal_signal_prompt('노란 버튼을 클릭해줘', lang='ko')
    print(prompt[:500])
    
    print('\n--- Decide Next Step Prompt ---')
    prompt = get_decide_next_step_prompt('Click yellow button', '[VERIFY] Success', 'Frame: test.png', None)
    print(prompt[:800])
