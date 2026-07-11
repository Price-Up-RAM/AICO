# ItemSystem — 아이템 정체·능력의 단일 출처 (기반 라운드)

아이템 부여/골드 지급이 Inventory 계열(InventorySystemManager / 미션 InventoryManager)에 흩어져 있어
"이 key가 무엇이고(정체) 무엇을 하는가(능력)"의 원본이 없었다. ItemSystem은 그 원본이다 —
Store의 리소스 구조와 동일하게 **ItemCatalog(카테고리 레지스트리) → 카테고리별 하위 카탈로그**
(ItemGiftCatalog 등)로 계층화하고, 얇은 파사드(ItemSystemManager)로 "아이템의 출입은 한 문으로"를
지향한다. **재화(돈)는 별도 축**: 정의는 ItemCurrencyCatalog, 잔액·증감은 CurrencyManager가 소유한다.
이번 라운드는 **기반만**: 카탈로그 계층 + 파사드 + 재화 지갑 + 생성 도구. 기존 시스템
(Store/Inventory/Mission)의 콜사이트 스위칭은 하지 않았다(4장 후속 계획).

## 1. 정체성 — 역할 분담 (전부 문자열 key 규약)

| 시스템 | 위치 | 소유 |
|---|---|---|
| **ItemSystem (여기)** | `Prefabs/Assist/ItemSystem` | 아이템의 **정체**(key/displayName/icon/description/maxStack)와 **능력**(선물 인연도 상승량 등), **재화의 정의**(ItemCurrencyCatalog)의 단일 출처 |
| **CurrencyManager (여기)** | `Prefabs/Assist/ItemSystem` | **재화 잔액·증감·누적의 공식 소유자** (전 재화 균일 처리 — 특정 재화 특례 없음). 저장: `persistentDataPath/ItemSystem/currency.json` |
| InventorySystem | `Prefabs/Assist/InventorySystem` | **보관·스택** (MAIN/CHAR 스토어, 저장/이동) |
| Mission InventoryManager | `Prefabs/UI/Mission` | **레거시 골드 지갑** (기존 UI/미션 집계의 결합 지점 — CurrencyManager와 양방향 동기화로 공존, 이관 후 골드 소유권 회수 대상) + item1~3 수치 재화 |
| Store | `Prefabs/UI/Store` | **상거래** (가격/구매/판매 UI — 가격은 Store 소유, 능력은 ItemSystem 소유) |
| EquipSystem | `Prefabs/Assist/EquipSystem` | **장착 메카닉** (소켓/배치) |

다섯 시스템은 전부 **같은 문자열 key 공간**(16키: InventoryCatalog/EquipCatalog/StoreCatalog와 공유)으로
느슨하게 이어진다. 네이밍 주의: bare `Item` 클래스와 `ItemRarity`는 금지(Cleverous enum과 모호성) —
그래서 엔트리는 `ItemEntry`, 매니저는 `ItemSystemManager`(옆 폴더 InventorySystemManager 선례).

## 2. 구성 (전부 `Assets/Prefabs/Assist/ItemSystem/` 안)

