# EquipSystem — 완전 독립(Standalone) 악세서리 장착 시스템

기존 `Accessory` 시스템과 **코드 의존이 전혀 없는** 독립 시스템.
사용법·스크립트 설명은 **`README.md`** 참조. 이 문서는 작업 이력/로드맵.

## 현행 철학 ("클릭 = 소켓", 캡슐 철거 완료)

- **커서 레이가 캐릭터 메시에 처음 맞는 지점이 곧 소켓 자리.**
- 구조: `Socket_<slotId>`(본 원점, 이름표) → `placeholder`(부착점 — 위치/회전/크기 기준 refDist 보유.
  구명 PH_spot/"spot"은 별칭 호환).
- 크기 기준 = **refDist 단독** (본→표면 거리의 부모-로컬 베이크 — 캐릭터가 커지면 lossy를 타고
  악세서리도 같이 커진다). 미베이크(0)면 장착 거부+경고. 캡슐/볼륨-핏 경로는 완전 삭제됨.
- 장착 해석 사다리 (`EquipSlotResolver`, 런타임·현황판 공용):
  ① 아이템 key와 같은 이름의 소켓 ② `targetSlotId` ③ `fallbackSlotIds` 순서대로 ④ 거부+사유.
- 배치 규약 = `contactAnchor`: Pivot(원점=부착점, 기본)/Center/BottomAlign.
  고스트(Socket Maker)와 실장착(`EquipPlacement.FitToPlaceholder`)이 동일 수식 (WYSIWYG).
- 배치 시 소켓에 `EquipPlacementRecord`(악세서리 key + 소켓-로컬 TRS) 기록 — [기록 재현] 버튼으로 검증.

## 최종보고 — 캡슐 철거 (P4 조기 집행, 2026-07-11)

감사 워크플로우(코드 그래프/데이터 잔존/데드 스크립트/refDist 검증 + 계획 + 비평 6에이전트) 후 집도.

**삭제된 것**
- 파일: `EquipCapsuleMath.cs`, `EquipSlotTemplate.cs`(+기본 템플릿 에셋), `EquipDemoController.cs`(워크벤치가 대체),
  문서 `SPEC_MeshFirst.md`/`PROPOSAL_SocketPlaceholder.md`/`PROPOSAL_SlotAuthoring.md`/`VERDICT_ClickIsSocket.md`(요지는 본 문서로 이관)
- 코드: 레거시 직부착 `EquipPlacement.Fit`, `EquipSocket`의 fit/pivot/placeholderAnchor/SizingVolume,
  `EquipPlaceholder`의 캡슐 좌표 5필드+Apply/CaptureFromTransform, `EquipEntry`의 fitMode/positionOffset/targetPlaceholderId,
  Template 크로스 캐릭터 전파 전체(캡처/스탬프/본 해석 사다리 — P3에서 메시 레이 기반 재구축 예정),
  `EquipFitter` 볼륨 함수, 각 에디터의 캡슐 분기(표준 시드/캡슐 스냅/볼륨-핏 미리보기)
- 데이터: `arona_6_clean_POC.prefab`의 레거시 캡슐 소켓 2개(Slot_HairPin_R/Slot_Head_1),
  `InventoryDemo.unity`의 동일 사본 2개 (캐릭터 물리용 `Collider` GO는 보존)

**바뀐 것**
- `EquipPlacement.FitToPlaceholder`가 **bool 반환** — Play 모드에서 Destroy가 지연되어 `== null` 검사가
  통하지 않는 침묵 실패를 반환값 판정으로 봉쇄 (호출자 전부 갱신: Manager/에디터 미리보기/테스트)
- `EquipSocketStamp.IsHandTuned` 잣대를 캡슐 높이 → **refDist 스냅샷**으로 재기저 (스케일 불변 유지)
- 전파 창은 **Donor 전용** (placeholder+refDist ×lossy 편차 환산 복사, 손보정 보호 유지)
- InventorySystem `EquipKey`가 장착 실패 시 미러를 오염시키지 않게 반환값 판정 추가
- **refDist 판정**: 캡슐과 코드 결합 전무(무죄) — 접점이던 "캡슐 반경 폴백"과 "캡슐 없음=신모델 게이팅"을
  걷어내 refDist가 유일한 크기 기준으로 승격됨

**남은 후속 과제**
- 카탈로그 4엔트리(chipao/ribbon/pareo→head1, hairpin_placeholder→hairpin)는 대응 소켓이 없어
  **전부 "사유 있는 거부" 상태 — 신규 소켓 재저작 대기** (에러 아님, 정상)
- 카탈로그 YAML의 구필드 잔값(fitMode 등)은 로드 시 무시되고 다음 저장에서 자동 소멸
- 고스트/실장착 수식 단일화(2차분), P3 전파 재구축

## Socket Maker 2단계 배치 + 고스트 재조정 (2026-07-11)

**작업 내용**
- **2단계 배치(검수 스테이지)**: 클릭 즉시 생성 → "클릭=후보 고정(Reviewing) → Enter/[승인]=생성"으로 변경.
  검수 중 자유 카메라(내비 이벤트 무소비) + 턴테이블/5방향 시점 버튼(`SceneView.LookAtDirect`, size=프레이밍
  반경이라 극단 스케일 대응). Esc=조준 복귀(회전·거리 보존, 직전 후보 회색 마커), [세션 취소]=완전 무변경.
