# Plan2: ApiAgentFunction 통합 및 이식 계획서 (Migration Plan)

본 문서는 `plan1.md` 및 `Agents_Router` 디렉토리의 기획을 바탕으로 현재까지의 구현 현황을 점검하고, 기존 시스템에 산재된 기능들을 신규 아키텍처(`ApiAgentFunction`)로 안전하게 이식하기 위한 계획입니다.

## Open Questions (Grill ME 🍖)

기존 로직을 변경하기 전에 반드시 짚고 넘어가야 할 설계상 리스크와 불명확한 점들입니다. 코드를 수정하기 전에 아래 사항들에 대한 정책 결정이 필요합니다.

### 1. 보안 및 프라이버시 리스크 (Clipboard I/O)
> [!CAUTION]
> AI 에이전트가 `clipboard_read`를 통해 클립보드에 무제한 접근할 경우, 유저가 복사해둔 비밀번호나 민감한 개인정보가 유출될 심각한 보안 리스크가 있습니다.
- **질문**: 클립보드 읽기를 수행할 때 유저에게 권한 팝업을 띄워야 할까요? 아니면 에이전트 전용 격리된(Sandboxed) 가상 클립보드만 사용하도록 제한하는 것이 좋을까요?

### 2. 기술적 불확실성 (Proxy Click과 Unity UI의 충돌)
> [!WARNING]
> `ProxyMouseAction`은 Win32 `PostMessage`를 통해 백그라운드 클릭을 보냅니다. 하지만 Unity는 단일 HWND를 사용하며, 특히 Unity의 `EventSystem`은 클릭 이벤트 처리 시 **실제 물리 마우스의 커서 위치**를 참조하는 경우가 많습니다.
- **질문**: 마우스 커서를 다른 곳에 둔 상태에서 `PostMessage`만 보냈을 때 Unity UI 버튼이 정상적으로 눌리는지 실제 검증이 완료되었나요? (만약 안 된다면, 메모리 상에서 직접 EventSystem Raycast를 쏘는 `Unity 내부 전용 Proxy Click`을 별도로 만들어야 할 수 있습니다.)

### 3. Fallback 판단의 모호성
> [!NOTE]
> 계획서에는 "Proxy 동작 실패 시 물리(Physical) 동작으로 Fallback 한다"고 되어 있습니다. 하지만 `PostMessage`는 단순히 메시지를 큐에 넣을 뿐, 실제 UI가 반응했는지 여부를 반환하지 않습니다.
- **질문**: 프록시 동작이 "실패했다"는 것을 시스템이 어떻게 판별하나요? 화면 캡처본을 `diff_compare`로 비교해서 변화가 없으면 실패로 간주하는 건가요? 아니면 스킬(SKILL.md) 내에 LLM이 명시적으로 판단하도록 맡기는 건가요?

### 4. JSON 파싱 라이브러리 및 데이터 포맷
> [!NOTE]
> `ApiAgentFunction`은 파라미터를 `Dictionary<string, object>` 형태로 받습니다. Unity의 기본 `JsonUtility`는 Dictionary나 동적 객체(Dynamic) 파싱을 지원하지 않습니다.
- **질문**: 현재 프로젝트에서는 백엔드에서 내려오는 동적 파라미터를 파싱하기 위해 `Newtonsoft.Json`(JObject)을 사용하고 있나요? 사용할 파서 라이브러리가 명확히 정해져 있나요?

### 5. Unity CRUD 툴의 접근 권한
> [!NOTE]
> `unity_crud_read/create/update/delete` 도구가 기획되어 있습니다.
- **질문**: 에이전트가 조작할 수 있는 데이터의 범위가 어디까지인가요? (예: `PlayerPrefs` 전체? 특정 JSON 세이브 파일? 에이전트 전용 메모리?) 치명적인 게임 설정이나 세이브 파일이 손상되지 않도록 접근 범위를 제한할 제안(Sandbox 제한)을 드리고 싶습니다.

---

## 1. 구현 현황 점검 (Plan1 & Agents_Router 대비)