| 파일 | 역할 |
|---|---|
| `Scripts/ItemCategoryCatalog.cs` | 공통 정체 엔트리 `ItemEntry{key, displayName, icon, description, maxStack=99}` + 추상 베이스 `ItemCategoryCatalog`(`GetEntry`/`Contains`/`BaseEntries` — IReadOnlyList\<T\> 공변으로 파생 리스트를 복사 없이 반환). |
| `Scripts/ItemBasicCatalog.cs` | 능력 없는 기본 카테고리 카탈로그 — 장착물/포즈/이펙트/잡화처럼 "정체"만 필요한 아이템용. lazy map(BuildMap/EnsureMap/OnValidate 무효화, 중복 키 경고 후 스킵 — StoreTagCatalog 관용구). |
| `Scripts/ItemGiftCatalog.cs` | 선물 카테고리 — `ItemGiftEntry : ItemEntry{affinityPoints}`(증정 시 인연도 상승량) + `GetGift(key)`. 능력 필드는 이렇게 카테고리별 파생 엔트리가 확장한다. |
| `Scripts/ItemCatalog.cs` | **카테고리 레지스트리**(`Resources/ItemCatalog.asset`) — `ItemCategoryEntry{category, catalog}` 리스트만 보유(자체 캐시 없음, 자식 lazy map에 위임). `Categories()`(빈/중복 스킵, 경고 1회 래치)/`CatalogForCategory`/`Get`(등록 순 자식 위임, 첫 히트)/`Contains`/`CategoryForKey`/`TryGetGiftPoints`(자식 중 ItemGiftCatalog 타입 조회). |
| `Scripts/ItemCurrencyCatalog.cs` | 재화 카테고리 — `ItemCurrencyEntry : ItemEntry{premium}`(정의만: 이름/아이콘/설명/프리미엄 여부. maxStack은 재화에서 미사용) + `GetCurrency(key)`. "카탈로그 등재 = 존재하는 재화" 불변식의 원천. |
| `Scripts/CurrencyManager.cs` | **재화 상시 관리 싱글톤** — 전 재화 잔액/누적(earned·spent)/저장의 공식 소유자. `GetBalance`/`Add`/`Spend`(카탈로그 미등재 키 거부)/`Gold` 편의 접근자/`CurrencyChanged(key)` 이벤트. **레거시 골드 브리지 내장**: 레거시 지갑이 살아 있는 동안 골드 증감은 그쪽을 트랜잭션 실행자로 쓰고 결과를 즉시 채택(양방향 동기화 — 어느 경로로 바뀌어도 수렴, 기존 골드 UI·미션 집계 무손상). 저장은 **체크섬(salted SHA-256) + 원자적 쓰기(temp→교체)** — 캐주얼 변조/파일 손상 감지 시 경고 후 무시(빈 지갑 시작, 골드는 브리지가 복원). Gem 등 추가 방법은 파일 상단 주석 예시 참조. |
| `Scripts/ItemSystemManager.cs` | **상시 파사드 싱글톤**(StoreManager 패턴: 플레이 중 `Instance` 자동 생성 + DontDestroyOnLoad, 에디트 모드 null). 조회 `GetItem`/`Contains`/`CategoryForKey`/`GetGiftAffinityPoints` + 부여 `GrantItem`/`GrantItemToChar`(→InventorySystemManager). **돈 관리는 하지 않는다 — 재화는 CurrencyManager.** 위임 대상 부재 시 false/0 + 경고 1회 래치. |
| `Editor/ItemSystemTools.cs` | `Tools/ItemSystem/1. Create Catalog` + batchmode 진입점 `BatchBuildAll`. **카탈로그 갱신은 전부 additive**(기존 엔트리/행 불변 + 누락 기본 키만 추가, 기본 키의 빈 icon·빈 affinityPoints·기본 카테고리 행의 빈 catalog만 보충 — 인스펙터 편집이 재실행에도 보존). 재화 추가 예시(Gem)는 CreateCatalog 내 주석 참조. |
| `Resources/ItemCatalog.asset` | 카테고리 레지스트리 — 6행(장착물/포즈/이펙트/선물/잡화 — Store 태그와 동일 이름 — + 재화. 생성물, 런타임 자동 로드). |
| `Resources/Item{Equip,Pose,Effect,Misc}Catalog.asset` | 기본 카테고리 4종(ItemBasicCatalog 타입, 생성물): 장착물 4 / 포즈 3 / 이펙트 3 / 잡화 3. |
| `Resources/ItemGiftCatalog.asset` | 선물 카테고리(ItemGiftCatalog 타입, 생성물): 선물(소/중/대) — affinityPoints 10/30/100. |
| `Resources/ItemCurrencyCatalog.asset` | 재화 카테고리(ItemCurrencyCatalog 타입, 생성물): `currency_gold` 골드 1종(premium=false, icon은 인스펙터 지정 몫). |

등록 데이터 16키:
- **장착물(4)**: `arona_a_chipao` 치파오 / `arona_a_idolfrontribbon` 아이돌 프론트리본 / `arona_a_pareo` 파레오 /
  `hairpin_placeholder` 헤어핀 — 아이콘은 `Assets/Model/Sprite` 원본 PNG를 guid로 로드(실패 시 Warning + null).
