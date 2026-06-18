# Skills UI 작성 가이드

이 폴더(`Assets/Prefabs/UI/Skills`)는 "스킬" 관련 UI를 관리한다.
새 UI를 만들 때는 기존 UI들(`CharacterDetail`, `Alarm`, `Calendar`, `Pomodoro`, `TODOList`)과
`Assets/Devion Games/UI Widgets`의 패턴을 그대로 따른다. 이 문서는 그 규칙을 정리한 것이다.

> 핵심 원칙: **새 패턴을 발명하지 말고, 옆 폴더의 UI가 하는 방식을 그대로 베껴라.**

---

## 1. 폴더 / 파일 구조

기능 하나당 폴더 하나. `Calendar` / `Pomodoro` / `TODOList`가 쓰는 하위 폴더 구조를 따른다.

```
Assets/Prefabs/UI/Skills/
  CLAUDE.md                       <- 이 문서
  <기능명>/
    Prefabs/<기능명>.prefab        <- 프리팹은 Prefabs/ 하위
    Scripts/<기능명>UI.cs          <- 스크립트는 Scripts/ 하위
    Scripts/<기능명>ItemRow.cs     <- (선택) 리스트 행 등
    Sprites/ , Sounds/            <- (선택) 전용 에셋
    <기능명>_UI_Plan.md            <- (선택) 프리팹 레이아웃 계획
    <기능명>_Integration_Plan.md   <- (선택) 런타임 연동 계획
```

- `Alarm`, `CharacterDetail`은 프리팹과 스크립트를 폴더 바로 아래 평평하게 둔다.
  `Calendar` / `Pomodoro` / `TODOList`는 `Scripts/`, `Prefabs/`로 분리한다.
  **신규 작업은 `Scripts/`, `Prefabs/` 분리 방식을 기본으로 한다.**
- 폴더명·프리팹명은 PascalCase (`CharacterDetail`, `TODOList`).
- Unity가 `.meta` 파일을 자동 생성하므로 직접 만들지 않는다. `.cs`/`.prefab`을 추가하면 에디터가 import 시 처리한다.

### 네이밍 규칙 (기존 코드 기준)

| 역할 | 클래스명 패턴 | 예시 |
|------|---------------|------|
| 메인 컨트롤러 | `<기능>Controller` 또는 `<기능>UI` | `CharacterDetailController`, `JarvisTodoListUI`, `AlarmUI` |
| 리스트 행 / 아이템 | `<기능>ItemRow`, `<기능>ListItemView` | `JarvisTodoItemRow`, `AlarmListItemView` |
| 드래그/입력 핸들러 | `<기능>...Handler`, `<기능>...DragHandler` | `PomodoroUIHandler`, `JarvisTodoRowDragHandle` |
| 데이터 저장소 | `<기능>Store`, `<기능>Repository` | `JarvisTodoStore`, `AlarmRepository` |

> `Calendar`/`TODOList` 계열은 `Jarvis` 접두사를 쓴다. 스킬 UI는 기능 성격에 맞는
> 접두사를 일관되게 쓰되, 한 폴더 안에서는 통일한다.

---

## 2. 컨트롤러(MonoBehaviour) 작성 규칙

기존 컨트롤러들이 공유하는 패턴. `CharacterDetailController.cs`, `JarvisTodoListUI.cs`를 참고 구현으로 삼아라.

### 직렬화 필드
- `[SerializeField] private` 로 선언하고 `[Header("...")]`로 그룹화한다.
- TextMeshPro를 사용한다: `TextMeshProUGUI` / `TMP_Text` / `TMP_InputField` / `TMP_Dropdown`.
  (레거시 `UnityEngine.UI.Text`는 신규 작업에서 쓰지 않는다.)
- 색상·임계값·기본 라벨 등 튜닝 값도 `[SerializeField]`로 노출한다 (`AlarmListItemView`의 `enabledColor`, `longPressSeconds` 등).

### 참조 자동 바인딩 (중요)
프리팹 와이어링이 끊겨도 동작하도록, `Awake`에서 자식을 이름으로 깊이 탐색해 비어있는 참조를 채운다.
`CharacterDetailController.AutoBindMissingReferences()` + `FindDeepChild()` 패턴을 그대로 복사:

```csharp
field = field != null ? field : FindComponent<Button>("HideButton");
```

