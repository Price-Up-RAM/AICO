"""
Flask AroplaChannel Server Implementation

기존 /conversation_stream을 확장한 아로프라 채널 전용 엔드포인트
Unity의 APIAroPlaManager와 연동하여 아로나-프라나-선생님 3자 대화를 처리
"""

from flask import Flask, request, jsonify, Response
from datetime import datetime
import json
import logging
from typing import Dict, List, Optional, Tuple

# 기존 시스템 import
import prompt_char
import state
# import ai_conversation_binary as ai_conversation
import ai_conversation_binary_multi as ai_conversation_multi  # 다중 참여자 대화 시스템
# import util_gemini
import util_translator
import ai_aropla_flow
import ai_emotion_classification

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Global variables (server_interface.py 스타일 참조)
translator = None

def set_translator(translator_instance):
    """외부에서 번역기 인스턴스를 주입하기 위한 함수"""
    global translator
    translator = translator_instance
    return True

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
        # 추후 'teaparty' 전용 구성이 필요하면 교체
        return create_default_aropla_participants()
    else:
        # 알 수 없는 타입은 우선 아로프라 기본값 사용
        return create_default_aropla_participants()

def process_participants(input_participants: List[Dict], conversation_type: str) -> List[Dict]:
    """입력받은 participants를 검증하고 누락된 기본 참여자를 추가하는 함수"""
    if not input_participants:
        # 입력이 없으면 기본 참여자 목록 반환
        return create_default_participants(conversation_type)
    
    # 입력받은 participants 목록을 기반으로 시작
    final_participants = []
    existing_names = set()
    
    # 입력받은 participants 검증 및 추가
    for participant in input_participants:
        if isinstance(participant, dict) and "name" in participant:
            # 필수 필드 검증 및 기본값 설정
            validated_participant = {
                "name": participant["name"],
                "type": participant.get("type", "ai" if participant["name"] != "sensei" else "user"),
                "display_name": participant.get("display_name", participant["name"]),
                "character_file": participant.get("character_file", participant["name"] if participant["name"] != "sensei" else None)
            }
            final_participants.append(validated_participant)
            existing_names.add(participant["name"])
    
    # 기본 참여자 중 누락된 것들 추가
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
    return "arona"  # fallback

