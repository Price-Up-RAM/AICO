# CharacterDetail — 작업 이력 / 사용법

계획 문서: `Affinity_Plan.md` (인연도 시스템 전환) / 상점 연동: `Affinity_Store_Integration.md`

## 2026-07-11 (2차) — 표시/게이지/모달 피드백 반영

- **표기 분할**: "인연도 Lv.0 / 0/100" 2줄 → 가로 1줄 `[Lv.03] [50/100]`.
  `AffinityLevelText`(골드, 20pt, Lv.00 제로패딩, MAX면 "Lv.MAX") + `AffinityValueText`(16pt, MAX면 빈 문자열).
  "인연도"라는 단어는 제거. 모달 표기도 `Lv.01` 형식으로 통일.
- **게이지**: 평시 = **연노랑 단색**(구 호감도 Yellow `(1, 0.827, 0.357)`), **MAX일 때만 무지개** 그라데이션.
  fill 2종(`AffinityBarFill` 연노랑 / `AffinityBarFillMax` 무지개)을 트랙 안쪽 **2px 인셋**으로 베이크 —
  트랙(어두운 배경)이 테두리로 보이고, fill이 트랙보다 커 보이던 문제 해결
  (원인: 구 fill은 Sliced UISprite라 10px 높이에서 시각적으로 얇아졌는데, 신 fill은 Single이라 그대로 꽉 참).
  단색 Filled용 `Sprites/AffinityBarWhite.png`(8x8) 추가 베이크.
- **모달**: 헤더 우측(X 왼쪽)에 **`전부 수령`** 버튼(도달한 미수령 레벨 일괄 수령, 골드 합산 1회 지급).
  하단 테스트 버튼 `+50` → **`+40 (테스트)`** + **`초기화 (테스트)`**(인연도 포인트만 0, 수령 상태 유지) 추가.

## 2026-07-11 — 인연도 1단계: 표시 교체 + 보상 수령 모달 (프로토타입)

호감도(affection, 0/300, 3단 바)를 인연도(affinity, Lv.N n/100, 무지개 단일 게이지)로 교체.
인연도 블록 클릭 → 레벨 보상 수령 모달(스크롤 리스트, Lv.1~10).

### 지금 할 수 있는 것

- **표시**: `인연도 Lv.N  n/100` + 우측 6단계 명칭(낯선 사이 ~ 둘도 없는 사이, 색상은 기존 핑크 유지).
  Lv.10은 `인연도 Lv.MAX`만 표시(숫자 없음), 게이지 만땅.
- **게이지**: 무지개 그라데이션(좌 빨강 → 우 보라) 단일 바 — Image Filled(Horizontal)라 진행할수록 색이 드러난다.
- **보상 모달**: 인연도 블록(카드) 클릭 → 모달. 레벨별 행 = 보상 표기 + 상태(수령/수령 완료/미도달).
  수령 시 골드는 Mission 지갑(`InventoryManager.AddGold`)으로 지급, 수령 상태는 `settings_char.json`
  (`affinityClaimedLevels`)에 charCode 단위 저장. 장신구/카드 테두리/명칭 커스텀은 **표기만** (후속 구현).
- **레벨업 미션**: 레벨 상승 시 `MissionList.Report("AF0004")` 자동 보고.
- **테스트**: 모달 좌하단 `+50 (테스트)` 버튼으로 포인트 지급(포인트 획득 규칙 확정 전 임시 — 확정 시 제거).

