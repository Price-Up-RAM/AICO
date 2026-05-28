# Plan3: Tool 전체 목록 및 신규 Tool 설계

## 1. 현재 보유 Tool 전체 목록 (현황)

> BackGround.md + 코드 분석 기반 정리

### Python 내부 실행 — Primitive

| # | Tool 이름 | 기능 | 분류 | 위험도 |
|---|---|---|---|---|
| 1 | `alarm_counter` | 자연어에서 알람 시간 오프셋 JSON 추출 | 텍스트 생성 | 🟢 조회 |
| 2 | `alarm_maker` | 알람 문장 생성 | 텍스트 생성 | 🟢 조회 |
| 3 | `vl_grounding` | 화면에서 텍스트 기반 대상 좌표 탐색 | VL 탐지 | 🟢 조회 |
| 4 | `vl_keyword_detect` | UI 키워드 존재 여부 탐지 | VL 탐지 | 🟢 조회 |
| 5 | `vl_prompt_call` | 커스텀 프롬프트로 VL 모델 단발 호출 | VL 탐지 | 🟢 조회 |
| 6 | `solve_problem` | 이미지 속 문제(수학, 퀴즈 등) 풀이 | VL 분석 | 🟢 조회 |
| 7 | `web_search` | 웹 검색 수행 + 결과 반환 | 정보 조회 | 🟢 조회 |
| 8 | `web_search_keyword` | 검색 키워드 생성 | 텍스트 생성 | 🟢 조회 |
| 9 | `paddle_ocr` | PaddleOCR 텍스트 인식 | VL 탐지 | 🟢 조회 |
| 10 | `emotion_classification` | 텍스트 감정 분류 (joy/anger/etc.) | 텍스트 분석 | 🟢 조회 |
| 11 | `detect_language` | 입력 언어 자동 감지 | 텍스트 분석 | 🟢 조회 |
| 12 | `translation` | 다국어 번역 (en/ko/ja) | 텍스트 변환 | 🟢 조회 |
| 13 | `tts_synthesis` | 텍스트 → 음성 합성 (wav) | 음성 | 🟢 연출 |
| 14 | `stt` | 음성 → 텍스트 (VAD + Whisper) | 음성 | 🟢 조회 |

### Python 내부 실행 — Intent 분류기

| # | Tool 이름 | 기능 | 비고 |
|---|---|---|---|
| 15 | `intent_web` | 웹 검색 필요 여부 판단 | on/off/force |
| 16 | `intent_image` | 이미지 관련 질문 여부 판단 | on/off/force |
| 17 | `intent_confirm` | 이전 intent 질문에 대한 확인 여부 | yes/no |
| 18 | `chk_smalltalk_relevance` | 스몰톡 응답 여부 판단 | 대화 맥락용 |
| 19 | `intent_turn_light` | IoT 조명 제어 의도 (비활성) | 현재 disabled |

### Unity 클라이언트 요청 — Primitive

| # | Tool 이름 | 기능 | 위험도 |
|---|---|---|---|
| 20 | `request_frame` | 새 화면 캡처 요청 | 🟢 조회 |
| 21 | `request_click` | 지정 좌표 클릭 수행 | 🔴 직접 조작 |
| 22 | `request_screenshot` | 전체 스크린샷 저장 | 🟢 조회 |
| 23 | `request_dance` | 캐릭터 댄스 | 🟢 연출 |
| 24 | `play_sfx_alert` | 알림 음성 재생 | 🟢 연출 |

### 중앙 제어 — Orchestration

| # | Tool 이름 | 기능 | 사용 tool |
|---|---|---|---|
| 25 | VL Planner | 목표 달성을 위한 관찰-판단-행동 루프 | `request_frame`, `vl_grounding`, `request_click` 등 |
| 26 | Scenario Engine (BAReader) | 대사 OCR → TTS 재생 → 클릭 넘기기 | `request_frame`, `vl_prompt_call`, `request_click`, `tts` |
| 27 | Scenario Engine (BASkip) | 스토리 자동 스킵 | `request_frame`, `vl_keyword_detect`, `request_click` |

---

## 2. 신규 Tool 설계 (Todo)

### 2-1. 고급 PC/UI 조작 도구 (Computer-Use 패턴)

> 기존 `request_click`의 한계를 극복하기 위해 확장되는 Unity 요청 도구들

