# Store(상점) — 기능 안내서

친밀도(Affinity_Plan) 재화 루프 검증용 **StandAlone 상점 프로토타입**.
폴더 감성 탭 상점(구매 확인 모달 + 페이징) + 인벤토리 드래그앤드롭 판매 +
포즈/이펙트 리얼타임 프리뷰 아이콘(Runtime/File 소스 · 포즈 리롤 · NoImage 폴백).
기존 시스템과는 문자열 key + 공개 API + 이벤트로만 연결되는 약결합 구조다.

- 지갑: 미션 재화 `InventoryManager`(Prefabs/UI/Mission)의 Gold.
- 아이템 보관/지급: `InventorySystemManager`(Prefabs/Assist/InventorySystem).
- 상세 설계: `Store_Design.md` / 작동 원리·프로토콜: `WORKLOG.md` / 포즈 상품 검토: `Store_PoseAnimation_Review.md`

## 1. 빠른 시작

1. 메뉴 `Tools/Store/Setup All (catalog + UI prefab + font + demo scene)` 실행.
   (개별 단계: `1. Create Catalog` → `2. Build UI Prefab` → `3. Apply SUIT-Bold Font` → `4. Build Demo Scene`.
   데모 씬은 `InventoryPanel.prefab`이 필요 — 커밋된 베이크 산출물이라 리포지토리에 있으며(생성 도구
   Tools/InventorySystem은 삭제됨), 없으면 에러 로그로 안내하고 패널 없이 계속 빌드한다.)
2. `Assets/Prefabs/UI/Store/Demo/StoreDemo.unity` 열고 **Play**.

batchmode 일괄 빌드(에디터 GUI가 닫혀 있어야 함):

```
"C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe" -batchmode -quit -projectPath "d:\unity\AICO" -executeMethod StoreTools.BatchBuildAll
```

### 조작 (데모 씬)

| 키 | 동작 |
|---|---|
| `S` | 상점 창 토글 |
| `I` | 인벤토리 창 토글 |
| `G` | +500 G |
| `1`~`4` | MAIN 인벤토리에 데모 아이템 지급 (1 치파오 / 2 선물(소) / 3 포즈: 댄스 / 4 바나나) |
| `5` | 포즈 프리뷰 리롤 (Runtime 아이콘 전 포즈 재캡처 — 매번 다른 정지 포즈) |

- **구매**: 탭 선택 → 카드 클릭 → 확인 모달에서 수량 조절("n / max", 합계 확인) → **"계산하기"**.
  골드 부족 시 토스트 + 골드 표시 빨강 플래시.
- **판매**: 인벤토리 슬롯을 상점 하단 **판매 존**으로 드래그 → 확인 모달에서 수량 선택(1~보유개수) →
  **"판매하기"**. 판매가 = 구매가의 50%(카탈로그 밖 아이템은 10 G).
- **리롤**: 포즈 탭에서 페이지바 우측 **주사위 버튼**(포즈 탭에서만 표시) 또는 키 `5`.
- 상품이 7종 이상인 탭은 그리드 하단 페이지바 `[ < n / m > ]`로 넘긴다.

## 2. 기능

- **폴더 탭 상점** — 태그 레지스트리(`Resources/StoreCatalog.asset`)의 태그 목록이 곧 탭이다
  (현재 5종: 장착물/포즈/이펙트/선물/잡화, 슬롯 최대 6 — 초과분은 경고 후 절단). 선택 탭이 본문과
  같은 색으로 이어지는 폴더 감성 UI. 탭 라벨/활성은 Refresh마다 카탈로그와 재동기화되어 태그
  추가·변경에 프리팹 리베이크가 필요 없다.
- **페이징** — 탭당 그리드 3x2(페이지당 6장) 고정, 하단 페이지바 `[ < n / m > ]`로 이동. 카드는
  좌상단부터 채운다(미완성 행도 왼쪽 정렬 — childAlignment UpperLeft).