@app.route('/aropla/conversation', methods=['POST'])
def main_stream_multi():
    """범용 다중 대화 스트리밍 API (Unity 연동) - server_interface.py 스타일"""
    
    start_time = datetime.now()
    
    if state.get_DEV_MODE():
        print('main_stream_multi request:', request.form)
    
    # 기존 방식 (server_interface.py 참조): Form 데이터 파싱
    query = request.form.get('query', '')
    player_name = request.form.get('player', 'sensei')
    current_speaker = request.form.get('current_speaker', 'sensei')  # 새로 추가된 변수 (방치)
    target_speaker = request.form.get('target_speaker')  # 새로 추가된 변수 (방치)
    chat_idx = request.form.get('chatIdx', '-1')
    multi_conversation_type = request.form.get('multi_conversation_type', '')  # 특수 대화 타입 'aropla', 'teaparty'
    conversation_type = (multi_conversation_type or '').lower() or 'aropla'
    
    # 설정 파라미터 (기존 방식)
    ai_language = request.form.get('ai_language', 'ko')
    ai_emotion = request.form.get('ai_emotion', 'off')
    memory = request.form.get('memory', '[]')
    guideline_list_raw = request.form.get('guideline_list', '[]')
    situation_raw = request.form.get('situation', '{}')
    participants_raw = request.form.get('participants', '[]')  # 동적 participants 입력
    
    intent_smalltalk = request.form.get('intent_smalltalk', 'off')
    max_ai_consecutive = int(request.form.get('max_ai_consecutive', '10'))  # 최대 AI 연속 대화 횟수 (기본값 4)
    cur_ai_consecutive = int(request.form.get('cur_ai_consecutive', '0'))  # 현재 AI 연속 대화 횟수
    
    is_player_next_speaker = False
    if cur_ai_consecutive - 1 >= max_ai_consecutive:
        is_player_next_speaker = True
    
    # JSON 파싱 (기존 방식 - server_interface.py 참조)
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
    
    # participants 파싱 및 검증
    participants_list = []
    try:
        participants_list = json.loads(participants_raw) if participants_raw else []
    except json.JSONDecodeError:
        participants_list = []
    

    print('### query', query)
    print('### player_name', player_name)
    print('### current_speaker', current_speaker)
    print('### target_speaker', target_speaker)
    print('### chat_idx', chat_idx)
    print('### ai_language', ai_language)
    print('### ai_emotion', ai_emotion)
    print('### guideline_list_raw', guideline_list_raw)
    print('### situation_raw', situation_raw)
    print('### participants_raw', participants_raw)
    print('### memory_list', memory_list)
    print('### guideline_list', guideline_list)
    print('### situation_dict', situation_dict)
    print('### participants_list', participants_list)
    print('### intent_smalltalk', intent_smalltalk)
    print('### max_ai_consecutive', max_ai_consecutive)
    print('### cur_ai_consecutive', cur_ai_consecutive)

    
    def generate():
        """server_interface.py 방식의 스트리밍 응답 생성"""
        nonlocal target_speaker  # AI Agent가 수정할 수 있도록
        
        try:
            # 🎭 AI가 '먼저' 말을 거는 trigger 상황 처리
            if intent_smalltalk == 'on':
                # AI가 '잡담', '인사' 등의 목적에 맞게 먼저 말을 거는 trigger 상황 처리
                try:
                    import ai_trigger_small_talk
                    
                    # 목적에 맞는 대화 시작 생성
                    trigger_message = ai_trigger_small_talk.process(
                        purpose=query,      # "잡담", "인사" 등의 목적
                        character=current_speaker,  # "arona" or "plana"
                        lang=ai_language
                    )
                    
                    logger.info(f"🎭 AI Trigger: {current_speaker} -> {trigger_message[:50]}...")
                    print(f"🎭 [DEBUG] Trigger mode: {current_speaker} 목적='{query}' -> '{trigger_message[:50]}...'")
                    
                    # 다국어 번역
                    result_ko, result_jp, result_en = translate_response(trigger_message, ai_language)
                    
                    # AI 정보 구성
                    ai_info = {
                        'server_type': 'AI-Trigger',
                        'model': state.model_name if hasattr(state, 'model_name') else "",
                        'prompt': f"{ai_language}/trigger/{query}",
                        'lang_used': ai_language,
                        'translator': '',
                        'time': f"{(datetime.now() - start_time).total_seconds():.2f} sec",
                        'intent': 'trigger',
                        'emotion': ''
                    }
                    
                    # 바로 스트리밍 응답 반환
                    answer_list = [{
                        "answer_en": result_en,
                        "answer_ko": result_ko,
                        "answer_jp": result_jp
                    }]
                    
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
                        "speaker": current_speaker,
                        "next_speaker": "sensei",  # 사용자 차례
                        "reasoning": f"AI Trigger: {query} -> {current_speaker}가 대화 시작"
                    }) + '\n'
                    
                    # 최종 정보 전송
                    yield json.dumps({
                        "type": "final",
                        "speaker": current_speaker,
                        "next_speaker": "sensei",
                        "reasoning": f"AI Trigger 완료: {current_speaker} -> sensei",
                        "chat_idx": chat_idx
                    }) + '\n'
                    
                    logger.info(f"🎭 AI Trigger completed: {current_speaker} -> sensei")
                    return  # trigger 모드에서는 여기서 종료
                    
                except Exception as trigger_error:
                    logger.warning(f"AI Trigger failed: {trigger_error}. Falling back to normal flow.")
                    print(f"🎭 [WARNING] Trigger fallback: {trigger_error}")
                    # fallback: 정상 플로우로 진행
            
            # Flow Director로 발화자 결정
            if target_speaker:
                # 이미 지정된 경우
                next_speaker, reasoning = target_speaker, f"지정된 발화자: {target_speaker}"
            else:
                # 지정 없는 경우 - 유저로부터의 메시지
                # 1단계: 사용자 메시지 대상 분석 (답변 전 - 누구에게 말하고 있는가?)
                try:
                    # 메시지 내용으로 사용자가 누구에게 말하고 있는지 AI Agent 분석 (analyze_target_speaker_from_message)
                    target_from_message, message_reasoning = ai_aropla_flow.analyze_target_speaker_from_message(
                        query, current_speaker, lang=ai_language, memory_list=memory_list
                    )
                    
                    if target_from_message:
                        # 메시지에서 명확한 대상이 감지됨
                        next_speaker, reasoning = target_from_message, f"메시지 분석: '{query[:50]}...' -> {message_reasoning}"
                        logger.info(f"🎯 Message analysis result: '{query[:50]}...' -> {target_from_message}")
                        print(f"🎯 [DEBUG] Message-based target: '{query[:50]}...' -> {target_from_message}")
                    else:
                        # 명확한 대상이 없으면 기본 발화자 선택 (첫 번째 AI 참여자)
                        # participants 처리
                        processed_participants = process_participants(participants_list, conversation_type)
                        first_ai = get_first_ai_participant(processed_participants)
                        next_speaker = first_ai
                        reasoning = f"명확한 대상 없음 - 기본 발화자({first_ai}) 선택: '{query[:30]}...'"
                        logger.info(f"🔄 Default speaker selected: {next_speaker}")
                        print(f"🔄 [DEBUG] Default target: '{query[:50]}...' -> {next_speaker} (기본 선택)")
                        
                except (ImportError, AttributeError) as e:
                    # ai_aropla_flow 모듈이 없거나 함수가 없는 경우 기본 발화자 선택
                    logger.warning(f"ai_aropla_flow not available: {e}. Using default speaker.")
                    # participants 처리
                    processed_participants = process_participants(participants_list, conversation_type)
                    first_ai = get_first_ai_participant(processed_participants)
                    next_speaker = first_ai
                    reasoning = f"AI Agent 모듈 오류 - 기본 발화자({first_ai}) 선택"
            
            logger.info(f"{conversation_type.upper()}: {current_speaker} -> {next_speaker} ({reasoning})")
            
            # target_listener 결정 (누구에게 말하는지)
            target_listener = "all"  # 기본값
            listener_reasoning = ""
            
            # AI 연속 대화 제한에 도달한 경우 강제로 sensei에게 대화 유도
            if is_player_next_speaker:
                target_listener = "sensei"
                listener_reasoning = f"AI 연속 대화 제한 도달 ({cur_ai_consecutive-1}/{max_ai_consecutive}) - sensei에게 자연스럽게 대화 유도"
                logger.info(f"🎯 Forced listener to sensei: AI consecutive limit reached")
                print(f"🎯 [DEBUG] Forced target_listener: sensei (AI limit: {cur_ai_consecutive-1}/{max_ai_consecutive})")
            else:
                try:
                    if current_speaker == "sensei":
                        # 사용자 메시지의 청취자 분석 (AI Agent 활용) - target_speaker 정보 포함
                        target_listener, listener_reasoning = ai_aropla_flow.analyze_target_listener_from_message(
                            query, current_speaker, next_speaker, lang=ai_language, memory_list=memory_list
                        )
                        logger.info(f"🎧 Response target analysis: {next_speaker} should respond to -> {target_listener}")
                        print(f"🎧 [DEBUG] Response target: '{query[:50]}...' -> {next_speaker} responds to {target_listener} ({listener_reasoning})")
                    else:
                        # AI가 말할 때는 맥락 기반으로 청취자 결정
                        target_listener, listener_reasoning = ai_aropla_flow.determine_target_listener_from_context(
                            current_speaker, next_speaker, query, memory_list, ai_language
                        )
                        logger.info(f"🎭 Context listener decision: {current_speaker} -> {next_speaker}, listener: {target_listener}")
                        print(f"🎭 [DEBUG] Listener from context: {current_speaker} -> {next_speaker} -> {target_listener} ({listener_reasoning})")
                    
                except (ImportError, AttributeError) as e:
                    # ai_aropla_flow 함수가 없는 경우 기본 로직 사용
                    logger.warning(f"ai_aropla_flow listener functions not available: {e}. Using fallback logic.")
                    if current_speaker == "sensei" and next_speaker in ["arona", "plana"]:
                        target_listener = next_speaker  # 선생님 -> 특정 AI
                        listener_reasoning = f"기본 로직: 선생님 -> {next_speaker}"
                    elif current_speaker in ["arona", "plana"] and next_speaker == "sensei":
                        target_listener = "sensei"  # AI -> 선생님
                        listener_reasoning = f"기본 로직: {current_speaker} -> 선생님"
                    else:
                        target_listener = "all"  # 전체 대화
                        listener_reasoning = "기본 로직: 전체 대화"
                    print(f"🎧 [DEBUG] Fallback listener: {target_listener} ({listener_reasoning})")
            
            logger.info(f"Aropla flow: {current_speaker} -> {next_speaker}, listener: {target_listener}")
            
            # 선생님 차례면 사용자 입력 대기 (스트리밍 응답) - 일반적으로 작동할 일 없음
            if next_speaker == 'sensei':
                # server_interface.py 스타일의 answer_list 구조 생성
                system_answer_list = []
                system_answer = {
                    "answer_en": "Waiting for user input.",
                    "answer_ko": "사용자 입력을 기다리는 중입니다.",
                    "answer_jp": "ユーザー入力をお待ちしております。"
                }
                system_answer_list.append(system_answer)
                
                yield json.dumps({
                    "reply_list": system_answer_list,  # 다국어 답변 리스트
                    "message": "사용자 입력을 기다리는 중입니다.",  # 하위 호환성
                    "speaker": "system",
                    "next_speaker": "sensei",
                    "reasoning": reasoning,
                    "ai_language_out": ai_language,
                    "chat_idx": chat_idx,  # Unity 호환성을 위해 추가
                    "type": "waiting"
                }) + '\n'
                return
            
            # AI 캐릭터 응답 생성 (다중 참여자 시스템 사용)
            target_speaker = next_speaker  # arona 또는 plana
            
            # 참여자 리스트 동적 생성
            participants = process_participants(participants_list, conversation_type)
            
            # 캐릭터명 검증 (server_interface.py 로직 참조)
            char_name = target_speaker
            if char_name not in prompt_char.get_all_filenames_in_prompt():
                print(f'{char_name} not in prompt_char.get_all_filenames_in_prompt()')
                char_name = 'kivotos_student_normal'  # 기본 캐릭터로 변경
                # participants에서도 업데이트
                for p in participants:
                    if p["name"] == target_speaker:
                        p["character_file"] = char_name
            
            # 다중 참여자 대화 시스템 활용
            lang_infer_type = ai_language
            
            # 🚀 스트리밍 응답 생성 (server_interface.py 방식) - 모든 응답을 yield
            if True:
                # Gemini Multi-Character 사용
                import util_gemini_multi
                
                response_generator = util_gemini_multi.process_multi_stream(
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
            else:
                # Qwen Multi-Character 사용 (기존 방식)
                response_generator = ai_conversation_multi.process_multi_stream(
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
                
            # 응답 진행 상태 전송 (server_interface.py 방식)
            yield json.dumps({
                "type": "thinking",
                "chat_idx": chat_idx
            }) + '\n'
            
            # 🔄 server_interface.py 정확한 방식 복사
            answer_list = []
            reply_len = 0
            actual_responding_speaker = target_speaker
            final_response = ""
            
            # AI 정보 초기화
            ai_info = {
                'server_type': 'Multi-Character',
                'model': state.model_name if hasattr(state, 'model_name') else "",
                'prompt': lang_infer_type + '/' + target_speaker,
                'lang_used': lang_infer_type,
                'translator': '',
                'time': '',
                'intent': 'None',
                'emotion': ''
            }
            
            for j, reply_batch in enumerate(response_generator):
                # reply_batch 처리 (튜플이면 reply_list만 추출)
                if reply_batch and len(reply_batch) == 2:  # (reply_list, speaker) 튜플
                    reply_list, responding_speaker = reply_batch
                    actual_responding_speaker = responding_speaker
                elif reply_batch and isinstance(reply_batch, list):  # 이전 방식 호환성
                    reply_list = reply_batch
                else:
                    continue
                
                # server_interface.py 정확한 로직 복사
                if reply_len < len(reply_list):                
                    reply_len = len(reply_list)
                    
                    # Emotion 처리 (server_interface.py 정확한 방식)
                    if reply_len == 1 and ai_emotion == 'on' and not ai_info['emotion']:
                        try:
                            ai_emotion_classification_reply = reply_list[0]
                            ai_emotion_reply = ai_emotion_classification.process(
                                query, ai_emotion_classification_reply, 
                                player_name, target_speaker, 
                                memory_list=memory_list, lang=lang_infer_type
                            )
                            if "emotion: " in ai_emotion_reply:  # 답에 emotion format이 있음
                                emotion_text = ai_emotion_reply.strip().split("emotion: ")[-1].strip().lower()
                                valid_emotions = ['joy', 'anger', 'confusion', 'sadness', 'surprise', 'neutral']
                                if emotion_text in valid_emotions:
                                    ai_info['emotion'] = emotion_text
                        except Exception as emotion_error:
                            print(f"Emotion classification error: {emotion_error}")
                    
                    reply_new = reply_list[-1]
                    final_response = " ".join(reply_list)  # 전체 응답 저장
                    
                    # 다국어 번역 (server_interface.py 정확한 방식)
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
                    
                    # AI 정보 업데이트
                    ai_info['time'] = f"{(datetime.now() - start_time).total_seconds():.2f} sec"
                    
                    # server_interface.py 정확한 응답 형식
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
                        "speaker": actual_responding_speaker,  # 추가 정보
                        "participants": [p["name"] for p in participants]  # 추가 정보
                    }) + '\n'
            
            # 2단계: AI 응답 후 다음 발화자 결정 (답변 후 - 누가 다음에 말할 차례인가?)
            if final_response:
                try:
                    # 대화 흐름을 분석하여 다음 발화자 결정 (process_flow_decision만 사용)
                    # memory_list와 현재 쿼리, AI 응답을 활용하여 대화 흐름 분석
                    final_next_speaker, final_reasoning = ai_aropla_flow.process_flow_decision(
                        memory_list, query=query, final_response=final_response, current_speaker=actual_responding_speaker, query_speaker=current_speaker, lang=ai_language, max_ai_consecutive=max_ai_consecutive
                    )
                    
                    logger.info(f"🤖 AI response flow decision: {current_speaker} -> {final_next_speaker}")
                    print(f"🔄 [DEBUG] Final flow decision: {current_speaker} -> {final_next_speaker} ({final_reasoning})")
                        
                except (ImportError, AttributeError) as e:
                    # ai_aropla_flow 모듈이 없거나 함수가 없는 경우 기본 흐름 로직
                    logger.warning(f"ai_aropla_flow not available for flow decision: {e}. Using fallback flow logic.")
                    final_next_speaker, final_reasoning = get_next_speaker_fallback(actual_responding_speaker, memory_list, participants)
                
                # 최종 응답 정보 전송 (다음 발화자 포함)
                yield json.dumps({
                    "type": "final",
                    "speaker": actual_responding_speaker,
                    "next_speaker": final_next_speaker,
                    "reasoning": f"{reasoning} | Next: {final_reasoning}",
                    "chat_idx": chat_idx
                }) + '\n'
                
                logger.info(f"Aropla response: {actual_responding_speaker} -> {final_next_speaker}")
            
            # 응답이 없으면 fallback
            if not final_response:
                fallback_answer_list = []
                fallback_answer = {
                    "answer_en": "Sorry, I couldn't generate a response.",
                    "answer_ko": "죄송해요, 응답을 생성할 수 없습니다.",
                    "answer_jp": "申し訳ございません、応答を生成できませんでした。"
                }
                fallback_answer_list.append(fallback_answer)
                
                yield json.dumps({
                    "type": "reply",
                    "reply_list": fallback_answer_list,
                    "speaker": target_speaker,
                    "next_speaker": "sensei",
                    "chat_idx": chat_idx
                }) + '\n'
            
        except Exception as e:
            logger.error(f"Aropla conversation error: {str(e)}")
            
            # 에러 응답도 server_interface.py 스타일로 구성
            error_answer_list = []
            error_answer = {
                "answer_en": "Server error occurred.",
                "answer_ko": "서버 오류가 발생했습니다.",
                "answer_jp": "サーバーエラーが発생しました。"
            }
            error_answer_list.append(error_answer)
            
            yield json.dumps({
                "reply_list": error_answer_list,  # 다국어 답변 리스트
                "error": str(e),
                "message": "서버 오류가 발생했습니다.",  # 하위 호환성
                "speaker": "system",
                "next_speaker": "sensei",
                "ai_language_out": ai_language,
                "chat_idx": chat_idx,
                "type": "error"
            }) + '\n'
    
    return Response(generate(), content_type='application/json')

