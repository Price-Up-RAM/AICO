# Multi Conversation 분리 작업 완료 보고서

## 개요

기존 `server_multi_impl.py`에서 Gemini API 호출과 Local LLM 연산이 혼재되어 있던 구조를 분리하여, 깔끔한 모듈 구조로 재설계했습니다.

## 생성된 파일 목록

| 파일명 | 역할 | 라인 수 |
|--------|------|---------|
| `multi_prompts.py` | 공통 프롬프트 모듈 | ~820 |
| `multi_gemini.py` | Gemini API 전용 모듈 | ~640 |
| `multi_local.py` | Local LLM 전용 모듈 | ~680 |
| `multi_server.py` | 새로운 서버 엔드포인트 | ~620 |

---

## 아키텍처 변경

### Before (ASIS)

```
server_multi_impl.py
├── Flow Director (ai_aropla_flow.py) → Local LLM 고정
├── 대화 생성
│   ├── if True: → util_gemini_multi.py (Gemini API)
│   └── else: → ai_conversation_binary_multi.py (Local LLM, dead code)
└── 프롬프트
    ├── Gemini: prompt_llm.get_gemma_multi_prompt → prompt_multi.py
    └── Qwen: 자체 get_qwen_multi_prompt (prompt_multi 미사용)
```

**문제점:**
- `if True:` 조건으로 Qwen 코드가 dead code
- Flow Director는 항상 Local LLM 사용 (Gemini와 혼재)
- Qwen 방식은 `prompt_multi.py` 공통 모듈 미사용

### After (TOBE)

```
multi_server.py
├── server_type='Gemini' → multi_gemini.py
│   ├── Flow Director (Gemini API)
│   └── 대화 생성 (Gemini API)
│
├── server_type='Local' → multi_local.py
│   ├── Flow Director (ai_singleton)
│   └── 대화 생성 (ai_singleton)
│
├── server_type='Hybrid' → multi_local (Flow) + multi_gemini (대화)
│   ├── Flow Director (ai_singleton) ← 빠른 연산
│   └── 대화 생성 (Gemini API) ← 고품질 응답
│
└── 공통: multi_prompts.py
    ├── get_multi_character_messages()
    ├── get_target_speaker_prompt()
    ├── get_flow_decision_prompt()
    ├── get_target_listener_prompt()
    └── format_qwen_prompt() / format_gemma_prompt()
```

---

## 파일별 상세 설명

### 1. multi_prompts.py

**역할:** Gemini/Local 공통 프롬프트 모듈

**주요 함수:**

| 함수명 | 설명 |
|--------|------|
| `get_multi_character_messages()` | 대화 생성용 전체 메시지 리스트 구축 |
| `get_target_speaker_prompt()` | 타겟 분석 프롬프트 (누구에게 말하는지) |
| `get_flow_decision_prompt()` | 다음 발화자 결정 프롬프트 |
| `get_target_listener_prompt()` | 청자 결정 프롬프트 |
| `format_qwen_prompt()` | Qwen 포맷 (`<\|im_start\|>`) |
| `format_gemma_prompt()` | Gemma 포맷 (`<start_of_turn>`) |
| `parse_target_speaker_response()` | 타겟 응답 파싱 |
| `parse_flow_decision_response()` | Flow 응답 파싱 |
| `parse_target_listener_response()` | 청자 응답 파싱 |

**핵심 변경:**
- 기존 `ai_conversation_binary_multi.py`의 자체 프롬프트 로직을 이 모듈로 통합
- Gemini와 Local이 동일한 프롬프트 품질을 사용

### 2. multi_gemini.py

**역할:** Gemini API 전용 통합 모듈

**주요 함수:**

| 함수명 | 설명 |
|--------|------|
| `process_conversation_stream()` | 대화 생성 스트리밍 (Gemini API) |
| `analyze_target_speaker()` | 타겟 분석 (Gemini API) |
| `decide_next_speaker()` | 다음 발화자 결정 (Gemini API) |
| `analyze_target_listener()` | 청자 결정 (Gemini API) |

**특징:**
- 모든 LLM 호출이 Gemini API 사용
- API 키 자동 회전 (GeminiAPIKeyManager)
- `util_gemini_multi.py` 참조하여 구현

### 3. multi_local.py

**역할:** Local LLM(Qwen) 전용 통합 모듈

**주요 함수:**

| 함수명 | 설명 |
|--------|------|
| `process_conversation_stream()` | 대화 생성 스트리밍 (ai_singleton) |
| `analyze_target_speaker()` | 타겟 분석 (ai_singleton) |
| `decide_next_speaker()` | 다음 발화자 결정 (ai_singleton) |
| `analyze_target_listener()` | 청자 결정 (ai_singleton) |

**핵심 업그레이드:**
- 기존 자체 `get_qwen_multi_prompt()` → `multi_prompts.py` 공통 모듈 사용
- Gemini와 동일한 프롬프트 품질 적용
- `ai_conversation_binary_multi.py` + `ai_aropla_flow.py` 통합

### 4. multi_server.py

**역할:** 새로운 서버 엔드포인트

**API:**

