import logging  # PIL, hpack 등 logger 쓰는 라이브러리가 너무 날뜀
# if not DEV_MODE or True:
#     logging.disable(logging.INFO) # disable INFO and DEBUG logging everywhere
#     logging.disable(logging.DEBUG) # disable INFO and DEBUG logging everywhere
#     logging.disable(logging.WARNING) # disable WARNING, INFO and DEBUG logging everywhere

import os
import re
import sys
import json
import state
import pygame
import uuid
import argparse
import nltk
from datetime import datetime
import platform
import shutil
from PIL import Image
from io import BytesIO

# Local
# import ai_conversation
import ai_conversation_binary as ai_conversation
import ai_vl_conversation_binary as ai_vl_conversation
import ai_vl_conversation_gemma
import ai_web_search
import ai_web_search_keyword
import ai_singleton
import util_openrouter
import util_gemini
import voice_inference
import util_pyngrok
import prompt_char
import util_translator
import util_silerovad
from util_tray import IconTrayApp
from util_string import detect_language
# import ai_intent_reader
import ai_intent_web
import ai_chk_image_relevance
import ai_intent_confirm
import ai_intent_turn_light
import ai_florence
import ai_emotion_classification
import util_IoT_SwitchBot
import util_speech_diarization
import constants

# Server-Flask
from flask import Flask, Response, request, jsonify, send_file, abort
from waitress import serve, create_server
import atexit

# Flask-Blueprint
from server_simple import bp_simple_api

app = Flask(__name__)
app.register_blueprint(bp_simple_api)

import server_interface_logger
server_interface_logger.register_logger(app)

# 로그 파일 설정 (Streaming 반환 안되고 격렬히 느려짐)
# if not DEV_MODE or True:
#     os.makedirs('./log', exist_ok=True)
#     log_file = f"./log/server_log_{datetime.datetime.now().strftime('%Y%m%d')}.txt"

#     # RotatingFileHandler 설정 (최대 5MB, 백업 5개)
#     from logging.handlers import RotatingFileHandler
#     handler = RotatingFileHandler(log_file, maxBytes=5 * 1024 * 1024, backupCount=5)
#     formatter = logging.Formatter('%(asctime)s - %(levelname)s - %(message)s')
#     handler.setFormatter(formatter)

#     # Flask 로거 설정
#     app.logger.setLevel(logging.INFO)
#     app.logger.addHandler(handler)

#     # Waitress 로거 설정
#     waitress_logger = logging.getLogger('waitress')
#     waitress_logger.setLevel(logging.INFO)
#     waitress_logger.addHandler(handler)

#     @app.before_request
#     def log_request_info():
#         app.logger.info(f"Request: {request.method} {request.url} - Headers: {dict(request.headers)}")

#     @app.after_request
#     def log_response_info(response):
#         if not response.direct_passthrough:  # wav 답변시 오류 발생하지 않게
#             app.logger.info(f"Response: {response.status} - {response.get_data(as_text=True)}")
#         else:
#             app.logger.info(f"Response: {response.status} (streaming response)")
#         return response


# 서버 정상 종료 시 실행할 함수 정의
def on_exit():
    # util_supabase.post_ngrok_path('', status="closed")  # 유니티쪽에서 "서버켜달라고 요청하세요 등의 트리거 생길때 활성화"
    print('Server End start')
    ai_singleton.release()    
    print('Server End Finish')
atexit.register(on_exit)


# Translator (TODO : interface에서 로직 분리)
translator = None
server_config = None

# ========================================
# 내부 헬퍼 함수들 (conversation_stream 관련)
# ========================================
import server_interface_func

# 일본어 번역 API
@app.route('/getJpTrans', methods=['GET'])
def get_jp_trans():
    text = request.args.get('text', default='', type=str)
    if translator:
        try:
            translated_text = translator.translate_formality(text, 'ja').text
            return jsonify({"translated_text": translated_text}), 200
        except Exception as e:
            return jsonify({"error": str(e)}), 500
    return jsonify({"error": "Translator not available"}), 500

# 일본어 후리가나 변환 API
@app.route('/furigana', methods=['GET', 'POST'])
def get_furigana():
    import util_japanese_fix
    
    start_time = datetime.now()
    
    if state.get_DEV_MODE():
        print('furigana request :', request.args or request.form or request.json)
        state.write_log('furigana request :' + str(request.args or request.form or request.json))
    
    # 파라미터 파싱 (GET query string, POST form, POST json 모두 지원)
    if request.method == 'GET':
        text = request.args.get('text', '')
    elif request.is_json:
        text = request.json.get('text', '')
    else:
        text = request.form.get('text', '')
    
    # 텍스트가 비어있을 경우 요청무시 (Early Return)
    if not text or not text.strip():
        return Response(json.dumps({"error": "Text is empty"}), 
                       status=400, 
                       content_type='application/json')
    
    def generate():
        # 1. 처리 시작 알림 (Think 단계)
        yield json.dumps({
            "type": constants.RESPONSE_TYPE_THINKING,
            "message": "Processing furigana conversion..."
        }) + '\n'
        
        # 2. util_japanese_fix로 후리가나 변환
        try:
            furigana_result = util_japanese_fix.ocr_postprocess(text)
            
            # 처리 시간 계산
            processing_time = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
            
            # 3. 최종 결과 반환
            result = {
                "type": constants.RESPONSE_TYPE_REPLY,
                "furigana": furigana_result,
                "original": text,
                "time": processing_time
            }
            
            if state.get_DEV_MODE():
                print(f'furigana result: {furigana_result}')
                state.write_log(f'furigana result: {furigana_result}')
            
            yield json.dumps(result) + '\n'
            
        except Exception as e:
            error_result = {
                "type": "error",
                "error": str(e),
                "original": text,
                "time": f"{(datetime.now() - start_time).total_seconds():.2f} sec"
            }
            yield json.dumps(error_result) + '\n'
    
    return Response(generate(), content_type='application/json')

# 멀티모달 능력 체크 함수
def check_multimodal_capability(using_model_name):
    if using_model_name and using_model_name in constants.MULTIMODAL_MODELS:
        return True
    return False

# 번역없이 답변
@app.route('/conversation_stream/simple', methods=['POST'])
def main_stream_simple():  # main logic
    query = request.json.get('query')
    # image = ''  # TODO
    def generate():
        reply_len = 0
        for j, reply_list in enumerate(ai_conversation.process_stream(query, 'm9dev', 'arona', True, False)):
            if reply_len < len(reply_list):
                reply_len = len(reply_list)
                yield json.dumps({"reply_list": reply_list}) + '\n'
    return Response(generate(), content_type='application/json')

