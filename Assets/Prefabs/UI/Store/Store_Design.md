# Store(상점) 프로토타입 — 설계 문서

> 목적: Affinity_Plan(친밀도)의 재화 루프를 검증하는 **상점 프로토타입**.
> 원칙: StandAlone(데모 씬 단독 동작), 기존 시스템과의 연결은 최대한 약하게(문자열 key + 공개 API + 이벤트만).

## 1. 범위

- 탭이 있는 상점 창(구매) + **인벤토리 → 상점 드래그앤드롭 판매**.
- 재화는 `CurrencyManager`(Prefabs/Assist/ItemSystem)의 골드(`CurrencyManager.GoldKey`)를 사용.
  (설계 당시의 미션 지갑 InventoryManager는 골드 단일화로 삭제됨 — 4동사 대응: SpendGold→Spend / RefundGold→Refund / EarnGold→Earn / AddGold→Add)
  - 구매: `CurrencyManager.Instance.Spend(GoldKey, price)` → 성공 시 `InventorySystemManager.Instance.AddToMain(key, 1)`. AddToMain 실패 시 `Refund(GoldKey, price)`로 환불(실패 결제 되돌림 — 누적 소비 역가산).
  - 판매: 아래 SellZone 참조. `Earn(GoldKey, 총액)`(소득 — 누적 획득 집계).
  - 골드 표시: `CurrencyManager.CurrencyChanged` 구독(GoldKey 필터).
  - CH0007(골드 소비) 미션은 Spend만으로 자동 진행. 구매 키의 소속 태그가 "장착물"이면(`StoreCatalog.TagForKey(key)` 판정 — 엔트리에 tab 필드가 없어 레지스트리 역조회) `MissionList.Instance.Report("AF0005", 수량)` 호출(널가드).
- 친밀도 연동(선물 → 포인트)은 이번 범위 밖. WORKLOG에 남은 일로 기록. 연동 설계 소유는 `../CharacterDetail/Affinity_Store_Integration.md`로 이관.

## 2. 폴더/파일

```
Assets/Prefabs/UI/Store/
  README.md                  사용법·스크립트·튜닝 변수 안내 (폴더의 첫 문서)
  Store_Design.md            (이 문서)
  Store_PoseAnimation_Review.md  포즈 상품 검토서 (애니메이션 제어 / 스냅샷 아이콘)
  WORKLOG.md
  Scripts/
    StoreCatalog.cs          태그 레지스트리 (태그 ↔ StoreTagCatalog 매핑 — 카탈로그 3계층의 1계층)
    StoreTagCatalog.cs       태그(탭) 하나의 상품 카탈로그 (StoreEntry — tab 필드 없음)
    StoreDetailPoseCatalog.cs   포즈 키→클립 상세 카탈로그 (프리뷰 캡처용)
    StoreDetailEffectCatalog.cs 이펙트 키→파티클 프리팹 상세 카탈로그 (프리뷰 캡처용)
    StoreManager.cs          상시 상점 서비스 싱글톤 (프리뷰 캐시/NoImage/리롤/IconReady)
    StoreView.cs             메인 컨트롤러 (런타임 BindExisting 전용 — Build는 에디터 베이크 전용)
    StoreConfirmView.cs      구매/판매 겸용 확인 모달 (Buy/Sell — StorePanel에 자식으로 베이크)
    StoreSellZone.cs         IDropHandler 판매 존 (검증+예약만)
    StoreDemoController.cs   데모 입력 (S/I/G/1~4/5)
  Test/
    StorePosePreviewRig.cs   포즈 정지(20~80% 랜덤)/이펙트 정지컷 캡처 리그 — 기존 코드 복사·변조본(출처 주석)
  Editor/
    StoreTools.cs            Tools/Store/* 메뉴 + BatchBuildAll
  Resources/
    StoreCatalog.asset       (생성물 — 태그 레지스트리 5행)
    StoreEquipCatalog.asset  (생성물 — 장착물 태그 상품 4종)
    StorePoseCatalog.asset   (생성물 — 포즈 태그 상품 3종)
    StoreEffectCatalog.asset (생성물 — 이펙트 태그 상품 3종)
    StoreGiftCatalog.asset   (생성물 — 선물 태그 상품 3종)
    StoreMiscCatalog.asset   (생성물 — 잡화 태그 상품 3종)
    StoreDetailPoseCatalog.asset   (생성물 — 포즈 프리뷰 상세 3종)
    StoreDetailEffectCatalog.asset (생성물 — 이펙트 프리뷰 상세 3종)
    StoreNoImage.png         (생성물 — 'NO IMAGE' 플레이스홀더 스프라이트)
  Sprites/
    RerollDieIcon.png        (생성물 — 포즈 리롤 버튼 주사위 아이콘)
  Prefabs/
    StorePanel.prefab        (생성물 — 확인 팝업 "StoreConfirm" 자식 + 카탈로그 참조 베이크)
    StoreConfirm.prefab      (생성물, StorePanel에 자식으로 베이크 + 구버전 호환 참조 주입)
  Demo/
    StoreDemo.unity          (생성물)
```

`.meta` 파일은 절대 손으로 만들지 않는다(Unity 임포트 시 생성).
모든 클래스는 네임스페이스 없음(프로젝트 전역 어셈블리), 로그 프리픽스 `[Store][클래스명]`.

## 3. 데이터 모델 — 카탈로그 3계층 (StoreCatalog / StoreTagCatalog / StoreDetail*)

