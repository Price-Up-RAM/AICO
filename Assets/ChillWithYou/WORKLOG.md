# ChillWithYou WORKLOG

앉아서 같이 포모도로 하는 시스템. 오피스 리소스(Desk_Set)는 Synty Polygon Office 발췌.

## Char_toon(Generic 리그) 착석 지원 — CharAvatarSO (2026-07-22)

착석 클립(SitTyping/SitLookAround)은 휴머노이드 muscle 클립이라 Generic 리그(CH0***/Original 계열,
프리팹 Animator의 Avatar가 NULL)에는 자세가 바인딩되지 않는다. 해결: **착석 구간 한정 아바타 스왑.**

- **CharAvatarSO** (`Scripts/CharAvatarSO.cs`, 에셋 `ScriptableObjects/CharAvatarSO.asset`):
  charcode→휴머노이드 Avatar 매핑 + `fallbackAvatar`(SimpleBAAvatar). 프리팹에 아바타를 영구
  할당하면 평상시 generic 경로 커브 애니(Cafe_Idle 등)가 죽기 때문에 반드시 런타임 스왑 전용.
- **ChillModeManager**: Enter 시 `originalAvatar` 저장 → `ResolveSitAvatar()`로 스왑 → 컨트롤러 교체.
  Exit 시 아바타/컨트롤러 원복. 스왑은 **아바타 없는(Generic) 캐릭터만** 대상(명시 등록 > 폴백) —
  자체 아바타 보유 캐릭터(diana/arona 등)는 항상 불변. charcode 중복 데이터(Mika_gmod=ch0069)가
  있어도 다른 스켈레톤 아바타가 오적용되지 않게 하기 위한 게이트.
  Exit의 `Play("idle")`는 HasState 가드 추가(토온 컨트롤러에 idle 상태가 없어도 에러 없이 기본 상태 재생).
- **SimpleBAAvatar** (`Assets/CharAvatars/SimpleBAAvatar.asset`): 사용자가 CH0069 복사본(CH0069B.fbx,
  Humanoid 임포트)에서 복제해 만든 공용 아바타(구명 CH0069BAvatar). Bip001 코어 19본만 매핑.
  BA 표준 `bone_root/Bip001/...` 스켈레톤(33종)에서 경로 일치. 래퍼 노드가 있는
  CH0293(`CH0293/bone_root`)/CH0334(`bone_root/bone_CH_root`)/Momoi·Midori·Miyako·Yuuka_Original
  (`<이름>_Original/bone_root`)에는 폴백으로 바인딩되지 않음 → 전용 아바타 필요.
- **CharAvatarGenerator** (`Editor/CharAvatarGenerator.cs`,
  `Tools → ChillWithYou → 5. Generate Char Avatars (CharAvatarSO)`):
  Char_toon 프리팹 전수(POC 제외, 루트 charcode+Animator+아바타 NULL 대상, charcode 중복 제거) →
  캐릭터별 아바타 생성 + SO upsert + ChillModeManager.prefab 배선. 멱등.
  1) importer 경로: FBX 임시 복사 → Humanoid 임포트(자동 매핑) → Avatar 독립 에셋화 → 임시 삭제.
  2) builder 경로: 사람 본 경로가 프리팹 계층과 다르면 AvatarBuilder로 프리팹 계층에서 직접 생성
     (route=builder로 로그 — 프리팹 저장 포즈가 T포즈가 아니면 품질 저하 가능, 시각 확인 대상).
  batchmode: `-executeMethod CharAvatarGenerator.GenerateBatch`
- **남은 것**: 토온 캐릭터 ChillSitData 착석 오프셋 튜닝(SitSupport 패널로 캐릭터별 저장),
  builder 경로 산출물 자세 시각 검수, SitTyping이 diana 비율 기준이라 chibi 손-키보드 정렬 확인.

## 시점 프리셋 (2026-07-15)

