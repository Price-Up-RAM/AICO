# Mission UI 작업 로그

> 설계: [MISSION_Design.md](MISSION_Design.md) · 카탈로그: [MISSION_Catalog.md](MISSION_Catalog.md)
> 작성: 2026-06-24

## 지금 할 수 있는 것 (구현됨)

- **데이터/로직 (런타임 무의존, 컴파일만 되면 동작)**
  - `MissionData.cs` — enum(`MissionCategory`/`MissionType`), `MissionReward`/`LocalizedText`/`MissionTier`/
    `MissionDef`/`MissionProgress`/`MissionSaveData`/`InventoryData` + 단계 계산 헬퍼.
  - `MissionCatalog.cs` — 미션 정의 **38개**(읽기 전용). `All`/`GetById`/`GetByCategory`/`IsMeta`.
  - `MissionRepository.cs` — `missions.json` 평문 저장/로드(AlarmRepository 복제).
  - `InventoryRepository.cs` / `InventoryManager.cs` — `inventory.json` CRUD 싱글톤
    (`AddGold/SpendGold/AddItem/SpendItem/AddReward/ResetAll` + `InventoryChanged`, 누적 획득/소비 통계).
  - `MissionManager.cs` — 싱글톤. `Report/ReportFlag/ClaimReward/GetProgress/GetDefs/IsClaimable`,
    인벤토리 연동(보상 적립), **메타/도전 파생 진행도 자동 계산**(카테고리 전체 달성, 누적 미션 달성,
    골드 획득/소비, 아이템 보유). 재진입 가드.
- **UI (이중 모드: Build/BindExisting)**
  - `MissionUi.cs` — 공통 팩토리/다크 팔레트(SkillView 차용).
  - `MissionTabButton.cs` — 좌측 카테고리 탭(선택 강조 + `달성/전체` 뱃지).
  - `MissionCardRow.cs` — 카드(설명/게이지/현황·목표/단계 라벨/보상 칩) + **도장 DOTween** +
    **보상 서랍**(칩 클릭 시 우→좌 scaleX 전개).
  - `MissionView.cs` — 헤더 + 좌측 탭 + 우측 카드 스크롤. 탭 전환, 보상 수령, 서랍 열림 상호배제 중재,
    도장 사운드(`AudioSource`+`stampClip`).
  - `MissionViewPrefabBuilder.cs` — `Tools/Mission/Build Mission Prefab` 베이크 메뉴.

## 구조 메모 (설계와의 차이)

- 설계 문서는 카드/탭을 별도 보조 프리팹(Jukebox 방식)으로 언급했으나, **Alarm 방식(단일 프리팹 +
  비활성 템플릿 클론)** 으로 구현했다. 카드 템플릿 `CardTemplate`(비활성)을 런타임에 `Instantiate`해 채운다.
  탭 5개는 `TabColumn`에 정적으로 베이크된다.
- 진행 저장은 **평문 JSON**(HMAC은 develop까지 보류, 설계 §6.1).
- 도장/서랍 애니메이션은 DOTween(`Assets/Plugins/Demigiant/DOTween`).

## 내가(사용자가) 해야 할 일

1. **에디터 리로드** — 이 환경은 Auto Refresh가 꺼져 있다. 스크립트가 컴파일되도록 한 번 리로드하고
   콘솔에 **컴파일 에러가 없는지** 확인. (에이전트는 Unity를 직접 띄우지 않음 — 베이크/플레이는 사용자 몫)
2. **프리팹 베이크** — 메뉴 `Tools → Mission → Build Mission Prefab` 실행.
   → `MissionView/Prefabs/MissionView.prefab` 생성(에디터에서 보이고 편집 가능).
3. **확인용 씬 배치(선택)** — Canvas 아래에 `MissionView.prefab`을 놓고 Play.
   - 좌측 탭 5개(첫걸음/대화/교감/생활/도전) 클릭 → 우측 카드가 카테고리별로 바뀌는지.
   - 카드의 `받기` 클릭 → 보상 적립 + **도장이 찍히는 연출**(사운드는 `stampClip` 지정 시).
   - 보상 여러 개인 카드의 칩 클릭 → **서랍이 좌측으로 펼쳐지는지**.
   - `stampClip`에 효과음 에셋을 지정하면 도장 시 사운드 재생.
4. **(추후) 게임 이벤트 연동** — 대화/설정/입력 시스템에서 `MissionManager.Instance.Report("CV0007")`,
   `ReportFlag("OB0003")` 등 호출 훅 배선. UIManager/UIPositionManager 연계(Skills CLAUDE.md 절차).

## 남은 것 / 열린 결정

- 실제 게임 이벤트 → `Report` 훅 배선(감정 분류·친밀도·캐릭터 변경 등).
- item1~3의 정체/아이콘, 보상 칩 아이콘화(현재 텍스트 `G/i1/i2/i3`).
- `Increment` 레벨 보상 점증, 메타 보상 수치 밸런싱.
- UIManager/UIPositionManager 등록(열기 버튼/위치).
- 도장 이미지·효과음 에셋(현재 색상 원형 + "달성" 텍스트, 무음 가드).
</content>
