# Unity-Python Tool Definitions (ApiAgentFunctionManager)

이 문서는 파이썬 서버(LLM)가 Unity 클라이언트에게 명령을 내릴 때 사용하는 **프로토콜 명세서**입니다.

---

## 프로토콜 구조

파이썬 서버는 아래 JSON 형태로 Unity에 명령을 전달합니다.

```json
{
  "function_name": "<functionName>",
  "parameters": { ... },
  "agent_state": { ... }
}
```

| 필드 | 타입 | 필수 | 설명 |
|---|---|---|---|
| `function_name` | string | ✅ | 실행할 Tool 이름 |
| `parameters` | object | ✅ | Tool별 파라미터 (파라미터 없는 Tool은 `{}`) |
| `agent_state` | object | ✅ | 에이전트 누적 상태. 이전 동작 이력, 컨텍스트 등을 포함. 비어있어도 반드시 전달 (`{}` 허용) |

### 응답

Unity는 실행 결과를 `(bool success, string message)` 형태로 콜백합니다.

```json
{
  "success": true,
  "message": "실행 결과 메시지"
}
```

### 예시

```json
{
  "function_name": "physical_click",
  "parameters": {
    "winX": 500,
    "winY": 300,
    "isMouseMove": true
  },
  "agent_state": {
    "previous_actions": ["captured screenshot", "found button at (500, 300)"],
    "goal": "닫기 버튼 클릭"
  }
}
```

> **진입점**: `ApiAgentFunctionManager.Instance.ExecuteAction(functionName, parameters, callback)`

---

## 1. Mouse Actions (마우스 제어)

### `physical_click`
* **설명**: 실제 마우스 커서를 이동시켜 물리 클릭을 수행합니다.
* **경유**: `ApiAgentFunctionMouseAction.Instance.PhysicalClick()`
* **Parameters**:
  * `winX` (int): Windows 화면 X 좌표
  * `winY` (int): Windows 화면 Y 좌표
  * `isMouseMove` (bool, default: `true`): 클릭 전 커서 이동 여부
* **응답**: 항상 `(true, "물리 클릭 실행 완료: ({winX}, {winY})")`

### `proxy_click`
* **설명**: 대상 창에 WinAPI 메시지를 직접 전송하는 비침습 클릭입니다. 커서가 이동하지 않습니다.
* **경유**: `ApiAgentFunctionProxyMouseAction.Instance.ProxyClick()`
* **Parameters**:
  * `winX` (int): 대상 창 기준 X 좌표
  * `winY` (int): 대상 창 기준 Y 좌표
* **응답**: 성공 `(true, "프록시 클릭 실행 완료: (...)")` / 실패 `(false, "프록시 클릭 실행 실패")`

### `physical_drag`
* **설명**: 실제 마우스 커서로 드래그를 수행합니다.
* **경유**: `ApiAgentFunctionMouseAction.Instance.PhysicalDrag()`
* **Parameters**:
  * `startX` (int): 시작 X 좌표
  * `startY` (int): 시작 Y 좌표
  * `endX` (int): 종료 X 좌표
  * `endY` (int): 종료 Y 좌표
  * `durationMs` (int, default: `500`): 드래그 소요 시간 (밀리초)
* **응답**: 항상 `(true, "물리 드래그 실행 완료: (...) -> (...)")`

### `proxy_drag`
* **설명**: 대상 창에 WinAPI 메시지를 직접 전송하는 비침습 드래그입니다.
* **경유**: `ApiAgentFunctionProxyMouseAction.Instance.ProxyDrag()`
* **Parameters**:
  * `startX` (int): 시작 X 좌표
  * `startY` (int): 시작 Y 좌표
  * `endX` (int): 종료 X 좌표
  * `endY` (int): 종료 Y 좌표
* **응답**: 성공 `(true, "프록시 드래그 실행 완료: (...)")` / 실패 `(false, "프록시 드래그 실행 실패")`

### `physical_scroll`
* **설명**: 실제 마우스 휠 스크롤을 수행합니다.
* **경유**: `ApiAgentFunctionMouseAction.Instance.PhysicalScroll()`
* **Parameters**:
  * `winX` (int): X 좌표
  * `winY` (int): Y 좌표
  * `scrollAmount` (int): 스크롤 틱 수 (양수=위, 음수=아래)
