# Store(상점) — 친밀도 재화 루프 검증용 StandAlone 프로토타입

Affinity_Plan(친밀도)의 재화 루프를 검증하는 상점 프로토타입. 폴더 감성 탭 상점(구매, 수량 확인 팝업)
+ 페이징 + **인벤토리 슬롯 → 판매 존 드래그앤드롭 → 수량 선택 판매**. 기존 시스템과의 연결은 전부
**문자열 key + 공개 API + 이벤트**뿐이라 약결합을 유지한다. 설계 문서: `Store_Design.md`.

- 지갑은 기존 미션 재화 시스템 `InventoryManager`(Prefabs/UI/Mission)의 Gold를 그대로 사용.
- 아이템 보관/지급은 `InventorySystemManager`(Prefabs/Assist/InventorySystem)에 위임.

---

## 1. 구성 (전부 `Assets/Prefabs/UI/Store/` 안)

| 파일 | 역할 |
|---|---|
| `README.md` | 폴더의 정문: 빠른 시작·UI 구성·스크립트별 함수 표·**튜닝 변수 표**("어디서 고치나")·등록 절차/제약/한계. |
| `Store_Design.md` | 설계 문서(계층 구조·바인딩 계약·팔레트·판매 프로토콜·아이템 등록 전략·프리뷰 서비스(StoreManager+리그)의 원본). |
| `Store_PoseAnimation_Review.md` | 포즈 상품 후속 검토서: ①포즈 애니메이션 제어(Playables one-shot 권장) ②포즈 스냅샷 아이콘(검토 당시 에디터 베이크 권장 — 이후 리얼타임 리그로 구현). |
| `Scripts/StoreCatalog.cs` | **태그 레지스트리**(카탈로그 3계층의 1계층): `StoreTagEntry{tag, catalog}` 리스트만 보유, 자체 캐시 없음(자식 lazy map에 위임). `TagEntries`/`Tabs()`/`CatalogForTab`/`EntriesForTab`(위임)/`Get`(자식 위임, 첫 히트)/`Contains`/`TagForKey`. 중복 태그 경고는 1회 래치(OnValidate 리셋). |
| `Scripts/StoreTagCatalog.cs` | 태그(탭) 하나의 상품 카탈로그(2계층): `StoreEntry{key, displayName, price, iconType(File|Runtime, 기본 File), icon, detailText}` — **tab 필드 없음**(태그는 소유 레지스트리가 결정), **`StoreIconType` enum 정의 파일**. 아이콘은 상점 소유(인벤토리 UI 아이콘과 별개), detailText는 카드 보조 표기 전용 자유 텍스트(성능 수치 아님). lazy map, `Get`/`Contains`/`Entries`/`ValidEntries`(빈 키·중복 키 대표 외 제외). |
| `Scripts/StoreDetailPoseCatalog.cs` | 포즈 상세(프리뷰) 카탈로그(신규 원본 코드): `StoreDetailPoseEntry{key, clip, freezeMin=0.2, freezeMax=0.8}` — **순수 캡처 설정**(아이콘 소스는 StoreEntry 소유). lazy-map 패턴, `Get`/`Contains`/`Entries`. 아이콘 캡처와 향후 포즈 재생의 공용 데이터 소스. |
| `Scripts/StoreDetailEffectCatalog.cs` | 이펙트 상세(프리뷰) 카탈로그(신규 원본 코드): `StoreDetailEffectEntry{key, effectPrefab, simulateTime=1.5}` — **순수 캡처 설정**. 같은 lazy-map 패턴, `Get`/`Contains`/`Entries`. 이펙트 정지컷 캡처의 데이터 소스(파티클 프리팹은 읽기 전용 참조). |
| `Scripts/StoreManager.cs` | **상시 상점 서비스 싱글톤**(MissionList 패턴: 플레이 중 `Instance` getter가 자동 생성 + DontDestroyOnLoad, **에디트 모드에서는 항상 null** — 베이크 경로가 매니저 없이 돈다). 프리뷰 캐시(previewCache)·중복 요청 방지(pendingKeys)·실패 확정(failedKeys)·NoImage 폴백(베이크 PNG 단일 소스)·`IconReady` 이벤트 소유. `ResolveIcon`(상점 엔트리 iconType 기준 — **InventoryCatalog 미조회**)/`IsPreviewKey`/`RequestPreview`/`RerollPoses`. Awake에서 레지스트리+상세 카탈로그 2종을 Resources에서 로드. |
| `Test/StorePosePreviewRig.cs` | 포즈/이펙트 **리얼타임 캡처 리그** — 캐시 없는 순수 캡처 서비스(캐시/정책은 StoreManager 소유). 기존 코드 복사·변조본(`animationplayermanager.cs` 포즈 프리즈 + `InventorySystemTools.cs` 스트립/프레이밍 — 헤더에 출처 명시)이라 Test 폴더에 격리. `Instance`/`IsDisabled`/`RequestPoseCapture`/`RequestEffectCapture`. 모든 실패/파괴 경로에서 `onDone(entry, null)` 통지. |
| `Scripts/StoreView.cs` | 메인 컨트롤러 — **런타임은 BindExisting 전용**(베이크 계층 없으면 에러 로그 후 무동작, `Build()`/팩토리 헬퍼는 `#if UNITY_EDITOR` 베이크 전용). 헤더(타이틀·골드·닫기) + **폴더 탭 바(슬롯 6개 — 태그 레지스트리의 태그 목록이 곧 탭, 카탈로그 부재 시 DefaultTabs 폴백)** + 상품 그리드(**페이지당 6장, [ < n / m > ] 페이지바, 좌상단 정렬**) + 판매 존 + 토스트. 구매는 확인 팝업 경유(팝업 부재 시 토스트 거부). PageBar 우측 주사위 리롤 버튼(PoseRerollButton)은 포즈 탭에서만 표시. |
| `Scripts/StoreConfirmView.cs` | **구매/판매(Buy/Sell) 겸용 확인 모달** — StorePanel.prefab에 "StoreConfirm" **자식으로 베이크**(런타임 코드 조립 없음, 계층 없으면 에러 로그 후 무동작). `StoreConfirmMode`에 따라 문구 전환, 수량 조절(1~max, **"n / max" 표시**) + 최종금액(단가 x 수량) → 확인 시 수량 콜백. 백드롭 클릭/취소 = 닫기. `Open`이 itemKey를 받아 보관(currentKey)하고, 프리뷰 캡처/리롤이 늦게 끝나면 `UpdateIcon(key, sprite)`로 열린 모달의 아이콘을 갱신. |
| `Scripts/StoreSellZone.cs` | `IDropHandler` 판매 존. **직접 팔지 않는다** — 드롭 **검증 + 소비(DropConsumed) + `StoreView.RequestSell` 예약**만 담당(아래 프로토콜 참조). 호버 시 AccentBlue 하이라이트. |
| `Scripts/StoreDemoController.cs` | 데모 입력(레거시 Input): S/I 창 토글, G 골드 지급, 1~4 아이템 지급, 5 포즈 리롤(`StoreManager.RerollPoses`). |
| `Editor/StoreTools.cs` | `Tools/Store/*` 메뉴(카탈로그 에셋 8종 = 레지스트리 1 + 태그 5 + 상세 2, NoImage/주사위 스프라이트 베이크/UI 프리팹 2종 베이크/SUIT-Bold 폰트/데모씬+StoreManager+프리뷰 리그) + batchmode 진입점 `BatchBuildAll`. **카탈로그 갱신은 전부 additive**(기존 엔트리 불변 + 누락 기본 키만 추가, **빈 값만 보충**: clip/effectPrefab 바인딩·File 행의 빈 icon·빈 detailText — 구 스키마(giftPoints 시절) 에셋의 이행 경로이기도 하다: 포즈/이펙트 기본 키는 Runtime으로 승격). 장착물 4종 아이콘은 Assets/Model 스프라이트 PNG를 guid로 찾아 베이크(`LoadSpriteByGuid`). |
| `Resources/StoreCatalog.asset` | **태그 레지스트리** — 태그 5행(장착물/포즈/이펙트/선물/잡화 → 각 태그 카탈로그 참조. 생성물, 런타임 자동 로드 — guid 보존을 위해 기존 에셋 재사용). |
| `Resources/Store{Equip,Pose,Effect,Gift,Misc}Catalog.asset` | 태그별 상품 카탈로그 5종(StoreTagCatalog 타입, 생성물): 장착물 4 / 포즈 3 / 이펙트 3 / 선물 3 / 잡화 3 — 합계 16종. 기본 구성: 장착물 4종 = **File 아이콘**(Assets/Model 스프라이트 PNG를 guid로 베이크) / 포즈·이펙트 6종 = **Runtime**(리그 캡처) / 선물 3종 = detailText "친밀도 +N". |
| `Resources/StoreDetailPoseCatalog.asset` | 포즈 상세(프리뷰) 카탈로그 3종 — pose_greeting/pose_dance/pose_sit 클립 바인딩(생성물, 런타임 자동 로드). 클립 해석 실패 시 clip null로 남고 리그가 널가드한다. |
| `Resources/StoreDetailEffectCatalog.asset` | 이펙트 상세(프리뷰) 카탈로그 3종 — fx_pat_heart→`Fx_LoveAura`(실제 머리 쓰다듬기 하트) / fx_pat_star→CFXR4 Falling Stars / fx_click_sparkle→CFXR2 Shiny Item (Loop) — 뒤 2종은 JMO Cartoon FX Remaster(생성물, 런타임 자동 로드, 프리팹은 읽기 전용 참조). 프리팹 해석 실패 시 null로 남고 리그가 널가드한다. |
| `Resources/StoreNoImage.png` | 'NO IMAGE' 플레이스홀더 스프라이트 256²(생성물 — `1. Create Catalog`이 베이크: 라운드 사각 + 5x7 픽셀폰트). **베이크 PNG가 단일 소스** — 없으면 매니저가 경고 1회 후 null을 반환하고 카드/모달은 아이콘을 숨긴다(런타임 절차 생성 폴백 없음). |
| `Sprites/RerollDieIcon.png` | 포즈 리롤 버튼용 주사위 아이콘 64²(생성물 — `2. Build UI Prefab`이 베이크). |
| `Prefabs/StorePanel.prefab` | 베이크된 다크테마 상점 프리팹(SUIT-Bold 적용, 생성물). **확인 팝업("StoreConfirm")이 자식으로 베이크**되고 카탈로그(태그 레지스트리)/주사위 아이콘 참조도 직렬화 고정된다. |
| `Prefabs/StoreConfirm.prefab` | 베이크된 구매 확인 팝업 프리팹(생성물). StorePanel에 자식으로 함께 베이크되며, 자식이 없는 구버전 StorePanel 호환용으로 confirmPrefab 참조도 주입된다. |
| `Demo/StoreDemo.unity` | 데모 씬(생성물). |

