# EquipSystem

캐릭터에 악세서리를 장착하는 **완전 독립(standalone)** 시스템.
기존 Accessory/CharManager 등 어떤 외부 스크립트에도 의존하지 않는다.

핵심 철학은 **"클릭 = 소켓"** — 씬에서 캐릭터 표면을 클릭한 지점이 곧 부착 자리가 되고,
크기 기준(refDist)이 그 순간 자동으로 구워져 캐릭터가 커지면 악세서리도 같이 커진다.

---

## 1. 핵심 개념

| 개념 | 뜻 |
|---|---|
| **소켓** (`Socket_<slotId>`) | 자리의 "이름표". 본(Bone) 밑에 붙는 빈 GO. 이름(slotId)이 카탈로그·전파의 열쇠 |
| **placeholder** (부착점) | 소켓의 자식. 악세서리가 실제로 붙는 위치/회전 + 크기 기준(refDist) 보유 |
| **refDist** | 본→표면 거리의 부모-로컬 베이크. 악세서리 최장변 = refDist × 2 × sizeRatio. 0이면 장착 거부 |
| **카탈로그** (`Resources/EquipCatalog.asset`) | 아이템 장부: key ↔ 프리팹 ↔ 자리 이름. 프로젝트에 1개만 유지 |
| **해석 사다리** | 장착 시 소켓을 찾는 순서: ①key와 같은 이름 ②targetSlotId ③fallbackSlotIds ④거부+사유 |
| **contactAnchor** | 접촉 규약: Pivot(모델 원점=클릭점, 기본) / Center(바운드 중심) / BottomAlign(표면에 얹음) |
| **EquipPlacementRecord** | 소켓 생성 순간의 고스트 결과(key+TRS) 기록 — [기록 재현]으로 검증 |

## 2. 빠른 시작 (사용법)

### 새 아이템 등록 → 소켓 만들기 → 장착

1. **카탈로그 등록**: `Resources/EquipCatalog.asset` 선택 → Entries `+` → Key와 Prefab만 채우면 시작 가능
   (Target Slot Id는 배치 때 자동 연결됨. sizeRatio는 0.3~0.5 추천)
2. **소켓 만들기**: 캐릭터 프리팹을 열고 `Tools → EquipSystem → Socket Maker`
   - 악세서리 선택 → **[+ 소켓]** → 고스트가 커서를 따라 표면을 흐름
   - **휠=거리 띄우기, Ctrl+휠=회전(법선축), Shift+휠=기울임, R=리셋, Esc=취소**
   - **클릭 = 후보 고정(검수)** — 고스트가 그 자리에 멈추고 아직 소켓은 안 생긴다.
     검수 중에는 카메라가 자유(휠 줌·우클릭 궤도·중클릭 팬 그대로) + **[턴테이블]**·5방향
     시점 버튼(정면/후면/좌/우/상)으로 여러 각도 확인 가능
   - **Enter 또는 [승인]** = `socket_N` 생성 (지배 본 자동 선택, refDist 자동 베이크, 카탈로그
     자동 연결, Ctrl+Z 1회로 전부 취소)
   - **Esc 1회 = 재조정** (조준 복귀 — 휠로 준 회전·거리는 유지, 직전 후보 자리에 회색 마커).
     **주의: Esc 2회 연속 = 세션 취소** (조준 단계의 Esc는 세션 취소)
3. **이름 짓기**: 생성된 소켓 인스펙터에서 slotId를 의미 있는 이름(hairpin 등)으로 —
   **GO명과 카탈로그가 자동 동기화**된다. (이미 등록된 자리 이름이면 배치 시 다이얼로그가 "그 이름으로 짓기"를 권장)
4. **미세조정**: `placeholder` 선택 → 씬의 구체 핸들 드래그(메시 표면 글라이드, 놓는 순간 refDist 재베이크)
   + 라이브 미리보기 + Size Ratio 직편집

### 고스트 재조정 (기존 소켓의 위치를 다시 잡기)

`placeholder` 인스펙터의 **[Socket Maker에서 고스트 재조정]** (또는 소켓 인스펙터의
[고스트 재조정]) → 그 아이템의 고스트로 픽 세션이 열리고, 기존 배치 자리에 주황 참조
마커(+실물 참조)가 남는다. 새 지점 클릭 → 검수 → 승인하면 **신규 소켓을 만들지 않고
그 소켓을 덮어쓴다** (본 이사 포함, Ctrl+Z 1회 완전 복원).

| 하고 싶은 것 | 도구 |
|---|---|
| 위치만 미세 이동 | 메시 글라이드 (placeholder 구체 핸들 — **회전은 표면 기준으로 리셋됨**) |
| 표면에서 띄우기 | 스냅 모드 Free |
| **위치·회전·본·refDist를 한 번에 다시 잡기** | **고스트 재조정** |

