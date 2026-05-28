'''
ai_20q_llm.py
20 Questions Game LLM 호출 모듈 (Local/Gemini 분기)

server_type에 따라 Local LLM 또는 Gemini API를 선택하여 호출합니다.
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from threading import Lock
from typing import Tuple, List, Generator, Optional

# 프롬프트 모듈
from ai_20q_prompts import (
    get_answer_prompt,
    get_classify_intent_prompt, parse_classify_intent_response,
    get_judge_guess_prompt, parse_judge_guess_response,
    get_generate_secret_prompt,
    get_classify_restart_prompt,
    get_classify_continue_prompt,
    get_casual_chat_prompt,
    get_extract_guess_prompt
)

# util 모듈
try:
    import util_string
except ImportError:
    util_string = None

generation_lock = Lock()


# ============================================================================
# LLM Provider 추상화
# ============================================================================

class LLMProvider:
    '''LLM Provider 추상 베이스 클래스'''
    
    def generate(self, prompt: str, max_tokens: int = 256) -> str:
        '''텍스트 생성'''
        raise NotImplementedError
    
    def generate_stream(self, prompt: str, max_tokens: int = 256) -> Generator[str, None, None]:
        '''스트리밍 텍스트 생성'''
        raise NotImplementedError


class LocalLLMProvider(LLMProvider):
    '''Local LLM Provider (ai_singleton 사용)'''
    
    def __init__(self):
        self.llm = None
    
    def _get_llm(self):
        if self.llm is None:
            from ai_singleton import get_llm
            self.llm = get_llm()
        return self.llm
    
    def generate(self, prompt: str, max_tokens: int = 256) -> str:
        llm = self._get_llm()
        cfg = {'max_new_tokens': max_tokens, 'temperature': 0.3}
        
        output = ''
        try:
            for out in llm.generate_with_streaming(prompt):
                output = out
        except:
            output = llm.generate(prompt, cfg)
        
        return self._clean_output(output)
    
    def generate_stream(self, prompt: str, max_tokens: int = 256) -> Generator[str, None, None]:
        llm = self._get_llm()
        cfg = {'max_new_tokens': max_tokens, 'temperature': 0.3}
        
        for output in llm.generate_with_streaming(prompt, cfg):
            yield self._clean_output(output)
    
    def _clean_output(self, output: str) -> str:
        '''think 태그 제거 및 정리'''
        if '</think>' in output:
            _, output = output.split('</think>', 1)
        return output.strip()


class GeminiLLMProvider(LLMProvider):
    '''Gemini API Provider'''
    
    def __init__(self, api_key: str = None):
        self.api_key = api_key
        self.model = None
        self._initialized = False
    
    def _init_model(self):
        if self._initialized:
            return
        
        try:
            import google.generativeai as genai
            
            # API 키 설정
            if self.api_key:
                genai.configure(api_key=self.api_key)
            else:
                # kei.py에서 API 키 가져오기
                try:
                    from kei import GEMINI_API_KEY
                    if GEMINI_API_KEY:
                        genai.configure(api_key=GEMINI_API_KEY)
                except ImportError:
                    pass
            
            # 모델 초기화
            self.model = genai.GenerativeModel('gemini-2.0-flash')
            self._initialized = True
        except Exception as e:
            print(f"[Gemini] Initialization failed: {e}")
            raise
    
    def generate(self, prompt: str, max_tokens: int = 256) -> str:
        self._init_model()
        
        try:
            response = self.model.generate_content(
                prompt,
                generation_config={
                    'max_output_tokens': max_tokens,
                    'temperature': 0.3
                }
            )
            return response.text.strip()
        except Exception as e:
            print(f"[Gemini] Generation failed: {e}")
            return ''
    
    def generate_stream(self, prompt: str, max_tokens: int = 256) -> Generator[str, None, None]:
        self._init_model()
        
        try:
            response = self.model.generate_content(
                prompt,
                generation_config={
                    'max_output_tokens': max_tokens,
                    'temperature': 0.3
                },
                stream=True
            )
            
            full_text = ''
            for chunk in response:
                if chunk.text:
                    full_text += chunk.text
                    yield full_text
        except Exception as e:
            print(f"[Gemini] Streaming failed: {e}")
            yield ''


# ============================================================================
# Provider Factory
# ============================================================================

_providers = {}


def get_provider(server_type: str = 'Local', api_key: str = None) -> LLMProvider:
    '''server_type에 따른 Provider 반환'''
    global _providers
    
    # Auto 모드 처리
    if server_type == 'Auto':
        # API 키가 있으면 Gemini, 없으면 Local
        if api_key:
            server_type = 'Gemini'
        else:
            try:
                from kei import GEMINI_API_KEY
                if GEMINI_API_KEY:
                    server_type = 'Gemini'
                    api_key = GEMINI_API_KEY
                else:
                    server_type = 'Local'
            except ImportError:
                server_type = 'Local'
    
    # Gemini 계열
    if server_type in ['Gemini', 'Free_Gemini']:
        key = f'gemini_{api_key or "default"}'
        if key not in _providers:
            _providers[key] = GeminiLLMProvider(api_key)
        return _providers[key]
    
    # Local
    if 'local' not in _providers:
        _providers['local'] = LocalLLMProvider()
    return _providers['local']


# ============================================================================
# 20Q 게임 전용 LLM 호출 함수들
# ============================================================================

def generate_answer(
    question: str,
    secret: str,
    lang: str = 'ko',
    char_name: str = 'arona',
    server_type: str = 'Local',
    api_key: str = None
) -> str:
    '''질문에 대한 예/아니오 답변 생성'''
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_answer_prompt(secret, lang, char_name)
    
    # Qwen 형식 프롬프트
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>user
질문: {question}<|im_end|>
<|im_start|>assistant
'''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        output = provider.generate(full_prompt, max_tokens=64)
    
    return _parse_generation(output)