- **구매 확인 모달** — 카드 클릭 시 수량(1~적재 여유분, "n / max" 표시)과 최종금액을 확인하고
  "계산하기". 합계가 보유 골드를 넘으면 빨강 표시, 지급 실패 시 전액 환불. 모달은 StorePanel
  프리팹에 "StoreConfirm" 자식으로 베이크되어 있다.
- **판매 드래그존** — 인벤토리 슬롯을 하단 판매 존으로 드래그하면 같은 확인 모달이 Sell 모드로
  열린다(수량 1~보유개수). 드롭 시점에는 아무것도 바꾸지 않고, 모달 확정 시에만 스택 차감 +
  골드 지급(MAIN 인벤토리만 허용, 슬롯+key 재검증).
- **프리뷰 아이콘 (포즈/이펙트)** — `iconType=Runtime`인 상품은 씬의 숨김 리그가 플레이 중
  캡처해 카드/모달에 채운다: 포즈 = 클립을 20~80% 랜덤 위치에서 정지, 이펙트 = 파티클을
  `Simulate`로 진행시킨 정지컷. 캐시는 상시 싱글톤 `StoreManager`가 보관해 창을 닫아도 유지된다.
- **아이콘 소스 모드 (File / Runtime)** — **상점 아이콘은 상점 카탈로그(StoreEntry)가 소유**한다:
  인벤토리 UI 아이콘과 완전 별개(InventoryCatalog를 조회하지 않음). 엔트리별 `iconType`:
  **File**(기본) = 등록한 `icon` 스프라이트(비면 NoImage), **Runtime** = 리그 캡처 + 리롤 대상
  (Detail 카탈로그 등재 키에서만 유효 — 미등재 Runtime 키는 NoImage로 정착).
- **카드 보조 표기 (detailText)** — 카드의 `CardSub`는 `StoreEntry.detailText` 자유 텍스트를 그대로
  표시한다(기본값은 선물 3종 "친밀도 +N"). **표시 전용** — 실제 아이템 성능 수치(선물 친밀도 등)는
  ItemSystem(`Assets/Prefabs/Assist/ItemSystem`)의 ItemGiftCatalog(`affinityPoints`)가 소유한다.
- **포즈 리롤** — 주사위 버튼/키 `5`로 Runtime 모드 포즈 전 키를 강제 재캡처. 정지 시점이
  랜덤이라 리롤마다 다른 포즈가 나온다.
- **NoImage 폴백** — 실아이콘이 없는 카드/모달은 베이크된 'NO IMAGE' 플레이스홀더를 우선 표시하고
  캡처가 끝나면 교체한다. 플레이스홀더 PNG가 없으면 아이콘을 숨기고 이름 텍스트만 남는다.
- **재화/미션 연동** — 구매 = `SpendGold` → `AddToMain`(지급 실패 시 `RefundGold`로 전액 환불 —
  실패 결제 되돌림, 누적 소비 역가산), 판매 = 스택 차감 → `EarnGold`(소득 — 누적 획득 집계).
  장착물 태그 구매 시 AF0005 미션 보고(`TagForKey` 판정), CH0007(골드 소비)은 SpendGold만으로
  자동 진행.

## 3. 카탈로그 관리

### 3계층 구조

| 계층 | 에셋 (Resources/) | 내용 |
|---|---|---|
| 1. 태그 레지스트리 | `StoreCatalog.asset` | `StoreTagEntry{tag, catalog}` 행 목록 — "어떤 탭이 있고 각 탭이 어느 카탈로그를 쓰는지". 자체 상품 데이터 없음 |
| 2. 태그별 상품 | `StoreEquipCatalog` / `StorePoseCatalog` / `StoreEffectCatalog` / `StoreGiftCatalog` / `StoreMiscCatalog`.asset | `StoreEntry{key, displayName, price, iconType(File|Runtime), icon, detailText}` — 태그는 소유 카탈로그가 결정(tab 필드 없음). 아이콘/보조 표기의 소유처 |
| 3. 프리뷰 상세 | `StoreDetailPoseCatalog` / `StoreDetailEffectCatalog`.asset | 순수 캡처 설정 — 포즈: 클립·freeze 구간 / 이펙트: 파티클 프리팹·simulateTime (아이콘 소스는 2계층 StoreEntry 소유) |

