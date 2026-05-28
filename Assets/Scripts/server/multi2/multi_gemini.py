'''
multi_gemini.py
Gemini API 전용 통합 모듈

대화 생성 + Flow Director 모두 Gemini API 사용
util_gemini_multi.py를 참조하여 구현
'''
from typing import List, Dict, Generator, Tuple, Optional
from threading import Lock
import re
import time

# Gemini API
import google.generativeai as genai
from kei import GEMINI_API_KEY, GEMINI_API_KEYS
from util_key_manager import GeminiAPIKeyManager

# 공통 프롬프트 모듈
from multi_prompts import (
    get_multi_character_messages,
    get_target_speaker_prompt,
    get_flow_decision_prompt,
    get_target_listener_prompt,
    parse_target_speaker_response,
    parse_flow_decision_response,
    parse_target_listener_response,
    format_gemma_prompt,
    get_display_name
)

# 유틸리티
import state
import util_string
import util_proper_nouns

# API 키 매니저 초기화
api_key_manager = GeminiAPIKeyManager(GEMINI_API_KEYS)
current_api_key = api_key_manager.get_current_key()

# API 키 설정
genai.configure(api_key=current_api_key)

# 모델 인스턴스
llm = None
generation_lock = Lock()
MAX_RETRY = 10

# AI 연속 대화 허용 최대 횟수
DEFAULT_MAX_AI_CONSECUTIVE = 10


def load_model(api_key: str = None):
    """Gemini 모델 로딩"""
    global llm, current_api_key
    if api_key:
        current_api_key = api_key
    genai.configure(api_key=current_api_key)
    llm = genai.GenerativeModel('gemma-3-27b-it')


def _rotate_key_and_reload():
    """키 회전 및 모델 리로드"""
    global current_api_key
    current_api_key = api_key_manager.rotate_key()
    load_model(api_key=current_api_key)


# ============================================================================
# 대화 생성 (스트리밍)
# ============================================================================

def process_conversation_stream(
    query: str,
    current_speaker: str,
    target_speaker: str = None,
    target_listener: str = "all",
    participants: List[Dict] = None,
    context: Dict = None,
    is_sentence: bool = True,
    is_regenerate: bool = False,
    info_img: str = None,
    memory_list: List[Dict] = None,
    lang: str = 'en',
    guideline_list: List = None,
    situation_dict: Dict = None,
    player_name: str = 'sensei',
    **kwargs
) -> Generator[Tuple[List[str], str], None, None]:
    """
    다중 캐릭터 대화용 Gemini 스트리밍 처리 함수
    
    Returns:
        Generator yielding (reply_list, responding_speaker) tuples
    """
    # 기본값 설정
    participants = participants or []
    context = context or {}
    memory_list = memory_list or []
    guideline_list = guideline_list or []
    situation_dict = situation_dict or {}
    
    # 고유명사 변환 (전처리)
    processed_query = util_proper_nouns.apply_proper_nouns(lang, query)
    if processed_query != query:
        print(f"[고유명사 변환] 적용됨")
    
    # target_speaker 기본값
    if not target_speaker and len(participants) > 2:
        ai_participants = [p for p in participants if p.get("type") == "ai"]
        target_speaker = ai_participants[0]["name"] if ai_participants else "arona"
    
    # 메시지 생성 (multi_prompts 사용)
    messages = get_multi_character_messages(
        query=processed_query,
        current_speaker=current_speaker,
        target_speaker=target_speaker,
        target_listener=target_listener,
        participants=participants,
        context=context,
        info_img=info_img,
        memory_list=memory_list,
        lang=lang,
        guideline_list=guideline_list,
        situation_dict=situation_dict,
        player_name=player_name
    )
    
    # Gemma 포맷으로 변환
    prompt = format_gemma_prompt(messages)
    
    print(f'##gemini_multi prompt preview: {prompt[:200]}...')
    
    # 스트리밍 생성
    for reply_list in _generate_stream(
        prompt=prompt,
        target_speaker=target_speaker,
        is_sentence=is_sentence,
        lang=lang,
        player_name=player_name
    ):
        # 후처리
        processed_reply = _post_process_reply(reply_list, target_speaker, lang, player_name)
        yield (processed_reply, target_speaker)