- **ChillSitData.viewPresets**: 책상 배치(deskPositionOffset/RotationOffset/ScaleMultiplier)의 저장
  슬롯 리스트(시점 1~3, 캐릭터 무관). [데이터 저장]으로 디스크 영속.
- **ChillModeManager**: `SaveViewPreset(i)` = 현재 배치를 슬롯에 기록,
  `ApplyViewPreset(i)` = 슬롯 배치로 전환. 착석 중이면 **시트(착석 캐릭터)를 고정점으로
  0.5초(viewTransitionSeconds) 부드럽게 보간** — 캐릭터는 제자리(앵커 직선 이동), 책상이 주위로
  회전/확대되는 시점 전환 연출. 비착석이면 값만 반영(다음 착석 때 적용). 빈 슬롯은 false.
  `IsViewTransitioning`으로 UI가 전환 중 책상 입력을 잠근다(스테일 앵커 덮어쓰기 방지).
- **SitSupport 패널**: "시점" 섹션 — [시점 1~3](빈 슬롯 회색) + [저장 1~3]. 적용 시 턴테이블 자동
  정지, 전환 완료를 폴링해 즉시 슬라이더/앵커 재동기.
- 본편 활용: 트리거/코드에서 `ChillModeManager.Instance.ApplyViewPreset(n)` 호출만 하면 됨.
- **메뉴 진입**: 우클릭 Mode 서브메뉴에 **포모도로 자세 1~3** 항목(빈 슬롯 회색) — 클릭 시 포모도로
  모드 진입 후 해당 시점 적용, 이미 모드 중이면 재착석 없이 시점만 부드럽게 전환.
  (MenuTrigger.BuildPomodoroPoseItem, OperatorMenuTrigger에 복제)

## Pomodoro 모드 (본편 통합 — 2026-07-13)

- **ChatMode.Pomodoro** 추가 (`ChatModeManager`): 진입 = ChillModeManager.EnterChillMode(착석) +
  UIManager.ShowPomodoro(타이머 UI, 시작은 유저 수동). 종료 = 착석 해제 + ClosePomodoro.
  **모든 모드 전환은 Chat 기저 상태를 경유** (SetMode가 Exit→CurrentMode=Chat→Enter 순서 보장).
- **진입점**: 캐릭터 우클릭 메뉴 Function → Pomodoro (토글, 기존 타이머-열기 항목을 모드 토글로 교체).
  모드 중 Character → Change Char/Summon Char는 회색 비활성. Operator 메뉴는 모드 배타라 미러링 불필요.
- **채팅 차단**: APIManager의 3개 진입점 게이트 — CallConversationStream(채팅창/음성/Idle Talk),
  CallSmallTalkStream(선톡, 풍선 피드백 없음), CallMiniGame20QStream(20Q 우회 방지).
  `IsChatBlockedByPomodoro()` 한 곳에 모여 있어 추후 "모드 전용 일방 발화"는 이 게이트를 우회하는
  전용 진입점으로 열면 됨. Aropla는 모드 배타(ChatModeManager)로 자동 차단.
- **Pomodoro UI 위치**: 표시할 때마다 `UIPositionManager`의 "pomodoro" 좌표로 강제 재배치되는 구조라
  씬/프리팹 이동이 불필요 — 해당 case를 캔버스 우측 상단(width/2−250, height/2−200)으로 변경.
- **SitSupport** (`Assets/Prefabs/UI/extra/SitSupport.prefab` + `SitSupportScript.cs`):
  착석 튜닝 패널의 공용 프리팹화(자체 오버레이 캔버스, 패널 기본 숨김). 본편에선 Dev → SitSupport로
  토글(에디터 전용), 데모씬에선 항상 표시. 대상 캐릭터/ChillSitData는 싱글톤에서 자동 해석,
  캐릭터 교체는 charcode 폴링으로 자동 감지. 착석 토글 버튼은 본편=ChatModeManager.ToggleMode(Pomodoro),
  데모(ChatModeManager 없음)=ChillModeManager.ToggleChillMode.
