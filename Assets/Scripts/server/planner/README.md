# TOBE: VL Planner

Vision-Language Planner 엔진 - VL Agent 아키텍처 기반

## 파일 구조

```
TOBE/
├── __init__.py
├── ai_vl_planner.py              # 메인 플래너 루프
├── ai_vl_planner_llm.py          # LLM 호출 인터페이스
├── ai_vl_planner_prompts.py     # 프롬프트 템플릿
└── server_interface_vl_planner_impl.py  # Flask 엔드포인트
```

## 주요 함수

### ai_vl_planner.py

- `ai_vl_planner_run_loop()` - 메인 플래너 루프, AgentEvent 생성
- `append_think_log()` - think_log 추가
- `restore_think_log()` - think_log 복원
- `extract_goal_from_think_log()` - Goal 추출
- `extract_success_from_think_log()` - Success Signal 추출
- `extract_query_from_think_log()` - 원본 쿼리 추출
- `extract_last_grounding_keyword()` - 마지막 Grounding 키워드 추출
- `extract_final_action_from_think_log()` - 최종 액션 추출

### ai_vl_planner_llm.py

- `ai_vl_planner_infer_goal_and_success()` - Goal/Success Signal 추론
- `ai_vl_planner_decide_next_step()` - 다음 스텝 결정
- `ai_vl_planner_check_success()` - 성공 여부 체크
- `call_llm()` - LLM 호출 공통 함수

## API 엔드포인트

### POST /vl_agent/run
VL Planner 실행 (스트리밍)

**Request (multipart/form-data):**
- `image`: 화면 캡처 이미지 (필수)
- `query`: 사용자 쿼리 (첫 요청 시 필수)
- `memory`: 대화 기록 JSON (선택)
- `think_log`: 재요청용 think_log JSON (재요청 시 필수)
- `retry_count`: 현재 재요청 횟수 (선택, 기본 0)
- `is_canceled`: 취소 여부 (선택, 기본 false)
- `verbose`: 디버그 모드 (선택, 기본 false)

**Response (Streaming JSON Lines):**
각 라인은 JSON 객체로 AgentEvent를 전달

### GET /vl_agent/health
헬스 체크

## 서버 통합

```python
from TOBE.server_interface_vl_planner_impl import app

# 또는 Blueprint로 통합
from flask import Flask
from TOBE.server_interface_vl_planner_impl import app as vl_planner_app

main_app = Flask(__name__)
# vl_planner_app의 라우트를 main_app에 추가
```

## 특징

- **Stateless**: think_log로 상태 관리 및 복원
- **재요청 지원**: Unity에서 새 프레임으로 재시도 가능
- **Vision-Language**: VL 모델 사용 (Grounding, 화면 분석)
- **Goal-Oriented**: Goal과 Success Signal 기반 작업 수행
- **스트리밍**: 실시간 이벤트 전송

## Flow 다이어그램

### 전체 흐름

