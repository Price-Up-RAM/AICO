# EquipSystem — "클릭=소켓, 캡슐 폐지" 판정문

> 2026-07-09. 워크플로우 산출물(설계→그릴→판정, 코드 3차 스팟체크 포함).
> 사용자 통찰: 레이 첫 충돌지점 = 소켓. 캡슐/콜라이더 폐지 여부에 대한 공식 판정.

---

## ✅ 사용자 확정 사항 (2026-07-09 — §6 질문에 대한 답, 본문보다 우선)

1. **P1 착수**: 보류 — 본 판정문 검토 후 지시. (sizeScale 포함 여부도 그때 결정)
2. **호환 정책**: 신규 카탈로그 엔트리를 spot 없는 구 캐릭터에 장착 시 **즉시 거부+경고** (2차 시도 없음 — 추천안보다 엄격, 예측 가능성 우선).
3. **리네임 UX**: 생성 직후 slotId 필드 포커스 + 미리네임 경고 배지 (모달 강제 없음).
4. **캡슐 과도기**: 클릭 생성 시 **기본 미생성 + 토글 잔존** (P4에서 토글 삭제).

---

# 클릭=소켓 판정

> 판정 근거: 설계 문서 + 그릴 결과 + 본 판정에서의 3차 스팟체크(`EquipMeshRaycaster.cs:161-165` 노멀 강제 플립, `EquipPlacement.cs:28-59` rWorld 단일 소비·fitBias 폭발 경로, `EquipSocketEditor.cs:73-79/218/252` 캡슐 경고·직부착 미리보기, `EquipManager.cs:73-96` 레거시 폴백 — 전부 원문 일치 확인).

## 1. 사용자 이해 확인

**맞는 부분.** "레이 첫 충돌지점에 소켓을 만든다 → 버튼 하나 → 클릭 → socket_1 자동 생성 → 리네임"은 코드 사실과 정합하며 즉시 구현 가능하다. 픽 파이프라인(`EquipSocketAuthorWindow.CreateSocketAtHit`)은 원래 slotId를 인자로 받는 범용 함수라 표준 슬롯 하드코딩이 애초에 없고, 버튼 하나 추가는 `StartPickQueue(["socket_" + n])` 한 줄이다. 캡슐 폐지도 참이다: placeholder 장착 경로에서 캡슐이 하는 일은 `rWorld = cap.radius × LossyAvg` 단 한 줄(`EquipPlacement.cs:31`)뿐이고, 클릭 순간의 본→히트점 거리를 float 하나(`bakedRefDistLocal`)로 구워두면 크기 기준과 positionOffsetRadii 환산이 동시에 대체된다. 콜라이더 저작, 캡슐 핸들 조작, 표준 슬롯 사전 선택(head→chest→back 강제 시퀀스) — 신규 저작에서 전부 사라진다. 이 통찰은 SPEC의 M2(런타임 전환)+M4(캡슐 미생성)를 앞당겨 "캡슐-프라이머리 단계를 건너뛰자"는 것과 동치이며, 판정은 **채택**이다.

**보정 필요한 부분.** 네 가지다. ① "여태 했던 게 다 필요 없어진다"는 절반만 참 — 그 클릭 한 번이 성립하는 이유가 바로 여태 만든 CPU 스키닝 레이캐스터, 지배 본 승격 사다리(머리카락을 클릭해도 head에 붙는 것), 호버 미리보기 픽 모드, 글라이드다. 죽는 것은 **캡슐이라는 중간 프록시**와 **슬롯 사전 선택**뿐이고, M1 자산은 새 모델의 몸통으로 전량 생존한다. ② "이름만 바꾸면 됨"의 이름 짓기는 소멸하지 않는다 — slotId는 카탈로그 매칭과 캐릭터 간 전파의 열쇠라서, socket_1을 "ribbon"으로 바꾸는 건 같은 어휘 작업의 재배치다(다만 강제 표준에서 자동완성 제안으로 격하되어 자유로워진다). 게다가 리네임은 기존 카탈로그 엔트리를 조용히 끊을 수 있어 참조 안전망이 필요하다. ③ 캡슐 **코드**는 즉시 삭제 불가 — 출하된 데모 카탈로그 4엔트리 전부와 기존 저작 캐릭터가 캡슐 소비 경로의 현역 사용자다. 삭제는 백필 후에만 안전하다. ④ 캡슐에는 반경 외에 숨은 역할이 하나 더 있었다: "이 부위 크기 일괄 조절 노브 + 한 소켓 위 페어 악세서리 동일 크기 보장". refDist는 부착점마다 따로라 이 기능이 공백이 되며, 소켓 `sizeScale` float 하나로 복원해야 한다.

