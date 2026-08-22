# cookie 강아지 리깅/애니메이션 작업 정리

프로젝트: AICO (`C:\UnityProject\AICO`)
모델: `Assets/Dog/cookie and cream oreo milk 1.fbx` (Blender + Rigify Wolf 메타리그 기반)

## 1. Blender 리깅 완료 (해결됨)

**증상:** Rigify "릭 재생성" 시 `Bone 'spine.003': Cannot connect chain - bone position is disjoint` 에러 반복.

**긴 원인 추적 과정:**
- spine.003의 부모가 spine.012로 잘못 지정된 걸 발견 → Dissolve로 spine.012 제거, 부모를 spine.004로 재연결
- 이후 spine.009에서 같은 에러 재발
- 순정 Wolf 메타리그와 좌표를 직접 대조한 끝에 **진짜 원인 확정: `metarig` 오브젝트 자체의 Scale이 0.477로 걸려 있었던 것**

**해결:** `metarig` 선택 → `Ctrl+A` → Scale 적용(오브젝트 스케일 1.0으로 정규화) → 릭 재생성 성공.

**교훈:** disjoint 에러가 여러 본을 옮겨 다니며 계속 뜨면, 본 좌표를 하나씩 스냅하기 전에 **오브젝트 자체의 Scale이 1.0인지부터 확인**할 것.

## 2. Unity 웨이트 페인팅 연결 문제 (해결됨)

**증상:** 릭 생성 후 Pose Mode에서 본을 움직여도 메쉬가 안 따라옴.

**원인 1:** `Dog` 메쉬의 Armature 모디파이어 Object 필드가 `rig`가 아니라 `metarig`를 가리키고 있었음 → `rig`로 수정.

**원인 2:** Vertex Group 이름이 `DEF-f_index.02.R`, `DEF-breast.R` 등 사람 손가락/가슴 이름으로 남아있었음. 이는 버그가 아니라 **Wolf 메타리그 자체가 발가락 마디·얼굴 세부까지 정교하게 설계된 리그**였기 때문(강아지 앞발 스크린샷으로 확인 — 발가락 마디가 실제로 있음).

**교훈:** 웨이트가 안 먹히면 (1) Armature 모디파이어 Object 필드, (2) Vertex Group 이름과 실제 rig의 DEF- 본 이름 일치 여부, 이 두 가지부터 확인.

## 3. Unity FBX 익스포트 / 렌더링 (해결됨)

- FBX Export 시 **경로 모드: 복사 + 내장된 텍스처** 옵션으로 텍스처까지 하나의 파일에 포함시킴
- WGT- 위젯 메쉬(Rigify 컨트롤러 도형)는 노멀 계산 경고를 180개나 유발하므로, Export 시 **"제한 > 선택된 오브젝트"** 체크 후 `Dog`+`rig`만 선택해서 내보내면 경고가 사라짐
- Unity에서 렌더링이 반투명/번들거리게 깨졌던 원인은 **머티리얼 셰이더가 URP 프로젝트인데 Standard로 잡혀있었기 때문** → URP/Lit으로 변경하여 해결

## 4. 애니메이션 확보 문제 (진행 중 — 다음에 이어갈 부분)

**핵심 문제:** `cookie and cream oreo milk 1` 모델은 Animator 컴포넌트는 있지만 Controller가 비어있고(`m_Controller: {fileID: 0}`), 애니메이션 클립이 전혀 없음. Rigify는 리그(뼈대) 생성기일 뿐 애니메이션은 제공하지 않음.

**프로젝트에 이미 있는 애니메이션 자산 비교 (본 이름/개수 실측):**

