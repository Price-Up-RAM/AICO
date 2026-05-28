'''
multi_server.py
새로운 Multi-Conversation 서버 엔드포인트

server_type 파라미터로 Gemini/Local/Hybrid 분기:
- 'Gemini': Flow + 대화 모두 Gemini API
- 'Local': Flow + 대화 모두 Local LLM
- 'Hybrid': Flow는 Local LLM (빠름), 대화 생성은 Gemini API (고품질)

기존 server_multi_impl.py 로직 참조하되 완전 분리된 구조
'''

from flask import Flask, request, jsonify, Response
from datetime import datetime
import json
import logging
from typing import Dict, List, Optional, Tuple

# 공통 모듈
import prompt_char
import state
import util_translator
import ai_emotion_classification

# multi_ 모듈들 (새로 생성된 모듈)
import multi_gemini
import multi_local

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Global variables
translator = None


def set_translator(translator_instance):
    """외부에서 번역기 인스턴스를 주입하기 위한 함수"""
    global translator
    translator = translator_instance
    return True


# ============================================================================
# 기본 참여자 생성 함수들
# ============================================================================

def create_default_aropla_participants() -> List[Dict]:
    """기본 아로프라 채널 참여자 생성"""
    return [
        {"name": "sensei", "type": "user", "display_name": "선생님"},
        {"name": "arona", "type": "ai", "display_name": "아로나", "character_file": "arona"},
        {"name": "plana", "type": "ai", "display_name": "프라나", "character_file": "plana"}
    ]


def create_default_participants(conversation_type: str) -> List[Dict]:
    """대화 타입별 기본 참여자 생성"""
    if conversation_type == 'aropla':
        return create_default_aropla_participants()
    elif conversation_type == 'teaparty':
        return create_default_aropla_participants()
    else:
        return create_default_aropla_participants()


def process_participants(input_participants: List[Dict], conversation_type: str) -> List[Dict]:
    """입력받은 participants를 검증하고 누락된 기본 참여자를 추가"""
    if not input_participants:
        return create_default_participants(conversation_type)
    
    final_participants = []
    existing_names = set()
    
    for participant in input_participants:
        if isinstance(participant, dict) and "name" in participant:
            validated_participant = {
                "name": participant["name"],
                "type": participant.get("type", "ai" if participant["name"] != "sensei" else "user"),
                "display_name": participant.get("display_name", participant["name"]),
                "character_file": participant.get("character_file", participant["name"] if participant["name"] != "sensei" else None)
            }
            final_participants.append(validated_participant)
            existing_names.add(participant["name"])
    
    default_participants = create_default_participants(conversation_type)
    for default_participant in default_participants:
        if default_participant["name"] not in existing_names:
            final_participants.append(default_participant)
    
    return final_participants


def get_first_ai_participant(participants: List[Dict]) -> str:
    """참여자 목록에서 첫 번째 AI 참여자의 이름을 반환"""
    for participant in participants:
        if participant.get("type") == "ai":
            return participant["name"]
    return "arona"


def get_next_speaker_fallback(current_speaker: str, memory_list: List[Dict], participants: List[Dict] = None) -> Tuple[str, str]:
    """fallback 발화자 결정 로직"""
    if not participants:
        participants = create_default_aropla_participants()
    
    ai_participants = [p["name"] for p in participants if p.get("type") == "ai"]
    first_ai = ai_participants[0] if ai_participants else "arona"
    second_ai = ai_participants[1] if len(ai_participants) > 1 else None
    
    if current_speaker == 'sensei':
        next_speaker = first_ai
        reasoning = f"사용자 후 {first_ai} 응답 (기본 로직)"
    elif current_speaker == first_ai and second_ai:
        recent_speakers = [turn.get('speaker', '') for turn in memory_list[-3:]] if memory_list else []
        if second_ai not in recent_speakers:
            next_speaker = second_ai
            reasoning = f"{second_ai}가 아직 참여하지 않아서 {second_ai} 차례 (기본 로직)"
        else:
            next_speaker = 'sensei'
            reasoning = f"{first_ai} 후 사용자 차례 (기본 로직)"
    elif current_speaker in ai_participants:
        next_speaker = 'sensei'
        reasoning = f"{current_speaker} 후 사용자 차례 (기본 로직)"
    else:
        next_speaker = first_ai
        reasoning = f"알 수 없는 상황, {first_ai} 차례로 설정 (기본 로직)"
    
    return next_speaker, reasoning


