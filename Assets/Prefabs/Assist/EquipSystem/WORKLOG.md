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

**1차 사용자 피드백 반영 (2026-07-11)**
- **회전 3축 완비**: 기존 yaw(Ctrl+휠, 법선축)·tilt(Shift+휠, 표면 오른쪽축) 2축만으로는 전 방향
  도달 불가 → **롤(Ctrl+Shift+휠, 접선/전방축)** 추가. `spunBase = yaw × tilt × roll × base`.
- **Shift+휠 한쪽 고정 버그 수정**: Shift를 누르면 OS/에디터가 휠 델타를 가로축(`delta.x`)으로
  보내는데 `delta.y`만 읽어 방향이 항상 +로 고정되던 문제 — 지배 축(|x|>|y|면 x)으로 읽도록
  공용 핸들러 `HandleGhostWheel`로 일원화 (Ctrl+Shift 조합도 동일 처리).
- **검수(Reviewing) 중 회전 계속 조정**: 지점(히트점·lift)은 동결한 채 Ctrl/Shift/Ctrl+Shift+휠로
  회전 조정 + R=회전 리셋(lift 유지) 가능. 조정 시 동결 히트 기준 재핏(UpdateGhost) + 후보 동결값
  재캡처(CaptureCandidate)로 커밋·Record 정합 유지. 맨휠은 계속 카메라 줌(미소비), 턴테이블 중에도 조정 가능.

**2차 사용자 피드백 반영 (2026-07-11)**
- **회전 표기 ZX/YZ/XY로 통일**: yaw/tilt/roll이 직관적이지 않다는 피드백 — 표면 프레임(X=오른쪽,
  Y=법선, Z=접선)의 회전 평면 표기로 전 UI·문서 교체 (Ctrl+휠=ZX, Shift+휠=YZ, Ctrl+Shift+휠=XY).
  내부 필드명(ghostYaw/Tilt/Roll)은 유지 — 표기만 변경.
- **검수 중 거리 조정**: Alt+휠(맨휠은 카메라 줌이라 충돌 회피) + [거리 ±5%] 버튼. 거리는 고스트
  유무와 무관하게 커밋에 반영되므로 게이트 없이 항상 허용. 조정 시 검수 프레이밍 초점(reviewFocus)도 추종.
- **검수 중 크기 조정**: [크기 ±0.1] 버튼 = 카탈로그 sizeRatio 직편집(Undo 가능, 아이템 공용 값).
  세션 중 버튼 변경은 승인 시 "sizeRatio 괴리" 경고를 억제(sizeAdjustedInReview — 의도적 변경).
- **검수 R = 전부 초기화**: 회전 3축·거리는 0으로, (세션 중 버튼으로 바꾼) 크기는 클릭 시점 값으로 원복.
  검수 중 Ctrl+Z(크기 원복 등)는 `Undo.undoRedoPerformed` 구독으로 관측해 고스트·후보 동결값을 재동기.
- 검수 조정 공통 후처리 RefreshReviewAfterAdjust: 재핏 + 후보 재동결(RecaptureCandidatePose —
  이제 lift 포함) + 프레이밍 갱신. 회전 조정만 고스트 유효(IsReviewGhostLive) 게이트 유지.

**3차 사용자 피드백 반영 (2026-07-11) — 카탈로그 읽기 전용 원칙**
- **"등록 덮어쓰기" 경로 삭제**: 구 다이얼로그의 "targetSlotId를 socket_N으로 바꾸기" 선택지는
  다른 캐릭터의 같은 자리 연결을 끊는 고위험 조작 — 경로 자체를 제거. **의미 있는 자리 이름(head1 등)은
  구조적으로 쓰기 불가** (CreateSocketAtHit의 카탈로그 쓰기 = "첫 등록"(빈 값·socket_*)만 허용).
- **다이얼로그 재설계 + 승인 시점으로 이동** (소켓 생성 "전"에 결정): 자리 소켓이 캐릭터에
  존재하면 ①그 소켓 덮어쓰기(기존 소켓 재사용 커밋 — repick과 동일 경로) ②아이템 key 이름으로 새 소켓
  ③임시 이름 / 부재면 ①그 이름으로 만들기(권장) ②key 이름 ③임시 이름. 버튼은 "1/2/3 수행",
  Esc=3(임시 이름, 최소 개입). 본문에 "카탈로그 등록은 어떤 선택에서도 바뀌지 않습니다" 명시.
- 참고: 소켓 리네임 동기화(EquipSocketEditor)와 베이크=이사(BakeFromObject)의 카탈로그 "이관"은
  기존 유지 — 자리 이름을 바꾸는 게 아니라 사용자가 이름을 바꾼 소켓을 링크가 따라가는 것.
- **승인 이중 커밋 방어** (임시+키 이름 소켓 2개 생성 리포트 대응): ① Enter 처리 시 모달이 열리기 전에
  이벤트 소비(씬·창 양쪽) ② `approveInProgress` 재진입 가드(try/finally) ③ ApproveCandidate/BackToPicking에
  페이즈 가드 — Off 상태에서 뒤늦은 호출이 "유령 Picking"(씬 구독 없는 Picking)을 만드는 구멍 봉쇄
  ④ 다이얼로그 선택 결과를 콘솔에 명시("연결 다이얼로그 선택 → 커밋 slotId ...") — 재발 시 즉시 진단 가능.

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
