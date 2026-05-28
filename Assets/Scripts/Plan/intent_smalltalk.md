# SmallTalk 고도화 구현 계획

## 개요
AI가 SmallTalk(잡담)을 생성한 후, 사용자가 관련 답변을 했는지 추적하고, 서버의 intent 판단을 통해 잡담 맥락을 메모리에 저장하는 시스템을 구현합니다.

## 핵심 변경사항

30초 이내 답변 전송

## 서버 응답 형식

### intent_info 파라미터

```json
{
  "is_intent_web": "off",
  "web_info": "",
  "is_intent_image": "off",
  "image_info": "",
  "is_intent_smalltalk_answer": "on",  // ← 이것 체크!
  "smalltalk_query": "선생님, 오늘 날씨가 좋네요!"
}
```

### 연관성 판단 예시

**Case 1: 연관성 있음**
- AI 잡담: "선생님, 오늘 날씨가 좋네요!"
- 사용자: "그러게요, 산책하기 좋아요"
- 결과: `is_intent_smalltalk_answer = "on"`

**Case 2: 연관성 없음**
- AI 잡담: "선생님, 오늘 날씨가 좋네요!"
- 사용자: "웹 검색해줘"
- 결과: `is_intent_smalltalk_answer = "off"`

## Unity 구현 세부사항

### 1. 트리거 변수 추가 (APIManager.cs)

**위치**: 클래스 멤버 변수 영역 (29번 줄 이후)

```csharp
// SmallTalk 트리거 관련
private string last_smalltalk = "";  // 마지막 SmallTalk 내용
private DateTime smalltalk_time = DateTime.MinValue;  // SmallTalk 응답 시간
private int chatOpenChance = 0;  // 채팅창 오픈 찬스 (최대 2회)
private DateTime lastChatOpenTime = DateTime.MinValue;  // 마지막 채팅창 오픈 시간
private bool wasChatting = false;  // 채팅창 상태 추적용
```

### 2. SmallTalk 응답 시 트리거 설정

**위치**: `CallSmallTalkStream()` 메서드 (320번 줄 블록)

```csharp
if (resolvedChatIdx == GameManager.Instance.chatIdxSuccess)
{
    if (currentMemoryType != "system")
    {
        OnFinalResponseReceived();
        
        // SmallTalk 트리거 설정
        string smalltalkText = string.Join(" ", replyListKo);
        if (SettingManager.Instance.settings.ui_language == "ja" || 
            SettingManager.Instance.settings.ui_language == "jp")
        {
            smalltalkText = string.Join(" ", replyListJp);
        }
        else if (SettingManager.Instance.settings.ui_language == "en")
        {
            smalltalkText = string.Join(" ", replyListEn);
        }
        
        if (!string.IsNullOrEmpty(smalltalkText))
        {
            last_smalltalk = smalltalkText;
            smalltalk_time = DateTime.Now;
            chatOpenChance = 2;
            Debug.Log($"[SmallTalk] Trigger activated: {smalltalkText}");
        }
    }
    else
    {
        Debug.Log("Skipping OnFinalResponseReceived for system type");
    }
}
```

### 3. Update() 메서드 추가 - 채팅창 감지 및 타이머

```csharp
void Update()
{
    // 채팅창 오픈/클로즈 감지
    bool isCurrentlyChatting = StatusManager.Instance.IsChatting;
    
    if (isCurrentlyChatting && !wasChatting)
    {
        // 채팅창이 방금 열렸음
        lastChatOpenTime = DateTime.Now;
        Debug.Log("[SmallTalk] Chat window opened");
    }
    else if (!isCurrentlyChatting && wasChatting)
    {
        // 채팅창이 방금 닫혔음 - 찬스 소모
        if (!string.IsNullOrEmpty(last_smalltalk) && chatOpenChance > 0)
        {
            double secondsSinceSmallTalk = (DateTime.Now - smalltalk_time).TotalSeconds;
            double timeSinceOpen = (DateTime.Now - lastChatOpenTime).TotalSeconds;
            
            // 10초 이내에 열었고, 10초 이상 열려있었던 경우 찬스 소모
            if (secondsSinceSmallTalk <= 10)
            {
                chatOpenChance--;
                Debug.Log($"[SmallTalk] Chat closed. Chance consumed. Remaining: {chatOpenChance}");
            }
        }
    }
    
    wasChatting = isCurrentlyChatting;
    
    // 30초 타이머 만료 체크
    if (!string.IsNullOrEmpty(last_smalltalk))
    {
        double elapsed = (DateTime.Now - smalltalk_time).TotalSeconds;
        if (elapsed > 30)
        {
            Debug.Log("[SmallTalk] Trigger expired (30s timeout)");
            ResetSmallTalkTrigger();
        }
    }
}

private void ResetSmallTalkTrigger()
{
    last_smalltalk = "";
    smalltalk_time = DateTime.MinValue;
    chatOpenChance = 0;
}
```

