# 배경

현재 무분별한 Tool과 Agent 제작 후 관리를 하지 않아서 만들고 사용하는 유저나 개발자도 파악을 하지 못하고 있음


## 현황 파악

현재 프로젝트 내에 산재되어 있는 에이전트, 기능(Function), 특수 목적 엔진 등을 분류하면 다음과 같습니다.

### 1. 단발성 텍스트 생성 에이전트 (Text-only Single-shot Agents)
단일 프롬프트로 텍스트나 데이터를 생성하는 역할을 합니다.
*   **`ai_alarm_maker`**: 알람 문장 생성 (`function_request_make_alarm`)
*   **`ai_alarm_counter`**: 알람 시간 오프셋을 JSON 형태로 추출 (`function_request_alarm_counter`)

### 2. 단발성 멀티모달 (VL) 에이전트 (Multimodal Single-shot Agents)
이미지와 텍스트 프롬프트를 함께 사용하여 분석 및 생성하는 역할을 합니다.
*   **`ai_vl_problem_solver`**: 이미지 속 문제(수학, 퀴즈 등)를 풀이 (`function_request_solve_problem`)
*   *(기타 구상 중인 기능)*: `ai_vl_ocr_reader`, `ai_vl_scene_describer` 등 (`agent_vl_functions.md` 참고)

### 3. 클라이언트(Unity) 제어 및 요청 기능 (Unity Request Functions)
서버에서 클라이언트로 특정 행동을 지시하는 기능들입니다.
*   `function_request_frame`: 새 화면 캡처 요청
*   `function_request_click`: 지정 좌표 클릭 수행
*   `function_request_play_sfx_alert`: 알림 음성 재생
*   `function_request_dance`: 캐릭터 댄스 요청
*   `function_request_screenshot`: 전체 스크린샷 저장

### 4. 비전 기반 요소 탐지 기능 (VL Detection Functions)
화면 내의 특정 요소를 찾는 기능입니다.
*   `function_vl_grounding`: 화면에서 텍스트 기반 대상 좌표 탐색
*   `function_vl_keyword_detect`: UI 키워드 존재 여부 탐지
*   `function_vl_prompt_call`: 커스텀 프롬프트로 VL 모델 단발 호출

### 5. 특수 목적 엔진 (Scenario Engines)
특정 게임 내 시나리오(블루아카이브 등)를 자동화하거나 읽어주는 고정된 파이프라인(엔진)입니다.
*   **BAReader (`vl_engine2/plan2.md`)**: 화면의 대사를 OCR로 읽고 TTS로 음성을 재생하며 클릭으로 넘기는 시나리오
*   **BASkip**: 스토리 등을 자동으로 스킵하는 시나리오
*   *(관련 API)*: `/vl_agent/engine_stream`, `/vl_agent/engine_form`

### 6. 범용 시각 인지 및 계획 에이전트 (VL Planner)
특정 목적 달성을 위해 루프(Loop)를 돌며 관찰, 사고, 행동을 반복하는 자율형 에이전트 설계입니다 (`VL_Agent_Final.md`).
*   **과정**: Goal/Success 정의 -> 프레임 관찰 -> Think Log 누적 -> Function(`grounding`, `request_frame`, `request_click` 등) 호출 반복.
*   *(관련 API)*: `/vl_agent/run`

---

## Router 정리를 위한 선별 (예비 단계)

최종적으로 업무의 경중을 판단하고 적절한 Agent나 Tool로 분배해주는 **Router** 관점에서, 이 기능들을 어떻게 묶고 노출할지 기준이 필요합니다.

1. **Router가 직접 호출할 수 있어야 하는 (노출형) 도구**:
    *   `function_request_solve_problem` (문제 풀이 요청 시)
    *   `function_request_make_alarm`, `function_request_alarm_counter` (사용자 알람 설정 시)
    *   `function_request_dance`, `function_request_play_sfx_alert`, `function_request_screenshot` (사용자가 춤, 효과음 재생, 스크린샷 캡처 등 클라이언트 직접 제어를 요청할 때)
2. **복합적인 태스크 수행을 위해 Router가 위임해야 하는 에이전트**:
    *   **VL Planner (`/vl_agent/run`)**: 사용자가 "화면에서 뭔가 찾아서 클릭해줘" 와 같은 자율적 동작을 요구할 때 위임
    *   **Scenario Engine (`/vl_agent/engine_stream`)**: 사용자가 "스토리 읽어줘(BAReader)", "스킵해줘(BASkip)" 같은 명확한 목적의 시나리오를 요구할 때 위임
3. **Router가 아닌, 하위 에이전트 전용으로 쓰이는 도구 (은닉형)**:
    *   `function_vl_grounding`, `function_request_frame`, `function_request_click` 등은 VL Planner나 Engine이 내부적으로 사용하는 원시 도구(Primitive Tools)이므로 Router가 굳이 직접 알아야 할 필요가 낮음.
