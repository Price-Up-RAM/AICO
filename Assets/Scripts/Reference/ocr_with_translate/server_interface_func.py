"""
server_interface.py의 헬퍼 함수들
conversation_stream 관련 업무 로직을 모듈화
"""

import json
import state
from flask import request
from datetime import datetime
from PIL import Image
from io import BytesIO
from pprint import pprint

# ai 관련 모듈들
import ai_florence
import ai_intent_web
import ai_intent_image
import ai_intent_confirm
import ai_intent_turn_light
import util_IoT_SwitchBot
from util_string import detect_language
import prompt_char


def parse_conversation_params(server_config):
    """conversation_stream 요청 파라미터 파싱 및 기본값 설정"""
    params = {}
    
    # 기본 파라미터
    params['query'] = request.form.get('query')  # 질문 내용
    params['player_name'] = request.form.get('player', 'sensei')
    params['char_name'] = request.form.get('char', 'arona')
    
    # 언어 관련 파라미터
    params['ai_language'] = request.form.get('ai_language', 'en')  #  추론에 쓸 언어. 설정언어(normal),모델추천(prefer),한국어,영어,일본어
    params['ai_language_in'] = request.form.get('ai_language_in', '')  #  STT 등을 통해, 해당언어가 무엇인지 알 수 있으면 번역필요가 없음. (whisper은 ja 코드)
    params['ai_language_out'] = request.form.get('ai_language_out', 'en')  # 메모리에 저장할 언어 (표시언어와 다름)
    params['sound_language'] = request.form.get('sound_language', 'jp')  # 사용언어
    
    # API 키
    params['api_key_Gemini'] = request.form.get('api_key_Gemini', '')
    params['api_key_OpenRouter'] = request.form.get('api_key_OpenRouter', '')
    params['api_key_ChatGPT'] = request.form.get('api_key_ChatGPT', '')
    
    # 대화 컨텍스트 - memory
    params['memory'] = request.form.get('memory')  # memory 값을 요청에서 가져옴
    
    # 대화 컨텍스트 - guideline_list
    raw_guideline = request.form.get('guideline_list', '[]')  # user_card에서 guideline_list 값을 요청에서 가져옴
    try:
        params['guideline_list'] = json.loads(raw_guideline)
    except json.JSONDecodeError:
        params['guideline_list'] = []
    
    # 대화 컨텍스트 - situation
    raw_situation = request.form.get('situation', '')
    if raw_situation:
        try:
            params['situation_dict'] = json.loads(raw_situation)
        except json.JSONDecodeError as e:
            print(f"[Warning] Situation JSON 파싱 실패: {e}")
            params['situation_dict'] = {}
    else:
        params['situation_dict'] = {}
    
    # 채팅 관련
    params['chat_idx'] = request.form.get('chatIdx', '-1')
    params['regenerate_count'] = request.form.get('regenerate_count', '0')  # regenerate 횟수
    params['is_regenerate'] = bool(params['regenerate_count'])
    
    # Intent 관련 파라미터
    params['ai_emotion'] = request.form.get('ai_emotion', 'off')  # on, off / 표정 반영
    params['intent_web'] = request.form.get('intent_web', 'off')  # on, off, force
    params['intent_image'] = request.form.get('intent_image', 'off')  # on, off, force
    params['intent_confirm'] = request.form.get('intent_confirm', 'off')  # on, off / 의도행동에 확인 받기[web검색하실까요 선생님?]
    params['intent_confirm_type'] = request.form.get('intent_confirm_type', '')  # "", web, light : 의도행동확인 종류
    params['intent_confirm_answer'] = request.form.get('intent_confirm_answer', '')  # true, false : 의도행동확인에 대한 답변[재생성시 확인 없이 적용하기 위해]
    params['intent_guideline'] = request.form.get('intent_guideline', 'off')  # on, off / guideline의 신규작성에 ai 활용
    params['intent_smalltalk_answer'] = request.form.get('intent_smalltalk_answer', 'off')  # on, off / 잡담 답변 가능성
    params['query_smalltalk'] = request.form.get('query_smalltalk', '')  # AI쪽에서 보낸 잡담
    
    # server_type (Unity C# 정의: 0=Auto, 1=Local, 2=Google, 3=OpenRouter, 4=Custom)
    # 1. request.form에서 값 가져오기 (Unity든 아니든 항상 확인)
    params['server_type'] = request.form.get('server_type', '')  # "Auto", "Local", "Google", "OpenRouter", "Custom"
    
    # 2. 각 서비스별 모델명 (서비스마다 모델 식별 체계가 다름)
    params['model_name_Local'] = request.form.get('model_name_Local', '')  # GGUF 파일명 (예: "Qwen3-8B-Q4_K_M.gguf")
    params['model_name_Gemini'] = request.form.get('model_name_Gemini', '')  # Gemini 모델 (예: "gemma-3-27b-it, gemini-1.5-flash")
    params['model_name_OpenRouter'] = request.form.get('model_name_OpenRouter', '')  # OpenRouter 모델 (예: "google/gemma-3-27b-it")
    params['model_name_ChatGPT'] = request.form.get('model_name_ChatGPT', '')  # ChatGPT 모델 (예: "gpt-4")
    params['model_name_Custom'] = request.form.get('model_name_Custom', '')  # Custom 모델 (예: "qwen-38b", "qwen-42b")
    params['server_local_mode'] = request.form.get('server_local_mode', '')  # CPU, GPU : 로컬 모델 실행 모드
    
    # 3. server_type 우선순위: 외부 파라미터 > (DEV_MODE일 때만 config) > 기본값
    if not params['server_type']:
        # DEV_MODE일 때만 server_config 사용
        if state.get_DEV_MODE() and 'server_type' in server_config and server_config['server_type']:
            params['server_type'] = server_config['server_type']
        else:
            params['server_type'] = 'Auto'  # 최종 기본값
    
    # 4. 각 모델명이 없으면 config → state → 기본값 순서
    if not params['model_name_Local']:
        if 'model_type' in server_config:
            params['model_name_Local'] = server_config['model_type']
        elif hasattr(state, 'model_name'):
            params['model_name_Local'] = state.model_name
        else:
            params['model_name_Local'] = 'Qwen3-14B-Q4_K_M.gguf'  # 최종 기본값
    
    if not params['model_name_Gemini']:
        params['model_name_Gemini'] = 'gemma-3-27b-it'  # Gemini 기본값
    
    if not params['model_name_OpenRouter']:
        params['model_name_OpenRouter'] = 'gemma-3-27b-it'  # OpenRouter 기본값
    
    if not params['model_name_ChatGPT']:
        params['model_name_ChatGPT'] = 'gpt-4-turbo'  # ChatGPT 기본값
    
    # 5. Auto 타입 처리 (가용한 서비스 자동 선택)
    if params['server_type'] == 'Auto':
        # API 키가 있으면 해당 서비스 사용, 없으면 Local
        if params['api_key_Gemini']:
            params['server_type'] = "Google"
        elif params['api_key_OpenRouter']:
            params['server_type'] = "OpenRouter"
        else:
            params['server_type'] = "Google"
        if state.get_DEV_MODE():
            print(f'### Auto server_type resolved to: {params["server_type"]}')
    
    # 6. 실제 사용 중인 모델명 결정
    params['using_model_name'] = get_using_model_name(params)
    
    if state.get_DEV_MODE():
        print('### params')
        pprint(params, width=120, sort_dicts=False)
    
    return params