def generate_answer_stream(
    question: str,
    secret: str,
    lang: str = 'ko',
    char_name: str = 'arona',
    server_type: str = 'Local',
    api_key: str = None
) -> Generator[List[str], None, None]:
    '''질문에 대한 답변 생성 (스트리밍)'''
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_answer_prompt(secret, lang, char_name)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>user
질문: {question}<|im_end|>
<|im_start|>assistant
'''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        reply_list = []
        
        for output in provider.generate_stream(full_prompt, max_tokens=128):
            cleaned = _parse_generation(output)
            
            # 문장 분리
            if util_string and hasattr(util_string, 'get_punctuation_sentences'):
                sentences = util_string.get_punctuation_sentences(cleaned)
                if sentences and len(sentences) > len(reply_list):
                    reply_list = sentences
                    yield reply_list
            else:
                yield [cleaned]
        
        if reply_list:
            yield reply_list
        else:
            yield [_parse_generation(output) if output else '']


def classify_user_intent(
    utterance: str,
    secret: str,
    history: list = None,
    lang: str = 'ko',
    server_type: str = 'Local',
    api_key: str = None
) -> dict:
    '''사용자 발화 의도 분류'''
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_classify_intent_prompt(utterance, secret, history, lang)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    with generation_lock:
        output = provider.generate(full_prompt, max_tokens=32)
    
    return parse_classify_intent_response(output)


def judge_guess_correctness(
    guess: str,
    secret: str,
    lang: str = 'ko',
    server_type: str = 'Local',
    api_key: str = None
) -> str:
    '''정답 판정 (1차 문자열 비교 + 2차 LLM)'''
    # 1차: Python 문자열 비교
    guess_normalized = guess.strip().lower()
    secret_normalized = secret.strip().lower()
    
    if guess_normalized == secret_normalized:
        return 'yes'
    
    # 2차: LLM 의미 판정
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_judge_guess_prompt(guess, secret, lang)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    with generation_lock:
        output = provider.generate(full_prompt, max_tokens=8)
    
    return parse_judge_guess_response(output)


def generate_secret_target(
    theme_key: str,
    lang: str = 'ko',
    used_answers: list = None,
    server_type: str = 'Local',
    api_key: str = None
) -> str:
    '''비밀 단어 생성'''
    # 먼저 ASIS2의 answer_pool 시도
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(__file__)), 'ASIS2'))
        import ai_game_20questions_answers as answer_pool
        
        secret = answer_pool.pick_answer_excluding_used(theme_key, lang, used_answers or [])
        if secret:
            return secret
    except ImportError:
        pass
    
    # answer_pool에 없으면 LLM 생성
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_generate_secret_prompt(theme_key, lang)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    with generation_lock:
        output = provider.generate(full_prompt, max_tokens=32)
    
    return _parse_generation(output)


def classify_restart_intent(
    utterance: str,
    history: list = None,
    lang: str = 'ko',
    server_type: str = 'Local',
    api_key: str = None
) -> str:
    '''재시작 의도 분류'''
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_classify_restart_prompt(utterance, history, lang)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    with generation_lock:
        output = provider.generate(full_prompt, max_tokens=16)
    
    label = output.strip().lower()
    return 'yes' if 'yes' in label[:5] else 'no'


def classify_continue_intent(
    utterance: str,
    lang: str = 'ko',
    server_type: str = 'Local',
    api_key: str = None
) -> str:
    '''계속/포기 의도 분류'''
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_classify_continue_prompt(utterance, lang)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    with generation_lock:
        output = provider.generate(full_prompt, max_tokens=16)
    
    label = output.strip().lower()
    return 'continue' if 'continue' in label else 'give_up'


def generate_casual_chat(
    utterance: str,
    lang: str = 'ko',
    char_name: str = 'arona',
    game_status: str = 'playing',
    server_type: str = 'Local',
    api_key: str = None
) -> str:
    '''일상 대화 생성'''
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_casual_chat_prompt(utterance, lang, char_name, game_status)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        output = provider.generate(full_prompt, max_tokens=128)
    
    return _parse_generation(output)


def generate_casual_chat_stream(
    utterance: str,
    lang: str = 'ko',
    char_name: str = 'arona',
    game_status: str = 'playing',
    server_type: str = 'Local',
    api_key: str = None
) -> Generator[List[str], None, None]:
    '''일상 대화 생성 (스트리밍)'''
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_casual_chat_prompt(utterance, lang, char_name, game_status)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    if util_string:
        full_prompt = util_string.replace_user_placeholder(full_prompt, 'sensei')
    
    with generation_lock:
        reply_list = []
        
        for output in provider.generate_stream(full_prompt, max_tokens=192):
            cleaned = _parse_generation(output)
            
            if util_string and hasattr(util_string, 'get_punctuation_sentences'):
                sentences = util_string.get_punctuation_sentences(cleaned)
                if sentences and len(sentences) > len(reply_list):
                    reply_list = sentences
                    yield reply_list
            else:
                yield [cleaned]
        
        if reply_list:
            yield reply_list


def extract_guess_from_text(
    utterance: str,
    lang: str = 'ko',
    server_type: str = 'Local',
    api_key: str = None
) -> str:
    '''발화에서 추측 단어 추출'''
    provider = get_provider(server_type, api_key)
    
    prompt_body = get_extract_guess_prompt(utterance, lang)
    
    full_prompt = f'''<|im_start|>system
{prompt_body}<|im_end|>
<|im_start|>assistant
'''
    
    with generation_lock:
        output = provider.generate(full_prompt, max_tokens=16)
    
    return _parse_generation(output)


# ============================================================================
# 유틸리티
# ============================================================================

def _parse_generation(text: str) -> str:
    '''생성된 텍스트 정리'''
    if not text:
        return ''
    
    # think 태그 제거
    if util_string and hasattr(util_string, 'remove_think_tag'):
        cleaned = util_string.remove_think_tag(text)
    else:
        cleaned = text
        if '</think>' in cleaned:
            _, cleaned = cleaned.split('</think>', 1)
    
    cleaned = cleaned.strip()
    
    # 캐릭터 접두어 제거
    if util_string and hasattr(util_string, 'remove_character_prefix'):
        cleaned = util_string.remove_character_prefix(cleaned)
    
    # 첫 비어있지 않은 줄만 선택 (단어 추출용)
    line = ''
    for l in cleaned.splitlines():
        s = l.strip()
        if s:
            line = s
            break
    
    if not line:
        return cleaned
    
    # 선행 불릿 제거
    for prefix in ['- ', '* ', '• ']:
        if line.startswith(prefix):
            line = line[len(prefix):].strip()
            break
    
    # 따옴표 제거 (단어 추출 결과)
    quotes = ['"', "'", '「', '『', '"', '"']
    if len(line) >= 2 and line[0] in quotes and line[-1] in quotes:
        line = line[1:-1].strip()
    
    return line if line else cleaned


if __name__ == '__main__':
    print('=== ai_20q_llm 테스트 ===')
    print('실제 테스트는 서버에서 실행하세요.')
    
    # Provider 테스트
    print('\n--- Provider Factory ---')
    local = get_provider('Local')
    print(f'Local provider: {type(local).__name__}')
    
    auto = get_provider('Auto')
    print(f'Auto provider: {type(auto).__name__}')