- **고스트 재조정(BeginRepick)**: placeholder/소켓 인스펙터 버튼 → 그 아이템(Record→key==slotId→
  targetSlotId 사다리로 해석)의 고스트 픽 세션 → 승인 시 신규 생성 대신 **기존 소켓 덮어쓰기**
  (직접 참조라 리네임 면역, 본 이사 포함). slotId·카탈로그 연결(링크 블록 스킵)·스탬프 무접촉,
  기존 배치는 주황 참조 마커+실물 참조(`__EquipPreview__RepickRef`, 외관 무변조)로 표시.
  repick이 덮은 창 설정(접촉 기준/악세서리 선택)은 세션 종료 시 원복.
- `pickActive`(bool) → `PickPhase`(Off/Picking/Reviewing) 승격 + `CreateSocketAtHit`를
  `fromPick`/`overwriteSocket` 파라미터화(bool 반환 — 프리팹 가드 실패 전파). 커밋은 후보에
  박제한 key 재해석 + 고스트 동결 TRS로 Record 산출(고스트 파괴 내성).
- **Undo 갭 2건 봉합**: 기존 소켓 재사용 시 소켓 TRS 재설정 / slotId 대입 — 둘 다 RecordObject 추가.
  승인 = 정확히 1 Undo 그룹("소켓 생성/재조정 (검수 승인)") — Ctrl+Z 1회 완전 복원.
- **세션 정리 콜백 신설**: 플레이 진입(`playModeStateChanged` — 도메인 리로드 off 환경 구멍 봉합)·
  프리팹 스테이지 닫힘(`prefabStageClosing` — 후보의 renderer/bone은 스테이지 결합이라 이월 금지).
- 세션 중 잠금: 대상 캐릭터 행(전 세션), 악세서리 블록(Reviewing∥repick), 베이크(전 세션), [테스트](Reviewing).
  승인 직전 재검증: slotId 충돌 재발급(신규)·프리팹 본 이사 차단(repick)·sizeRatio 괴리 경고.

**알려진 한계 (P3 후보)**
- ① refDist **1% 이하** 변화 재조정은 `IsHandTuned`(EquipSocketStamp)가 placeholder 자체 TRS를 안 봐
  다음 전파에 덮일 수 있음 — 승인 로그의 refDist 변화율로 가시화만. 판정식 확장 또는 전파 검수
  (Record 활용 계획)와 함께 결정.
- ② 씬 뷰 dynamicClip off 사용자 설정 + 극단 스케일에서 프레이밍 잘림 가능 (오버레이 경고만, 설정 강제 변경 안 함).
- ③ 재조정의 기존 배치 실물 참조는 외관 무변조(반투명화 없음 — 머티리얼 오염/누수 회피). 구분은 라벨이 담당.

## 메뉴 (3개)

| 메뉴 | 용도 |
|---|---|
| `Tools/EquipSystem/Socket Maker` | 소켓 만들기 (고스트 클릭 배치 + 카탈로그 연결 + 현황판 + [테스트] + 베이크=이사) |
| `Tools/EquipSystem/Propagation Window` | Donor 전파 (같은 스켈레톤 의상에 소켓 무손실 복사, 드라이런 기본) |
| `Tools/EquipSystem/Build Workbench Scene (EquipDemo)` | 워크벤치 씬 재생성 |

(+ 캐릭터 루트에 `EquipSocketController` 추가 = origin 소켓(0,0,0) 자동 생성)

## P3 예정 (전파 재구축 — 메시 레이 기반)

Template 전파(캡슐 소켓 공장)는 삭제됨. 재구축 시 반영할 재료:

- **레이 기하 수정 4건**(구 VERDICT §5): 본 내부 출발 레이 노멀 반전 / 최원(가장 바깥) 히트 채택 /
  소켓 단위 별칭 학습 / 비표준 slotId 정의 완화
- **본 별칭 표** (구 템플릿 기본값):

| slotId | 별칭 토큰 | Humanoid 본 |
|---|---|---|
| head | head, 頭, atama | HumanBodyBones.Head |
| chest / back | spine2, chest, upperchest, 上半身2 | HumanBodyBones.Chest |
| origin | (본 탐색 없음 — 루트 부착) | — |

- **살아있는 결정**(구 PROPOSAL): 물리 흔들림(MagicaCloth) 비대응 — 흔들림 적은 부위(머리/등/가슴/원점)만 표적.
  골든 캐릭터 = Mari_Original. 자동 피팅은 계속 보류(수동만).
- 캡처 UI를 Propagation 창에 재배치, 전파 결과 검수에 `EquipPlacementRecord` 활용.

## 남은 로드맵

1. **2차분**: 고스트(Socket Maker)와 실장착(FitToPlaceholder)의 배치 수식 단일 함수화 — "두 곳 수정" 사고 봉쇄
2. **P3**: 메시 레이 전파 재구축 (위 재료)
3. 카탈로그 신규 재저작 (사용자 — 프레시 스타트 방침)

## 독립성
- EquipSystem 코드는 기존 Accessory/CharManager 등을 참조하지 않음. 악세서리 프리팹(`Assets/Model/Prefab/*`)만 자산 공유.
- InventorySystem → EquipSystem 단방향 의존(Resolver/Equip 경유)만 존재.