### 4. CallConversationStream 수정

**위치**: `CallConversationStream()` 메서드 (1377번 줄 requestData 생성 전)

```csharp
// SmallTalk 트리거 조건 체크
string intent_smalltalk_answer = "off";
string query_smalltalk = "";

if (!string.IsNullOrEmpty(last_smalltalk))
{
    double secondsSinceSmallTalk = (DateTime.Now - smalltalk_time).TotalSeconds;
    
    // 조건 1: 30초 이내에 답변 전송
    bool condition1 = secondsSinceSmallTalk <= 30;
    
    // 조건 2: 10초 이내에 채팅창을 열었고 찬스가 남아있음
    bool condition2 = secondsSinceSmallTalk <= 10 && chatOpenChance < 2;
    
    if (condition1 || condition2)
    {
        intent_smalltalk_answer = "on";
        query_smalltalk = last_smalltalk;
        Debug.Log($"[SmallTalk] Sending to server. Seconds: {secondsSinceSmallTalk:F1}s");
    }
}

// ... 기존 코드 ...

var requestData = new Dictionary<string, string>
{
    // ... 기존 파라미터들 ...
    { "server_local_mode", server_local_mode},
    { "intent_smalltalk_answer", intent_smalltalk_answer},  // 추가
    { "query_smalltalk", query_smalltalk}  // 추가
};
```

### 5. FetchStreamingData 수정 - intent_info 처리

**위치**: `FetchStreamingData()` 메서드

**수정 1**: intent_info 저장용 변수 추가 (메서드 시작 부분)

```csharp
string latestIntentSmallTalkAnswer = "off";
string latestSmallTalkQuery = "";
```

**수정 2**: JSON 파싱 부분에서 intent_info 저장 (943번 줄 이후)

```csharp
var jsonObject = JObject.Parse(line);
Debug.Log("jsonObject Start");
Debug.Log(jsonObject.ToString());
Debug.Log("jsonObject End");

// intent_info 저장 (추가)
try
{
    if (jsonObject["intent_info"] != null)
    {
        latestIntentSmallTalkAnswer = jsonObject["intent_info"]["is_intent_smalltalk_answer"]?.ToString() ?? "off";
        latestSmallTalkQuery = jsonObject["intent_info"]["smalltalk_query"]?.ToString() ?? "";
    }
}
catch (Exception ex)
{
    Debug.Log($"[SmallTalk] Failed to parse intent_info: {ex.Message}");
}

// 생각중 등등의 답변타입체크
string replyType = jsonObject["type"]?.ToString() ?? "reply";
```

**수정 3**: 응답 완료 시 SmallTalk 처리 (1070번 줄)

```csharp
if (curChatIdx == GameManager.Instance.chatIdxSuccess)
{
    if (currentMemoryType != "system")
    {
        OnFinalResponseReceived(); // 기존: 사용자 질문 + AI 응답 저장
        
        // SmallTalk 연관성이 있으면 사용자 답변만 추가 저장
        // (AI 잡담은 서버가 이미 memory에 추가했음)
        if (latestIntentSmallTalkAnswer == "on" && !string.IsNullOrEmpty(latestSmallTalkQuery))
        {
            SaveSmallTalkUserReply();
        }
    }
    else
    {
        Debug.Log("Skipping OnFinalResponseReceived for system type");
    }
}
```

### 6. SmallTalk 사용자 답변 저장 메서드 추가

