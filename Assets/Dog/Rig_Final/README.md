# 쿠키(cookie) 강아지 — 애니메이션 적용 완료본 / 인수인계

AI 생성 강아지 메쉬("쿠키")에 애니메이션을 붙여 Unity에서 재생되는 상태까지 만든 결과물입니다.
이 폴더 하나만 옮기면 되고, 다른 폴더에 의존하지 않습니다.

- **Unity 6000.3.15f1** / Built-in Render Pipeline (기본 3D 템플릿)
- 클립 **10개**, 본 39개, 메쉬 9,146정점 (Unity 임포트 후 14,754 — UV/노멀 분리)
- 실측 몸 크기: 길이 약 1.0 m, 키 약 0.62 m

---

## 1. 바로 확인하기

1. Unity Hub에서 `UnityProject` 폴더를 **Add project from disk** 로 추가하고 엽니다.
   (첫 실행 시 `Library` 재생성으로 1분 정도 걸립니다 — 정상입니다.)
2. `Assets/DogDemo.unity` 씬을 열고 **Play**.
3. 좌측 UI에서
   - **반복 재생**: `bWalk2` 를 켜면 무한히 걷습니다 (권장). `bWalk` 는 원본 클립.
   - **1회 재생**: `tJump`, `tSit1`, `tSit2`, `tUnique1/4/5`, `tDefault`(즉시 Idle 복귀)
   - 회전 / 재생속도 슬라이더, 현재 클립 표시

문제가 있어 보이면 메뉴 **`Dog > 3. Verify`** 를 실행하세요. GPU 없이 수치로 검증하고 Console에 결과를 출력합니다 (RDP·헤드리스 환경에서도 동작).

---

## 2. 들어있는 것

```
final/
├── README.md                     ← 이 문서
├── UnityProject/
│   ├── Assets/
│   │   ├── DogDemo.unity                     데모 씬
│   │   ├── Dog/
│   │   │   ├── cookie_all.fbx                메쉬 + 39본 리그 + 클립 10개
│   │   │   ├── cookie_all.fbm/               텍스처 3장 (basecolor 4096², normal, 보조)
│   │   │   ├── Cookie_AnimCtrl.controller    Animator Controller
│   │   │   └── Materials/                    자동 생성된 머티리얼
│   │   ├── Scripts/DogAnimDemo.cs            데모용 런타임 UI (제품에는 넣지 않음)
│   │   └── Editor/
│   │       ├── DogSetup.cs                   임포트 설정 + 컨트롤러 + 씬을 코드로 생성
│   │       └── DogVerify.cs                  GPU 없이 수치 검증
│   ├── Packages/  ProjectSettings/
└── blender_pipeline/             FBX를 다시 만들거나 품질을 조정할 때 쓰는 스크립트
```

**임포트 설정과 컨트롤러는 손으로 클릭해서 만든 게 아니라 `DogSetup.cs` 가 코드로 생성합니다.**
설정이 틀어졌거나 FBX를 교체했으면 메뉴 `Dog > 1. Reimport + Rebuild Controller` 를 누르면 원래 상태로 복원됩니다. 씬을 다시 만들려면 `Dog > 2. Build Demo Scene`.

---

## 3. 연동 규격 (Animator)

Animator Controller 의 파라미터와 스테이트 이름은 **기존 `Pet_Dog_Latte_01_AnimCtrl` 과 동일하게 맞춰뒀습니다.** 따라서 AICO 의 `DogAnimationController.cs` 를 **수정 없이 그대로** 붙일 수 있습니다.

| 파라미터 | 타입 | 스테이트 | 클립 길이 | 루프 |
|---|---|---|---|---|
| `bWalk` | bool | `Walk` | 2.57s | ON |
| `bWalk2` | bool | `Walk2` | **0.73s** | ON |
| `tJump` | trigger | `Jump` | 0.93s | off |
| `tSit1` | trigger | `Sit_01` | 3.60s | off |
| `tSit2` | trigger | `Sit_02` | 3.40s | off |
| `tUnique1` | trigger | `Unique_01` | 2.93s | off |
| `tUnique4` | trigger | `Unique_04` | 2.70s | off |
| `tUnique5` | trigger | `Unique_05` | 2.47s | off |
| `tDefault` | trigger | `Idle_01` 로 즉시 복귀 | 4.00s | ON |
| (기본 상태) | — | `Idle_01` | 4.00s | ON |
| (미사용) | — | `Idle_02` | 3.77s | ON |