| 리그 | 대략 본 개수 | 본 이름 예시 | 파라미터 체계 |
|---|---|---|---|
| **cookie (Rigify, 타겟)** | 약 197개 (DEF- 접두사만) | `DEF-spine.003`, `DEF-tail.001` | - |
| **Latte** (`Assets/Dog/Woongjin`, 3ds Max Biped) | 약 20~30개 | `Bip001 Spine`, `Tail_01` | `bWalk`(bool) + `tDefault/tJump/tSit1/tSit2/tUnique1~5`(trigger) |
| **LowPoly** (`Assets/Dog/Low Poly Animated Animals`, GoldenRetriever/Chihuahua/GreatDane/Fox) | 목 4마디, 발가락 16개 등 더 세분화 | `Spine1_M`, `Neck1~4_M`, `Tail0~4_M`, `frontToes1~4_L/R` | `isWalking/isRunning/isBarking/isSitting`(bool, Fox는 `isAttacking` 추가) |
| **Wolf** (`Assets/Dog/Low Poly Animated Animals`) | - | - | `isHowling/isAttacking/isDead/isRunning/isWalking`(bool) |

세 리그 모두 본 이름 체계가 서로 완전히 달라서 Unity Generic 애니메이션은 그대로 재사용 불가 (본 이름이 정확히 일치해야만 재생됨).

**방향 결정 (재변경, 최신): Latte로 확정.**
- 이전에는 LowPoly 쪽 확장으로 결정했었으나(사유: Latte는 전 회사 애니메이터 수작업이라 확장 불가 막다른 자산이라 판단) 다시 **Latte로 최종 변경**.
- 이유: Wolf 메타리그가 발가락/얼굴 세부까지 정교해서 본 개수가 197개까지 늘어나는 바람에 Latte(20~30본)와 격차가 너무 컸음. Rigify에는 Wolf 말고 **`아마츄어 > 메타-리그 리깅 > 기본 > Basic Quadruped`** 라는 단순화된 4족보행 템플릿이 있고, 이게 Latte의 본 개수/구조에 훨씬 가까워서 리타겟이 쉬울 것으로 판단.

**방향 추가 변경 (최신 확정): Basic Quadruped 신규 제작 대신, Latte의 기존 본(Armature)을 그대로 cookie 메쉬에 이식.**
- 이유: 어차피 Latte 애니메이션 클립을 재생하려면 cookie 위에 Latte와 동일한 본 이름/계층이 필요함. 새로 만들어서 이름을 맞추느니, Latte에 원래 있던 Armature를 통째로 가져다 쓰는 게 더 정확하고 빠름 (리네임 불필요).
- 리스크 인지: cookie와 Latte는 **체형/크기 차이가 큼**(다리 길이, 척추 비율 등). 본의 레스트 포즈가 크게 바뀌면 기존 애니메이션 회전 키프레임과 어긋나 동작이 부자연스러워질 수 있음. 그래도 우선 간단한 방법(본 그대로 이식)부터 시도해보고, 결과가 어색하면 **Copy Rotation 본 컨스트레인트 + Bake Action 리타겟**(레스트 포즈 차이를 흡수 가능)으로 전환하기로 함.

**새 리깅 절차 (Latte 본 이식):**
1. 기존 cookie의 `metarig`/`rig`(Wolf 또는 Basic Quadruped 실험분) 삭제
2. Latte FBX를 cookie와 같은 Blender 씬으로 Append (File → Append → Object → Armature만)
3. Latte Armature를 Edit Mode에서 cookie 메쉬 크기/자세에 맞게 위치·스케일 조정 → **반드시 `Ctrl+A`로 Scale 적용(1.0 정규화)** — Wolf 때 겪은 "bone position is disjoint" 에러 원인이 오브젝트 스케일 미적용이었음, 재발 방지
4. `Dog` 메쉬 선택 → Armature 선택 → `Ctrl+P` → Armature Deform (Automatic Weights) → 수동 웨이트 보정
5. Latte 기존 애니메이션 액션(Walk/Sit 등)을 cookie 위에서 재생 테스트 → 관절 뚫림/이상 꺾임 등 왜곡 확인
   - 왜곡 심하면 → Copy Rotation 컨스트레인트 + Bake Action 방식으로 전환 (본 20~30개, 회전값만 복사해 레스트 포즈 차이 흡수)
6. 문제없으면 FBX 재익스포트 (Path Mode: Copy+내장 텍스처, 제한>선택된 오브젝트로 Dog+Armature만) → Unity에서 `DogAnimationController.cs`(Latte 전용) 붙여서 테스트

