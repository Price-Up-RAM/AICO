# BAReader 시나리오 구현 계획

## 개요

BASkip이 모모토크 스토리를 **스킵**하는 시나리오라면, BAReader는 모모토크 스토리를 **읽어주는** 시나리오.

- 대사 화면(푸른 역삼각 ▽)을 감지하면 → 2단계 분류 + OCR로 텍스트 추출 → Unity에 통보 → Unity가 음성 요청 → 음성 반환 → 재생 + 대기 → 클릭 → 반복
- `シナリオリスト`가 OCR로 감지되면 종료

---

## Unity ↔ Python 통신 흐름

### 엔드포인트 2개 사용

| 엔드포인트 | 용도 | 반환 |
|------------|------|------|
| `POST /vl_agent/engine_stream` | 시나리오 판별 + OCR + 액션 결정 | JSON 스트리밍 (AgentEvent) |
| `POST /vl_agent/engine_form` | 음성 합성 + 반환 | wav 바이너리 (`send_file`) + 헤더에 duration |

### 전체 시퀀스

```
[1] Unity → POST /vl_agent/engine_stream (프레임 + agent_state)
    Python: 푸른 삼각 감지 → 분류 → OCR
    Python → Unity: AgentEvent(kind='act') + OCR 결과 + ocr_history 누적
    ※ 이 시점에서 음성은 아직 없음. "음성을 요청하라"는 정보만 전달

[2] Unity → POST /vl_agent/engine_form (actor, txt)
    Python: synthesize_char(actor, txt) → wav 생성
    Python → Unity: send_file(wav) + 헤더 { duration: soundfile길이+0.5s }
    Unity: wav 재생 + duration만큼 대기

[3] Unity: 클릭 실행 (역삼각 위치)

[4] Unity → POST /vl_agent/engine_stream (새 프레임 + agent_state)
    ※ agent_state에 ocr_history 포함 → 누적 유지
    Python: S1 or S10 판별 → [1]로 반복 또는 종료
```

---

## `ocr_history` (Stateless 누적)

`agent_state` 안에 포함하여 매 요청마다 왕복:

```
agent_state = {
    'expected_state': ['S1', 'S10'],
    'remain_retry_count': 5,
    'retry_interval': 2.0,
    'ocr_history': [
        {'type': 'dialogue_with_actor', 'actor': '新素材開発部員A', 'txt': 'やっ、アリス。'},
        {'type': 'narration', 'actor': '', 'txt': 'しばらくして...'},
        {'type': 'choice', 'actor': '', 'txt': 'やあ。', 'choices': ['やあ。', '何してる？']},
    ]
}
```

- `engine_stream` 호출 시 Unity가 `agent_state` 전체를 보냄 (`ocr_history` 포함)
- Python은 새 OCR 결과를 `ocr_history`에 append 후 반환
- `engine_form` 호출 시에는 ocr_history 불필요 (단순 TTS)

---

## 시나리오 흐름 (S1 / S10)

### S1: 대사 읽기 (메인 루프)

**식별 조건**: OpenCV 템플릿 매칭으로 **푸른 역삼각(▽)** 감지 (threshold=0.9)

**액션 순서**:
1. **2단계 분류** (`build_classify_step1_is_choice` → `build_classify_step2_non_choice`)
   - choice / dialogue_with_actor / dialogue_no_actor / narration / none 판별
2. **OCR 추출** (`build_ocr_dialogue_prompt(dialogue_type)`)
   - `actor`(화자명)와 `txt`(대사 텍스트) 획득
3. **ocr_history에 append**
4. **AgentEvent 반환**: OCR 결과 + 역삼각 클릭 좌표 + `request_voice: true`

**기대 전이**: S1 (반복) 또는 S10 (종료)

### S10: 종료 판별

**식별 조건**: `シナリオリスト` 키워드가 OCR로 find됨

**액션**: `done` 반환

---

## 신규 파일