* **응답**: 항상 `(true, "물리 스크롤 실행 완료: (...)")`

### `proxy_scroll`
* **설명**: 대상 창에 WinAPI 메시지를 직접 전송하는 비침습 스크롤입니다.
* **경유**: `ApiAgentFunctionProxyMouseAction.Instance.ProxyScroll()`
* **Parameters**:
  * `winX` (int): 대상 창 기준 X 좌표
  * `winY` (int): 대상 창 기준 Y 좌표
  * `scrollAmount` (int): 스크롤 틱 수 (양수=위, 음수=아래)
* **응답**: 성공 `(true, "프록시 스크롤 실행 완료: (...)")` / 실패 `(false, "프록시 스크롤 실행 실패")`

---

## 2. Keyboard Actions (키보드 제어)

### `type_text`
* **설명**: 현재 포커스된 창에 텍스트를 타이핑합니다.
* **경유**: `ApiAgentFunctionKeyboardAction.Instance.TypeText()`
* **Parameters**:
  * `text` (string): 입력할 문자열
* **응답**: 항상 `(true, "타이핑 실행 완료: {text}")`

### `send_hotkey`
* **설명**: 단축키 조합을 입력합니다. modifier와 key를 **분리**하여 전달합니다.
* **경유**: `ApiAgentFunctionKeyboardAction.Instance.SendHotkey()`
* **Parameters**:
  * `modifier` (string): 수식키. 예) `"Ctrl"`, `"Alt"`, `"Shift"`, `""` (없음)
  * `key` (string): 기본키. 예) `"C"`, `"V"`, `"Tab"`, `"Enter"`
* **응답**: 항상 `(true, "단축키 실행 완료: {modifier} + {key}")`
* **예시**: `{"modifier": "Ctrl", "key": "C"}`

---

## 3. System Actions (시스템 제어)

### `run_process`
* **설명**: 시스템 프로세스를 실행합니다.
* **경유**: `ApiAgentFunctionSystemAction.Instance.RunProcess()`
* **Parameters**:
  * `fileName` (string): 실행 파일명 (예: `"notepad.exe"`, `"calc.exe"`)
* **응답**: 성공 `(true, "<PID>")` — 프로세스 ID를 문자열로 반환 / 실패 `(false, "프로세스 실행 실패")`

### `focus_process`
* **설명**: PID로 프로세스를 찾아 최상단으로 포커스합니다.
* **경유**: `ApiAgentFunctionSystemAction.Instance.FocusProcess()`
* **Parameters**:
  * `pid` (int): 대상 프로세스 ID
* **응답**: 성공 `(true, "프로세스 포커스 성공")` / 실패 `(false, "프로세스 포커스 실패")` / 예외 `(false, "프로세스 찾기 오류: ...")`

---

## 4. Clipboard Actions (클립보드)

### `read_clipboard`
* **설명**: PC 시스템 클립보드에서 텍스트를 읽어옵니다.
* **경유**: `ApiAgentFunctionSystemAction.Instance.ReadClipboardText()`
* **Parameters**: 없음
* **응답**: `(true, "<클립보드 텍스트>")`

### `write_clipboard`
* **설명**: PC 시스템 클립보드에 텍스트를 씁니다.
* **경유**: `ApiAgentFunctionSystemAction.Instance.WriteClipboardText()`
* **Parameters**:
  * `text` (string): 클립보드에 쓸 문자열
* **응답**: 항상 `(true, "클립보드 쓰기 완료")`

---

## 5. Screenshot Action (스크린샷)

### `capture_screenshot`
* **설명**: 현재 화면을 캡처하여 지정 경로에 저장합니다.
* **경유**: `ApiAgentFunctionScreenshotAction.Instance.CaptureAndSave()`
* **Parameters**:
  * `path` (string): 저장할 파일 경로 (빈 문자열이면 기본 경로)
* **응답**: 항상 `(true, "스크린샷 캡처 완료: {path}")`

---

## 6. Data & Skill CRUD (데이터 및 스킬 파일 입출력)

*Unity의 `Application.persistentDataPath` 기준으로 동작합니다.*

### 일반 데이터 파일