```
Unity Client
    ↓
POST /vl_agent/run (multipart/form-data)
    ↓
ai_vl_planner_run_loop(query, memory, initial_frame, resume_think_log, retry_count, max_retry, is_canceled, max_iters)
    ↓
├─ [취소 요청] is_canceled=true
│   └─ EVENT_KIND_FAIL (작업 취소됨)
│
├─ [최대 재시도] retry_count >= max_retry
│   └─ EVENT_KIND_MAX_RETRY_REACHED
│
├─ [재요청] resume_think_log 존재
│   ↓
│   ├─ restore_think_log() → 상태 복원
│   │   └─ goal, success_signal, final_action, original_query, current_grounding_keyword
│   ↓
│   ├─ EVENT_KIND_OBSERVE (새 프레임 수신)
│   ↓
│   ├─ ai_vl_planner_check_success(success_signal, frame_summary, last_function_results)
│   │   └─ ai_vl_planner_llm.py → call_llm()
│   │       └─ ai_vl_planner_prompts.py (PROMPT_CHECK_SUCCESS)
│   ↓
│   ├─ EVENT_KIND_CHECK (성공 조건 확인)
│   ↓
│   └─ [성공] is_done=true
│       └─ EVENT_KIND_DONE (작업 완료)
│
└─ [첫 요청] resume_think_log 없음
    ↓
    ├─ ai_vl_planner_infer_goal_and_success(query, memory, lang)
    │   └─ ai_vl_planner_llm.py → call_llm()
    │       └─ ai_vl_planner_prompts.py (PROMPT_GOAL_SIGNAL)
    │   └─ goal, success_signal, final_action 추출
    ↓
    ├─ EVENT_KIND_GOAL (목표 설정 완료)
    ↓
    └─ [메인 루프] (max_iters만큼 반복)
        ↓
        ├─ ai_vl_planner_decide_next_step(goal, success_signal, frame_summary, last_function_results)
        │   └─ ai_vl_planner_llm.py → call_llm()
        │       └─ ai_vl_planner_prompts.py (PROMPT_DECIDE_NEXT_STEP)
        │   └─ action, reason, extra_args 추출
        ↓
        ├─ [DONE] 작업 완료
        │   └─ EVENT_KIND_DONE
        ↓
        ├─ [FAIL] 실패
        │   └─ EVENT_KIND_FAIL
        ↓
        ├─ [WAIT] 대기
        │   └─ EVENT_KIND_WAIT
        ↓
        └─ [CALL_FUNCTION] 함수 호출
            ↓
            ├─ FUNC_VL_TARGET_FIND (Grounding)
            │   └─ EVENT_KIND_ACT (target_text)
            │       └─ call_vl_function() → 화면에서 타겟 찾기
            │           └─ 결과: {x, y, ...}
            ↓
            ├─ FUNC_REQUEST_CLICK (클릭)
            │   └─ EVENT_KIND_ACT (x, y)
            │       └─ call_vl_function() → Unity에 클릭 요청
            │           └─ 새 프레임 필요 → Unity가 재요청
            ↓
            ├─ FUNC_REQUEST_FRAME (프레임 요청)
            │   └─ EVENT_KIND_ACT
            │       └─ Unity에 새 프레임 요청
            ↓
            └─ [기타 함수]
                ├─ FUNC_REQUEST_DANCE
                ├─ FUNC_REQUEST_PLAY_SFX_ALERT
                └─ FUNC_REQUEST_SCREENSHOT
```

### Unity 연동 흐름

```
Unity 클라이언트
    ↓
[1] 첫 요청
    ├─ query: "가방 열어줘"
    ├─ image: 현재 화면 캡처
    ├─ memory: [] (선택)
    └─ retry_count: 0
    ↓
[2] Flask 서버 응답 (스트리밍)
    ├─ EVENT_KIND_GOAL: goal="가방 열기", success_signal="[VERIFY] 가방 UI 표시"
    ├─ EVENT_KIND_PLAN: 다음 액션 결정
    ├─ EVENT_KIND_ACT: GROUNDING(target="가방 버튼")
    ├─ EVENT_KIND_ACT: CLICK(x=100, y=200)
    └─ EVENT_KIND_WAIT: 새 프레임 필요
    ↓
[3] Unity가 새 프레임으로 재요청
    ├─ image: 클릭 후 화면 캡처 (새)
    ├─ think_log: 이전 응답의 think_log (전체)
    ├─ retry_count: 1
    └─ query: (없음)
    ↓
[4] Flask 서버 응답
    ├─ EVENT_KIND_OBSERVE: 새 프레임 수신
    ├─ EVENT_KIND_CHECK: 성공 조건 확인
    └─ [성공 시] EVENT_KIND_DONE: 작업 완료
    └─ [실패 시] 메인 루프 계속 (다른 액션 시도)
    ↓
[5] 최대 재시도 도달 시
    └─ EVENT_KIND_MAX_RETRY_REACHED (retry_count >= max_retry)
```

### 주요 파라미터

**ai_vl_planner_run_loop 입력:**
- `query`: 사용자 쿼리 (첫 요청 시 필수)
- `memory`: 대화 기록 (첫 요청 시 사용)
- `initial_frame`: 현재 화면 캡처 경로
- `resume_think_log`: 재요청 시 이전 think_log (Stateless 핵심)
- `retry_count`: 현재까지 재요청 횟수 (Unity가 관리)
- `max_retry`: 최대 재요청 허용 횟수 (기본 5)
- `is_canceled`: 취소 요청 여부
- `max_iters`: 루프 내 최대 반복 횟수 (기본 5)

