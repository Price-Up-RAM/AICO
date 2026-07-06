# 📦 인벤토리 시스템 (Cleverous VaultInventory 연동)

## 1. 시스템 개요
Cleverous VaultInventory 에셋(`Assets/Cleverous/`)을 기반으로, 캐릭터가 획득한 악세서리를 인벤토리에 보관하고 클릭으로 장착/해제할 수 있게 만든 시스템. 기존 [Accessory.md](Accessory.md)의 `AccessoryManager`/`AccessoryData`(부위 오프셋 관리)와 그대로 연동되며, 인벤토리는 "어떤 악세서리를 갖고 있는가"만 관리하고 실제 장착 로직(위치/회전 보정, 슬롯 탐색)은 여전히 `AccessoryManager.Equip`이 담당한다.

**지금 단계는 임시 구현이다.** 슬롯을 좌클릭하면 즉시 장착/해제되는 방식이며, 최종 목표는 드래그 앤 드롭으로 (MR 포팅 시) 레이캐스트로 캐릭터에 얹으면 장착되는 것이다.

---

## 2. 스크립트별 역할

### `Assets/Scripts/CharInventoryOwner.cs`
캐릭터를 Vault의 `IUseInventory` 소유자로 만드는 컴포넌트. **인벤토리 컴포넌트(Vault의 `Inventory`)와 같은 GameObject(캐릭터 프리팹 루트)에 붙여야 한다** (`[RequireComponent(typeof(Inventory))]`).
- `Start()`에서 저장 파일(`inventory_{charcode}.json`)이 있으면 복원, 없으면 인스펙터의 `Starting Items`로 초기화.
- 초기화가 끝나면 `VaultInventory.OnPlayerSpawn`을 발동시켜, 이 이벤트를 구독 중인 `InventoryUi`가 이 캐릭터의 인벤토리로 자동 바인딩되게 한다.
- `OnDestroy()`에서 자동 저장(`SaveInventory()`).

### `Assets/Scripts/AccessoryItem.cs`
Vault의 `UseableItem`을 상속하는 실제 "악세서리 인벤토리 아이템" SO. 인벤토리 슬롯에 들어가는 실체가 이것.
- `accessoryName`: `AccessoryData`의 `AccessorySlotItem.accessoryName`과 반드시 매칭되어야 함 (이 이름으로 `AccessoryManager`가 부위/오프셋을 찾음).
- `slotName`: 비워두면 `AccessoryManager.Equip`이 아이템 검색만으로 슬롯을 자동으로 찾음. 명시하면 그 슬롯을 우선 사용.
- `UseBegin(IUseInventory user)`: 이 아이템이 "사용"될 때 실행되는 로직. 이미 장착 중이면 해제, 아니면 장착 (클릭 = 토글).

### `Assets/Scripts/ClickToUseItemUiPlug.cs`
Vault 기본 `ItemUiPlug`을 상속한 커스텀 클래스. **Vault 원본 코드는 건드리지 않고, 좌클릭 시 바로 `UseBegin()`이 호출되도록 우회 연결하는 임시 구현.**
- Vault 기본값은 좌클릭 = 아이템 선택(`InventoryUi.ClickedItem` 세팅)만 하고, 우클릭 → 컨텍스트 메뉴 → "USE" 버튼까지 눌러야 `UseBegin`이 호출됨.
- 이 클래스는 좌클릭에 `UseBegin()` 호출을 바로 붙여서, 슬롯 클릭 한 번으로 장착/해제가 되게 만듦.
- 나중에 드래그 앤 드롭으로 바꿀 때도 이 파일(및 `ItemSlotTemplate` 프리팹)만 교체하면 되므로 Vault 원본 코드와 분리되어 있음.

### `Assets/Scripts/AccessoryManager.cs`, `AccessoryData.cs`
기존 문서([Accessory.md](Accessory.md)) 참고. 변경 없음 — 인벤토리 시스템은 이 위에 얹힌 것뿐.