## 2. 기존 방식 비교표

| 축 | 캡슐-프라이머리 (현행 M1) | 클릭=소켓 + refDist (신규) |
|---|---|---|
| 저작 조작 수 | 슬롯 선택 → 클릭 → 캡슐 생성/유도(3×hitDist) → (필요시) 캡슐 핸들 튜닝 → 글라이드 | **[+소켓] → 클릭 → 리네임** (+선택적 글라이드). 콜라이더 조작 0회 |
| 크기 기준 | 캡슐 radius×LossyAvg (콜라이더 1개 유지 비용) | `bakedRefDistLocal` float 1개 — 생성 시점 수치는 캡슐과 등가(radius≈hitDist) |
| 크기 가시성 | 캡슐 기즈모가 잘못된 크기를 즉시 폭로 | **열세** — float는 안 보임. refDist 와이어디스크 기즈모+키 대비 % 표기로 상속 필수 (그릴 4) |
| 부위 일괄 크기 노브 | radius 하나로 페어/부위 전체 조절 | **공백** — 소켓 `sizeScale` 신설로 복원 (그릴 6) |
| 전파 | 캡슐 비율 스탬프 (단, 현행은 placeholder를 아예 전파 못 함 — 기존 구멍) | 대상 자신의 메시에 레이 → 실측 refDist (대상 체형에 자동 적응). 단 **레이 기하 2건 수정 전 출하 불가**: 본 내부 출발 노멀 반전, 최근접 히트=두피 (그릴 1·2) |
| 레거시 안전 | 캡슐 없는 소켓 = scale 1×fitBias → 루트 20000 캐릭터에서 폭발 (SPEC 공격 6) | 거부 가드로 폭발을 **장착 거부+경고**로 전환 — `Equip()`과 `Fit()`(에디터 미리보기) 양쪽 필수 (그릴 9) |
| 개념 수 (사용자 노출) | 슬롯 4종 + 소켓 + 캡슐 + placeholder + 글라이드 스냅 모드 | **소켓(=이름 하나) + 클릭점**. placeholder는 "spot" 고정 규약으로 비노출, 캡슐 소멸 |

## 3. 최종 아키텍처 결정

**안 B — 플랫 *주소* + 2계층 *GO 구조* 채택** (설계·그릴 합의, 반대 논거 없음).

```
Bone
└─ Socket_ribbon (EquipSocket, 본 원점·루트 프레임)   ← 사용자가 만지는 유일한 이름(slotId)
     └─ PH_spot (EquipPlaceholder, 클릭점)             ← id="spot" 고정 규약, 비노출
          bakedRefDistLocal (신규 float, 부모-로컬)     ← 메시 히트에서만 (재)베이크
카탈로그: targetSlotId만. 런타임이 "spot"을 자동 라우팅.
```

근거: (1) 진짜 플랫(안 A)의 추가 이득은 "씬에 GO 1개 덜"뿐인데 비용은 런타임 전면 개조+글라이드 에디터 이전+**기존 저작물 전량 마이그레이션**. 안 B는 같은 개념 수 이득(placeholderId가 상수가 된 순간 2계층은 사용자 머릿속에서 소멸)을 diff ~45줄, 마이그레이션 0건으로 얻는다. (2) 소켓 GO는 캡슐이 죽어도 "노멀 따라 기우는 클릭점"과 별개의 **안정 기준 프레임**으로 남는다 — SocketFrame orientation(천사링)이 소비. (3) 주소가 이미 플랫이므로 물리 병합은 나중에 언제든 기계적으로 가능.

