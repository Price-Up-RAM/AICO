'''
Multi-Character Conversation용 Gemini API 처리
util_gemini.py 기반, get_gemma_multi_prompt 활용
'''
from kei import GEMINI_API_KEY, GEMINI_API_KEYS
import google.generativeai as genai
from threading import Thread, Lock
import re
import time
import random
from typing import List, Dict, Generator, Tuple, Optional

# Local imports
import state
import util_string
import util_proper_nouns
from prompt_llm import get_gemma_multi_prompt
from util_key_manager import GeminiAPIKeyManager

# API 키 매니저 초기화
api_key_manager = GeminiAPIKeyManager(GEMINI_API_KEYS)
current_api_key = api_key_manager.get_current_key()

# API 키 설정
genai.configure(api_key=current_api_key)

llm = None
def load_model(is_use_cuda=False, api_key=None):
    global llm, current_api_key
    if api_key:
        current_api_key = api_key
    genai.configure(api_key=current_api_key)
    llm = genai.GenerativeModel('gemma-3-27b-it')
    # llm = genai.GenerativeModel('gemma-3n-e4b-it')

generation_lock = Lock()
MAX_RETRY = 10

def process_multi_stream(
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
    """다중 캐릭터 대화용 Gemini 스트리밍 처리 함수 - ai_conversation_binary_multi.process_multi_stream 인터페이스"""
    
    # 매개변수 기본값 설정
    participants = participants or []
    context = context or {}
    memory_list = memory_list or []
    guideline_list = guideline_list or []
    situation_dict = situation_dict or {}
    
    # 1단계: Generate 전 고유명사 변환 (사용자 입력 전처리)
    processed_query = util_proper_nouns.apply_proper_nouns(lang, query)
    if processed_query != query:
        print(f"[고유명사 변환] 있음")
    
    # target_speaker가 없으면 기본값 설정 (ai_conversation_binary_multi.py 참조)
    if not target_speaker and len(participants) > 2:
        # AI 참여자 중 첫 번째 선택
        ai_participants = [p for p in participants if p.get("type") == "ai"]
        target_speaker = ai_participants[0]["name"] if ai_participants else "arona"
    
    # 단일 캐릭터 대화 후처리 함수
    def post_process_multi_reply(reply_list: List[str], target_speaker: str, lang: str = 'ko') -> List[str]:
        """다중 캐릭터 응답 후처리 - ai_conversation_binary_multi.py 로직"""
        processed_list = []
        
        for reply in reply_list:
            # ai_conversation_binary.py와 동일한 후처리
            visible_reply = reply
            if player_name:
                visible_reply = re.sub("(<USER>|<user>|{{user}})", player_name, visible_reply)
            else:
                visible_reply = re.sub("(<USER>|<user>|{{user}})", 'You', visible_reply)
            visible_reply = visible_reply.replace("\n",'')
            visible_reply = re.sub(r'\([^)]*\)', '', visible_reply)  # ()와 안의 내용물 제거
            visible_reply = re.sub(r'\[[^)]*\]', '', visible_reply)  # []와 안의 내용물 제거
            visible_reply = re.sub(r'\*[^)]*\*', '', visible_reply)  # * *과 안의 내용물 제거
            visible_reply = visible_reply.lstrip(' ')
            
            # 2단계: AI 응답 후 고유명사 변환 (translate 전/후 통합)
            original_reply = visible_reply
            visible_reply = util_proper_nouns.apply_proper_nouns(lang, visible_reply)
            if original_reply != visible_reply:
                print(f"[응답 고유명사 변환] '{original_reply}' → '{visible_reply}'")
            
            processed_list.append(visible_reply)
        
        return processed_list

    # Gemini 스트리밍 처리
    for reply_list in generate_multi_reply(
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
        player_name=player_name,
        is_sentence=is_sentence,
        is_regenerate=is_regenerate
    ):
        # 후처리
        processed_reply = post_process_multi_reply(reply_list, target_speaker, lang)
        yield (processed_reply, target_speaker)

def generate_multi_reply(*args, **kwargs):
    """멀티 스레딩 보호가 적용된 생성 함수"""
    global generation_lock
    generation_lock.acquire()
    try:
        for result in _generate_multi_reply(*args, **kwargs):
            yield result
    finally:
        generation_lock.release()

def apply_stopping_strings(reply, all_stop_strings = ['\nYou:', '<|im_end|>\n<|im_start|>user\n', '<|im_start|>assistant\n', '\nAI:', '<start_of_turn>user', '<end_of_turn>']):
    """정지 문자열 적용 - util_gemini.py와 동일"""
    stop_found = False
    for string in all_stop_strings:
        idx = reply.find(string)
        if idx != -1:
            reply = reply[:idx]
            stop_found = True
            break

    if not stop_found:
        # If something like "\nYo" is generated just before "\nYou:"
        # is completed, trim it
        for string in all_stop_strings:
            for j in range(len(string) - 1, 0, -1):
                if reply[-j:] == string[:j]:
                    reply = reply[:-j]
                    break
            else:
                continue
            break

    return reply, stop_found

def _generate_multi_reply(
    query: str,
    current_speaker: str,
    target_speaker: str,
    target_listener: str = "all",
    participants: List[Dict] = None,
    context: Dict = None,
    info_img: str = None,
    memory_list: List[Dict] = None,
    lang: str = 'en',
    guideline_list: List = None,
    situation_dict: Dict = None,
    player_name: str = 'sensei',
    is_sentence: bool = True,
    is_regenerate: bool = False,
    temperature: float = 0.7,
    api_key: str = None
):    
    """실제 Gemini API 호출 및 다중 캐릭터 스트리밍 처리"""
    global llm, current_api_key, api_key_manager
    
    # 외부에서 특정 API 키를 지정한 경우
    if api_key and api_key != current_api_key:
        current_api_key = api_key
        load_model(api_key=api_key)
    else:
        # 매 요청마다 키 회전 (외부에서 키를 지정하지 않은 경우)
        current_api_key = api_key_manager.rotate_key()
        load_model(api_key=current_api_key)

    # get_gemma_multi_prompt 사용
    prompt = get_gemma_multi_prompt(
        query=query,
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
    
    print(f'##gemma_multi prompt preview: {prompt[:200]}...')
    
    all_stop_strings = ['\nYou:', '<|im_end|>', '<|im_start|>user', '<|im_start|>assistant\n', '\nAI:', "<|eot_id|>", "< |", "<start_of_turn>user", "<end_of_turn>"]

    is_answer_success = False
    answer_try_cnt = 0
    
    for i in range(MAX_RETRY):
        try:
            reply = ''
            reply_list = list()
            answer_try_cnt = i
            
            # 재시도 시 키 회전 (첫 번째 시도가 아닌 경우)
            if i > 0 and not api_key:  # 외부에서 지정한 키가 아닌 경우만 회전
                current_api_key = api_key_manager.rotate_key()
                load_model(api_key=current_api_key)
            
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
                        
                        # 문장 부호 체크 (is_sentence 모드일 때만)
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
                            reply_list = [reply]  # 단일 응답 모드
                        
                        # 첫 문장 생성중
                        if not reply_list:
                            continue
                        
                        # 멈추라면 그대로 break
                        if state.get_is_stop_requested():       
                            state.set_is_stop_requested(False)
                            break
                        
                        # stop 문 있으면 break
                        if reply_list:
                            _, stop_found = apply_stopping_strings(reply_list[-1], all_stop_strings)  # 마지막 문장만 체크
                            if stop_found:
                                if len(reply_list) >= 1:
                                    reply_list = reply_list[:len(reply_list)-1]
                                break
                        
                        # 문장 수 제한
                        if len(reply_list) >= 20:
                            break
                        
                        yield reply_list                    
                except Exception as e:
                    # print(f"청크 처리 오류: {e}")
                    continue
            
            is_answer_success = True
            break 
            
        except Exception as e:
            print(f"API 호출 오류 (시도 {i+1}, 키 인덱스 {api_key_manager.get_current_index()}): {e}")
            time.sleep(0.1)
    
    # 실패 시 fallback 메시지
    if not is_answer_success:
        fallback_message = get_fallback_message(target_speaker, lang)
        yield [fallback_message]

def get_fallback_message(target_speaker: str, lang: str = 'en') -> str:
    """캐릭터별 fallback 메시지 - ai_conversation_binary_multi.py와 동일"""
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

# 현재 사용 중인 API 키 정보를 확인하는 함수
def get_current_api_key_info():
    return {
        'current_index': api_key_manager.get_current_index(),
        'current_key': current_api_key[:8] + '...' if current_api_key else None  # 보안상 일부만 표시
    }

if __name__ == "__main__":
    # Test - 다중 캐릭터 대화
    print(f"시작 키 인덱스: {api_key_manager.get_current_index()}")
    
    # 아로프라 참여자 생성
    participants = [
        {"name": "sensei", "type": "user", "display_name": "선생님"},
        {"name": "arona", "type": "ai", "display_name": "아로나", "character_file": "arona"},
        {"name": "plana", "type": "ai", "display_name": "프라나", "character_file": "plana"}
    ]
    
    # 한국어 다중 캐릭터 Test
    if True:
        question = "아로나야, 오늘 기분 어때?"
        current_speaker = "sensei"
        target_speaker = "arona"
        
        print(f"\n=== Multi-Character Test ===")
        print(f"Query: {question}")
        print(f"Participants: {[p['display_name'] for p in participants]}")
        print(f"Flow: {current_speaker} -> {target_speaker}")
        
        reply_len = 0
        final_reply = []
        
        for j, (reply_list, responding_speaker) in enumerate(process_multi_stream(
            query=question,
            current_speaker=current_speaker,
            target_speaker=target_speaker,
            target_listener="sensei",
            participants=participants,
            context={"description": "아로프라 채널 테스트 대화"},
            is_sentence=True,
            memory_list=[],
            lang='ko',
            player_name='sensei'
        )):
            if reply_len < len(reply_list):                
                reply_len = len(reply_list)
                final_reply = reply_list
            pass
        
        print(f'[{target_speaker}] reply_list: {final_reply}')
        print(f"최종 사용 키 인덱스: {api_key_manager.get_current_index()}")