---

## 2. 작동 원리

### 프리팹 완결 UI (런타임 BindExisting 전용)
- `StoreView.Awake`: 베이크된 계층("HeaderBar" 자식 존재)이 있으면 `BindExisting`(이름 기반 재바인딩),
  **없으면 에러 로그 후 무동작**(built=false — Refresh/Show 등 공개 API 전부 무동작). 런타임 코드
  조립(Build 폴백)은 제거됐고 `Build()`와 UI 팩토리 헬퍼는 전부 `#if UNITY_EDITOR`(플레이어 빌드
  제외). `StoreConfirmView.Awake`도 동일 규칙. 첫 페인트는 `Start()`(매니저 Awake 이후 보장).
- 확인 팝업은 StorePanel.prefab에 **"StoreConfirm" 자식으로 베이크** — BindExisting이 바로 연결한다.
  자식이 없는 구버전 프리팹만 confirmPrefab 참조로 1회 인스턴스(코드 자가 구축 폴백 없음).
  팝업을 못 찾으면 구매/판매는 토스트로 거부한다(즉시구매/전량판매 폴백 제거).
- 카탈로그(태그 레지스트리) 참조는 StoreTools가 프리팹에 베이크(`EditorSetCatalog`) — 런타임
  Resources 폴백은 Awake 1회뿐.