#### `save_data`
* **설명**: 텍스트 데이터를 지정한 상대 경로에 저장합니다. 하위 폴더가 없으면 자동 생성합니다.
* **경유**: `ApiAgentFunctionSkillManager.Instance.SaveData()`
* **Parameters**:
  * `path` (string): 상대 경로. 예) `"test_data.txt"`, `"notes/memo.md"`
  * `content` (string): 저장할 내용
* **응답**: 항상 `(true, "데이터 저장 완료")`

#### `read_data`
* **설명**: 지정된 상대 경로의 파일 내용을 읽어옵니다.
* **경유**: `ApiAgentFunctionSkillManager.Instance.ReadData()`
* **Parameters**:
  * `path` (string): 읽어올 파일의 상대 경로
* **응답**: `(true, "<파일 내용>")`

#### `delete_data`
* **설명**: 지정된 상대 경로의 파일을 삭제합니다.
* **경유**: `ApiAgentFunctionSkillManager.Instance.DeleteData()`
* **Parameters**:
  * `path` (string): 삭제할 파일의 상대 경로
* **응답**: 항상 `(true, "데이터 삭제 완료")`

### 스킬 파일 (Frontmatter 포함 마크다운)

#### `save_skill`
* **설명**: 마크다운 스킬 파일을 frontmatter + body 형태로 저장합니다.
* **경유**: `ApiAgentFunctionSkillManager.Instance.SaveSkill()`
* **Parameters**:
  * `key` (string): 스킬 식별 키 (파일명 기준)
  * `frontmatter` (string): YAML frontmatter 문자열
  * `body` (string): 본문 마크다운 내용
* **응답**: 항상 `(true, "스킬 저장 완료: {key}")`

#### `read_skill_body`
* **설명**: 스킬 파일의 본문(body) 내용만 읽어옵니다.
* **경유**: `ApiAgentFunctionSkillManager.Instance.ReadSkillBody()`
* **Parameters**:
  * `key` (string): 스킬 식별 키
* **응답**: `(true, "<스킬 본문>")`

---

## 7. Audio (오디오 재생)

### `play_sfx`
* **설명**: Unity `StreamingAssets` 폴더의 음원 파일을 재생합니다. 확장자(`.wav`/`.ogg`)를 자동 판별합니다.
* **경유**: `ApiAgentFunctionSfx.Instance.PlaySfx()`
* **Parameters**:
  * `path` (string): `StreamingAssets` 기준 상대 경로. 예) `"Sound/arona/Arona_Academy_Talk_4.ogg"`
* **지원 포맷**: `.ogg` (OGG Vorbis), `.wav` (PCM WAV)
* **응답**: 항상 `(true, "SFX 재생 완료: {path}")`

---

## 8. Chat Mode (대화 모드 제어)

### `set_chat_mode`
* **설명**: 대화 모드를 지정한 모드로 전환합니다.
* **경유**: `ApiAgentFunctionChatMode.Instance.SetChatMode()`
* **Parameters**:
  * `mode` (string): `"chat"` | `"aropla"` | `"operator"`
* **응답**: 성공 `(true, "대화 모드 설정 완료: {mode}")` / 실패 `(false, "대화 모드 설정 실패")`

### `toggle_chat_mode`
* **설명**: 지정한 대화 모드를 토글합니다. 이미 해당 모드이면 `"chat"`으로 복귀합니다.
* **경유**: `ApiAgentFunctionChatMode.Instance.ToggleChatMode()`
* **Parameters**:
  * `mode` (string): `"chat"` | `"aropla"` | `"operator"`
* **응답**: 성공 `(true, "대화 모드 토글 완료: {mode}")` / 실패 `(false, "대화 모드 토글 실패")`

### `get_chat_mode`
* **설명**: 현재 활성화된 대화 모드를 반환합니다.
* **경유**: `ApiAgentFunctionChatMode.Instance.GetChatMode()`
* **Parameters**: 없음
* **응답**: `(true, "<현재 모드>")`

---

## 9. Character Actions (캐릭터 액션 제어)

### `character_dance`
* **설명**: 캐릭터가 무작위 댄스 애니메이션을 수행합니다.
* **경유**: `ApiAgentFunctionAction.Instance.Dance()`
* **Parameters**: 없음
* **응답**: 항상 `(true, "캐릭터 춤추기 실행")`

### `character_walk_left`
* **설명**: 캐릭터가 왼쪽 방향으로 걷기 이동을 시작합니다.
* **경유**: `ApiAgentFunctionAction.Instance.WalkLeft()`
* **Parameters**: 없음
* **응답**: 항상 `(true, "캐릭터 왼쪽 걷기 실행")`