def get_next_speaker_fallback(current_speaker: str, memory_list: List[Dict], participants: List[Dict] = None) -> Tuple[str, str]:
    """ai_aropla_flow가 없을 때 사용하는 기본 발화자 결정 로직"""
    
    # participants가 없으면 기본 참여자 사용
    if not participants:
        participants = create_default_aropla_participants()
    
    # AI 참여자 목록 추출
    ai_participants = [p["name"] for p in participants if p.get("type") == "ai"]
    first_ai = ai_participants[0] if ai_participants else "arona"
    second_ai = ai_participants[1] if len(ai_participants) > 1 else None
    
    # 간단한 순환 로직
    if current_speaker == 'sensei':
        # 사용자 다음에는 첫 번째 AI가 먼저 응답
        next_speaker = first_ai
        reasoning = f"사용자 후 {first_ai} 응답 (기본 로직)"
    elif current_speaker == first_ai and second_ai:
        # 첫 번째 AI 다음에는 두 번째 AI 또는 사용자
        # 최근 대화에서 두 번째 AI가 말한 적이 없다면 두 번째 AI, 있다면 사용자
        recent_speakers = [turn.get('speaker', '') for turn in memory_list[-3:]] if memory_list else []
        if second_ai not in recent_speakers:
            next_speaker = second_ai
            reasoning = f"{second_ai}가 아직 참여하지 않아서 {second_ai} 차례 (기본 로직)"
        else:
            next_speaker = 'sensei'
            reasoning = f"{first_ai} 후 사용자 차례 (기본 로직)"
    elif current_speaker in ai_participants:
        # 다른 AI 다음에는 사용자 또는 첫 번째 AI
        next_speaker = 'sensei'
        reasoning = f"{current_speaker} 후 사용자 차례 (기본 로직)"
    else:
        # 기본값
        next_speaker = first_ai
        reasoning = f"알 수 없는 상황, {first_ai} 차례로 설정 (기본 로직)"
    
    return next_speaker, reasoning

