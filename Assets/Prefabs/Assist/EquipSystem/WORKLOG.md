# EquipSystem — 완전 독립(Standalone) 악세서리 장착 시스템

기존 `Accessory` 시스템과 **코드 의존이 전혀 없는** 독립 시스템.

## 현행 철학 (2026-07-09 확정: "클릭 = 소켓")

- **커서 레이가 캐릭터 메시에 처음 맞는 지점이 곧 소켓 자리.** 캡슐/콜라이더/표준 슬롯 시퀀스 폐지.
- 부착 본 = 히트 삼각형의 **본 웨이트 지배 본** 자동(물리 헤어 본은 승격 사다리로 회피 — 머리카락을 클릭해도 head에 붙음).
- 크기 기준 = 클릭 순간의 본→히트점 거리 **refDist**(부모-로컬 float) — **캐릭터가 커지면 lossy를 타고
  악세서리도 같이 커진다.** 크기 사다리: refDist → 캡슐(레거시) → 둘 다 없으면 장착 거부+경고.
- 구조: `Socket_<slotId>`(본 원점, 사용자가 만지는 유일한 이름) → `placeholder`(클릭점 부착점 — 구명 PH_spot/"spot"은 별칭 호환).
  카탈로그는 `targetSlotId`만. slotId가 카탈로그·전파의 열쇠 (socket_N은 임시 이름 — 리네임 필수, 배지로 경고).

## 메뉴 (딱 2개)

| 메뉴 | 용도 |
|---|---|
| `Tools/EquipSystem/Socket Maker` | **소켓 만들기.** 악세서리 고르고 [+ 소켓] → 고스트(실물 미리보기)가 커서 따라 표면을 흐름 → 휠=거리 띄우기, Ctrl/Shift+휠=회전, R=리셋 → 클릭 = 소켓+부착점 생성 + refDist 베이크 + (토글 시) 카탈로그 targetSlotId 자동 연결. 접촉 기준: Pivot(기본, 원점=클릭점)/BottomAlign/Center. 베이크 폴드아웃: 손 배치한 오브젝트+본 → 소켓. |
| `Tools/EquipSystem/Propagation Window` | **복사(전파).** Donor(같은 스켈레톤 의상 — placeholder까지 복제)/Template 모드. 드라이런 기본. |

(+ 부착점 `placeholder` 선택 시 인스펙터: **글라이드**(메시 표면 드래그, 확정 시 refDist 재베이크) + 라이브 미리보기 + Size Ratio(카탈로그 직편집))

- **장착 소켓 해석 사다리** (`EquipSlotResolver` — 런타임·현황판 공용): ① 아이템 key와 같은 이름의 소켓
  ② `targetSlotId` ③ `fallbackSlotIds` 순서대로 ④ 없으면 장착 불가+경고. overhead→head 별칭 포함.
  등록 요령: 특정 자리 아이템은 targetSlotId(예: hairpin), 폴백엔 범용 자리(head/chest/origin)를 순서대로.
- 배치 시 소켓에 **`EquipPlacementRecord`**(악세서리 key + sizeRatio + 고스트 최종 소켓-로컬 TRS)가 기록됨 — 재현/전파 검수/조정 시작값용.
- 배치/베이크 직후 베이크 폴드아웃에 **방금 만든 부착점이 자동 매핑** — 본/이름만 바꿔 "다른 본으로 다시 굽기" 가능. 위치 소스에 소켓을 주면 자동으로 그 부착점(placeholder) 위치로 대체.

## 표준 워크플로우

1. 씬(또는 프리팹 모드)에 캐릭터 → **Socket Maker** 열기(대상 자동 인식).
2. 악세서리 선택 → **[+ 소켓]** → 고스트 보면서 클릭 → `socket_N` 생성(소켓 자동 선택됨).
3. 인스펙터에서 **slotId 리네임**(head, ribbon 등 — 카탈로그 연결 토글을 켰다면 카탈로그 targetSlotId도 함께!).
4. 미세조정: 부착점 `placeholder` 선택 → 글라이드/라이브 미리보기/Size Ratio.
5. 커밋: 씬 인스턴스면 Overrides → Apply All.
6. 장착: `EquipManager.Instance.Equip(char, key)` / 해제 `Unequip(char, slotId)`.

