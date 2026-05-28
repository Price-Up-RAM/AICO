# BAReader Unity 연동 가이드

## 개요

BAReader는 게임 스토리를 자동으로 읽어주는 시나리오입니다.
- **푸른 역삼각(▽)** 감지 → 대사 OCR → 음성 합성 → 재생 → 클릭
- **シナリオリスト** 감지 → 종료

---

## 엔드포인트

### 1. `POST /vl_agent/engine_stream`
**역할**: 화면 분석 + 액션 결정

**Request**:
```json
{
  "image": <png file>,
  "scenario_name": "BAReader",
  "agent_state": {
    "expected_state": ["S1", "S10"],
    "remain_retry_count": 5,
    "ocr_history": [
      {
        "type": "dialogue_with_actor",
        "actor": "新素材開発部員A",
        "txt": "やっ、アリス。"
      }
    ]
  }
}
```

**Response** (JSON 스트리밍):
```json
{
  "kind": "act",
  "message": "S1 대사 읽기...",
  "data": {
    "action": "click",
    "x": 1234,
    "y": 567,
    "request_voice": true,
    "voice_actor": "新素材開発部員A",
    "voice_txt": "やっ、アリス。",
    "dialogue_type": "dialogue_with_actor",
    "ocr_result": {
      "actor": "新素材開発部員A",
      "txt": "やっ、アリス。"
    },
    "choices": null,
    "agent_state": {
      "expected_state": ["S1", "S10"],
      "remain_retry_count": 5,
      "ocr_history": [ ... 업데이트된 히스토리 ... ]
    }
  }
}
```

### 2. `POST /vl_agent/engine_form`
**역할**: 음성 합성 + wav 반환

**Request**:
```
POST /vl_agent/engine_form
Content-Type: multipart/form-data

actor=新素材開発部員A
txt=やっ、アリス。
lang=ja
speed=1.0
```

> **Note**: `actor`가 비어있거나 전송하지 않으면 자동으로 `"arona"`로 기본값 처리됩니다.

**Response**:
```
Content-Type: audio/wav
X-Audio-Duration: 3.7

<wav binary data>
```

---

## 구현 시퀀스

### 일반 대사/선택지 (request_form)

```
[1] Unity → engine_stream
    - 스크린샷 전송
    - agent_state 전송 (ocr_history 포함)

[2] Python → Unity: AgentEvent (kind="act")
    - action: "request_form"  ← NEW!
    - voice_actor, voice_txt 포함
    - x, y 좌표 포함
    - agent_state.ocr_history 업데이트됨

[3] Unity → engine_form
    - voice_actor, voice_txt로 요청
    - wav + X-Audio-Duration 헤더 수신

[4] Unity: 음성 재생
    - duration만큼 대기 (예: 3.7초)

[5] Unity: 클릭
    - (x, y) 좌표 클릭

[6] 반복 → [1]로 돌아감
```

### 나레이션/None (observe만)

```
[1] Unity → engine_stream
    - 스크린샷 전송

[2] Python → Unity: AgentEvent (kind="observe")
    - narration 또는 none 감지
    - retry_interval: 2.0~3.0초
    - 음성 요청 없음, 클릭 없음

[3] Unity: 대기
    - retry_interval만큼 대기 (화면 변화 기다림)

[4] 반복 → [1]로 돌아감
```

### 중복 OCR / 템플릿 실패 (observe만)

```
[1] Unity → engine_stream
    - 스크린샷 전송

[2] Python → Unity: AgentEvent (kind="observe")
    - 중복 감지 또는 템플릿 매칭 실패
    - retry_interval: 1.0~2.0초

[3] Unity: 대기 + 재시도
    - retry_interval만큼 대기 후 [1]로

[4] 반복 → [1]로 돌아감
```

---

## Unity 구현 체크리스트

### 1. engine_stream 응답 처리