**ai_vl_planner_run_loop 출력:**
- `AgentEvent` Generator (kind, message, think_log, data 포함)

**think_log 구조:**
- `idx`: 인덱스
- `phase`: PHASE_GOAL/SIGNAL/OBSERVE/PLAN/ACT/CHECK/WAIT
- `content`: 내용
- `timestamp`: 타임스탬프

**Goal/Success Signal:**
- `goal`: 사용자의 최종 목표 (한 문장)
- `success_signal`: 목표 달성 검증 조건
  - `[ONE_SHOT]`: 화면으로 검증 불가, 최종 액션 후 완료
  - `[VERIFY]`: 화면으로 검증 가능, 액션 후 확인
- `final_action`: 목표를 완료하는 함수 (선택)

### LLM 호출 계층

```
ai_vl_planner.py (플래너 로직)
    ↓
ai_vl_planner_llm.py (LLM 인터페이스)
    ↓
call_llm(prompt, max_tokens)
    ↓
ai_singleton.get_llm(require_vl=True)
    └─ Vision-Language 모델
    ↓
ai_vl_planner_prompts.py (프롬프트 템플릿)
    ├─ PROMPT_GOAL_SIGNAL → Goal/Success Signal 추론
    ├─ PROMPT_DECIDE_NEXT_STEP → 다음 스텝 결정
    └─ PROMPT_CHECK_SUCCESS → 성공 여부 체크
```

### 이벤트 종류

- `EVENT_KIND_GOAL`: 목표 설정 완료
- `EVENT_KIND_OBSERVE`: 화면 관찰
- `EVENT_KIND_PLAN`: 다음 액션 계획
- `EVENT_KIND_ACT`: 액션 실행
- `EVENT_KIND_CHECK`: 성공 조건 확인
- `EVENT_KIND_WAIT`: 대기 (새 프레임 필요)
- `EVENT_KIND_DONE`: 작업 완료
- `EVENT_KIND_FAIL`: 실패
- `EVENT_KIND_MAX_RETRY_REACHED`: 최대 재시도 도달

### 함수 종류

- `FUNC_VL_TARGET_FIND`: Grounding (화면에서 타겟 찾기)
- `FUNC_REQUEST_CLICK`: 클릭 요청
- `FUNC_REQUEST_FRAME`: 프레임 요청
- `FUNC_REQUEST_DANCE`: 춤 요청
- `FUNC_REQUEST_PLAY_SFX_ALERT`: 효과음 재생
- `FUNC_REQUEST_SCREENSHOT`: 스크린샷

## Unity 통합 가이드

### 1. 첫 요청 예시

```csharp
// Unity C# 예시
string serverUrl = "http://localhost:5000/vl_agent/run";
string query = "가방 열어줘";
byte[] screenshot = CaptureScreenshot();

WWWForm form = new WWWForm();
form.AddField("query", query);
form.AddBinaryData("image", screenshot, "frame.png", "image/png");
form.AddField("memory", JsonUtility.ToJson(memoryList)); // 선택
form.AddField("retry_count", "0");

UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
yield return request.SendWebRequest();

// 스트리밍 응답 처리
string[] lines = request.downloadHandler.text.Split('\n');
foreach (string line in lines) {
    if (string.IsNullOrEmpty(line)) continue;
    AgentEvent evt = JsonUtility.FromJson<AgentEvent>(line);
    HandleEvent(evt);
}
```

### 2. 재요청 예시 (새 프레임)

```csharp
// 이전 응답에서 think_log 저장
List<ThinkEntry> savedThinkLog = lastEvent.think_log;
int currentRetryCount = 1;

// 새 프레임으로 재요청
byte[] newScreenshot = CaptureScreenshot();

WWWForm form = new WWWForm();
form.AddBinaryData("image", newScreenshot, "frame.png", "image/png");
form.AddField("think_log", JsonUtility.ToJson(savedThinkLog)); // 중요!
form.AddField("retry_count", currentRetryCount.ToString());

UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
yield return request.SendWebRequest();

// 응답 처리...
```