연계 카탈로그(폴더 밖, 읽기·additive 등록만): **InventoryCatalog**(모든 아이템 필수 — 없으면 소유
불가. 상점은 표시 이름/maxStack만 참조하고 **아이콘은 참조하지 않는다**), **EquipCatalog**(장착물만).
아이템 성능 데이터(선물 친밀도 수치 등)는 **ItemSystem**(`Assets/Prefabs/Assist/ItemSystem`,
ItemGiftCatalog.`affinityPoints`)이 소유 — Store는 detailText로 표시만 한다.
아이템 정체성은 문자열 key 하나로 전 카탈로그가 공유한다.

**Setup All 재실행은 안전하다** — StoreTools의 카탈로그 갱신은 전부 additive: 기존 엔트리/태그 행은
불변이고, 기본 키 중 누락분만 추가하며 기본 키의 **빈 값만 보충**한다(빈 clip/effectPrefab 바인딩,
File 행의 빈 icon, 빈 detailText. 구 스키마(giftPoints 시절) 에셋은 이 빈 값 보충으로 이행 —
포즈/이펙트 기본 키는 Runtime으로 승격된다). **인스펙터에서 직접 추가·수정한 상품/설정이
재실행에도 보존된다.**

### 상품 추가

1. 해당 태그의 StoreTagCatalog 에셋(예: 잡화 = `Resources/StoreMiscCatalog.asset`)에
   `StoreEntry{key, displayName, price, iconType, icon, detailText}` 1개 추가 — 레지스트리는 수정 불필요.
2. 같은 key를 `InventoryCatalog`에 등록(없으면 구매 AddToMain 실패).
3. 장착물이면 `EquipCatalog`에도 같은 key 등록.
4. 리그 캡처 아이콘을 쓰려면(`iconType = Runtime`) 상세 카탈로그
   (`StoreDetailPose/EffectCatalog.asset`)에도 캡처 설정을 등록 — 미등재 Runtime 키는 NoImage.

### 새 탭 추가

`Resources/StoreCatalog.asset`에 행 1개(tag 이름 + 카탈로그 참조) + StoreTagCatalog 에셋 1개
(Create > Store > Store Tag Catalog). 슬롯 6 한도. 탭 라벨/슬롯은 런타임 재동기화라 프리팹
리베이크 불필요. 단 `"포즈"`/`"장착물"` 태그 이름은 코드 상수와 결합이라 변경 금지(6장 참조).

### 아이콘 지정 (File vs Runtime)

**상점 아이콘은 상점 카탈로그(StoreEntry)가 소유** — 인벤토리 UI 아이콘과 완전 별개다
(InventoryCatalog를 조회하지 않음). 태그 카탈로그 엔트리의 `iconType`으로 키별 선택:

- **File**(기본) — 등록한 `icon` 스프라이트 사용. 비어 있으면 NoImage.
  (기본 데이터: 장착물 4종은 StoreTools가 Assets/Model의 스프라이트 PNG를 guid로 찾아 베이크)
- **Runtime** — 리그가 플레이 중 캡처(포즈 리롤 대상). **Detail 카탈로그 등재 키만 유효** —
  미등재 Runtime 키는 캡처 불가라 NoImage로 정착한다. (기본 데이터: 포즈/이펙트 6종)

아이콘 해석 순서: (File) StoreEntry.icon → (Runtime) 캡처 캐시 → NoImage.

## 4. 스크립트 레퍼런스