| 파일 | 설명 |
|------|------|
| `ai_vl_scenario_identify_BARead.py` | S1, S10 식별 함수 |
| `ai_vl_scenario_action_BARead.py` | S1, S10 액션 함수 |
| `ai_vl_engine_images.py` | OpenCV 템플릿 매칭 유틸 (`find_template`) |

> `ai_vl_engine.py`는 이미 `BAReader` 분기가 존재하므로 수정 불필요
> 템플릿 이미지는 `./prompt/extra/` 에 저장 관리

### 수정 파일

| 파일 | 변경 내용 |
|------|-----------|
| `ai_vl_engine_keywords.py` | `KEYWORD_SCENARIO_LIST = 'シナリオリスト'` 추가 |
| `server_interface_vl_engine_impl.py` | `engine_form` 엔드포인트 추가 |

---

## AgentEvent / AgentState 활용

### 기존 활용하는 것들

| 항목 | 용도 |
|------|------|
| `EVENT_KIND_ACT` | S1: OCR 결과 + 클릭 좌표 + 음성 요청 플래그 전달 |
| `EVENT_KIND_DONE` | S10 종료 |
| `EVENT_KIND_OBSERVE` | 식별 실패 시 재시도 |
| `EVENT_KIND_FAIL` | 연속 실패 시 |
| `agent_state['expected_state']` | 다음 예상 시나리오 (S1 또는 S10) |
| `agent_state['remain_retry_count']` | 재시도 횟수 |
| `agent_state['retry_interval']` | 재시도 간격 |
| `agent_state['ocr_history']` | OCR 대화 내역 누적 (동적 추가) |

---

## 기술 검토 사항 (확정)

### 1. 음성 전달 방식 — 별도 엔드포인트

`engine_stream`은 JSON 스트리밍이므로 wav 바이너리를 직접 포함 불가.
→ `engine_form` 엔드포인트에서 `send_file`로 반환 (기존 TTS 패턴 동일)

```
POST /vl_agent/engine_form
  Body: actor=新素材開発部員A&txt=やっ、アリス。
  Response: wav binary + Header { X-Audio-Duration: soundfile길이+0.5 }
```

### 2. 푸른 역삼각(▽) 아이콘 검색 — OpenCV 템플릿 매칭 채택

**채택: OpenCV `matchTemplate`** (기술 검증 완료, threshold=0.9)

- 템플릿 이미지 저장 위치: `./prompt/extra/triangle.png`
- `ai_vl_engine_images.py` 에서 `find_template()` 함수 제공

### 3. `シナリオリスト` 키워드 등록

`ai_vl_engine_keywords.py`에 추가

### 4. 음성 길이 측정 (duration = soundfile 길이 + 0.5초)

`soundfile`로 wav 길이 측정 + 0.5초 → `X-Audio-Duration` 헤더에 포함

### 5. 선택지(choice) 처리

선택지 정보와 OCR 내용을 그대로 `data`에 포함하여 전달.
Python은 판단하지 않고, Unity 쪽에서 활용 방법 결정.

---

## S1 액션의 AgentEvent data 구조

```
AgentEvent(
    kind='act',
    data={
        'action': 'click',
        'x': ..., 'y': ...,               # 역삼각 위치 (클릭 좌표)
        'request_voice': True,             # Unity가 engine_form 호출해야 함
        'voice_actor': '新素材開発部員A',   # engine_form에 보낼 actor
        'voice_txt': 'やっ、アリス。',      # engine_form에 보낼 txt
        'dialogue_type': 'dialogue_with_actor',
        'ocr_result': {
            'actor': '新素材開発部員A',
            'txt': 'やっ、アリス。それに先生も、こんにちは。',
        },
        'choices': null,                   # choice일 때만 배열
        'agent_state': {
            'expected_state': ['S1', 'S10'],
            'remain_retry_count': 5,
            'ocr_history': [...]           # 누적 대화 내역
        }
    }
)
```
