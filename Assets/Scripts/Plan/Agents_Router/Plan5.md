# Plan5: 통합 라우터(Router) 구현 설계서

본 문서는 기존 `server_interface.py`에 파편화되어 있던 Intent 분류기를 대체하고, Primitive Tool과 Markdown Skill을 모두 포괄하여 처리할 수 있는 **통합 라우터(Integrated Router)의 코드 구현 레벨 설계도**입니다.

---

## 1. 라우팅 파이프라인 (The Routing Pipeline)

요청이 들어왔을 때 라우터가 처리하는 3단계 핵심 프로세스입니다.

### Phase 1: Discovery (무기 탐색)
라우터는 무작정 판단을 내리기 전에, 현재 자신이 사용할 수 있는 패(Tool과 Skill)가 무엇인지 탐색합니다.
*   `router_discovery.py`의 헬퍼 함수를 실행합니다.
    1.  `get_available_tools()`: 파이썬 내부/유니티 요청용 Primitive Tool들의 이름과 한 줄 요약을 가져옵니다.
    2.  `get_available_skills()`: 로컬 `skills/` 디렉토리를 순회하며 모든 `.md` 파일의 YAML Frontmatter(이름, description, triggers)만 파싱해옵니다.

### Phase 2: LLM Judgment (의미론적 매칭)
가벼운 추론 전용 LLM(또는 메인 LLM의 단발성 호출)에게 유저의 요청과 탐색된 목록을 던져주어 판단을 내립니다.
*   **프롬프트 구조**: 
    > "유저의 요청: [스토리 넘겨줘]"
    > "사용 가능 Primitive Tools: [vl_grounding, request_click, alarm_maker ...]"
    > "사용 가능 Skills: [스토리 스킵 스킬, 우편함 수령 스킬 ...]"
    > "가장 적합한 단일 도구(또는 스킬)를 선택해라. 없으면 'None'을 반환해라."
*   **결과(Match)**: `Skill:스토리 고속 스킵` 또는 `Tool:request_dance` 또는 `None`

### Phase 3: Dispatch & Execution (실행 및 주입)
판단 결과에 따라 실제 실행 흐름을 분기합니다.
*   **Primitive Tool 매칭 시**: 
    -   라우터가 직접 해당 파이썬 함수를 호출하여 단일 실행(Single-shot)하고 결과를 유저에게 반환합니다.
*   **Skill 매칭 시 (On-Demand Loading)**: 
    -   해당 스킬의 마크다운 파일(`skill_skip_story.md`) 본문을 통째로 읽어옵니다.
    -   중앙 제어(VL Planner 또는 Orchestrator)의 시스템 프롬프트 하단에 이 마크다운 본문을 주입(Inject)합니다.
    -   중앙 제어 루프를 가동시킵니다.
*   **None 매칭 시 (Fallback)**: 
    -   어떤 툴도 실행하지 않습니다.
    -   일반 대화 모델에게 넘겨 "그건 제가 할 수 없는 일입니다" 류의 일상적인 텍스트 응답만 생성하여 반환합니다. (스킬 제작 제안 절대 금지)

---

## 2. 코드 모듈 구조 제안 (Python)

기존 코드를 건드리지 않고, 새로운 라우터 아키텍처를 점진적으로 이식하기 위한 모듈 분리 제안입니다.

### `ai_router_main.py`
*   라우터의 진입점(Entry Point).
*   기존 `/conversation_stream` 등에서 job이 들어왔을 때 가장 먼저 호출되는 메인 컨트롤러.
*   내부적으로 Phase 1 -> Phase 2 -> Phase 3의 파이프라인을 순차적으로 호출하는 뼈대 함수 `process_request(query, context)`를 가집니다.

### `ai_router_discovery.py`
*   파일 시스템 입출력을 담당하는 헬퍼 모듈.
*   `def get_available_skills()`: `skills/*.md`를 읽어 Frontmatter를 JSON 리스트로 묶어 반환.
*   `def get_available_tools()`: 하드코딩된 또는 등록된 파이썬 함수(Primitive)들의 명세 반환.

### `ai_router_judge.py`
*   LLM 프롬프팅 모듈.
*   오로지 "요청과 목록을 비교하여 정답(매칭 대상) 하나를 출력"하는 데 최적화된 프롬프트를 보관하고, LLM API(Gemini/Local)를 호출해 파싱된 결과값만 넘깁니다.

### `ai_router_dispatcher.py`
*   판단 결과를 바탕으로 실제로 툴을 실행시키거나, 중앙 제어(Orchestrator) 객체를 생성하고 마크다운을 주입하는 역할.
*   Primitive Tool 실행 중 발생하는 에러나, 중앙 제어 루프 정료 후의 최종 결과를 취합하여 `router_main`으로 올려보냅니다.

---

## 3. 유저 피드백 요청 구역

> 이 문서는 기존 `server_interface.py`에 엉켜있는 Intent 로직을 완전히 현대적인 **시맨틱 라우터(Semantic Router)** 구조로 탈바꿈하기 위한 제안입니다.

**[리뷰 포인트]**
1. 이 모듈 구조(`router_main`, `discovery`, `judge`, `dispatcher`)가 기존 코드베이스에 추가되기에 적절해 보이시나요?
2. Phase 2의 LLM Judgment에서, 기존처럼 로컬 모델(GGUF)이나 가벼운 Gemini Flash 모델을 라우팅 전용으로 쓰는 방식에 동의하시나요?
3. 라우터가 Primitive Tool과 Skill을 구분 없이 통합해서 판단하는 이 파이프라인 흐름이 직관적이신가요?