```csharp
// 1계층 — 태그 레지스트리 (Resources/StoreCatalog.asset). "어떤 탭이 있고 각 탭이 어느 카탈로그를
// 쓰는지"만 관리 — 새 상품 추가 시 이 에셋은 수정 불필요.
[Serializable] public class StoreTagEntry {
    public string tag;               // 탭 이름 (표시 문자열이자 식별자, 예: "포즈")
    public StoreTagCatalog catalog;  // 이 태그의 상품 카탈로그 (null 허용 — 빈 탭)
}
[CreateAssetMenu(menuName = "Store/Store Catalog (Tag Registry)")]
public class StoreCatalog : ScriptableObject {
    // 자체 캐시 없음 — 자식 StoreTagCatalog의 lazy map에 위임 (태그 수만큼의 O(1) 조회)
    public IReadOnlyList<StoreTagEntry> TagEntries;
    public List<string> Tabs();                    // 유효 태그 목록 (빈/중복 스킵, 중복 경고 1회 래치 — OnValidate 리셋)
    public StoreTagCatalog CatalogForTab(string tab);
    public List<StoreEntry> EntriesForTab(string tab);  // 자식 위임 (없으면 빈 리스트)
    public StoreEntry Get(string key);             // 태그 등록 순 자식 위임, 첫 히트 (없으면 null)
    public bool Contains(string key);
    public string TagForKey(string key);           // key가 속한 첫 태그 (미션 판정용)
}

// 2계층 — 태그별 상품 카탈로그 (태그당 에셋 1개)
public enum StoreIconType { File, Runtime }  // StoreTagCatalog.cs 정의 — File이 기본
[Serializable] public class StoreEntry {
    public string key;          // InventoryCatalog/EquipCatalog와 같은 키 공간
    public string displayName;  // InventoryCatalog에 없을 때 폴백 표기
    public int price = 100;     // 구매가(G)
    public StoreIconType iconType = StoreIconType.File;  // 아이콘 소스 — 상점 소유 (인벤토리 UI 아이콘과 별개)
    public Sprite icon;         // File 모드 아이콘 (비면 NoImage)
    public string detailText;   // 카드 보조 표기 자유 텍스트 (예: "친밀도 +100") — 표시 전용, 성능 수치 아님
    // tab 필드 없음 — 태그는 이 카탈로그를 참조하는 레지스트리 행이 결정한다
}
[CreateAssetMenu(menuName = "Store/Store Tag Catalog")]
public class StoreTagCatalog : ScriptableObject {
    // lazy map + OnValidate 무효화 + 중복키 경고 스킵
    public StoreEntry Get(string key);
    public bool Contains(string key);
    public IReadOnlyList<StoreEntry> Entries;
    public List<StoreEntry> ValidEntries();  // 등록 순서 유지, 빈 키/중복 키(대표 외) 제외
}

// 3계층 — 프리뷰 상세(Detail) 카탈로그 2종은 10장 참조 (StoreDetailPoseCatalog / StoreDetailEffectCatalog).
// 순수 캡처 설정만 담는다: Pose{key, clip, freezeMin, freezeMax} / Effect{key, effectPrefab, simulateTime}
// — 아이콘 소스(iconType)는 2계층 StoreEntry가 소유한다
```

- 판매가 규칙: `Contains(key)`면 `price / 2`, 아니면 기본 10G. (`StoreView.GetSellPrice(string key) : int`로 노출)
- **주의**: `"포즈"`/`"장착물"` 태그 이름은 코드 상수와 결합(리롤 버튼 표시 / AF0005 미션 보고 —
  `TagForKey` 판정)이라 변경 금지. 같은 key를 두 태그 카탈로그에 중복 등록하면 첫 태그가 우선
  (경고 없음 — 금지 관례).
- 카탈로그 초기 데이터(StoreTools.CreateCatalog가 태그 카탈로그 5종에 생성, 합계 16종):
  - 장착물(File 아이콘 — Assets/Model 스프라이트 PNG를 guid로 베이크): arona_a_chipao 300 / arona_a_idolfrontribbon 200 / arona_a_pareo 250 / hairpin_placeholder 150
  - 포즈(Runtime 아이콘): pose_greeting "포즈: 인사" 150 / pose_dance "포즈: 댄스" 300 / pose_sit "포즈: 앉기" 200
  - 이펙트(Runtime 아이콘): fx_pat_heart "쓰다듬기: 하트" 250 / fx_pat_star "쓰다듬기: 별" 250 / fx_click_sparkle "클릭: 반짝임" 200
  - 선물(detailText "친밀도 +10/+30/+100" — 표시 전용): gift_s "선물(소)" 50 / gift_m "선물(중)" 120 / gift_l "선물(대)" 300
  - 잡화: snack_banana "바나나" 10 / potion_energy "에너지 드링크" 30 / ticket_random "랜덤 티켓" 80
- **아이템 성능 데이터는 상점 소유가 아니다** — 선물의 친밀도 수치(`affinityPoints`)는
  ItemSystem(`Assets/Prefabs/Assist/ItemSystem`의 ItemGiftCatalog, 별도 WORKLOG)이 소유하고,
  Store는 `detailText`로 표시만 한다.
- **의상 탭은 제외** — 캐릭터별로 판매 내용이 바뀌는 카테고리라 프로토 범위 밖 (탭 예비 슬롯으로 대응).
- **상점 전용 키(포즈/이펙트/선물/잡화 12종)는 InventoryCatalog_Demo.asset에 추가 등록**(StoreTools가 SerializedObject로 additive 등록, 이미 있으면 스킵; displayName 지정, icon null — 인벤토리 UI용 메타일 뿐 **상점 아이콘은 별개로 StoreEntry가 소유**(10장), category "store"). InventorySystem **코드는 수정하지 않고 데이터만 추가**한다. AddToMain이 카탈로그 검증을 하므로 필수.

### 아이템 등록·관리 전략 (게임 내 아이템의 단일한 관리 방식)

아이템 정체성은 **문자열 key 하나**이고, 시스템별 관심사는 카탈로그로 분리한다:

| 카탈로그 | 역할 | 필수 여부 |
|---|---|---|
| InventoryCatalog | 아이템의 "존재" 등록부 — 표시 메타(displayName/icon/description/maxStack/category) | **모든 아이템 필수** (없으면 소유 불가) |
| EquipCatalog | 장착 메카닉 (프리팹/소켓/핏) | 장착물만 |
| StoreCatalog → StoreTagCatalog | 상거래 — 태그 레지스트리(탭 구성)와 태그별 상품(가격/아이콘 iconType·icon/보조 표기 detailText)의 2단 구조 | 상점 판매 품목만 |
| StoreDetailPose/EffectCatalog | 프리뷰 캡처 데이터 (클립 / 파티클 프리팹 — 순수 캡처 설정) | Runtime 아이콘 포즈/이펙트만 |
| ItemCatalog(ItemSystem, 폴더 밖) | 아이템 성능 데이터 — 선물 `affinityPoints` 등 (`Assets/Prefabs/Assist/ItemSystem`) | 성능 있는 아이템만 — **Store는 표시(detailText)만** |