```csharp
// engine_stream 응답 파싱
var kind = eventData["kind"];
var data = eventData["data"];

if (kind == "act")
{
    string action = data["action"];
    
    if (action == "request_form")  // NEW!
    {
        // 음성 요청 + 클릭
        string actor = data["voice_actor"];
        string txt = data["voice_txt"];
        int x = data["x"];
        int y = data["y"];
        
        // engine_form 호출 → 음성 재생 → 클릭
        StartCoroutine(RequestVoiceAndClick(actor, txt, x, y));
    }
    else if (action == "click")
    {
        // 단순 클릭 (음성 없음)
        int x = data["x"];
        int y = data["y"];
        SimulateClick(x, y);
    }
}
else if (kind == "observe")
{
    // 대기 (narration, none, 중복, 템플릿 실패)
    float retryInterval = data["retry_interval"];
    yield return new WaitForSeconds(retryInterval);
    
    // 다시 engine_stream 호출
    StartCoroutine(CallEngineStream());
}
else if (kind == "done")
{
    // 시나리오 종료
    Debug.Log("BAReader 종료: " + data["reason"]);
}

// agent_state 저장 (다음 요청에 사용)
currentAgentState = data["agent_state"];
```

### 2. engine_form 호출 + 클릭

```csharp
IEnumerator RequestVoiceAndClick(string actor, string txt, int x, int y)
{
    WWWForm form = new WWWForm();
    form.AddField("actor", actor);
    form.AddField("txt", txt);
    form.AddField("lang", "ja");
    form.AddField("speed", "1.0");
    
    UnityWebRequest request = UnityWebRequest.Post(
        "http://localhost:5000/vl_agent/engine_form", 
        form
    );
    
    DownloadHandlerAudioClip audioHandler = new DownloadHandlerAudioClip(
        "", 
        AudioType.WAV
    );
    request.downloadHandler = audioHandler;
    
    yield return request.SendWebRequest();
    
    if (request.result == UnityWebRequest.Result.Success)
    {
        // 헤더에서 duration 추출
        string durationStr = request.GetResponseHeader("X-Audio-Duration");
        float duration = float.Parse(durationStr);
        
        // AudioClip 재생
        AudioClip clip = audioHandler.audioClip;
        audioSource.PlayOneShot(clip);
        
        // duration만큼 대기
        yield return new WaitForSeconds(duration);
        
        // 음성 재생 완료 후 클릭
        SimulateClick(x, y);
    }
    else
    {
        Debug.LogError("engine_form 실패: " + request.error);
    }
}
```

### 3. ocr_history 관리

```csharp
// engine_stream 요청 시
WWWForm form = new WWWForm();
form.AddBinaryData("image", pngBytes);
form.AddField("scenario_name", "BAReader");

// agent_state를 JSON으로 직렬화하여 전송
string agentStateJson = JsonUtility.ToJson(currentAgentState);
form.AddField("agent_state", agentStateJson);

// ... 요청 전송
```

```csharp
// engine_stream 응답 시
var responseData = JsonUtility.FromJson<AgentEventData>(responseJson);

// 업데이트된 agent_state 저장 (ocr_history 포함)
currentAgentState = responseData.data.agent_state;

// 다음 요청 시 이 agent_state를 다시 전송
```

### 4. 종료 조건 처리

```csharp
// S10 (종료) 감지
if (eventData["kind"] == "done")
{
    string message = eventData["message"];
    
    if (message.Contains("シナリオリスト"))
    {
        Debug.Log("BAReader 시나리오 종료");
        // 자동 읽기 모드 종료
        StopAutoRead();
    }
}
```

---

## dialogue_type 종류

| 타입 | 설명 | ocr_result 예시 |
|------|------|-----------------|
| `dialogue_with_actor` | 화자 있는 대사 | `{"actor": "アリス", "txt": "..."}` |
| `dialogue_no_actor` | 화자 없는 대사 | `{"actor": "", "txt": "..."}` |
| `choice` | 선택지 | `{"actor": "", "txt": "...", "choices": ["やあ。", "何してる？"]}` |
| `narration` | 나레이션 | `{"actor": "", "txt": "..."}` |
| `none` | 텍스트 없음 | 스킵 |

