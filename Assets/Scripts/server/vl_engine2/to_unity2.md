# BAReader Phase 1 Unity 연동 변경사항

> **Phase 1에서 추가된 내용만 요약**

---

## 1. 새로운 Action Type: `request_form` ✨

**이전**: `action: "click"` + `request_voice: true`

**변경**: `action: "request_form"`

### AgentEvent 구조

```json
{
  "kind": "act",
  "data": {
    "action": "request_form",  // NEW!
    "x": 1234,
    "y": 567,
    "voice_actor": "新素材開発部員A",
    "voice_txt": "やっ、アリス。",
    "dialogue_type": "dialogue_with_actor",
    "ocr_result": {...},
    "choices": null,
    "agent_state": {...}
  }
}
```

### Unity 처리

```csharp
if (kind == "act" && data["action"] == "request_form")
{
    string actor = data["voice_actor"];
    string txt = data["voice_txt"];
    int x = data["x"];
    int y = data["y"];
    
    // engine_form 호출 → 음성 재생 → 클릭
    StartCoroutine(RequestVoiceAndClick(actor, txt, x, y));
}
```

---

## 2. engine_form 파라미터 추가 ✨

### 기존 파라미터
```
actor=新素材開発部員A
txt=やっ、アリス。
lang=ja
speed=1.0
```

### 신규 파라미터
```
verbose=true                          ← DevManager 상태 반영
ocr_history_json={"history":[...]}    ← agent_state.ocr_history를 JSON으로
```

**verbose**:
- 타입: string (`"true"` / `"false"`)
- 출처: `DevManager.Instance.IsDevModeEnabled()`
- 동작: `verbose=true`일 때 서버가 `./test/vl_agent/ocr_history_YYYYMMDD_HHMMSS.txt` 생성

**ocr_history_json**:
- 타입: string (JSON 인코딩)
- 출처: `agent_state["ocr_history"]`를 JSON 직렬화
- 예시: `{"history": [{"type":"dialogue","actor":"アリス","txt":"やあ。"}]}`
- 사용: verbose=true일 때만 서버에서 파싱하여 로그 파일에 기록

### Unity 구현 예시

```csharp
IEnumerator RequestVoiceAndClick(string actor, string txt, int x, int y)
{
    WWWForm form = new WWWForm();
    form.AddField("actor", actor);
    form.AddField("txt", txt);
    form.AddField("lang", "ja");
    form.AddField("speed", sound_speedMaster / 100f);  // 100 → 1.0 변환
    
    // NEW! verbose 파라미터
    bool verbose = DevManager.Instance.IsDevModeEnabled();
    form.AddField("verbose", verbose ? "true" : "false");
    
    // NEW! ocr_history_json 파라미터
    if (verbose && currentAgentState.ContainsKey("ocr_history"))
    {
        string json = JsonUtility.ToJson(new { history = currentAgentState["ocr_history"] });
        form.AddField("ocr_history_json", json);
    }
    
    // ... (기존 코드)
}
```

---

## 3. 새로운 Observe 케이스 4가지 🔄

### 2-1. Narration (나레이션)

**응답**:
```json
{
  "kind": "observe",
  "data": {
    "reason": "나레이션 - 자동 전환 대기: ...",
    "retry_interval": 3.0,
    "agent_state": {...}
  }
}
```

**특징**: 음성 없음, 클릭 없음, 3초 대기

---

### 2-2. None (빈 화면)

**응답**:
```json
{
  "kind": "observe",
  "data": {
    "reason": "dialogue_type=none - 화면 전환 대기",
    "retry_interval": 2.0,
    "agent_state": {...}
  }
}
```

**특징**: 텍스트 없는 화면, 2초 대기

---

### 2-3. 중복 OCR

**응답**:
```json
{
  "kind": "observe",
  "data": {
    "reason": "중복 OCR - 대기: ...",
    "retry_interval": 2.0,
    "agent_state": {...}
  }
}
```

**특징**: 같은 대사 반복 방지, 2초 대기

---

### 2-4. 템플릿 매칭 실패

**응답**:
```json
{
  "kind": "observe",
  "data": {
    "reason": "선택지/삼각형 재감지 실패 - 재시도",
    "retry_interval": 1.0,
    "agent_state": {...}
  }
}
```

**특징**: 역삼각/선택지 박스 둘 다 감지 실패, 1초 재시도

---

## 4. Unity 구현 통합 예시

```csharp
// engine_stream 응답 처리
var kind = eventData["kind"];
var data = eventData["data"];

if (kind == "act")
{
    string action = data["action"];
    
    if (action == "request_form")
    {
        // 음성 요청 + 클릭
        StartCoroutine(RequestVoiceAndClick(
            data["voice_actor"], 
            data["voice_txt"],
            data["x"], 
            data["y"]
        ));
    }
    else if (action == "click")
    {
        // 단순 클릭만
        SimulateClick(data["x"], data["y"]);
    }
}
else if (kind == "observe")
{
    // 대기 후 재호출
    float retryInterval = data["retry_interval"];
    yield return new WaitForSeconds(retryInterval);
    StartCoroutine(CallEngineStream());
}
else if (kind == "done")
{
    Debug.Log("BAReader 종료: " + data["reason"]);
}

// agent_state 저장
currentAgentState = data["agent_state"];
```

---

## 5. 대기 시나리오 요약

| 케이스 | retry_interval | 음성 | 클릭 |
|--------|----------------|------|------|
| narration | 3.0초 | ❌ | ❌ |
| none | 2.0초 | ❌ | ❌ |
| 중복 OCR | 2.0초 | ❌ | ❌ |
| 템플릿 실패 | 1.0초 | ❌ | ❌ |

**공통**: 모든 observe는 `retry_interval` 대기 후 `engine_stream` 재호출

---

## 6. 기존 코드와의 호환성

**삭제 필요**:
- ✅ `request_voice: true` 플래그 체크 로직

**유지 가능**:
- ✅ `action: "click"` 처리 (일부 케이스에서 여전히 사용)
- ✅ `kind: "done"`, `kind: "fail"` 처리
- ✅ `agent_state` 관리

---

## 요약

**추가 구현 필요**:
1. `request_form` action 처리
2. `observe` 케이스별 retry_interval 대기
3. observe 후 재호출 로직

**제거 필요**:
1. `request_voice` 플래그 체크

**변경 없음**:
1. `engine_form` 엔드포인트 호출 방식
2. `agent_state` 관리 방식