- 등록 절차: 새 아이템 = ①InventoryCatalog 등록(필수) → ②장착물이면 EquipCatalog → ③판매하려면
  **해당 태그의 StoreTagCatalog 에셋에 엔트리 1개 추가**(레지스트리는 수정 불필요. 아이콘은
  `iconType=File`+`icon` 또는 `iconType=Runtime`) → ④Runtime 아이콘 포즈/이펙트면 Detail
  카탈로그에도 캡처 설정 등록(미등재 Runtime 키는 NoImage).
- 새 탭 추가 = 레지스트리(`StoreCatalog.asset`)에 행 1개 + StoreTagCatalog 에셋 1개(슬롯 6 한도 —
  초과분은 경고 후 절단). 탭 라벨/슬롯은 런타임 재동기화라 프리팹 리베이크 불필요.
- 키 네이밍 규칙(제안): 카테고리 접두사 — `pose_*`, `fx_*`, `gift_*`, 장착물은 캐릭터 접두사(`arona_a_*`).
- 현재 프로토의 한계: 등록이 에디터 툴(InventorySystemTools/StoreTools) 하드코딩에 분산되어 있다.
  **중기 개선안**: "아이템 마스터 시트"(json 또는 SO) 1곳에 전 아이템을 정의하고, 빌더가 각 카탈로그를
  생성/동기화하는 구조로 전환 — 카탈로그 간 키 불일치를 원천 차단. (남은 일로 관리)
- 포즈/이펙트는 현재 "아이템으로 소유 + 프리뷰 아이콘(10장)"까지 구현 — 실제 포즈 재생/파티클 발동 연동은 후속.

## 4. StoreView.cs — 메인 컨트롤러

**프리팹 완결 UI — 런타임은 BindExisting 전용.** 베이크 계층이 없으면 에러 로그 후 무동작하고
(built=false → 공개 API 전부 무동작), 런타임 코드 조립(Build 폴백)은 없다. `Build()`와 UI 팩토리
헬퍼는 전부 `#if UNITY_EDITOR`(플레이어 빌드에서 제외):

```csharp
public class StoreView : MonoBehaviour {
    // Awake: HasBakedHierarchy() ("HeaderBar" 존재 확인) ? BindExisting() : 에러 로그 + 무동작.
    //        Build()는 에디터 베이크(EditorBuild) 전용. 첫 페인트는 Start()
    public void Show(); public void Hide(); public void Toggle();   // CanvasGroup만 조작. SetActive 금지
    public void Refresh();                                          // 현재 탭·페이지 그리드 재구성 + 골드 갱신
    public void SelectTab(string tab);                              // 탭 변경 시 페이지 0으로 리셋
    public int GetSellPrice(string key);
    public void NotifySold(string displayName, int count, int gold); // SellZone이 호출 → 토스트 + 골드 갱신
    public void ShowToast(string message);                           // 2초 후 자동 소거(Invoke/CancelInvoke)
#if UNITY_EDITOR
    public void EditorBuild(Sprite roundedSprite, TMP_FontAsset font); // Build() 래퍼
    public void EditorSetConfirmPrefab(GameObject prefab);            // 확인 팝업 프리팹 주입 (구버전 호환)
    public void EditorSetRerollSprite(Sprite sprite);                 // 리롤 버튼 주사위 아이콘 주입 (베이크 시)
    public void EditorSetCatalog(StoreCatalog registry);              // 카탈로그 참조 베이크 (런타임 Resources 폴백 최소화)
#endif
}
```

### 계층 구조 (자식 이름 = 바인딩 계약, 절대 변경 금지)

```
StorePanel (RectTransform 520x560, 포인트 앵커, Image RootBg(0.09,0.09,0.11,0.96) raycastTarget=true, CanvasGroup)
├─ Handler            ← 첫 번째 자식. full-stretch, Image(0,0,0,0) raycastTarget=true, DragUIHandler
├─ HeaderBar          (상단 고정앵커 스트립 h40) : TitleText "상점", GoldText(우측, "1,234 G"), CloseButton "X"
├─ Body               (top 80 ~ bottom 114 고정앵커, PanelBg)
│   ├─ GoodsGrid      GridLayoutGroup cell(150,160) spacing 8 padding 8, childAlignment UpperLeft
│   │                  (좌상단 정렬 — 미완성 행이 가운데로 몰리지 않는다) — 페이지당 6장(3x2) 고정, 스크롤 없음
│   └─ PageBar        (Body 내부 하단 h22) : PagePrevButton "<" / PageLabel "1 / 3" / PageNextButton ">" /
│                      PoseRerollButton (주사위 아이콘 20x20, x=+235 — 포즈 탭에서만 표시, 포즈 프리뷰 리롤)
├─ TabBar             (top 44, h42 = 표시 36 + 본문 겹침 6 — Body 뒤 형제라 본문 위에 그려짐. 폴더 감성)
│                      TabButton_0 ~ TabButton_5 (슬롯 6개 — catalog.Tabs()의 태그 수만큼 사용(현재 5:
│                      장착물/포즈/이펙트/선물/잡화), 태그 없는 슬롯 숨김. 카탈로그 부재 시 DefaultTabs
│                      폴백, 한도 초과 태그는 경고 1회 후 절단. 라벨/슬롯은 Refresh마다 재동기화,
│                      클릭 리스너는 예비 슬롯에도 걸린다 — 런타임 태그 추가 대응)
│                      선택 탭 = PanelBg(본문과 같은 색으로 이어짐) + 전체 높이 / 비선택 = HeaderBg + 6px 낮음 + 라벨 muted
├─ CardTemplate       (루트 직속, 비활성, Grid 밖) : CardIcon / CardName / CardPrice("100 G") /
│                      CardSub(StoreEntry.detailText 직표시 — 예: "친밀도 +10", 비면 공백) /
│                      CardOwned("보유 n") / 전체가 Button(확인 팝업 열기)
├─ SellZone           (하단 고정앵커 스트립 h84, Image Track(0.047,0.055,0.071,0.9) raycastTarget=true,
│                      StoreSellZone 컴포넌트) : SellZoneText "판매: 인벤토리 아이템을 여기로 드래그 (구매가의 50%)"
├─ ToastText          (SellZone 위 고정앵커, raycastTarget=false, 기본 빈 문자열)
└─ StoreConfirm       (베이크된 확인 팝업 자식 — Backdrop + ConfirmPanel, CanvasGroup 숨김 상태.
                       BindExisting이 바로 연결한다)
```