```
Assets/Prefabs/UI/Store/
├─ Scripts/   StoreCatalog · StoreTagCatalog · StoreDetailPose/EffectCatalog · StoreManager ·
│             StoreView · StoreConfirmView · StoreSellZone · StoreDemoController
├─ Test/      StorePosePreviewRig (기존 코드 복사·변조본 격리 — 헤더에 원본 출처 명시)
├─ Editor/    StoreTools (Tools/Store/* 메뉴 + batchmode 진입점 BatchBuildAll)
├─ Resources/ 카탈로그 에셋 8종(레지스트리 1 + 태그 5 + 상세 2) + StoreNoImage.png  (생성물)
├─ Sprites/   RerollDieIcon.png  (생성물)
├─ Prefabs/   StorePanel.prefab(확인 팝업 자식 포함) · StoreConfirm.prefab  (생성물)
└─ Demo/      StoreDemo.unity  (생성물)
```

(생성물) = `Tools/Store` 메뉴가 만든다. 프리팹/씬/스프라이트는 직접 편집하지 말고 리베이크한다.
카탈로그 에셋은 인스펙터 편집이 보존된다(additive — 위 3장).

### StoreView.cs — 메인 컨트롤러

베이크된 프리팹 계층을 `BindExisting`(이름 기반, 자식 이름이 곧 바인딩 계약 — 변경 금지)으로
연결해 동작한다. **런타임 코드 조립은 없다** — 베이크 계층이 없으면 에러 로그 후 무동작하며,
`Build()`와 UI 팩토리 헬퍼는 전부 에디터 베이크 전용(`#if UNITY_EDITOR`, 플레이어 빌드에서 제외).
표시·숨김은 CanvasGroup만 조작(SetActive 금지). 카탈로그 참조는 프리팹에 베이크되어 있고
런타임 Resources 폴백은 Awake 1회뿐. 탭은 `catalog.Tabs()`가 곧 목록(부재 시 `DefaultTabs` 폴백,
6 초과는 경고 후 절단), 장착물 미션 보고(AF0005)는 `catalog.TagForKey(key) == "장착물"` 판정.

| 공개 API | 설명 |
|---|---|
| `Show()` / `Hide()` / `Toggle()` | CanvasGroup으로 창 표시/숨김. Hide는 열린 확인 모달도 닫는다 |
| `SelectTab(tab)` | 탭 전환 + 페이지 0으로 리셋 + Refresh |
| `Refresh()` | 골드/탭 비주얼/그리드 재구성 + 포즈 리롤 버튼 표시 갱신(포즈 탭에서만) |
| `GetSellPrice(key)` | 카탈로그에 있으면 `price * SellPricePercent / 100`(최소 1 G), 없으면 `DefaultSellPrice`(10 G) |
| `RequestSell(key, slotIndex, count)` | SellZone의 판매 예약을 받아 확인 모달을 Sell 모드로 연다 |
| `NotifySold(name, count, gold)` | 판매 완료 토스트("이름 xN 판매 +NG") + 골드 갱신 |
| `ShowToast(msg)` | 토스트 표시, 2초 후 자동 소거 |

아이콘 처리: `StoreManager.ResolveIcon(key)`로 해석 → 없으면 NoImage를 깔고 프리뷰 키(Runtime)면
`RequestPreview`로 비동기 캡처 요청. 캡처 완료는 `IconReady` 브로드캐스트 → 살아 있는
카드(`Card_<key>`)와 열린 확인 모달(`UpdateIcon`)에 반영. 확인 팝업이 없으면(베이크 누락)
구매/판매를 토스트로 거부한다 — 즉시구매/전량판매 폴백은 없다.

### StoreManager.cs — 상시 상점 서비스 싱글톤

플레이 중 `Instance` getter가 자동 생성(GO "StoreManager") + DontDestroyOnLoad. **에디트 모드에서는
항상 null**(프리팹 베이크 경로가 매니저 없이 돈다). Awake에서 Resources의 태그 레지스트리
("StoreCatalog")와 상세 카탈로그 2종("StoreDetailPoseCatalog"/"StoreDetailEffectCatalog")을 로드한다.