- 자식 이름이 곧 바인딩 계약(HeaderBar/Body/GoodsGrid/PageBar/TabButton_0~5/CardTemplate/SellZone/ToastText/StoreConfirm 등) — 변경 금지.
- 정적 크롬(루트/헤더/탭/페이지바/판매 존)은 레이아웃 그룹 금지·고정 앵커, 레이아웃 그룹은 동적
  콘텐츠인 GoodsGrid(GridLayoutGroup)에만 허용. 첫 자식은 투명 `Handler`(DragUIHandler) — 창 드래그.
- 창 토글은 CanvasGroup(alpha/interactable/blocksRaycasts)만 조작, `SetActive` 금지.

### 폴더 탭 + 페이징
- TabBar는 Body **뒤 형제**라 본문 위에 6px 겹쳐 그려진다. 선택 탭은 본문과 같은 색(PanelBg) +
  전체 높이로 본문과 이어지고(폴더 앞면), 비선택 탭은 어둡게(HeaderBg) + 6px 낮게 내려앉는다.
- 슬롯은 `TabButton_0~5` 6개 고정. **탭 목록은 카탈로그(태그 레지스트리)가 소유** — `catalog.Tabs()`가
  곧 탭이고(현재 5종: 장착물/포즈/이펙트/선물/잡화), 카탈로그 부재 시 `StoreView.DefaultTabs` 폴백.
  슬롯 6 한도(`MaxTabSlots`) 초과 태그는 경고 1회 후 절단. `RefreshTabVisuals`가 매 갱신마다 슬롯
  활성/라벨을 카탈로그와 재동기화하고, 클릭 리스너는 예비 슬롯에도 걸려 있어(핸들러가 슬롯→태그를
  매번 재해석) 런타임 태그 추가에도 리베이크 없이 대응한다. **의상 탭은 캐릭터별 가변이라 제외**.