def _generate_stream(
    prompt: str,
    target_speaker: str,
    is_sentence: bool = True,
    lang: str = 'en',
    player_name: str = 'sensei',
    temperature: float = 0.7
) -> Generator[List[str], None, None]:
    """실제 Gemini API 스트리밍 호출"""
    global llm, current_api_key
    
    # 모델 로딩 확인
    if not llm:
        _rotate_key_and_reload()
    else:
        _rotate_key_and_reload()
    
    all_stop_strings = ['\nYou:', '<|im_end|>', '<|im_start|>user', '\nAI:', "<|eot_id|>", 
                        "<start_of_turn>user", "<end_of_turn>"]
    
    is_answer_success = False
    
    with generation_lock:
        for i in range(MAX_RETRY):
            try:
                reply = ''
                reply_list = []
                
                if i > 0:
                    _rotate_key_and_reload()
                
                # Gemini 스트리밍 호출
                response = llm.generate_content(
                    prompt,
                    generation_config={
                        "temperature": temperature,
                        "top_p": 0.9,
                        "max_output_tokens": 1024,
                    },
                    stream=True
                )
                
                for chunk in response:
                    try:
                        content = None
                        if hasattr(chunk, 'text') and chunk.text:
                            content = chunk.text
                        elif hasattr(chunk, 'candidates') and chunk.candidates:
                            candidate = chunk.candidates[0]
                            if (hasattr(candidate, 'content') and
                                hasattr(candidate.content, 'parts') and
                                isinstance(candidate.content.parts, list) and
                                len(candidate.content.parts) > 0 and
                                hasattr(candidate.content.parts[0], 'text')):
                                content = candidate.content.parts[0].text
                        
                        if content:
                            reply += content
                            
                            # 문장 부호 체크
                            if is_sentence:
                                is_punc = False
                                if reply:
                                    for punc in util_string.STREAMING_PUNCS:
                                        if punc in reply[-3:]:
                                            is_punc = True
                                            break
                                if not is_punc:
                                    continue
                            
                            # 문장 분리
                            if is_sentence:
                                reply_list = util_string.get_punctuation_sentences(reply)
                            else:
                                reply_list = [reply]
                            
                            if not reply_list:
                                continue
                            
                            # 정지 요청 확인
                            if state.get_is_stop_requested():
                                state.set_is_stop_requested(False)
                                break
                            
                            # stop 문자열 체크
                            if reply_list:
                                _, stop_found = _apply_stopping_strings(reply_list[-1], all_stop_strings)
                                if stop_found:
                                    if len(reply_list) >= 1:
                                        reply_list = reply_list[:len(reply_list)-1]
                                    break
                            
                            # 문장 수 제한
                            if len(reply_list) >= 20:
                                break
                            
                            yield reply_list
                            
                    except Exception as e:
                        continue
                
                is_answer_success = True
                break
                
            except Exception as e:
                print(f"API 호출 오류 (시도 {i+1}, 키 인덱스 {api_key_manager.get_current_index()}): {e}")
                time.sleep(0.1)
    
    # 실패 시 fallback
    if not is_answer_success:
        fallback_message = _get_fallback_message(target_speaker, lang)
        yield [fallback_message]


def _apply_stopping_strings(reply: str, all_stop_strings: List[str]) -> Tuple[str, bool]:
    """정지 문자열 적용"""
    stop_found = False
    for string in all_stop_strings:
        idx = reply.find(string)
        if idx != -1:
            reply = reply[:idx]
            stop_found = True
            break

    if not stop_found:
        for string in all_stop_strings:
            for j in range(len(string) - 1, 0, -1):
                if reply[-j:] == string[:j]:
                    reply = reply[:-j]
                    break
            else:
                continue
            break

    return reply, stop_found