`FindDeepChild(transform, name)` 헬퍼는 거의 모든 컨트롤러에 복붙되어 있다. 동일하게 가져다 쓴다.

### 라이프사이클 & 공개 API
표준 진입점을 맞춘다. UIManager가 이 이름들에 의존한다.

```csharp
public void Show(...)   // 활성화 + 데이터 갱신. gameObject.SetActive(true) 포함
public void Hide()      // 또는 Close(). gameObject.SetActive(false)
public void Toggle(...) // (선택)
private void Refresh()  // 데이터 → UI 반영. Show 및 데이터 변경 이벤트에서 호출
```

- 이벤트 구독은 `Awake`/`OnEnable`에서, 해제는 `OnDestroy`/`OnDisable`에서.
  반드시 짝을 맞춘다 (`onClick.AddListener` ↔ `RemoveListener`/`RemoveAllListeners`).
- 데이터 저장소가 이벤트를 노출하면 `OnEnable`에서 구독하고 `OnDisable`에서 해제
  (`JarvisTodoStore.Instance.Changed += Refresh`).

### Null-가드 정적 헬퍼
참조가 비어도 죽지 않도록 작은 정적 헬퍼를 둔다 (`CharacterDetailController` 하단 참고):

```csharp
private static void SetText(TextMeshProUGUI t, string v) { if (t != null) t.text = v; }
private static void SetActive(GameObject go, bool on)     { if (go != null) go.SetActive(on); }
```

### 리스트 / 반복 행
- 비활성화된 **템플릿(sample) 자식**을 `Instantiate`해서 행을 만든다.
  `JarvisTodoListUI.FindRowTemplate()`은 이름에 `sample`/`template`이 들어가거나 비활성인 자식을 템플릿으로 본다.
- `Refresh` 때마다 기존 행을 `Destroy`하고 다시 만든다 (`ClearRows` 패턴).
- 행 스크립트는 `Setup(...)` 또는 `Bind(item, owner)`로 데이터와 콜백(`Action<T>`)을 주입받는다.

### 입력 (클릭 vs 드래그 vs 롱프레스)
포인터 핸들러는 `IPointerDownHandler`/`IDragHandler`/`IPointerUpHandler` 등을 구현한다.
- 짧은 클릭과 드래그 구분: 이동 픽셀 한계(`clickMoveLimit`/`cancelMovePixels`)와 시간으로 판정
  (`PomodoroUIHandler`, `AlarmListItemView`).
- 롱프레스: `Update`에서 `Time.unscaledTime - pointerDownTime` 비교
  (`AlarmListItemView.Update`, CharacterDetail 롱프레스 진입).
- 스크롤뷰 안의 행은 드래그를 부모 `ScrollRect`로 포워딩한다 (`AlarmListItemView`의 `forwardingScrollDrag`).

---

## 3. UIManager 연동

모든 관리 대상 UI는 `Assets/Scripts/UIManager.cs`를 통해 표시/숨김된다.
새 Skills UI를 붙이려면:

1. **필드 추가**
   ```csharp
   [SerializeField] public GameObject skills; // Skills UI
   ```
2. **`Awake`에서 해석 + 초기 비활성화**
   ```csharp
   skills = ResolveManagedUI(skills, "Skills");
   SetInitialInactive(skills);
   ```
   `ResolveManagedUI`는 씬 오브젝트면 그대로, 프리팹이면 `CanvasManager.Instance.canvasUI` 아래로 Instantiate, 둘 다 없으면 이름으로 씬 검색까지 해준다.
3. **Show/Close/Toggle 메서드 추가** — 둘 중 하나를 고른다:
   - **단순 UI (UIWidget 미사용, SetActive 기반)**: `Pomodoro`/`Alarm`처럼 `ShowSimpleUI(target, "skills")` / `CloseSimpleUI(target)` 사용.
   - **타입 컨트롤러가 있는 UI**: `TODOList`/`Calendar`처럼
     `GetOrCreateTypedManagedUI<SkillsUI>(ref skills, "Skills", "skills")`로 컨트롤러를 얻어 `.Show(...)` 호출.
   - **UIWidget 애니메이션 UI**: `CharacterDetail`/`charChange`처럼 `UIWidget.Show()`/`Close()` 직접 호출 (`ShowManagedUI`/`CloseManagedUI`).

   ```csharp
   public void ShowSkills()  { skills = ResolveManagedUI(skills, "Skills"); ShowSimpleUI(skills, "skills"); }
   public void CloseSkills() { CloseSimpleUI(skills); }
   public void ToggleSkills(){ if (skills != null && skills.activeSelf) CloseSkills(); else ShowSkills(); }
   ```

