'''
server_interface_vl_planner_impl.py
VL Planner Flask 엔드포인트 (범용 Stateless 버전)
'''
import sys
import os

from flask import Flask, request, jsonify, Response
import io
import json
import time
import traceback
from datetime import datetime
from pathlib import Path

from flask import Flask, request, jsonify, Response, Blueprint

from PIL import Image

import state
from util_image import save_uploaded_image_as_png
from ai_vl_planner import ai_vl_planner_run_loop
from ai_vl_agent_types import ThinkEntry, AgentEvent


app = Flask(__name__)


# 임시 이미지 저장 경로
TEMP_IMAGE_DIR = Path('./test/temp_frames')
TEMP_IMAGE_DIR.mkdir(parents=True, exist_ok=True)


def save_temp_image(image_data, prefix='frame'):
    '''이미지 데이터를 임시 파일로 저장하고 경로 반환'''
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S_%f')
    filename = f'{prefix}_{timestamp}.png'
    filepath = TEMP_IMAGE_DIR / filename
    
    try:
        if hasattr(image_data, 'read'):
            # FileStorage 객체
            img = Image.open(image_data)
            img.save(str(filepath), format='PNG')
        elif isinstance(image_data, bytes):
            # 바이트 데이터
            img = Image.open(io.BytesIO(image_data))
            img.save(str(filepath), format='PNG')
        else:
            return None
        
        return str(filepath)
    except Exception as e:
        print(f'[save_temp_image] Error: {e}')
        return None


def think_log_to_dict(think_log):
    '''ThinkEntry 리스트를 dict 리스트로 변환'''
    result = []
    for entry in think_log:
        if isinstance(entry, ThinkEntry):
            result.append({
                'idx': entry.idx,
                'phase': entry.phase,
                'content': entry.content,
                'timestamp': entry.timestamp
            })
        elif isinstance(entry, dict):
            result.append(entry)
    return result


def agent_event_to_dict(event):
    '''AgentEvent를 dict로 변환'''
    if isinstance(event, AgentEvent):
        return {
            'kind': event.kind,
            'message': event.message,
            'think_log': think_log_to_dict(event.think_log),
            'data': event.data
        }
    return event


@app.route('/vl_agent/run', methods=['POST'])
def vl_planner_stream():
    '''
    VL Planner 실행 (스트리밍)
    
    Parameters (multipart/form-data):
    - image: 화면 캡처 이미지 (필수)
    - query: 사용자 쿼리 (첫 요청 시 필수)
    - memory: 대화 기록 JSON (선택)
    - think_log: 재요청용 think_log JSON (재요청 시 필수)
    - retry_count: 현재 재요청 횟수 (선택, 기본 0)
    - is_canceled: 취소 여부 (선택, 기본 false)
    - verbose: 디버그 모드 (선택, 기본 false)
    
    Response (Streaming JSON Lines):
    각 줄은 JSON 객체:
    {"kind": "goal", "message": "...", "think_log": [...], "data": {...}}
    '''
    try:
        # ========================================
        # 파라미터 파싱
        # ========================================
        
        # 이미지 처리
        image_file = request.files.get('image')
        if not image_file:
            return jsonify({'error': 'image is required'}), 400
        
        frame_path = save_temp_image(image_file)
        if not frame_path:
            return jsonify({'error': 'Failed to save image'}), 500
        
        # 쿼리 (첫 요청 시 필수)
        query = request.form.get('query', '')
        
        # 메모리 (대화 기록)
        memory_str = request.form.get('memory', '')
        memory = None
        if memory_str:
            try:
                memory = json.loads(memory_str)
            except:
                memory = None
        
        # think_log (재요청 시)
        think_log_str = request.form.get('think_log', '')
        resume_think_log = None
        if think_log_str:
            try:
                resume_think_log = json.loads(think_log_str)
            except:
                resume_think_log = None
        
        # retry_count
        try:
            retry_count = int(request.form.get('retry_count', '0'))
        except:
            retry_count = 0
        
        # is_canceled
        is_canceled_str = request.form.get('is_canceled', 'false').lower()
        is_canceled = is_canceled_str in ['true', '1', 'yes']
        
        # verbose
        verbose_str = request.form.get('verbose', 'false').lower()
        verbose = verbose_str in ['true', '1', 'yes']
        
        # 검증: 첫 요청이면 query 필수
        if not resume_think_log and not query:
            return jsonify({'error': 'query is required for initial request'}), 400
        
        print(f'\n[VL Planner] Request received')
        print(f'  query: {query[:50]}...' if query else '  query: (resume)')
        print(f'  frame: {frame_path}')
        print(f'  resume_think_log: {len(resume_think_log) if resume_think_log else 0} entries')
        print(f'  retry_count: {retry_count}')
        print(f'  is_canceled: {is_canceled}')
        print(f'  verbose: {verbose}')
        
        # ========================================
        # 스트리밍 응답 생성
        # ========================================
        
        def generate():
            try:
                for event in ai_vl_planner_run_loop(
                    query=query,
                    memory=memory,
                    initial_frame=frame_path,
                    resume_think_log=resume_think_log,
                    retry_count=retry_count,
                    max_retry=5,
                    is_canceled=is_canceled,
                    max_iters=5
                ):
                    event_dict = agent_event_to_dict(event)
                    event_json = json.dumps(event_dict, ensure_ascii=False)
                    yield event_json + '\n'
                    
                    if verbose:
                        print(f'[VL Planner] Event: {event.kind} - {event.message}')
                    
            except Exception as e:
                error_event = {
                    'kind': 'error',
                    'message': str(e),
                    'think_log': [],
                    'data': {'traceback': traceback.format_exc()}
                }
                yield json.dumps(error_event, ensure_ascii=False) + '\n'
        
        return Response(
            generate(),
            mimetype='application/x-ndjson',
            headers={
                'Cache-Control': 'no-cache',
                'X-Accel-Buffering': 'no'
            }
        )
        
    except Exception as e:
        traceback.print_exc()
        return jsonify({
            'error': str(e),
            'traceback': traceback.format_exc()
        }), 500


@app.route('/health', methods=['GET'])
def vl_planner_health():
    '''헬스 체크'''
    return jsonify({
        'status': 'ok',
        'service': 'vl_planner',
        'timestamp': datetime.now().isoformat()
    })

# ========================================
# 독립 실행 (테스트용)
# ========================================
if __name__ == '__main__':
    # 서버 초기화
    state.set_use_gpu_percent(99999)
    state.model_name = "Qwen3VL-8B-Instruct-Q4_K_M.gguf"

    state.DEV_MODE = True
    state.is_write_log_file = True
    
    print('### Starting VL Agent Server on port 5000 (Streaming Mode)...')
    app.run(host='0.0.0.0', port=5000, debug=True)

