'''
server_interface_20q_impl.py
20 Questions Game Flask Blueprint 엔드포인트

Flask Blueprint로 모듈화하여 메인 서버에 쉽게 통합 가능합니다.
Stateless 스트리밍 API를 제공합니다.
'''
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import json
from flask import Blueprint, Response, request, jsonify
from threading import Lock

# 게임 모듈
from ai_20q_game import game_run_loop
from ai_vl_agent_types_addon import AgentEvent


# ============================================================================
# Blueprint 생성
# ============================================================================

game_20q_bp = Blueprint('game_20q', __name__, url_prefix='/game/20q')

request_lock = Lock()


# ============================================================================
# Stateless Streaming 엔드포인트
# ============================================================================
'''
스무고개 게임 처리 (Stateless 스트리밍)

Request JSON:
{
    "query": "살아있어?",           # 사용자 입력
    "context_data": {...},         # 게임 상태 (Stateless 핵심)
    "lang": "ko",                  # 언어 (ko/en/ja)
    "char_name": "arona",          # 캐릭터 이름
    "server_type": "Local",        # LLM 타입 (Local/Gemini/Auto)
    "api_key": "",                 # Gemini API 키 (선택)
    "history": [],                 # 전체 대화 히스토리
    "history_question": []         # 질문/답변 히스토리
}

Response (Stream):
    각 라인은 JSON 객체로 AgentEvent를 전달
    {"kind": "...", "message": "...", "think_log": [...], "data": {...}}
'''
@game_20q_bp.route('/process', methods=['POST'])
def process():

    try:
        data = request.get_json() or {}
    except:
        data = {}
    
    query = data.get('query', '')
    context_data = data.get('context_data')
    lang = data.get('lang', 'ko')
    char_name = data.get('char_name', 'arona')
    server_type = data.get('server_type', 'Local')
    api_key = data.get('api_key', '')
    history = data.get('history', [])
    history_question = data.get('history_question', [])
    
    def generate():
        with request_lock:
            try:
                for event in game_run_loop(
                    query=query,
                    context_data=context_data,
                    lang=lang,
                    char_name=char_name,
                    server_type=server_type,
                    api_key=api_key,
                    history=history,
                    history_question=history_question
                ):
                    event_dict = event.to_dict()
                    yield json.dumps(event_dict, ensure_ascii=False) + '\n'
            except Exception as e:
                error_event = AgentEvent(
                    kind='error',
                    message=str(e),
                    think_log=[],
                    data={'error_type': type(e).__name__}
                )
                yield json.dumps(error_event.to_dict(), ensure_ascii=False) + '\n'
    
    return Response(
        generate(),
        mimetype='application/x-ndjson',
        headers={'Cache-Control': 'no-cache'}
    )

'''
새 게임 시작 (Stateless)

Request JSON:
{
    "theme_key": "animal",         # 테마 키 (선택, 없으면 랜덤)
    "lang": "ko",                  # 언어
    "char_name": "arona",          # 캐릭터 이름
    "server_type": "Local",        # LLM 타입
    "api_key": "",                 # Gemini API 키
    "history_secret_list": []      # 이전에 사용한 정답 목록
}

Response:
    JSON {"kind": "game_start", "message": "...", ...}
'''
@game_20q_bp.route('/start', methods=['POST'])
def start_game():

    try:
        data = request.get_json() or {}
    except:
        data = {}
    
    theme_key = data.get('theme_key')  # None이면 랜덤
    lang = data.get('lang', 'ko')
    char_name = data.get('char_name', 'arona')
    server_type = data.get('server_type', 'Local')
    api_key = data.get('api_key', '')
    history_secret_list = data.get('history_secret_list', [])
    
    # context_data 없이 호출하면 새 게임 시작
    initial_context = None
    if theme_key:
        from ai_vl_agent_types_addon import create_20q_context_data
        initial_context = create_20q_context_data(
            secret=None,
            theme_key=theme_key,
            question_count=0,
            max_questions=20,
            waiting_for=None,
            game_status='game_start',
            game_result=None,
            history_secret_list=history_secret_list
        )
    
    def generate():
        with request_lock:
            try:
                for event in game_run_loop(
                    query='',
                    context_data=initial_context,
                    lang=lang,
                    char_name=char_name,
                    server_type=server_type,
                    api_key=api_key,
                    history=[],
                    history_question=[]
                ):
                    event_dict = event.to_dict()
                    yield json.dumps(event_dict, ensure_ascii=False) + '\n'
            except Exception as e:
                error_event = AgentEvent(
                    kind='error',
                    message=str(e),
                    think_log=[],
                    data={'error_type': type(e).__name__}
                )
                yield json.dumps(error_event.to_dict(), ensure_ascii=False) + '\n'
    
    return Response(
        generate(),
        mimetype='application/x-ndjson',
        headers={'Cache-Control': 'no-cache'}
    )

# 게임 정보 반환
@game_20q_bp.route('/info', methods=['GET'])
def get_info():
    from ai_vl_agent_functions_addon import THEME_LIST
    
    return jsonify({
        'themes': list(THEME_LIST.keys()),
        'theme_names': THEME_LIST,
        'max_questions': 20,
        'supported_langs': ['ko', 'en', 'ja']
    })


@game_20q_bp.route('/health', methods=['GET'])
def health_check():
    '''헬스 체크'''
    return jsonify({'status': 'ok', 'service': '20q_game'})


# ============================================================================
# Blueprint 등록 헬퍼
# ============================================================================

def register_blueprint(app):
    '''
    Flask 앱에 Blueprint 등록
    
    Usage:
        from TOBE2.server_interface_20q_impl import register_blueprint
        register_blueprint(app)
    '''
    app.register_blueprint(game_20q_bp)


# ============================================================================
# Standalone 서버 (개발/테스트용)
# ============================================================================

def create_app():
    '''개발/테스트용 Flask 앱 생성'''
    from flask import Flask
    app = Flask(__name__)
    register_blueprint(app)
    
    @app.route('/')
    def index():
        return jsonify({
            'service': '20 Questions Game API',
            'endpoints': [
                '/game/20q/process (POST) - 게임 처리',
                '/game/20q/start (POST) - 새 게임 시작',
                '/game/20q/info (GET) - 게임 정보',
                '/game/20q/health (GET) - 헬스 체크'
            ]
        })
    
    return app


if __name__ == '__main__':
    print('=== 20 Questions Game Server (Standalone) ===')
    app = create_app()
    app.run(host='0.0.0.0', port=5001, debug=True)