- 그리드는 페이지당 6장(3x2) 고정, Body 하단 PageBar `[ < n / m > ]`로 이동(InventoryView 푸터 방식).
  탭 전환 시 페이지 0으로 리셋, 경계에서 버튼 interactable off. 카드는 **좌상단부터** 채운다
  (GridLayoutGroup childAlignment UpperLeft — 미완성 행이 가운데로 몰리지 않는다).

### 카탈로그와 key 규약
- `StoreEntry.key`는 InventoryCatalog/EquipCatalog와 **같은 key 문자열 공간**.
  표기는 `InventorySystemManager.Instance.Catalog.Get(key)`의 displayName 우선, 없으면 `StoreEntry.displayName` 폴백.
  **아이콘은 상점 카탈로그 소유**(인벤토리 UI 아이콘과 완전 별개 — `ResolveIcon`은 InventoryCatalog을
  조회하지 않는다): 상점 엔트리 `iconType` 기준 (File) 등록 icon / (Runtime) 캡처 캐시 → 없으면
  NoImage 플레이스홀더 체인(PNG 부재 시 아이콘 숨김 + 이름 텍스트).
- 태그(탭): `StoreEntry`에 tab 필드가 없다 — **키의 소속 태그는 그 키를 담은 StoreTagCatalog을 참조하는
  레지스트리 행이 결정**(`TagForKey`). 현재 `"장착물"`(EquipCatalog 키 4종) / `"포즈"` / `"이펙트"` /
  `"선물"`(카드에 detailText "친밀도 +N" 표기 — 표시 전용, 실제 수치는 ItemSystem의 ItemGiftCatalog 소유) / `"잡화"`.
- **주의**: `"포즈"`/`"장착물"` 태그 이름은 코드 상수와 결합(리롤 버튼 표시 / AF0005 미션 보고) —
  태그명 변경 금지. 같은 key를 두 태그 카탈로그에 중복 등록하면 첫 태그가 우선(경고 없음 — 금지 관례).
- 상점 전용 키(포즈/이펙트/선물/잡화 12종)는 `StoreTools.CreateCatalog`이 **InventoryCatalog_Demo.asset에
  데이터로만 additive 등록**(이미 있으면 스킵) — `AddToMain`이 카탈로그 검증을 하므로 필수.
  InventorySystem 코드는 이 경로에서 일절 수정하지 않는다.
- 아이템 등록 전략(카탈로그 3분할·키 네이밍·마스터 시트 개선안)은 `Store_Design.md` 3장 참조.

### 프리뷰 파이프라인 (StoreManager + StorePosePreviewRig)

포즈/이펙트 상품은 상점 엔트리가 `iconType=Runtime`이다 — 등록 스프라이트 대신 플레이 중
**리얼타임 캡처**로 아이콘을 만들어 카드/확인 모달에 채운다(설계 상세: `Store_Design.md` 10장).
역할은 상시 매니저와 씬 소속 리그로 나뉜다.

- **StoreManager (정책/캐시, 상시)**: 캐시(previewCache)·요청 상태(pendingKeys 중복 방지 /
  failedKeys 실패 확정 — 재요청 방지, 리롤로만 해제)·NoImage 폴백을 소유. 카드/모달 아이콘은
  `ResolveIcon(key)`(**상점 엔트리 `iconType` 기준** — (File) 등록 icon / (Runtime) 캡처 캐시 → null.
  **InventoryCatalog은 조회하지 않는다**)로 해석하고, null이면 호출측이 `NoImageSprite`(베이크된
  Resources "StoreNoImage" 단일 소스 — 없으면 경고 1회 후 null, 호출측이 아이콘 숨김)를 씌운 뒤
  `IsPreviewKey`(상점 엔트리 iconType == Runtime **+ 상세 카탈로그 등재**)면 `RequestPreview(key)`로
  캡처를 요청한다. 리그는 씬 전환으로 사라질 수 있어 매 호출 `Instance`로 조회한다(참조 캐시 금지).
