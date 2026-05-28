'''
server_interface_vl_engine_impl.py
VL Engine Flask 엔드포인트 (시나리오 기반 엔진)
'''
import os
import json
import time
import traceback
from datetime import datetime

from flask import Response, request, jsonify, send_file

import state
from util_image import save_uploaded_image_as_png

from ai_vl_engine import ai_vl_engine_run
from ai_vl_logger import save_session_log


def vl_engine_stream():
    '''
    POST /vl_agent/engine_stream
    시나리오 기반 VL Engine 실행 (스트리밍)
    '''
    start_ts = time.time()
    image_path = None
    image_size = None
    
    collected_events = []
    request_params = {}
    
    # 요청 헤더 로깅
    print('\n' + '='*80)
    print(f'  URL: {request.url}')
    print(f'  Remote: {request.remote_addr}')
    print('='*80)
    

    # 파라미터 파싱
    query = request.form.get('query', '').strip()
    scenario_name = request.form.get('scenario_name', 'BASkip').strip()
    is_canceled = request.form.get('is_canceled', 'false').lower() in ('true', '1', 'yes')
    verbose = request.form.get('verbose', 'false').lower() in ('true', '1', 'yes')
    if state.get_DEV_MODE():
        verbose = True
    
    # JSON 파싱
    memory = None
    try:
        memory = json.loads(request.form.get('memory', ''))
    except:
        pass
    
    parsed_think_log = None
    try:
        parsed_think_log = json.loads(request.form.get('think_log', ''))
    except:
        pass
    
    parsed_agent_state = None
    try:
        parsed_agent_state = json.loads(request.form.get('agent_state', ''))
    except:
        pass
    
    # 이미지 업로드 (필수)
    uploaded_file = request.files.get('image')
    if not (uploaded_file and uploaded_file.filename):
        print('  ERROR: image is required')
        return jsonify({'ok': False, 'error': "Missing required parameter 'image'"}), 400
    
    image_path, image_size = save_uploaded_image_as_png(uploaded_file, save_dir='./files/image')
    
    # think_log와 agent_state 복원
    from ai_vl_agent_types import AgentState, restore_think_log
    
    if parsed_think_log and len(parsed_think_log) > 0:
        think_log = restore_think_log(parsed_think_log)
    else:
        think_log = []
    
    agent_state = AgentState.from_dict(parsed_agent_state).to_dict()
    is_resume = parsed_think_log and len(parsed_think_log) > 0
    
    # 파싱 완료 후 로깅
    if True:
        print(f'\n[REQUEST PARAMS]')
        print(f'  query: "{query}"')
        print(f'  memory: {len(memory)} entries' if memory else '  memory: (none)')
        print(f'  think_log: {len(think_log)} entries' if think_log else '  think_log: (none)')
        print(f'  agent_state: keys={list(agent_state.keys())}' if agent_state else '  agent_state: (none)')
        print(f'  is_canceled: {is_canceled}')
        print(f'  verbose: {verbose}')
        print(f'  image: {image_path} ({image_size[0]}x{image_size[1]})')
    
    # 로그용 request_params
    is_resume = parsed_think_log and len(parsed_think_log) > 0
    request_params = {
        'query': query,
        'memory': memory,
        'image_path': image_path,
        'image_size': image_size,
        'think_log_count': len(think_log),
        'agent_state': agent_state,
        'is_canceled': is_canceled,
        'verbose': verbose,
        'mode': 'engine'
    }
    print('\n' + '-'*80)
    print(f'[VL ENGINE START] {"(재요청)" if is_resume else "(첫 요청)"}')
    print('-'*80)
    
    # ========================================
    # 스트리밍 응답 생성
    # ========================================
    def generate():        
        final_result = None
        
        try:
            for event in ai_vl_engine_run(
                image=image_path,
                think_log=think_log,
                agent_state=agent_state,
                query=query,
                memory=memory,
                is_canceled=is_canceled,
                verbose=verbose,
                scenario_name=scenario_name
            ):
                event_dict = event.to_dict()
                elapsed = int((time.time() - start_ts) * 1000)
                event_dict['elapsed_ms'] = elapsed
                
                collected_events.append(event_dict)
                
                # 콘솔 로깅
                print(f'[{elapsed}ms] {event.kind.upper()}: {event.message}')
                
                # final_result 저장
                if event.kind in ('done', 'fail', 'request_observation', 'act'):
                    final_result = event_dict
                
                yield json.dumps(event_dict, ensure_ascii=False) + '\n'
            
            print(f'\n[VL ENGINE END] Total: {int((time.time() - start_ts) * 1000)}ms')
            
            # 세션 로그 저장
            save_session_log(request_params, collected_events, final_result)
        
        except Exception as e:
            print(f'\n[ERROR] {str(e)}')
            print(traceback.format_exc())
            error_event = {
                'kind': 'fail',
                'message': f'Error: {str(e)}',
                'think_log': [],
                'data': {'trace': traceback.format_exc()},
                'elapsed_ms': int((time.time() - start_ts) * 1000)
            }
            collected_events.append(error_event)
            
            # 에러도 로그 저장
            save_session_log(request_params, collected_events, error_event)
            
            yield json.dumps(error_event, ensure_ascii=False) + '\n'
    
    return Response(generate(), content_type='application/json; charset=utf-8')