- **포즈(3)**: `pose_greeting` / `pose_dance` / `pose_sit` (icon null).
- **이펙트(3)**: `fx_pat_heart` / `fx_pat_star` / `fx_click_sparkle` (icon null).
- **선물(3)**: `gift_s`(+10) / `gift_m`(+30) / `gift_l`(+100) — 인연도 상승량은 ItemGiftEntry.affinityPoints 소유.
- **잡화(3)**: `snack_banana` / `potion_energy` / `ticket_random` (icon null).
- **재화(1)**: `currency_gold` 골드 — 정의는 카탈로그, 잔액은 CurrencyManager. 재화 키는 `currency_` 접두 규약.

## 3. 사용법

### A. 카탈로그 베이크
1. 메뉴 `Tools/ItemSystem/1. Create Catalog` — `Resources/`에 에셋 7종(레지스트리 1 + 카테고리 6) 생성/갱신.
   additive라서 재실행해도 인스펙터 편집(엔트리 추가/수정/재배열)이 보존된다.
2. batchmode 일괄 빌드:
   `Unity.exe -batchmode -quit -projectPath <proj> -executeMethod ItemSystemTools.BatchBuildAll`

### B. 코드에서 호출
```csharp
// 아이템 (ItemSystemManager 파사드)
ItemEntry it = ItemSystemManager.Instance.GetItem("gift_s");            // 정체 조회 (미등재 null)
int pts = ItemSystemManager.Instance.GetGiftAffinityPoints("gift_l");   // 능력 조회: 100 (비선물/미등재 0)
string cat = ItemSystemManager.Instance.CategoryForKey("snack_banana"); // "잡화" (미등재 null)
bool ok = ItemSystemManager.Instance.GrantItem("snack_banana", 3);      // MAIN 부여 (보관은 InventorySystemManager 위임)

// 재화 (CurrencyManager — 돈 관리의 공식 창구)
CurrencyManager.Instance.Add(CurrencyManager.GoldKey, 500);             // 골드 적립 (카탈로그 미등재 키는 거부)
bool paid = CurrencyManager.Instance.Spend(CurrencyManager.GoldKey, 120); // 골드 차감 (잔액 부족 시 false)
int gold = CurrencyManager.Instance.Gold;                               // 잔액 편의 접근자 (= GetBalance(GoldKey))
CurrencyManager.Instance.CurrencyChanged += key => { /* UI 갱신 */ };   // 잔액 변경 브로드캐스트
```
레거시 공존: 골드는 기존 지갑(미션 InventoryManager) 경유로도 계속 바뀔 수 있고(상점/미션 보상),
CurrencyManager가 이벤트로 즉시 채택하므로 어느 쪽에서 읽어도 같은 값이다.

### B2. 재화 추가 (예: Gem)
지갑/증감/저장은 키 기반이라 CurrencyManager 코드 수정이 거의 없다:
① `ItemSystemTools.CreateCatalog`의 재화 배열에 한 줄 추가(그 파일의 Gem 주석 예시) 또는 인스펙터에서
`ItemCurrencyCatalog.asset`에 엔트리 직접 추가 → ② `CurrencyManager`의 `GemKey` 주석 상수 활성화 → 끝.
신규 재화는 레거시 브리지와 무관하게 처음부터 CurrencyManager 지갑에 네이티브 저장된다.

### C. 아이템 추가
1. 해당 카테고리의 하위 카탈로그 에셋(예: 잡화 = `Resources/ItemMiscCatalog.asset`)에 엔트리 1개 추가 —
   레지스트리는 수정 불필요. 인스펙터 직접 편집도 안전(도구 additive).
2. 능력이 필요한 새 카테고리 = `ItemEntry` 파생 엔트리 + `ItemCategoryCatalog` 파생 카탈로그 신설 +
   레지스트리(`ItemCatalog.asset`)에 행 1개.
3. 실제 지급(GrantItem)까지 되려면 같은 key가 InventoryCatalog에도 있어야 한다(AddToMain이 검증) —
   현행 규약 유지. 이중 등록의 해소는 후속 3번 옵션.

## 4. 후속 계획 (정찰 근거 요약)

이번 라운드는 기존 파일 무수정 원칙 — 아래는 정찰로 확인한 이관 후보다.