- 루트/헤더/탭/페이지바/SellZone은 **레이아웃 그룹 금지, 고정 앵커** (신규 정적 UI 규칙). 레이아웃 그룹은 동적 콘텐츠인 GoodsGrid에만 허용.
- 텍스트는 전부 TMP, raycastTarget=false. 버튼 배경 Image만 raycastTarget=true.
- **구매 = 카드 클릭 → StoreConfirm 팝업(Buy 모드, 수량 1~남은 적재 가능량 "n / max" + 최종금액) → "계산하기"**. 골드 부족 시 토스트 "골드가 부족합니다" + GoldText 빨강 플래시(0.5초). AddToMain 실패 시 전액 환불.
- 페이징: `EntriesForTab` 결과를 6장씩 잘라 표시. PageLabel "현재+1 / 총페이지", 경계에서 버튼 interactable off.
- 보유 수: `InventorySystemManager.Instance.GetMainStore().CountOf(key)` (널가드). `InventoryEvents.OnStoreChanged` 구독으로 갱신.
- 이름: `InventorySystemManager.Instance.Catalog.Get(key)`의 displayName 우선, 없으면 StoreEntry.displayName
  (InventoryCatalog 참조는 이름/maxStack뿐 — 아이콘은 참조하지 않는다).
  아이콘: `StoreManager.ResolveIcon` = 상점 엔트리 `iconType` 기준 (File) 등록 icon / (Runtime) 캡처 캐시
  → 없으면 NoImage 폴백(10장 — PNG 부재 시 아이콘 숨김 + 이름 텍스트).
- 팔레트: RootBg(0.09,0.09,0.11,0.96) HeaderBg(0.125,0.141,0.173) PanelBg(0.137,0.157,0.196) ButtonBg(0.22,0.25,0.31) AccentBlue(0.243,0.325,0.502) AccentBlueHi(0.306,0.404,0.608) Track(0.047,0.055,0.071) TextWhite(0.92,0.93,0.95) TextMuted(0.6,0.62,0.66) FlashRed(0.95,0.35,0.35 — 골드 부족 플래시/합계 경고) GoldYellow(0.95,0.78,0.30). 라운드 스프라이트: builtin `UI/Skin/UISprite.psd` Sliced.

### StoreConfirmView.cs — 구매/판매 겸용 확인 모달 (StorePanel에 자식으로 베이크)

- StorePanel.prefab에 "StoreConfirm" **자식으로 베이크**되는 모달(패널 전체 덮는 Backdrop + 중앙 340x280 ConfirmPanel). CanvasGroup 표시/숨김, 베이크 기본 상태 = 숨김. 런타임 코드 조립 없음 — 베이크 계층이 없으면 에러 로그 후 무동작(`Build()`는 `#if UNITY_EDITOR`).
- **`StoreConfirmMode { Buy, Sell }`** — 하나의 모달이 구매 확인과 판매 확인을 겸한다. 계층/프리팹은 동일하고 모드에 따라 문구만 전환:
  - Buy: 타이틀 "구매 확인" / 메시지 "정말 계산하시겠습니까?" / 확인 버튼 "계산하기".
  - Sell: 메시지 "정말 판매하시겠습니까?" / 확인 버튼 "판매하기".
- 구성(자식 이름 계약): Backdrop(클릭=닫기) / ConfirmPanel / ConfirmTitle / ItemIcon / ItemNameText /
  QtyMinusButton "-" / QtyText / QtyPlusButton "+" / TotalText "합계 N G" / ConfirmMessageText /
  CancelButton "취소" / ConfirmButton.
- **`Open(mode, itemKey, displayName, icon, unitPrice, maxQty, Action<int> onConfirm)`** — 수량 1로 초기화, 범위 1~`maxQty`, 경계에서 +/- 버튼 interactable off. 합계 = `unitPrice` x 수량. itemKey는 열려 있는 동안 보관(currentKey)되어, 프리뷰 캡처/리롤이 늦게 끝나면 **`UpdateIcon(key, sprite)`**(StoreView가 IconReady를 중계 — 같은 키 + 열림 상태일 때만 교체)로 모달 아이콘이 갱신된다.
  - **수량 표시는 "n / max" 형식** (예: "3 / 12") — 상한이 항상 보인다.
  - `maxQty` 의미: Buy = **남은 적재 가능량**(인벤토리 스택 잔여 공간 — 더 못 담는 수량은 애초에 못 고른다), Sell = **보유개수**(드래그한 스택의 count).
  - Buy 모드에서 합계가 보유 골드를 넘으면 TotalText 빨강 표시(결제 최종 판정은 StoreView).
- 확인 시 선택 수량으로 `onConfirm(qty)` 콜백 — 실제 골드/인벤토리 변경은 StoreView(구매 결제 경로 / 판매 `ExecuteSale`)가 수행하고, 모달은 수량 선택 UI일 뿐이다.
- 베이크: StoreTools가 StoreConfirm.prefab을 **먼저** 굽고, 저장된 에셋을 다시 로드해 StorePanel의
  **자식으로 인스턴스해 함께 베이크**한다(+구버전 호환용 `EditorSetConfirmPrefab` 참조 주입).
  런타임은 BindExisting이 베이크된 자식을 바로 연결 — 자식이 없는 구버전 프리팹만 confirmPrefab
  참조로 1회 인스턴스하고, **코드 자가 구축 폴백은 없다**. 팝업을 못 찾으면 구매/판매는 토스트로
  거부된다(즉시구매/전량판매 폴백 제거 — 확인 없는 거래 금지).

## 5. 판매 프로토콜 — StoreSellZone + StoreView (검증 → 예약 → 모달 → ExecuteSale)