`tUnique2` / `tUnique3` 는 기존 컨트롤러와 파라미터 목록을 맞추기 위해 **선언만 해두고 배선하지 않았습니다** (원본에도 대응 스테이트가 없었습니다). 스크립트에서 호출해도 경고가 나지 않습니다.

### `bWalk` 와 `bWalk2` 의 차이 — 반드시 `bWalk2` 를 쓰세요

원본 Latte Walk 클립(78프레임)을 분해해보면:

| 구간 | 다리 움직임 | 꼬리 움직임 | 내용 |
|---|---|---|---|
| f1–f27 | **0.0°** | 63.7° | 서서 꼬리만 흔듦 |
| f28–f49 | 148.4° | — | **실제 보행** |
| f50–f78 | **0.0°** | 64.6° | 다시 서서 꼬리만 흔듦 |

**클립 길이의 72%가 정지 구간**이라 그냥 루프하면 "꼬리 흔들고 조금 걷고 멈춤"이 반복됩니다.
`Walk2`(`cookie_WalkCycle`) 는 실제 보행 구간만 잘라낸 23프레임 / 0.73초 클립이고, 이음새 오차 **0.04°** 로 첫 프레임과 마지막 프레임이 사실상 동일합니다. 네 발이 각각 정확히 1회 앞뒤 스윙하고 대각(diagonal) 위상이 맞는 완전한 1주기임을 검증했습니다.

`bWalk` 는 원본 비교용으로만 남겨뒀습니다.

---

## 4. ⚠️ AICO 로 옮길 때 반드시 수정해야 하는 것

이 4가지를 놓치면 **눈에 보이는 버그로 재발합니다.**

### 4-1. `QualitySettings.skinWeights` 를 `Unlimited` 로 (가장 중요)

**Project Settings > Quality > 사용 중인 레벨 > Skin Weights = Unlimited**

Unity 기본 품질 레벨별 `skinWeights` 는 `Very Low:1 / Low:2 / Medium:2 / High:2 / Very High:4 / Ultra:255` 입니다.
이 모델은 웨이트 스무딩 때문에 정점당 본이 최대 8개인데, 런타임에서 2본으로 잘리면 **목덜미·겨드랑이가 뾰족하게 튀어나오는 현상이 그대로 재발합니다.**

측정값 (엣지가 레스트 대비 몇 배로 늘어나는지):

| 정점당 본 제한 | Sit_02 | Walk |
|---|---|---|
| 4본 | 6.72× | 8.84× |
| **8본** | **4.53×** | **4.81×** |

### 4-2. FBX 임포터 `Max Bones/Vertex = 8`

`cookie_all.fbx` 선택 → Inspector → **Rig** 탭 → `Skin Weights: Custom`, `Max Bones/Vertex: 8`.
이 폴더의 `.meta` 에 이미 들어있으므로 `.meta` 를 함께 복사하면 유지됩니다. 새로 임포트한다면 `Dog > 1. Reimport` 를 실행하세요.

Unity 기본값 `Standard` 는 정점당 본을 **4개로 자르고 재정규화**합니다. 잘렸는지는 `Dog > 3. Verify` 가 `>4본 정점` 개수로 알려줍니다 (정상: 2,697개).

### 4-3. 머티리얼 셰이더를 **URP/Lit** 으로 변경

이 프로젝트는 Built-in RP 기본 템플릿이라 셰이더가 `Standard` 입니다.
AICO 는 URP이므로 그대로 넣으면 **반투명하게 번들거리며 깨집니다.** `Assets/Dog/Materials/` 의 머티리얼을 URP/Lit 으로 바꾸고 Base Map / Normal Map 을 다시 연결하세요.

### 4-4. `DogAnimDemo.cs` 는 제품에 넣지 마세요

데모 확인용 `OnGUI` UI 입니다. 제품에서는 이 스크립트를 제거하고 AICO 의 `DogAnimationController` + `DogTestInput` 을 붙이면 됩니다 (파라미터 이름이 동일).

주의: `anim.Play("Walk")` 만 호출하면 `IfNot bWalk` 전이가 즉시 발동해 Idle 로 되돌아갑니다. **`SetBool("bWalk2", true)` 가 선행돼야** 합니다.

---

## 5. 알려진 한계 (남아있는 문제)

솔직하게 적습니다. 치명적인 건 없지만 눈에 띌 수 있는 수준입니다.

### 5-1. 목덜미 / 겨드랑이 메쉬가 약간 뾰족해짐

8본 설정에서도 Sit_02 에 4.53×, Walk 에 4.81× 로 늘어나는 엣지가 남습니다 (각각 49개 / 104개).