| 멤버 | 설명 |
|---|---|
| `ResolveIcon(key)` | 상점 엔트리 `iconType` 기준 — (File) 등록 icon / (Runtime) 캡처 캐시 → null (**InventoryCatalog 미조회**, NoImage는 호출측이 씌움) |
| `NoImageSprite` | 베이크된 `Resources/StoreNoImage` 단일 소스 — 없으면 경고 1회 후 null(호출측이 아이콘 숨김) |
| `IsPreviewKey(key)` | 상점 엔트리 `iconType == Runtime` **+ Detail 카탈로그 등재** (File/미등재 키는 캡처 대상 아님) |
| `RequestPreview(key)` | 캡처 요청 — 비 Runtime/Detail 미등재/캐시 히트/요청 중(pendingKeys)/실패 확정(failedKeys)/리그 부재 시 무동작 |
| `RerollPoses()` | 상점 엔트리가 Runtime 모드인 포즈 키만 강제 재캡처 (리롤 버튼/데모 키 5의 진입점) |
| `IconReady` (이벤트) | (key, sprite) 캡처 완료 브로드캐스트 — 옛 스프라이트 파괴는 발화 뒤에 이루어진다 |

### StoreConfirmView.cs — 구매/판매 겸용 확인 모달

StorePanel.prefab에 "StoreConfirm" 자식으로 베이크된다(런타임 코드 조립 없음 — 베이크 계층이
없으면 에러 로그 후 무동작. 자식이 없는 구버전 프리팹만 confirmPrefab 참조로 1회 인스턴스).

- `Open(mode, itemKey, displayName, icon, unitPrice, maxQty, onConfirm)` — 수량 1~maxQty("n / max"
  표시), 합계 = 단가 x 수량. Buy = maxQty는 적재 여유분·합계가 보유 골드 초과면 빨강,
  Sell = maxQty는 보유개수·합계는 항상 노랑. 확정 시 `onConfirm(qty)` 콜백만 넘기고 실제
  결제/판매는 StoreView가 수행.
- `UpdateIcon(key, sprite)` — 캡처/리롤이 팝업 오픈보다 늦게 끝난 경우의 지연 아이콘 반영
  (열려 있고 같은 키를 표시 중일 때만 교체).
- `Close()` — 콜백 폐기 + 숨김(백드롭/취소 클릭과 동일).

### StoreSellZone.cs — 판매 존

`IDropHandler`. 드롭 검증(MAIN 인벤토리만 — CHAR 스토어는 토스트+거부) + 드롭
소비(`InventorySlotView.DropConsumed`) + `StoreView.RequestSell` 예약까지만 담당한다.
인벤토리/골드 변경은 모달 확정 시 `StoreView.ExecuteSale`이 슬롯+key를 재검증한 뒤 수행.
드래그 호버 시 파랑 하이라이트.

### 카탈로그 4종 — 데이터 모델

- `StoreCatalog`(태그 레지스트리): `TagEntries` / `Tabs()`(유효 태그 목록 — 빈/중복 스킵) /
  `CatalogForTab` / `EntriesForTab`(자식 위임) / `Get`(태그 등록 순 자식 위임, 첫 히트) /
  `Contains` / `TagForKey`(키가 속한 첫 태그). 자체 캐시 없음.
- `StoreTagCatalog`(태그별 상품): `StoreEntry{key, displayName, price, iconType(File|Runtime, 기본
  File), icon, detailText}` — `StoreIconType` enum 정의 파일. `Get` / `Contains` / `Entries` /
  `ValidEntries`(등록 순서 유지, 빈 키·중복 키 대표 외 제외). lazy map + OnValidate 무효화.
- `StoreDetailPoseCatalog`: `StoreDetailPoseEntry{key, clip, freezeMin=0.2, freezeMax=0.8}` —
  순수 캡처 설정(아이콘 소스는 StoreEntry 소유).
- `StoreDetailEffectCatalog`: `StoreDetailEffectEntry{key, effectPrefab, simulateTime=1.5}` —
  순수 캡처 설정. 기본 바인딩: fx_pat_heart→`Fx_LoveAura`(실제 머리 쓰다듬기 하트) /
  fx_pat_star→CFXR4 Falling Stars / fx_click_sparkle→CFXR2 Shiny Item (Loop) — 파티클 프리팹은
  읽기 전용 참조.