**리타겟 방법 후보:**
1. **Auto-Rig Pro** (Blender Market/Superhive, 약 $40, [정품 링크](https://superhivemarket.com/products/auto-rig-pro)) — Remap 기능으로 본 매핑 후 리타겟. 가장 빠르고 검증된 방법. **절대 크랙/불법 배포 사이트(unityunreal.com 등)에서 받지 말 것 — 악성코드/저작권 문제.**
2. **무료: Blender 기본 Bone Constraint(Copy Rotation) + Bake Action** — cookie 본 하나하나에 LowPoly 대응 본을 Copy Rotation으로 걸고, 애니메이션 재생 상태에서 Bake Action으로 구워냄. 본 수만큼(20~30개) 반복 작업 필요, 시간 오래 걸림. 회전축(Roll) 차이로 일부 본이 이상하게 꺾이면 축 보정 필요.

**리타겟 시 본 개수 불일치 관련 (오해 정정):** "본 이름만 바꾸면 리타겟된다"는 부정확한 설명이었음. 실제로는 본 이름 + 계층구조 + 비율까지 맞아야 함. Auto-Rig Pro 같은 도구는 본이 1:1로 안 맞아도 매핑 가능한 본만 연결하고, 매핑 안 된 본(cookie의 발가락/얼굴 세부 등)은 그냥 애니메이션 영향 없이 고정 상태로 남을 뿐 — "오토 리깅 기능을 못 쓰게 되는 것"은 아님. Auto-Rig Pro의 오토 리깅(웨이트 자동 생성)과 리타겟(Remap)은 별개 기능.

## 4-1. cookie 눈(Eye) 웨이트 문제 (해결됨)

**증상:** Latte 본을 cookie에 이식(Armature Deform, Automatic Weights)한 뒤, Pose Mode에서 눈 근처 작은 본을 움직이면 눈 부위 메쉬가 고무처럼 늘어남.

**원인:** cookie는 안구가 얼굴과 분리된 별도 지오메트리가 아니라 `tripo_mesh` 안에 통합된 형태. Automatic Weights가 눈 부위 버텍스를 `Eye_L`/`Eye_R`이라는 작은 눈 전용 본 Vertex Group에 웨이트 1.0으로 잡아버렸고, 그 작은 본이 움직이면 눈 부위만 분리되듯 딸려 늘어남.
(참고: Latte 원본은 반대로 안구가 별도의 작은 구체 메쉬로 분리되어 있어서 같은 증상이 안 보였음 — cookie와 구조 자체가 다름.)

**해결:** Latte 쪽 눈 깜빡임 애니메이션(`Pet_Dog_Latte_01@Unique_04.FBX`의 `Take 001.003` 액션) 확인 결과, cookie에서는 눈 전용 본을 쓰지 않고 머리 본에 고정시키는 쪽으로 결정. cookie의 눈 웨이트를 다음과 같이 재작업:
1. Edit Mode에서 눈 부위 버텍스 선택
2. Object Data Properties → Vertex Groups에서 `Eye_L`, `Eye_R` 그룹 각각 선택 → **`선택`으로 해당 버텍스 재확인 → `제거`**로 웨이트 삭제
3. 같은 눈 부위 버텍스를 다시 선택 → `Bip001 Head` 그룹 선택 → Weight 1.000 → **`할당`**
4. Pose Mode에서 검증: 눈 전용 본을 움직여도 눈이 안 딸려오고, `Bip001 Head` 본을 움직이면 눈이 얼굴과 자연스럽게 같이 움직임 — 확인 완료.

**Blender UI 팁 (헤맸던 부분 기록):**
- Vertex Groups 목록 오른쪽의 `-` 버튼은 그룹 자체를 삭제하는 것이고, 우리가 쓴 건 목록 아래의 `할당`/`제거`/`선택`/`선택 해제` 버튼 줄임 — 헷갈리기 쉬움.
- `할당`/`제거`는 Edit Mode에서 **뷰포트에 실제로 선택된 버텍스**에만 적용됨. 목록에서 그룹 이름만 클릭한다고 자동 적용되는 게 아니라, 먼저 버텍스를 선택한 상태여야 함.
- 그룹을 클릭 후 `선택`을 누르면 그 그룹에 속한 기존 버텍스가 뷰포트에 표시(선택)됨 — 이건 미리보기/확인용이지, 그 자체로 뭔가를 바꾸는 액션이 아님.
- 액션(Action) 데이터를 다른 Armature 오브젝트로 옮겨붙이는 것은 오브젝트 이름과 무관 — Properties 탭(뼈다귀 아이콘) → 애니메이션 섹션의 액션 브라우저 드롭다운에서 기존 액션을 골라 연결하면 됨. 다만 FBX를 여러 번 Import하면 `Bip001.001`, `.002`처럼 계속 새 오브젝트가 생겨 헷갈리기 쉬우므로, 필요한 파일들만 한번에 깨끗한 새 씬에서 Import하는 게 훨씬 빠름.

## 4-2. Unity Play 모드에서 cookie 메쉬가 극단적으로 찌그러지는 문제 (미해결, 조사 중)

**증상:** `cookie_latte_rig.fbx`(Latte 본 이식 완료본)를 Unity에 Import해서 씬에 배치 후 Play 모드에 들어가면, 메쉬가 뾰족하게 늘어나며 완전히 찌그러짐. Edit 모드/Object 모드(Play 아닐 때)는 정상.

**핵심 관찰:**
- Play 진입 **전에 Animator 컴포넌트를 미리 비활성화**해두면 정상 — 찌그러지지 않음.
- Play 진입 **후**(이미 찌그러진 상태)에 Animator를 꺼도 찌그러진 채로 굳음(리셋 안 됨).
- Animator Controller를 아예 `None`으로 비워도 여전히 찌그러짐 → 특정 애니메이션 클립의 회전값 문제가 아니라, **Animator 컴포넌트가 활성화되어 SkinnedMeshRenderer가 바인드 포즈를 재계산하는 시점 자체**에서 뭔가 깨짐.
- Avatar Definition을 `Create From This Model`에서 `Copy From Other Avatar`(Latte 원본 Avatar 재사용)로 바꿔도 동일하게 찌그러짐 → Avatar 매핑 문제 아님.

**배제된 원인들 (전부 시도했으나 무관함이 확인됨):**
- FBX Export 옵션 조합 (모디파이어 적용/변형 뼈대 만 ON/OFF 각 조합 — 오히려 껐을 때 본 계층이 사라지거나 메쉬가 안 보이는 등 부작용만 발생, 원래 조합인 둘 다 ON이 그나마 정상)
- 아마츄어 기본/보조 뼈대 축 설정 (Y/X ↔ 다른 조합)
- Blender 씬 정리 상태 (재확인 결과 깨끗함, 중복 오브젝트 없음)
- Unity Root Node 드롭다운에 본이 2번씩 중복 표시되는 현상 — **이건 정상 패턴이었음**. 원래 잘 작동하던 Wolf 리그 cookie FBX(`Cookie and cream oreo milk 1.fbx`)에서도 동일하게 본이 2번씩 중복 표시됨. FBX Import UI의 표시 방식일 뿐 실제 데이터 중복이 아닌 것으로 추정.
- 메쉬 버텍스 웨이트 정규화 (Weight Paint 모드에서 "모두 노멀라이즈" 실행 후 재익스포트해도 변화 없음)

**남아있는 부수적 문제 (별개, 찌그러짐의 근본 원인은 아닌 것으로 확인됨):**
- Hierarchy에서 `Bip001`(Armature) 오브젝트의 Scale이 `100`으로 Import됨 (Bounds Extent가 0.003 같은 극소값으로 나오는 것과 연관). 아마 Blender→Unity 단위 변환 과정에서 Latte 원본(3ds Max, cm 단위 가능성) 스케일이 곱해진 것으로 추정. Scale을 1로 낮추면 강아지가 1/100 크기로 작아짐 (최상위 오브젝트 Scale을 100으로 보정하면 겉보기 크기는 맞출 수 있음). **다만 이 Scale 100을 그대로 두든 1로 고치든 찌그러짐 자체는 동일하게 재현됨 — 즉 이 스케일 문제와 찌그러짐은 서로 다른 두 개의 버그.**

**원인 확정 (대조 실험 4가지 조합 완료):**

| 본 | Animator | Controller | 결과 |
|---|---|---|---|
| Latte | 비활성화 | - | 정상 |
| Latte | 활성화 | 없음(None) | **정상** |
| Latte | 활성화 | `Pet_Dog_Latte_01_AnimCtrl` | **찌그러짐** |
| Wolf | 활성화 | 없음(None) | 정상 |

Controller가 없으면 Latte 본도 멀쩡함 — 즉 Animator 활성화 자체(Avatar 바인드포즈 재계산)는 원인이 아니었음 (이전 기록의 결론은 오판이었음, 정정). **`Pet_Dog_Latte_01_AnimCtrl`이 실제로 애니메이션 클립을 재생시킬 때만 찌그러지는 것으로 확정.**

이건 애초에 우려했던 시나리오와 일치함: cookie는 Latte와 **체형(다리 길이·척추 비율 등)이 많이 다름** ([관련 기록](#) 참고). Latte 애니메이션 클립은 원래 Latte 체형 기준 Rest Pose에서 녹화된 본 회전값인데, 이 본을 cookie 체형에 맞게 Blender에서 재배치(Rest Pose 자체를 수정)했으므로, 클립이 재생하는 회전값과 새 Rest Pose가 어긋나 극단적으로 왜곡되는 것으로 추정.

**다음에 시도할 것:**
1. Animator Controller 안의 개별 State(예: `Idle_01`)를 하나씩 Preview하며 어느 클립에서 왜곡이 시작되는지, 왜곡 정도가 본마다 다른지 확인 — 만약 특정 본(예: 다리처럼 체형 차이가 큰 부위)에서만 심하면 체형 차이 가설이 강해짐
2. **Copy Rotation 컨스트레인트 + Bake Action** 방식으로 전환 검토 (레스트 포즈 차이를 흡수하는 방식, 애초에 체형 차이 클 때의 대안으로 노트에 남겨뒀던 것)
3. 대안: Latte 본의 Rest Pose를 cookie 체형에 맞추되 **비율(스케일)만 균일하게 조정**하고 관절 각도(Roll/방향)는 원본 그대로 유지하는 방식으로 재작업 — 각도가 안 바뀌면 회전값 어긋남이 줄어들 가능성

## 5. Unity 스크립트 작성 완료

`Assets/Scripts/Dog/` 폴더:
- `DogAnimationController.cs` — Latte(`Pet_Dog_Latte_01_AnimCtrl`) 전용
- `LowPolyDogAnimationController.cs` — Chihuahua/GreatDane/GoldenRetriever/Fox 전용
- `WolfAnimationController.cs` — Wolf 전용
- `DogTestInput.cs` — 에디터 테스트용 WASD 이동 + 액션 키(Space/C/V/B) 입력, 위 세 컨트롤러 중 붙어있는 것 자동 감지해서 조작

**주의:** 한 오브젝트에 위 세 컨트롤러(DogAnimationController/LowPolyDogAnimationController/WolfAnimationController)를 동시에 붙이면 안 됨 — 각 모델 전용이라 파라미터 이름이 안 맞는 것까지 호출되어 콘솔 경고 발생. cookie 강아지가 LowPoly 애니메이션으로 리타겟되면, `LowPolyDogAnimationController` + `DogTestInput`을 붙이면 됨 (단, cookie 전용 새 Animator Controller를 만들어 리타겟된 클립을 연결해야 함).

## 다음에 할 일

1. Auto-Rig Pro 구매 여부 결정 (또는 무료 Bone Constraint 방법 택일)
2. LowPoly GoldenRetriever FBX를 cookie 리그와 같은 Blender 씬으로 Import
3. 본 매핑 (주요 관절: Root, Spine, Neck, Head, Tail, 다리 4관절 x4 위주. 발가락/얼굴 세부는 생략 가능)
4. Walk/Run/Sit/Bark/Idle 클립 리타겟
5. cookie 리그 기준으로 FBX 재익스포트 (Path Mode: Copy, Deform Bones Only 옵션 동일하게 적용)
6. Unity에서 cookie 전용 새 Animator Controller 생성, State에 리타겟된 클립 연결
7. `LowPolyDogAnimationController` + `DogTestInput`을 cookie 오브젝트에 붙여서 테스트
