# SkillView 사용 정리

## 관련 파일

- Prefab: `Assets/Prefabs/UI/Skills/SkillView/Prefabs/SkillView.prefab`
- View script: `Assets/Prefabs/UI/Skills/SkillView/Scripts/SkillView.cs`
- Server/client bridge: `Assets/Prefabs/UI/Skills/SkillView/Scripts/SkillCatalogClient.cs`
- Menu entry: `Assets/Scripts/MenuTrigger.cs`
- UI open/close manager: `Assets/Scripts/UIManager.cs`
- Initial position key: `Assets/Scripts/UIPositionManager.cs`
- Server route: `GET /skills/list`

## 호출 흐름

1. `MenuTrigger`의 Function 메뉴에서 `Skill` 항목 클릭
2. `UIManager.Instance.ShowSkill()` 호출
3. `ShowSimpleUI(skill, "skill")`로 `SkillView` 활성화 및 위치 지정
4. `SkillCatalogClient.OnEnable()`에서 이벤트 구독 후 `ReloadCatalog()` 호출
5. `GET /skills/list?lang={lang}` 호출
6. 응답을 `SkillView.SkillEntry` 목록으로 변환
7. `SkillView.SetSkills(entries)` 호출
8. `SkillView.Refresh()`에서 카테고리 필터, 스킬 목록, 배지, 본문 갱신

이미 열린 상태에서 메뉴로 다시 호출하면 `UIManager.ShowSkill()`이 `SkillCatalogClient.ReloadCatalog()`를 다시 호출한다.

## 프리팹 바인딩 방식

`SkillView.Awake()`는 `InputArea`가 있으면 프리팹에 이미 UI가 구성되어 있다고 보고 `BindExisting()` 경로를 탄다. 이때 새로 전체 UI를 만들지 않고, 이름 기반으로 프리팹 child object를 찾아 이벤트를 연결한다.

`BindExisting()`에서 직접 찾는 주요 이름:

- `HeaderTitleText`: 화면 제목. `Skills` 고정 표시
- `NameInput`: custom skill 이름 저장용 hidden input
- `PreviousSkillButton`: 이전 skill 이동
- `SkillDropdown`: skill 선택
- `NextSkillButton`: 다음 skill 이동
- `CategoryFilterDropdown`: `category` 기준 필터
- `LanguageDropdown`: `/skills/list` 호출 언어 선택
- `InputArea`: description 또는 custom body 표시/편집
- `SaveButton`: custom skill 저장
- `ReloadButton`: 현재 선택 skill 내용을 다시 화면에 반영
- `DeleteButton`: custom skill 삭제
- `CloseButton`: UI 닫기
- `NewButton`: 새 custom skill 생성
- `RefreshButton`: 서버 목록 다시 불러오기
- `TagArea/Content`: source, category, image badge 표시 영역

Alarm UI와 비슷하게, `PreviousSkillButton`, `NextSkillButton`, `CategoryFilterDropdown`, `NewButton`이 프리팹에서 누락되어 있으면 `EnsurePrefabBoundControls()`가 `SelectorRow` 아래에 추가한다. 이미 있으면 기존 프리팹 오브젝트를 그대로 사용하고 listener만 다시 연결한다.

## 데이터 모델

`SkillView.SkillEntry`

- `id`: API name 또는 custom skill key
- `displayName`: UI 표시 이름
- `source`: `server`, `unity`, `custom`
- `category`: category badge 및 filter 값
- `description`: read-only skill 설명
- `requireImage`: image badge 표시 여부
- `parameters`: read-only skill parameter 목록
- `content`: custom skill body
- `IsEditable`: `source == "custom"`일 때만 true

서버 `/skills/list` 응답 매핑:

- `name` -> `id`, `displayName`
- `source` -> `source`
- `category` -> `category`
- `description` -> `description`
- `require_image` -> `requireImage`
- `parameters[].name/type/required/description` -> `SkillParam`

## 이벤트 발동 시점

- `SkillSelected`
  - 발동: `SkillDropdown` 변경, 이전/다음 버튼 클릭, category filter 변경
  - 동작: 선택 인덱스 변경, delete confirm 상태 초기화, badge/content 갱신

- `RefreshRequested`
  - 발동: `RefreshButton` 클릭
  - 구독자: `SkillCatalogClient`
  - 동작: `ReloadCatalog()`로 `/skills/list` 재호출

- `LanguageChanged`
  - 발동: `LanguageDropdown` 변경
  - 구독자: `SkillCatalogClient`
  - 동작: `lang` 변경 후 `ReloadCatalog()`

- `SaveRequested`
  - 발동: `SaveButton` 클릭
  - 조건: 현재 skill이 `custom`
  - 구독자: `SkillCatalogClient`
  - 동작: local 저장 후 `POST /skills/custom`

- `DeleteRequested`
  - 발동: `DeleteButton` 두 번째 클릭
  - 조건: 현재 skill이 `custom`
  - 구독자: `SkillCatalogClient`
  - 동작: local 삭제 후 `DELETE /skills/custom/{key}`

## 버튼/UI 사용법

- Header
  - `HeaderTitleText`: `Skills` 고정 표시
  - `Header`: `DragUIHandler`가 붙어 있어 드래그 핸들 역할
  - `CloseButton`: SkillView 닫기

- SelectorRow
  - `PreviousSkillButton`: 현재 필터 안에서 이전 skill로 이동
  - `SkillDropdown`: 현재 필터 안의 skill 선택
  - `NextSkillButton`: 현재 필터 안에서 다음 skill로 이동
  - `CategoryFilterDropdown`: `All` 또는 category별 필터
  - `NewButton`: 새 custom skill 생성. 생성 시 필터는 `All`로 돌아감
  - `RefreshButton`: 서버 목록 재호출
  - `LanguageDropdown`: 서버 목록 호출 언어 변경

- Content/Input area
  - `server`, `unity` skill: read-only description 및 parameters 표시
  - `custom` skill: body 편집 가능
  - `NameInput`은 hidden 상태로 유지되며 custom skill 이름 저장용 바인딩 대상이다.

- CRUD row
  - `DeleteButton`: custom skill만 활성화. 첫 클릭은 삭제 확인, 두 번째 클릭은 실제 삭제
  - `SaveButton`: custom skill만 활성화. 이름/body를 local 및 server에 저장
  - `ReloadButton`: 현재 선택 skill 내용을 다시 화면에 반영. Pomodoro Reset sprite를 아이콘으로 사용

## 주의사항

- 프리팹 child 이름을 바꾸면 `SkillView.cs`의 이름 기반 바인딩이 끊길 수 있다.
- `SkillView.prefab`은 baked hierarchy가 있으므로 일반 실행에서는 `Build()`가 아니라 `BindExisting()` 경로를 사용한다.
- category filter는 `skills` 원본 목록을 삭제하지 않고 `visibleSkills` 화면 목록만 다시 만든다.