def get_using_model_name(params):
    """server_type에 따라 실제 사용 중인 모델명 반환"""
    server_type = params.get('server_type', '')
    model_name_Local = params.get('model_name_Local', '')
    model_name_Gemini = params.get('model_name_Gemini', '')
    model_name_OpenRouter = params.get('model_name_OpenRouter', '')
    model_name_ChatGPT = params.get('model_name_ChatGPT', '')
    model_name_Custom = params.get('model_name_Custom', '')
    
    if server_type == "Local":
        return model_name_Local
    elif server_type == "Google":
        return model_name_Gemini
    elif server_type == "OpenRouter":
        return model_name_OpenRouter
    elif server_type == "ChatGPT":
        return model_name_ChatGPT
    elif server_type == "Custom":
        return model_name_Custom
    else:
        # Auto 또는 기타: 기본적으로 Local 모델명 반환
        return model_name_Local


def get_custom_model_provider(model_name_custom):
    """Custom 모델명에 따라 사용할 provider와 실제 모델명 반환
    
    Args:
        model_name_custom: Custom 모델명 (예: "qwen-38b", "qwen-42b")
    
    Returns:
        dict: {
            'provider': "Google", "OpenRouter", "ChatGPT" 등
            'model': 실제 API에서 사용할 모델명
            'api_key_type': 필요한 API 키 타입
        }
    """
    # Custom 모델 매핑 테이블
    CUSTOM_MODEL_MAP = {
        "qwen-38b": {
            "provider": "OpenRouter",
            "model": "qwen/qwen-2.5-72b-instruct",
            "api_key_type": "api_key_OpenRouter"
        },
        "qwen-42b": {
            "provider": "Google",
            "model": "gemini-1.5-pro",
            "api_key_type": "api_key_Gemini"
        },
        "claude-sonnet": {
            "provider": "OpenRouter",
            "model": "anthropic/claude-3.5-sonnet",
            "api_key_type": "api_key_OpenRouter"
        },
        "gpt-4o": {
            "provider": "ChatGPT",
            "model": "gpt-4o",
            "api_key_type": "api_key_ChatGPT"
        },
    }
    
    model_name_lower = model_name_custom.lower()
    
    # 매핑 테이블에서 찾기
    if model_name_lower in CUSTOM_MODEL_MAP:
        return CUSTOM_MODEL_MAP[model_name_lower]
    
    # 기본값: OpenRouter (매핑되지 않은 모델은 그대로 OpenRouter로 전달)
    print(f"[Warning] Unknown custom model: {model_name_custom}, using OpenRouter as fallback")
    return {
        "provider": "OpenRouter",
        "model": model_name_custom,
        "api_key_type": "api_key_OpenRouter"
    }