```
POST /multi/conversation
```

**주요 파라미터:**

| 파라미터 | 타입 | 설명 |
|----------|------|------|
| `server_type` | string | `'Gemini'` / `'Local'` / `'Hybrid'` (기본: Gemini) |
| `query` | string | 사용자 메시지 |
| `current_speaker` | string | 현재 발화자 |
| `target_speaker` | string | (선택) 지정 응답자 |
| `participants` | JSON | 참여자 목록 |
| `memory` | JSON | 대화 기록 |
| `guideline_list` | JSON | 가이드라인 |
| `situation` | JSON | 상황 정보 |

**server_type별 동작:**

| server_type | Flow Director | 대화 생성 | 특징 |
|-------------|---------------|-----------|------|
| `Gemini` | Gemini API | Gemini API | 고품질, API 비용 발생 |
| `Local` | ai_singleton | ai_singleton | 무료, 로컬 GPU 사용 |
| `Hybrid` | ai_singleton | Gemini API | 빠른 Flow + 고품질 대화 |

### Hybrid 모드 상세 설명

Hybrid 모드는 **비용 효율성**과 **응답 품질** 사이의 균형을 맞춘 모드입니다:

```
┌─────────────────────────────────────────────────────────────┐
│                      Hybrid Mode                            │
├─────────────────────────────────────────────────────────────┤
│  Flow Director (Local LLM - 빠름)                           │
│  ├── analyze_target_speaker()    → 타겟 분석               │
│  ├── analyze_target_listener()   → 청자 결정               │
│  └── decide_next_speaker()       → 다음 발화자 결정        │
├─────────────────────────────────────────────────────────────┤
│  Conversation (Gemini API - 고품질)                         │
│  └── process_conversation_stream() → 실제 대화 생성        │
└─────────────────────────────────────────────────────────────┘
```

**장점:**
- Flow Director는 단순 분류 작업이므로 Local LLM으로 충분
- 실제 대화 생성은 Gemini API의 고품질 응답 활용
- API 호출 횟수 감소로 비용 절감

---

## 사용 예시

### Python 클라이언트

```python
import requests

# Gemini 모드
response = requests.post('http://localhost:5001/multi/conversation', data={
    'server_type': 'Gemini',
    'query': '아로나야, 오늘 뭐해?',
    'current_speaker': 'sensei',
    'ai_language': 'ko'
})

# Local 모드
response = requests.post('http://localhost:5001/multi/conversation', data={
    'server_type': 'Local',
    'query': '프라나는 어떻게 생각해?',
    'current_speaker': 'sensei',
    'ai_language': 'ko'
})

# Hybrid 모드 (Flow는 Local, 대화는 Gemini)
response = requests.post('http://localhost:5001/multi/conversation', data={
    'server_type': 'Hybrid',
    'query': '둘 다 의견 말해봐',
    'current_speaker': 'sensei',
    'ai_language': 'ko'
})
```

### 서버 실행

```bash
python multi_server.py
# Starting Multi Server on port 5001...
```

---

## 의존성 다이어그램

```
multi_server.py
    │
    ├── multi_gemini.py ──────┬─→ google.generativeai
    │       │                 └─→ kei.py (API Keys)
    │       │
    │       └─→ multi_prompts.py ──→ prompt_char.py
    │
    ├── multi_local.py ───────┬─→ ai_singleton.py
    │       │                 │
    │       └─→ multi_prompts.py ──→ prompt_char.py
    │
    └── (공통)
        ├── state.py
        ├── util_string.py
        ├── util_proper_nouns.py
        └── util_translator.py
```

---

## ASIS 파일과의 관계

| ASIS 파일 | TOBE 파일 | 관계 |
|-----------|-----------|------|
| `server_multi_impl.py` | `multi_server.py` | 참조 (변경 없음) |
| `util_gemini_multi.py` | `multi_gemini.py` | 참조 (변경 없음) |
| `ai_conversation_binary_multi.py` | `multi_local.py` | 참조 (변경 없음) |
| `ai_aropla_flow.py` | `multi_gemini.py`, `multi_local.py` | 참조 (변경 없음) |
| `prompt_multi.py` | `multi_prompts.py` | 참조 (변경 없음) |
| `ai_multi_prompts.py` | `multi_prompts.py` | 참조 (변경 없음) |

**모든 ASIS 파일은 변경하지 않았습니다.**

---

## 테스트 방법

각 모듈은 `if __name__ == '__main__':` 블록에 테스트 코드가 포함되어 있습니다.

```bash
# 프롬프트 모듈 테스트
python multi_prompts.py

# Gemini 모듈 테스트
python multi_gemini.py

# Local 모듈 테스트
python multi_local.py

# 서버 실행
python multi_server.py
```

---

## 향후 개선 사항

1. **캐싱**: Flow Director 결과 캐싱으로 반복 호출 최소화
2. **병렬 처리**: 다중 캐릭터 동시 응답 생성
3. **모델 선택**: server_type별 세부 모델 지정 기능
4. **메트릭스**: 응답 시간, API 비용 추적 기능

---

*작성일: 2026-01-28*
