'''
ai_multi_llm.py
Multi-Conversation LLM 호출 모듈 (Local/Gemini 분기)

server_type에 따라 Local LLM 또는 Gemini API를 선택하여 호출합니다.
ai_singleton 기반으로 Local LLM 관리, Gemini는 직접 호출 (ASIS2 스타일)
'''
from threading import Lock

# 프롬프트 모듈
from ai_multi_prompts import (
    get_generate_reply_prompt,
    get_flow_director_prompt, parse_flow_director_response,
    get_ai_trigger_prompt, parse_ai_trigger_response,
    get_greeting_prompt,
    get_target_speaker_analysis_prompt, parse_target_speaker_response,
    get_flow_decision_prompt, parse_flow_decision_response,
    get_target_listener_analysis_prompt, parse_target_listener_response
)

# Local LLM (ai_singleton 사용)
from ai_singleton import get_llm

# util 모듈
import util_string

generation_lock = Lock()

# Gemini 설정 (ASIS2 스타일)
gemini_model = None
current_gemini_key = None

try:
    import google.generativeai as genai
    from kei import GEMINI_API_KEY
    if GEMINI_API_KEY:
        current_gemini_key = GEMINI_API_KEY
        genai.configure(api_key=current_gemini_key)
        gemini_model = genai.GenerativeModel('gemini-2.0-flash')
except:
    pass


def load_gemini_model(api_key=None):
    '''Gemini 모델 로딩 (ASIS2 스타일)'''
    global gemini_model, current_gemini_key
    
    if api_key:
        current_gemini_key = api_key
    
    if not current_gemini_key:
        try:
            from kei import GEMINI_API_KEY
            current_gemini_key = GEMINI_API_KEY
        except:
            pass
    
    if current_gemini_key:
        import google.generativeai as genai
        genai.configure(api_key=current_gemini_key)
        gemini_model = genai.GenerativeModel('gemini-2.0-flash')
    
    return gemini_model


def generate_with_llm(prompt, max_tokens=256, server_type='Local', api_key=None, temperature=0.7):
    '''LLM 호출 (Local 또는 Gemini)'''
    
    # Gemini 사용
    if server_type in ['Gemini', 'Free_Gemini', 'Auto']:
        if not gemini_model:
            load_gemini_model(api_key)
        
        if gemini_model:
            try:
                response = gemini_model.generate_content(
                    prompt,
                    generation_config={
                        'max_output_tokens': max_tokens,
                        'temperature': temperature
                    }
                )
                return response.text.strip()
            except Exception as e:
                print(f"[Gemini] 생성 실패: {e}")
                return ''
    
    # Local LLM 사용 (ai_singleton)
    llm = get_llm()
    cfg = {'max_new_tokens': max_tokens, 'temperature': temperature, 'repetition_penalty': 1.1}
    
    output = ''
    try:
        for out in llm.generate_with_streaming(prompt, cfg):
            output = out
    except:
        output = llm.generate(prompt, cfg)
    
    # </think> 태그 제거
    if '</think>' in output:
        _, output = output.split('</think>', 1)
    
    return output.strip()


# ============================================================================
# Multi-Conversation 전용 LLM 호출 함수들
# ============================================================================

def generate_reply(char_name, speaker, listener, participants, history,
                   last_utterance, lang='ko', server_type='Local', api_key=None):
    '''캐릭터 대화 생성'''
    prompt_body = get_generate_reply_prompt(
        char_name=char_name,
        speaker=speaker,
        listener=listener,
        participants=participants,
        history=history,
        last_utterance=last_utterance,
        lang=lang
    )
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        output = generate_with_llm(full_prompt, max_tokens=256, server_type=server_type, api_key=api_key)
    
    return _parse_generation(output)


def decide_next_flow(participants, history, last_speaker, last_content,
                     lang='ko', server_type='Local', api_key=None):
    '''다음 화자/청자 결정 (Flow Director)'''
    prompt_body = get_flow_director_prompt(
        participants=participants,
        history=history,
        last_speaker=last_speaker,
        last_content=last_content,
        lang=lang
    )
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    with generation_lock:
        output = generate_with_llm(full_prompt, max_tokens=32, server_type=server_type, api_key=api_key)
    
    return parse_flow_director_response(output)