def set_default_response_format_ai_info():
    """응답에 포함될 AI 메타정보 딕셔너리 기본 형식 설정"""
    ai_info = dict()
    ai_info['server_type'] = ''      # 사용된 서버 타입 (Local/Google/OpenRouter)
    ai_info['model'] = ''             # 사용된 모델명
    ai_info['prompt'] = ''            # 사용된 프롬프트 (lang/char)
    ai_info['lang_used'] = ''         # 추론에 사용된 언어 (en/ko/ja)
    ai_info['translator'] = ''        # 사용된 번역기 (DeepL/Google)
    ai_info['time'] = ''              # 처리 시간 (초)
    ai_info['intent'] = 'None'        # 의도 유형 (web/image/lightOn/lightOff/None)
    ai_info['emotion'] = ''           # 감정 분석 결과 (joy/anger/confusion/sadness/surprise/neutral)
    return ai_info


def set_default_response_format_intent_info():
    """응답에 포함될 Intent 정보 딕셔너리 기본 형식 설정"""
    intent_info = dict()
    intent_info['is_intent_web'] = 'off'           # 웹 검색 의도 감지 여부
    intent_info['web_info'] = ''                   # 웹 검색 결과 정보
    intent_info['web_search_keyword'] = ''         # 웹 검색에 사용된 키워드
    intent_info['web_search_detail'] = 'false'     # 웹 검색 상세 정보 포함 여부
    intent_info['is_intent_image'] = 'off'         # 이미지 분석 의도 감지 여부
    intent_info['image_info'] = ''                 # 이미지 분석 결과
    intent_info['is_intent_smalltalk_answer'] = 'off'  # 잡담 답변 가능성
    intent_info['smalltalk_query'] = ''            # AI가 생성한 잡담 내용
    return intent_info


