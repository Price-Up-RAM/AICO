# ChillWithYou WORKLOG

앉아서 같이 포모도로 하는 시스템. 오피스 리소스(Desk_Set)는 Synty Polygon Office 발췌.

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