### `Assets/Scripts/CharManager.cs` — `setInventoryVar(GameObject charObj)`
캐릭터 스폰 시 호출되어 `Inventory` 컴포넌트와 `CharInventoryOwner`를 자동으로 붙이고 `Inventory.Configuration`(= `PlayerAccessoryInventoryConfig`, 캐릭터 공용 인벤토리 설정 SO)을 주입한다. 캐릭터 프리팹에 이미 두 컴포넌트가 있으면 재사용.

### `Assets/Scripts/TestManager.cs` (테스트/임시용)
- **I 키**: 인벤토리 창 토글. `SetActive`가 아니라 `CanvasGroup.alpha`/`interactable`/`blocksRaycasts`로 표시만 껐다 켠다 (아래 "주의할 점" 참고).
- **6~9번 키**: `pickupTestItems[0~3]`을 현재 캐릭터 인벤토리에 획득 테스트로 추가.
- **2~5번 키**: 기존 방식(코드로 직접 `AccessoryManager.Equip/UnEquip` 호출)의 장착 테스트. 인벤토리 클릭 장착과는 별개의 경로이며 회귀 확인용으로 남겨둠.

### `Assets/Scripts/AnimationPlayerManager.cs` — `BuildBlacklistFromStates`
애니메이션 클립 블랙리스트 수집을 위해 캐릭터를 화면 밖(9999,9999,9999)에 복제(clone)하는 코루틴. 이 clone도 원본의 모든 컴포넌트를 복사하므로, **clone 생성 직후 같은 프레임에 `CharInventoryOwner.enabled = false`로 꺼줘야 한다.** 그렇지 않으면 clone의 `Start()`가 실행되어 `OnPlayerSpawn`을 재발동시키고, `InventoryUi`가 진짜 캐릭터가 아닌 clone으로 재바인딩되어버린다 (clone은 곧 파괴되므로 `InventoryUi.TargetInventory`가 Missing이 됨).

---

## 3. 인스펙터 세팅

### 캐릭터 프리팹
- `Inventory` (Vault 컴포넌트): `Configuration` 필드는 런타임에 `CharManager.setInventoryVar`가 자동으로 `PlayerAccessoryInventoryConfig`를 주입하므로 **프리팹에서 수동으로 채워둘 필요는 없다** (다만 채워져 있어도 런타임 값으로 덮어써지므로 무해).
- `CharInventoryOwner`:
  - `Starting Items`: 저장 파일이 없을 때(첫 실행) 지급할 `AccessoryItem` 목록.
  - `Starting Amounts`: `Starting Items`와 같은 인덱스로 매칭되는 개수. **비워둬도(size 0) 안전** — 코드상 `startingAmounts`가 비어있으면 각 아이템 개수는 기본값 1로 처리됨.

### `AccessoryItem` SO 에셋 (예: `Accessory_AronaPareo` 등)
- `accessoryName`: `AccessoryData`에 등록된 이름과 정확히 일치해야 함.
- `Ui Icon`: 인벤토리 슬롯에 표시될 스프라이트. **반드시 확인** — 엉뚱한 스프라이트가 잘못 연결되어 있어도 에러 없이 조용히 다른 아이콘이 뜨므로 눈으로 직접 확인 필요.
- `Db Key`: **건드리지 말 것.** Vault가 자동으로 관리하는 값이며, 수동으로 세팅하면 오히려 충돌 유발. 새 에셋을 만들면 기본값(`int.MinValue`, 미할당)으로 시작하는데, 이는 아래 "새 아이템 추가하기"의 등록 절차로 해결한다.

### `AccessoryManager`
- `All Items`: 인벤토리에 존재할 수 있는 모든 `AccessoryItem`을 여기 등록해둬야 `accessoryName`으로 실제 장착 프리팹(`ArtPrefab`)을 찾을 수 있다. 새 아이템 추가 시 여기 등록 누락하면 인벤토리엔 보이는데 장착은 안 되는 상태가 됨.

### `TestManager`
- `Pickup Test Items`: 6~9번 키 테스트용 4개 슬롯.
- `Inventory Panel`: **`CanvasGroup` 컴포넌트를 참조**해야 함 (GameObject 자체가 아님). `(UI) Inventory Panel`에 `CanvasGroup`을 달고 그걸 연결.