### Test/StorePosePreviewRig.cs — 포즈/이펙트 캡처 리그

캐시 없는 순수 캡처 서비스 — 캐시·키 해석·NoImage 정책은 `StoreManager` 소유. 씬에 배치돼야
동작한다(자동 생성 없음, 데모 씬 빌더가 배치).

- `RequestPoseCapture(entry, onDone)` / `RequestEffectCapture(entry, onDone)` — 큐 → 코루틴 순차
  캡처 → 콜백. 엔트리 타입은 `StoreDetailPoseEntry`/`StoreDetailEffectEntry`.
  **모든 실패/파괴/비활성 경로에서 `onDone(entry, null)` 통지 보장**(매니저 pending 고착 방지 계약).
- `IsDisabled` — 영구 캡처 불가 확정(캐릭터 프리팹/Animator 부재). `EditorSet(prefab)` — 빌더 주입.
- 포즈 캡처: 스트립된 캐릭터 클론에 PlayableGraph로 클립을 freezeMin~freezeMax 랜덤 정지 →
  재프레이밍 → RT 캡처. 이펙트 캡처: 캐릭터 렌더러 off → 이펙트 프리팹 인스턴스 스트립 →
  최상위 ParticleSystem들 `Simulate(simulateTime, withChildren, restart)` 정지컷 → RT 캡처 → 정리.
- 캡처는 전용 레이어(PortraitModel)·전용 카메라(RenderTexture iconSize²)에서 수행 — 메인 카메라에
  비치지 않는다. 캡처 스프라이트의 수명(파괴)은 StoreManager 캐시가 관리.

### StoreDemoController.cs — 데모 입력

S 상점 토글 / I 인벤토리 토글 / G +500G / 숫자키 지급(직렬화 `grants` 리스트, 데모 씬 빌더가 기록) /
5 포즈 리롤(`rerollPoseKey` → `StoreManager.RerollPoses`). 레거시 `Input.GetKeyDown`만 사용.

### Editor/StoreTools.cs — 셋업 파이프라인

| 메뉴 | 역할 |
|---|---|
| `Setup All` | 아래 4단계 일괄 실행 |
| `1. Create Catalog` | 레거시 프리뷰 에셋 정리 → 태그별 상품 카탈로그 5종(**장착물 4종 아이콘을 Assets/Model 스프라이트 PNG에서 guid로 찾아 베이크**, 선물 detailText "친밀도 +N") → 태그 레지스트리(guid 보존) → 상점 전용 키 12종 InventoryCatalog_Demo 등록 → 프리뷰 상세 카탈로그 2종(클립/파티클 프리팹 해석 실패 시 에러 로그 + null 유지) → NoImage 베이크. **전부 additive**(기존 엔트리·태그 행 보존, 누락 기본 키만 추가, 빈 값만 보충 — 구 스키마 에셋은 이 보충으로 이행: 포즈/이펙트 기본 키 Runtime 승격, File 행 빈 icon·빈 detailText 채움) |
| `2. Build UI Prefab` | **카탈로그 선행 보장**(존재 + `Tabs()` 비어있지 않음 — 미비 시 CreateCatalog) → StoreConfirm.prefab 선베이크 → StorePanel.prefab에 카탈로그/확인 팝업/주사위 아이콘 참조 베이크(`EditorSetCatalog` 등) + **확인 팝업을 "StoreConfirm" 자식으로 베이크** |
| `3. Apply SUIT-Bold Font` | 두 프리팹의 전 TMP_Text를 SUIT-Bold로 교체(베이크 후 필수 마지막 단계) |
| `4. Build Demo Scene` | 카탈로그 선행 보장(위와 동일 검사) 후 카메라/EventSystem/매니저(GO "StoreManager" 포함)/Canvas/인벤토리·상점 패널/프리뷰 리그(GO "StorePosePreviewRig", 아로나 POC 프리팹 주입)/데모 컨트롤러/안내 배치 |
| `BatchBuildAll` (메뉴 없음) | batchmode `-executeMethod` 진입점 — 다이얼로그 없이 전 단계 + SaveAssets |