### ✅ 구현 완료 항목
1. **단일 책임 원칙에 따른 모듈화 (plan1.md)**
   - `ApiAgentFunction.cs` (게이트웨이 뼈대)
   - `ApiAgentFunctionMouseAction.cs` (물리 마우스: Click, Drag, Scroll)
   - `ApiAgentFunctionProxyMouseAction.cs` (비침습 프록시: Click, Drag, Scroll)
   - `ApiAgentFunctionKeyboardAction.cs` (키보드: TypeText, SendHotkey)
   - `ApiAgentFunctionSystemAction.cs` (창 포커스, 프로세스 실행)
   - `ApiAgentFunctionScreenshotAction.cs` (화면 캡처, 로컬 저장)
   - `ApiAgentFunctionSkillManager.cs` (로컬 마크다운 스킬 CRUD)
   - `ApiAgentFunctionTester.cs` (UI 기반 통합 테스트 환경)

### ⚠️ 누락 및 추가 필요 항목 (Agents_Router/Plan3.md 참조)
1. **클립보드 텍스트 제어 (Clipboard I/O)**
   - 스크린샷의 클립보드 복사는 목업으로 있으나, `Plan3`에서 요구한 텍스트의 `clipboard_read`, `clipboard_write` 액션이 아직 누락되어 있습니다. `ApiAgentFunctionSystemAction`에 추가 구현이 필요합니다.
2. **Unity 내부 데이터 CRUD 도구 (unity_crud_read / create / update / delete)**
   - 로컬 마크다운 스킬 외에 일반적인 인게임 상태 데이터를 조작/조회하기 위한 툴이 누락되어 있습니다.
3. **ApiAgentFunction.cs 의 실제 라우팅 로직 미구현**
   - 현재 `ExecuteAction` 메소드가 `test` 명령어에만 반응하는 더미 코드로 작성되어 있습니다. 모든 기능 클래스로 파라미터를 파싱해 넘겨주는 라우팅 테이블 스위치(Switch/If-Else)의 완성이 시급합니다.

---

## 2. 기존 로직 이식 계획 (Migration Strategy)

현재 기존 매니저(예: `ApiVlPlannerManager`, `APIManager`)들에 파편화된 액션 실행부를 `ApiAgentFunction`으로 통일하기 위해 다음의 3단계로 이식을 진행합니다.

### Phase 1: 라우터(Gateway) 본체 완성 및 누락 기능 추가
* **작업 대상**: `ApiAgentFunction.cs`, `ApiAgentFunctionSystemAction.cs`
* **작업 내용**: 
  * `clipboard_read`, `clipboard_write` 기능 추가.
  * 외부(Python 서버 등)로부터 전달받은 JSON 파라미터(`Dictionary<string, object>`)를 파싱하여 각각의 하위 Action 클래스의 메소드를 호출하는 라우팅 분기문 작성.
  * 타입 변환 오류나 필수 파라미터 누락에 대비한 안전한 헬퍼 메소드 추가 (vibe/basicRule.md 규칙 엄수).

### Phase 2: 기존 매니저들의 액션 호출부 교체
* **작업 대상**: 기존의 마우스/키보드/시스템 제어를 직접 호출하고 있는 기존 매니저 스크립트.
* **작업 내용**:
  * 기존의 파편화된 화면 클릭, 키보드 입력, 스크린샷 요청 처리 부분을 `ApiAgentFunction.Instance.ExecuteAction(이름, 파라미터, 콜백)` 단일 인터페이스 호출로 일괄 대체합니다.
  * 변경 시 기존 통신 로직에 영향을 주지 않도록 하위 호환성을 유지하며 전환합니다.

### Phase 3: 테스트 및 안정화
* **작업 대상**: `ApiAgentFunctionTester.cs`
* **작업 내용**:
  * 새로 연결된 전체 라우팅 명령들을 유니티 에디터상에서 버튼 한 번으로 테스트할 수 있도록 테스터 UI를 갱신합니다.
  * 비침습적 동작(Proxy) 실패 시 물리 동작으로 안전하게 Fallback 하는지 시나리오 테스트를 수행합니다.

---

## 3. 코드 수정 주의사항 (vibe/basicRule.md)
* **주석 규칙**: 메소드 위에는 오직 한 줄 `//` 주석만 사용합니다.
* **조건문 규칙**: 1줄짜리 `if`문이나 짧은 분기라도 무조건 `{}` 중괄호를 사용하여 명시적인 `if-else` 블록 형태로 작성합니다 (삼항 연산자 절대 금지).
* **Null Check 규칙**: 싱글톤 접근 시 `.Instance`는 null을 반환하지 않으므로, `Instance != null` 식의 중복 검사를 지양합니다.

> 본 문서는 로직 이식을 위한 최종 계획서이며, 승인 후 실제 코드 수정을 시작합니다.