---

## 4. 씬(Canvas) 세팅 — 반드시 필요한 오브젝트

Vault는 정적 템플릿(슬롯 프리팹 등)을 씬의 특정 컴포넌트가 초기화해줘야 동작한다. 다음 3개가 씬에 없으면 인벤토리 자체가 비어 보이거나 에러가 난다.

1. **`(MG) VAULT Runtime.prefab`** (`Assets/Cleverous/VaultInventory/Inventory Example/Prefabs/`)
   - `VaultRuntime` 컴포넌트가 `Awake()`에서 `VaultInventory.InitReferences(...)`를 호출해 `ItemSlotTemplate` 등 정적 참조를 세팅함. **이게 없으면 슬롯 자체가 생성되지 않는다** (`ArgumentException: The Object you want to instantiate is null`).
   - 프리팹에 `ItemSlotTemplate`, `ItemFloaterTemplate`, `RuntimeItemTemplate`, `GenericInventoryUi`는 이미 연결되어 있음. **`GameCanvas` 필드만 비어있으니 씬의 최상위 Canvas(인벤토리 패널이 속한 Canvas)를 연결해야 한다.**

2. **`(UI) Tooltip Panel.prefab`** (같은 폴더)
   - `UiTooltip` 컴포넌트가 `Awake()`에서 자기 자신을 `UiTooltip.Instance`(static)로 등록함.
   - **이게 씬에 없으면 슬롯에 마우스를 올리거나(`OnPointerEnter`) 뗄 때(`OnPointerExit`) `NullReferenceException`이 난다.** Canvas 아래 아무 데나 배치하면 됨 (위치는 자동 조정됨).

3. **인벤토리 UI 오브젝트 자체**
   - `InventoryUi` 컴포넌트가 붙은 오브젝트(예: `Inventory Items`)는 `(UI) Inventory Panel`의 자식으로 존재.
   - `InventoryUi.Restrictions[]`와 `Inventory.Configuration.SlotRestrictions[]`가 인덱스별로 매치되는 슬롯만 실제로 생성됨 (둘 다 `None`이면 매치되어 정상 생성).

### 캐릭터 프리팹의 `ItemSlotTemplate` 커스터마이징
좌클릭 즉시 장착 기능을 쓰려면 `(VD) Template Slot.prefab`의 `ItemUiPlug` 컴포넌트를 `ClickToUseItemUiPlug`로 교체해야 한다:
1. 프리팹 편집 모드 진입
2. `ItemUiPlug`이 붙은 오브젝트에 `Click To Use Item Ui Plug` 컴포넌트 추가
3. 기존 필드(`SlotOwnerObject`, `MyTypeImage`, `MyItemImage`, `MyHighlight`, `StackSizeBox`, `StackSizeText`) 재연결
4. 기존 `Item Ui Plug` 컴포넌트 제거

---

## 5. 새 악세서리 아이템 추가하기

1. `AccessoryData`에 부위/오프셋 등록 (기존 [Accessory.md](Accessory.md) 절차 그대로).
2. `Create > AICO > Accessory Item`으로 새 `AccessoryItem` SO 생성.
   - `accessoryName`을 1번의 `accessoryName`과 동일하게 입력.
   - `Ui Icon`에 인벤토리에 표시될 스프라이트 연결.
   - `ArtPrefab`(`UseableItem`/`RootItem` 쪽 필드)에 실제 장착될 프리팹 연결.
3. `AccessoryManager.allItems`에 새로 만든 에셋 등록.
4. **Db Key 등록 확인**: 새로 만든 SO는 `Db Key`가 `int.MinValue`(미할당) 상태로 시작한다. 이 상태로는 인벤토리에 슬롯은 보이지만 아이콘이 안 뜨고 실제로는 텅 빈 것처럼 동작한다 (아래 "주의할 점" 참고). Unity 메뉴 `Tools > Cleverous > Vault > Data Key Upgrader (safe)` 실행 → 확인 창에서 Proceed. 이 프로젝트의 모든 미할당 `DataEntity`에 새 Key를 일괄 부여해준다. 스크립트 리컴파일 시 자동으로 감지되어 안내 다이얼로그가 뜨기도 함.
5. `CharInventoryOwner.Starting Items`(필요 시)나 `TestManager.pickupTestItems`(테스트용)에 등록.