재조정은 slotId·카탈로그 연결·전파 스탬프를 건드리지 않는다. refDist가 재베이크되므로
(1% 초과 변화 시) 다음 도너 전파에서 손보정(KEEP_TUNED)으로 보호된다.

**제약**: 재조정 중 악세서리 변경 불가(먼저 카탈로그를 정리한 뒤 재조정) ·
프리팹 인스턴스 소켓의 본 이사는 프리팹 모드에서 · 2D/회전잠금 씬 뷰에서는
턴테이블·시점 버튼 비활성.
5. **장착 테스트**: Socket Maker 하단 현황판(초록=가능/빨강=불가+사유)의 **[테스트]** 버튼 — 플레이 불필요
6. **런타임 장착**: `EquipManager.Instance.Equip(캐릭터, key)` / 해제 `Unequip(캐릭터, slotId)`
   - 씬에 `EquipManager` 컴포넌트 GO 필요 (카탈로그는 Resources에서 자동 로드)

### origin 소켓 (표면이 없는 자리 — 오오라류)

캐릭터 루트에 **`EquipSocketController` 컴포넌트 추가** → 원점(0,0,0)에 origin 소켓 자동 생성
(refDist = 키 5%). 인스펙터에서 캐릭터의 소켓 현황도 보인다.

### 의상 전파 (같은 스켈레톤)

`Tools → EquipSystem → Propagation Window`: 소켓을 완성한 프리팹(Donor)을 지정하고 대상 의상
프리팹들을 추가 → 드라이런으로 리포트 확인 → 적용. 본 이름 일치 기준 무손실 복사(refDist 포함),
손보정된 소켓(KEEP_TUNED/KEEP_MANUAL)은 덮어쓰지 않는다.
(다른 캐릭터로의 크로스 전파는 P3에서 재구축 예정)

### 워크벤치 씬 (EquipDemo.unity)

캐릭터 프리팹을 씬에 끌어놓고 **Play**:

| 기능 | 설명 |
|---|---|
| 장착 매트릭스 | 캐릭터×아이템 그리드. 셀 색=사다리 순위(초록①/청록②/노랑③/빨강=불가). **셀 클릭=실장착** |
| 점유 현황 | 선택 캐릭터의 소켓별 장착물 + 개별 [해제] |
| 스모크 테스트 | 전 캐릭터×전 아이템 장착 시도 → 실패 사유 리포트 (기존 코디는 해제됨) |
| 코디/스케일 | 전부 장착·해제·랜덤 / 스케일 ×10·×0.1·복원(크기 추종 검증) |
| 하단 로그 | 모든 장착 결과 + **거부 사유** 상시 표시 |

**키**: `Tab` 캐릭터 전환 · `F5` 재스캔 · `F1` 패널 토글 · `F` 카메라 프레이밍 · `T` 턴테이블 · `M` 소켓 마커
**카메라**: 우클릭 드래그=궤도 · 휠=줌 · 휠클릭=팬 (스케일 1~120000 대응)
씬이 망가지면 `Tools → EquipSystem → Build Workbench Scene (EquipDemo)`로 재생성.

## 3. 스크립트 안내

### Scripts/ (런타임)

| 파일 | 역할 | 주요 함수 |
|---|---|---|
| `EquipManager.cs` | 장착/해제 매니저 (lazy 싱글톤) | `Equip(target, key)` / `Equip(target, key, out reason)` — bool+사유 / `Unequip(target, slotId)` |
| `EquipSlotResolver.cs` | 해석 사다리 (런타임·에디터 공용) | `Resolve(character, entry, out slotId, out priority)` / `Candidates(entry)` |
| `EquipPlacement.cs` | 배치 수학 (런타임=미리보기=WYSIWYG) | `FitToPlaceholder(inst, socket, ph, entry)` — bool 반환, 거부 시 인스턴스 파괴 |
| `EquipSocket.cs` | 소켓 컴포넌트 + 마커 | `slotId` / `FindPlaceholder(id)`(spot 별칭 호환) / `static Find(character, slotId)` / `EquipMarker`(장착물 표식) |
| `EquipPlaceholder.cs` | 부착점 컴포넌트 | `placeholderId` / `bakedRefDistLocal` / `contactAnchor` / `OwnerSocket` |
| `EquipCatalog.cs` | 카탈로그 SO + 엔트리 | `Get(key)` / `Contains(key)` / `Entries` |
| `EquipFitter.cs` | 크기 측정 순수 계산 | `MeasureNaturalFull(inst, out longest, out center, out extents)` / `ComputeFitScale` |
| `EquipMath.cs` | 공용 수학 | `LossyAvg(transform)` — 스케일 정규화 기준 |
| `EquipSocketController.cs` | 캐릭터 쪽 진입점 | 추가 순간(Reset) `CreateOriginSocket()` — origin 소켓 부트스트랩 |
| `EquipPlacementRecord.cs` | 배치 기록 (데이터 전용) | accessoryKey + 고스트 소켓-로컬 TRS |
| `EquipSocketStamp.cs` | 전파 스탬프 마커 | `IsHandTuned()`(refDist 잣대 상대 오차) / `TakeSnapshot()` |
| `EquipWorkbenchController.cs` | 워크벤치 코어+IMGUI 패널 | `Roster`/`Selected`/`RefreshRoster()`/`Select(i)`/`Log(msg)` |
| `EquipWorkbenchCamera.cs` | 워크벤치 카메라 | 궤도/줌/팬/`FrameSelected()`/턴테이블 (클립 플레인 자동 적응) |
| `EquipWorkbenchTools.cs` | 워크벤치 도구 (static) | `RunSmokeTest(roster)` / `EquipAll`/`UnequipAll`/`EquipRandom` / `ApplyScale`/`RestoreScale` |
| `EquipWorkbenchMarkers.cs` | 소켓/부착점 화면 마커 | `M` 토글, 선택 캐릭터의 ● 소켓 / ◆ 부착점(refDist) 표시 |