**위치**: `OnFinalResponseReceived()` 메서드 이후

```csharp
// SmallTalk 관련 대화 저장
// 주의: 서버가 이미 AI 잡담을 memory에 추가했으므로
// Unity는 사용자 답변과 AI 응답만 저장 (OnFinalResponseReceived에서 처리됨)
private void SaveSmallTalkUserReply()
{
    Debug.Log("[SmallTalk] Detected related conversation");
    
    // OnFinalResponseReceived()가 이미 다음을 저장함:
    // 1. 사용자 답변 (player/user)
    // 2. AI 응답 (character/assistant)
    
    // 서버가 이미 AI 잡담을 memory에 추가했으므로
    // Unity는 추가 작업 불필요
    
    // 순서 정리:
    // Server memory: [AI 잡담]
    // OnFinalResponseReceived: [사용자 답변, AI 응답]
    // 최종 순서: AI 잡담 → 사용자 답변 → AI 응답
    
    Debug.Log("[SmallTalk] Conversation saved (user reply + AI response)");
    
    // SmallTalk 트리거 초기화
    ResetSmallTalkTrigger();
}
```

**주의**: `OnFinalResponseReceived()`가 이미 사용자 질문과 AI 응답을 저장하므로, 별도 저장 불필요. 다만 서버에서 `is_intent_smalltalk_answer=on`인 경우 서버가 memory에 AI 잡담을 먼저 추가했으므로, 최종 순서는:
1. AI 잡담 (서버가 memory에 추가)
2. 사용자 답변 (Unity OnFinalResponseReceived)
3. AI 응답 (Unity OnFinalResponseReceived)

### 7. CallConversationStreamGemini도 동일 패턴 적용

**위치**: `CallConversationStreamGemini()` 메서드 (1520번 줄)

```csharp
// SmallTalk 트리거 조건 체크 (동일 로직)
string intent_smalltalk_answer = "off";
string query_smalltalk = "";

if (!string.IsNullOrEmpty(last_smalltalk))
{
    double secondsSinceSmallTalk = (DateTime.Now - smalltalk_time).TotalSeconds;
    bool condition1 = secondsSinceSmallTalk <= 30;
    bool condition2 = secondsSinceSmallTalk <= 10 && chatOpenChance < 2;
    
    if (condition1 || condition2)
    {
        intent_smalltalk_answer = "on";
        query_smalltalk = last_smalltalk;
        Debug.Log($"[SmallTalk Gemini] Sending to server. Seconds: {secondsSinceSmallTalk:F1}s");
    }
}

// requestData에 추가
var requestData = new Dictionary<string, string>
{
    // ... 기존 파라미터들 ...
    { "chatIdx", chatIdx },
    { "regenerate_count", GameManager.Instance.chatIdxRegenerateCount.ToString() },
    { "intent_smalltalk_answer", intent_smalltalk_answer},  // 추가
    { "query_smalltalk", query_smalltalk}  // 추가
};
```

## 트리거 조건 상세

### 30초 이내 답변 전송
- Smalltalk 응답 후 최초 대화일 경우 on

### 최종 메모리 추가내용

잡담과 상관있었을 경우
```json
[
  // 잡담내용
  {
    "speaker": "character",
    "role": "assistant",
    "message": "선생님, 오늘 날씨가 좋네요!",
    "type": "conversation"
  },
  // 플레이어응답
  {
    "speaker": "player",
    "role": "user",
    "message": "그러게요, 산책하기 좋아요",
    "type": "conversation"
  },
  // AI응답
  {
    "speaker": "character",
    "role": "assistant",
    "message": "네! 햇살도 따뜻하고 정말 좋네요~",
    "type": "conversation"
  }
]
```

잡담과 상관없었을 경우. 잡담은 저장되지 않음
```json
[
  // 플레이어응답
  {
    "speaker": "player",
    "role": "user",
    "message": "그러게요, 산책하기 좋아요",
    "type": "conversation"
  },
  // AI응답
  {
    "speaker": "character",
    "role": "assistant",
    "message": "네! 햇살도 따뜻하고 정말 좋네요~",
    "type": "conversation"
  }
]
```