| # | Tool 이름 | 기능 | 실행 위치 | 상태 |
|---|---|---|---|---|
| C1 | `request_scroll` | 지정된 위치에서 위/아래/좌/우 스크롤 | Unity 요청 | `[ ]` Todo |
| C2 | `request_drag` | A좌표에서 B좌표로 드래그 (슬라이더 조작 등) | Unity 요청 | `[ ]` Todo |
| C3 | `request_type` | 지정된 텍스트 타이핑 입력 | Unity 요청 | `[ ]` Todo |
| C4 | `request_hotkey` | 단축키 입력 (Ctrl+C, Alt+Tab 등) | Unity 요청 | `[ ]` Todo |
| C5 | `request_window_focus` | 특정 애플리케이션 창 포커스/활성화 | Unity 요청 | `[ ]` Todo |
| C6 | `clipboard_read` | 클립보드 텍스트 읽기 | Unity/Python | `[ ]` Todo |
| C7 | `clipboard_write` | 클립보드에 텍스트 쓰기 | Unity/Python | `[ ]` Todo |

---

### 2-2. Analysis Tool (LLM 결합형 파이썬 래퍼)

> 파이썬 코드의 하드코딩적 안정성(에러 체크, 흐름 제어)과 LLM의 유연한 분석(결과 해석) 능력을 결합한 체이닝 도구입니다.

| # | Tool 이름 | 기능 | 상태 |
|---|---|---|---|
| A1 | `analysis_alarm` | `alarm_maker` 등의 JSON 반환값을 분석하여 에러 체크 후 알람 설정 지시 | `[ ]` Todo |
| A2 | `analysis_grounding` | `vl_grounding` 좌표 분석 → 에러 체크 → 클릭 여부/방향 지시 | `[ ]` Todo |
| A3 | `analysis_screen` | 화면 전체 분석 → 상황 파악 → 다음 행동 방향 지시 | `[ ]` Todo |
| A4 | `analysis_diff` | `diff_compare` 결과 비교 → 변화 판단 → 대기 계속/종료 지시 | `[ ]` Todo |

---

### 2-3. 외부 이벤트 API (tool_calling)

| # | Tool 이름 | 동작 | 상태 |
|---|---|---|---|
| E1 | `tool_calling` (API) | 외부 트리거 발생 시 파이썬 라우터를 호출하여 작업을 지시하는 엔드포인트 | `[ ]` Todo |

---

### 2-4. Skill / Rule 베이스 도구

> 환각 방지를 위해 철저히 유저가 명시적으로 정의한 마크다운 기반 템플릿입니다.

| # | Tool 이름 | 동작 | 상태 |
|---|---|---|---|
| S1 | `skill_maker` | 유저가 정의한 스킬(행동 패턴)을 로컬 파일에 저장 | `[ ]` Todo |
| S2 | `skill_reader` | 로컬 파일에서 저장된 스킬을 중앙 제어 루프가 읽어옴 | `[ ]` Todo |
| S3 | `rule_maker` | 유저가 정의한 판단 기준(Rule)을 로컬 파일에 저장 | `[ ]` Todo |
| S4 | `rule_reader` | 로컬 파일에서 판단 기준(Rule)을 읽어옴 | `[ ]` Todo |

---

### 2-5. 온디맨드 로딩 지원 (Router Helper Tools)

> 라우터(Router)가 유저 요청을 처리할 때, 사전에 사용 가능한 무기들을 파악하기 위해 명시적으로 호출하는 리스트 반환 툴입니다.

| # | Tool 이름 | 기능 | 출력 형태 | 상태 |
|---|---|---|---|---|
| H1 | `get_available_tools` | 현재 시스템에서 바로 호출 가능한 Primitive Tool들의 목록과 기능 요약을 반환 | JSON 배열 | `[ ]` Todo |
| H2 | `get_available_skills` | 유저가 작성한 `skills/` 폴더 내 마크다운 파일들의 메타데이터(Frontmatter) 목록 반환 | JSON 배열 | `[ ]` Todo |

---

### 2-6. Unity / Python CRUD Tool

| # | Tool 이름 | 동작 | 상태 |
|---|---|---|---|
| U1 | `unity_crud_read` | Unity Persist 저장소에서 데이터 조회 | `[ ]` Todo |
| U2 | `unity_crud_create` | Unity Persist 저장소에 데이터 추가 | `[ ]` Todo |
| U3 | `unity_crud_update` | Unity Persist 저장소 데이터 수정 | `[ ]` Todo |
| U4 | `unity_crud_delete` | Unity Persist 저장소 데이터 삭제 | `[ ]` Todo |
| P1 | `python_crud_read` | 파이썬 로컬 파일/데이터 조회 | `[ ]` Todo |
| P2 | `python_crud_create`| 파이썬 로컬 데이터 생성 | `[ ]` Todo |
| P3 | `python_crud_update`| 파이썬 로컬 데이터 수정 | `[ ]` Todo |
| P4 | `python_crud_delete`| 파이썬 로컬 데이터 삭제 | `[ ]` Todo |

---

### 2-7. Diff 비교 Tool (대기 판단용)

| # | Tool 이름 | 동작 | 입력 | 출력 | 상태 |
|---|---|---|---|---|---|
| D1 | `diff_compare` | 두 이미지(화면)의 차이 분석 | before, after | 변화 여부/비율 | `[ ]` Todo |