def _post_process_reply(reply_list: List[str], target_speaker: str, lang: str, player_name: str) -> List[str]:
    """응답 후처리"""
    processed_list = []
    
    for reply in reply_list:
        visible_reply = reply
        if player_name:
            visible_reply = re.sub("(<USER>|<user>|{{user}})", player_name, visible_reply)
        else:
            visible_reply = re.sub("(<USER>|<user>|{{user}})", 'You', visible_reply)
        visible_reply = visible_reply.replace("\n", '')
        visible_reply = re.sub(r'\([^)]*\)', '', visible_reply)
        visible_reply = re.sub(r'\[[^)]*\]', '', visible_reply)
        visible_reply = re.sub(r'\*[^)]*\*', '', visible_reply)
        visible_reply = visible_reply.lstrip(' ')
        
        # 고유명사 변환
        original_reply = visible_reply
        visible_reply = util_proper_nouns.apply_proper_nouns(lang, visible_reply)
        if original_reply != visible_reply:
            print(f"[응답 고유명사 변환] 적용됨")
        
        processed_list.append(visible_reply)
    
    return processed_list


def _get_fallback_message(target_speaker: str, lang: str = 'en') -> str:
    """캐릭터별 fallback 메시지"""
    fallback_messages = {
        'ko': {
            'arona': "음... 잘 이해가 안 돼요, 선생님.",
            'plana': "처리 중 오류가 발생했습니다.",
            'default': "죄송해요, 다시 말씀해 주시겠어요?"
        },
        'ja': {
            'arona': "うーん... よく分からないです、先生。",
            'plana': "処理中にエラーが発生しました。",
            'default': "すみません、もう一度お願いします。"
        },
        'en': {
            'arona': "Um... I don't quite understand, Sensei.",
            'plana': "An error occurred during processing.",
            'default': "I'm sorry, could you repeat that?"
        }
    }
    
    lang_key = 'ja' if lang in ['ja', 'jp'] else lang
    messages = fallback_messages.get(lang_key, fallback_messages['en'])
    return messages.get(target_speaker, messages['default'])


# ============================================================================
# Flow Director 함수들 (Gemini API 사용)
# ============================================================================

def analyze_target_speaker(
    message: str,
    current_speaker: str = "sensei",
    lang: str = 'en',
    memory_list: List[Dict] = None
) -> Tuple[Optional[str], str]:
    """
    사용자 메시지를 분석하여 누구에게 말하고 있는지 판단 (Gemini API)
    
    Returns:
        (target_speaker, reason) 튜플
    """
    start_time = time.time()
    memory_info = f" (메모리: {len(memory_list) if memory_list else 0}턴)" if memory_list else ""
    print(f"[Gemini Target Analysis] 시작: '{message[:30]}...' ({lang}){memory_info}")
    
    # 프롬프트 생성
    prompt_body = get_target_speaker_prompt(message, memory_list, lang)
    
    # Gemma 포맷으로 래핑
    full_prompt = f"<bos><start_of_turn>user\n{prompt_body}<end_of_turn>\n<start_of_turn>model\n"
    
    # Gemini 호출
    with generation_lock:
        try:
            if not llm:
                _rotate_key_and_reload()
            
            response = llm.generate_content(
                full_prompt,
                generation_config={
                    "temperature": 0.1,
                    "max_output_tokens": 50,
                }
            )
            
            output = response.text.strip() if hasattr(response, 'text') else ''
            
        except Exception as e:
            print(f"[Gemini Target Analysis] API 오류: {e}")
            return 'arona', 'API 오류 - 기본값 선택'
    
    # 파싱
    target, reason = parse_target_speaker_response(output)
    
    total_time = time.time() - start_time
    print(f"[Gemini Target Analysis] 완료 ({total_time:.2f}s): {target} - {reason}")
    
    # 기본값 처리
    if not target or target not in ['arona', 'plana']:
        target = 'arona'
        reason = f"잘못된 응답으로 기본 선택 - {reason}"
    
    return target, f"Gemini 분석: {reason}"