- **아이콘 소스 모드 (File / Runtime)**: 상점 엔트리별 `iconType`(enum `StoreIconType`,
  `StoreTagCatalog.cs` 정의) — 아이콘은 상점 카탈로그가 소유한다. **File**(기본) = 등록한 `icon`
  스프라이트가 곧 실아이콘(비면 NoImage). **Runtime** = 리그 캡처 + 리롤 대상 — 단 **Detail 카탈로그
  등재 키만 캡처 가능**(미등재 Runtime 키는 캡처 불가 → NoImage로 정착).
- **StorePosePreviewRig (캡처 기계, 씬 소속)**: 캐시 없는 순수 캡처 서비스 —
  `RequestPoseCapture`/`RequestEffectCapture(entry, 콜백)`를 큐에 넣고 코루틴이 순차 캡처해 콜백만 한다.
- **리그 구성**: 데모 씬 빌더가 GO "StorePosePreviewRig"를 (0,-1000,0)에 배치하고 아로나 POC 프리팹
  (`Assets/Prefabs/Char_toon/arona_6_clean_POC.prefab`)만 주입(`EditorSet`) — 상세 카탈로그
  (StoreDetailPoseCatalog/StoreDetailEffectCatalog)는 StoreManager가 Resources에서 로드한다. Start에서 캐릭터를 **비활성 홀더** 밑에 인스턴스화 →
  MonoBehaviour 전부 스트립(다중 패스, Animator는 컴포넌트라 생존) → 레이어 6(PortraitModel)으로
  격리 → 전용 Camera(RenderTexture iconSize=256², cullingMask 레이어 6만, 어두운 단색 클리어) +
  Directional Light 구성 → 렌더러 bounds로 프레이밍. 데모 Main Camera는 cullingMask에서 레이어 6을
  제외해 리그가 화면에 비치지 않는다.
- **포즈 캡처**: PlayableGraph + AnimationClipPlayable로 클립을 **재생 위치 20~80% 랜덤 정지**
  (`freezeMin~freezeMax`, SetSpeed(0)+SetTime+Evaluate) → 재프레이밍 → 프레임 끝에서 RT ReadPixels →
  Sprite.Create → 콜백.
- **이펙트 캡처(신설)**: 캐릭터 렌더러 전부 off → **비활성 FxHolder**에 이펙트 프리팹 Instantiate →
  MonoBehaviour 스트립(CFXR_Effect 대비) → PortraitModel 레이어 → 활성화 → **최상위 ParticleSystem들만
  `Simulate(simulateTime, withChildren, restart)`로 정지컷**(중첩 시스템은 부모의 withChildren이 처리,
  Simulate 후 일시정지 상태라 다음 프레임에도 같은 그림) → 이펙트 바운드로 프레이밍(살아있는 파티클이
  0개면 바운드 퇴화 — 반경 하한 클램프) → RT 캡처 → FxHolder 파괴 + 캐릭터 렌더러 복원.
- **완료 브로드캐스트 → 옛 스프라이트 파괴 (순서 고정)**: 매니저가 캐시를 갱신하고 `IconReady(key,
  sprite)`를 발화 — StoreView가 살아 있는 카드(`Card_<key>`)와 열린 확인 모달
  (`StoreConfirmView.UpdateIcon`)에 반영한다. **옛 스프라이트/텍스처 파괴는 반드시 브로드캐스트 뒤** —
  구독자가 교체를 마치기 전에 파괴하면 파괴된 참조가 흰 사각형으로 그려진다.
- **리롤**: `RerollPoses()`가 **상점 엔트리가 Runtime 모드인 포즈 키만** 캐시 유무와 무관하게 강제
  재캡처(failed/pending 해제 후 재등록 — File/레지스트리 미등재 키는 제외). 정지 시점이 랜덤이라
  리롤마다 다른 포즈가 나온다. 진입점 2곳 — PageBar 우측 주사위 버튼
  (`PoseRerollButton`, 20x20, x=+235, 포즈 탭에서만 표시 — Refresh 말미 `UpdatePoseRerollVisibility`) +
  데모 키 `5`(`StoreDemoController.rerollPoseKey`).
- **리그 파괴 시 실패 통지**: 리그(씬 소속)는 파괴/비활성/영구 비활성(캐릭터 프리팹·Animator 부재)의
  모든 경로에서 진행 중+대기 요청에 `onDone(entry, null)`을 통지한다(`DrainRequestsWithFailure`) —
  상시 매니저(DontDestroyOnLoad)의 pendingKeys가 고착되지 않기 위한 계약. 실패 키는 failedKeys로
  확정되어 재요청되지 않고, 리롤로만 다시 시도한다.