---

## 선택지 처리 (선택사항)

선택지 화면일 경우 `choices` 배열이 포함됩니다:

```json
{
  "dialogue_type": "choice",
  "choices": ["やあ。", "何してる？"],
  "ocr_result": {
    "actor": "",
    "txt": "やあ。"
  }
}
```

**Unity 처리**:
- 선택지 UI 표시 (선택사항)
- 사용자 선택 대기 또는 자동 선택
- 선택 후 `txt`를 음성 재생

---

## agent_state 구조

```json
{
  "expected_state": ["S1", "S10"],
  "remain_retry_count": 5,
  "retry_interval": 2.0,
  "identify_fail_count": 0,
  "ocr_history": [
    {
      "type": "dialogue_with_actor",
      "actor": "新素材開発部員A",
      "txt": "やっ、アリス。"
    },
    {
      "type": "choice",
      "actor": "",
      "txt": "やあ。",
      "choices": ["やあ。", "何してる？"]
    }
  ]
}
```

**중요**: `ocr_history`는 **stateless**로 관리됩니다.
- Unity → Python: 전체 히스토리 전송
- Python → Unity: 업데이트된 전체 히스토리 반환

---

## 에러 처리

### 시나리오 식별 실패 (5회)
```json
{
  "kind": "done",
  "message": "시나리오 식별 연속 실패 - 알림 요청",
  "data": {
    "request_type": "function_request_play_sfx_alert",
    "reply_list": [
      {
        "answer_ko": "확인이 필요한 사항이 생겼어요, 선생님.",
        "answer_jp": "確認が必要なことがあります、先生。",
        "answer_en": "Something needs your attention, Sensei."
      }
    ]
  }
}
```

**Unity 처리**: 알림 음성 재생 + 자동 모드 중지

---

## 테스트 시나리오

### 1. 기본 대사 읽기
```
입력: 역삼각이 있는 대사 화면
출력: request_voice=true, voice_txt="..."
동작: 음성 재생 → 2초 대기 → 클릭
```

### 2. 선택지 처리
```
입력: 선택지 2개 화면
출력: choices=["やあ。", "何してる？"]
동작: 첫 번째 선택지 음성 재생 → 클릭
```

### 3. 종료 조건
```
입력: "シナリオリスト" 키워드 화면
출력: kind="done"
동작: 자동 읽기 모드 종료
```

---

## FAQ

**Q1. `request_voice`가 `false`인 경우가 있나요?**  
A. 네, OCR 결과가 없거나 `dialogue_type`이 `none`인 경우 `false`입니다. 이 경우 음성 재생 건너뛰고 바로 클릭합니다.

**Q2. `ocr_history`는 언제 초기화하나요?**  
A. 시나리오 시작 시 빈 배열로 초기화하고, 종료 시까지 누적합니다.

**Q3. `X-Audio-Duration`이 없으면요?**  
A. Fallback으로 5초를 사용하거나, AudioClip.length를 사용하세요.

**Q4. 음성 합성 실패 시 처리는?**  
A. `engine_form`이 500 에러를 반환합니다. 이 경우 음성 없이 클릭만 수행하거나, 재시도 로직을 구현하세요.

**Q5. `actor`가 비어있으면 어떻게 되나요?**  
A. 서버에서 자동으로 `"arona"`로 기본값 처리됩니다. `dialogue_no_actor` 타입이나 나레이션의 경우 actor가 빈 문자열로 오는데, 이 경우 arona 목소리로 읽힙니다.

---

## 추가 리소스

- Python 구현 상세: `plan/vl_engine2/plan.md`
- 전체 구현 내역: `plan/vl_engine2/walkthrough.md`