원인은 **웨이트 오염**이며 애니메이션 문제가 아닙니다. 애니메이션 없이 본 하나만 30° 돌려도 재현됩니다:

| 돌린 본 | 최대 늘어남 | 문제 엣지의 지배본 |
|---|---|---|
| `Bip001 Head` | 4.0× | `Ear_L01` ↔ `Bip001 Neck` (목덜미) |
| `Bip001 Neck` | 3.2× | `Bip001 R Forearm` ↔ `Neck` (겨드랑이) |
| `Bip001 R Clavicle` | 3.1× | `Bip001 R Forearm` (겨드랑이) |

즉 **귀 본이 목덜미 정점을, 앞발 `Forearm` 본이 겨드랑이·가슴 정점을 잡고 있습니다.**
스무딩으로 원본 대비 크게 줄였지만(Sit_02 16.80× → 4.53×) 완전히 없애려면 **그 두 부위를 Blender에서 손으로 웨이트 페인팅**해야 합니다. 기계적 처방은 여기가 한계입니다(§7 참조).

재현/측정: `blender_pipeline/isolate_single_bone.py`, `measure_stretch.py`

### 5-2. 걷기의 수직 흔들림 약 4.6 cm

쿠키의 아랫다리가 원본(Latte)보다 2.6배 길어서, 스윙하는 발이 과도하게 뻗으며 몸이 위아래로 약 4.6 cm 움직입니다 (키 0.62 m 기준 7%). 접지 보정은 클립당 상수 오프셋만 적용했습니다 — 프레임별 보정은 시도했다가 오히려 10.9 cm 로 악화돼서 되돌렸습니다.

### 5-3. 귀 / 얼굴 애니메이션 없음

귀·눈·입 본은 리타겟 대상에서 제외했습니다(FBX 임포트 시 생기는 리프 본 아티팩트와 구분이 어려움). 눈 전용 본의 웨이트는 `Bip001 Head` 로 이관해 머리와 함께 움직입니다.

### 5-4. Tripo 오토리깅 경로는 보류 상태

Tripo Studio 로 오토리깅을 시도했으나 **내보낸 파일에 스킨 웨이트와 애니메이션이 담기지 않았습니다.** GLB의 glTF 접근자 메타데이터가 `WEIGHTS_0 max=[1.0, 0, 0, 0]`, 즉 14,746 정점 전부가 본 1개에 웨이트 1.0 으로 묶여 있고 그 중 99.7%가 루트 본입니다. 애니메이션을 걸면 강아지가 통째로 강체처럼 움직입니다. 무료 플랜 내보내기 제한으로 추정되며, 애니메이션 포함 재출력이 필요합니다.

참고로 Tripo 의 4족 애니메이션 프리셋은 **`걷기` 하나뿐**이라 idle/sit 은 얻을 수 없습니다.

---

## 6. FBX를 다시 만들거나 품질을 조정하려면

`blender_pipeline/` 의 스크립트는 **Blender 5.1 헤드리스**로 동작합니다(GPU 불필요).

```
blender --background --factory-startup --python build_cookie_fbx.py -- <dog3.fbx> <Latte폴더> <출력.fbx>
```

**이 스크립트들은 데모 실행에 필요하지 않습니다.** 아래 원본 파일이 있어야 동작하며, 그 파일들은 이 폴더에 포함하지 않았습니다 (AICO 의 `Assets/Dog/` 에 있습니다).

| 필요한 입력 | 역할 |
|---|---|
| `dog3.fbx` | 쿠키 메쉬 + 메쉬에 정렬된 39본 리그/웨이트 (리타겟 대상) |
| `Woongjin/Models/Pet/Pet_Dog_Latte_01@*.FBX` | Latte 애니메이션 소스 9개 |

### 파이프라인이 하는 일

1. **웨이트 정리** — `Eye_L`/`Eye_R` 웨이트를 `Bip001 Head` 로 이관 + 이웃 평균 스무딩 4회(λ=0.5)
2. **델타 리타겟** — Latte 클립에서 "레스트 대비 회전 변화량"만 뽑아 쿠키 레스트 포즈에 적용

   ```
   delta = src_pose_rot · src_rest_rot⁻¹          (armature space)
   goal  = delta · cookie_rest_rot
   basis = (rest_parent⁻¹·rest_bone)⁻¹ · (goal_parent⁻¹·goal_bone)
   ```

   **쿠키의 레스트 포즈를 절대 수정하지 않는 것이 핵심입니다.** 소스와 타겟의 레스트 오리엔테이션 차이(중앙값 8.5° / 최대 41.4°)를 리타겟이 흡수합니다. 충실도 검증 결과 소스 대비 최대 오차 **0.283°**.