def translate_response(reply: str, source_lang: str) -> Tuple[str, str, str]:
    """응답을 다국어로 번역"""
    result_ko = reply
    result_jp = reply
    result_en = reply
    
    if not translator:
        logger.warning("Translator not initialized, returning original text")
        return result_ko, result_jp, result_en
    
    try:
        if source_lang != 'ko':
            try:
                translate_ko = translator.translate_formality(reply, 'ko')
                result_ko = translate_ko['text'] if isinstance(translate_ko, dict) and 'text' in translate_ko else reply
            except:
                pass
        
        if source_lang != 'ja':
            try:
                translate_ja = translator.translate_formality(reply, 'ja')
                result_jp = translate_ja['text'] if isinstance(translate_ja, dict) and 'text' in translate_ja else reply
            except:
                pass
        
        if source_lang != 'en':
            try:
                translate_en = translator.translate_formality(reply, 'en')
                result_en = translate_en['text'] if isinstance(translate_en, dict) and 'text' in translate_en else reply
            except:
                pass
    
    except Exception as e:
        logger.error(f"Translation error: {e}")
    
    return result_ko, result_jp, result_en


# ============================================================================
# 메인 API 엔드포인트
# ============================================================================

@app.route('/multi/conversation', methods=['POST'])
def multi_conversation():
    """
    범용 다중 대화 스트리밍 API
    
    server_type 파라미터로 Gemini/Local/Hybrid 분기:
    - 'Gemini': Flow + 대화 모두 Gemini API (고품질, API 비용)
    - 'Local': Flow + 대화 모두 Local LLM (무료, 로컬 GPU)
    - 'Hybrid': Flow는 Local LLM (빠름), 대화 생성은 Gemini API (고품질)
    """
    
    start_time = datetime.now()
    
    if state.get_DEV_MODE():
        print('multi_conversation request:', request.form)
    
    # Form 데이터 파싱
    query = request.form.get('query', '')
    player_name = request.form.get('player', 'sensei')
    current_speaker = request.form.get('current_speaker', 'sensei')
    target_speaker = request.form.get('target_speaker')
    chat_idx = request.form.get('chatIdx', '-1')
    multi_conversation_type = request.form.get('multi_conversation_type', '')
    conversation_type = (multi_conversation_type or '').lower() or 'aropla'
    
    # 서버 타입 (핵심 파라미터)
    server_type = request.form.get('server_type', 'Gemini')  # 기본값: Gemini
    
    # 설정 파라미터
    ai_language = request.form.get('ai_language', 'ko')
    ai_emotion = request.form.get('ai_emotion', 'off')
    memory = request.form.get('memory', '[]')
    guideline_list_raw = request.form.get('guideline_list', '[]')
    situation_raw = request.form.get('situation', '{}')
    participants_raw = request.form.get('participants', '[]')
    
    intent_smalltalk = request.form.get('intent_smalltalk', 'off')
    max_ai_consecutive = int(request.form.get('max_ai_consecutive', '10'))
    cur_ai_consecutive = int(request.form.get('cur_ai_consecutive', '0'))
    
    is_player_next_speaker = False
    if cur_ai_consecutive - 1 >= max_ai_consecutive:
        is_player_next_speaker = True
    
    # JSON 파싱
    memory_list = []
    if memory:
        try:
            memory_list = json.loads(memory)
        except json.JSONDecodeError:
            memory_list = []
    
    guideline_list = []
    try:
        guideline_list = json.loads(guideline_list_raw)
    except json.JSONDecodeError:
        guideline_list = []
    
    situation_dict = {}
    if situation_raw:
        try:
            situation_dict = json.loads(situation_raw)
        except json.JSONDecodeError:
            situation_dict = {}
    
    participants_list = []
    try:
        participants_list = json.loads(participants_raw) if participants_raw else []
    except json.JSONDecodeError:
        participants_list = []
    
    print(f'### server_type: {server_type}')
    print(f'### query: {query}')
    print(f'### current_speaker: {current_speaker}')
    print(f'### target_speaker: {target_speaker}')
    
    # LLM 모듈 선택 (flow_module: Flow Director용, conversation_module: 대화 생성용)
    server_type_lower = server_type.lower()
    
    if server_type_lower in ['gemini', 'free_gemini', 'auto']:
        # 순수 Gemini: Flow + 대화 모두 Gemini API
        flow_module = multi_gemini
        conversation_module = multi_gemini
        print(f'### Using Gemini API (Pure Gemini Mode)')
    elif server_type_lower == 'hybrid':
        # Hybrid: Flow는 Local (빠름), 대화는 Gemini (고품질)
        flow_module = multi_local
        conversation_module = multi_gemini
        print(f'### Using Hybrid Mode (Flow: Local, Conversation: Gemini)')
    else:
        # 순수 Local: Flow + 대화 모두 Local LLM
        flow_module = multi_local
        conversation_module = multi_local
        print(f'### Using Local LLM (Pure Local Mode)')
    
    def generate():
        """스트리밍 응답 생성"""
        nonlocal target_speaker
        
        try:
            # 1. Flow Director로 발화자 결정 (flow_module 사용)
            if target_speaker:
                next_speaker, reasoning = target_speaker, f"지정된 발화자: {target_speaker}"
            else:
                try:
                    target_from_message, message_reasoning = flow_module.analyze_target_speaker(
                        query, current_speaker, lang=ai_language, memory_list=memory_list
                    )
                    
                    if target_from_message:
                        next_speaker, reasoning = target_from_message, f"메시지 분석: {message_reasoning}"
                        logger.info(f"Message analysis result: '{query[:50]}...' -> {target_from_message}")
                    else:
                        processed_participants = process_participants(participants_list, conversation_type)
                        first_ai = get_first_ai_participant(processed_participants)
                        next_speaker = first_ai
                        reasoning = f"명확한 대상 없음 - 기본 발화자({first_ai}) 선택"
                        
                except Exception as e:
                    logger.warning(f"Flow analysis error: {e}. Using default speaker.")
                    processed_participants = process_participants(participants_list, conversation_type)
                    first_ai = get_first_ai_participant(processed_participants)
                    next_speaker = first_ai
                    reasoning = f"Flow 분석 오류 - 기본 발화자({first_ai}) 선택"
            
            logger.info(f"{conversation_type.upper()}: {current_speaker} -> {next_speaker} ({reasoning})")
            
            # 2. target_listener 결정
            target_listener = "all"
            listener_reasoning = ""
            
            if is_player_next_speaker:
                target_listener = "sensei"
                listener_reasoning = f"AI 연속 대화 제한 도달 - sensei에게 대화 유도"
            else:
                try:
                    if current_speaker == "sensei":
                        target_listener, listener_reasoning = flow_module.analyze_target_listener(
                            query, current_speaker, next_speaker, lang=ai_language, memory_list=memory_list
                        )
                    else:
                        target_listener, listener_reasoning = flow_module.determine_target_listener_from_context(
                            current_speaker, next_speaker, query, memory_list, ai_language
                        )
                except Exception as e:
                    logger.warning(f"Listener analysis error: {e}. Using fallback logic.")
                    if current_speaker == "sensei" and next_speaker in ["arona", "plana"]:
                        target_listener = next_speaker
                        listener_reasoning = f"기본 로직: 선생님 -> {next_speaker}"
                    elif current_speaker in ["arona", "plana"] and next_speaker == "sensei":
                        target_listener = "sensei"
                        listener_reasoning = f"기본 로직: {current_speaker} -> 선생님"
                    else:
                        target_listener = "all"
                        listener_reasoning = "기본 로직: 전체 대화"
            
            logger.info(f"Flow: {current_speaker} -> {next_speaker}, listener: {target_listener}")
            
            # 3. 선생님 차례면 대기 상태 반환
            if next_speaker == 'sensei':
                system_answer_list = [{
                    "answer_en": "Waiting for user input.",
                    "answer_ko": "사용자 입력을 기다리는 중입니다.",
                    "answer_jp": "ユーザー入力をお待ちしております。"
                }]
                
                yield json.dumps({
                    "reply_list": system_answer_list,
                    "message": "사용자 입력을 기다리는 중입니다.",
                    "speaker": "system",
                    "next_speaker": "sensei",
                    "reasoning": reasoning,
                    "ai_language_out": ai_language,
                    "chat_idx": chat_idx,
                    "type": "waiting"
                }) + '\n'
                return
            
            # 4. AI 캐릭터 응답 생성
            target_speaker = next_speaker
            participants = process_participants(participants_list, conversation_type)
            
            # 캐릭터명 검증
            char_name = target_speaker
            if char_name not in prompt_char.get_all_filenames_in_prompt():
                print(f'{char_name} not in prompt_char')
                char_name = 'kivotos_student_normal'
                for p in participants:
                    if p["name"] == target_speaker:
                        p["character_file"] = char_name
            
            lang_infer_type = ai_language
            
            # 5. 스트리밍 응답 생성 (conversation_module 사용)
            response_generator = conversation_module.process_conversation_stream(
                query=query,
                current_speaker=current_speaker,
                target_speaker=target_speaker,
                target_listener=target_listener,
                participants=participants,
                context={"description": f"{conversation_type} 채널 - {current_speaker}에서 {target_speaker}로 응답, 청취자: {target_listener}"},
                is_sentence=True,
                is_regenerate=False,
                info_img=None,
                memory_list=memory_list,
                lang=lang_infer_type,
                guideline_list=guideline_list,
                situation_dict=situation_dict,
                player_name=player_name
            )
            
            # thinking 상태 전송
            yield json.dumps({
                "type": "thinking",
                "chat_idx": chat_idx
            }) + '\n'
            
            # 응답 처리
            answer_list = []
            reply_len = 0
            actual_responding_speaker = target_speaker
            final_response = ""
            
            ai_info = {
                'server_type': f'Multi-{server_type}',
                'model': state.model_name if hasattr(state, 'model_name') else "",
                'prompt': lang_infer_type + '/' + target_speaker,
                'lang_used': lang_infer_type,
                'translator': '',
                'time': '',
                'intent': 'None',
                'emotion': ''
            }
            
            for j, reply_batch in enumerate(response_generator):
                if reply_batch and len(reply_batch) == 2:
                    reply_list, responding_speaker = reply_batch
                    actual_responding_speaker = responding_speaker
                elif reply_batch and isinstance(reply_batch, list):
                    reply_list = reply_batch
                else:
                    continue
                
                if reply_len < len(reply_list):
                    reply_len = len(reply_list)
                    
                    # Emotion 처리
                    if reply_len == 1 and ai_emotion == 'on' and not ai_info['emotion']:
                        try:
                            ai_emotion_classification_reply = reply_list[0]
                            ai_emotion_reply = ai_emotion_classification.process(
                                query, ai_emotion_classification_reply,
                                player_name, target_speaker,
                                memory_list=memory_list, lang=lang_infer_type
                            )
                            if "emotion: " in ai_emotion_reply:
                                emotion_text = ai_emotion_reply.strip().split("emotion: ")[-1].strip().lower()
                                valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']
                                if emotion_text in valid_emotions:
                                    ai_info['emotion'] = emotion_text
                        except Exception as emotion_error:
                            print(f"Emotion classification error: {emotion_error}")
                    
                    reply_new = reply_list[-1]
                    final_response = " ".join(reply_list)
                    
                    # 다국어 번역
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
                    
                    answer = {
                        "answer_en": result_en,
                        "answer_ko": result_ko,
                        "answer_jp": result_jp
                    }
                    answer_list.append(answer)
                    
                    ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                    
                    yield json.dumps({
                        "type": "reply",
                        "reply_list": answer_list,
                        "query": {"origin": query, "text": "", "source": "", "time": "0"},
                        "ai_info": ai_info,
                        "intent_info": {
                            "is_intent_web": "off",
                            "web_info": "",
                            "web_search_keyword": "",
                            "web_search_detail": "false",
                            "is_intent_image": "off",
                            "image_info": ""
                        },
                        "chat_idx": chat_idx,
                        "ai_language_out": ai_language,
                        "speaker": actual_responding_speaker,
                        "participants": [p["name"] for p in participants]
                    }) + '\n'
            
            # 6. 다음 발화자 결정 (flow_module 사용)
            if final_response:
                try:
                    final_next_speaker, final_reasoning = flow_module.decide_next_speaker(
                        memory_list, query=query, final_response=final_response,
                        current_speaker=actual_responding_speaker, query_speaker=current_speaker,
                        lang=ai_language, max_ai_consecutive=max_ai_consecutive
                    )
                    
                    logger.info(f"Flow decision: {current_speaker} -> {final_next_speaker}")
                    
                except Exception as e:
                    logger.warning(f"Flow decision error: {e}. Using fallback.")
                    final_next_speaker, final_reasoning = get_next_speaker_fallback(
                        actual_responding_speaker, memory_list, participants
                    )
                
                # final 이벤트 전송
                yield json.dumps({
                    "type": "final",
                    "speaker": actual_responding_speaker,
                    "next_speaker": final_next_speaker,
                    "reasoning": f"{reasoning} | Next: {final_reasoning}",
                    "chat_idx": chat_idx
                }) + '\n'
                
                logger.info(f"Response: {actual_responding_speaker} -> {final_next_speaker}")
            
            # 응답이 없으면 fallback
            if not final_response:
                fallback_answer_list = [{
                    "answer_en": "Sorry, I couldn't generate a response.",
                    "answer_ko": "죄송해요, 응답을 생성할 수 없습니다.",
                    "answer_jp": "申し訳ございません、応答を生成できませんでした。"
                }]
                
                yield json.dumps({
                    "type": "reply",
                    "reply_list": fallback_answer_list,
                    "speaker": target_speaker,
                    "next_speaker": "sensei",
                    "chat_idx": chat_idx
                }) + '\n'
            
        except Exception as e:
            logger.error(f"Conversation error: {str(e)}")
            
            error_answer_list = [{
                "answer_en": "Server error occurred.",
                "answer_ko": "서버 오류가 발생했습니다.",
                "answer_jp": "サーバーエラーが発生しました。"
            }]
            
            yield json.dumps({
                "reply_list": error_answer_list,
                "error": str(e),
                "message": "서버 오류가 발생했습니다.",
                "speaker": "system",
                "next_speaker": "sensei",
                "ai_language_out": ai_language,
                "chat_idx": chat_idx,
                "type": "error"
            }) + '\n'
    
    return Response(generate(), content_type='application/json')


