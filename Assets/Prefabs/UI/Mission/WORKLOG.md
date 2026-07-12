# Mission UI 작업 로그

> 설계: [MISSION_Design.md](MISSION_Design.md) · 카탈로그: [MISSION_Catalog.md](MISSION_Catalog.md)
> 작성: 2026-06-24

## 지금 할 수 있는 것 (구현됨)

- **데이터/로직 (JSON 폐지, 메모리 전용)**
  - `MissionInfo.cs` — enum(`MissionTab`/`MissionType`), `MissionReward`/`LocalizedText`/`MissionTier` +
    **MissionInfo(정의+런타임 상태 통합)** + 단계 계산 헬퍼(`NextTarget`/`Claimable`/`Progress01`/`AllDone`).
  - `MissionList.cs` — **싱글톤. `List<MissionInfo>`를 코드(`BuildMissions`)로 1줄씩 보유(38개)**.
    `GetByTab/GetById/Report/ReportFlag/ClaimReward/TestIncrement/ResetAllProgress/GetTabCounts/IsClaimable`,
    인벤토리 연동(보상 적립), **메타/도전 파생 진행도 자동 계산**. 저장 없음(메모리). 재진입 가드.
  - `InventoryManager.cs` — **gold 단일 재화** 싱글톤(`inventory.json` 영속). 아이템(i1~3) 재화는 폐지.
    `EarnGold/AddGold/RefundGold/SpendGold/AddReward/ResetAll` + `InventoryChanged`, 누적 획득/소비 통계.
    - **Earn/Add/Refund 의미론**:
      - `EarnGold` = 소득. 잔액 + 누적 획득(`goldEarnedTotal`) 동시 가산 → CH0001(골드 모으기) 반응.
      - `AddGold` = 순수 잔액 변경(0 하한). 누적 집계 무반응 — 미션류에 잡히지 않는 db성 변경(치트/데모 지급 등).
      - `RefundGold` = 실패한 결제 되돌림. 잔액 복구 + 누적 소비(`goldSpentTotal`) 차감.
        **환불 시 CH0007(골드 소비하기) 진행 후퇴는 의도된 동작**(실패 결제는 소비가 아님).
      - `AddReward`(미션 보상)의 gold는 Earn 의미(양수면 누적 획득 가산).
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
- **영속성**: 진행도는 `missions.json`(id/current/claimedTiers), 재화는 `inventory.json`(평문). 정의는 코드(`MissionDatabase`)에만.
- **집계 방식**: `Report`(누적) / `ReportFlag`(단발) / `ReportBest`(한 세션 최고치, 예 "한 번의 대화에 바나나 5회") / 인벤토리·메타는 `UpdateDerived` 자동.
- 미션 정의는 `MissionDatabase.Build()`에 코드로 1줄씩. 추가/수정은 여기서(프리팹 재베이크 불필요, UI 구조 바꿀 때만 베이크).
- **보상은 gold 단일**: 아이템(i1~3) 재화·보상 성분을 폐지하고, 아이템 성분이 있던 티어는 **티어당 +100G로 일괄 환산**.
  '아이템 모으기' 도전 미션(구 `cha_item_own`)은 DB에서 삭제 — 저장 파일(`missions.json`)에 남은 해당 진행도는
  `LoadProgress`가 미지 id로 스킵하고 다음 저장에서 자연 소멸. 보상 셀 순환(다중 보상 페이드)도 제거(단일 셀 고정).
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
4. **(추후) 게임 이벤트 연동** — 대화/설정/입력 시스템에서 `MissionList.Instance.Report("CV0007")`,
   `ReportFlag("OB0003")` 등 호출 훅 배선. UIManager/UIPositionManager 연계(Skills CLAUDE.md 절차).

## 남은 것 / 열린 결정

- 실제 게임 이벤트 → `Report` 훅 배선(감정 분류·친밀도·캐릭터 변경 등).
- 보상 칩 아이콘화(현재 골드 아이콘 미지정 시 텍스트 폴백 `G50`).
- `Increment` 레벨 보상 점증, 메타 보상 수치 밸런싱.
- UIManager/UIPositionManager 등록(열기 버튼/위치).
- 도장 이미지·효과음 에셋(현재 색상 원형 + "달성" 텍스트, 무음 가드).
</content>