- **약결합**: 리그가 씬에 없으면 카드는 NoImage 플레이스홀더 그대로 — 상점은 리그 없이도 완전
  동작한다. 리그는 OnDestroy에서 그래프/RT를, 매니저는 OnDestroy에서 캐시 스프라이트/텍스처를 정리한다.
- **한계**: MagicaCloth 스트립으로 머리카락/치마는 바인드 포즈, 배경은 단색(투명 아님), 포즈는 랜덤
  정지 위치·이펙트는 파티클 랜덤 시드 때문에 캡처마다 그림이 달라진다.

### 구매 흐름 (지갑 = 미션 InventoryManager)
- 카드 클릭 → `StoreConfirmView.Open(Buy 모드, 키, 이름, 아이콘, 단가, 최대수량, 콜백)` — 수량과
  **최종금액(합계 N G)**을 보여주고 "정말 계산하시겠습니까?" 확인. 합계가 보유 골드 초과면 합계 텍스트 빨강.
- **수량 상한 = 남은 적재 가능량**(스택 잔여 공간): 무조건 1~99가 아니라 인벤토리에 더 담을 수 있는
  만큼만 올라가고, 수량 표시는 **"n / max"** 형식.
- "계산하기" → `InventoryManager.Instance.SpendGold(총액)` 성공 시
  `InventorySystemManager.Instance.AddToMain(key, 수량)`. AddToMain 실패 시 `RefundGold(총액)`로 전액
  환불(실패 결제 되돌림 — 누적 소비(goldSpentTotal)를 역가산, 소득/소비 집계를 펌핑하지 않는다).
- 골드 부족: 토스트 "골드가 부족합니다" + GoldText 빨강 플래시(0.5초).
- 골드 표시는 `InventoryManager.InventoryChanged`, 보유 수("보유 n")는
  `InventoryEvents.OnStoreChanged` 구독으로 갱신.
- **미션 연동**: CH0007(골드 소비)은 `SpendGold` 내부 집계만으로 자동 진행(상점 측 코드 불필요).
  구매 키의 소속 태그가 `"장착물"`이면(`catalog.TagForKey(key)` 판정 — StoreEntry에 tab 필드가 없어
  레지스트리에서 역조회) `MissionList.Instance.Report("AF0005", 수량)` 호출
  (주의: `MissionList.Instance` getter는 플레이 중 자동 생성되므로 데모 구매도 실제 미션 저장에 반영된다).

### 판매 존 프로토콜 (핵심 통합 지점) — 검증 → 예약 → 모달 → ExecuteSale
드롭 즉시 팔지 않는다. SellZone은 **검증 + 드롭 소비 + 판매 예약**만 하고, 실제 변경(mutation)은
확인 모달 확정 시 `StoreView.ExecuteSale`에서 일어난다.

- 판매가: `StoreCatalog.Contains(key)`면 `price / 2`, 카탈로그 밖 아이템은 기본 10G
  (`StoreView.GetSellPrice`). **판매 수량 = 모달에서 선택(1~보유개수)**, 합계 = 판매가 x 수량.
- `StoreSellZone.OnDrop` (중요: **OnDrop이 소스 셀의 OnEndDrag보다 먼저 실행됨**) — 여기서는
  인벤토리를 일절 바꾸지 않는다:
  1. `pointerDrag`에서 `InventorySlotView` 획득, 없거나 `HasItem == false`면 return.
  2. MAIN 스토어 소속(`slot.Owner?.OwnerId() == InventorySystemManager.MainOwnerId`)이 아니면
     토스트 + `DropConsumed = true` + return — CHAR 스토어 판매는 장착 꼬임 방지를 위해 금지.
  3. `GetMainStore().FindBySlot(slot.SlotIndex)`로 스택 존재 확인.
  4. `InventorySlotView.DropConsumed = true;` — 소스 셀의 `HandleSlotDrop` 실행 차단
     (드롭은 소비됐고, 판매 여부는 모달에서 결정).
  5. `StoreView.RequestSell(...)` 호출(예약) → 구매와 **같은 확인 모달**이 Sell 모드로 열린다:
     수량 1~보유개수("n / max"), 합계 = 판매가 x 수량, "정말 판매하시겠습니까?" / "판매하기".
- 확정 시 `StoreView.ExecuteSale`:
  1. 슬롯 인덱스 + key로 스택을 **재조회** — 모달이 떠 있는 동안 인벤토리가 바뀌었을 수 있으므로,
     스택이 사라졌거나 key가 다르면 토스트로 안내하고 판매 취소.
  2. 부분 판매(수량 < 보유): `stack.count`에서 수량만 차감. 전량 판매: 스택 자체를 제거.
  3. `SaveStore` → `InventoryEvents.OnStoreChanged?.Invoke(MainOwnerId)` → `InventoryManager.EarnGold(총액)`
     (판매 대금은 소득 — 누적 획득(goldEarnedTotal) 집계) → `NotifySold(...)` 토스트 "이름 xN 판매 +NG".

