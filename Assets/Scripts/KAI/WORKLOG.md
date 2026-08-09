# KAI — 제출용 프로토타입 (SampleSceneKAI)

SampleScene을 완전 복제한 제출용 씬. 완성된 기능만 노출하며, **기존 스크립트·프리팹·씬은 일절 수정하지 않는다.**
변조가 필요한 코드는 이 폴더에 복사해 변조한다(원본 불변 원칙).

## 구성

| 파일 | 역할 |
|---|---|
| `MenuTriggerKAI.cs` | `Assets/Scripts/MenuTrigger.cs` 사본. 메뉴를 제출용으로 정리 |
| `KAIManager.cs` | 씬 전용 매니저. ① 캐릭터를 AICO로 고정 ② 씬 내 모든 MenuTrigger를 MenuTriggerKAI로 in-place 교체 (SubCharManager의 교체 패턴 준용) |
| `Editor/KAISceneBuilder.cs` | `Tools → KAI → Build SampleSceneKAI`. SampleScene 복제 + KAIManager 루트 오브젝트 추가 + KAI 전용 직렬화 오버라이드(VL Router·UIManager.skill·KAIManager.storePanelPrefab) (멱등: 재실행 시 최신 SampleScene 기준 재생성) |

## MenuTriggerKAI 메뉴 구조

```
Settings / Character Detail / Action
Function ▸ Inventory · Store · Skill
Chat    ▸ New Chat · Chat History · Idle Talk
Mode    ▸ Chat · Pomodoro · Operator (현재 모드 회색)
Control ▸ Show Voice Panel · Show/Hide TalkInfo · Set Screenshot Area
Exit
```

원본 대비 제외: Character(변경/소환/의상/코스튬), Guideline, Situation, OCR,
Experiment/Dev/Debug, Version. Function은 Inventory/Store/Skill 구성으로 복원.

### Function 메뉴 배선 (2026-08-09)

- **Inventory**: `UIManager.ToggleInventory()` — 본편에 이미 완전 배선돼 있어 그대로 사용.
- **Skill**: `UIManager.ToggleSkill()` — Root260616.prefab의 UIManager 직렬화에 `skill` 필드가 비어 있어,
  빌더의 `ApplySkillPrefabOverride()`가 씬 인스턴스에 SkillView.prefab을 할당한다(프리팹 원본 무수정).
- **Store**: UIManager 통합이 없어 KAI 전용 경로 — `KAIManager.ToggleStore()`가 빌더가 할당한
  `storePanelPrefab`을 canvasUI 아래에 지연 인스턴스화하고 `StoreView.Show()/Toggle()`을 호출한다.
  "상점" 라벨은 LanguageData 미등록이라 파일 내 `GetStoreMenuLabel()` 헬퍼로 처리(LanguageData 무수정).
- 데이터: 카탈로그 신버전(MY-Little-Jarvis-3D 8월판)과 인형 7종·장비 아이콘은 별도 커밋으로 동기화됨.
  장비 착용 소켓은 `Aico.prefab`에 Naost판 소켓 4종(hairpin1/back1/arona_a_chipao/hat1)을 접목.

## 동작 방식 (기존 코드 무수정 근거)

- **캐릭터 고정**: AICO = `Assets/Char/Aico/Aico.prefab` (charcode `aico`, PrefabDataLocal 키 `naost`).
  PrefabDataLocal.Awake가 로컬 프리팹을 charList에 등록하므로 `ChangeCharacterFromCharCode("aico")`로 교체 가능.
  KAIManager가 1초 주기로 감시해 초기 스폰(기본 arona)·AI 의도(change_model) 등 어떤 경로로 바뀌어도 AICO로 되돌린다.
  Pomodoro 착석 중엔 CharManager가 교체를 차단하므로 시도를 보류한다.
- **메뉴 교체**: MenuTrigger는 캐릭터 프리팹 루트 + GameManager(활성) + 2DCharSample(비활성)에 부착돼 있다.
  인스펙터 직렬화 필드가 없는 자급자족형 컴포넌트라 같은 GameObject에 MenuTriggerKAI를 붙이고 원본을 Destroy하면 끝.
  캐릭터 교체 시 새 인스턴스에 딸려오는 MenuTrigger도 0.25초 주기 스윕이 처리한다.
- **Character Detail**: `CharManager.FindCharacterInfoByCharacterId(charcode 소문자)` → `UIManager.ShowCharacterDetail(charInfo, clothesList[0])`.
  라벨은 LanguageData 기등록 엔트리 조합("Character" + "Detail") — LanguageData.cs 무수정.

## How to use

1. (자동화됨) `Tools → KAI → Build SampleSceneKAI` 실행 → `Assets/Scenes/SampleSceneKAI.unity` 생성.
2. SampleSceneKAI 열고 Play → 시작 캐릭터가 AICO로 고정되는지, 우클릭 메뉴가 정리된 구성인지 확인.
3. SampleScene이 갱신되면 1번을 재실행해 KAI 씬을 다시 동기화한다 (씬에 추가 수작업이 있었다면 날아가니 주의).

## 알려진 한계 / 남은 것

- 최초 실행 시 CharManager가 기본 캐릭터(arona)를 먼저 스폰한 뒤 ~1초 내 AICO로 교체된다
  (CharManager 무수정 제약 때문. 교체 이펙트가 소환 연출처럼 보임).
- AICO로 교체되면 CharManager가 last_char를 `aico`로 저장한다(원본 동작). isStartWithLastChar 사용 시
  본편 SampleScene도 다음 실행에 AICO로 시작할 수 있다.
- 빌드 세팅(EditorBuildSettings)에는 추가하지 않았다. 제출용 빌드에 넣으려면 File → Build Settings에서 수동 추가.
- 씬은 Root260616 프리팹 인스턴스를 원본과 공유한다. 프리팹 수정은 양쪽 씬에 반영되므로 주의.
