# Sub-Character 채팅 시스템 구현 계획 (v2)

서브 캐릭터(SubChar)를 좌클릭 시 채팅이 가능하도록 확장. `CharManager.cs` 수정 최소화.

---

## Proposed Changes

### 1. CharManager — `activeCharacter` 추가

#### [MODIFY] CharManager.cs

```csharp
// 추가 (3줄)
private GameObject activeCharacter;  // 현재 대화 대상 (서브 캐릭터 클릭 시 설정)
public GameObject GetActiveCharacter() { return activeCharacter ?? currentCharacter; }
public void SetActiveCharacter(GameObject character) { activeCharacter = character; }
```

- `GetActiveCharacter()`: `activeCharacter`가 null이 아니면 그것, null이면 `currentCharacter` 반환
- API 호출 시 닉네임 참조에 사용

---

### 2. SubClickHandler — 좌클릭 시 채팅 활성화

#### [MODIFY] SubClickHandler.cs

- `HandleLeftClick()` / `HandleClickMobile()` 에 채팅 경로 추가:
  1. `CharManager.Instance.SetActiveCharacter(this.transform.parent.gameObject)`
  2. `ChatBalloonManager.Instance.characterTransform` = 서브 캐릭터 `RectTransform`
  3. `ChatBalloonManager.Instance.clickedCharacter` = 서브 캐릭터 GameObject
  4. `ChatBalloonManager.Instance.ToggleChatBalloon()`

---

### 3. APIManager — `GetActiveCharacter()` 닉네임 사용

#### [MODIFY] APIManager.cs

- 닉네임 참조 3곳 변경:
  - `CallConversationStream` (L1614)
  - `CallConversationStreamGemini` (L1850)
  - `CallConversationStreamGeminiDirect` (L1945)
- `CharManager.Instance.GetNickname(CharManager.Instance.GetCurrentCharacter())` → `CharManager.Instance.GetNickname(CharManager.Instance.GetActiveCharacter())`

---

### 4. SubAnswerBalloonSimpleController — 서브 캐릭터별 답변 풍선

#### [NEW] SubAnswerBalloonSimpleController.cs

서브 캐릭터 각각에 동적으로 `AddComponent` 되는 컨트롤러. 각 인스턴스는 자기 캐릭터의 답변 풍선 UI를 관리.

- **필드**: `answerBalloonInstance` (복제된 풍선 GameObject), `characterTransform`, `answerText`, `answerBalloonTransform`, `hideTimer`
- **주요 메서드** (AnswerBalloonSimpleManager 인터페이스 미러):
  - `Init(GameObject balloonPrefab, RectTransform charTransform)` — 풍선 프리팹 복제 및 초기화
  - `ShowAnswerBalloonSimple()` / `ShowAnswerBalloonSimpleInf()`
  - `ModifyAnswerBalloonSimpleText(string text)`
  - `HideAnswerBalloonSimple()`
  - `HideAnswerBalloonSimpleAfterAudio()` — `SubVoiceManager` 연동
  - `Destroy()` — 풍선 GameObject 파괴 + 자기 자신 컴포넌트 제거
- **Update**: `hideTimer` 기반 자동 숨김, 위치 추적 (자기 characterTransform 따라감)

#### [NEW] SubAnswerBalloonSimpleManager.cs

싱글톤 매니저. 서브 캐릭터별 `SubAnswerBalloonSimpleController` 생성/파괴 관리.

- **필드**: `answerBalloonSimplePrefab` (에디터에서 할당, 복제 원본), `Dictionary<GameObject, SubAnswerBalloonSimpleController> controllers`
- **주요 메서드**:
  - `GetOrCreateController(GameObject subChar)` — 해당 서브 캐릭터에 Controller가 없으면 생성
  - `RemoveController(GameObject subChar)` — Controller와 풍선 파괴
  - `ClearAll()` — 모든 Controller 정리 (서브 캐릭터 전체 제거 시)

---

### 5. SubStatusManager — 대화 관련 상태 활성화

#### [MODIFY] SubStatusManager.cs

주석 해제하여 활성화할 필드/프로퍼티:
- `isListening`, `isChatting`, `isAnswering`, `isAnsweringSimple`
- 대응 프로퍼티: `IsListening`, `IsChatting`, `IsAnswering`, `IsAnsweringSimple`
- `IsConversationing` Getter (readonly)

> `isAsking`, `isThinking`, `isOnTop`, `isMinimize`, `isAiUsing` 등은 서브 캐릭터에 불필요하므로 비활성 유지.