### InventorySlotView 최소 패치 (유일한 기존 코드 수정)
`Prefabs/Assist/InventorySystem/Scripts/InventorySlotView.cs`에만 최소 패치:
1. `public string Key { get { return key; } }` — 페이로드 공개 getter.
2. `public static bool DropConsumed;` — 외부 드롭 소비 플래그(범용 프로토콜, Store를 모름).
3. `OnBeginDrag`에서 `DropConsumed = false;` 리셋.
4. `OnEndDrag`의 `owner.HandleSlotDrop(...)` 호출을 `if (!DropConsumed)`로 감싼다.
5. `OnDisable`에서 dragGhost 파괴 — 외부 드롭 처리(OnStoreChanged)가 그리드를 즉시 재구축하면
   드래그 중이던 셀이 비활성화되어 OnEndDrag가 생략되므로, 고스트를 셀 수명에 맞춰 정리한다.

의존 방향은 **Store → InventorySystem 단방향**. InventorySystem은 "누군가 드롭을 소비했다"만 안다.

---

## 3. 사용법

### A. 셋업 + 데모 씬
1. 메뉴 `Tools/Store/Setup All (catalog + UI prefab + font + demo scene)` 실행
   (개별 단계: `1. Create Catalog`(태그 레지스트리 + 태그별 상품 5종 + 프리뷰 상세 2종 + NoImage 스프라이트) →
   `2. Build UI Prefab`(주사위 리롤 아이콘 베이크, 확인 팝업 → 상점 패널 순 2종 베이크) →
   `3. Apply SUIT-Bold Font`(2종 모두) → `4. Build Demo Scene`(+StoreManager/프리뷰 리그 배치)).
   데모 씬은 `InventoryPanel.prefab`이 필요 — 커밋된 베이크 산출물이라 리포지토리에 있으며(생성 도구
   Tools/InventorySystem은 삭제됨), 없으면 에러 로그로 안내하고 패널 없이 계속 빌드한다.
2. `Assets/Prefabs/UI/Store/Demo/StoreDemo.unity` 열기 → **Play**.
3. 키: `S` 상점 토글 / `I` 인벤토리 토글 / `G` +500G / `1~4` MAIN에 데모 아이템 지급
   (1 치파오 / 2 선물(소) / 3 포즈: 댄스 / 4 바나나 — 판매 시연용) / `5` 포즈 프리뷰 리롤.
   포즈 탭에서는 페이지바 우측 **주사위 버튼**으로도 리롤할 수 있다(정지 시점이 랜덤이라 매번 다른 포즈).
4. **구매**: 탭 선택 → 카드 클릭 → 팝업에서 수량 조절(합계 확인) → "계산하기".
5. **판매**: 인벤토리 슬롯을 하단 **판매 존**으로 드래그 → 팝업에서 수량 선택(1~보유개수) →
   "판매하기". 판매가는 구매가의 50%(카탈로그 밖 아이템 10G).
6. 페이지: 상품 7종 이상 탭은 그리드 하단 `[ < n / m > ]`로 넘긴다.
7. batchmode 일괄 빌드:
   `Unity.exe -batchmode -quit -projectPath <proj> -executeMethod StoreTools.BatchBuildAll`

### B. 코드에서 호출
```csharp
storeView.Toggle();                       // 창 열기/닫기 (CanvasGroup)
storeView.SelectTab("선물");              // 탭 전환 (페이지 0으로 리셋)
int sell = storeView.GetSellPrice(key);   // 판매가 질의 (카탈로그 밖이면 10G)
```

### C. 상품 추가
1. 해당 태그의 StoreTagCatalog 에셋(예: 잡화 = `Resources/StoreMiscCatalog.asset`)에
   `StoreEntry{key, displayName, price, iconType, icon, detailText}` 1개 추가 — 레지스트리는 수정 불필요.
   **인스펙터에서 직접 추가해도 안전** — StoreTools의 카탈로그 갱신은 전부 additive(기존 엔트리
   불변 + 누락 기본 키만 추가, 빈 값만 보충)라 `Setup All` 재실행에도 보존된다.