- **SampleScene 설치**: `Tools → ChillWithYou → 4. Install SitSupport Into SampleScene` —
  씬 루트에 SitSupport 인스턴스 추가(멱등). **Root260616.prefab 불가침** — 씬 파일만 수정.
- batchmode: `BuildAll`(프리팹+데모씬) / `BuildAllAndInstall`(+본편 설치).

## 아키텍처 (2026-07-12 3차 — 본편 ChillModeManager 기준으로 통합)

**단일 출처는 본편 시스템이다.** 데모는 자체 착석 로직 없이 본편 코드를 그대로 구동하는 튜닝 환경.

- **ChillModeManager** (`Scripts/ChillModeManager.cs`, 프리팹 `Prefabs/ChillModeManager.prefab`):
  진입 시 현재 캐릭터의 원상태(부모/위치/회전/스케일/컨트롤러)를 저장하고 FallingObject·PhysicsManager를
  멈춘 뒤, 캐릭터를 `chairSeatPoint`(의자 하위 RectTransform)에 SetParent → ChillSitData의 charcode별
  오프셋 적용 → HY Motion Animator로 교체 → SitTyping 재생. 종료 시 전부 복원.
  Desk_Set은 평소 transform 0으로 접혀 있다가 진입 시 desk 오프셋(위치/회전/×배율)으로 펼쳐진다.
  - 데모 호환 확장(이번 작업): `overrideCharacter`(CharManager 부재 시 대상 직접 지정),
    CharManager/PhysicsManager null 가드, `IsChillMode` 공개 프로퍼티.
- **ChillSitData** (`ScriptableObjects/ChillSitData.asset`): charcode별
  {positionOffset, rotationOffset, scaleMultiplier(착석 스케일 절대값), chairLocalPosition/Rotation} + defaultOffset.
  본편 charcode 기준: diana / arona_tripo(=arona_6_clean) / arona(=arona_sfm) / aico / mari_pajama / kkum / amber / jonryo.
- **HY Motion Animator** (`Materials/Animation/HY Motion Animator.controller`):
  SitTyping(기본) ↔ SitLookAround. `LookAround` 트리거 → SitLookAround 1회 재생 후 복귀.
  ChillModeManager가 8~20초 랜덤으로 트리거. 클립은 추출본 `SitTyping.anim`/`SitLookAround.anim`
  (원본 diana FBX 2종은 저장소에서 삭제됨).
  - **배선 수정(이번 작업, 빌더가 멱등 적용)**: 원본은 트리거를 소비하는 전이가 없고 SitLookAround에서
    나오는 전이도 없어 LookAround가 재생되지 않았다 → 트리거 조건 전이 + 복귀 전이 추가.
- **chairSeatPoint**: Desk_Set 프리팹 안 의자 하위 RectTransform(스케일 0.1, 시트 높이 0.44).
  본편 SampleScene에는 Desk_Set + ChillModeManager가 이미 배선되어 있고 에디터에서 **7키**로 토글(ChillModeTestManager).

## 데모씬 (`ChillWithYouSample.unity`)

- 본편 Canvas_Char 환경(Main Camera FOV 10/투명 배경/레이어 3+6, Screen Space-Camera plane 100,
  스케일러 2560x1440) + Desk_Set(본편처럼 transform 0) + ChillModeManager 프리팹(참조 배선) +
  데모 캐릭터(POC) + 좌측 상단 튜닝 UI.