def vl_engine_form():
    '''
    POST /vl_agent/engine_form
    BAReader 시나리오 전용: 음성 합성 + 반환
    
    Parameters:
        actor: 화자 이름
        txt: 대사 텍스트
        lang: 언어 (기본: ja)
        speed: 속도 (기본: 1.0)
    
    Returns:
        wav binary + Header { X-Audio-Duration: float }
    '''
    print('\n' + '='*80)
    print(f'  URL: {request.url}')
    print(f'  Remote: {request.remote_addr}')
    print('='*80)
    
    # 파라미터 파싱
    actor = request.form.get('actor', '').strip()
    txt = request.form.get('txt', '').strip()
    lang = request.form.get('lang', 'ja').strip()
    speed = float(request.form.get('speed', '1.0'))
    verbose = request.form.get('verbose', 'false').lower() == 'true' 
    ocr_history_json = request.form.get('ocr_history_json', '').strip() 
    
    # actor가 비어있으면 기본값 'arona' 사용
    if not actor:
        actor = 'arona'
        print(f'  WARNING: actor is empty, using default: arona')
    
    print(f'\n[REQUEST PARAMS]')
    print(f'  actor: "{actor}"')
    print(f'  txt: "{txt[:50]}..."' if len(txt) > 50 else f'  txt: "{txt}"')
    print(f'  lang: {lang}')
    print(f'  speed: {speed}')
    print(f'  verbose: {verbose}')
    print(f'  ocr_history_json: {"<provided>" if ocr_history_json else "<empty>"}')
    
    # verbose 모드: ocr_history 로그 파일 저장
    if verbose and ocr_history_json:
        try:
            import json
            import os
            
            # JSON 파싱
            history_data = json.loads(ocr_history_json)
            ocr_history = history_data.get('history', [])
            
            if ocr_history:
                # 디렉토리 생성
                log_dir = './test/vl_agent'
                os.makedirs(log_dir, exist_ok=True)
                
                # 파일명: ocr_history_YYYYMMDD_HHMMSS.txt
                timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
                log_path = os.path.join(log_dir, f'ocr_history_{timestamp}.txt')
                
                # txt 파일 작성
                with open(log_path, 'w', encoding='utf-8') as f:
                    f.write(f'=== OCR History Log ===\n')
                    f.write(f'Generated: {datetime.now().strftime("%Y-%m-%d %H:%M:%S")}\n')
                    f.write(f'Total Entries: {len(ocr_history)}\n')
                    f.write('=' * 50 + '\n\n')
                    
                    for idx, entry in enumerate(ocr_history, 1):
                        f.write(f'[{idx}] Type: {entry.get("type", "unknown")}\n')
                        f.write(f'    Actor: {entry.get("actor", "")}\n')
                        f.write(f'    Text: {entry.get("txt", "")}\n')
                        if entry.get('choices'):
                            f.write(f'    Choices: {entry.get("choices")}\n')
                        f.write('\n')
                
                print(f'  [VERBOSE] OCR history saved: {log_path}')
        except Exception as e:
            print(f'  [VERBOSE] Failed to save ocr_history: {e}')
    
    # 필수 파라미터 검증
    if not txt:
        print('  ERROR: txt is required')
        return jsonify({'ok': False, 'error': "Missing required parameter 'txt'"}), 400
    
    # 음성 합성
    try:
        import voice_inference
        
        print(f'\n[VOICE] Synthesizing...')
        result_path = voice_inference.synthesize_char(actor, txt, audio_language=lang, speed=speed)
        
        if result_path == 'early stop':
            print('  ERROR: Synthesis stopped early')
            return jsonify({'ok': False, 'error': 'Synthesis process stopped early'}), 500
        
        print(f'  Generated: {result_path}')
        
        # 음성 길이 측정
        try:
            import soundfile as sf
            data, sr = sf.read(result_path)
            duration = len(data) / sr + 0.5  # soundfile 길이 + 0.5초
            print(f'  Duration: {duration:.2f}s (audio: {len(data)/sr:.2f}s + 0.5s)')
        except Exception as e:
            print(f'  WARNING: Could not measure audio duration: {e}')
            duration = 5.0  # fallback
        
        # wav 파일 반환
        response = send_file(result_path, mimetype='audio/wav')
        response.headers['X-Audio-Duration'] = str(duration)
        
        print(f'\n[VOICE] Sent: {result_path} (duration={duration:.2f}s)')
        return response
        
    except Exception as e:
        print(f'\n[ERROR] {str(e)}')
        print(traceback.format_exc())
        return jsonify({'ok': False, 'error': str(e)}), 500