def translate_response(reply: str, source_lang: str) -> Tuple[str, str, str]:
    """응답을 다국어로 번역 (기존 로직 활용)"""
    
    result_ko = reply
    result_jp = reply  
    result_en = reply
    
    # translator가 초기화되지 않은 경우 원본 반환
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

# 기존 /conversation_stream과의 통합을 위한 라우팅
@app.route('/conversation_stream', methods=['POST'])
def conversation_stream_router():
    """기존 엔드포인트에서 아로프라 모드 확인 후 라우팅"""
    
    # 아로프라 모드 확인 (특정 파라미터로 판단)
    is_aropla_mode = request.form.get('aropla_mode', 'false').lower() == 'true'
    has_current_speaker = request.form.get('current_speaker')
    
    if is_aropla_mode or has_current_speaker:
        # 다중 대화 메인 스트림으로 라우팅
        return main_stream_multi()
    else:
        # 기존 단일 대화 시스템으로 처리
        # return original_conversation_stream()  # 기존 구현 호출
        return jsonify({"error": "Original conversation_stream not implemented"}), 501

# Small Talk 전용 엔드포인트
@app.route('/conversation/small_talk', methods=['POST'])
def get_smalk_talk():
    """단일 AI가 먼저 말을 거는 small talk 트리거 엔드포인트 (스트리밍)

    - 입력 파라미터 (form):
      - query 또는 purpose: 트리거 목적(예: "잡담", "인사", "대화주제")
      - current_speaker 또는 character: 발화 AI (기본 'arona')
      - ai_language: 언어 코드 (기본 'ko')
      - chatIdx: 대화 인덱스 (옵션)
      - intent_smalltalk: on/off (옵션, 기본 on)
    """
    start_time = datetime.now()

    # 폼 파라미터 파싱
    query = request.form.get('query', '')
    purpose = request.form.get('purpose', '')
    if not query and purpose:
        query = purpose

    current_speaker = request.form.get('current_speaker', 'arona')
    character_param = request.form.get('character')
    if character_param:
        current_speaker = character_param

    ai_language = request.form.get('ai_language', 'ko')
    chat_idx = request.form.get('chatIdx', '-1')
    intent_smalltalk = request.form.get('intent_smalltalk', 'on')

    def generate():
        try:
            import random
            import ai_trigger_small_talk
            import ai_trigger_small_topic

            # 목적이 비어있을 때 기본값
            if not query:
                query_value = '잡담'
            else:
                query_value = query

            # purpose에 따라 모듈 선택
            use_topic_module = False
            if query_value == '대화주제':
                # 대화주제일 때는 100% 확률로 topic 모듈 사용
                use_topic_module = True
            elif query_value == '잡담':
                # 잡담일 때는 30% 확률로 topic 모듈 사용
                use_topic_module = (random.random() < 0.90)

            # 선택된 모듈로 메시지 생성
            if use_topic_module:
                # query_value가 '대화주제' 또는 '잡담'이면 자동 선택
                if query_value in ['대화주제', '잡담', '']:
                    from ai_trigger_topics import get_random_topic_by_time_and_lang
                    actual_topic = get_random_topic_by_time_and_lang(ai_language)
                    logger.info(f"🎲 Auto-selected topic: {actual_topic}")
                    print(f"🎲 [DEBUG] Auto-selected topic: {actual_topic}")
                else:
                    actual_topic = query_value  # 사용자 지정 토픽 사용
                
                # ai_trigger_small_topic 사용
                trigger_message = ai_trigger_small_topic.process(
                    topic=actual_topic,
                    character=current_speaker,
                    lang=ai_language
                )
                logger.info(f"🎭 AI SmallTopic: {current_speaker} -> {trigger_message[:50]}...")
                print(f"🎭 [DEBUG] SmallTopic: {current_speaker} 주제='{actual_topic}' -> '{trigger_message[:50]}...'")
            else:
                # ai_trigger_small_talk 사용 (기존 방식)
                trigger_message = ai_trigger_small_talk.process(
                    purpose=query_value,
                    character=current_speaker,
                    lang=ai_language
                )
                logger.info(f"🎭 AI SmallTalk: {current_speaker} -> {trigger_message[:50]}...")
                print(f"🎭 [DEBUG] SmallTalk: {current_speaker} 목적='{query_value}' -> '{trigger_message[:50]}...'")

            # 다국어 번역
            result_ko, result_jp, result_en = translate_response(trigger_message, ai_language)

            # AI 정보 구성 (server_interface.py 스타일)
            ai_info = {
                'server_type': 'AI-Trigger',
                'model': state.model_name if hasattr(state, 'model_name') else "",
                'prompt': f"{ai_language}/trigger/{query}",
                'lang_used': ai_language,
                'translator': '',
                'time': f"{(datetime.now() - start_time).total_seconds():.2f} sec",
                'intent': 'trigger',
                'emotion': ''
            }

            # 스트리밍 응답 - reply
            answer_list = [{
                "answer_en": result_en,
                "answer_ko": result_ko,
                "answer_jp": result_jp
            }]

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
                "speaker": current_speaker,
                "next_speaker": "sensei",
                "reasoning": f"AI SmallTalk: {query} -> {current_speaker}가 대화 시작",
                "intent_smalltalk": intent_smalltalk
            }) + '\n'

            # 스트리밍 응답 - final
            yield json.dumps({
                "type": "final",
                "speaker": current_speaker,
                "next_speaker": "sensei",
                "reasoning": f"AI SmallTalk 완료: {current_speaker} -> sensei",
                "chat_idx": chat_idx
            }) + '\n'

        except Exception as e:
            logger.error(f"SmallTalk error: {str(e)}")

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

