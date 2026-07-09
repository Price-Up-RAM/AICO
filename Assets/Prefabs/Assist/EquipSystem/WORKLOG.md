# EquipSystem — 완전 독립(Standalone) 악세서리 장착 시스템

기존 `Accessory` 시스템과 **코드 의존이 전혀 없는** 독립 시스템.

## 전제

- **캐릭터 프리팹에 슬롯은 원래 없다.** 슬롯(소켓)은 저작으로 만든다.
- 핵심 가치 = 콜라이더 다루듯 쉬운 소켓 저작: **본을 지정하면 소켓이 생기고**, 캡슐 핸들로
  조정하고, 한 번 만든 배치(원본=템플릿)를 다른 캐릭터/의상에 **복사(스탬프)**한다.
- 본 이름 하드코딩 없음 — 부착 본은 사용자가 지정(드래그)하는 것이 최종. 자동 해석
  (NAME→HUMANOID→ALIAS→물리필터 NEAREST)은 제안/전파용.
- 표준 슬롯 5종(사용자 확정): `chest`(앞가슴) `back`(등) `head`(헤어핀) `overhead`(모자/천사링) `origin`(오오라).
  물리 흔들림은 비목표 — 본 추종까지만.

## 메뉴 (딱 2개)

| 메뉴 | 용도 |
|---|---|
| `Tools/EquipSystem/Socket Author` | **소켓 만들기.** 표준 5슬롯 각각에 본을 드래그 → [소켓 생성/이동]. [본 자동 제안]은 빈 필드를 사다리 해석으로 채워주는 참고. [템플릿 캡처]로 현재 배치를 원본에 저장. |
| `Tools/EquipSystem/Propagation Window` | **복사(전파).** Donor 모드(같은 스켈레톤 의상, 무손실)/Template 모드(다른 캐릭터, 사다리). 드라이런 기본, 리포트+[열기]. |

(+ 소켓 선택 시 Inspector의 **라이브 미리보기** — 실제 악세서리를 띄워놓고 캡슐/Transform 드래그로 조정)

## 표준 워크플로우

### 1) 만들기 (씬에서 연습 → 커밋)
1. 씬에 캐릭터 프리팹 인스턴스 드래그.
2. `Socket Author` 창 → 대상 지정([선택에서]) → 각 슬롯에 본 드래그(또는 [본 자동 제안] 후 확인/수정)
   → **[소켓 생성/이동]**. origin은 루트 자동.
3. 각 소켓 선택 → 라이브 미리보기로 위치/캡슐 조정.
4. 확정: 인스턴스 루트 → **Overrides → Apply All** (연습 폐기는 Revert All).
5. `Socket Author`의 **[템플릿 캡처]** → 이 배치가 "원본"이 됨.

### 2) 복사 (전파)
- **같은 캐릭터 의상들**: Propagation 창 Donor 모드 — 완성된 프리팹을 Donor로, 의상 다중선택 → 드라이런 → Stamp.
- **다른 캐릭터**: Template 모드 — 드라이런 리포트에서 해석 라벨(NAME/HUMANOID/ALIAS/NEAREST) 확인 → Stamp → 경고 슬롯만 [열기]로 조정.
- **보호**: 손으로 만든/보정한 소켓은 어떤 전파도 덮어쓰지 않음(KEEP_MANUAL/KEEP_TUNED).

### 3) 장착 (런타임)
```csharp
EquipManager.Instance.Equip(characterGameObject, "hairpin_placeholder"); // 장착/교체
EquipManager.Instance.Unequip(characterGameObject, "head");              // 해제
```
- 씬에 `EquipManager` 1개 + 캐릭터에 해당 `slotId` 소켓만 있으면 동작. 같은 slotId 재장착=교체.
- 악세서리는 소켓 캡슐 크기에 uniform 핏 + 카탈로그(`Resources/EquipCatalog_Demo`)의 fitBias/offset 보정.
- 아이템 추가 = 카탈로그 entries에 한 줄(key/prefab/targetSlotId/fitBias/offset).

## 구성 파일

| 파일 | 역할 |
|---|---|
| `Scripts/EquipSocket.cs` | 소켓 컴포넌트(slotId/fit/pivot, 같은 GO의 Collider=사이징 볼륨) + `EquipMarker` |
| `Scripts/EquipSocketStamp.cs` | 전파 스탬프 마커+스냅샷(손보정 감지 — 캡슐 대비 상대 오차, 스케일 불변) |
| `Scripts/EquipFitter.cs` / `EquipPlacement.cs` | 볼륨-핏 계산/배치 (런타임=미리보기 공유) |
| `Scripts/EquipCatalog.cs` / `EquipManager.cs` / `EquipDemoController.cs` | 카탈로그 SO / 장착 매니저 / 데모 |
| `Editor/EquipSocketAuthorWindow.cs` | **Socket Author 창** |
| `Editor/EquipPropagationWindow.cs` | **전파 창** |
| `Editor/EquipSlotStamper.cs` | Capture / Donor 복사 / 사다리 스탬프(`ResolveBone`) / 배치 IO |
| `Editor/EquipSlotTemplate.cs` | 원본 SO (표준 5종, `Editor/Templates/EquipSlotTemplate_Default.asset`) |
| `Editor/EquipAuthoringUtil.cs` / `EquipPhysicsBoneFilter.cs` | 공용 계산 / 물리 본 안전망 |
| `Editor/EquipSocketEditor.cs` | 소켓 인스펙터 + 라이브 미리보기 |
| `EquipDemo.unity` / `Resources/EquipCatalog_Demo.asset` | 데모 씬 / 데모 카탈로그 |

## 이력 메모
- 2026-07-09: Phase1+2 구현(멀티에이전트 리뷰 40건 반영, 스모크 77/77 PASS 후 테스트 파일은 정리).
  이후 사용자 피드백으로 도구 표면 대청소: 메뉴 2개(Socket Author/Propagation)로 축소,
  POC 셋업/클립보드/골든 하드코딩(Bip001)/스모크/데모빌더 메뉴·파일 삭제.
- 독립성: EquipSystem 코드는 기존 Accessory/CharManager 등을 참조하지 않음. 악세서리 프리팹
  (`Assets/Model/Prefab/*`)만 자산으로 공유.