2. 같은 key가 `InventoryCatalog`에 등록되어 있어야 구매(AddToMain) 성공 — 없으면 등록.
3. 장착물이면 `EquipCatalog`에도 같은 key 필요(기존 규약 그대로).
4. 아이콘: 기본은 `iconType=File` — `icon`에 스프라이트를 등록한다(비면 NoImage). 리그 캡처
   아이콘을 쓰려면 `iconType=Runtime` + 상세 카탈로그(`StoreDetailPoseCatalog`/
   `StoreDetailEffectCatalog`)에 캡처 설정(클립/파티클 프리팹)을 등록한다 — 미등재 Runtime 키는
   NoImage로 정착. 카드 보조 표기는 `detailText`(자유 텍스트 — 표시 전용, 성능 수치는 ItemSystem 소유).
5. 새 탭 = `Resources/StoreCatalog.asset`(태그 레지스트리)에 행 1개 + StoreTagCatalog 에셋 1개(슬롯 6 한도).
   탭 라벨/슬롯은 런타임에 재동기화되므로 프리팹 리베이크 불필요. 단 `"포즈"`/`"장착물"` 태그명은
   코드 상수와 결합(리롤 버튼/AF0005)이라 변경 금지.

---

## 4. 독립성

- **의존하는 것**: ① 골드 지갑 `InventoryManager`(Mission)의 4동사
  `EarnGold(소득)/AddGold(순수 변경)/RefundGold(환불)/SpendGold(소비)` + `InventoryChanged`,
  ② InventorySystem 공개 API(`AddToMain`/`GetMainStore`/`SaveStore`/`Catalog`/`InventoryEvents`)
  + 위 최소 슬롯 패치, ③ InventoryCatalog/EquipCatalog와 공유하는 문자열 key 공간,
  ④ `MissionList.Report` — AF0005 보고용(플레이 중 자동 생성 싱글톤).
- **건드리지 않는 것**: InventorySystem/EquipSystem/Mission의 코드(최소 슬롯 패치 제외),
  UIManager·실앱 씬, Vault 레거시. 카탈로그 additive 등록도 데이터 추가일 뿐 기존 엔트리는 불변.
- 이 폴더를 통째로 지워도 다른 시스템에 missing script가 생기지 않는다(프리팹·카탈로그·데모씬이
  전부 폴더 안). 슬롯 패치의 `DropConsumed`는 Store 없이도 무해한 범용 플래그로 남는다.

## 5. 남은 것 (후속 작업)
- **선물 → 친밀도 포인트 연동** — 증정 UX와 포인트 지급은 친밀도 시스템 구현 시. 능력
  수치(`affinityPoints`)는 **ItemSystem**(`Assets/Prefabs/Assist/ItemSystem`의 ItemGiftCatalog —
  별도 WORKLOG)이 소유하고, Store는 `detailText`("친밀도 +N")로 표시만 한다.
  설계 책임 이관: `../CharacterDetail/Affinity_Store_Integration.md`.
- **포즈 애니메이션 연동 구현** — Playables one-shot 기반 PoseSystem(PoseManager + equipped_pose
  저장). 검토 완료: `Store_PoseAnimation_Review.md` 1장. 클립 데이터 소스(`StoreDetailPoseCatalog`)는 구현됨.
- ~~포즈 아이콘~~ — **리얼타임 프리뷰로 구현됨**(`Scripts/StoreManager.cs` +
  `Test/StorePosePreviewRig.cs`, 위 "프리뷰 파이프라인" 절). 에디터 베이크(PNG → Sprite → InventoryCatalog 주입,
  `Store_PoseAnimation_Review.md` 2장)는 실앱에서 리그 상주가 부담될 때의 **대안으로 격하** —
  아이콘 고정/출하는 상점 엔트리 `iconType=File`(+icon)로 키 단위 대응이 이미 가능하다.
- 이펙트 아이템의 실제 발동 연동(쓰다듬기/클릭 파티클) — **정지컷 프리뷰는 구현됨**
  (`StoreDetailEffectCatalog` + 리그 이펙트 캡처), 실제 발동(쓰다듬기/클릭 연동)은 남음.
- 의상 탭(캐릭터별 가변 상품) — 캐릭터 컨텍스트 설계 후 예비 슬롯에 추가.
- 친밀도 단계 한정 상품(단계 미달 시 잠금 표시) / 상품 로테이션(기간/일일 갱신).
  설계 책임 이관: `../CharacterDetail/Affinity_Store_Integration.md`.
- UIManager/실앱 통합(데모 씬 → SampleScene 배선, 열기 버튼).
- 전용 장신구 판매불가 플래그(카탈로그에 sellable 필드).
- 아이템 마스터 시트 도입(카탈로그 3분할 동기화 빌더) — `Store_Design.md` 3장 개선안.
