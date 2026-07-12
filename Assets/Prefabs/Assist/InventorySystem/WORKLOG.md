# InventorySystem — 완전 독립(Standalone) 2계층 인벤토리 시스템

기존 Vault 인벤토리(`Assets/Cleverous/` + `CharInventoryOwner`/`AccessoryItem` 등)와 **코드 의존이
전혀 없는** 독립 시스템. 아이템을 `{key, count}`로만 보관하고, 장착은 전부
**EquipSystem**(`EquipManager.Instance.Equip/Unequip`)에 위임한다.

- 의존 방향은 **단방향**: InventorySystem → EquipSystem (`EquipManager`, `EquipCatalog.Contains/Get`만).
  EquipSystem은 InventorySystem을 전혀 모른다.
- 구 Vault/Accessory 시스템과는 **병존** 중이며 서로 간섭하지 않는다 (후속 커밋에서 Vault 측 제거 예정).

---

## 1. 구성 (전부 `Assets/Prefabs/Assist/InventorySystem/` 안)

| 파일 | 역할 |
|---|---|
| `Scripts/InventoryModel.cs` | 순수 데이터: `InvItemStack{key,count,slot}`, `InvStore{ownerId,stacks,equippedKeys}` + Add/Remove/CountOf/FindBySlot/FirstFreeSlot/NormalizeSlots. **slot = 그리드 칸 위치**(드래그 배치·페이지 기준, 저장됨). **equippedKeys = 착용 중 key 목록**(§2 착용 영속화). JsonUtility 직렬화. |
| `Scripts/InventoryEvents.cs` | 정적 이벤트 허브: `OnActiveOwnerChanged`(캐릭터 전환), `OnStoreChanged`(스토어 변경/장착 토글). |
| `Scripts/InventoryCatalog.cs` | 아이템 **표시 메타** SO (`displayName`/`icon`/`maxStack`/`category`). EquipCatalog과 동일 구조, key 문자열 공간 공유. |
| `Scripts/InventorySystemManager.cs` | 싱글톤. MAIN + 캐릭터별 스토어, JSON 저장/로드, 이동(MoveMainToChar/MoveCharToMain), 장착(EquipKey 멱등 / ToggleEquip) + 표시용 장착 미러, 정렬(SortStore: 종류→이름). (이름 유래: 명명 당시 미션 재화 시스템이 `InventoryManager`를 전역으로 쓰고 있어 충돌 회피로 이 이름을 택했다 — 그 클래스는 이후 골드 단일화로 삭제됐지만 이름은 유지.) |
| `Scripts/InventoryView.cs` | UI 창 컨트롤러(**베이크 전용**: Awake가 `BindExisting`으로 참조만 연결 — 베이크 계층이 없으면 에러 로그 + 무동작). **창 1개 = 스토어 1개**(`InventorySection.Main/Char`, `ConfigureSection`으로 지정). 헤더 바(타이틀+정렬/닫기) + **8x6=48칸 고정 그리드(빈 칸 포함)** + 푸터 바(`<` 페이지 `>`). 드롭 해석(`HandleSlotDrop`): 칸=위치 이동/스왑/병합, 반대 창=이동, 캐릭터(3D, 렌더러 바운드 스크린 투영)=이동+장착. 우클릭=컨텍스트 메뉴. CanvasGroup 토글 + `IsVisible`(외부 토글 판정용 공개 프로퍼티). |
| `Scripts/InventorySlotView.cs` | 그리드 셀 1칸(64px): **빈 칸/아이템 칸** 표시(아이콘·이름·수량·장착 하이라이트). 좌클릭(퀵액션)/우클릭(메뉴)/**아이템 드래그**(고스트 추종, 빈 칸은 드래그 불가·드롭 타깃만). |
| `Scripts/InventoryMenu.cs` | 우클릭 컨텍스트 메뉴(상세/장착·해제/**CHAR·MAIN으로 이동**) + 상세 팝업 (Devion UIWidgets ContextMenu 참고, 서브메뉴 없음). 코드 런타임 생성, 백드롭 클릭 닫기, 동시 1개. 폰트는 뷰의 SUIT-Bold를 물려받음. |
| `Scripts/InventoryTooltip.cs` | **hover 미니 툴팁**(이름 + 수량·분류 + 짧은 설명). 레이캐스트 완전 통과(깜빡임 방지), 드래그 중/메뉴 열림/Rebuild 시 자동 숨김. |
| `Scripts/InventoryWindowDragHandler.cs` | 창 전역 드래그(패널 루트 부착, 슬롯 위 드래그도 버블링). 프로젝트 확립 패턴(JarvisCalendarToggleDragHandler) 미러. |
| `Scripts/InventoryDemoController.cs` | 데모 트리거: 숫자키로 MAIN에 아이템 지급, I키로 패널 토글. |
| `Resources/InventoryCatalog_Demo.asset` | 카탈로그(런타임 자동 로드 폴백). 아이템 추가/수정은 인스펙터에서 직접. |
| `InventoryPanel.prefab` | 베이크된 다크테마 UI 프리팹 (SUIT-Bold 적용). **UI 크롬의 유일한 원본** — 외형 변경은 이 프리팹을 직접 편집한다. |
| `InventoryDemo.unity` | 데모 씬 (이미 빌드 완료 — 재생성 수단 없음). |

> **프리팹 완결 선언**: 런타임 자가구축 코드(`InventoryView.BuildHierarchy/BuildInternal`,
> `InventorySlotView.BuildTemplate`)를 제거했다. UI는 베이크된 `InventoryPanel.prefab`이 완결 상태이며,
> 컨트롤러는 `BindExisting` 전용이다 (베이크 계층이 없으면 에러 로그 + 무동작).
> **Editor 도구(`Editor/InventorySystemTools.cs`)도 함께 삭제** — 재베이크 수단은 소멸했고, 이후 크롬 변경은
> 프리팹 직접 편집으로만 한다. 부수 효과로, 재실행 시 `InventoryCatalog_Demo.asset`을 하드코딩 목록으로
> 재작성해 이후 추가된 엔트리(Store 등이 주입한 상점 키 포함)를 날릴 수 있던 **파괴적 `CreateCatalog` 지뢰도
> 함께 소멸**했다.

---

## 2. 작동 원리

### 2계층 = 이동(move) 모델
- **MAIN**(유저 공용 풀) 1개 + **캐릭터별**(charcode당) N개. 아이템 인스턴스는 항상 **한 스토어에만** 존재.
- MAIN → 캐릭터 이동 시 MAIN에서 차감(원자적: 차감 성공 시에만 추가). 반대 방향도 동일.

### 소유(인벤토리) vs 착용(EquipSystem)의 분리
- 인벤토리는 "무엇을 **소유**하는가"만 관리. **장착해도 스토어에서 빠지지 않는다.**
- "무엇을 **착용** 중인가"의 씬 실체는 EquipSystem(소켓 하위 `EquipMarker`)이 갖고,
  **영속 진실은 스토어의 `equippedKeys`(착용 key 목록, char_{charcode}.json에 저장)** (2026-07-12 도입).
  `InventorySystemManager`의 미러(charcode→slotId→key)는 여전히 UI 하이라이트용 런타임 캐시(저장 안 함).
- 같은 슬롯에 다른 아이템 장착 시 EquipManager가 기존 장착물을 교체하며, 미러·착용 기록도 함께 갱신.

### 착용 영속화 (2026-07-12)
- **저장 데이터는 key 목록뿐** (`InvStore.equippedKeys`) — 슬롯(slotId)은 저장하지 않는다.
  복원 시 `EquipManager.Equip`의 해석 사다리가 현재 소켓 구성 기준으로 재해석하므로
  소켓 리네임/재저작에 강건. 구버전 세이브는 필드가 없어도 로드 무해(빈 목록).
- **기록 시점 3곳** (즉시 저장, `PersistEquipped`): ① `EquipKey` 성공(같은 슬롯 교체 시 이전 key 기록 제거 포함)
  ② `ToggleEquip` 해제 ③ `UnequipIfMirrored`(이동으로 강제 해제 — 비활성 캐릭터 포함).
- **복원**: `SetActiveOwner` → `RestoreEquips` — 플레이 중에만(origin 주입과 동일 게이트),
  보유 검증(미보유 key는 기록 정리) 후 `Equip` 직접 호출(EquipKey 미경유 — 토글/재기록과 분리),
  성공 시 해석된 슬롯으로 미러 재구성, 실패(소켓 없음)는 경고 후 **기록 유지**(소켓 저작 후 자연 복원).
- 앱 배선은 `CharManager.setInventoryVar`가 스폰/전환 3지점에서 `SetActiveOwner` 호출 — 이미 연결됨.

### key 규약 — 카탈로그 2개의 관계
- `InventoryCatalog`(표시 메타)와 `EquipCatalog`(장착 물리정보)는 **같은 key 문자열 공간**을 쓴다.
- "장착 가능한가"는 런타임에 `EquipCatalog.Contains(key)`로 판정 → 소유만 가능하고 착용 불가한
  아이템(소모품 등)도 모순 없이 표현된다.

### 저장
- 경로: `Application.persistentDataPath/InventorySystem/` 아래 `main.json` + `char_{charcode}.json`.
- 시점: 아이템 조작 즉시 저장 + `OnApplicationQuit`에서 전체 저장 (구 시스템의 OnDestroy-only 유실 문제 개선).
- 구 Vault 세이브(`persistentDataPath/inventory_{charcode}.json`, `accessory_data.json`)는
  **마이그레이션하지 않고 방치**한다 (새 출발, 사용자 결정). 이 시스템은 해당 파일을 읽지도 쓰지도 않으므로
  고아로 남아도 **무해**하다.

### 함정 원천 회피 (구 Vault 트러블슈팅 이력 반영)
- **DbKey류 조용한 실패 없음** — 식별은 string key + `Contains(key)` 검증뿐.
- **패널을 `SetActive(false)`로 끄지 않는다** — `CanvasGroup` alpha/interactable/blocksRaycasts만 조작.
- **씬 필수 오브젝트 최소화** — VaultRuntime/UiTooltip류 정적 템플릿 초기화 요구 없음.
  씬에는 `InventorySystemManager` + `EquipManager` + 패널 프리팹 + EventSystem만 있으면 된다.
- 캐릭터 clone(probe) 이슈: 이 시스템은 캐릭터 컴포넌트를 쓰지 않으므로(스토어는 charcode 문자열 기준)
  clone이 생겨도 재바인딩 사고가 없다. 활성 캐릭터 전환은 `SetActiveOwner` 명시 호출로만 일어난다.

---

## 3. 사용법

### A. 데모 씬으로 확인
1. `Assets/Prefabs/Assist/InventorySystem/InventoryDemo.unity` 열기 → **Play**.
   (캔버스는 SampleScene과 동일 스케일러: ScaleWithScreenSize 2560x1440, match 0.5)
2. `1~4`: MAIN에 아이템 지급 (chipao / idolfrontribbon / pareo / hairpin — EquipCatalog와 동일 키).
3. **두 창 분리**: 왼쪽 = MAIN 창(8x6=48칸), 오른쪽 = CHAR 창. `I`: 두 창 동시 토글. 창의 빈 영역(헤더/여백)을 잡고 드래그하면 창 이동.
4. **아이템 드래그 앤 드롭**:
   - 같은 창의 다른 칸에 드롭: **위치 이동** (찬 칸이면 스왑, 같은 아이템이면 병합).
   - 반대 창의 칸에 드롭: 그 칸으로 이동. 창 여백에 드롭: 빈 칸에 자동 배치.
   - MAIN 아이템 → **캐릭터(3D)에 드롭: 이동 + 즉시 장착**. CHAR 아이템 → 캐릭터에 드롭: 장착.
5. **클릭**: MAIN 좌클릭=캐릭터로 1개 이동 / CHAR 좌클릭=장착·해제 토글 /
   **우클릭=컨텍스트 메뉴** (`상세` 팝업, `장착`/`해제`, `CHAR로 이동`/`MAIN으로 이동`(스택 통째)). 메뉴 밖 클릭 = 닫기.
   **hover** = 미니 툴팁(이름·수량·분류·설명). 드래그 고스트는 매 프레임 커서에 고정(부드러운 추종),
   드래그 시작 판정 5px(EventSystem pixelDragThreshold).
6. **헤더 버튼**: `정렬` = 종류→이름 순 정렬 + 1페이지부터 재배치(저장됨) / `X` = 창 닫기(`I`로 다시 열기).
7. **푸터**: `<` `>` 페이지 이동 (칸이 48개를 넘으면 페이지가 늘어난다. 예: 정렬 없이 slot 48+ 위치로 이동 시).

### B. 에디터 메뉴 — 삭제됨
`Tools/InventorySystem/*` 메뉴(`Editor/InventorySystemTools.cs`)는 프리팹 완결 선언과 함께 삭제했다 (§1 참조).
- UI 크롬 변경: `InventoryPanel.prefab` **직접 편집** (재베이크 수단 없음). 새 `TMP_Text`를 추가했다면
  범용 창 `Tools → TMP → Replace Font In Prefab`으로 SUIT-Bold를 적용.
- 카탈로그 아이템 추가/수정: `Resources/InventoryCatalog_Demo.asset` 인스펙터에서 직접.
- 데모 씬: 이미 빌드된 `InventoryDemo.unity`를 그대로 사용.

### C. 코드에서 호출
```csharp
InventorySystemManager.Instance.SetActiveOwner("arona_poc", characterGameObject); // 활성 캐릭터 지정(전환 시 재호출)
InventorySystemManager.Instance.AddToMain("arona_a_pareo", 1);                    // 획득 → MAIN
InventorySystemManager.Instance.MoveMainToChar("arona_poc", "arona_a_pareo", 1);  // MAIN → 캐릭터
InventorySystemManager.Instance.ToggleEquip("arona_a_pareo");                     // 장착/해제 (EquipSystem 위임)
```
씬에 `InventorySystemManager` + `EquipManager` 하나씩, 캐릭터에 해당 slotId의 `EquipSocket`만 있으면 된다.

### D. 새 아이템 추가
1. `EquipCatalog`(장착물이면)에 엔트리 추가: key/prefab/targetSlotId/fitBias/offset.
2. `InventoryCatalog`에 **같은 key**로 표시 메타 추가: displayName/icon/maxStack.
3. 끝. (Db Key 업그레이더류 절차 없음 — key 오타는 `Contains` 실패 로그로 즉시 드러남.)

---

## 4. 독립성 / discard 안전성

- InventorySystem 코드는 **Vault/Accessory/CharManager를 전혀 참조하지 않음** → 그쪽을 제거·변경해도
  컴파일/동작 영향 없음. 반대로 이 폴더를 통째로 지워도 다른 시스템에 missing script가 생기지 않는다
  (데모씬·프리팹·카탈로그가 전부 폴더 안).
- 유일한 코드 의존은 EquipSystem — EquipSystem이 남아 있는 한 안전. (EquipSystem을 지우면 이 폴더도
  함께 지우는 것이 전제.)
- 데모 캐릭터(arona POC)는 씬에 **완전 언팩** + 앱 종속 컴포넌트 제거(StripAppComponents, EquipSocket/
  EquipMarker만 보존) → 외부 프리팹/매니저 없이 데모 단독 동작.
- 저장 파일은 전용 하위 폴더(`persistentDataPath/InventorySystem/`)만 사용 — 구 시스템 세이브와 충돌 없음.

## 5. 남은 것 (후속 작업)
- **앱 배선 (진행 중 — 타 에이전트 작업)**: `CharManager`가 캐릭터 스폰/전환 시
  `SetActiveOwner(charcode, charObj)`를 호출하고, 창 열기/닫기는 `UIManager` 메뉴 경유로 배선한다
  (`InventoryView.IsVisible`이 이 배선의 토글 판정용 공개 API). 실캐릭터 프리팹에 EquipSocket 저작 필요.
- **Vault 제거**: `CharInventoryOwner`/`AccessoryItem`/`ClickToUseItemUiPlug`/`AccessoryManager`/`AccessoryData`
  + SampleScene의 Vault UI 4프리팹 정리 (별도 커밋).
- **Vault 고아 세이브 파일**: `persistentDataPath/inventory_{charcode}.json`, `accessory_data.json`이
  구 시스템 잔재로 남는다 — 이 시스템은 접근하지 않으므로 **방치 (무해)**. 정리 여부는 Vault 제거 커밋에서 결정.
- (완료) 드래그 앤 드롭 인터랙션 교체 — 칸 이동/스왑/병합, 창 간 이동, 캐릭터(3D) 드롭 장착까지 구현 (§2·§3).
- (완료) 아이콘: 구 Vault AccessoryItem이 쓰던 `Assets/Model/Sprite/*.png` 스프라이트를 카탈로그에 연결
  (Vault와 무관한 독립 자산이라 discard-안전). 아이콘 없는 아이템은 이름 텍스트 폴백.