### `character_walk_right`
* **설명**: 캐릭터가 오른쪽 방향으로 걷기 이동을 시작합니다.
* **경유**: `ApiAgentFunctionAction.Instance.WalkRight()`
* **Parameters**: 없음
* **응답**: 항상 `(true, "캐릭터 오른쪽 걷기 실행")`

### `character_stop`
* **설명**: 현재 수행 중인 모든 캐릭터 액션(춤, 걷기 등)을 중지하고 Idle 상태로 복귀합니다.
* **경유**: `ApiAgentFunctionAction.Instance.StopAction()`
* **Parameters**: 없음
* **응답**: 항상 `(true, "캐릭터 동작 멈춤 실행")`

---

## 10. Debug

### `test`
* **설명**: 연결 테스트용. 항상 성공을 반환합니다.
* **Parameters**: 없음
* **응답**: 항상 `(true, "테스트 성공")`

---

## 전체 functionName 요약표

| functionName | 카테고리 | 필수 파라미터 | 실패 가능 |
|---|---|---|---|
| `physical_click` | Mouse | `winX`, `winY` | ✗ |
| `proxy_click` | Mouse | `winX`, `winY` | ✔ |
| `physical_drag` | Mouse | `startX`, `startY`, `endX`, `endY` | ✗ |
| `proxy_drag` | Mouse | `startX`, `startY`, `endX`, `endY` | ✔ |
| `physical_scroll` | Mouse | `winX`, `winY`, `scrollAmount` | ✗ |
| `proxy_scroll` | Mouse | `winX`, `winY`, `scrollAmount` | ✔ |
| `type_text` | Keyboard | `text` | ✗ |
| `send_hotkey` | Keyboard | `modifier`, `key` | ✗ |
| `run_process` | System | `fileName` | ✔ |
| `focus_process` | System | `pid` | ✔ |
| `read_clipboard` | Clipboard | (없음) | ✗ |
| `write_clipboard` | Clipboard | `text` | ✗ |
| `capture_screenshot` | Screenshot | `path` | ✗ |
| `save_data` | Data CRUD | `path`, `content` | ✗ |
| `read_data` | Data CRUD | `path` | ✗ |
| `delete_data` | Data CRUD | `path` | ✗ |
| `save_skill` | Skill CRUD | `key`, `frontmatter`, `body` | ✗ |
| `read_skill_body` | Skill CRUD | `key` | ✗ |
| `play_sfx` | Audio | `path` | ✗ |
| `set_chat_mode` | Chat Mode | `mode` | ✔ |
| `toggle_chat_mode` | Chat Mode | `mode` | ✔ |
| `get_chat_mode` | Chat Mode | (없음) | ✗ |
| `character_dance` | Character | (없음) | ✗ |
| `character_walk_left` | Character | (없음) | ✗ |
| `character_walk_right` | Character | (없음) | ✗ |
| `character_stop` | Character | (없음) | ✗ |
| `get_functions_list` | Debug | (없음) | ✗ |
| `get_functions_detail_list` | Debug | (없음) | ✗ |
| `test` | Debug | (없음) | ✗ |

---

## 11. Function Registry (기능 목록 반환)

### `get_functions_list`
* **설명**: 지원하는 모든 기능 이름과 카테고리만 리스트로 반환합니다.
* **Parameters**: 없음
* **응답**: `(true, "<JSON 직렬화된 기능 목록>")`
* **반환 JSON 구조 예시**:
  ```json
  [
    {
      "name": "physical_click",
      "category": "Mouse"
    },
    ...
  ]
  ```

### `get_functions_detail_list`
* **설명**: 지원하는 모든 기능과 각 기능의 상세 설명 및 파라미터 규격을 포함한 전체 정보를 반환합니다.
* **Parameters**: 없음
* **응답**: `(true, "<JSON 직렬화된 기능 상세 정보 목록>")`
* **반환 JSON 구조 예시**:
  ```json
  [
    {
      "name": "physical_click",
      "category": "Mouse",
      "description": "...",
      "parameters": [
        {
          "name": "winX",
          "type": "int",
          "description": "...",
          "required": true
        },
        ...
      ]
    },
    ...
  ]
  ```