# 번역포함 답변
@app.route('/conversation_stream', methods=['POST'])
def main_stream():  # main logic
    start_time = datetime.now()
    if state.get_DEV_MODE():
        print('conversation_stream request :', request.form)  
        state.write_log('conversation_stream request :' + str(request.form))
    
    # 1. 파라미터 파싱
    params = server_interface_func.parse_conversation_params(server_config)
    
    # 2. 변수 추출 (가독성을 위해)
    query = params['query']
    # 쿼리가 비어있을 경우 요청무시 (Early Return)
    if not query or not query.strip():
        return jsonify({"error": "Query is empty"}), 400
    
    player_name = params['player_name']  # 플레이어 이름
    char_name = params['char_name']  # 캐릭터 이름
    ai_language = params['ai_language']  # AI 추론에 사용할 언어 (normal/prefer/ko/en/ja)
    ai_language_in = params['ai_language_in']  # 입력 언어 (STT에서 감지된 언어)
    ai_language_out = params['ai_language_out']  # 메모리 저장 언어
    sound_language = params['sound_language']  # TTS 출력 언어
    ai_emotion = params['ai_emotion']  # 표정 반영 여부 (on/off)
    api_key_Gemini = params['api_key_Gemini']  # Google Gemini API 키
    api_key_OpenRouter = params['api_key_OpenRouter']  # OpenRouter API 키
    api_key_ChatGPT = params['api_key_ChatGPT']  # ChatGPT API 키
    memory = params['memory']  # 대화 기록 메모리
    guideline_list = params['guideline_list']  # 유저 카드 가이드라인 목록
    situation_dict = params['situation_dict']  # 현재 상황 정보
    chat_idx = params['chat_idx']  # 채팅 인덱스
    regenerate_count = params['regenerate_count']  # 재생성 횟수
    is_regenerate = params['is_regenerate']  # 재생성 여부
    intent_web = params['intent_web']  # 웹 검색 의도 (on/off/force)
    intent_image = params['intent_image']  # 이미지 생성 의도 (on/off/force)
    intent_confirm = params['intent_confirm']  # 의도 행동 확인 여부 (on/off)
    intent_confirm_type = params['intent_confirm_type']  # 확인할 의도 행동 타입 (web/light)
    intent_confirm_answer = params['intent_confirm_answer']  # 의도 행동 확인 답변 (true/false)
    intent_guideline = params['intent_guideline']  # 가이드라인 AI 작성 여부 (on/off)
    intent_smalltalk_answer = params['intent_smalltalk_answer']  # 잡담 답변 가능성 (on/off)
    query_smalltalk = params['query_smalltalk']  # AI쪽에서 보낸 잡담
    server_type = params['server_type']  # 서버 타입 (Auto/Local/Google/OpenRouter/ChatGPT/Custom)
    model_name_Local = params['model_name_Local']  # Local GGUF 모델 파일명
    model_name_Gemini = params['model_name_Gemini']  # Gemini 모델명
    model_name_OpenRouter = params['model_name_OpenRouter']  # OpenRouter 모델명
    model_name_ChatGPT = params['model_name_ChatGPT']  # ChatGPT 모델명
    model_name_Custom = params['model_name_Custom']  # Custom 모델명
    server_local_mode = params['server_local_mode']  # 로컬 모델 실행 모드 (CPU/GPU)
    using_model_name = params['using_model_name']  # server_type에 따라 실제 사용 중인 모델명
    
    # Custom 타입 처리: model_name_Custom에 따라 실제 provider와 모델명 결정
    custom_provider = None
    custom_actual_model = None
    custom_api_key_type = None
    is_custom_vl_model = False  # VL 모델 여부 플래그
    
    '''
    Custom 관련 내용은 차후 추출

    # is_custom_vl_model 처리
    if server_type == "Custom" and model_name_Custom:
        # VL 모델 체크 (예: Qwen3VL-8B, Qwen3VL-30B)
        if (model_name_Custom == 'Qwen3VL-8B-Instruct-Q4_K_M.gguf' or
            model_name_Custom == 'Qwen3VL-30B-A3B-Instruct-Q4_K_M.gguf'):
            is_custom_vl_model = True
            print(f'### Custom VL model detected: {model_name_Custom}')
            # VL 모델은 Local로 처리
            server_type = "Local"
            model_name_Local = model_name_Custom
        else:
            # 일반 Custom 모델 처리
            custom_config = server_interface_func.get_custom_model_provider(model_name_Custom)
            custom_provider = custom_config['provider']
            custom_actual_model = custom_config['model']
            custom_api_key_type = custom_config['api_key_type']
            
            print(f'### Custom model routing: {model_name_Custom} -> {custom_provider} ({custom_actual_model})')
            # Custom은 내부적으로 해당 provider로 처리
            server_type = custom_provider
            # 해당 provider의 모델명 설정
            if custom_provider == "Google":
                model_name_Gemini = custom_actual_model
            elif custom_provider == "OpenRouter":
                model_name_OpenRouter = custom_actual_model
            elif custom_provider == "ChatGPT":
                model_name_ChatGPT = custom_actual_model
                
    ## VL 모델 Local 정규 편입
    if server_type == "Local" and model_name_Local:
        # VL 모델 체크 (예: Qwen3VL-8B, Qwen3VL-30B)
        if (model_name_Local == 'Qwen3VL-8B-Instruct-Q4_K_M.gguf' or
            model_name_Local == 'Qwen3VL-30B-A3B-Instruct-Q4_K_M.gguf'):
            is_custom_vl_model = True
            print(f'### Custom VL model detected: {model_name_Local}')
            # VL 모델은 Local로 처리
            # server_type = "Local"
            # model_name_Local = model_name_Local
    
    # Custom 타입 처리 후 using_model_name 업데이트
    if is_custom_vl_model:
        # VL 모델은 Local로 처리되므로 model_name_Local 사용
        using_model_name = model_name_Local
    elif server_type == "Custom":
        # Custom 모델이 다른 provider로 라우팅된 경우 해당 모델명 사용
        if custom_provider == "Google":
            using_model_name = model_name_Gemini
        elif custom_provider == "OpenRouter":
            using_model_name = model_name_OpenRouter
        elif custom_provider == "ChatGPT":
            using_model_name = model_name_ChatGPT
        else:
            using_model_name = model_name_Custom
    else:
        # 일반적인 경우: server_type에 따라 모델명 결정
        temp_params = {
            'server_type': server_type,
            'model_name_Local': model_name_Local,
            'model_name_Gemini': model_name_Gemini,
            'model_name_OpenRouter': model_name_OpenRouter,
            'model_name_ChatGPT': model_name_ChatGPT,
            'model_name_Custom': model_name_Custom
        }
        using_model_name = server_interface_func.get_using_model_name(temp_params)
    '''
        
    # 5. 멀티모달 능력 체크 및 change_model intent 처리
    # intent_image가 'on' 또는 'force'일 때만 체크
    has_multimodal = False  # 기본값
    if intent_image in ('on', 'force'):
        has_multimodal = check_multimodal_capability(using_model_name)

    # 향후 Validation 체크용 예시 로직(유니티쪽 구현 되어있음)
    # # Case 1: intent_image="force" + 이미지 있음 + 멀티모달 불가능
    # if intent_image == 'force' and uploaded_file and uploaded_file.filename and not has_multimodal:
    #     print(f"### Change model needed: Force mode but no multimodal capability")
    #     return Response(json.dumps({
    #         "type": constants.RESPONSE_TYPE_ASKING_INTENT,
    #         "intent_info": constants.INTENT_CHANGE_MODEL,
    #         "chat_idx": chat_idx,
    #         "reason": "force_mode_no_multimodal"
    #     }), content_type='application/json')

    # # Case 2: intent_image="on" + image_intent 판단 결과 이미지 필요 + 멀티모달 불가능
    # # intent_image가 "force"인 경우는 Case 1에서 이미 처리되므로 제외
    # if (intent_image != 'force') and (is_intent_image and not has_multimodal):
    #     print(f"### Change model needed: Image intent detected but no multimodal capability")
    #     return Response(json.dumps({
    #         "type": constants.RESPONSE_TYPE_ASKING_INTENT,
    #         "intent_info": constants.INTENT_NO_IMAGE,
    #         "chat_idx": chat_idx,
    #         "reason": "intent_detected_no_multimodal"
    #     }), content_type='application/json')

    # 3. 응답 형식 설정
    ai_info = server_interface_func.set_default_response_format_ai_info()
    intent_info = server_interface_func.set_default_response_format_intent_info()

    # 4. 이미지 업로드 처리 (업로드 된 경우)
    image_info = None
    image_info_text = None
    uploaded_file = request.files.get('image')  
    if uploaded_file and uploaded_file.filename:  # 이미지 파일이 업로드된 경우
        print(f"### Image uploaded: {uploaded_file.filename}")
        
        if has_multimodal:
            # VL 모델: 파일로 저장 (Florence 스킵)
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            temp_image_path = os.path.join('./files/image', f"vl_temp_{timestamp}.png")
            os.makedirs('./files/image', exist_ok=True)
            uploaded_file.save(temp_image_path)
            image_info = temp_image_path
            print(f"### VL model image saved: {image_info}")
        else:
            # 일반 모델: Florence 사용 여부 분기
            use_florence = False  # Florence는 영입영출 특화라 이제 기본적으로 사용 안 함
            if use_florence:
                # 레거시: Florence로 이미지 분석 (텍스트 반환)
                image_info_text = server_interface_func.process_uploaded_image(request.files, intent_image, intent_info)
    
    # 6. 언어 감지 및 번역 준비
    lang_infer_type, lang_infer, is_query_en_translated, query_en = server_interface_func.detect_and_prepare_language(query, ai_language, ai_language_in)
    
    # 7. Intent 처리
    query_intent, is_intent_web, is_intent_image, is_intent_smalltalk_answer = server_interface_func.process_intents(
        query, query_en, intent_web, intent_image, intent_confirm, intent_confirm_type, 
        image_info, lang_infer_type, server_type, intent_info, ai_info, image_info_text,
        intent_smalltalk_answer, query_smalltalk
    )
    
    # 8. VL 모델 사용 여부 최종 결정: 이미지가 있고 + 멀티모달 모델 + intent_image가 활성화된 경우
    # Intent 처리 후에 수행하여 is_intent_image 값을 활용
    if image_info and has_multimodal and is_intent_image:
        is_custom_vl_model = True
        print(f'### VL model will be used: {using_model_name} with image (intent_image={intent_image}, is_intent_image={is_intent_image})')

    # lightOn/lightOff IoT 응답 처리
    if query_intent == 'lightOn':
        answer_list = list()
        answer = dict()
        answer['answer_en'] = "Okay, Sensei. I'll turn on the light."                
        answer['answer_ko'] = '알겠습니다. 선생님. 불을 킬게요.'
        answer['answer_jp'] = 'わかりました、先生。電気をつけます。'            
        answer_list.append(answer)
        query_trans = {'origin':query, 'text': '', 'source': '', 'time': '0'}
        return Response(json.dumps({"reply_list": answer_list, "query":query_trans, "chat_idx":chat_idx, "ai_language_out":ai_language_out, "type": constants.RESPONSE_TYPE_TRIGGER, "type_desc" : query_intent}), content_type='application/json')
    if query_intent == 'lightOff':
        answer_list = list()
        answer = dict()
        answer['answer_en'] = "Okay, Sensei. I'll turn off the light."                  
        answer['answer_ko'] = '알겠습니다. 선생님. 불을 끌게요.'
        answer['answer_jp'] = 'わかりました、先生。電気消しますね。'            
        answer_list.append(answer)
        query_trans = {'origin':query, 'text': '', 'source': '', 'time': '0'}
        return Response(json.dumps({"reply_list": answer_list, "query":query_trans, "chat_idx":chat_idx, "ai_language_out":ai_language_out, "type": constants.RESPONSE_TYPE_TRIGGER, "type_desc" : query_intent}), content_type='application/json')
    
    # 추론 자체에 영어 번역이 필요한 경우    
    query_trans = {'origin':query, 'text': '', 'source': '', 'time': '0'}
    
    if char_name not in prompt_char.get_all_filenames_in_prompt():
        char_name = 'kivotos_student_normal'  # ./prompt/kivotos_student_normal.json
        
    # memory가 있을 경우, 대화내역으로 변경
    if memory:
        memory = json.loads(memory)  # 문자열을 JSON으로 변환
    else:
        memory = None
    
    # Intent smalltalk이 감지되었을 때, memory에 AI 잡담 추가
    if is_intent_smalltalk_answer and query_smalltalk:
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        
        smalltalk_entry = {
            "speaker": "character",
            "message": query_smalltalk,
            "message_trans": query_smalltalk,
            "role": "assistant",
            "type": "conversation",
            "messageKo": query_smalltalk,
            "messageJp": query_smalltalk,
            "messageEn": query_smalltalk,
            "timestamp": timestamp
        }
        
        if memory is None:
            # memory가 없으면 새로 생성
            memory = [smalltalk_entry]
        else:
            # memory가 있으면 마지막에 추가
            memory.append(smalltalk_entry)
            
    def generate(image_info):
        nonlocal start_time
        nonlocal is_intent_image, is_query_en_translated, query_en, lang_infer_type
        nonlocal model_name_Local, model_name_Gemini, model_name_OpenRouter, model_name_ChatGPT, model_name_Custom  # 각 서비스별 모델명
        nonlocal server_local_mode  # CPU/GPU 모드
        nonlocal custom_provider, custom_actual_model  # Custom 모델 정보
        nonlocal is_custom_vl_model  # VL 모델 플래그
        
        answer_list = list()
        reply_len = 0
        print('###server_type', server_type)
        
        # Google 또는 OpenRouter 사용 시 로컬 모델 해제
        if server_type in ("Google", "OpenRouter"):
            if ai_singleton.check_llm():
                print(f'### Releasing local model for {server_type} API usage')
                ai_singleton.release()
        
        # Local 모델 사용 시 model_name_Local 적용
        if server_type == "Local" and model_name_Local:
            print('###model_name_Local', model_name_Local)
            print('###server_local_mode', server_local_mode)
            print('###is_custom_vl_model', is_custom_vl_model)
            
            # VL 모델 처리
            if is_custom_vl_model:
                # 기존 모델이 로드되어 있고, 요청된 모델과 다르면 release
                if ai_singleton.check_llm():
                    if state.get_DEV_MODE() and model_name_Local != state.model_name:
                        print(f'### Releasing old model: {state.model_name} (requested VL model: {model_name_Local})')
                        ai_singleton.release()
                
                # VL 모델 설정
                state.model_name = model_name_Local
                
                # server_local_mode에 따라 GPU/CPU 설정
                if server_local_mode == 'GPU':
                    state.set_var_from_model(model_name_Local)
                    state.set_n_gpu_layers()  # GPU layer 설정
                    print(f'### VL model GPU mode - n_gpu_layers: {state.n_gpu_layers}')
                elif server_local_mode == 'CPU':
                    state.use_vram = 0
                    state.set_use_gpu_percent(0)
                    state.set_var_from_model("erase")  # CPU 모드
                    state.set_n_gpu_layers()
                    print(f'### VL model CPU mode - n_gpu_layers: {state.n_gpu_layers}')
                else:
                    # server_local_mode가 지정되지 않은 경우 기본 동작
                    state.set_var_from_model(model_name_Local)
                    state.set_n_gpu_layers()
                    print(f'### VL model default mode - n_gpu_layers: {state.n_gpu_layers}')
            else:
                # 기존 일반 모델 로딩 로직
                # 이미 모델이 로드되어 있는지 확인
                if ai_singleton.check_llm():
                    # 로드된 모델이 있으면 그냥 사용
                    if state.get_DEV_MODE() and model_name_Local != state.model_name:
                        print(f'### Using already loaded model: {state.model_name} (requested: {model_name_Local})')
                else:
                    # 로드된 모델이 없으면 요청된 모델로 설정 후 로딩
                    if state.get_DEV_MODE():
                        print(f'### No model loaded, loading model: {model_name_Local} (mode: {server_local_mode})')
                    
                    state.model_name = model_name_Local
                    
                    # server_local_mode에 따라 GPU/CPU 설정
                    if server_local_mode == 'GPU':
                        state.set_var_from_model(model_name_Local)
                    elif server_local_mode == 'CPU':
                        state.use_vram = 0
                        state.set_use_gpu_percent(0)
                        state.set_var_from_model("erase")  # CPU 모드
                        state.set_n_gpu_layers()
                    else:
                        # server_local_mode가 지정되지 않은 경우 기본 동작 (기존 로직)
                        state.set_var_from_model(model_name_Local)
        
        if server_type == "OpenRouter":  # OpenRouter API 사용
            print('###model_name_OpenRouter', model_name_OpenRouter)
            if not is_query_en_translated:
                query_translate = translator.translate(query, 'en')  # 존댓말없이 번역
                is_query_en_translated = True
                if 'text' in query_translate and query_translate['text']:
                    query_en = query_translate['text']
                    lang_infer_type = 'en'
                else:
                    query_en = query
                
            for j, reply_list in enumerate(util_openrouter.process_stream(query_en, player_name, char_name, True, False, memory_list=memory, lang=lang_infer_type, api_key=api_key_OpenRouter)):
                if reply_len < len(reply_list):                
                    reply_len = len(reply_list)
                    
                    reply_new = reply_list[-1]
                    result_ko = reply_new
                    result_jp = reply_new
                    try:
                        translate_ko = translator.translate_formality(reply_new, 'ko')
                        result_ko = translate_ko['text']
                        translate_ja = translator.translate_formality(reply_new, 'ja')
                        result_jp = translate_ja['text']   
                    except:
                        pass
                    answer = dict()
                    answer['answer_en'] = reply_new                
                    answer['answer_ko'] = result_ko
                    answer['answer_jp'] = result_jp            
                    answer_list.append(answer)
                    
                    # 사용 ai 관련 정보
                    ai_info['server_type'] = 'OpenRouter'
                    # Custom 모델 사용 시 원래 모델명 표시
                    if custom_provider and model_name_Custom:
                        ai_info['model'] = f"{model_name_Custom} ({model_name_OpenRouter})"
                    else:
                        ai_info['model'] = model_name_OpenRouter
                    ai_info['prompt'] = lang_infer_type + '/' + char_name
                    ai_info['lang_used'] = lang_infer_type
                    ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                    
                    yield json.dumps({"reply_list": answer_list, "query":query_trans, "ai_info":ai_info, "intent_info":intent_info, "chat_idx":chat_idx, "ai_language_out":ai_language_out}) + '\n'
            
            if state.get_DEV_MODE():
                print('util_openrouter|lang_infer : ' + lang_infer)  
                print('util_openrouter|answer_list : ' + str(answer_list))  
                state.write_log('util_openrouter|answer_list : ' + str(answer_list))
        elif query_intent == "lightOn" or query_intent == "lightOff":  # IoT 관련
            pass
        elif query_intent == "web" or is_intent_web:
            # reply 받기 전, 기본 상태 전송
            yield json.dumps({
                "type": constants.RESPONSE_TYPE_WEB_SEARCH,
                "intent_info":intent_info,
                "chat_idx":chat_idx
            }) + '\n'
            web_keyword = None
            web_keyword_failed = False  # 키워드 생성 실패 여부
            
            # web인데 이미지까지 보낼 경우 이미지 의도가 있다고 판단.
            if image_info:
                is_intent_image = True
            # 이미지 의도 없을 경우 지우기
            if (query_intent != "image" or True) and (intent_image == 'on' and not is_intent_image):
                image_info = None
            else:
                # 웹 + 이미지 까지 합쳐서 query 다시 만들기
                try:
                    web_keyword_raw = ai_web_search_keyword.process(query, image_info, lang=lang_infer_type)
                    if web_keyword_raw:
                        _, web_keyword = web_keyword_raw.split("keyword:", 1)
                        web_keyword = web_keyword.strip()
                        print('##web_keyword', web_keyword)
                    else:
                        # 키워드 생성 실패
                        web_keyword = None
                        web_keyword_failed = True
                        print('[ERROR] Keyword generation returned empty')
                except Exception as e:
                    # 키워드 생성 실패
                    web_keyword = None
                    web_keyword_failed = True
                    print(f'[ERROR] Keyword generation failed: {e}')
                
            # 키워드 생성 실패 시 메타데이터 설정
            if web_keyword_failed:
                intent_info['web_search_keyword'] = ""
                intent_info['web_search_method'] = "Fail(Keyword)"
                intent_info['web_search_content'] = ""
                # 키워드 실패해도 원본 query로 웹검색 시도는 진행
                
            for j, reply_list in enumerate(ai_web_search.process(query, info_img=image_info, lang=lang_infer_type, web_keyword=web_keyword)):
                if reply_len < len(reply_list):                
                    reply_len = len(reply_list)
                    
                    reply_new = reply_list[-1]
                    result_en = reply_new
                    if lang_infer_type != 'en':
                        try:
                            translate_en = translator.translate_formality(reply_new, 'en')
                            result_en = translate_en['text']
                        except:
                            pass                     
                    result_ko = reply_new
                    if lang_infer_type != 'ko':
                        try:
                            translate_ko = translator.translate_formality(reply_new, 'ko')
                            result_ko = translate_ko['text']
                        except:
                            pass                     
                    result_jp = reply_new
                    if lang_infer_type != 'ja':
                        try:
                            translate_ja = translator.translate_formality(reply_new, 'ja')
                            result_jp = translate_ja['text']
                        except:
                            pass                     
                    answer = dict()
                    answer['answer_en'] = result_en                
                    answer['answer_ko'] = result_ko
                    answer['answer_jp'] = result_jp            
                    answer_list.append(answer)
                    
                    # 사용 ai 관련 정보
                    ai_info['server_type'] = server_type
                    ai_info['model'] = state.model_name
                    ai_info['prompt'] = lang_infer_type + '/' + char_name  # TODO 파일 없을 경우의 예외 처리
                    ai_info['lang_used'] = lang_infer_type
                    ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                    print('## ai_info', ai_info) 
                    
                    yield json.dumps({"reply_list": answer_list, "query":query_trans, "ai_info":ai_info, "intent_info":intent_info,"chat_idx":chat_idx, "ai_language_out":ai_language_out}) + '\n'
            
            # 웹 검색 완료 후 메타데이터 추출
            if not web_keyword_failed:
                web_metadata = ai_web_search.get_metadata()
                
                # LLM이 Search_web를 생성하지 못한 경우
                if not web_metadata['llm_generated']:
                    intent_info['web_search_keyword'] = ""
                    intent_info['web_search_method'] = "Fail(LLM)"
                    intent_info['web_search_content'] = ""
                else:
                    # 정상 케이스
                    intent_info['web_search_keyword'] = web_metadata['keyword']
                    intent_info['web_search_method'] = web_metadata['method']
                    intent_info['web_search_content'] = web_metadata['content']
                
                print(f"## web_search_metadata - keyword: {intent_info['web_search_keyword']}, method: {intent_info['web_search_method']}, content_len: {len(intent_info['web_search_content'])}")
            
            if state.get_DEV_MODE():
                print('web_search|query_intent : ' + query_intent)  
                print('web_search|answer_list : ' + str(answer_list))  
                state.write_log('web_search|answer_list : ' + str(answer_list))
        else:
            # 이미지 의도 없을 경우 지우기
            if (query_intent != "image" or True) and (intent_image == 'on' and not is_intent_image):
                image_info = None
            # reply 받기 전, 기본 상태 전송
            yield json.dumps({
                "type": constants.RESPONSE_TYPE_THINKING,
                "intent_info":intent_info,
                "chat_idx":chat_idx
            }) + '\n'

            # 답변 시작
            if server_type == "Google":  # Google Gemini API 사용
                print('##Google Gemini answer start')
                print('###model_name_Gemini', model_name_Gemini)
                
                # VL 모델 분기 (이미지 있을 때)
                if is_custom_vl_model and image_info:
                    # VL 모델 사용 (ai_vl_conversation_gemma)
                    print(f'##Using Google VL model: {model_name_Gemini} with image')
                    for j, reply_list in enumerate(ai_vl_conversation_gemma.process_stream(query, player_name, char_name, True, 
                                                                                  info_img=image_info, is_regenerate=is_regenerate, memory_list=memory, 
                                                                                  lang=lang_infer_type, api_key=api_key_Gemini, guideline_list=guideline_list, situation_dict=situation_dict, use_memory=True)):
                        if reply_len < len(reply_list):                
                            reply_len = len(reply_list)
                            
                            # VL 모델은 감정 분류 스킵
                            
                            print('##lang_infer_type', lang_infer_type)
                            
                            reply_new = reply_list[-1]
                            result_en = reply_new
                            if lang_infer_type != 'en':
                                try:
                                    translate_en = translator.translate_formality(reply_new, 'en')
                                    result_en = translate_en['text']
                                    if 'source' in translate_en:
                                        ai_info['translator'] = translate_en['source']
                                except:
                                    pass                     
                            result_ko = reply_new
                            if lang_infer_type != 'ko':
                                try:
                                    translate_ko = translator.translate_formality(reply_new, 'ko')
                                    result_ko = translate_ko['text']
                                    if 'source' in translate_ko:
                                        ai_info['translator'] = translate_ko['source']
                                except:
                                    pass                     
                            result_jp = reply_new
                            if lang_infer_type != 'ja':
                                try:
                                    translate_ja = translator.translate_formality(reply_new, 'ja')
                                    result_jp = translate_ja['text']
                                    if 'source' in translate_ja:
                                        ai_info['translator'] = translate_ja['source']
                                except:
                                    pass                     
                            answer = dict()
                            answer['answer_en'] = result_en                
                            answer['answer_ko'] = result_ko
                            answer['answer_jp'] = result_jp           
                            answer_list.append(answer)
                            
                            # 사용 ai 관련 정보
                            ai_info['server_type'] = 'Google-VL'
                            # Custom 모델 사용 시 원래 모델명 표시
                            if custom_provider and model_name_Custom:
                                ai_info['model'] = f"{model_name_Custom} ({model_name_Gemini})"
                            else:
                                ai_info['model'] = model_name_Gemini
                            ai_info['prompt'] = lang_infer_type + '/' + char_name
                            ai_info['lang_used'] = lang_infer_type
                            ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                            
                            print('### ai_info', ai_info)
                            
                            response_data = {"type": constants.RESPONSE_TYPE_REPLY, "reply_list": answer_list, "query":query_trans, "ai_info":ai_info, "intent_info":intent_info,"chat_idx":chat_idx, "ai_language_out":ai_language_out}
                            yield json.dumps(response_data) + '\n'
                            
                            # 최종 응답 저장 (매 yield마다 업데이트)
                            if state.get_DEV_MODE():
                                try:
                                    server_interface_logger.save_final_response(response_data)
                                except Exception as e:
                                    print(f'[LOG ERROR] save_final_response failed: {e}')
                else:
                    # 일반 Google 모델 사용 (기존 로직)
                    for j, reply_list in enumerate(util_gemini.process_stream(query, player_name, char_name, True, info_img=None, 
                                                                              is_regenerate=is_regenerate, memory_list=memory, 
                                                                              lang=lang_infer_type, api_key=api_key_Gemini, guideline_list=guideline_list, situation_dict=situation_dict)):
                        if reply_len < len(reply_list):                
                            reply_len = len(reply_list)

                            # Gemini 감정분석은 기본 OFF                        
                            # if reply_len == 1 and ai_emotion == 'on' and not ai_info['emotion']:
                            #     ai_emotion_classification_reply = reply_list[0]
                            #     ai_emotion_reply = ai_emotion_classification.process(query, ai_emotion_classification_reply, player_name, char_name, memory_list=memory, lang=lang_infer_type)
                            #     if "emotion: " in ai_emotion_reply:  # 답에 emotion format이 있음
                            #         emotion_text = ai_emotion_reply.strip().split("emotion: ")[-1].strip().lower()
                            #         valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']  # Joy/Anger/Confusion/Sadness/Surprise/Neutral
                            #         if emotion_text in valid_emotions:
                            #             ai_info['emotion'] = emotion_text
                            
                            # print('reply_list', reply_list)
                            print('##lang_infer_type', lang_infer_type)
                            
                            reply_new = reply_list[-1]
                            result_en = reply_new
                            if lang_infer_type != 'en':
                                try:
                                    translate_en = translator.translate_formality(reply_new, 'en')
                                    result_en = translate_en['text']
                                    if 'source' in translate_en:
                                        ai_info['translator'] = translate_en['source']
                                except:
                                    pass                     
                            result_ko = reply_new
                            if lang_infer_type != 'ko':
                                try:
                                    translate_ko = translator.translate_formality(reply_new, 'ko')
                                    result_ko = translate_ko['text']
                                    if 'source' in translate_ko:
                                        ai_info['translator'] = translate_ko['source']
                                except:
                                    pass                     
                            result_jp = reply_new
                            if lang_infer_type != 'ja':
                                try:
                                    translate_ja = translator.translate_formality(reply_new, 'ja')
                                    result_jp = translate_ja['text']
                                    if 'source' in translate_ja:
                                        ai_info['translator'] = translate_ja['source']
                                except:
                                    pass                     
                            answer = dict()
                            answer['answer_en'] = result_en                
                            answer['answer_ko'] = result_ko
                            answer['answer_jp'] = result_jp           
                            answer_list.append(answer)
                            
                            # 사용 ai 관련 정보
                            ai_info['server_type'] = 'Google'
                            # Custom 모델 사용 시 원래 모델명 표시
                            if custom_provider and model_name_Custom:
                                ai_info['model'] = f"{model_name_Custom} ({model_name_Gemini})"
                            else:
                                ai_info['model'] = model_name_Gemini
                            ai_info['prompt'] = lang_infer_type + '/' + char_name
                            ai_info['lang_used'] = lang_infer_type
                            ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                            
                            print('### ai_info', ai_info)
                            
                            response_data = {"type": constants.RESPONSE_TYPE_REPLY, "reply_list": answer_list, "query":query_trans, "ai_info":ai_info, "intent_info":intent_info,"chat_idx":chat_idx, "ai_language_out":ai_language_out}
                            yield json.dumps(response_data) + '\n'
                            
                            # 최종 응답 저장 (매 yield마다 업데이트)
                            if state.get_DEV_MODE():
                                try:
                                    server_interface_logger.save_final_response(response_data)
                                except Exception as e:
                                    print(f'[LOG ERROR] save_final_response failed: {e}')
            else:  # Local - 로컬 GGUF 모델 사용
                print(f'##Local model answer start (model: {model_name_Local})')
                print('###model_name_Local', model_name_Local)
                print('###is_custom_vl_model', is_custom_vl_model)
                
                # VL 모델 분기
                if is_custom_vl_model:
                    # VL 모델 사용
                    print(f'##Using VL model: {model_name_Local}')
                    for j, reply_list in enumerate(ai_vl_conversation.process_stream(query, player_name, char_name, True, 
                                                                                  info_img=image_info, is_regenerate=is_regenerate, memory_list=memory, 
                                                                                  lang=lang_infer_type, guideline_list=guideline_list, situation_dict=situation_dict, use_memory=True)):
                        if reply_len < len(reply_list):                
                            reply_len = len(reply_list)
                            
                            # VL 모델은 감정 분류 스킵
                            
                            # print('reply_list', reply_list)
                            print('##lang_infer_type', lang_infer_type)
                            
                            reply_new = reply_list[-1]
                            result_en = reply_new
                            if lang_infer_type != 'en':
                                try:
                                    translate_en = translator.translate_formality(reply_new, 'en')
                                    result_en = translate_en['text']
                                except:
                                    pass                     
                            result_ko = reply_new
                            if lang_infer_type != 'ko':
                                try:
                                    translate_ko = translator.translate_formality(reply_new, 'ko')
                                    result_ko = translate_ko['text']
                                except:
                                    pass                     
                            result_jp = reply_new
                            if lang_infer_type != 'ja':
                                try:
                                    translate_ja = translator.translate_formality(reply_new, 'ja')
                                    result_jp = translate_ja['text']
                                except:
                                    pass                     
                            answer = dict()
                            answer['answer_en'] = result_en                
                            answer['answer_ko'] = result_ko
                            answer['answer_jp'] = result_jp           
                            answer_list.append(answer)
                            
                            # 사용 ai 관련 정보
                            ai_info['server_type'] = 'Local-VL'
                            ai_info['model'] = state.model_name
                            ai_info['prompt'] = lang_infer_type + '/' + char_name  # TODO 파일 없을 경우의 예외 처리
                            ai_info['lang_used'] = lang_infer_type
                            ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                            
                            print('### ai_info', ai_info)
                            
                            response_data = {"type": constants.RESPONSE_TYPE_REPLY, "reply_list": answer_list, "query":query_trans, "ai_info":ai_info, "intent_info":intent_info,"chat_idx":chat_idx, "ai_language_out":ai_language_out}
                            yield json.dumps(response_data) + '\n'
                            
                            # 최종 응답 저장 (매 yield마다 업데이트)
                            if state.get_DEV_MODE():
                                try:
                                    server_interface_logger.save_final_response(response_data)
                                except Exception as e:
                                    print(f'[LOG ERROR] save_final_response failed: {e}')
                else:
                    # 일반 로컬 모델 사용 (기존 로직)
                    for j, reply_list in enumerate(ai_conversation.process_stream(query, player_name, char_name, True, 
                                                                                  info_img=image_info, is_regenerate=is_regenerate, memory_list=memory, 
                                                                                  lang=lang_infer_type, guideline_list=guideline_list, situation_dict=situation_dict)):
                        if reply_len < len(reply_list):                
                            reply_len = len(reply_list)
                            
                            if reply_len == 1 and ai_emotion == 'on' and not ai_info['emotion']:
                                ai_emotion_classification_reply = reply_list[0]
                                ai_emotion_reply = ai_emotion_classification.process(query, ai_emotion_classification_reply, player_name, char_name, memory_list=memory, lang=lang_infer_type)
                                if "emotion: " in ai_emotion_reply:  # 답에 emotion format이 있음
                                    emotion_text = ai_emotion_reply.strip().split("emotion: ")[-1].strip().lower()
                                    valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']  # Joy/Anger/Confusion/Sadness/Surprise/Neutral
                                    if emotion_text in valid_emotions:
                                        ai_info['emotion'] = emotion_text
                            
                            # print('reply_list', reply_list)
                            print('##lang_infer_type', lang_infer_type)
                            
                            reply_new = reply_list[-1]
                            result_en = reply_new
                            if lang_infer_type != 'en':
                                try:
                                    translate_en = translator.translate_formality(reply_new, 'en')
                                    result_en = translate_en['text']
                                except:
                                    pass                     
                            result_ko = reply_new
                            if lang_infer_type != 'ko':
                                try:
                                    translate_ko = translator.translate_formality(reply_new, 'ko')
                                    result_ko = translate_ko['text']
                                except:
                                    pass                     
                            result_jp = reply_new
                            if lang_infer_type != 'ja':
                                try:
                                    translate_ja = translator.translate_formality(reply_new, 'ja')
                                    result_jp = translate_ja['text']
                                except:
                                    pass                     
                            answer = dict()
                            answer['answer_en'] = result_en                
                            answer['answer_ko'] = result_ko
                            answer['answer_jp'] = result_jp           
                            answer_list.append(answer)
                            
                            # 사용 ai 관련 정보
                            ai_info['server_type'] = server_type
                            ai_info['model'] = state.model_name
                            ai_info['prompt'] = lang_infer_type + '/' + char_name  # TODO 파일 없을 경우의 예외 처리
                            ai_info['lang_used'] = lang_infer_type
                            ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                            
                            print('### ai_info', ai_info)
                            
                            response_data = {"type": constants.RESPONSE_TYPE_REPLY, "reply_list": answer_list, "query":query_trans, "ai_info":ai_info, "intent_info":intent_info,"chat_idx":chat_idx, "ai_language_out":ai_language_out}
                            yield json.dumps(response_data) + '\n'
                            
                            # 최종 응답 저장 (매 yield마다 업데이트)
                            if state.get_DEV_MODE():
                                try:
                                    server_interface_logger.save_final_response(response_data)
                                except Exception as e:
                                    print(f'[LOG ERROR] save_final_response failed: {e}')
                
            if state.get_DEV_MODE():
                print('conversation_stream|answer_list : ' + str(answer_list))  
                state.write_log('conversation_stream|answer_list : ' + str(answer_list))
                
                # 최종 응답 저장 (generator 완료 후 마지막 저장)
                try:
                    final_response_data = {
                        "type": constants.RESPONSE_TYPE_REPLY,
                        "reply_list": answer_list,
                        "query": query_trans,
                        "ai_info": ai_info,
                        "intent_info": intent_info,
                        "chat_idx": chat_idx,
                        "ai_language_out": ai_language_out
                    }
                    server_interface_logger.save_final_response(final_response_data)
                    print(f'[LOG] response_final 최종 저장 완료 (답변 개수: {len(answer_list)})')
                except Exception as e:
                    print(f'[LOG ERROR] save_final_response (final) failed: {e}')
            
            # VL 모델 임시 이미지 파일 정리
            if is_custom_vl_model and image_info and isinstance(image_info, str) and image_info.startswith('./files/image/vl_temp_'):
                try:
                    if os.path.exists(image_info):
                        os.remove(image_info)
                        print(f"### VL model temp image removed: {image_info}")
                except Exception as e:
                    print(f"### Failed to remove temp image: {e}")
            
    return Response(generate(image_info), content_type='application/json')