드롭 즉시 판매하지 않는다. 역할 분리:
- **StoreSellZone(IDropHandler)**: 드롭 **검증** + 드롭 **소비**(DropConsumed) + `StoreView.RequestSell` **예약**. 인벤토리/골드를 일절 변경하지 않는다.
- **StoreView.RequestSell**: 구매와 **같은 확인 모달**을 Sell 모드로 연다(수량 선택).
- **StoreView.ExecuteSale**: 모달 확정 시에만 실제 변경(mutation) 수행.

```csharp
public class StoreSellZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] private StoreView owner;   // 토스트/판매 예약 대상
    public void OnDrop(PointerEventData eventData) { ... }   // 검증 + 소비 + RequestSell만
}
```

### 5.1 StoreSellZone.OnDrop — 검증 + 예약 (순서 중요: **OnDrop은 소스 셀 OnEndDrag보다 먼저 실행됨**)

1. `eventData.pointerDrag?.GetComponent<InventorySlotView>()` — 없으면 return.
2. `slot.HasItem == false` → return.
3. `slot.Owner?.OwnerId() != InventorySystemManager.MainOwnerId` → 토스트 "메인 인벤토리 아이템만 판매할 수 있습니다" + `InventorySlotView.DropConsumed = true` + return. (CHAR 스토어 판매는 장착 꼬임 방지를 위해 금지)
4. `GetMainStore().FindBySlot(slot.SlotIndex)`로 스택 존재 확인 — null → return.
5. `InventorySlotView.DropConsumed = true;` — 소스 셀의 HandleSlotDrop 실행 차단. **판매 여부와 무관하게 드롭 자체를 소비**한다(모달을 띄우는 시점에 드래그를 원위치 처리로 넘기지 않기 위함).
6. `owner.RequestSell(key, 슬롯 인덱스, 스택 개수)` — 여기서 OnDrop 종료. 스택 제거/골드 지급 없음.

### 5.2 StoreView.RequestSell — Sell 모드 모달 오픈

- `GetSellPrice(key)`로 판매 단가 계산 후 `StoreConfirmView.Open(Sell, 키, 표시이름, 아이콘, 판매단가, maxQty = 보유개수, onConfirm)`.
- 아이콘은 구매와 같은 체인(`StoreManager.ResolveIcon` → NoImage 폴백) — 판매 대상은 카드가 만들어진 적 없는 키일 수 있어(다른 탭에서 드래그), 프리뷰 키인데 캐시가 없으면 여기서 캡처를 요청하고 완료 시 IconReady → UpdateIcon으로 열린 모달에 반영된다.
- 모달: 수량 1~보유개수("n / max" 표시), 합계 = 판매가 x 수량, "정말 판매하시겠습니까?" / "판매하기". 백드롭/취소 = 판매 없이 닫기.

### 5.3 StoreView.ExecuteSale — 확정 시 실제 판매 (유일한 mutation 지점)

1. `GetMainStore().FindBySlot(슬롯 인덱스)`로 스택 **재조회**. 모달이 떠 있는 동안 인벤토리가 바뀔 수 있으므로(다른 판매/장착/이동), **스택이 없거나 key가 예약 시점과 다르면 토스트로 안내하고 판매 취소**(slot+key 이중 검증).
2. 수량 처리: 선택 수량 < `stack.count` → `stack.count`에서 수량만 차감(부분 판매). 선택 수량 == `stack.count` → `store.stacks.Remove(stack)`(전량 판매, 같은 키 중복 스택 대비 키가 아닌 스택 자체를 제거).
3. `InventorySystemManager.Instance.SaveStore(store); InventoryEvents.OnStoreChanged?.Invoke(InventorySystemManager.MainOwnerId); CurrencyManager.Instance.Earn(CurrencyManager.GoldKey, 판매가 x 수량);` — 판매 대금은 소득(Earn — 누적 획득 집계).
4. `NotifySold(표시이름, 수량, 총액)` → 토스트 "이름 xN 판매 +NG".

호버 시 SellZone 배경을 AccentBlue로 하이라이트(Enter/Exit)는 기존과 동일.

## 6. InventorySystem 최소 패치 (유일한 기존 코드 수정)

`Prefabs/Assist/InventorySystem/Scripts/InventorySlotView.cs`에만 최소 패치:

1. `public string Key { get { return key; } }` — 페이로드 공개 getter.
2. `public static bool DropConsumed;` — 외부 드롭 소비 플래그(범용 프로토콜, Store를 모름).
3. `OnBeginDrag`에서 `DropConsumed = false;`
4. `OnEndDrag`에서 `owner.HandleSlotDrop(...)` 호출을 `if (!DropConsumed)`로 감싼다.
5. `OnDisable`에서 dragGhost 파괴 — 외부 드롭이 OnStoreChanged로 그리드를 즉시 재구축하면
   드래그 중이던 셀이 비활성화되어 OnEndDrag가 생략되므로, 고스트를 셀 수명에 맞춰 정리한다.

Store 방향 참조 없음 — InventorySystem은 "누군가 드롭을 소비했다"만 안다.

## 7. StoreDemoController.cs — 데모 입력 (레거시 Input)

```csharp
[SerializeField] StoreView storeView;          // S: storeView.Toggle()
[SerializeField] InventoryView inventoryView;  // I: inventoryView.Toggle()
[SerializeField] KeyCode toggleStoreKey = KeyCode.S;
[SerializeField] KeyCode toggleInventoryKey = KeyCode.I;
[SerializeField] KeyCode grantGoldKey = KeyCode.G;   // +500G (CurrencyManager.Add — 집계 무반응 데모 충전)
[SerializeField] KeyCode rerollPoseKey = KeyCode.Alpha5;  // 포즈 프리뷰 리롤 (StoreManager.RerollPoses)
// 1~4: InventorySystemManager.AddToMain(각 데모 키 1개) — 판매 시연용
```

Start()에서 FindFirstObjectByType 폴백. Update()는 `Input.GetKeyDown`만 사용.

## 8. StoreTools.cs — 에디터 파이프라인 (InventorySystemTools 패턴)