# ============================================================================
# 유틸리티 엔드포인트들
# ============================================================================

@app.route('/multi/health', methods=['GET'])
def multi_health():
    """상태 확인"""
    return jsonify({
        "status": "healthy",
        "service": "multi_conversation",
        "version": "1.0",
        "timestamp": datetime.now().isoformat(),
        "features": ["gemini", "local", "multi_character", "streaming"]
    })


@app.route('/reset_translator', methods=['GET'])
def reset_translator():
    """번역기 재설정"""
    global translator
    if translator:
        translator.get_freeDeepLFreeUrls()
        return jsonify({"result": "translator reset"}), 200
    else:
        return jsonify({"error": "translator not initialized"}), 500


# ============================================================================
# 서버 초기화
# ============================================================================

def init_multi_server():
    """서버 초기화"""
    global translator
    
    state.model_name = "Qwen3-8B-Q4_K_M.gguf"
    state.set_var_from_model(state.model_name)
    state.DEV_MODE = True
    state.is_write_log_file = True
    
    # 번역기 초기화
    translator = util_translator.Translator()
    translator.get_freeDeepLFreeUrls()
    print('### translator setted')
    
    # Local LLM 모델 로딩
    try:
        multi_local.load_model(is_use_cuda=True)
        print('### Local LLM model loaded')
    except Exception as e:
        print(f'### Warning: Failed to load Local LLM model: {e}')
    
    # Gemini 모델 로딩
    try:
        multi_gemini.load_model()
        print('### Gemini model loaded')
    except Exception as e:
        print(f'### Warning: Failed to load Gemini model: {e}')
    
    print('### Multi Server initialized with Gemini/Local dual support')


if __name__ == '__main__':
    # 서버 초기화
    init_multi_server()
    
    print('### Starting Multi Server on port 5001 (Streaming Mode)...')
    app.run(host='0.0.0.0', port=5001, debug=True)