1. **콜사이트 이관 — "출입 한 문" 완성.** 정찰 결과 골드 변경 5곳 / 아이템 지급 3곳이 파사드 밖에 있다.
   - 골드 변경: `Store/Scripts/StoreView.cs` 3곳(구매 결제 SpendGold / 지급 실패 환불 AddGold / 판매 대금 AddGold),
     `Store/Scripts/StoreDemoController.cs` 1곳(G키 +500 AddGold),
     `Mission/MissionView/Scripts/InventoryManager.cs` 1곳(AddReward의 미션 보상 가산 — 지갑 소유자 내부라 이관 제외 검토).
   - 아이템 지급: `StoreView.cs`(구매 AddToMain) / `StoreDemoController.cs`(데모 지급) /
     `InventorySystem/Scripts/InventoryDemoController.cs`(데모 지급) — 전부 `AddToMain` 직호출.
   - → 아이템은 `GrantItem`, 골드는 `CurrencyManager.Add/Spend` 경유로 순차 교체.
     Store 쪽은 "Store 내부만 수정" 원칙에 따라 별도 라운드.
   - **브리지 제거 조건**: 골드 콜사이트가 전부 CurrencyManager로 이관되고 미션 집계(CH0001/CH0007)가
     `CurrencyChanged` + `GetEarnedTotal/GetSpentTotal`을 구독하게 되면, CurrencyManager의 레거시 골드
     브리지(레거시 지갑 통과 + 채택)를 제거하고 골드도 네이티브 경로로 일원화한다.
2. **StoreView 판매 차감의 캡슐화 우회 해소.** 현행 `StoreView.ExecuteSale`은 MAIN 스토어의 스택을
   직접 차감/제거하고 `SaveStore` + `OnStoreChanged` 발화까지 자기가 한다 — InventorySystemManager에
   차감 공개 API가 없어 생긴 우회. → 파사드에 차감 API(예: `ConsumeItem(key, amount)`, 내부는
   InventorySystemManager에 신설할 제거 API에 위임) 신설 후 ExecuteSale을 교체한다.
3. **InventoryCatalog를 ItemCatalog 파생물로 격하 (옵션).** InventoryCatalog 엔트리
   (key/displayName/icon/description/maxStack/category)는 ItemEntry와 필드가 겹친다 — ItemCatalog을
   원본으로 두고 도구가 InventoryCatalog.asset을 "생성물"로 동기 생성하면 **InventorySystem 독자 코드
   무수정**으로 정체의 단일 출처화가 완성된다. Store가 InventoryCatalog에 additive 등록하는 상점 전용
   12키도 이 경로로 흡수 가능. maxStack도 이때 ItemEntry 값을 원천으로 이관.
4. **지갑 item1~3(미션 보상)과 keyed 아이템의 통합 여부.** 미션 InventoryManager의 item1~3은 키 없는
   수치 재화(미션 보상 전용)고, 도전과제 CH0008(아이템 모으기)이 item1~3 합계를 집계 기준으로 삼는다
   (`MissionList.SetCurrent("CH0008", inv.ItemTotal)`). keyed 아이템으로 통합하려면 CH0008 재정의(집계
   기준 변경)가 선행돼야 한다 — 통합/현행 유지 결정 대기.
5. **선물 증정 UX + AddAffinityPoints 연동.** 능력 데이터(`affinityPoints`)와 조회 파사드
   (`GetGiftAffinityPoints`)는 준비됨 — 증정 플로우(대상 캐릭터 선택 → 소모 → 포인트 지급)와
   AddAffinityPoints 연동은 인연도 시스템 구현 시. 설계 오너:
   `../../UI/CharacterDetail/Affinity_Store_Integration.md`.
6. **설계 오너 문서 동기화.** `Affinity_Store_Integration.md` 2장은 아직 "`StoreEntry.giftPoints`
   필드 준비됨"이라 서술하지만 그 필드는 삭제됐고(Store의 `detailText`는 표시 전용) 수치의 실 소유는
   `ItemGiftEntry.affinityPoints`다 — 오너 문서의 해당 서술을 이 구조로 갱신해야 독자가 존재하지 않는
   필드를 찾지 않는다 (사용자 소유 문서라 이 라운드에서 임의 수정하지 않음).
7. **레지스트리 교차 중복 키 검증.** 카테고리 카탈로그 간 같은 키가 중복 등록되면 Get(첫 히트)과
   TryGetGiftPoints(선물 카탈로그만 조회)가 서로 다른 엔트리를 볼 수 있다(StoreCatalog와 동일 특성,
   중복 금지 규약으로 방어 중) — 에디터 도구에 교차 중복 리포트를 추가할 가치.