### Editor/ (에디터 전용)

| 파일 | 역할 | 핵심 UI |
|---|---|---|
| `EquipSocketMakerWindow.cs` | **Socket Maker 창** | 대상 자동 인식 / 고스트 픽(휠·회전·접촉기준) / 클릭=후보→검수(턴테이블·시점 버튼)→승인=생성 / 고스트 재조정(BeginRepick — 기존 소켓 덮어쓰기) / 배치=카탈로그 연결+Record 기록 / 베이크(=이사: 링크·기록 이관+원본 정리) / 카탈로그 연결 현황판+[테스트] |
| `EquipPlaceholderEditor.cs` | 부착점 인스펙터 | 메시 글라이드(놓으면 refDist 재베이크, Alt+휠 앞뒤 표면, Esc 취소) / [고스트 재조정] 진입 버튼 / 라이브 미리보기 / Size Ratio 직편집 / refDist 와이어 원 시각화 |
| `EquipSocketEditor.cs` | 소켓 인스펙터 | slotId 리네임 동기화(GO명+카탈로그) / 임시명 경고 / 부착점 상태 안내·목록 / [고스트 재조정] 진입 버튼 |
| `EquipSocketControllerEditor.cs` | 컨트롤러 인스펙터 | 캐릭터 소켓 현황 + [origin 소켓 생성] |
| `EquipPlacementRecordEditor.cs` | 기록 인스펙터 | [기록 재현] — 기록 TRS 복원 미리보기 (저장 안 됨) |
| `EquipCatalogEditor.cs` | 카탈로그 인스펙터 | 해석 사다리 3단 구조 안내 헤더 |
| `EquipPropagationWindow.cs` / `EquipSlotStamper.cs` | Donor 전파 창/로직 | 드라이런 리포트 / `RunDonorBatch` / 손보정 보호 |
| `EquipMeshRaycaster.cs` | CPU 수동 스키닝 레이캐스터 | `RaycastCursor`/`RaycastAll`/`QueryDominantBone`/`HasCache`/`Invalidate` — BakeMesh 없이 스케일 안전 |
| `EquipPhysicsBoneFilter.cs` | 물리 본 필터 | 머리카락 클릭 시 물리 본 대신 head로 승격시키는 판정 |
| `EquipAuthoringUtil.cs` | 저작 공용 유틸 | `ResolveCharRoot`/`MeasureBounds`/`MeasureCharHeight`/`FindSocketBySlotId`/`LossyAvg` 등 |
| `EquipWorkbenchSceneBuilder.cs` | 워크벤치 씬 빌더 | `BuildScene()` — 메뉴/batchmode 겸용 |

## 4. 자주 하는 질문

- **장착이 안 돼요** → 콘솔/워크벤치 로그의 사유를 보세요. "후보 소켓 모두 없음" = 캐릭터에 그 이름의
  소켓이 없음(만들면 됨). "refDist 미베이크" = 부착점을 메시 글라이드로 한 번 움직이거나 refDist 직접 입력.
- **카탈로그가 안 잡혀요** → 파일명이 `EquipCatalog`이고 `Resources/` 안에 있어야 자동 로드됩니다. 1개만 유지.
- **아이템마다 캐릭터별 위치를 다르게** → 그 캐릭터에 **key와 같은 이름의 소켓**을 만들면 최우선(사다리 1순위)으로 이깁니다.
- **프리팹 모드에서 Game 뷰에 안 보여요** → Unity 격리 규칙(정상). Scene 뷰로 확인하거나 씬 인스턴스에서 작업.

## 5. 작업 이력/로드맵

`WORKLOG.md` 참조 (캡슐 철거 최종보고, P3 전파 재구축 재료 포함).