- **origin(오오라류, 표면 없음)**: 베이크 폴드아웃에서 본 필드에 캐릭터 루트를 지정하면 동작
  (refDist는 5%키 폴백 — 필요 시 PH의 bakedRefDistLocal 직접 조정). 전용 버튼은 P3에서 복원 예정.
- 신규 카탈로그 엔트리를 spot 없는 구 캐릭터에 장착 → **즉시 거부+경고** (확정 정책).

## 구성 파일

| 파일 | 역할 |
|---|---|
| `Scripts/EquipSocket.cs` / `EquipPlaceholder.cs` | 소켓(slotId) / 부착점(spot, refDist·캡슐좌표·회전규약·contactAnchor) |
| `Scripts/EquipPlacement.cs` | 배치 공유 로직(런타임=미리보기=고스트). 크기 사다리 + **바운드 중심 정렬**(피벗-메시 오프셋 립 악세서리 대응) + BottomAlign |
| `Scripts/EquipManager.cs` / `EquipCatalog.cs` / `EquipFitter.cs` / `EquipCapsuleMath.cs` | 장착 매니저(spot 자동 라우팅+거부 가드) / 카탈로그 / 계산 |
| `Editor/EquipSocketMakerWindow.cs` | **Socket Maker** (고스트 픽+회전+카탈로그 연결+베이크) |
| `Editor/EquipPlaceholderEditor.cs` | 글라이드(메시/캡슐/자유) + 라이브 미리보기 + Size Ratio |
| `Editor/EquipMeshRaycaster.cs` | CPU 수동 스키닝 레이캐스터 + 지배 본 질의 (에디터 전용) |
| `Editor/EquipSlotStamper.cs` / `EquipPropagationWindow.cs` / `EquipSlotTemplate.cs` | 전파(Donor는 placeholder 복제 포함) / 전파 창 / 템플릿 SO |
| `Editor/EquipSocketEditor.cs` / `EquipAuthoringUtil.cs` / `EquipPhysicsBoneFilter.cs` | 소켓 인스펙터(리네임 배지) / 공용 유틸 / 물리 본 필터 |
| `EquipDemo.unity` / `Resources/EquipCatalog.asset` | 데모 씬 / 카탈로그 |

## P3 예정 (전파 개편 — 미구현)

- 템플릿 캡처 UI를 Propagation 창에 재배치(현재 캡처 진입점 없음 — 기존 템플릿 에셋은 동작),
  신모델(refDist) 캡처 지원, 별칭 학습 버튼 복원(현재는 템플릿 에셋 인스펙터에서 boneAliases 직편집 가능).
- 메시 레이 전파(본 해석+방향 레이→대상 자신의 메시): 레이 기하 수정 4건(본 내부 출발 노멀 반전,
  최원 히트, 소켓 단위 별칭 학습, 비표준 def 완화) — VERDICT_ClickIsSocket.md §5 P3.
- [+ origin 소켓] 버튼 복원. P4: 캡슐 백필 후 캡슐 코드/UI 삭제.

## 문서
- `VERDICT_ClickIsSocket.md` — "클릭=소켓, 캡슐 폐지" 판정문(사망·생존 목록, 로드맵)
- `SPEC_MeshFirst.md` — 메시-퍼스트 스펙 / `PROPOSAL_*.md` — 이전 단계 설계 이력

## 독립성
- EquipSystem 코드는 기존 Accessory/CharManager 등을 참조하지 않음. 악세서리 프리팹(`Assets/Model/Prefab/*`)만 자산 공유.