@app.route('/aropla/conversation', methods=['POST', 'GET'])
@app.route('/conversation_stream_multi', methods=['POST'])
def main_stream_multi():
    # 새로운 multi_server 모듈 사용 (Gemini/Local/Hybrid 분기)
    from multi_server import multi_conversation, set_translator
    try:
        set_translator(translator)
    except Exception:
        pass
    return multi_conversation()

# Small Talk 전용 라우팅 (server_multi_impl로 위임)
@app.route('/conversation/small_talk', methods=['POST'])
def conversation_small_talk():
    from server_multi_impl import get_smalk_talk, set_translator
    try:
        set_translator(translator)
    except Exception:
        pass
    return get_smalk_talk()

@app.route('/vl_agent/test', methods=['POST'])
def vl_agent_test():
    from server_interface_vl_agent_impl import vl_agent_test
    return vl_agent_test()

@app.route('/vl_agent/job', methods=['POST'])
def vl_agent_job():
    from server_interface_vl_agent_impl import vl_agent_job
    return vl_agent_job()

@app.route('/vl_agent/run', methods=['POST'])
def vl_planer_run():
    from server_interface_vl_planner_impl import vl_planner_stream
    return vl_planner_stream()

@app.route('/vl_agent/engine_stream', methods=['POST'])
def vl_engine_stream():
    from server_interface_vl_engine_impl import vl_engine_stream
    return vl_engine_stream()