def check_ai_trigger(situation, history, lang='ko', server_type='Local', api_key=None):
    '''AI 트리거 상황 판단'''
    prompt_body = get_ai_trigger_prompt(
        situation=situation,
        history=history,
        lang=lang
    )
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    with generation_lock:
        output = generate_with_llm(full_prompt, max_tokens=64, server_type=server_type, api_key=api_key)
    
    return parse_ai_trigger_response(output)


def generate_greeting(char_name, situation=None, lang='ko', server_type='Local', api_key=None):
    '''인사말 생성'''
    prompt_body = get_greeting_prompt(
        char_name=char_name,
        situation=situation,
        lang=lang
    )
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        output = generate_with_llm(full_prompt, max_tokens=128, server_type=server_type, api_key=api_key)
    
    return _parse_generation(output)


# ============================================================================
# Flow Analysis 함수들 (ASIS3 ai_aropla_flow 대응)
# ============================================================================

def analyze_target_speaker(message, memory_multi, lang='ko', server_type='Local', api_key=None):
    '''명시적 타겟 분석 (ASIS3: analyze_target_speaker_from_message)'''
    prompt_body = get_target_speaker_analysis_prompt(
        message=message,
        memory_multi=memory_multi,
        lang=lang
    )
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>user
분석해주세요. /no_think<|im_end|>
<|im_start|>assistant
'''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        output = generate_with_llm(full_prompt, max_tokens=50, server_type=server_type, api_key=api_key, temperature=0.1)
    
    return parse_target_speaker_response(output)


def decide_flow(memory_multi, query, final_response, current_speaker, query_speaker,
                lang='ko', server_type='Local', api_key=None):
    '''대화 흐름 결정 (ASIS3: process_flow_decision)'''
    prompt_body = get_flow_decision_prompt(
        memory_multi=memory_multi,
        query=query,
        final_response=final_response,
        current_speaker=current_speaker,
        query_speaker=query_speaker,
        lang=lang
    )
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>user
결정해주세요. /no_think<|im_end|>
<|im_start|>assistant
next_speaker: '''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        output = generate_with_llm(full_prompt, max_tokens=50, server_type=server_type, api_key=api_key, temperature=0.1)
    
    return parse_flow_decision_response(output)


def analyze_target_listener(message, current_speaker, target_speaker, memory_multi,
                            lang='ko', server_type='Local', api_key=None):
    '''청자 결정 (ASIS3: analyze_target_listener_from_message)'''
    prompt_body = get_target_listener_analysis_prompt(
        message=message,
        current_speaker=current_speaker,
        target_speaker=target_speaker,
        memory_multi=memory_multi,
        lang=lang
    )
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>user
분석해주세요. /no_think<|im_end|>
<|im_start|>assistant
'''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        output = generate_with_llm(full_prompt, max_tokens=50, server_type=server_type, api_key=api_key, temperature=0.1)
    
    return parse_target_listener_response(output)


# ============================================================================
# 유틸리티
# ============================================================================

def _parse_generation(text: str) -> str:
    '''생성된 텍스트 정리'''
    if not text:
        return ''
    
    if util_string and hasattr(util_string, 'remove_think_tag'):
        cleaned = util_string.remove_think_tag(text)
    else:
        cleaned = text
        if '</think>' in cleaned:
            _, cleaned = cleaned.split('</think>', 1)
    
    cleaned = cleaned.strip()
    
    if util_string and hasattr(util_string, 'remove_character_prefix'):
        cleaned = util_string.remove_character_prefix(cleaned)
    
    # 첫 유효한 응답만 추출 (캐릭터 이름: 형식 제거)
    lines = cleaned.split('\n')
    result_lines = []
    for line in lines:
        line = line.strip()
        if not line:
            continue
        # "아로나:" 형식 제거
        if ':' in line and len(line.split(':')[0]) < 15:
            parts = line.split(':', 1)
            if parts[1].strip():
                line = parts[1].strip()
        result_lines.append(line)
    
    return '\n'.join(result_lines[:3]) if result_lines else cleaned


if __name__ == '__main__':
    print('=== ai_multi_llm 테스트 ===')
    print('ai_singleton 기반 LLM, Gemini 직접 호출 (ASIS2 스타일)')
    print('실제 테스트는 서버에서 실행하세요.')
