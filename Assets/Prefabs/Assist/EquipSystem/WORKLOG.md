# EquipSystem — 완전 독립(Standalone) 악세서리 장착 시스템

기존 `Accessory` 시스템(AccessoryManager/AccessoryData/AccessorySocket 등)과 **코드 의존이 전혀 없는**
독립 시스템. 소켓(콜라이더 볼륨)에 악세서리를 자동 크기 맞춤(볼륨-핏)으로 붙인다.

---

## 1. 구성 (전부 `Assets/Prefabs/Assist/EquipSystem/` 안)

| 파일 | 역할 |
|---|---|
| `Scripts/EquipSocket.cs` | 소켓 컴포넌트(`EquipSocket`) + 장착물 표식(`EquipMarker`). 같은 GameObject의 Collider를 "사이징 볼륨"으로 사용. 필드: `slotId`, `fit`, `pivot`, `placeholderAnchor`. |
| `Scripts/EquipFitter.cs` | 순수 계산: 볼륨 길이/center, 악세서리 고유 크기 측정, uniform 핏 스케일. |
| `Scripts/EquipPlacement.cs` | 배치 로직(측정→스케일→회전→위치). **런타임과 에디터 미리보기가 공유** → WYSIWYG. |
| `Scripts/EquipCatalog.cs` | ScriptableObject 카탈로그. `EquipEntry{key, prefab, targetSlotId, fitBias, positionOffset, rotationOffset}`. |
| `Scripts/EquipManager.cs` | 싱글톤(지연초기화). `Equip(target,key)` / `Unequip(target,slotId)`. catalog 미지정 시 `Resources.Load("EquipCatalog_Demo")` 폴백. |
| `Scripts/EquipDemoController.cs` | 데모 트리거. 키 바인딩으로 장착/교체/해제. |
| `Editor/EquipSocketEditor.cs` | `EquipSocket` 커스텀 인스펙터 + **라이브 미리보기**. |
| `Editor/EquipSystemTools.cs` | `Tools/EquipSystem/*` 메뉴(소켓 추가/복제 셋업/카탈로그/데모씬 빌드/정리). |
| `Resources/EquipCatalog_Demo.asset` | 데모 카탈로그(런타임 자동 로드). |
| `EquipDemo.unity` | 데모 씬. |

---

## 2. 작동 원리

### 소켓과 본의 관계 (중요)
- 소켓(예: `Slot_HairPin_R`)은 **캐릭터의 본(머리 등)의 자식 GameObject**다. → **본을 따라 움직인다**(애니메이션 추종).
- 소켓의 **로컬 위치/회전이 이미 "악세서리가 놓일 자리"로 오프셋**되어 있다(원본 리그에 저작됨).
  그래서 악세서리를 소켓 로컬 원점에 놓으면 → 그 자리에 앉고 + 본을 따라간다. 본에서 수동 오프셋 불필요.
- **조절 지점 = 소켓 자신**(본 기준 Transform) 또는 **콜라이더(캡슐) center/size**. → "콜라이더처럼" 배치.

### 볼륨-핏
- 소켓의 Collider(Capsule 권장)가 "목표 크기 볼륨". 악세서리는 이 볼륨 길이에 맞춰 **균일(uniform) 스케일**.
- 캡슐 크기 = 캐릭터 전체 높이 비례로 자동 계산(스케일이 캐릭터마다 달라도 대응).
- 아이템별 미세보정: 카탈로그 `fitBias`(크기), `rotationOffset`/`positionOffset`(자세/위치).

### 장착 흐름
`EquipManager.Equip(target, key)` → 카탈로그에서 key→(prefab, slot, 오프셋) → `EquipSocket.Find(target, slot)`
→ `EquipPlacement.Fit`로 볼륨-핏 배치 → `EquipMarker` 부착(해제 시 식별).

---

## 3. 사용법

### A. 데모 씬으로 확인
1. `Assets/Prefabs/Assist/EquipSystem/EquipDemo.unity` 열기 → **Play**.
2. `3` chipao / `4` idolfrontribbon / `5` pareo (head1 슬롯에서 교체) · `6` hairpin · `J` head1 해제.

### B. 소켓 새로 배치 (콜라이더처럼)
1. 캐릭터 프리팹 더블클릭(프리팹 모드).
2. 소켓으로 쓸 본(또는 본 하위 빈 GameObject) 선택 → 메뉴 **`Tools/EquipSystem/Add EquipSocket To Selection`**
   → `EquipSocket` + CapsuleCollider 자동 부착(캐릭터 크기 비례).
3. Inspector에서 `slotId` 지정.
4. Inspector의 **`라이브 미리보기` 체크** + Catalog/Accessory Key 선택 → 실제 악세서리가 소켓에 뜬다.
5. **CapsuleCollider 핸들 / 소켓 Transform을 드래그** → 악세서리가 즉시 재핏(크기·위치·자세). Play 불필요.
6. 프리팹 저장.

### C. 악세서리 추가
`Resources/EquipCatalog_Demo.asset` 선택 → `entries`에 한 줄 추가:
`key`(식별자) · `prefab`(악세서리 프리팹) · `targetSlotId`(붙일 슬롯) · `fitBias`(크기 미세) · `rotation/positionOffset`.

### D. 코드에서 호출
```csharp
EquipManager.Instance.Equip(characterGameObject, "hairpin_placeholder");
EquipManager.Instance.Unequip(characterGameObject, "head1");
```
씬에 `EquipManager` 하나 + 캐릭터에 해당 `slotId`의 `EquipSocket`만 있으면 된다.

### E. 메뉴 요약 (`Tools/EquipSystem/`)
- `Setup All` — 소켓+카탈로그+데모씬 일괄 생성.
- `Add EquipSocket To Selection` — 선택 본에 소켓 원클릭.
- `Cleanup Legacy Accessory Components On POC` — POC 프리팹의 옛 Accessory 컴포넌트 제거.

---

## 4. 독립성 / discard 안전성

- EquipSystem **코드는 Accessory 시스템을 전혀 참조하지 않음** → 기존 Accessory 변경(AccessoryManager/
  AccessoryData/Assets/Scripts/Accessory/* 등)을 discard해도 컴파일/동작 영향 없음.
- 데모 캐릭터(arona)는 씬에 **언팩 베이크**됨(외부 프리팹 참조 0) → POC 프리팹이 사라져도 데모는 동작.
- 카탈로그가 참조하는 악세서리 프리팹(`arona_a_chipao/idolfrontribbon/pareo`, `hairpin_placeholder`)은
  **모두 tracked·미변경 자산** → discard 후에도 유지.
- POC 프리팹의 옛 `AccessorySocket`은 제거 완료 → discard로 스크립트 삭제돼도 missing script 없음.

### 외부(assist 밖) 의존 — 전부 유지되는 자산
- 악세서리 프리팹: `Assets/Model/Prefab/arona_a_*.prefab`, `hairpin_placeholder.prefab` (tracked).
- 소켓이 부착된 캐릭터 프리팹: `Assets/Prefabs/Char_toon/*_POC.prefab` (소켓은 콜라이더처럼 캐릭터에 붙는 게 정상).