- Play 시 0.5초 후 자동 착석. UI(`ChillWithYouDemoController`)는 ChillModeManager의
  `SetCharacterOffset`/`SetChairOffset`/`ApplyDeskOffset`을 호출해 **ChillSitData에 직접 기록**:
  - [착석/일어나기]·[멈추기] / 캐릭터 착석 오프셋(위치 XYZ, 크기, 회전 Y) / 의자 오프셋(XYZ)
  - 책상: 위치 X/Y 슬라이더(deskPositionOffset) + 각도(턴테이블 30°/s·정면·±15° + 각도 표시).
    **회전은 chairSeatPoint(착석 캐릭터)를 고정점으로 자전** — 회전으로 시트가 밀린 만큼
    deskPositionOffset을 보상한다(Desk_Set 루트 피벗이 소품에서 ~7유닛 떨어져 있어 보상 없이는 공전함).
    비착석 중에는 슬라이더 잠금·회전 무시(ApplyDeskOffset이 no-op이라 값만 어긋나는 것 방지).
  - [리셋]: 시작 시점 스냅샷(현재 캐릭터 착석값 + 책상 포즈) 복원. [정면]: 책상 포즈만 복원.
  - [데이터 저장]: ChillSitData 에셋을 디스크 저장(에디터 전용) — **데모 튜닝값이 그대로 본편 데이터**.
    책상 포즈(deskPosition/RotationOffset/ScaleMultiplier)는 ChillSitData가 아니라 **ChillModeManager
    프리팹 필드**라서 [값 로그]로 찍어 프리팹에 수동 반영해야 한다.
  - 캐릭터 변경: Diana(diana) / Arona6(arona_tripo) / SFM(arona) — 일어나기→교체→재착석, 의자·책상 불변
- POC 프리팹(데모용 캐릭터 복사본, charcode는 본편과 동일):
  - `Prefabs/AICO_POC.prefab` ← diana_rigging (charcode diana)
  - `Assets/Prefabs/Char_toon/arona_6_clean_POC2.prefab` (charcode arona_tripo)
  - `Assets/Prefabs/Char_toon/arona_sfm_POC.prefab` (charcode arona)
  - 처리 내용: 매니저 의존 마스코트 스크립트 제거(FallingObject/MenuTrigger/Click·Drag·Wheel핸들러/
    AnimationController/EmotionFaceArona*) + missing script 정리 + 레이어 3.
    **컨트롤러/스케일은 원본 유지** — 착석 시 교체는 ChillModeManager의 몫. 원본 프리팹 불변.

## How to use

1. `Tools → ChillWithYou → 1. Build POC Prefabs (Diana + Arona)` — POC 3종 재처리 + 컨트롤러 배선 수정(멱등).
2. `Tools → ChillWithYou → 2. Build ChillWithYouSample Scene` — 씬 재베이크(ChillSitData 튜닝값은 SO라 보존).
3. `ChillWithYouSample.unity` Play → 자동 착석 → 슬라이더로 캐릭터/의자/책상 조정 →
   캐릭터별로 **[데이터 저장]** → 본편 SampleScene에서 7키로 확인.
- batchmode: `-executeMethod ChillWithYouSampleBuilder.BuildAll`

## 폐기된 것 (3차에서 제거)

- 구 데모 전용 시스템: ChillWithYouSeatAnimator(5초 랜덤 CrossFade), ChillWithYouSeatCatalog(SO),
  AICO_POC_Animator.controller — 본편 ChillModeManager/ChillSitData/HY Motion Animator로 대체.
  ChillSet/ChillWithYou 루트 계층(전체 스케일 노드)도 본편 방식(desk 오프셋 ×배율)으로 대체.

## 남은 것

- 캐릭터 3종 착석값 튜닝 확정(사용자 Play 작업) — 특히 arona(arona_sfm)는 ChillSitData에
  전용 엔트리가 없어 defaultOffset로 시작(저장 시 자동 생성됨)
- 본편 진입점: 우클릭 메뉴(MenuTrigger)에 Chill 항목 추가, ChatMode.Chill 연동 여부 결정
- 포모도로 타이머 UI/로직, PlayMusic 구현