- `[MenuItem("Tools/Store/Setup All (catalog + UI prefab + font + demo scene)")] SetupAll()`
- `[MenuItem("Tools/Store/1. Create Catalog")] CreateCatalog()` — 실행 순서: ①**레거시 프리뷰 에셋 정리**(클래스 리네임으로 Detail 타입이 된 구 StorePoseCatalog.asset/StoreEffectCatalog.asset 삭제 — 태그 카탈로그가 같은 경로를 쓰므로 자리 확보. 이미 신형 StoreTagCatalog 타입이면 타입 불일치로 로드가 null이라 삭제되지 않음) → ②태그별 상품 카탈로그 5종(`CreateTagCatalog` — StoreEquip/StorePose/StoreEffect/StoreGift/StoreMiscCatalog.asset. **장착물 4종 아이콘은 Assets/Model 스프라이트 PNG를 guid로 찾아 베이크**(`LoadSpriteByGuid`), 포즈/이펙트 6종은 Runtime, 선물 3종은 detailText "친밀도 +N") → ③태그 레지스트리(StoreCatalog.asset — 기존 에셋 재사용으로 guid 보존, 프리팹 직렬화 참조 유지) → ④InventoryCatalog_Demo에 상점 전용 키 additive 등록 → ⑤프리뷰 상세 카탈로그 2종(StoreDetailPoseCatalog.asset 포즈 3종 클립 / StoreDetailEffectCatalog.asset 이펙트 3종 파티클 프리팹 — Fx_LoveAura/CFXR4 Falling Stars/CFXR2 Shiny Item (Loop), 읽기 전용 참조) → ⑥NoImage 스프라이트 베이크(`EnsureNoImageSprite`).
  **additive 보존(관리 흐름의 핵심)**: 레지스트리(태그 행)/태그 카탈로그(상품)/상세 카탈로그(프리뷰 메타) 전부 **기존 엔트리 불변 + 누락 기본 키만 추가**한다 — 기본 태그 행의 catalog 참조가 비었으면 채우고, 기본 키의 **빈 값만 보충**한다(clip/effectPrefab 바인딩, File 행의 빈 icon, 빈 detailText). 인스펙터에서 사용자가 추가·수정한 상품/설정(iconType/icon/detailText/freeze 등)이 `Setup All` 재실행에도 보존된다.
  **스키마 이행**: 구 스키마(giftPoints 시절) 에셋은 iconType/icon/detailText가 File(0)/null/""로 로드되므로 이 빈 값 보충이 이행 경로가 된다 — 단 "File + 빈 icon"은 사용자 의도로 볼 수 없어, 기본이 Runtime인 키(포즈/이펙트)는 **Runtime으로 승격**해 캡처 대상으로 복귀시킨다. `EnsureNoImageSprite`/`EnsureRerollIcon`은 기존 파일을 재사용한다(재베이크 = PNG 삭제 후 재실행).
- `[MenuItem("Tools/Store/2. Build UI Prefab")] BuildUiPrefab()` — **카탈로그 선행 보장**(레지스트리 존재 + `Tabs()` 비어있지 않음 — 미비 시 CreateCatalog) 후 임시 GO → `EditorSetConfirmPrefab` + `EditorSetRerollSprite(EnsureRerollIcon())` + `EditorSetCatalog(registry)` → `StoreView.EditorBuild(builtin UISprite, SUIT-Bold)` → **확인 팝업을 "StoreConfirm" 자식으로 인스턴스해 함께 베이크** → SaveAsPrefabAsset → finally DestroyImmediate
- `[MenuItem("Tools/Store/3. Apply SUIT-Bold Font")] ApplyFont()` — LoadPrefabContents → 전 TMP_Text font 교체(Assets/FontAssets/SUIT-Bold.asset) → Save → finally Unload. LoadPrefabContents 전 존재 확인 필수
- `[MenuItem("Tools/Store/4. Build Demo Scene")] BuildDemoScene()` — 카탈로그 선행 보장은 레지스트리 **"존재 + `Tabs()` 비어있지 않음" 검사**(구 스키마 에셋은 guid가 보존돼 신형 StoreCatalog로 로드되지만 tags가 비어 상점이 조용히 텅 빈다) — 미비 시 CreateCatalog 재실행
- `public static void BatchBuildAll()` — 다이얼로그 없이 전 단계 + AssetDatabase.SaveAssets (batchmode -executeMethod 진입점)

### 데모 씬 구성 (InventorySystemTools.BuildDemoScene 레시피)

- Main Camera(SolidColor 0.06,0.07,0.09) / EventSystem+StandaloneInputModule(pixelDragThreshold=5)
- Canvas ScreenSpaceOverlay + CanvasScaler ScaleWithScreenSize **2560x1440 match 0.5** + GraphicRaycaster (InventoryPanel과 크기 호환)
- GO "InventorySystemManager" + GO "EquipManager" + GO "StoreManager" (Awake에서 Resources 카탈로그 자동 로드; 캐릭터는 배치하지 않음. StoreManager는 없어도 플레이 중 자동 생성되지만 명시 배치)
- `Assets/Prefabs/Assist/InventorySystem/InventoryPanel.prefab` 인스턴스 — MAIN 섹션, 우측(anchor/pivot (1,0.5), pos (-60,0)). **없으면 에러 로그로 안내하고 패널 없이 계속 빌드(직접 빌드하지 않음 — 약결합). InventoryPanel.prefab은 커밋된 베이크 산출물(생성 도구 Tools/InventorySystem은 삭제됨 — 리포지토리에서 복원)**
- StorePanel.prefab 인스턴스 — 좌측(anchor/pivot (0,0.5), pos (60,0)). Show 상태로 배치하되 S로 토글
- GO "StoreDemoController" — 위 참조 연결(에디터 전용 `EditorSet(...)` 세터)
- InfoCanvas(레거시 UI Text, raycastTarget=false, GraphicRaycaster 없음): "S: 상점 / I: 인벤토리 / G: +500G / 1~4: 아이템 지급 / 5: 포즈 리롤 / 카드 클릭: 구매 / 슬롯→판매존 드래그: 판매"
- MarkSceneDirty + SaveScene(Demo/StoreDemo.unity)

## 9. 준수 사항 체크리스트