def decide_next_speaker(
    memory_list: List[Dict] = None,
    query: str = "",
    final_response: str = "",
    current_speaker: str = None,
    query_speaker: str = None,
    lang: str = 'en',
    max_ai_consecutive: int = DEFAULT_MAX_AI_CONSECUTIVE
) -> Tuple[str, str]:
    """
    대화 흐름을 분석하여 다음 발화자를 결정 (Gemini API)
    
    Returns:
        (next_speaker, reason) 튜플
    """
    start_time = time.time()
    memory_list = memory_list or []
    print(f"[Gemini Flow Decision] 시작: {len(memory_list)}턴 분석 ({lang})")
    print(f"[Gemini Flow Decision] 현재 발화자: {current_speaker}")
    
    # 프롬프트 생성
    prompt_body = get_flow_decision_prompt(
        memory_list, query, final_response, current_speaker, query_speaker, lang
    )
    
    # Gemma 포맷으로 래핑
    full_prompt = f"<bos><start_of_turn>user\n{prompt_body}<end_of_turn>\n<start_of_turn>model\nnext_speaker: "
    
    # Gemini 호출
    with generation_lock:
        try:
            if not llm:
                _rotate_key_and_reload()
            
            response = llm.generate_content(
                full_prompt,
                generation_config={
                    "temperature": 0.1,
                    "max_output_tokens": 50,
                }
            )
            
            output = "next_speaker: " + (response.text.strip() if hasattr(response, 'text') else '')
            
        except Exception as e:
            print(f"[Gemini Flow Decision] API 오류: {e}")
            return 'sensei', 'API 오류 - 선생님께 턴 넘김'
    
    # 파싱
    next_speaker, reason = parse_flow_decision_response(output)
    
    # 동일 발화자 방지
    if next_speaker == current_speaker:
        original = next_speaker
        next_speaker = 'sensei'
        reason = f"동일 발화자 방지: {original} → sensei 자동 변경"
        print(f"[Gemini Flow Decision] 동일 발화자 감지! '{original}' → 'sensei'로 변경")
    
    # AI 연속 대화 방지
    if next_speaker != 'sensei':
        current_is_user = (query_speaker == 'sensei')
        
        if current_is_user:
            print(f"[Gemini Flow Decision] 현재 턴이 user 턴이므로 연속 대화 방지 불필요")
        elif len(memory_list) >= max_ai_consecutive:
            recent_turns = memory_list[-max_ai_consecutive:]
            
            all_roles = [entry.get('role') for entry in recent_turns]
            all_non_user = all(role != 'user' for role in all_roles)
            
            if all_non_user:
                original = next_speaker
                next_speaker = 'sensei'
                reason = f"AI 연속 방지: {original} → sensei 강제 변경"
                print(f"[Gemini Flow Decision] AI 연속 감지! '{original}' → 'sensei'로 변경")
    
    total_time = time.time() - start_time
    print(f"[Gemini Flow Decision] 완료 ({total_time:.2f}s): {next_speaker}")
    
    return next_speaker, reason


def analyze_target_listener(
    message: str,
    current_speaker: str = "sensei",
    target_speaker: str = None,
    lang: str = 'en',
    memory_list: List[Dict] = None
) -> Tuple[str, str]:
    """
    메시지 분석을 통해 target_speaker가 누구에게 응답해야 하는지 결정 (Gemini API)
    
    Returns:
        (target_listener, reason) 튜플
    """
    start_time = time.time()
    memory_info = f" (메모리: {len(memory_list) if memory_list else 0}턴)" if memory_list else ""
    print(f"[Gemini Listener Analysis] 시작: {target_speaker} 응답 대상 분석 '{message[:30]}...' ({lang}){memory_info}")
    
    # 프롬프트 생성
    prompt_body = get_target_listener_prompt(message, current_speaker, target_speaker, memory_list, lang)
    
    # Gemma 포맷으로 래핑
    full_prompt = f"<bos><start_of_turn>user\n{prompt_body}<end_of_turn>\n<start_of_turn>model\n"
    
    # Gemini 호출
    with generation_lock:
        try:
            if not llm:
                _rotate_key_and_reload()
            
            response = llm.generate_content(
                full_prompt,
                generation_config={
                    "temperature": 0.1,
                    "max_output_tokens": 50,
                }
            )
            
            output = response.text.strip() if hasattr(response, 'text') else ''
            
        except Exception as e:
            print(f"[Gemini Listener Analysis] API 오류: {e}")
            return 'all', 'API 오류'
    
    # 파싱
    target_listener, reason = parse_target_listener_response(output)
    
    total_time = time.time() - start_time
    print(f"[Gemini Listener Analysis] 완료 ({total_time:.2f}s): {target_listener} - {reason}")
    
    # 유효성 검증
    valid_listeners = ["sensei", "arona", "plana", "all"]
    if target_listener not in valid_listeners:
        target_listener = "all"
        reason = f"잘못된 응답으로 기본 선택 - {reason}"
    
    return target_listener, f"Gemini 분석: {reason}"


