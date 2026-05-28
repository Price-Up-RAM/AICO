# APIManager 병렬 대화 동시성(Concurrency) 문제 및 리팩토링 가이드

본 문서는 플레이어가 2명 이상의 캐릭터(예: Yuzu, Kei 등)와 동시에 대화(SmallTalk 포함)를 시도할 때, 양쪽 캐릭터의 데이터가 섞여서 같은 메모리 내용이 저장되는 버그의 원인과 해결책(`AIChatSession` 객체 지향 상태 관리)을 다음 담당 Agent에게 전달하기 위해 작성되었습니다.

---

## 1. 문제 현상 (Bug Description)
플레이어가 서브캐릭터 A(Yuzu)에게 말을 건 직후, 답변이 채 끝나기 전에 서브캐릭터 B(Kei)에게 말을 걸었을 때:
- A와 B의 메모리 JSON 파일은 각각 정상적으로 별도 생성됨. (파일명 지정 및 `targetCharacter` 처리 정상)
- **그러나 두 JSON 파일에 기록된 답변 내용이 A와 B의 응답이 섞이거나 동일하게 덮어써진 결과로 저장됨.**

## 2. 근본 원인 (Root Cause: Shared State Override)
`APIManager.cs`는 싱글톤(`Instance`)으로 작동하며, 비동기 스트리밍(Streaming)으로 LLM에서 오는 답변 청크(Chunk)들을 취합합니다.
하지만 현재 스트리밍 상태를 유지하기 위해 사용하는 변수들이 **클래스의 멤버 변수(전역 인스턴스 필드)**로 등록되어 있습니다.

### 문제가 되는 APIManager.cs 내 전역 변수들
```csharp
// Reply 리스트를 저장할 리스트
private List<string> replyListKo = new List<string>();
private List<string> replyListJp = new List<string>();
private List<string> replyListEn = new List<string>();

public string query_origin = "";
private string query_trans = "";
private string ai_language_out = "en";  
private bool isResponsedStarted = false; 
```

**[발생 시나리오]**
1. **Yuzu 요청 시작**: `replyListKo.Clear()` 호출, Yuzu용 스트리밍 시작. Yuzu의 청크들이 `replyListKo`에 담김.
2. **Kei 요청 시작**: 또다시 `replyListKo.Clear()` 호출, Kei용 스트리밍 시작. 이후 들어오는 모든 청크(Yuzu 것과 Kei 것)가 **동일한 `replyListKo`** 하나에 섞임.
3. 양쪽 비동기 통신이 완료(OnFinalResponseReceived)되면, 둘 다 오염된 하나의 `replyListKo` 내용을 읽어 각자의 파일에 저장하게 됨.

## 3. 해결 방안 (Solution: AIChatSession State Object)

전역 멤버 변수에 의존하던 상태를, **요청(Request) 단위의 지역 상태 객체**로 묶어(`Context` 객체 패턴) 매개변수로 전달하도록 리팩토링해야 합니다.

### Step 1. AIChatSession 클래스 선언
APIManager 상단이나 별도 스크립트(또는 새 파일 `AIChatSession.cs`)에 상태를 하나로 묶는 데이터 클래스를 선언합니다.
```csharp
public class AIChatSession
{
    public List<string> replyListKo = new List<string>();
    public List<string> replyListJp = new List<string>();
    public List<string> replyListEn = new List<string>();
    
    public string query_origin = "";
    public string query_trans = "";
    public string ai_language_out = "en";
    
    public bool isResponsedStarted = false;
    
    // 현재 세션의 타겟 캐릭터 명시
    public GameObject targetCharacter;

    public AIChatSession(GameObject targetCharacter = null)
    {
        this.targetCharacter = targetCharacter;
    }
}
```

### Step 2. APIManager의 전역 필드 삭제
분쟁의 씨앗이 되는 `APIManager.cs` 최상단의 `replyListKo`, `query_origin` 등의 인스턴스 필드들을 통째로 삭제합니다.

### Step 3. 함수 시그니처 수정 및 Session 객체 전달
진입점 메서드들 내부에서 `AIChatSession session = new AIChatSession(targetCharacter)`를 직접 생성하고, 스트리밍 데이터를 받는 파서 및 콜백들에게 이 `session` 객체를 넘겨 연쇄적으로 상태를 업데이트하게 만들어야 합니다.

**리팩토링 대상 핵심 흐름 및 메서드:**

1. **`FetchStreamingData` (비동기 루프 자체 처리 흐름)**
   - `CallSmallTalkStream` 등에서 호출됨.
   - 처음 시작할 때 `AIChatSession session = new AIChatSession(targetCharacter);` 생성.
   - 루프 안에서 발생하는 상태(`replyListKo.Add(...)`, `query_trans = ...` 등)를 전부 `session.` 필드에 업데이트하도록 변경.
   - 완료 후 뒷정리 로직(`OnFinalResponseReceived` 등)에서 이 `session` 객체를 가져다 쓰도록 구현.

2. **`CallConversationStreamGeminiDirect` (콜백 구조 흐름)**
   - 시작 부분에서 `AIChatSession session = new AIChatSession(targetCharacter);` 생성 후 초기화.
   - `ServerManager.Instance.CallConversationStreamGeminiDirect`로 넘기는 `onChunkReceived`, `onComplete` 람다 캡처 인자에 이 `session`을 물려줌.
   ```csharp
   onChunkReceived: (chunk) => ProcessReplyGeminiDirect(chunk, session),
   onComplete: () => OnFinalResponseReceived(session)
   ```

3. **`ProcessReplyGeminiDirect(string line, AIChatSession session)`**
   - 기존에 전역 리스트/문자열을 다루던 부분을 모두 `session.replyListKo.Add(...)`, `session.query_trans` 형식으로 접근하도록 치환.

4. **`OnFinalResponseReceived(AIChatSession session)`**
   - 메모리 저장의 종착지. 기존 전역 리스트 대신 넘어온 `session.replyListKo` 등을 파싱 및 `string.Join`하여 처리.
   - `session.targetCharacter`를 참고해 MemoryManager에 내용 저장.

이 문서의 지침대로 `AIChatSession` 객체를 통한 컨텍스트 통제만 완전하게 구현하면, 시스템 내에서 수십 개의 동시 스트리밍이 들어온다고 해도 각각 고유 영역에 묶여 데이터가 혼용(Overwrite)될 일이 사라집니다.