### 3. 이벤트 처리

```csharp
void HandleEvent(AgentEvent evt) {
    switch (evt.kind) {
        case "goal":
            Debug.Log($"목표 설정: {evt.data.goal}");
            break;
        
        case "act":
            string func = evt.data.function_name;
            if (func == "REQUEST_CLICK") {
                int x = evt.data.x;
                int y = evt.data.y;
                SimulateClick(x, y);
                
                // 클릭 후 새 프레임으로 재요청 필요
                StartCoroutine(RetryWithNewFrame(evt.think_log));
            }
            break;
        
        case "wait":
            // 프레임 요청 대기
            StartCoroutine(RetryWithNewFrame(evt.think_log));
            break;
        
        case "done":
            Debug.Log("작업 완료!");
            ShowReply(evt.data.reply_list);
            break;
        
        case "fail":
            Debug.LogError($"실패: {evt.message}");
            break;
        
        case "max_retry_reached":
            Debug.LogWarning("최대 재시도 횟수 도달");
            break;
    }
}
```

### 4. 취소 요청

```csharp
WWWForm form = new WWWForm();
form.AddBinaryData("image", screenshot, "frame.png", "image/png");
form.AddField("think_log", JsonUtility.ToJson(savedThinkLog));
form.AddField("is_canceled", "true"); // 취소 플래그

UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
yield return request.SendWebRequest();
// 응답: EVENT_KIND_FAIL with reason="user_canceled"
```

## 상태 관리 (Stateless)

### think_log의 역할

VL Planner는 **완전히 Stateless**합니다. 모든 상태는 `think_log`에 저장되며, Unity가 이를 관리합니다.

**think_log에 저장되는 정보:**
- 사용자 쿼리 (original_query)
- Goal & Success Signal
- 모든 액션 히스토리
- Grounding 키워드
- 함수 호출 결과
- 성공 체크 결과

**재요청 시 복원:**
```python
# ai_vl_planner.py
if resume_think_log:
    think_log = restore_think_log(resume_think_log)
    goal_text = extract_goal_from_think_log(think_log)
    success_signal = extract_success_from_think_log(think_log)
    original_query = extract_query_from_think_log(think_log)
    current_grounding_keyword = extract_last_grounding_keyword(think_log)
    # 상태 완전 복원!
```

### Unity에서의 상태 관리

```csharp
public class VLPlannerState {
    public List<ThinkEntry> thinkLog;
    public int retryCount;
    public string lastQuery;
    public bool isWaitingForFrame;
    
    public void SaveFromEvent(AgentEvent evt) {
        thinkLog = evt.think_log;
        if (evt.kind == "wait" || evt.kind == "act") {
            isWaitingForFrame = true;
        }
    }
    
    public WWWForm CreateRetryRequest(byte[] newScreenshot) {
        WWWForm form = new WWWForm();
        form.AddBinaryData("image", newScreenshot, "frame.png", "image/png");
        form.AddField("think_log", JsonUtility.ToJson(thinkLog));
        form.AddField("retry_count", (++retryCount).ToString());
        return form;
    }
}
```

## 디버깅 팁

### 1. Verbose 모드

```csharp
form.AddField("verbose", "true");
```

서버 로그에 자세한 정보 출력

### 2. think_log 확인

```csharp
foreach (var entry in evt.think_log) {
    Debug.Log($"[{entry.phase}] {entry.content}");
}
```

### 3. LLM 응답 확인

서버 콘솔에서 LLM 호출 로그 확인:
```
[LLM] infer_goal_and_success
  prompt: <|im_start|>system...
  output: goal:가방 열기...
  parsed: goal=가방 열기, signal=[VERIFY] 가방 UI 표시...
```

## 성능 최적화

### 1. 이미지 압축

Unity에서 스크린샷 전송 시 PNG 압축 적용

### 2. think_log 압축

큰 think_log는 JSON 압축 고려

### 3. 재시도 제한

```csharp
const int MAX_RETRY = 3; // 서버 기본값: 5
```

### 4. 타임아웃 설정

```csharp
request.timeout = 30; // 30초
```