# 유틸리티 엔드포인트들 (server_interface.py 참조)
@app.route('/reset_translator', methods=['GET'])
def reset_translator():
    """번역기 재설정"""
    global translator
    if translator:
        translator.get_freeDeepLFreeUrls()
        return jsonify({"result": "translator reset"}), 200
    else:
        return jsonify({"error": "translator not initialized"}), 500

@app.route('/health', methods=['GET'])
def health():
    """기본 헬스체크"""
    return jsonify({"status": "healthy"}), 200

@app.route('/aropla/health', methods=['GET'])
def aropla_health():
    """아로프라 채널 상태 확인"""
    
    # 다중 참여자 시스템 상태 확인
    multi_character_status = "unknown"
    try:
        if hasattr(ai_conversation_multi, 'llm') and ai_conversation_multi.llm:
            multi_character_status = "initialized"
        else:
            multi_character_status = "not_initialized"
    except Exception:
        multi_character_status = "error"
    
    # AI Agent 플로우 시스템 상태 확인
    ai_agent_status = "unknown"
    try:
        if hasattr(ai_aropla_flow, 'llm') and ai_aropla_flow.llm:
            ai_agent_status = "initialized"
        else:
            ai_agent_status = "not_initialized"
    except Exception:
        ai_agent_status = "error"
    
    return jsonify({
        "status": "healthy",
        "service": "aropla_channel", 
        "version": "2.0",  # 다중 참여자 지원으로 버전업
        "timestamp": datetime.now().isoformat(),
        "systems": {
            "translator": "initialized" if translator else "not_initialized",
            "multi_character_conversation": multi_character_status,
            "ai_agent_flow": ai_agent_status
        },
        "participants": ["sensei", "arona", "plana"],
        "features": ["multi_character_chat", "ai_agent_flow", "auto_translation", "streaming_response"]
    })