# ============================================================
# PaddleOCR API 엔드포인트 (server_impl_OCR로 위임)
# ============================================================

# 통합 OCR 엔드포인트 (OCR + 선택적 번역)
@app.route('/paddle/ocr', methods=['POST'])
def paddle_ocr():
    from server_impl_OCR import paddle_ocr as _paddle_ocr
    return _paddle_ocr()

# 모델 리로드 (GPU/CPU 전환)
@app.route('/paddle/reload', methods=['POST'])
def paddle_reload():
    from server_impl_OCR import paddle_reload as _paddle_reload
    return _paddle_reload()

# OCR 서버 상태 확인
@app.route('/paddle/health', methods=['GET'])
def paddle_health():
    from server_impl_OCR import paddle_health as _paddle_health
    return _paddle_health()

# PaddleOCR 모델 정보
@app.route('/paddle/info', methods=['GET'])
def paddle_info():
    from server_impl_OCR import paddle_info as _paddle_info
    return _paddle_info()

# 20 Questions Game 라우팅 (server_game로 위임)
@app.route('/game/20q/process', methods=['POST'])
def game_20q_process():
    from server_game import process_game, set_translator
    try:
        set_translator(translator)
    except Exception:
        pass
    return process_game()

# MY-Little-Jarvis-Server 개발용 (deprecated)
@app.route('/conversation_stream_gemini', methods=['POST'])
def main_stream_gemini():
    start_time = datetime.now()
    
    if state.get_DEV_MODE():
        print('conversation_stream_gemini request :', request.form)  
        state.write_log('conversation_stream_gemini request :' + str(request.form))
    
    # 요청 파라미터 파싱
    query = request.form.get('query')
    player_name = request.form.get('player', 'sensei')
    char_name = request.form.get('char', 'arona')
    
    ai_language = request.form.get('ai_language', 'en')
    ai_language_in = request.form.get('ai_language_in', '')
    ai_language_out = request.form.get('ai_language_out', 'en')
    api_key_Gemini = request.form.get('api_key_Gemini', '')
    
    memory = request.form.get('memory')
    raw_guideline = request.form.get('guideline_list', '[]')
    guideline_list = list()
    try:
        guideline_list = json.loads(raw_guideline)
    except json.JSONDecodeError:
        guideline_list = []
        
    raw_situation = request.form.get('situation', '')
    situation_dict = {}
    if raw_situation:
        try:
            situation_dict = json.loads(raw_situation)
        except json.JSONDecodeError as e:
            print(f"[Warning] Situation JSON 파싱 실패: {e}")
            situation_dict = {}
            
    chat_idx = request.form.get('chatIdx', '-1')
    regenerate_count = request.form.get('regenerate_count', '0')
    is_regenerate = False
    if regenerate_count:
        is_regenerate = True
    
    # Intent smalltalk 파라미터 추가 (아직 check 안함. 향후 고도화염두만해두기.)
    intent_smalltalk_answer = request.form.get('intent_smalltalk_answer', 'off')
    query_smalltalk = request.form.get('query_smalltalk', '')

    # 언어 감지 및 설정
    if not ai_language_in:
        if ai_language == 'ko':
            ai_language_in = 'ko'
        if ai_language == 'jp' or ai_language == 'ja':
            ai_language_in = 'jp'
                
    lang_infer_type = ai_language_in
    lang_infer = query
    
    if not ai_language_in:
        lang_infer_type = detect_language(query)

    # 캐릭터 확인
    if char_name not in prompt_char.get_all_filenames_in_prompt():
        char_name = 'kivotos_student_normal'

    # 메모리 처리
    if memory:
        memory = json.loads(memory)
    else:
        memory = None

    # AI 정보 초기화
    ai_info = dict()
    ai_info['server_type'] = 'Google'
    ai_info['model'] = ''
    ai_info['prompt'] = ''
    ai_info['lang_used'] = ''
    ai_info['translator'] = ''
    ai_info['time'] = ''
    ai_info['intent'] = 'None'
    ai_info['emotion'] = ''

    # Intent 정보 초기화
    intent_info = dict()
    intent_info['is_intent_web'] = 'off'
    intent_info['web_info'] = ''
    intent_info['web_search_keyword'] = ''
    intent_info['web_search_detail'] = 'false'
    intent_info['is_intent_image'] = 'off'
    intent_info['image_info'] = ''
    intent_info['is_intent_smalltalk_answer'] = 'off'  # 잡담 답변 가능성
    intent_info['smalltalk_query'] = ''  # AI가 생성한 잡담 내용

    query_trans = {'origin': query, 'text': '', 'source': '', 'time': '0'}

    def generate():
        answer_list = list()
        reply_len = 0
        
        # 답변 시작 신호 전송
        yield json.dumps({
            "type": constants.RESPONSE_TYPE_THINKING,
            "intent_info": intent_info,
            "chat_idx": chat_idx
        }) + '\n'

        print('##Gemini answer start')
        
        for j, reply_list in enumerate(util_gemini.process_stream(
            query, player_name, char_name, True, 
            info_img=None, is_regenerate=is_regenerate, 
            memory_list=memory, lang=lang_infer_type, 
            api_key=api_key_Gemini, guideline_list=guideline_list, 
            situation_dict=situation_dict
        )):
            if reply_len < len(reply_list):                
                reply_len = len(reply_list)
                
                print('##lang_infer_type', lang_infer_type)
                
                reply_new = reply_list[-1]
                result_en = reply_new
                if lang_infer_type != 'en':
                    try:
                        translate_en = translator.translate_formality(reply_new, 'en')
                        result_en = translate_en['text']
                        if 'source' in translate_en:
                            ai_info['translator'] = translate_en['source']
                    except:
                        pass                     
                        
                result_ko = reply_new
                if lang_infer_type != 'ko':
                    try:
                        translate_ko = translator.translate_formality(reply_new, 'ko')
                        result_ko = translate_ko['text']
                        if 'source' in translate_ko:
                            ai_info['translator'] = translate_ko['source']
                    except:
                        pass                     
                        
                result_jp = reply_new
                if lang_infer_type != 'ja':
                    try:
                        translate_ja = translator.translate_formality(reply_new, 'ja')
                        result_jp = translate_ja['text']
                        if 'source' in translate_ja:
                            ai_info['translator'] = translate_ja['source']
                    except:
                        pass                     
                        
                answer = dict()
                answer['answer_en'] = result_en                
                answer['answer_ko'] = result_ko
                answer['answer_jp'] = result_jp           
                answer_list.append(answer)
                
                # 사용 ai 관련 정보
                ai_info['server_type'] = 'Google'
                ai_info['model'] = state.model_name
                ai_info['prompt'] = lang_infer_type + '/' + char_name
                ai_info['lang_used'] = lang_infer_type
                ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                
                print('### ai_info', ai_info)
                
                yield json.dumps({
                    "type": constants.RESPONSE_TYPE_REPLY, 
                    "reply_list": answer_list, 
                    "query": query_trans, 
                    "ai_info": ai_info, 
                    "intent_info": intent_info,
                    "chat_idx": chat_idx, 
                    "ai_language_out": ai_language_out
                }) + '\n'
                
        if state.get_DEV_MODE():
            print('conversation_stream_gemini|answer_list : ' + str(answer_list))  
            state.write_log('conversation_stream_gemini|answer_list : ' + str(answer_list))
            
    return Response(generate(), content_type='application/json')