## 5. 튜닝 변수 표 — "어디서 고치나"

| 무엇 | 기본값 | 파일 · 심볼 |
|---|---|---|
| 판매가 비율 | 50% | `Scripts/StoreView.cs` 상수 `SellPricePercent` |
| 카탈로그 밖 아이템 판매가 | 10 G | `Scripts/StoreView.cs` 상수 `DefaultSellPrice` |
| 상품 목록·이름·가격 | 태그별 합계 16종 | 태그별 StoreTagCatalog 에셋 인스펙터에서 직접 추가/수정(**재실행에도 보존**). 기본 16종의 초기값은 `Editor/StoreTools.cs` `CreateCatalog()`의 `CreateTagCatalog(...)` 배열 |
| 탭 구성 | 태그 5종 (장착물/포즈/이펙트/선물/잡화) | `Resources/StoreCatalog.asset` 태그 레지스트리 — 행 추가/재배열/참조 교체 보존(기본 5태그 누락분만 재실행이 보충). 슬롯 최대 6(`MaxTabSlots`), 부재 시 `StoreView.DefaultTabs` 폴백 |
| 아이콘 소스/스프라이트(키별) | File(장착물·선물·잡화) / Runtime(포즈·이펙트) | 태그 카탈로그 에셋 엔트리 `iconType`/`icon` (인스펙터 편집 보존 — 상점 소유, Inventory 아이콘과 별개) |
| 카드 보조 표기(키별) | 선물 3종 "친밀도 +N" (나머지 공백) | 태그 카탈로그 에셋 엔트리 `detailText` — 표시 전용 자유 텍스트(성능 수치는 ItemSystem 소유) |
| 장착물 4종 기본 아이콘 | Assets/Model 스프라이트 PNG | `Editor/StoreTools.cs` `LoadSpriteByGuid(...)` guid 4종 — File 행의 빈 icon만 보충하므로 에셋에서 직접 바꿔도 보존 |
| 포즈 정지 구간(랜덤) | 20%~80% | `StoreDetailPoseEntry.freezeMin/freezeMax` (엔트리별, 에셋 인스펙터 — 편집 보존) |
| 이펙트 정지컷 시각 | 1.5초 | `StoreDetailEffectEntry.simulateTime` (엔트리별, 에셋 인스펙터 — 편집 보존) |
| 이펙트 프리뷰 프리팹 기본 바인딩 | Fx_LoveAura / CFXR4 Falling Stars / CFXR2 Shiny Item (Loop) | `Editor/StoreTools.cs` 경로 상수 `FxLoveAuraPath`/`FxPatStarPath`/`FxClickSparklePath` — 빈 effectPrefab만 보충하므로 에셋에서 직접 바꿔도 보존 |
| 프리뷰 아이콘 해상도(포즈/이펙트 공용) | 256 | `Test/StorePosePreviewRig.cs` 직렬화 필드 `iconSize` |
| 페이지당 카드 수 | 6 (3x2) | `Scripts/StoreView.cs` 상수 `CardsPerPage` (그리드 셀 크기/정렬은 `BuildBody`의 GridLayoutGroup — 좌상단 정렬) |
| 패널 크기 | 520x560 | `Scripts/StoreView.cs` 상수 `PanelWidth` / `PanelHeight` (수정 후 리베이크) |
| 리롤 버튼 위치/크기 | PageBar 중앙 기준 x=+235, 20x20 | `Scripts/StoreView.cs` `BuildBody()`의 `AnchorCenter(poseRerollButton...)` (수정 후 리베이크) |
| 토스트 표시 시간 | 2초 | `Scripts/StoreView.cs` `ShowToast()`의 `Invoke(nameof(ClearToast), 2f)` |
| 구매 수량 상한 | 99 (+ 적재 잔여량 캡) | `Scripts/StoreView.cs` `OnCardClicked()`의 `Mathf.Min(99, room)` — 모달에는 `StoreConfirmView.Open`의 maxQty로 전달 |
| NoImage/주사위 아이콘 그림 | 256² / 64² PNG | `Editor/StoreTools.cs` `EnsureNoImageSprite()`/`EnsureRerollIcon()` — 기존 파일을 재사용하므로 재베이크는 해당 PNG 삭제 후 `Setup All` 재실행 |
| 포즈 리롤 데모 키 | 5 | `Scripts/StoreDemoController.cs` 직렬화 필드 `rerollPoseKey` |
| 데모 지급 키(1~4) | 치파오/선물(소)/포즈: 댄스/바나나 | `Editor/StoreTools.cs` `WriteDemoGrants()`의 `keys/itemKeys` 배열 (수정 후 데모 씬 리빌드) |