- [ ] 프리팹은 코드 빌드(에디터 전용) → 정적 베이크, 폰트는 마지막에 SUIT-Bold 일괄 적용
- [ ] 프리팹 완결 UI: 런타임은 BindExisting 전용(베이크 계층 없으면 에러 로그 + 무동작),
      Build()/팩토리 헬퍼는 #if UNITY_EDITOR — 플레이어 빌드에서 제외
- [ ] Show/Hide는 CanvasGroup만 (SetActive 금지)
- [ ] 정적 크롬 고정 앵커, 레이아웃 그룹은 동적 그리드만
- [ ] Handler 오버레이(투명 Image + DragUIHandler) 첫 자식
- [ ] 레거시 Input만, InputSystem 임포트 금지
- [ ] .meta 손 생성 금지 / Editor 코드는 Editor/ 폴더 / #if UNITY_EDITOR 가드
- [ ] InventorySystem 수정은 6장 최소 패치(5항목)가 전부

## 10. 프리뷰 서비스 — StoreManager + StorePosePreviewRig (리얼타임 아이콘 캡처)

포즈(`pose_*`)/이펙트(`fx_*`) 상품은 상점 엔트리가 `iconType=Runtime`이라, 등록 스프라이트 대신
플레이 중 실시간 캡처로 카드/확인 모달 아이콘을 채운다. `Store_PoseAnimation_Review.md` 2장의
(b)안(런타임 캡처)에 해당 — 에디터 베이크 (a)안은 `iconType=File`(+icon)로 키 단위 대응.

### 왜 매니저(캐시/정책)와 리그(캡처 기계)를 나눴나

- **수명이 다르다**: StoreView(창)는 꺼질 수 있고, 리그는 씬 소속이라 씬 전환으로 사라진다.
  캡처 결과·요청 상태를 창이나 리그에 두면 창을 닫거나 씬을 바꿀 때마다 증발한다. 그래서
  캐시(previewCache)·요청 상태(pendingKeys 중복 방지 / failedKeys 실패 확정)·NoImage 폴백 정책은
  **상시 StoreManager(DontDestroyOnLoad)** 가 소유하고, 리그는 "엔트리를 받아 캡처해 콜백"만 하는
  **캐시 없는 기계**로 남긴다.
- 매니저는 리그를 매 호출 `StorePosePreviewRig.Instance`로 조회한다(참조 캐시 금지 — 씬 전환 대비).
- **실패 계약**: 리그는 파괴/비활성/영구 비활성(캐릭터 프리팹·Animator 부재)의 **모든 경로에서
  진행 중+대기 요청에 `onDone(entry, null)`을 통지**(`DrainRequestsWithFailure`)한다 — 리그가 캡처
  도중 죽어도 상시 매니저의 pendingKeys가 고착되지 않는다. 실패 키는 failedKeys로 확정되어
  재요청이 차단되고, 리롤로만 해제된다.

### 계층 (데모 씬, StoreTools.BuildDemoScene이 배치)

```
StoreManager        (GO — 상시 서비스. 씬에 없어도 플레이 중 Instance getter가 자동 생성 +
                     DontDestroyOnLoad. 에디트 모드에서는 Instance가 항상 null — 베이크 경로가
                     매니저 없이 돈다. Awake에서 Resources의 태그 레지스트리 "StoreCatalog"와
                     프리뷰 상세 카탈로그 2종 "StoreDetailPoseCatalog"/"StoreDetailEffectCatalog" 로드)
StorePosePreviewRig (GO, 씬 (0,-1000,0) — 화면 밖 숨김. EditorSet으로 캐릭터 프리팹만 주입 —
│                    상세 카탈로그는 StoreManager가 Resources에서 로드)
├─ Holder           캐릭터를 비활성 상태로 인스턴스화(앱 스크립트 Awake/OnEnable 차단) → 스트립 후 활성화
│   └─ (캐릭터 인스턴스)  characterPrefab(아로나 POC) 복제 — MonoBehaviour 전부 스트립(다중 패스,
│                      Animator는 생존), 본인+자식 전부 레이어 6(PortraitModel)으로 격리
├─ FxHolder         (이펙트 캡처 중에만 존재) 비활성 인스턴스화 → 스트립 → 활성화 → 캡처 후 파괴
├─ RigCamera        enabled, targetTexture = RenderTexture(iconSize=256², 24, ARGB32),
│                    cullingMask = 1<<6, SolidColor 클리어(0.11,0.12,0.15,1), 렌더러 bounds 프레이밍
└─ RigLight         Directional, cullingMask = 1<<6
```

데모 Main Camera는 cullingMask에서 레이어 6을 제외(`~(1<<6) & 기존`). NameToLayer 실패 시
레이어 변경을 생략하는 폴백.

### 책임 분리

- **StoreManager**(`Scripts/`, 상시 싱글톤): `ResolveIcon(key)`(**상점 엔트리 `iconType` 기준** —
  (File) 등록 icon / (Runtime) 캡처 캐시 → null. **InventoryCatalog은 조회하지 않는다** — 상점
  아이콘은 인벤토리 UI 아이콘과 완전 별개. NoImage는 호출측이 씌움) /
  `NoImageSprite`(베이크된 Resources "StoreNoImage" **단일 소스** — 없으면 경고 1회 후 null, 호출측이
  아이콘 숨김. 런타임 절차 생성 폴백 없음) / `IsPreviewKey(key)`(상점 엔트리 iconType == Runtime **+
  상세 카탈로그 등재** — 미등재 Runtime 키는 false → NoImage 정착) / `RequestPreview(key)`(비 Runtime·
  Detail 미등재·캐시·pending·failed·리그 부재 시 무동작. pending 등록이 리그 요청보다 먼저 — 리그가
  실패를 동기 콜백할 수 있다) / `RerollPoses()`(**상점 엔트리가 Runtime 모드인 포즈 키만** 강제
  재캡처 — 레지스트리 부재 시 판정 불가라 전체 스킵) / `IconReady(key, sprite)` 이벤트.
  OnDestroy에서 캐시 스프라이트/텍스처 정리.