# 업로드 된 이미지를 Florence로 분석하여 텍스트 반환
def process_uploaded_image(request_files, intent_image, intent_info):
    import os
    
    image_info_text = ''
    uploaded_file = request_files.get('image')  # "image"라는 키로 파일이 전달됨
    if uploaded_file and intent_image != 'off':
        image = Image.open(BytesIO(uploaded_file.read())).convert("RGB")
        image_info_text = ai_florence.get_image_info_from_image(image)
        # print('###image_info_text', image_info_text)
        intent_info['image_info_text'] = image_info_text
        
        # Test용 파일저장
        if state.get_DEV_MODE():
            file_name = str(datetime.now().strftime("%y%m%d_%H%M%S")) + "_" + uploaded_file.filename
            file_path = os.path.join('./test/screenshot', file_name)  # 충돌방지용
            os.makedirs('./test/screenshot', exist_ok=True) 
            try:
                # 이미지 저장
                image.save(file_path)  # 이미지 형식은 PIL이 자동으로 결정
                print(f"파일이 저장됨: {file_path}")
            except Exception as e:
                print(f"파일 저장 실패: {e}")
    
    return image_info_text


def detect_and_prepare_language(query, ai_language, ai_language_in):
    """언어 감지 및 번역 준비"""
    # 실험적 : 입력언어 미 판별상태에서 사용언어를 일본어, 영어, 한국어로 뚜렷히 지정했을 경우 입력언어도 동일하다고 추측
    if not ai_language_in:
        if ai_language == 'ko':
            ai_language_in = 'ko'
        if ai_language == 'jp' or ai_language == 'ja':
            ai_language_in = 'jp'
                
    lang_infer_type = ai_language_in  # 추론 언어 종류(jp, ko, en)
    lang_infer = query  # 추론 쿼리
    
    is_query_en_translated = False  # 영어 질문은 Web 검색, Florence 등에서 사용
    query_en = query
    if not ai_language_in: # 입력언어 없을 경우 detect
        lang_infer_type = detect_language(query)
        if lang_infer_type == 'en':
            query_en = query
            is_query_en_translated = True
    
    return lang_infer_type, lang_infer, is_query_en_translated, query_en


def check_intent_all_unified(query_en, intent_web, intent_image, image_description, intent_confirm, intent_confirm_type, server_type):
    """의도파악 (의도 중 하나를 반환)"""
    if not state.get_DEV_MODE() and server_type in ("OpenRouter", "Google"):  # Local일때만 사용
        return None
    if intent_web == 'force':
        return 'web'
    if intent_image == 'force':
        return 'image'
    if intent_web == 'off' and intent_image == 'off':
        return None

    # 0순위: 의도에 대한 답 여부
    if intent_confirm_type != '':
        intent_response = ai_intent_confirm.process(query_en)
        if "Intent: Yes" in intent_response:  # Web검색을할까요 선생님? > (응) : 의도 YES
            return intent_confirm_type 
    
    # Web 검색 의도 파악
    if intent_web == 'on':
        intent_response = ai_intent_web.process(query_en)
        if "web: True" in intent_response:  # Web검색이 필요한 질문
            return 'web'
        
    # 이미지와 관련된 질문인지 파악
    if intent_image == 'on':
        intent_response = ai_chk_image_relevance.process(query_en, image_description)
        if "related: True" in intent_response:
            return 'image'     
        
    # (IoT 모드) 불을 꺼줄지 켜줄지 고민
    if False:
        intent_response = ai_intent_turn_light.process(query_en)
        if 'Light: On' in intent_response:
            util_IoT_SwitchBot.command("turnOff")
            return 'lightOn'
        elif 'Light: Off' in intent_response:
            util_IoT_SwitchBot.command("turnOn")
            return 'lightOff'
        
    # 평범한 문답
    return None


def check_individual_intent_web(query, intent_web, lang_infer_type):
    """개별 웹 검색 의도 체크"""
    if intent_web == 'force':
        return True
    if intent_web == 'off':
        return False
    intent_response = ai_intent_web.process(query, lang=lang_infer_type)
    if "web: True" in intent_response:  # Web검색이 필요한 질문
        return True
    return False


