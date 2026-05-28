'''
server_interface_multi_impl.py
Multi-Conversation Flask 엔드포인트

ASIS3의 Unity 클라이언트와 호환되도록 form-data 및 ASIS3 응답 형식을 사용합니다.
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import json
from datetime import datetime
from flask import Flask, Response, request, jsonify
from threading import Lock

# 대화 모듈
import time
from ai_multi_conversation import conversation_run_loop
from ai_vl_agent_types import AgentEvent
from ai_vl_logger import save_session_log

app = Flask(__name__)
request_lock = Lock()

# Global variables
translator = None

def set_translator(translator_instance):
    """외부에서 번역기 인스턴스를 주입하기 위한 함수"""
    global translator
    translator = translator_instance
    return True

def translate_response(reply, source_lang):
    """응답을 다국어로 번역"""
    result_ko = reply
    result_jp = reply  
    result_en = reply
    
    if not translator:
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
        pass
    
    return result_ko, result_jp, result_en



@app.route('/aropla/conversation', methods=['POST'])
def main_stream_multi():
    '''Multi-Conversation Stateless 스트리밍 처리 (ASIS3 호환)'''
    
    start_time = datetime.now()
    start_ts = time.time()
    
    collected_events = []
    request_params = {}
    
    # Form 데이터 파싱 (ASIS3 형식)
    query = request.form.get('query', '')
    current_speaker = request.form.get('current_speaker', 'sensei')
    target_speaker = request.form.get('target_speaker')
    ai_language = request.form.get('ai_language', 'ko')
    player = request.form.get('player', 'sensei')
    chat_idx = request.form.get('chat_idx', '-1')
    ai_emotion = request.form.get('ai_emotion', 'off')
    intent_smalltalk = request.form.get('intent_smalltalk', 'off')
    max_ai_consecutive = int(request.form.get('max_ai_consecutive', '10'))
    cur_ai_consecutive = int(request.form.get('cur_ai_consecutive', '0'))
    
    # JSON 파싱
    memory_multi = []
    try:
        memory_multi = json.loads(request.form.get('memory_multi', '[]'))
    except:
        memory_multi = []
    
    guideline_list = []
    try:
        guideline_list = json.loads(request.form.get('guideline_list', '[]'))
    except:
        guideline_list = []
    
    situation_dict = {}
    try:
        situation_dict = json.loads(request.form.get('situation', '{}'))
    except:
        situation_dict = {}
    
    participants = []
    try:
        participants = json.loads(request.form.get('participants', '[]'))
    except:
        participants = ['sensei', 'arona', 'plana']
    
    # 로그용 request_params (ASIS3 방식)
    request_params = {
        'query': query,
        'current_speaker': current_speaker,
        'target_speaker': target_speaker,
        'participants': participants,
        'memory_multi_count': len(memory_multi),
        'ai_language': ai_language,
        'chat_idx': chat_idx,
        'timestamp': datetime.now().isoformat(),
        'mode': 'multi_conversation'
    }
    
    def generate():
        final_result = None
        with request_lock:
            try:
                # 1. thinking 응답
                yield json.dumps({"type": "thinking", "chat_idx": chat_idx}, ensure_ascii=False) + '\n'
                
                # 2. conversation_run_loop 호출
                for event in conversation_run_loop(
                    query=query,
                    participants=participants,
                    lang=ai_language,
                    server_type='Local',
                    api_key=None,
                    history=[],
                    memory_multi=memory_multi,
                    ai_trigger_situation=None
                ):
                    event_kind = event.kind
                    event_message = event.message
                    event_data = event.data
                    
                    # AgentEvent를 ASIS3 응답으로 변환
                    if event_kind in ['multi_reply', 'conversation_start', 'ai_triggered']:
                        # 다국어 번역
                        ko, jp, en = translate_response(event_message, ai_language)
                        
                        # 실제 화자 (event.data에서 추출)
                        actual_speaker = event_data.get('speaker', target_speaker or 'arona')
                        
                        # reply 응답
                        reply_response = {
                            "type": "reply",
                            "reply_list": [{
                                "answer_ko": ko,
                                "answer_jp": jp,
                                "answer_en": en
                            }],
                            "query": {
                                "origin": query,
                                "text": "",
                                "source": "",
                                "time": "0"
                            },
                            "ai_info": {
                                "server_type": "Multi-Character",
                                "model": "local",
                                "prompt": f"{ai_language}/{actual_speaker}",
                                "lang_used": ai_language,
                                "translator": "",
                                "time": f"{(datetime.now() - start_time).total_seconds():.2f} sec",
                                "intent": "None",
                                "emotion": ""
                            },
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
                            "speaker": actual_speaker,
                            "participants": participants
                        }
                        
                        collected_events.append(reply_response)
                        yield json.dumps(reply_response, ensure_ascii=False) + '\n'
                    
                    # final 응답
                    if event_kind in ['waiting_user', 'conversation_end']:
                        # 다음 화자 결정
                        next_speaker = event_data.get('next_speaker', 'sensei')
                        actual_speaker = event_data.get('speaker', target_speaker or 'arona')
                        
                        final_response = {
                            "type": "final",
                            "speaker": actual_speaker,
                            "next_speaker": next_speaker,
                            "reasoning": event_data.get('reasoning', '대화 완료'),
                            "chat_idx": chat_idx
                        }
                        
                        collected_events.append(final_response)
                        final_result = final_response
                        yield json.dumps(final_response, ensure_ascii=False) + '\n'
                
                # 세션 로그 저장
                try:
                    save_session_log(request_params, collected_events, final_result)
                except ImportError:
                    print("[Warning] ai_vl_logger not available, skipping session log")
                except Exception as log_error:
                    print(f"[Warning] Failed to save session log: {log_error}")
                
            except Exception as e:
                # 에러 응답
                error_event = {
                    "type": "error",
                    "message": str(e),
                    "error_type": type(e).__name__,
                    "chat_idx": chat_idx,
                    "elapsed_ms": int((time.time() - start_ts) * 1000)
                }
                collected_events.append(error_event)
                
                # 에러도 로그 저장
                try:
                    save_session_log(request_params, collected_events, error_event)
                except:
                    pass
                
                yield json.dumps(error_event, ensure_ascii=False) + '\n'
    
    return Response(
        generate(),
        mimetype='application/x-ndjson',
        headers={'Cache-Control': 'no-cache'}
    )


@app.route('/aropla/health', methods=['GET'])
def health_check():
    '''헬스 체크'''
    return jsonify({'status': 'ok', 'service': 'multi_conversation'})



def create_memory_multi_entry(speaker, message, role, message_ko,
                               message_jp, message_en, character_name=None,
                               timestamp=None, entry_type='conversation'):
    '''memory_multi 단일 항목 생성'''
    if timestamp is None:
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    
    entry = {
        'speaker': speaker,  # 'player' or 'character'
        'message': message,
        'message_trans': message,
        'role': role,  # 'user' or 'assistant'
        'type': entry_type,
        'messageKo': message_ko,
        'messageJp': message_jp,
        'messageEn': message_en,
        'timestamp': timestamp
    }
    
    # character 발화일 경우 character_name 추가
    if speaker == 'character' and character_name:
        entry['character_name'] = character_name
    
    return entry


def append_to_memory_multi(memory_multi, speaker, message, role,
                            message_ko, message_jp, message_en, character_name=None):
    '''memory_multi에 새 항목 추가'''
    if memory_multi is None:
        memory_multi = []
    
    entry = create_memory_multi_entry(
        speaker=speaker,
        message=message,
        role=role,
        message_ko=message_ko,
        message_jp=message_jp,
        message_en=message_en,
        character_name=character_name
    )
    memory_multi.append(entry)
    return memory_multi


def format_memory_multi_for_prompt(memory_multi, max_turns=10):
    '''memory_multi를 프롬프트용 문자열로 변환'''
    if not memory_multi:
        return "대화 기록 없음"
    
    # 최근 max_turns개만 사용
    recent = memory_multi[-max_turns*2:] if len(memory_multi) > max_turns*2 else memory_multi
    
    lines = []
    for entry in recent:
        speaker = entry.get('speaker', 'unknown')
        message = entry.get('message', '')
        lines.append(f"{speaker}: {message}")
    
    return '\n'.join(lines)


if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