---

### 6. AnswerBalloon 위치 동적 전환 (메인 풍선)

#### [MODIFY] AnswerBalloonManager.cs

- `ShowAnswerBalloonInf()` / `ShowAnswerBalloon()` 호출 시, `CharManager.Instance.GetActiveCharacter()`가 currentCharacter가 아니면 `characterTransform`을 해당 캐릭터의 `RectTransform`으로 동적 설정

#### [MODIFY] AnswerBalloonSimpleManager.cs

- 동일 로직

---

### 7. ChatBalloonManager — 초기화

#### [MODIFY] ChatBalloonManager.cs

- `HideChatBalloon()` 에서 `CharManager.Instance.SetActiveCharacter(null)` 호출

---

## 데이터 흐름 (서브 캐릭터 좌클릭 → 답변 수신)

```
User → SubClickHandler: 좌클릭
SubClickHandler → CharManager: SetActiveCharacter(subChar)
SubClickHandler → ChatBalloon: ToggleChatBalloon()
User → ChatHandler: 질문 입력 & Enter
ChatHandler → APIManager: CallConversationStream()
APIManager → CharManager: GetActiveCharacter() → subChar
APIManager → APIManager: nickname = subChar.CharAttributes.nickname
APIManager → AnswerBalloonManager: ShowAnswerBalloon() (위치: subChar)
APIManager → AnswerBalloonManager: ModifyText(streaming)
ChatBalloon → CharManager: HideChatBalloon → SetActiveCharacter(null)
```

---

## Verification Plan

### Manual Verification (Unity Editor)
1. 컴파일 에러 없음 확인
2. 서브 캐릭터 소환 → 좌클릭 → ChatBalloon이 서브 캐릭터 위에 표시
3. 채팅 전송 → Debug.Log에서 서브 캐릭터 닉네임 사용 확인
4. AnswerBalloon이 서브 캐릭터 위에 표시
5. 메인 캐릭터 클릭 시 기존 동작 정상
6. 채팅 닫기 후 activeCharacter null 복원 확인

---

## 실제 작업 및 수정 내역 (최종 반영 방식)

### 1. 라우팅 로직 롤백 및 순수성 보존
- `AnswerBalloonManager.cs` 및 `AnswerBalloonSimpleManager.cs`에 추가되었던 `targetCharacter` 기반 분기 로직(오염된 로직)을 모두 제거하여 메인 캐릭터 전용 동작으로 롤백했습니다.
- 서브 캐릭터의 말풍선 관리를 메인 매니저에 섞지 않고 완전 분리된 방식으로 구현 적용.

### 2. APIManager 직접 라우팅 설계 채택
- `APIManager.cs`의 `ProcessReply`, `FetchStreamingData`, `CallConversationStream`, `CallConversationStreamGeminiDirect` 등 채팅 응답 처리 메서드에서 `targetCharacter` 정보를 비동기 환경에서 안전하게 끝까지 전달받음.
- 파싱 시점에 `targetCharacter != CharManager.Instance.GetCurrentCharacter()` 조건 검사를 통해, 응답 대상이 서브 캐릭터일 경우 즉시 `SubAnswerBalloonSimpleManager`의 서브 컨트롤러로 직접 라우팅되도록 구현.
- `TTSManager.GetKoWavFromAPI` 및 `GetJpWavFromAPI` 호출 시에도 서브 캐릭터의 닉네임을 파라미터로 명시 전달하여 서브캐릭터 전용 보이스를 정상적으로 사용하도록 처리.

### 3. 프리팹 종속성 제거 및 Instantiate 직접 복제
- `SubAnswerBalloonSimpleManager.cs`에서 별도로 Inspector를 통해 Prefab 할당을 요구하지 않도록 자동 탐색 로직 개선.
- 런타임에 씬에 이미 존재하는 `AnswerBalloonSimple` (메인 캐릭터용) 오브젝트를 찾아 원본(`balloonSourceObject`)으로 취급하고, 이를 직접 `Instantiate`하여 서브캐릭터용으로 복제.
- `SubAnswerBalloonSimpleController.cs`는 복제된 자기 자신의 풍선 오브젝트를 관리. 또한 역번역이나 기타 UI 갱신 동작 처리를 위해 오염되지 않은 전용 `ModifyAnswerBalloonSimpleTextInfo` 언어 캐싱 메서드를 추가하여 개별 구동되도록 확장.