## 6. 제약 / 알려진 한계

### 제약 (이 폴더의 규약)

- **프리팹 완결 UI**: 상점/확인 모달의 런타임 코드 조립은 없다 — 베이크 계층이 없으면 에러 로그 후
  무동작(`Tools/Store`로 리베이크). 확인 팝업이 없으면 구매/판매가 토스트로 거부된다.
- **태그 이름 결합**: `"포즈"`(리롤 버튼 표시) / `"장착물"`(AF0005 미션 보고) 태그 이름은 코드
  상수와 결합 — 변경 금지.
- **중복 키 금지 관례**: 같은 key를 두 태그 카탈로그에 등록하면 첫 태그가 우선(경고 없음).
- 탭 슬롯 최대 6 — 초과 태그는 경고 1회 후 절단.
- **기존 코드 불가침**: Store 폴더 밖의 코드·에셋은 수정하지 않는다(읽기 참조 + InventoryCatalog
  additive 등록만). 생성·수정 파일은 전부 `Assets/Prefabs/UI/Store/` 아래.
- **Test 폴더 규약**: 기존 프로젝트 코드를 복사·변조한 파생 코드는 `Test/`에 격리하고 파일 헤더에
  원본 출처를 명시한다(StorePosePreviewRig가 해당). 완전 신규 원본 코드는 일반 폴더에 둔다.
- 공통 규칙: 네임스페이스 없음 / .meta 손 생성 금지 / 레거시 Input만 / TMP 전용 /
  창 표시·숨김은 CanvasGroup만 / 로그 프리픽스 `[Store][클래스명]` / UI 문자열·주석 한국어.

### 알려진 한계

- **Runtime 프리뷰는 리그가 있는 씬에서만** 뜬다(데모 씬 전용) — 리그가 없으면 NoImage 유지.
  포즈는 랜덤 정지 위치·이펙트는 파티클 랜덤 시드라 캡처마다 그림이 달라진다(아이콘을 고정하려면
  StoreEntry의 `iconType = File` + `icon` 등록으로 전환). 배경은 어두운 단색(투명 아님).
- **MagicaCloth 바인드포즈** — 리그가 MonoBehaviour를 스트립하므로 머리카락/치마가 바인드 포즈로
  캡처된다(스틸 아이콘으로는 수용 범위).
- NoImage 플레이스홀더는 베이크 PNG 단일 소스 — 없으면 경고 1회 + 아이콘 숨김(이름 텍스트만).
- 포즈 실발동(PoseManager)/이펙트 실발동(쓰다듬기·클릭 연동) 미구현 — 현재는 소유 + 프리뷰까지
  (포즈 검토: `Store_PoseAnimation_Review.md` 1장).
- 선물 증정 → 친밀도 포인트 지급은 미구현. 능력 수치(`affinityPoints`)의 소유처는
  ItemSystem(`Assets/Prefabs/Assist/ItemSystem`의 ItemGiftCatalog — 별도 WORKLOG 참조)이고,
  Store는 `detailText`로 표시만 한다.
  설계 소유는 `../CharacterDetail/Affinity_Store_Integration.md` — Store는 구현 시 해당 문서를 따른다.
- 의상 탭(캐릭터별 가변 상품)은 범위 밖 — 예비 탭 슬롯으로 대응 예정.