# wav에 번역포함 답변 답변
@app.route('/stt', methods=['POST'])
def main_stream_stt():  # main logic
    def transcribe_audio_to_text(audio_path, expected_stt_lang='en', model_name= "small") -> str:
        from faster_whisper import WhisperModel
        try:
            # Load the Whisper model
            print(f"Loading Whisper model: {model_name}...")
            model = WhisperModel(model_name, device="cpu", download_root='./model')
            
            # Transcribe the audio file
            print(f"Transcribing {audio_path}...")
            segments, info = model.transcribe(audio_path)
            text =""
            for segment in segments:
                text = text + segment.text 
            if state.get_DEV_MODE():
                print('stt response :', text.lower(), '-', info.language)    
                
            return text.lower(), info.language
        except Exception as e:
            print(f"Error occurred during transcription: {e}")
            return ""
    
    # STT 변수
    try:
        # Get 'lang' and 'level' from the form data
        stt_lang = request.form.get('lang', 'ko') 
        stt_level = request.form.get('level', 'small')  
        stt_chat_idx = request.form.get('chatIdx', '-1')  
        
        # Handle the uploaded file
        if 'file' not in request.files:
            return jsonify({"error": "No file uploaded"}), 400
        file = request.files['file']
        
        # 파일 저장
        audio_path = os.path.join('./files', f"{uuid.uuid4()}.wav")  # 충돌방지용
        os.makedirs('./files', exist_ok=True)
        file.save(audio_path)
        
        # 최소 0.3초 이상일 경우에만 판단
        trim_silence_len = util_silerovad.get_trim_silence_len(audio_path)
        if  trim_silence_len < 0.3:  # '안녕' 정도의 길이
            print(f"too short wav : {trim_silence_len}s")
            # Test용 파일저장
            if state.get_DEV_MODE():
                stt_file_name = "stt_" + str(datetime.now().strftime("%y%m%d_%H%M%S")) + "_short.wav"
                stt_audio_path = os.path.join('./test/stt', stt_file_name)  # 충돌방지용
                os.makedirs('./test/stt', exist_ok=True) 
                # 기존 저장된 파일을 복사
                shutil.copy(audio_path, stt_audio_path)
                
            # Clean up the temporary file
            os.remove(audio_path)  # 충돌주의
                
            return jsonify({"error": f"too short wav : {trim_silence_len}s"}), 500 

        # Transcribe the audio file
        trans_text, trans_lang = transcribe_audio_to_text(audio_path, stt_lang, stt_level)
        
        import ai_name_checker
        # 이름 체크 및 수정
        if True:
            try:
                trans_text_checked = ai_name_checker.correct_name(
                    trans_text, 
                    ['arona', 'plana'], 
                    trans_lang
                )
                
                # 정확한 비교 + None 체크 + 대소문자 무시
                if trans_text_checked and trans_text_checked.lower() != 'none':
                    trans_text = trans_text_checked
                    print(f"[이름 수정] {trans_text} (from original)")
                else:
                    print(f"[이름 수정 없음] {trans_text}")
                    
            except Exception as e:
                print(f"[이름 체크 오류] {e}")
                # 오류 발생 시 원본 유지
                pass
        
        # Test용 파일저장
        if state.get_DEV_MODE():
            try:
                stt_file_name = "stt_" + str(datetime.now().strftime("%y%m%d_%H%M%S")) + "_" + trans_text + ".wav"
                stt_audio_path = os.path.join('./test/stt', stt_file_name)  # 충돌방지용
                os.makedirs('./test/stt', exist_ok=True)
                # 기존 저장된 파일을 복사
                shutil.copy(audio_path, stt_audio_path)
            except:
                # trans_text가 파일명으로 쓰기 힘든 경우 우려
                print('fail saving stt wav')

        # Clean up the temporary file
        os.remove(audio_path)  # 충돌남

        # Build the response
        response = {"text": trans_text, "lang": trans_lang, "chatIdx":stt_chat_idx}
        return jsonify(response), 200

    except Exception as e:
        print(f"Error in /stt endpoint: {e}")
        return jsonify({"error": "Internal server error"}), 500
    # finally:
    #     # 파일 삭제
    #     if os.path.exists(audio_path):
    #         os.remove(audio_path)