def init_aropla_server():
    """아로프라 서버 초기화 (server_interface.py 로직 참조)"""
    global translator
    
    state.model_name = "Qwen3-8B-Q4_K_M.gguf"
    # state.model_name = "Qwen3-14B-Q4_K_M.gguf"
    # state.model_name = "gemma-3-12b-it-Q4_K_M.gguf"
    state.set_var_from_model(state.model_name)
    state.DEV_MODE = True
    state.is_write_log_file = True
    
    # 번역기 초기화
    translator = util_translator.Translator()
    translator.get_freeDeepLFreeUrls()
    print('### translator setted')
    
    # 다중 참여자 대화 시스템 초기화
    try:
        ai_conversation_multi.load_model(is_use_cuda=True)
        print('### Multi-character conversation model loaded')
    except Exception as e:
        print(f'### Warning: Failed to load multi-character model: {e}')
    
    # AI Agent 플로우 시스템 초기화  
    try:
        ai_aropla_flow.load_model(is_use_cuda=True)
        print('### AI Agent flow model loaded')
    except Exception as e:
        print(f'### Warning: Failed to load AI Agent flow model: {e}')
    
    print('### Aropla Server initialized with multi-character streaming support')

if __name__ == '__main__':
    # 서버 초기화
    init_aropla_server()
    
    print('### Starting Aropla Server on port 5000 (Streaming Mode)...')
    app.run(host='0.0.0.0', port=5000, debug=True)