그릴 수정 4건을 아키텍처에 확정 반영한다: **spot 자동 라우팅은 캡슐 없는 소켓에서만**(레거시 튜닝 조용한 전환 차단, 그릴 8) · **글라이드 확정 재베이크도 캡슐 없는 소켓에서만**(백필 규약 `refDist=cap.radius`와의 모순 해소, 그릴 3) · 재베이크 직후 `RefitPreview` 1회(WYSIWYG 보존) · **origin 소켓은 클릭 대신 `refDist = 0.175×키` 합성 베이크**로 캡슐 프리 성립(그릴 7). 크기 사다리 최종형: `refDist 우선 → 캡슐 폴백 → 둘 다 없으면 거부`, 결과에 소켓 `sizeScale`(신설) 곱.

## 4. 사망·생존 목록

"여태 했던 캡슐이니 collider니 예시니 다 필요 없어지는 것 아닌가?"에 대한 정면 답 — **저작 도구로서의 캡슐은 죽지만, 파일은 하나도 지울 수 없다.**

| 대상 | 운명 | 비고 |
|---|---|---|
| 캡슐 = 신규 저작의 표면/크기 프록시 | **사망** | refDist float 1개로 대체. 단 가시화 기즈모는 상속 |
| CapsuleCollider 저작 (생성 버튼·핸들 튜닝·표준 시드 top/side/halo) | **사망 예정** | P1에서 경고 HelpBox·무동작 버튼 침묵 필수, 삭제는 P4 |
| 표준 슬롯 사전 선택 (head→chest→back 강제) | **사망** | "표준 세트" 프리셋+제안 칩으로 격하 |
| `targetPlaceholderId` (카탈로그 필드) | **상수화 사망** | "spot" 규약. 구 캐릭터 폴백 사다리 필요 |
| 캡슐 = 부위 크기 일괄 노브 | **사망 + 대체 필수** | 소켓 `sizeScale` 신설 전까지 기능 공백 |
| 기존 캐릭터·데모의 캡슐 **데이터** | **생존 (동결)** | rWorld 사다리 폴백의 현역 소비자. 백필 후에만 삭제 가능 |
| 데모 카탈로그 4엔트리 (`EquipCatalog_Demo.asset`) | **생존 (동결 무수정)** | 전부 레거시 직부착 경로 현역 |
| `EquipCapsuleMath.cs` | **생존** | `LossyAvg`가 런타임 핵심 공용. Encode/Decode는 폴백 전용 강등 |
| `EquipFitter.cs` | **생존** | MeasureNatural/ComputeFitScale은 캡슐 무관 현역 |
| 픽 모드·호버 미리보기·`QueryDominantBone` 물리 승격 사다리·글라이드·`EquipMeshRaycaster` | **생존 — 새 모델의 몸통** | 제안 ①이 이 자산 위에서만 성립 |
| `ResolveBone` 사다리·KEEP_MANUAL/TUNED·배치 IO·`EquipPhysicsBoneFilter` | **생존 무수정** | 전파의 뼈대 |
| slotId 이름 짓기 | **불사** | 강제→제안으로 완화될 뿐 소멸 불가 |
| **즉시 삭제 가능한 파일** | **없음** | 캡슐 코드 전부가 출하 데이터의 소비자 |

## 5. 수정된 로드맵

기존 SPEC 매핑: 이 계획 = **M2 런타임 전환 + M4 캡슐 미생성을 앞당기고, M4의 전제(공격 6 해체)를 P1에서 선해결, M3는 축소판으로 P3에 흡수**(단 레이 기하 2건 수정 조건부).

**P1 — 런타임 안전화 + 클릭 저작 (다음에 구현할 최소 단위, ~1작업)**
- `EquipPlaceholder`: `bakedRefDistLocal` 필드 추가.
- `EquipPlacement`: rWorld 사다리(refDist→캡슐), ContainUniform+캡슐無+refDist有 자동 폴백, 거부 가드 — **`FitToPlaceholder`와 `Fit`(직부착) 양쪽**(그릴 9: 에디터 미리보기 폭발 차단).
- `EquipManager.Equip`: 캡슐 없는 소켓 한정 spot 자동 라우팅(그릴 8) + 레거시 직부착 진입 전 캡슐無 거부.
- `EquipSocketAuthorWindow`: [+소켓] 버튼(socket_N 자동 넘버링) → 기존 픽 파이프라인 → 캡슐 미생성 + refDist 베이크(hitDist≈0 하한 가드, 그릴 11b) → Selection=소켓. origin은 0.175×키 합성 베이크(그릴 7).
- `EquipPlaceholderEditor`: 글라이드 MouseUp 재베이크(**캡슐 없는 소켓만**) + 직후 RefitPreview(그릴 3). refDist 와이어디스크 기즈모 + 키 대비 % 표기(그릴 4).
- `EquipSocketEditor`: refDist 있으면 캡슐 경고/버튼 숨김(그릴 10).