# 음성 화자 분석 및 필터링
@app.route('/speech_diarization', methods=['POST'])
def speech_diarization_filter():
    """
    음성 화자 분석을 통한 음성 필터링
    
    Parameters:
    - file: wav 파일
    - player: 플레이어 이름 (기본값: sensei)
    - char: 캐릭터 이름 (기본값: arona)
    - ai_voice_filter_idx: 필터 모드
      - 0: 무조건 False 반환 (무시)
      - 1: 캐릭터와 음성 일치 여부에 따라 True/False
      - 2: 무조건 False 반환 (무시)
    
    Returns:
    - should_ignore: bool (True면 무시해야 함)
    - similarity: float (유사도 점수, mode=1일 때만 유효)
    - character: str (비교 대상 캐릭터)
    """
    try:
        # 파라미터 가져오기
        player_name = request.form.get('player', 'sensei')
        char_name = request.form.get('char', 'arona')
        ai_voice_filter_idx = request.form.get('ai_voice_filter_idx', '0')
        
        # 파일 체크
        if 'file' not in request.files:
            return jsonify({
                "error": "No file uploaded",
                "should_ignore": True,
                "similarity": 0.0,
                "character": char_name
            }), 400
            
        file = request.files['file']
        
        # 파일 저장
        audio_path = os.path.join('./files', f"speech_check_{uuid.uuid4()}.wav")
        os.makedirs('./files', exist_ok=True)
        file.save(audio_path)
        
        # 필터 모드에 따른 처리
        if ai_voice_filter_idx == '0':
            # 모드 0: 무조건 False (무시하지 않음)
            result = {
                "should_ignore": False,
                "similarity": 0.0,
                "character": char_name,
                "mode": "disabled"
            }
            
        elif ai_voice_filter_idx == '1':
            # 모드 1: 캐릭터 음성 일치 여부 확인
            try:
                # 음성 화자 분석 수행
                speaker_result = util_speech_diarization.identify_speaker(
                    input_audio_path=audio_path,
                    character_name=char_name,
                    threshold=0.6,  # 기본 임계값
                    model_type='ecapa',  # 기본 모델 (빠른 처리용)
                    use_gpu=False  # CPU 사용 (안정성 우선)
                )
                
                similarity = float(speaker_result['similarity'])  # numpy float을 Python float으로 변환
                is_match = bool(speaker_result['is_match'])  # numpy bool_을 Python bool로 변환
                
                result = {
                    "should_ignore": is_match,  # 일치하면 무시
                    "similarity": similarity,
                    "character": char_name,
                    "threshold": float(speaker_result['threshold']),  # 이것도 변환
                    "mode": "character_match",
                    "match_status": "match" if is_match else "no_match"
                }
                
            except Exception as e:
                
                # 에러 발생시 안전하게 무시하지 않음
                result = {
                    "should_ignore": False,
                    "similarity": 0.0,
                    "character": char_name,
                    "mode": "error",
                    "error": str(e),
                    "error_type": type(e).__name__
                }
                
        elif ai_voice_filter_idx == '2':
            # 모드 2: 무조건 False (무시하지 않음)
            result = {
                "should_ignore": False,
                "similarity": 0.0,
                "character": char_name,
                "mode": "disabled"
            }
            
        else:
            # 잘못된 모드: 안전하게 무시하지 않음
            result = {
                "should_ignore": False,
                "similarity": 0.0,
                "character": char_name,
                "mode": "invalid",
                "error": f"Invalid ai_voice_filter_idx: {ai_voice_filter_idx}"
            }
        
        # 임시 파일 삭제
        if os.path.exists(audio_path):
            os.remove(audio_path)
        
        return jsonify(result), 200
        
    except Exception as e:
        print(f"[ERROR] Main exception in /speech_diarization endpoint: {type(e).__name__}: {e}")
        import traceback
        print(f"[ERROR] Main traceback: {traceback.format_exc()}")
        
        # 임시 파일 삭제 (에러 시에도)
        try:
            if 'audio_path' in locals() and os.path.exists(audio_path):
                print(f"[DEBUG] Cleaning up temp file: {audio_path}")
                os.remove(audio_path)
        except Exception as cleanup_e:
            print(f"[ERROR] Error during cleanup: {cleanup_e}")
            
        return jsonify({
            "error": "Internal server error",
            "should_ignore": False,  # 에러 시 안전하게 무시하지 않음
            "similarity": 0.0,
            "character": char_name if 'char_name' in locals() else 'unknown',
            "error_details": str(e),
            "error_type": type(e).__name__
        }), 500