> `menuName`("skills")은 위치 조회 키다. 4번 항목과 문자열을 일치시킨다.
> UI 인스턴스는 항상 `CanvasManager.Instance.canvasUI` 아래에 부모로 놓인다.

---

## 4. UIPositionManager 위치 등록

`Assets/Scripts/UIPositionManager.cs`의 `GetMenuPosition(string menuName)` switch에 case를 추가한다.
좌표는 `canvas.transform.TransformPoint(new Vector3(x, y, 0f))`로 반환한다.

```csharp
case "skills":
    return canvas.transform.TransformPoint(new Vector3(300f, 0f, 0f));
```

UIManager가 UI를 처음 켤 때 이 위치를 `RectTransform.position`에 적용한다.
화면 모서리 기준 헬퍼(`GetCanvasPositionRight()` 등)도 활용 가능.

---

## 5. UIWidget (Devion Games) 사용 여부

`DevionGames.UIWidgets.UIWidget`은 알파+스케일 트윈으로 열고 닫는 애니메이션 베이스다
(`Assets/Devion Games/UI Widgets/Scripts/Runtime/UIWidget.cs`).

- `CanvasGroup`을 **필수**로 요구한다(`[RequireComponent]`).
- `Show()` / `Close()` / `Toggle()` / `Focus()` 제공, KeyCode 토글·사운드·포커스 옵션을 인스펙터에서 설정.
- 커스텀 위젯은 `OnAwake` / `OnStart` 오버라이드로 초기화한다(생성자 대신).
- `WidgetUtility.Find<T>(name)`으로 이름 기반 조회가 가능하다.

**선택 기준:**
- 부드러운 팝업 애니메이션·포커스 관리가 필요하면 UIWidget 상속 또는 프리팹에 부착.
- 단순히 켜고 끄기만 하면 평범한 `MonoBehaviour` + `SetActive`로 충분하다
  (`Pomodoro`/`Alarm`/`TODOList`/`Calendar`가 이 방식).

---

## 6. 체크리스트 (새 Skills UI 추가 시)

- [ ] `Skills/<기능명>/Scripts/`, `Skills/<기능명>/Prefabs/` 폴더 생성
- [ ] 프리팹 루트 이름 = `ResolveManagedUI`에 넘길 이름과 동일하게 (`"Skills"` 등)
- [ ] 컨트롤러에 `Show`/`Hide`(또는 `Close`)/`Refresh` 구현, 이벤트 구독·해제 짝 맞춤
- [ ] `Awake`에서 자식 자동 바인딩(`FindDeepChild`), null-가드 헬퍼 사용
- [ ] TextMeshPro 사용 (레거시 `Text` 금지)
- [ ] `UIManager`에 필드 + `Awake` 해석 + Show/Close/Toggle 메서드 추가
- [ ] `UIPositionManager.GetMenuPosition`에 `case` 추가 (menuName 문자열 일치)
- [ ] 리스트가 있으면 비활성 템플릿 자식 + Instantiate + Refresh 시 재생성 패턴
- [ ] 디버그 로그는 기존처럼 `[기능][클래스]` 접두사로 (`[CharacterDetail][UIManager]`)

---

## 참고 파일

- 풀 기능 컨트롤러: `Assets/Prefabs/UI/CharacterDetail/CharacterDetailController.cs`
- 리스트 + 드래그 정렬: `Assets/Prefabs/UI/TODOList/Scripts/JarvisTodoListUI.cs`
- 리스트 행 + 클릭/드래그/롱프레스: `Assets/Prefabs/UI/Alarm/AlarmListItemView.cs`
- 드래그 이동 + 컴팩트 토글: `Assets/Prefabs/UI/Pomodoro/Scripts/PomodoroUIHandler.cs`
- 표시 관리 진입점: `Assets/Scripts/UIManager.cs`
- 위치 계산: `Assets/Scripts/UIPositionManager.cs`
- 애니메이션 위젯 베이스: `Assets/Devion Games/UI Widgets/Scripts/Runtime/UIWidget.cs`
- 계획 문서 예시: `Assets/Prefabs/UI/CharacterDetail/CharacterDetail_UI_Plan.md`, `CharacterDetail_Integration_Plan.md`