3. **접지 보정** — 클립당 상수 오프셋 하나 (메쉬 최저 Z 의 중앙값을 0 으로)
4. **`cookie_WalkCycle` 생성** — Walk 의 f27–f49 만 잘라냄
5. **FBX 내보내기** — `apply_unit_scale=True`(Unity 에서 루트 스케일 1.0 로 들어옴), 텍스처는 `.fbm` 사이드카로 (내장하면 Unity 가 자동 추출하지 않아 흰색으로 렌더됨)

### 품질을 더 조정하려면

- 스무딩 횟수는 `build_cookie_fbx.py` 의 `SMOOTH_ITERS` (현재 4). **올릴 때 주의**: 정점당 본 개수가 늘어나 임포터 제한에 더 많이 걸립니다. 4본 제한 하에서는 4회 이상 올리면 Walk 가 오히려 나빠집니다.
- 루프 구간은 `WALK_LOOP = (27, 49)`. 다시 찾으려면 `find_walk_loop.py`.
- 수정 후에는 반드시 `measure_stretch.py` 로 재측정하고, Unity 에서 `Dog > 3. Verify` 를 실행하세요.

---

## 7. 하지 말 것 (이미 실패가 확인된 접근)

같은 실수를 반복하지 않도록 남깁니다. 전부 실측으로 악화가 확인됐습니다.

| 시도 | 결과 |
|---|---|
| **본 길이·비율을 소스(Latte)에 맞추기** | 리그가 메쉬 밖으로 벗어남. 쿠키는 허벅지가 짧고 아랫다리가 긴 체형(Calf 가 Latte 의 2.6배)인데 Latte 비율(허벅지=종아리의 1.9배)을 강제해서 정렬이 깨졌음 |
| **거리 기준 웨이트 삭제(prune)** | Sit_02 17.1× → **35.2×** 악화 |
| **거리 기준 최근접 본 이관(transfer)** | Sit_02 → **13.09×** 악화 (말단 본만 한정해도 12.96×) |
| **프레임별 접지 고정** | Walk 흔들림 5 cm → **10.9 cm** 악화. 스윙하는 발이 과도하게 뻗는 프레임에 몸 전체를 들어올림 |

앞의 세 가지가 실패한 공통 원인: **FBX로 임포트한 본은 tail 이 노드 축을 향하고, 척추처럼 자식이 여러 방향으로 뻗는 본은 "본까지의 거리" 추정이 부정확**합니다. 그 거리를 근거로 웨이트를 재배치하면 인접 정점 사이에 새 불연속이 생겨 더 찢어집니다.
**스파이크의 원인이 불연속일 때는 재배치가 아니라 스무딩이 답입니다.**

---

## 8. 작업 환경 관련 함정

다음 사람이 이어받을 때 시간을 아끼도록 적어둡니다.

- **Blender 4.4+ 슬롯 액션**: `Action` 을 연결해도 `animation_data.action_slot` 을 바인딩하지 않으면 키프레임이 전부 있는데도 포즈가 **전혀** 안 잡힙니다. 스크립트에서 반드시 슬롯을 잡아주세요. `Action.fcurves` 와 `Action.id_root` 는 5.x 에서 제거됐습니다(→ `action.layers[].strips[].channelbags[].fcurves`). FBX 임포트 오퍼레이터도 `bpy.ops.wm.fbx_import` 로 교체됐습니다.
- **Unity `clipAnimations` 고정**: `ModelImporter.clipAnimations` 를 한 번 명시 지정하면 Unity 가 그 목록에 고정되어 **FBX에 새 테이크가 생겨도 무시합니다.** `defaultClipAnimations` 로 다시 읽어야 합니다 (`DogSetup.cs` 가 그렇게 합니다).
- **Animator Controller 를 지우고 다시 만들면 GUID 가 바뀌어** 씬/프리팹의 `Animator.Controller` 참조가 조용히 끊깁니다. `DogSetup.cs` 는 에셋을 유지한 채 내용만 비우고 재구성합니다.
- **GPU 없는 환경(RDP 등)**에서는 Blender 의 Workbench/EEVEE 렌더가 크래시합니다. `DogVerify.cs` 처럼 `Animator.Update` + `SkinnedMeshRenderer.BakeMesh` 로 수치 검증하면 렌더링 없이 판정할 수 있습니다.
- **Unity 는 glTF/GLB 를 기본 지원하지 않습니다.** 필요하면 glTFast 나 UnityGLTF 패키지를 추가해야 합니다.