# TODO : DeepL 번역 비활성화 등의 선택하기
@app.route('/reset_translator', methods=['GET'])
def reset_translator():
    translator.get_freeDeepLFreeUrls()
    return jsonify({"result": "reseted"}), 200

@app.route('/health', methods=['GET'])
def health():
    return jsonify({"status": "healthy"}), 200

# 무료 서버 연결 가능 체크
@app.route('/health_free_server', methods=['GET'])
def health_free_server():
    print('health_free_server start')
    FAIL_CHECK_WORD = "The"  # The server seems to be busy, Sensei. Could you please say that again?

    api_key_Gemini = request.form.get('api_key_Gemini', '')  # on, off / 표정 반영
    api_key_OpenRouter = request.form.get('api_key_OpenRouter', '')  # on, off / 표정 반영

    try:
        test_query = "Hello"
        player_name = "m9dev"
        char_name = "arona"

        # 1차 시도: Google Gemini
        try:
            for reply_list in util_gemini.process_stream(
                test_query, player_name, char_name,
                is_sentence=True, is_regenerate=False,
                memory_list=[], api_key=api_key_Gemini
            ):
                if reply_list and reply_list[0].startswith(FAIL_CHECK_WORD):
                    continue  # 실패 응답으로 간주
                if len(reply_list) > 0:
                    print('health_free_server end : Google')
                    return jsonify({
                        "status": "available",
                        "server_type": "Google",
                        "reply": reply_list[0]
                    }), 200
        except Exception as e:
            print("Google Gemini failed:", e)

        # 2차 시도: OpenRouter
        try:
            for reply_list in util_openrouter.process_stream(
                test_query, player_name, char_name,
                is_sentence=True, is_regenerate=False,
                memory_list=[], api_key=api_key_OpenRouter
            ):
                if reply_list and reply_list[0].startswith(FAIL_CHECK_WORD):
                    continue  # 실패 응답으로 간주
                if len(reply_list) > 0:
                    print('health_free_server end : OpenRouter')
                    return jsonify({
                        "status": "available",
                        "server_type": "OpenRouter",
                        "reply": reply_list[0]
                    }), 200
        except Exception as e:
            print("OpenRouter failed:", e)

        # 둘 다 실패했을 경우
        print('health_free_server end : Failed')
        return jsonify({
            "status": "unavailable",
            "server_type": None,
            "error": "no valid reply from both Google and OpenRouter"
        }), 500

    except Exception as e:
        return jsonify({
            "status": "error",
            "server_type": None,
            "message": str(e)
        }), 500

# 플랫폼 연결 가능 체크(KEY 필수)
@app.route('/health_check_platform', methods=['GET'])
def health_check_platform():
    FAIL_CHECK_WORD = "The"  # The server seems to be busy, Sensei. Could you please say that again?

    target = request.args.get('target', default='', type=str)
    api_key = request.args.get('api_key', default='', type=str).strip()
    test_query = "Hello"
    player_name = "m9dev"
    char_name = "arona"

    # api_key가 반드시 필요한 플랫폼
    api_key_required_targets = ["ChatGPT", "Google", "OpenRouter"]
    if target in api_key_required_targets and not api_key:
        return jsonify({"status": "unavailable", "server_type": target}), 200

    try:
        if target == "ChatGPT":
            import util_chatGPT
            for reply_list in util_chatGPT.process_stream(test_query, player_name, char_name, True, False, memory_list=[], api_key=api_key):
                if reply_list and not reply_list[0].startswith(FAIL_CHECK_WORD):
                    return jsonify({"status": "available", "server_type": "ChatGPT"}), 200

        elif target == "Google":
            import util_gemini
            for reply_list in util_gemini.process_stream(test_query, player_name, char_name, True, False, memory_list=[], api_key=api_key):
                if reply_list and not reply_list[0].startswith(FAIL_CHECK_WORD):
                    return jsonify({"status": "available", "server_type": "Google"}), 200

        elif target == "OpenRouter":
            import util_openrouter
            for reply_list in util_openrouter.process_stream(test_query, player_name, char_name, True, False, memory_list=[], api_key=api_key):
                if reply_list and not reply_list[0].startswith(FAIL_CHECK_WORD):
                    return jsonify({"status": "available", "server_type": "OpenRouter"}), 200

        else:
            return jsonify({"status": "error", "message": f"Unknown target: {target}"}), 400

        return jsonify({"status": "unavailable", "server_type": target}), 200

    except Exception as e:
        print(f"[health_check_platform] error: {e}")
        return jsonify({"status": "error", "server_type": target, "message": str(e)}), 500

# cuda 되는지 확인 (C# 자체 기능이 더 좋음)
import pynvml
@app.route('/check_cuda', methods=['GET'])
def check_cuda():
    try:
        pynvml.nvmlInit()
        device_count = pynvml.nvmlDeviceGetCount()
        pynvml.nvmlShutdown()
        if device_count > 0:
            return jsonify({"nvidia_gpu_detected": True})
        else:
            return jsonify({"nvidia_gpu_detected": False})
    except Exception as e:
        return jsonify({"nvidia_gpu_detected": False, "error": str(e)}), 500

# 모델 해제
@app.route('/model/release', methods=['POST'])
def release():
    ai_singleton.release()    
    print('Server release')
    return jsonify({"response": "release"}), 200

# 모델 (재)로딩
@app.route('/model/load', methods=['POST'])
def load_model():
    try:
        # 기존 로딩 해제
        ai_singleton.release() 
         
        model_name_Local = request.json.get('model_name_Local', '')
        server_local_mode = request.json.get('server_local_mode', '')
        print(f'### server_local_mode : {server_local_mode}')
        print(f'### model_name_Local : {model_name_Local}')
        
        state.model_name = model_name_Local
        if server_local_mode == 'GPU':
            is_use_cuda=True
            state.set_var_from_model(state.model_name)
            state.use_vram = 8
            state.set_use_gpu_percent(8)
            state.set_n_gpu_layers()
        else:
            is_use_cuda=False
            state.set_var_from_model("erase") # CPU
            state.use_vram = 0
            state.set_use_gpu_percent(0)
            state.set_n_gpu_layers()
  
        ai_conversation.load_model(is_use_cuda)

        # # 새 모델 로드 (서버가 준비될 때까지 블로킹됨)
        llm = ai_singleton.get_llm()
        
        # 서버 정상 작동 테스트
        for j, reply_list in enumerate(ai_conversation.process_stream('안녕', 'sensei', 'noa', True, False, lang='ko')):
            pass
        
        # 프로세스 정보 수집
        import os
        process_info = {
            "server_pid": os.getpid(),  # Flask 서버의 PID
            "llm_process_pid": None,  # LLM 서버 프로세스의 PID
            "llm_port": None,  # LLM 서버 포트
            "llm_model_path": None,  # 로드된 모델 경로
        }
        
        # LLM 프로세스 정보 가져오기
        if hasattr(llm, 'process') and llm.process is not None:
            process_info["llm_process_pid"] = llm.process.pid
        if hasattr(llm, 'port'):
            process_info["llm_port"] = llm.port
        if hasattr(llm, 'model_path'):
            process_info["llm_model_path"] = llm.model_path
            
        print(f'Server loaded - Process Info: {process_info}')
        return jsonify({
            "response": "loaded", 
            "process_info": process_info
        }), 200
    except Exception as e:
        print(f'Model load failed: {str(e)}')
        return jsonify({"response": "failed", "error": str(e)}), 500

# 한국어 텍스트를 입력받아 변환
@app.route('/getSound/jp', methods=['POST'])  # legacy
@app.route('/getSound/ko', methods=['POST'])  # legacy
@app.route('/getSound', methods=['POST'])
def synthesize_sound():
    def get_sound_text_ja(text):
        # text = text.lower()
        text = text.replace('RABBIT', 'ラビット')
        text = text.replace('SCHALE', 'シャーレ')
        return text   
    
    if state.get_DEV_MODE():
        print('###getSound request', request.json)
    text = request.json.get('text', '안녕하십니까.')
    char = request.json.get('char', 'arona')
    lang = request.json.get('lang', 'ko')
    speed = request.json.get('speed', 100)  # % 50~100
    speed = float(speed)/100 
    chat_idx = request.json.get('chatIdx', '-1')
    is_furigana = request.json.get('is_furigana', 'off')  # 후리가나 전처리 옵션 (기본값: off)
    
    if lang == 'ja' or lang =='jp':
        lang = 'ja'  # 단어보정
        text = get_sound_text_ja(text)
        
        # 후리가나 전처리 (is_furigana가 'on'일 경우에만 적용)
        if is_furigana == 'on':
            try:
                import util_japanese_fix
                text = util_japanese_fix.ocr_postprocess(text, verbose=False)
                if state.get_DEV_MODE():
                    print(f'###getSound furigana processed: {text}')
            except Exception as e:
                if state.get_DEV_MODE():
                    print(f'###getSound furigana error: {e}')
                # 에러 발생 시 원본 텍스트 유지
    
    text = text.replace("\n",'')  # 텍스트 중 \n 있으면 망가짐.

    result = voice_inference.synthesize_char(char, text, audio_language=lang, speed=speed)  # 'output*.wav'
    if result == 'early stop':
        abort(500, description="Synthesis process stopped early.")
    response = send_file(result, mimetype="audio/wav")
    response.headers['Chat-Idx'] = chat_idx
    return response
    

# http://localhost:5000/getSound/test/?text=안녕&char=noa&lang=ko
# http://localhost:5000/getSound/test/?text=お疲れ様です。&char=noa&lang=ja
# @app.route('/getSound/test/')
# def synthesize_sound_get(text=None):
#     text = request.args.get('text', default='안녕하십니까', type=str)
#     char = request.args.get('char', default='arona', type=str)
#     lang = request.args.get('lang', default='ko', type=str)