def check_individual_intent_image(query, image_info, intent_image, lang_infer_type, image_info_text=None):
    """개별 이미지 관련 의도 체크"""
    if intent_image == 'force':
        return True
    if intent_image == 'off':
        return False
    
    use_florence = False  # 멀티모달 모드에서는 ai_intent_image 사용
    
    if use_florence:
        # 레거시: Florence를 사용할 때 (image_info_text는 이미지 설명 텍스트)
        import ai_chk_image_relevance
        intent_response = ai_chk_image_relevance.process(query, image_info_text, lang=lang_infer_type)
        if "related: True" in intent_response:  # 이미지 관련 질문
            return True
    else:
        # 멀티모달 모드: ai_intent_image 사용 (질문만으로 이미지 필요 여부 판단)
        intent_response = ai_intent_image.process(query, lang=lang_infer_type)
        if "related: True" in intent_response:
            return True
    
    return False


def check_individual_intent_smalltalk_answer(query, query_smalltalk, intent_smalltalk_answer, lang_infer_type):
    """개별 잡담 답변 가능성 체크 - AI 생성 잡담과 사용자 답변의 연관성 판단"""
    if intent_smalltalk_answer == 'force':
        return True
    if intent_smalltalk_answer == 'off':
        return False
    
    # query_smalltalk이 없으면 체크할 수 없음
    if not query_smalltalk or not query_smalltalk.strip():
        return False
    
    import ai_chk_smalltalk_relevance
    intent_response = ai_chk_smalltalk_relevance.process(query_smalltalk, query, lang=lang_infer_type)
    if "related: True" in intent_response:  # AI 잡담과 사용자 답변이 연관됨
        return True
    return False


def process_intents(query, query_en, intent_web, intent_image, intent_confirm, intent_confirm_type, 
                    image_info, lang_infer_type, server_type, intent_info, ai_info, image_info_text,
                    intent_smalltalk_answer, query_smalltalk):
    """Intent 처리 통합 로직"""
    is_intent_all = False
    query_intent = ''
    is_intent_web = False
    is_intent_image = False
    is_intent_smalltalk_answer = False
    
    is_intent = (intent_web != 'off') or (intent_image != 'off' and image_info) or (intent_confirm != 'off')
    if is_intent:
        print('### intent check start')
        
        # 전체 intent / 개별 intent
        if is_intent_all:  # 전체 intent
            # 전체 intent 중 최대 하나만 반영                
            query_intent = check_intent_all_unified(query_en, intent_web, intent_image, image_info, intent_confirm, intent_confirm_type, server_type)
            print('check_intent_all', query_intent, intent_web, intent_image, image_info, intent_confirm, intent_confirm_type, server_type)
            if query_intent:
                if query_intent == 'web':
                    intent_info['is_intent_web'] = 'on'
                if query_intent == 'image':
                    intent_info['is_intent_image'] = 'on'
                ai_info['intent'] = query_intent 
        else:  # 개별 intent (Default)
            # 개별 intent 전부 반영
            is_intent_web = check_individual_intent_web(query, intent_web, lang_infer_type)
            if is_intent_web:
                intent_info['is_intent_web'] = 'on'
            if state.get_DEV_MODE():
                print('### is_intent_web :', is_intent_web)
            
            # intent_image가 "force"인 경우는 intent_image 체크할 필요 없음. 애초에 force인데 뭐...
            if intent_image != 'force':
                is_intent_image = check_individual_intent_image(query, image_info, intent_image, lang_infer_type, image_info_text)
                if is_intent_image:
                    intent_info['is_intent_image'] = 'on'
                if state.get_DEV_MODE():
                    print('### is_intent_image :', is_intent_image)
            
            # intent_smalltalk_answer 체크
            is_intent_smalltalk_answer = check_individual_intent_smalltalk_answer(
                query, query_smalltalk, intent_smalltalk_answer, lang_infer_type)
            if is_intent_smalltalk_answer:
                intent_info['is_intent_smalltalk_answer'] = 'on'
                intent_info['smalltalk_query'] = query_smalltalk  # AI가 생성한 잡담 내용 포함
            if state.get_DEV_MODE():
                print('### is_intent_smalltalk_answer :', is_intent_smalltalk_answer)

    return query_intent, is_intent_web, is_intent_image, is_intent_smalltalk_answer



