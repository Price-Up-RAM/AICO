# Skills UI 작업 로그

> 작성: 2026-06-18 · 작업자: Claude (Opus 4.8)
> 이 문서는 Skills UI 폴더의 작업 진행 상황과 결정 사항을 정리한 것이다.

---

## 1. 목표

`Assets/Prefabs/UI/Skills`에 스킬 관련 UI를 관리한다.
기존 UI(`CharacterDetail`, `Alarm`, `Calendar`, `Pomodoro`, `TODOList`)와 `Devion Games/UI Widgets`의
패턴을 따라, 어두운 테마를 유지하며 첫 번째 스킬 UI(`SkillView`)를 만든다.

---

## 2. 현재 폴더 구조

```
Assets/Prefabs/UI/Skills/
  CLAUDE.md                       <- UI 작성 가이드 (규칙 모음)
  WORKLOG.md                      <- (이 문서) 작업 로그
  SkillView/
    Scripts/SkillView.cs          <- 컨트롤러. UI를 코드로 구성
    Scripts/SkillView.cs.meta     <- guid: 5c9b1f2a3d4e4f5a8b6c7d8e9f0a1b2c
    Prefabs/SkillView.prefab      <- 루트 "SkillView" + RectTransform + CanvasGroup + SkillView
    Prefabs/SkillView.prefab.meta <- guid: 7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a
```

---

## 3. 완료한 작업

### 3-1. CLAUDE.md (UI 작성 가이드)
기존 UI들을 조사해 공통 규칙을 정리했다. 폴더 구조 / 네이밍 / 컨트롤러 패턴
(`Show`·`Hide`·`Refresh`, 자동 바인딩, null-가드 헬퍼) / UIManager·UIPositionManager 연동 /
UIWidget 사용 기준 / 체크리스트 포함.

### 3-2. SkillView.cs (컨트롤러)
`Awake`에서 어두운 테마 패널 전체를 **코드로 직접 생성**한다.
빌드 순서: `BuildHeader` → `BuildSelectorRow` → `BuildTagArea` → `BuildCrudRow` → `BuildInputArea`.

요청한 5개 요소:
1. **헤더** — 타이틀("스킬 관리") + 닫기(`×`) 버튼
2. **셀렉터 행** — 스킬 드롭다운(가변 폭) + `⟳` Refresh 버튼 + 언어 드롭다운(한국어/영어/일본어)
3. **태그 영역** — Unity/Local/Python 둥근 태그(pill), 카테고리별 색상, `×`로 제거, 폭 초과 시 가로 스크롤. 가변 리스트
4. **CRUD 행** — `저장`(accent blue) / `되돌리기`(reload)
5. **입력 영역** — 멀티라인 `TMP_InputField` + 우측 세로 스크롤바

공개 API: `Show()`, `Hide()`, `Refresh()`, `SetSkills(IEnumerable<SkillEntry>)`, `AddTagToCurrent(string)`
외부 연동 이벤트: `SkillSelected`, `SaveRequested`, `RefreshRequested`, `LanguageChanged`
데이터 없이 단독 실행되도록 `EnsureSampleData()`로 샘플 2건 제공.

### 3-3. SkillView.prefab
루트 GameObject `SkillView` (Layer 5/UI) + `RectTransform`(520×640) + `CanvasGroup` + `SkillView`.
프리팹의 `font` 필드에 프로젝트 기본 TMP 폰트(guid `8f586378b4e144a9851e7b34d9b748ee`)를 지정해
다른 UI와 글꼴을 맞췄다.

---

## 4. 핵심 결정 사항 & 이유

### (A) UI를 프리팹 YAML이 아니라 코드로 구성
- `CharacterDetail.prefab`은 9,450줄. Unity 에디터를 열 수 없는 환경에서 그 규모의 YAML을
  손으로 작성하면 fileID/GUID 오류로 프리팹이 깨질 위험이 매우 크다.
- 그래서 **결정적이고 안전한 코드 빌드 방식**을 택했다. 프리팹은 루트 + 컨트롤러만 가진 얇은 형태이고,
  실제 위젯은 런타임에 생성된다.
- 트레이드오프: 에디터에서 레이아웃을 시각적으로 편집할 수 없다.
  필요하면 이 컨트롤러가 만드는 구조를 그대로 정적 프리팹으로 펼칠 수 있다.

### (B) 다크 테마 값은 CharacterDetail에서 추출
- 루트 `#16191F`(0.086,0.098,0.125), 패널 `#23282F`, accent blue `#3E5380`(0.243,0.325,0.502) 등.
- 둥근 모서리는 프로젝트가 쓰는 **빌트인 `UISprite`**(`fileID 10905`, Sliced, PPU Multiplier 1)를
  `Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")`로 로드.

### (C) 스크롤은 UGUI 기본 컴포넌트로만 처리 (요구사항)
- 입력: `TMP_InputField`(휠 자체 지원) + `verticalScrollbar`.
- 태그/드롭다운 리스트: `ScrollRect`.
- 프로젝트 내 다른 스크립트에 의존하지 않는다.

---

## 5. 미완료 / 다음 단계

아직 **씬에 띄우는 연동은 하지 않았다** (이번 요청 범위가 프리팹+스크립트였음).
CLAUDE.md의 연동 규칙대로 하려면:

- [ ] `UIManager.cs` : `[SerializeField] GameObject skillView;` 필드 추가
- [ ] `UIManager.Awake` : `skillView = ResolveManagedUI(skillView, "SkillView"); SetInitialInactive(skillView);`
- [ ] `UIManager` : `ShowSkillView()` / `CloseSkillView()` / `ToggleSkillView()` 추가
      (타입 컨트롤러가 있으므로 `GetOrCreateTypedManagedUI<SkillView>(ref skillView, "SkillView", "skillview")` 패턴)
- [ ] `UIPositionManager.GetMenuPosition` : `case "skillview":` 추가

> 연동 시 주의: `ResolveManagedUI`/`GetMenuPosition`은 **GameObject 이름·menuName 문자열**로 찾는다.
> 프리팹 루트 이름은 `SkillView`이므로 연동 키도 `"SkillView"` / `"skillview"`로 맞출 것.

---

## 6. 검증 상태

- ⚠️ **Unity 에디터에서 import/컴파일 검증은 못 했다** (에디터 실행 불가 환경).
- 스크립트는 표준 UGUI/TMP API만 사용하도록 작성했으나, 에디터에서 한 번 열어
  컴파일·렌더링을 확인해야 한다. 특히 코드로 만든 `TMP_Dropdown` 템플릿 구조가
  실제로 펼쳐지는지(Show 동작) 확인 필요.
- `.meta` GUID는 직접 부여했다. 에디터가 import하면 폴더 `.meta`는 자동 생성된다.

---

## 7. 비고

- 작업 중 한때 `C:` 드라이브가 100%(여유 0B)가 되어 이 문서 저장이 일시 실패했고, 공간 확보 후 재작성했다.