#     result = voice_inference.synthesize_char(char, text, audio_language=lang)
#     return send_file('output.wav', mimetype="audio/wav")

def get_available_vram_gb_for_server(max_vram = 8):
    from pynvml import nvmlInit, nvmlDeviceGetHandleByIndex, nvmlDeviceGetMemoryInfo, nvmlShutdown
    try:
        nvmlInit()
        handle = nvmlDeviceGetHandleByIndex(0)  # 0번 GPU
        info = nvmlDeviceGetMemoryInfo(handle)
        available_vram_mb = info.free // 1024**3  # 바이트를 GB로 변환
        nvmlShutdown()
        return min(available_vram_mb-1, max_vram)  # 여유 vram 1GB 남기기
    except Exception as e:
        # print(f"Failed to get VRAM info: {e}")
        return 0  # 기본값 8GB, 예외 발생 시

def load_id_from_config():
    config_path = "./config/server.json"
    if os.path.exists(config_path):
        with open(config_path, "r") as f:
            data = json.load(f)
            return data.get("id", "")
    return ""

def start_server():
    print('Server Start')
    # app.run(host='0.0.0.0', port=5000)  # For Test
    serve(app, host="0.0.0.0", port=5000)

def set_max_vram():
    global server_config
    max_vram = 8  # Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf
    if 'model_type' not in server_config:
        return max_vram
    if server_config['model_type'] == 'Qwen2.5-7B-Instruct-1M-Q4_K_M.gguf':
        max_vram = 7
    if server_config['model_type'] == 'Qwen2.5-14B-Instruct-1M-Q4_K_M.gguf':
        max_vram = 12
    if server_config['model_type'] == 'Qwen3-8B-Q4_K_M.gguf':
        max_vram = 8
    elif server_config['model_type'] == 'Qwen3-14B-Q4_K_M.gguf':
        max_vram = 12
    elif server_config['model_type'] == 'Qwen3-32B-Q4_K_M.gguf':
        max_vram = 24
    elif server_config['model_type'] == 'Qwen3-4B-Q4_K_M.gguf':
        max_vram = 4
    elif server_config['model_type'] == 'Qwen3-1.7B-Q4_K_M.gguf':
        max_vram = 3
    elif server_config['model_type'] == 'Qwen3-0.6B-Q4_K_M.gguf':
        max_vram = 2
  
    return max_vram

# Server 관련 설정 json에서 읽기
def load_server_config():
    config_path = "./config/server.json"
    # 기본 설정값
    default_config = {
        "id": "",
        "model_type": "",
        "server_type": "OpenRouter",
        "ngrok_api_key": "",
        "deepl_api_key": "",
        "is_deepl_use": False
    }
    
    if os.path.exists(config_path):
        with open(config_path, "r") as f:
            config = json.load(f)
    else:
        config = {}

    # 기본값을 설정
    for key, value in default_config.items():
        config.setdefault(key, value)
    
    return config

@app.route('/set_server_info', methods=['POST'])
def set_server_info():
    global server_config
    config_path = "./config/server.json"

    # 기존 설정값 불러오기
    config = load_server_config()

    # 요청에서 전달된 설정만 반영
    for key in request.json:
        config[key] = request.json[key]

    # 파일로 저장
    with open(config_path, "w") as f:
        json.dump(config, f, indent=4)

    # server_config 갱신
    server_config = config

    return jsonify({"status": "updated", "server_config": server_config}), 200

# server_test용 변수 설정
def init_server_var():
    import platform
    import nltk
    import pygame
    import sys
    from ai_conversation_binary_shared import parser

    # 서버 설정 로딩
    global server_config
    server_config = load_server_config()

    # 최대 VRAM 설정
    max_vram = set_max_vram()

    # 번역기 초기화
    global translator
    translator = util_translator.Translator()
    translator.get_freeDeepLFreeUrls()
    print('### translator setted')

    # 오디오 믹서 초기화
    pygame.mixer.init()

    # 기본값
    server_type = "GPU"
    id = 'temp'

    # 실행 환경 확인
    if sys.argv[0].endswith('.py'):
        print('server from python')
        use_vram = get_available_vram_gb_for_server(max_vram)
        state.use_vram = use_vram
        id = 'test'
        state.is_write_log_file = True
        state.set_DEV_MODE(True)
    else:
        try:
            args = parser.parse_args()
            use_vram = args.use_vram
            state.use_vram = args.use_vram
            server_type = args.server_type
            id = args.id if args.id else 'temp'
            if id in ('dev', 'test'):
                state.set_DEV_MODE(True)
            print('## ID :', id)
        except:
            use_vram = get_available_vram_gb_for_server(max_vram)
            state.use_vram = use_vram
            id = 'temp'

    # 모델 관련 설정
    state.model_name = server_config.get("model_type", "Qwen3-8B-Q4_K_M.gguf")
    server_config["model_type"] = state.model_name
    state.set_var_from_model(state.model_name)
    state.set_n_gpu_layers()

    # 필수 다운로드
    nltk.download('averaged_perceptron_tagger_eng')

if __name__ == '__main__': 
    # UTF-8 인코딩 설정
    os.environ['PYTHONIOENCODING'] = 'utf-8'
    
    # args 관련 자료
    from ai_conversation_binary_shared import parser

    server_config = load_server_config()
    max_vram = set_max_vram()
    
    # trayicon 소환(Windows)
    if platform.system() == "Windows":
        try:
            tray = IconTrayApp()
            tray.start_in_thread()
            tray.hide_console()
        except:
            print('cannot make tray icon')

    # TODO : 로컬화할 경우, 영향도 파악 (현재 패키징은 가능)
    nltk.download('averaged_perceptron_tagger_eng')

    # 번역기 키기
    translator = util_translator.Translator()
    translator.get_freeDeepLFreeUrls()
    # translator.load_deep_api_key()
    print('### tranlator setted')
    
    pygame.mixer.init()
    
    server_type = "GPU"
    id='temp'    
    # 실행 환경 감지
    if sys.argv[0].endswith('.py'):  # .py 로 끝남 = vs_code
        print('server from python')
        use_vram = get_available_vram_gb_for_server(max_vram)
        state.use_vram = use_vram
        id='test'
        state.is_write_log_file = True
        state.set_DEV_MODE(True)
    else:  # exe 실행
        if len(sys.argv) > 1: 
            print('server from config_exe')
            try:
                # 명령어 인자 처리 (ai_conversation_binary_shared에 갱신)
                # parser = argparse.ArgumentParser(description="Start the server with specific parameters")
                # parser.add_argument('--server_type', type=str, choices=['GPU', 'CPU'], default='GPU', help="Type of server to run (GPU or CPU)")
                # parser.add_argument('--use_vram', type=int, default=8, help="Amount of VRAM to use (in GB)")
                # parser.add_argument('--id', type=str, default='temp', help="ID of server")
                args = parser.parse_args()

                is_unity = args.is_unity
                state.is_unity = True if is_unity else False
                print('## is_unity : ', state.is_unity)
                use_vram = args.use_vram
                state.use_vram = args.use_vram
                print('## use_vram : ', use_vram)
                server_type = args.server_type
                print('## server_type : ', server_type)
                id = args.id
                if not id:
                    id = 'temp'
                print('id', id)          
                if id in ('dev', 'test'):
                    state.set_DEV_MODE(True)
                print('## ID : ' + id)
            except:
                print('server from config_exe : args failed')
                use_vram = get_available_vram_gb_for_server(max_vram)
                state.use_vram = use_vram
                id = 'temp'
        else:
            print('server from exe')
            use_vram = get_available_vram_gb_for_server(max_vram)
            state.use_vram = use_vram
            id=load_id_from_config()  # config에 id가 있을 경우 사용
            if not id:
                id = 'temp'
            if id in ('dev', 'test'):
                state.set_DEV_MODE(True)
            
            print('server from exe. ID : ' + id)
        
    # Server Config 대로 변수 세팅
    state.model_name = server_config["model_type"]  # 8 ex) 'Qwen_Qwen3-8B-Q4_K_M.gguf'
    if not state.model_name:
        state.model_name = 'Qwen3VL-8B-Instruct-Q4_K_M.gguf'
        server_config["model_type"] = 'Qwen3VL-8B-Instruct-Q4_K_M.gguf'
    
    state.set_var_from_model(state.model_name)
    state.set_n_gpu_layers()
        
    # preloading - TTS 모델
    try:
        voice_inference.synthesize_char('noa', '안녕하세요!', audio_language='ja')
        print('[SUCCESS] TTS preloading completed')
    except Exception as e:
        print(f'[WARNING] TTS preloading failed: {e}')
        print('[INFO] TTS will be loaded on first request.')
    
    # preloading2 - LLM 모델
    if state.get_DEV_MODE() and False:
        print('[INFO] DEV MODE : preloading disabled')
        if server_config['server_type'] not in ("OpenRouter", "Google") and not state.is_unity:  # OpenRouter, Google은 preloading 없음 # Todo 유니티로 시작할 경우, 서버 시작 없음
            try:
                state.set_use_gpu_percent(use_vram)  # 8,12 = GPU 100%
                print(f"Init Option - vram: {use_vram}/{max_vram} GB")
                    
                for j, reply_list in enumerate(ai_conversation.process_stream('반드시 짧게 1문장으로 대답해줘. 안녕? ', 'sensei', 'noa', True, False, lang='ko')):
                    pass
                print('[SUCCESS] LLM preloading completed')
            except Exception as e:
                print(f'[WARNING] LLM preloading failed: {e}')
                print('[INFO] Server will continue without preloading. Model will load on first request.')
        # print(f"Server Type : {server_config['server_type']}")
    
    # Tunnel
    if state.get_DEV_MODE():
        if server_config['server_type'] == "Server":  # env여부
            util_pyngrok.start_ngrok(id=id, key=server_config['ngrok_api_key'])
        else:
            print('Making Local Server...')
    
    print('Server Start')
    # app.run(host='0.0.0.0', port=5000)  # For Test
    # serve(app, host="0.0.0.0", port=5000)  # For Production
    
    server = create_server(app, host="0.0.0.0", port=5000)
    print(f"Local Server at {server.effective_host}:{server.effective_port} ...")
    server.run()  # For Production
