# TOBE2: 20 Questions Game

스무고개 게임 엔진 - VL Agent 아키텍처 기반

## 파일 구조

```
TOBE2/
├── __init__.py
├── ai_vl_agent_types_addon.py       # EVENT_KIND 확장
├── ai_vl_agent_functions_addon.py   # Function 메타
├── ai_20q_prompts.py                # 언어별 프롬프트 (ko/en/ja)
├── ai_20q_llm.py                    # LLM 호출 (Local/Gemini)
├── ai_20q_game.py                   # 메인 게임 루프
└── server_interface_20q_impl.py     # Flask Blueprint
```

## 주요 함수

### ai_20q_game.py

- `game_run_loop()` - 메인 게임 루프, AgentEvent 생성
- `handle_new_game()` - 새 게임 시작
- `handle_special_intents()` - 재시작/포기 의도 처리
- `handle_casual_chat()` - 일상 대화 처리
- `handle_stop_intent()` - 중단 의도 처리
- `handle_guess_intent()` - 추측 판정
- `handle_guide_question()` - 규칙 안내
- `handle_valid_question()` - 유효한 질문 처리

### ai_20q_llm.py

- `generate_answer()` - 질문에 예/아니오 답변 생성
- `generate_answer_stream()` - 답변 스트리밍 생성
- `classify_user_intent()` - 사용자 의도 분류
- `judge_guess_correctness()` - 정답 판정
- `generate_secret_target()` - 비밀 단어 생성
- `classify_restart_intent()` - 재시작 의도 분류
- `classify_continue_intent()` - 계속/포기 의도 분류
- `generate_casual_chat()` - 일상 대화 생성
- `extract_guess_from_text()` - 추측 단어 추출

## API 엔드포인트

### POST /game/20q/process
스무고개 게임 처리 (Stateless 스트리밍)

### POST /game/20q/start
새 게임 시작

### GET /game/20q/info
게임 정보 조회

### GET /game/20q/health
헬스 체크

## 서버 통합

```python
from TOBE2.server_interface_20q_impl import register_blueprint

app = Flask(__name__)
register_blueprint(app)
```

## 특징

- **Stateless**: context_data로 게임 상태 관리
- **다국어**: ko/en/ja 완전 지원
- **LLM 선택**: Local/Gemini/Auto
- **캐릭터**: 아로나 페르소나
- **스트리밍**: 실시간 응답

## Flow 다이어그램

### 호출 흐름

```
Flask Request (server_interface_20q_impl.py)
    ↓
game_run_loop(query, context_data, lang, char_name, server_type, api_key, history, history_question)
    ↓
├─ restore_20q_context(context_data) → 게임 상태 복원
│   └─ secret, theme_key, question_count, max_questions, waiting_for, game_status, game_result, history_secret_list
│
├─ [새 게임] handle_new_game()
│   ↓
│   generate_secret_target(theme_key, history_secret_list, lang, server_type, api_key)
│   └─ ai_20q_llm.py → LLMProvider → Local/Gemini API
│       └─ ai_20q_prompts.py (PROMPT_GENERATE_SECRET)
│
├─ [특수 의도] handle_special_intents()
│   ↓
│   classify_restart_intent(query, lang, server_type, api_key)
│   classify_continue_intent(query, lang, server_type, api_key)
│   └─ ai_20q_llm.py → LLMProvider
│
├─ [일상 대화] handle_casual_chat()
│   ↓
│   generate_casual_chat_stream(query, history, lang, server_type, api_key)
│   └─ ai_20q_llm.py → LLMProvider
│       └─ ai_20q_prompts.py (PROMPT_CASUAL_CHAT)
│
├─ [중단 의도] handle_stop_intent()
│   └─ 게임 종료 처리
│
├─ [추측 의도] handle_guess_intent()
│   ↓
│   extract_guess_from_text(query, lang, server_type, api_key)
│   judge_guess_correctness(secret, guess, theme_key, lang, server_type, api_key)
│   └─ ai_20q_llm.py → LLMProvider
│       └─ ai_20q_prompts.py (PROMPT_JUDGE_GUESS)
│
├─ [규칙 안내] handle_guide_question()
│   └─ 게임 규칙 안내 메시지
│
└─ [유효 질문] handle_valid_question()
    ↓
    generate_answer_stream(query, secret, theme_key, history_question, lang, server_type, api_key)
    └─ ai_20q_llm.py → LLMProvider
        └─ ai_20q_prompts.py (PROMPT_ANSWER)
```

### 주요 파라미터

**game_run_loop 입력:**
- `query`: 사용자 입력
- `context_data`: 게임 상태 (Stateless 핵심)
- `lang`: 언어 코드 (ko/en/ja)
- `char_name`: 캐릭터 이름 (arona)
- `server_type`: LLM 서버 타입 (Local/Gemini/Auto)
- `api_key`: Gemini API 키 (선택)
- `history`: 전체 대화 히스토리
- `history_question`: 질문/답변만 있는 히스토리

**game_run_loop 출력:**
- `AgentEvent` Generator (kind, message, think_log, data 포함)

**context_data 구조:**
- `secret`: 비밀 단어
- `theme_key`: 테마 키
- `question_count`: 현재 질문 카운트
- `max_questions`: 최대 질문 수
- `waiting_for`: 대기 상태
- `game_status`: 게임 상태
- `game_result`: 게임 결과
- `history_secret_list`: 이전 게임 정답 목록

### LLM 호출 계층

```
ai_20q_game.py (게임 로직)
    ↓
ai_20q_llm.py (LLM 인터페이스)
    ↓
LLMProvider (추상화)
    ├─ LocalLLMProvider → ai_singleton.get_llm()
    └─ GeminiLLMProvider → google.generativeai
    ↓
ai_20q_prompts.py (프롬프트 템플릿)
    └─ get_*_prompt() → 언어별 프롬프트 반환
```