**P2 — 리네임 UX**: slotId↔GO명 자동 동기화, 표준 슬롯명 제안 칩, 카탈로그 참조 카운트+일괄 갱신 버튼, 생성 직후 slotId 필드 하이라이트(그릴 11a), 캐릭터 간 유사 slotId 린트.

**P3 — 전파 개편 (출하 조건 4건 포함)**: def에 `dirRootFrame/distScale/refDistHeightRatio` 인코딩, 대상 메시 레이 전파 + NO_HIT 사다리(반전 레이→키 비율 합성→리포트). **필수 수정**: ① 본 내부 출발 레이의 노멀을 "본에서 멀어지는 방향"으로 플립(그릴 1 — `EquipMeshRaycaster:162-165`의 레이 대면 플립은 커서 전용) ② 바깥 방향 레이는 **최원(最遠) 히트** 채택(그릴 2 — 두피/헤어 다층의 주류 케이스) ③ `EquipSocketEditor`에 소켓 단위 [이 본을 별칭으로 학습] 신설(그릴 5 — 이것 없이는 비표준 전파 0건 성립) ④ 스탬퍼 비표준 하드블록을 "크로스 힌트 보유 def 허용"으로 완화. + Donor 모드 placeholder 복사 신설(현행은 부착점을 아예 전파 못 하는 기존 구멍).

**P4 — 가지치기**: 백필 메뉴(`refDist = cap.radius`, radiusScale 미곱 — sizeRatio 튜닝 보존) → 데모 4엔트리 재저작 → 캡슐 UI/코드 삭제.

## 6. 사용자 결정 질문

1. **소켓 `sizeScale` 노브를 P1에 넣을까?** (추천: **예** — float 1개, rWorld에 곱. 페어 악세서리 좌우 크기 보정과 "부위 크기 일괄 조절"을 동시에 복원하며, 예전에 원했던 기능이다. 미루면 캡슐 폐지 직후 기능 퇴행을 체감하게 된다.)
2. **신규 플랫 카탈로그 엔트리를 구 캐릭터(top/side_l만 있고 spot 없음)에 장착하면?** (추천: **spot miss 시 "top" 2차 시도, 그것도 없으면 거부** — 조용한 레거시 직부착 낙하보다 예측 가능. 대안: 즉시 거부+경고.)
3. **socket_N 리네임을 어느 강도로 유도할까?** (추천: **생성 직후 slotId 필드 포커스+미리네임 경고 배지**까지만 — 모달 강제는 1인 저작 흐름을 끊는다. socket_1 방치가 캐릭터 간 어휘 오염이라는 점만 인지하면 됨.)
4. **P1에서 클릭 시 캡슐을 아예 안 만들까, 옵션 토글로 남길까?** (추천: **기본 미생성 + 폴드아웃 토글 잔존** — 전파(P3)가 완성되기 전 구캐릭터와 섞어 쓸 과도기의 보험. P4에서 토글 삭제.)

관련 파일: `d:\unity\AICO\Assets\Prefabs\Assist\EquipSystem\Scripts\EquipPlacement.cs`, `Scripts\EquipManager.cs`, `Scripts\EquipPlaceholder.cs`, `Scripts\EquipCapsuleMath.cs`, `Editor\EquipSocketAuthorWindow.cs`, `Editor\EquipPlaceholderEditor.cs`, `Editor\EquipSocketEditor.cs`, `Editor\EquipSlotStamper.cs`, `Editor\EquipMeshRaycaster.cs`, `Resources\EquipCatalog_Demo.asset`, `SPEC_MeshFirst.md`