### 파일

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/AffinityData.cs` | 도메인 단일 출처: 100pt/레벨, Lv.10 max, 6단계 명칭, 레벨 보상 정의 |
| `Assets/Scripts/SettingCharManager.cs` | `CharCodeSetting.affinityPoints`(0~1000) + `affinityClaimedLevels`, `AddAffinityPoints`/`ClaimAffinityReward` (구 affection 제거, 마이그레이션 없음) |
| `Assets/Scripts/CharacterDetailStateManager.cs` | 상태 DTO(affinityPoints/Level/StageName) + `AddAffinityPoints`(AF0004 보고) |
| `CharacterDetailController.cs` | `SetAffinity(points)` 표시 + 인연도 블록 Button → 모달 오픈. "호감도 보유" 태그는 표시 시점에 "인연도 보유"로 치환 |
| `AffinityRewardModalView.cs` | 보상 수령 모달 (베이크 계층 BindExisting + 행 코드 생성) |
| `Editor/AffinityUiTools.cs` | `Tools/CharacterDetail/*` 메뉴 + batchmode 진입점 `BatchBuildAll` |
| `Sprites/AffinityRainbow.png` | (생성물) 무지개 게이지 스프라이트 256x16 |

### 셋업 (에디터 메뉴)

1. `Tools/CharacterDetail/1. Bake Rainbow Sprite`
2. `Tools/CharacterDetail/2. Convert Affinity UI (prefab)` — 리네임/게이지 교체/모달 베이크/컨트롤러 참조 갱신
3. `Tools/CharacterDetail/3. Apply SUIT-Bold Font`
한 번에: `Tools/CharacterDetail/Setup All`.
batchmode: `Unity.exe -batchmode -quit -projectPath <proj> -executeMethod AffinityUiTools.BatchBuildAll`

### 테스트 방법 (전용 데모 씬 없음)

SampleScene Play → 캐릭터 카드/리스트 **롱프레스(0.5초)** → CharacterDetail 열림 →
인연도 블록 클릭 → 모달에서 `+50 (테스트)` 연타 → 게이지/명칭/Lv 변화 확인 → `수령` 버튼 → 골드 증가(미션 지갑).

## 2026-07-11 (3차) — 카드 3중 테두리 구현 (ChangeChar 카드)

아래 "설계 검토"를 3중 구조로 확정·구현. 대상: `Assets/Migration/Root260616.prefab`의 "Item Slot".

- **구조**: 카드 루트 마지막 자식 3개, 전부 풀스트레치·Sliced·raycastTarget off·기본 비활성.
  1. `CardBorderImage` — 공통 테두리(외부 이미지 기반). placeholder = Layer Lab `ItemFrame01_White4`(굵은 화이트 링).
  2. `CardBorderSubImage` — 보조 테두리(White 프레임 + 유니티 틴트). placeholder = `ItemFrame01_White2`(얇은 라인).
  3. `CardBorderOriginalImage` — 전용 테두리(전용 PNG용, sprite=null 베이크).
- **우선순위**: Original.sprite 있으면 Original만 활성(나머지 강제 off) → 없으면 등급 판정:
  Lv4~6 동 `#B87333` / Lv7~9 은 `#C0C0C0` / Lv10 금 `#FFD700` (Image+Sub 활성, Sub에 틴트) / 미만 전부 off.
  판정/색은 `AffinityData.BorderTierFor/BorderTintFor`(enum `AffinityBorderTier`)가 단일 출처.
- **배선**: `ChangeCharCardController.UpdateClothesUI`에서 `CharacterDetailStateManager.BuildCharacterId(charData, currentClothes)`
  키로 판정 — **인연도 적립과 같은 키 규칙**(소문자화+이름 폴백)이라 Detail에서 올린 포인트와 매칭된다.
  `SettingCharManager.OnCharacterSettingChanged`/`OnSettingsLoaded` 구독으로 라이브 갱신(구독 인스턴스 캐시 후 OnDestroy 해제).
  전용 테두리 진입점 `SetOriginalBorderSprite(Sprite)` (호출처는 아직 없음 — 테두리 보상/변경 기능용).
- **안전장치(검증에서 잡은 치명 버그 수정)**: `SettingCharManager.GetCharCodeSetting`이 조회만으로
  파일을 저장하던 부작용 제거 + `isLoaded` 전 조회 금지 — 카드가 로드 전에 판정하면서 빈 설정으로
  `settings_char.json`을 덮어쓰는 레이스를 차단. 로드 완료 시 `OnSettingsLoaded`로 카드가 재판정.
- **베이크**: `Tools/CharacterDetail/4. Inject Card Border (ChangeChar)` (Setup All/BatchBuildAll에 포함).
  재실행 멱등(직계 자식만 스캔·제거 후 재생성), 스프라이트 로드 실패 시 저장 없이 중단.
- **알려진 캐벳**: ① 전용 테두리는 현재 charcode 무관 — 보상 시스템 배선 시 "어느 캐릭터의 전용인가"를
  함께 저장하고 대조 필요. ② 빌드 씬 목록상 SampleScene이 비활성(enabled:0)이고 MRSampleScene에는
  카드 UI가 없음 — 카드 테두리는 SampleScene(에디터 Play) 기준. ③ 기존 잠복 버그:
  `GeneratePaginationDots`는 InitSlot 재호출 시 점이 누적됨(현재 재호출 경로 없음 — 카드 재사용 도입 시 수정).
- **프리팹 저장 차단 이슈 2건 해결 (Root260616은 그동안 에디터에서 저장 불가 상태였음)**:
  ① `animationplayermanager.cs`의 첫 클래스가 비-MonoBehaviour `PlayerRuntime`이라 저장 검증기가
  "PlayerRuntime은 MonoBehaviour가 아님"으로 거부 → **클래스 순서 교체**(PlayerRuntime을 파일 하단으로,
  guid·시맨틱 불변)로 해결. 파일에 재발 방지 주석 있음.
  ② 미싱 스크립트 2개(`Canvases/Canvas/PortraitMask`, `PortraitMask/PortraitBorder` — 죽은 guid 껍데기)가
  저장 거부 → `InjectCardBorder`가 제거 후 저장(대상 경로 로그). 원본 백업: 세션 스크래치패드
  `Root260616_BACKUP_20260711.prefab`.

## 카드 테두리(동/은/금) 설계 검토 — 2026-07-11 (구현 전, 설계만)

대상: 캐릭터 선택 카드 = `Assets/Migration/Root260616.prefab`의 **"Item Slot"** 노드
(`ChangeCharManager.GenerateCharacterSlots`가 Instantiate 복제, `ChangeCharCardController` 부착).

### 조사 결과 (구조 사실)

- 카드 크기는 부모 "Slots"의 GridLayoutGroup이 강제: **120x160 고정** (카드 루트 sizeDelta는 0).
- 카드에 **Mask/RectMask2D 없음** → 풀스트레치 오버레이가 잘리지 않는다.
- 기존 "Outline" 노드(9-slice `Rounded Square Outline`)가 있지만 **자식[0] = 맨 뒤 렌더**라
  초상화 뒤에 깔림 — 등급 테두리 용도로는 부적합(배경 프레임 역할로 유지).
- 컨트롤러에 테두리 직렬화 필드 없음 → `[SerializeField] Image cardBorder` 신설 필요.
- 리스트 슬롯(`ChangeCharListSlotController`, "Item Slot_Sample")도 같은 방식 적용 가능.

### 권장 설계

1. **오버레이 위치**: "Item Slot" 루트의 **마지막 자식**으로 `CardBorderImage` 추가 —
   풀스트레치(anchor 0,0~1,1, offset 0) + `Image(Sliced, raycastTarget=0)`.
   마지막 자식 = 최상단 렌더라 초상화/이름 위에 테두리가 덮이고, 셀 크기 변화에도 자동 대응.
   루트 Button은 비활성이고 raycast는 루트 배경 Image 담당이라 입력 간섭 없음.
2. **등급 표현 (하이브리드)**: 시작은 **White 9-slice 프레임 + 색 틴트** —
   후보 스프라이트: `Assets/Layer Lab/GUI Pro-SimpleCasual/.../Frame/Frame_Custom/CardFrame01_White1~5.png`
   (9-slice 확인됨, White라 틴트 자유). 틴트: 동 `#B87333` / 은 `#C0C0C0` / 금 `#FFD700`.
   → 유저가 원하던 "색상 변경 정도"가 스프라이트 1장으로 성립.
3. **확장 구조**: 등급→모양 매핑을 `AffinityData`에 테이블로:
   `BorderTierFor(level)`(Lv4 동/Lv7 은/Lv10 금/미만 없음) + `{sprite, tint}` 엔트리.
   전용 PNG를 구해오면 엔트리의 sprite만 교체(틴트 white) — **전용 테두리 보상/테두리 변경 기능**은
   엔트리 목록을 늘리고 선택값을 `settings_char.json`(예: `cardBorderId`)에 저장하는 것으로 자연 확장.
4. **화려한 금테**(참고 이미지의 발광 느낌)는 9-slice 틴트만으로는 한계 —
   같은 프레임을 약간 확대+반투명으로 한 장 더 깔아 글로우를 흉내내거나, 전용 PNG로 해결(후속).
5. 적용 지점 2곳: ChangeChar 카드(위) + CharacterDetail 초상화 카드(같은 오버레이 패턴).
   `InitSlot`/`ApplyState`에서 `SettingCharManager`의 affinityPoints로 tier 판정.

### 남은 것 (후속)

- 포인트 획득 규칙 (대화/쓰다듬기/선물 등 — 사실상 시스템 본체, `Affinity_Plan.md` 7장)
- 카드 테두리(동/은/금) 실装, 전용 장신구 실지급(EquipSystem 등록), Lv.10 명칭 커스텀 입력 UI
- 선물 → 포인트 연동 (`Affinity_Store_Integration.md`)
- LanguageData ko/jp/en 등록 (현재 컨트롤러 전체가 한국어 하드코딩 — 일괄 과제)
- `+50 (테스트)` 디버그 버튼 제거
- CharAttributes.featureTags 원본 데이터의 "호감도 보유" → "인연도 보유" 일괄 수정(현재는 표시 치환)