---

## 6. 저장/로드 구조

캐릭터별로 **인벤토리 상태 + 장착 상태를 하나의 JSON 파일**로 묶어서 저장한다.

- **저장 경로**: `Application.persistentDataPath/inventory_{charcode}.json` (캐릭터마다 별도 파일)
- **저장 시점**: `CharInventoryOwner.OnDestroy()` — 캐릭터가 파괴될 때(씬 전환, 다른 캐릭터로 교체 등) 자동 저장.
- **로드 시점**: `CharInventoryOwner.Start()` — 저장 파일이 있으면 복원, 없으면(첫 실행) `Starting Items`로 초기화.
- **데이터 구조** (`CharSaveData`):
  ```csharp
  public class CharSaveData
  {
      public InventoryState inventoryState;   // Vault의 Inventory.ToState() 결과 (슬롯별 아이템/개수)
      public EquippedStateList equippedState; // AccessoryManager가 관리하는 현재 장착 상태 (슬롯명 -> accessoryName)
  }
  ```
- **복원 순서**: `Inventory.Initialize(this, saveData.inventoryState)` (인벤토리 내용물 복원) → `AccessoryManager.Instance.RestoreEquippedState(gameObject, saveData.equippedState)` (장착 중이던 악세서리 재장착).
- 참고: `accessory_data.json`(부위 오프셋 데이터, `AccessoryManager.SaveAccessoryData`)은 이것과 **별개의 파일**이며 캐릭터별이 아니라 전역 공용이다. 헷갈리지 않도록 주의.

---

## 7. 주의할 점 (트러블슈팅 이력)

- **인벤토리 패널을 `SetActive(false)`로 끄지 말 것.** 자식 오브젝트의 `Awake()`가 실행되지 않아 `InventoryUi`가 `OnPlayerSpawn` 구독을 놓친다. 반드시 `CanvasGroup`으로 표시만 제어.
- **애니메이션 블랙리스트 clone 주의.** `AnimationPlayerManager`가 만드는 probe clone은 `CharInventoryOwner`를 비활성화해뒀는지 확인. 안 그러면 `InventoryUi.TargetInventory`가 엉뚱한(곧 파괴될) clone으로 바뀌어버린다.
- **씬에 `VaultRuntime`, `UiTooltip` 둘 다 있는지 항상 확인.** 새 씬을 만들거나 인벤토리 UI를 새로 배치할 때 가장 흔하게 빠뜨리는 부분. 없으면 각각 "슬롯이 아예 안 생김" / "슬롯에 마우스 올리면 에러" 증상으로 나타난다.
- **`Db Key`가 `int.MinValue`인 아이템은 슬롯에 아이콘이 안 뜬다.** 증상: 슬롯은 생기는데 안이 계속 비어(검게) 보임. Vault 내부적으로 `RpcRemoteUpdateSlot`이 `Vault.Get(dbKey)`로 아이템을 재조회하는데, 미등록 키는 조회 실패 → 아이템 참조가 null로 덮어써짐. `Tools > Cleverous > Vault > Data Key Upgrader (safe)`로 해결.
- **좌클릭만으로는 아이템이 장착되지 않는 게 Vault 기본 동작이다.** 버그가 아니라 우클릭(컨텍스트 메뉴 → USE)까지 가야 하는 구조. 이 프로젝트에서는 `ClickToUseItemUiPlug`로 좌클릭 = 즉시 사용이 되도록 우회했음 (섹션 4의 프리팹 세팅 필요).
- **네트워크(Mirror/Fishnet) 미사용 환경**이라 `[ClientRpc]`/`[Server]` 등은 `Assets/Cleverous/NetworkImposter/NetworkAttributes.cs`의 순수 표식 어트리뷰트로 대체되어, 실제로는 그냥 일반 메서드처럼 항상 실행된다. 네트워크 동기화를 기대하고 코드를 읽으면 헷갈리니 참고.