def determine_target_listener_from_context(
    current_speaker: str,
    target_speaker: str,
    message: str = "",
    memory_list: List[Dict] = None,
    lang: str = 'en'
) -> Tuple[str, str]:
    """대화 맥락에서 청취자 결정 (규칙 기반 - API 미사용)"""
    print(f"[Context Listener] 맥락 분석: {current_speaker} -> {target_speaker}")
    
    if current_speaker == "sensei":
        if target_speaker in ["arona", "plana"]:
            return target_speaker, f"선생님 -> {target_speaker} 개별 대화"
        else:
            return "all", "선생님의 전체 발언"
    
    elif current_speaker in ["arona", "plana"]:
        if target_speaker == "sensei":
            return "sensei", f"{current_speaker} -> 선생님 개별 응답"
        elif target_speaker in ["arona", "plana"] and target_speaker != current_speaker:
            return target_speaker, f"{current_speaker} -> {target_speaker} AI끼리 대화"
        else:
            return "all", f"{current_speaker}의 전체 발언"
    
    return "all", "맥락 불분명 - 전체 대화로 설정"


# ============================================================================
# 현재 API 키 정보
# ============================================================================

def get_current_api_key_info() -> Dict:
    """현재 사용 중인 API 키 정보"""
    return {
        'current_index': api_key_manager.get_current_index(),
        'current_key': current_api_key[:8] + '...' if current_api_key else None
    }


# ============================================================================
# 테스트
# ============================================================================

if __name__ == "__main__":
    print(f"=== multi_gemini.py 테스트 ===")
    print(f"시작 키 인덱스: {api_key_manager.get_current_index()}")
    
    # 참가자 생성
    participants = [
        {"name": "sensei", "type": "user", "display_name": "선생님"},
        {"name": "arona", "type": "ai", "display_name": "아로나", "character_file": "arona"},
        {"name": "plana", "type": "ai", "display_name": "프라나", "character_file": "plana"}
    ]
    
    # 대화 스트리밍 테스트
    if True:
        question = "아로나야, 오늘 기분 어때?"
        current_speaker = "sensei"
        target_speaker = "arona"
        
        print(f"\n=== Multi-Character Gemini Test ===")
        print(f"Query: {question}")
        print(f"Flow: {current_speaker} -> {target_speaker}")
        
        reply_len = 0
        final_reply = []
        
        for j, (reply_list, responding_speaker) in enumerate(process_conversation_stream(
            query=question,
            current_speaker=current_speaker,
            target_speaker=target_speaker,
            target_listener="sensei",
            participants=participants,
            context={"description": "테스트 대화"},
            is_sentence=True,
            memory_list=[],
            lang='ko',
            player_name='sensei'
        )):
            if reply_len < len(reply_list):
                reply_len = len(reply_list)
                final_reply = reply_list
        
        print(f'[{target_speaker}] reply_list: {final_reply}')
    
    # Flow Director 테스트
    if True:
        print(f"\n=== Flow Director Test ===")
        
        # 타겟 분석
        target, reason = analyze_target_speaker(
            message="프라나는 어떻게 생각해?",
            current_speaker="sensei",
            lang='ko'
        )
        print(f"타겟 분석: {target} - {reason}")
        
        # 다음 발화자 결정
        next_speaker, reason = decide_next_speaker(
            memory_list=[{"speaker": "arona", "message": "안녕하세요!", "role": "assistant"}],
            query="오늘 뭐해?",
            final_response="저도 잘 모르겠어요~",
            current_speaker="arona",
            query_speaker="sensei",
            lang='ko'
        )
        print(f"다음 발화자: {next_speaker} - {reason}")
    
    print(f"\n최종 사용 키 인덱스: {api_key_manager.get_current_index()}")