- **아이콘 소스 모드**: 상점 엔트리(StoreEntry) 필드 `iconType`(enum `StoreIconType`,
  StoreTagCatalog.cs 정의) + `icon`. **File**(기본) = 등록 스프라이트가 곧 실아이콘(비면 NoImage),
  **Runtime** = 리그 캡처 + 리롤 대상(Detail 카탈로그 등재 키만 유효) — 아이콘 고정/출하 시
  키 단위로 File로 전환한다.
- **StoreDetailPoseCatalog / StoreDetailEffectCatalog**(`Scripts/`, 신규 원본 코드):
  `StoreDetailPoseEntry{key, clip, freezeMin=0.2, freezeMax=0.8}` /
  `StoreDetailEffectEntry{key, effectPrefab, simulateTime=1.5}` — **순수 캡처 설정**(아이콘 소스는
  StoreEntry 소유). 같은 lazy-map 패턴, 에셋은 Resources(StoreTools 1단계가 기본 3종을 additive로
  보장, 클립/프리팹 해석 실패 시 에러 로그 + null → 리그가 널가드). 태그 카탈로그(상품/가격/아이콘)와
  분리된 3계층 — 프리뷰 캡처 메타만 담는다.
- **StorePosePreviewRig**(`Test/` — `animationplayermanager.cs` 포즈 프리즈 +
  `InventorySystemTools.cs` 스트립/프레이밍의 복사·변조본이라 출처 헤더와 함께 Test에 격리):
  `Instance`(Awake 설정/OnDestroy 해제, 자동 생성 없음) / `IsDisabled`(영구 캡처 불가 확정) /
  `RequestPoseCapture(entry, onDone)` / `RequestEffectCapture(entry, onDone)`(큐 → 코루틴 순차 캡처) /
  `EditorSet(prefab)`(에디터 전용 주입 — 캐릭터 프리팹만). 캡처 스프라이트의 수명(파괴)은
  StoreManager 캐시가 관리.
- **StoreView**: 카드/모달 아이콘 = `ResolveIcon` → 없으면 NoImage를 우선 표시 → 프리뷰 키면
  `RequestPreview`. `IconReady` 구독으로 살아 있는 카드(`Card_<key>`, FindDeepChild + 파괴 가드)와
  열린 확인 모달(`StoreConfirmView.UpdateIcon`)에 반영. 리그가 씬에 없어도 NoImage로 완전 동작
  (약결합 — 매니저의 RequestPreview가 무동작으로 끝난다).

### 캡처 시퀀스

포즈 (요청 1건당):
1. 매니저: pendingKeys 등록 → `rig.RequestPoseCapture(entry, 콜백)` — 리그 큐에 적재.
2. 리그 코루틴: 캐릭터 렌더러 복원(직전 이펙트 캡처 대비) → PlayableGraph + AnimationClipPlayable로
   `normalizedTime = Random.Range(freezeMin, freezeMax)`(기본 20~80% 랜덤 정지) 지점에서
   `SetSpeed(0)` + `SetTime` + `graph.Evaluate()` — 포즈 프리즈 → 포즈 바운드로 재프레이밍.
3. 한 프레임 + `WaitForEndOfFrame` → 리그 카메라 RT에서 `ReadPixels` → `Sprite.Create` → 콜백.

이펙트 (요청 1건당):
1. 캐릭터 렌더러 전부 off(이펙트 단독 아이콘) → **비활성 FxHolder**에 effectPrefab Instantiate →
   MonoBehaviour 스트립(CFXR_Effect 대비) → PortraitModel 레이어 → 활성화.
2. **최상위 ParticleSystem들만 `Simulate(simulateTime, withChildren: true, restart: true)`로 정지컷** —
   중첩 시스템은 부모의 withChildren이 함께 처리하고, Simulate 후 일시정지 상태로 남아 다음
   프레임에도 같은 그림이 렌더된다.
3. 이펙트 바운드로 프레이밍(살아있는 파티클 0개면 바운드 퇴화 — 반경 하한 클램프) → 한 프레임 +
   `WaitForEndOfFrame` → RT 캡처 → FxHolder 파괴 + 캐릭터 렌더러 복원 → 콜백.

공통 (매니저 측 완료 처리):
- 실패(sprite == null): pending 해제 + failedKeys 확정(리롤로만 해제).
- 성공: previewCache 갱신 → **`IconReady` 브로드캐스트 → 그 뒤에 옛 스프라이트/텍스처 파괴** —
  구독자(카드/모달)가 교체를 마치기 전에 파괴하면 파괴된 참조가 흰 사각형으로 그려진다.
- 리그는 OnDestroy에서 PlayableGraph/RenderTexture 정리.

### NoImage / 리롤 UX

- 실아이콘이 전혀 없는 카드/확인 모달은 `Resources/StoreNoImage.png`(256², 에디터 베이크: 라운드
  사각 + 5x7 픽셀폰트 "NO IMAGE")를 우선 깔고, 캡처가 끝나면 교체된다. **베이크 PNG가 단일 소스** —
  없으면 매니저가 경고 1회 후 null을 반환하고 카드/모달은 아이콘을 숨긴다(이름 텍스트만, 런타임
  절차 생성 폴백 없음). 판매(Sell) 모달도 구매와 같은 체인 + 프리뷰 키면 그 자리에서 캡처 요청.
- 포즈 리롤: PageBar 우측 주사위 버튼(`PoseRerollButton`, 20x20, x=+235, 포즈 탭에서만 표시 —
  Refresh 말미 `UpdatePoseRerollVisibility`) 또는 데모 키 5(`StoreDemoController.rerollPoseKey`) →
  `RerollPoses()` — **상점 엔트리가 Runtime 모드인 포즈 키만** 캐시 유무와 무관하게 강제 재캡처
  (failed/pending 해제 후 재등록. File 모드는 등록 스프라이트 고정이라 제외). 정지 시점이 랜덤이라
  리롤마다 다른 포즈가 나온다.

한계: MagicaCloth 스트립으로 머리카락/치마는 바인드 포즈(기존 한계 유지), 배경 단색(투명 아님),
포즈는 랜덤 정지 위치·이펙트는 파티클 랜덤 시드라 매 캡처 그림이 달라진다. 아이콘 고정/출하가
필요하면 해당 키 StoreEntry의 `iconType`을 File로 전환하고 `icon`을 등록한다(에디터 베이크 대안의
키 단위 구현).
