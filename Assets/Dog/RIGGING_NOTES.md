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

## 4-2. Unity Play 모드에서 cookie 메쉬가 극단적으로 찌그러지는 문제 (해결됨)

**해결:** 스케일 문제로 해결됨(정확한 조치 내용은 기록 누락 — 아마 Bip001 오브젝트 Scale 100 관련 조정으로 추정, 재발 시 아래 원인 분석부터 재확인할 것).

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

## 6. Blender 작업 파일 위치 (중요, 팀원 전달용)

**이 문서가 다루는 리깅 작업은 Unity 프로젝트 밖, 로컬 Blender 파일에서 진행 중입니다.** Unity `Assets/Dog/`에는 완성/중간 산출물 FBX만 들어있고, 실제 본/웨이트 편집은 아래 blend 파일에서 이루어집니다.

- **작업 중인 blend 파일:** `C:\Users\BaeSoohyun\Documents\dog3.blend` (Latte 본을 cookie 메쉬 "쿠키"에 이식하는 리타겟 작업 진행 중)
- **Idle 애니메이션 완성 백업:** `C:\Users\BaeSoohyun\Documents\dog3.fbx`
- 이 파일들은 Unity 프로젝트 디렉터리 밖에 있어 git으로 관리되지 않으므로, 팀원과 공유하려면 파일을 직접 전달해야 함.

## 7. 2026-08-22 진행 — 팔다리 스케일 조정 스크립트 적용 + 애니메이션 미재생 이슈 (미해결)

**배경:** cookie는 Latte보다 체형(다리 길이·척추 비율)이 커서, Latte 본을 그대로 이식하면 팔다리가 짧게 붙는 문제가 있었음. Neck 이하 각 본 체인(척추/양팔/양다리/꼬리)을 "Latte 안에서의 본별 상대 길이 비율은 유지한 채, 체인 하나당 하나의 배율만 곱해서" cookie 크기에 맞게 확대하는 스크립트(`retarget_limbs_scaled.py`, 다운로드 폴더)를 작성해 `dog3.blend`에 적용 완료.

적용된 체인별 배율: 척추 1.41배, 왼팔 1.70배, 오른팔 1.72배, 왼다리 1.58배, 오른다리 1.62배, 꼬리 1.84배. Pelvis(기준점)와 Neck 이상(Head/Eye/Mouth/Tongue/Nose/Ear)은 건드리지 않음.

**새로 발생한 문제 (미해결):** 이 스크립트로 본 Rest Pose(위치/길이)를 바꾼 뒤, cookie(`Bip001`) 아마추어에서 기존 Walk 애니메이션을 재생해보면 **Latte(`Bip001.001`)는 정상적으로 움직이는데 cookie(`Bip001`)는 전혀 움직이지 않음.** Action Editor상 `Bip001`에 `Walk` Action이 정상 연결돼 있고 각 본마다 키프레임도 존재하는 것까지는 확인됨 (즉 Action 연결 자체가 끊긴 건 아님). "포즈 위치/휴식 위치" 토글을 "포즈 위치"로 바꿔도 해결 안 됨.

**다음 세션에서 확인할 것 (아직 안 해본 것):**
1. Pose Mode에서 본을 손으로 직접 회전(R키)해도 반응이 없는지 → 반응 없으면 본 자체가 잠겨있거나 다른 문제, 반응 있으면 재생/Action 연결 쪽 문제로 좁혀짐
2. NLA 에디터에 별도 스트립이 있고 influence가 0이거나 뮤트돼 있는지 확인
3. Rest Pose 변경으로 기존 F-curve(포즈 본 회전값)의 상대 계산이 새 Rest Pose 기준으로 깨졌을 가능성 (다만 보통 "이상하게 움직인다"가 예상되는데 실제로는 "전혀 안 움직임"이라 이 가설과는 안 맞을 수 있음 — 원인 미확정)

**주의:** 이 문제를 해결하기 전까지는 Weight Paint(웨이트 보정) 단계로 넘어가지 않는 게 좋음 — 애니메이션이 재생 안 되면 웨이트가 맞는지 눈으로 확인할 방법이 없음.

## 8. 팀원 작업용 "생메시" FBX 추출 (완료)

다른 팀원이 별도로 리깅/웨이트를 시도해볼 수 있도록, `dog3.blend`의 cookie 메쉬(`Dog` 오브젝트, `DogMesh` 컬렉션)에서 리깅/웨이트를 제거한 순수 지오메트리만 FBX로 추출함.

**절차 (재현 시 참고):**
1. `Dog` 메쉬 오브젝트 선택 → 오브젝트 데이터 프로퍼티스(초록 삼각형 아이콘) → 버텍스 그룹 패널
2. 버텍스 그룹이 197개(Rigify 기반 `DEF-f_index.02.R` 등 이름 패턴)로 많아 UI에서 하나씩 지우기 번거로우므로, **Scripting 탭 파이썬 콘솔에서 `bpy.data.objects["Dog"].vertex_groups.clear()`로 일괄 삭제**
3. 모디파이어 프로퍼티스(렌치 아이콘)에서 Armature 모디파이어 제거
4. File → Export → FBX, 오브젝트 유형에서 "메시"만 체크(아마츄어 체크 해제)하고 익스포트

**주의 (재현 시 실수하기 쉬운 부분):** 아웃라이너에서 초록 역삼각형 "data" 항목을 직접 삭제하면 안 됨 — 그건 메시 지오메트리 데이터 블록 자체라 삭제 시 메시가 통째로 사라짐. 반드시 속성 에디터의 버텍스 그룹 패널(또는 파이썬 콘솔)에서 그룹만 지울 것.

이 작업은 `dog3.blend` 원본에서 진행했으나 **작업 완료 직후 Ctrl+Z로 전부 되돌려서 원본은 리깅/웨이트가 살아있는 상태 그대로 안전하게 보존됨.** 팀원에게 넘긴 생메시 FBX는 원본과 별개의 산출물이며, 팀원이 그 위에 새로 리깅한 결과물을 다시 합칠 계획이 있다면 병합 방식을 사전에 논의할 것.

## 9. 쿠키 본 리타게팅 트러블슈팅 상세 기록 (Blender, 다운로드 폴더 작업분)

**주의:** 이 섹션은 7번 항목(팔다리 스케일 조정 스크립트)과 같은 작업 줄기지만, 다른 세션에서 더 상세히 기록된 시행착오 로그다. 작업 파일은 `C:\Users\BaeSoohyun\Downloads\dog3.blend`(Unity 프로젝트 밖, Blender 5.1.0) — 6번 항목의 `Documents\dog3.blend`와는 별개 경로이니 혼동 주의.

### 2026-08-20 시점 상태
- Idle 애니메이션 웨이트는 완전히 해결됨(눈/코/입/귀/정수리 포함). 완성 백업: `C:\Users\BaeSoohyun\Documents\dog3.fbx`.
- Walk 작업 중 `dog3.blend` 본체가 오염되어 저장까지 되며 복구 불가 상태가 됐던 적 있음.
- Walk에서 앞다리 겨드랑이 메시가 크게 늘어나는 현상 — `Bip001 L Forearm` 웨이트가 겨드랑이~가슴 옆까지 과도하게 퍼져 Clavicle/UpperArm과 겹치는 게 원인으로 추정.

### 2026-08-21 세션 — Automatic Weight 삽질, 결국 전날 백업으로 롤백
"Automatic Weight로 베이스 전체 재생성"을 시도했다가 척추 웨이트가 겹치거나 사각지대가 생기는 새 문제(재생 시 몸통이 꺾이거나 움푹 들어감)가 발생, 손으로 복구 시도했지만 전날 백업보다 나아지지 않아 **그날 작업분은 전부 폐기하고 저장하지 않은 채 전날 `dog3.fbx` 백업으로 재시작**.

이 세션에서 확인된 것:
- AI 생성 메시(`tripo_mesh`)는 좌우 토폴로지가 대칭이 아니라서 "웨이트 → 미러" 기능이 조용히 실패함(에러 없이 무변화). 이 메시에서는 좌우 미러링을 포기하고 양쪽 다 수동 작업해야 함.
- Blender 5.0/5.1의 "웨이트 → 뼈에서 자동 할당"은 알려진 버그로 신뢰 불가(공식 버그 트래커 #150081). 신뢰 가능한 건 Object Mode `Ctrl+P` → "자동 웨이트로"(전체 재계산)뿐인데, 이건 기존에 손으로 다듬은 부분까지 전부 덮어씀.
- Automatic Weight(Heat Map)는 척추처럼 짧고 촘촘한 본 체인에서 특히 서투름 — 웨이트가 뭉치거나 사각지대가 생김. 이런 부위는 처음부터 그라디언트/브러시로 수동 작업하는 게 나음.
- Weight Paint "그라디언트(선형)" 도구는 화면 공간 기준 무한 평면에 적용되어 원치 않는 부위까지 영향을 줌 — 좁은 부위 보정은 Add/Subtract 브러시로 손으로 문지르는 게 가장 확실함.
- `Ctrl+A → Scale` 정규화 자체는 안전하나, 이미 Parent로 연결된 상태에서 `Ctrl+P` → "자동 웨이트로"를 다시 실행하면 스케일이 중복 적용되어 메시가 비정상적으로 커지고 뒤틀림.

### 반복해서 겪은 핵심 실수 (매번 재발 주의)
1. **Rest Pose 아닌 상태에서 본 재배치/웨이트 페인트 금지.** Pose Mode "휴식 위치" 전환 또는 Action 연결 해제 후 작업.
2. **오브젝트 레벨 트랜스폼(Scale/Rotation) vs Action 내부 F-curve는 별개.** 라떼 원본 오브젝트는 Scale 0.01, Rotation 약 -97도로 배치돼 있고 이건 정상이라 건드리면 안 됨. 그런데 Action 안에도 오브젝트 레벨 위치/회전/스케일 채널이 F-curve로 박혀 있어서, 그 Action을 쿠키(Scale 1.0)에 연결하면 이 채널이 쿠키 트랜스폼을 강제로 덮어씀 — Action Editor에서 오브젝트 레벨(본 이름 없는 최상위) 채널만 찾아 삭제할 것. 매 애니메이션(Walk, Jump, Sit_01/02, Unique_01/04/05)마다 재발 가능.
3. **Pelvis(계층 루트) 축 반전이 하위 전체에 전파됨.** 해결책: 본을 라떼 것을 복제해서 오브젝트 레벨 값은 유지한 채(Rest Pose 상태에서) 위치·길이만 재배치.
4. **버텍스 그룹 Assign/Remove가 반영 안 되는 것처럼 보이는 경우**: Action Editor "선택된 항목만 표시" 필터가 켜져 있거나, 정점 1개가 누락된 경우가 많음. 안 고쳐지면 그룹을 통째로 삭제하고 이름만 동일하게 재생성하면 해결.
5. **정점 1개 누락은 재생 중 스파이크로만 드러남.** Edit Mode는 항상 Rest Pose만 보여주므로, Weight Paint 모드에서 문제 프레임에 멈춰 확인해야 함.
6. Head 그룹이 정수리/이마 정점을 안 잡으면 이마가 눌린 밴드처럼 보임 — 정점 추가 할당으로 해결.

### 자동화 스크립트 `retarget_animation.py` (다운로드 폴더)
애니메이션 1개당 소스 FBX 경로만 바꿔 실행하면 라떼 애니메이션을 쿠키 아마추어(`Bip001`)에 연결하고 `_cookie.fbx`로 익스포트하는 스크립트. 원래 버전은 라떼 원본의 오브젝트 레벨 스케일/회전 F-curve가 그대로 딸려 들어와 쿠키 오브젝트 트랜스폼을 덮어쓰는 문제가 있었음 → `strip_object_level_transform_fcurves()`를 추가해 `pose.bones[...]`로 시작하지 않는 location/rotation_*/scale 채널을 연결 직전에 자동 삭제하도록 수정(본 채널은 안전하게 보존).

### 라떼 본 좌표가 100배 큰 이유 (반복 헷갈렸던 부분, 확정)
라떼 아마추어 오브젝트 Scale이 0.01인 건 **FBX 익스포트 시 "단위를 적용(Apply Unit)" 옵션을 켜고 내보냈기 때문**. 이 옵션은 본의 실제 좌표(미터 단위 대형 값)는 그대로 두고 오브젝트 레벨 Scale에만 배율(0.01)을 얹는다. 즉 Edit Mode에서 `eb.head`/`eb.tail`(로컬 좌표)을 읽으면 오브젝트 Scale이 반영 안 된 100배 큰 값이 그대로 나온다.

**결론: 라떼 쪽 본 좌표를 다루는 모든 스크립트는 반드시 오브젝트 Scale을 곱해 정규화한 뒤 사용해야 한다.** 단, `matrix_world`를 통째로(Rotation 포함) 곱하면 안 되는 경우가 있음 — 라떼와 쿠키 오브젝트의 Rotation 값 자체가 다르면(예: 라떼 Z:-86.84°, 쿠키 Z:-97.273°) 두 좌표를 같은 "월드 스페이스"로 섞을 때 축이 어긋난다. 오브젝트 레벨 Rotation이 다른 두 아마추어의 본을 비교/이식할 때는 **Scale만** 곱한 좌표로 비교해야 한다(`Vector((eb.head.x*scale.x, eb.head.y*scale.y, eb.head.z*scale.z))` 형태, Rotation/Location 제외).

### "라떼 형태 비율 유지 + 쿠키 크기로 스케일" 스크립트 시행착오 (`retarget_limbs_scaled.py`)
목표: 쿠키의 Neck 이하 각 본 체인(척추/양팔/양다리/꼬리)을 라떼와 "형태"(본 사이 간격·방향·상대 길이 비율)는 유지하면서, **체인 하나당 딱 하나의 배율**만 곱해 쿠키 크기로 확대. Pelvis는 고정, Neck 이상(Head/Eye/Mouth/Tongue/Nose/Ear)은 안 건드림.

시행착오 순서 (동일 실수 반복 방지용):
1. **v1(폐기): "부모 Tail = 자식 Head"로 강제 재연결.** 라떼도 본끼리 원래 간격이 있는 구조인데 이걸 억지로 붙여서 팔다리가 사방으로 뻗어나가는 결과가 나옴. **라떼가 본끼리 안 붙어있다는 걸 사용자가 명시적으로 알려줬는데도 이 실수를 반복한 적 있음 — "본 사이 간격을 강제로 없애면 안 된다"를 최우선으로 기억할 것.**
2. **v2**: 라떼/쿠키 오브젝트 Rotation이 서로 달라 `matrix_world`로 월드 스페이스 변환 시 서로 다른 좌표축이 섞여 목/다리가 삐져나옴 → Scale만 반영하는 방식으로 수정.
3. **v3(폐기): "본 하나하나마다 개별 비율"을 독립 계산하는 방식.** 손으로 배치하며 생긴 오차가 특정 본(예: 쿠키 R Calf가 2.46배로 튐)에 과도하게 반영되어 다음 관절 간격이 20cm까지 벌어짐. **핵심 교훈: "라떼 대비 각 본이 몇 배 커졌나"를 개별로 재는 것과 "라떼 안에서 본끼리의 상대 비율을 유지한 채 체인 전체를 하나의 배율로 키우는 것"은 다른 질문 — 후자가 맞는 접근.**
4. **최종(v4): 체인 하나당 "라떼 체인 전체길이 대비 쿠키 체인 전체길이" 비율 딱 하나(`CHAIN_RATIO`)만 계산해 체인 안의 모든 본에 동일 적용.** 오프셋도 본 길이도 전부 이 하나의 배율로 스케일 — 라떼 안에서의 본별 상대 비율이 유지되면서 손 작업 오차가 특정 본에 몰리지 않고 전체에 고르게 분산됨.

중요 교훈:
- Edit Mode(Rest Pose)에서 본 구조를 바꾼 뒤, **기존 애니메이션이 걸린 Pose Mode/재생 화면으로 결과를 확인하면 안 됨.** Rest Pose가 바뀌면 예전 Rest Pose 기준으로 기록된 Action의 회전 F-curve가 새 Rest Pose에서는 완전히 다른 결과로 재생되어, 마치 본 배치 자체가 잘못된 것처럼 보임(뒷다리가 심하게 뒤틀려 보이는 식). 본 구조 변경 후 확인은 **반드시 Edit Mode(정지 상태)** 또는 Pose Mode에서 "포즈 > 변환을 지우기 > 모두"로 강제 Rest Pose를 만든 뒤 할 것. 애니메이션 재생 확인은 F-curve를 새 Rest Pose에 맞게 재입히는 작업 이후 단계.
- 단위를 이중으로 나누는(0.01을 두 번 곱하는) 실수 주의 — 데이터가 이미 스케일 적용됐는지 변수명만으로 판단하지 말고 항상 재확인할 것.

**결과**: `retarget_limbs_scaled.py` dry_run에서 척추 1.41배, 왼팔 1.70배, 오른팔 1.72배, 왼다리 1.58배, 오른다리 1.62배, 꼬리 1.84배로 정상 범위 출력 확인 후 `dry_run=False`로 실제 적용 완료 (7번 항목과 동일 결과).

### 새로 발견된 문제: 쿠키(Bip001)가 Walk 애니메이션 재생 시 안 움직임 (미해결)
`retarget_limbs_scaled.py` 적용 후, 라떼(`Bip001.001`)는 정상 재생되는데 쿠키(`Bip001`)는 전혀 움직이지 않음. Action Editor 확인 결과 `Bip001`에 `Walk` Action이 정상 연결돼 있고 각 본마다 키프레임 F-curve도 존재함 — Action 연결 자체는 문제가 아님. "포즈 위치/휴식 위치" 토글도 무관. 다음에 확인할 것(7번 항목과 동일):
1. Pose Mode에서 본을 손으로 직접 회전(R키)해도 반응이 없는지 vs 애니메이션 재생만 안 되는지 구분
2. NLA 에디터에 별도 스트립이 있고 influence가 0이거나 뮤트돼 있는지 확인
3. Rest Pose 변경으로 기존 F-curve의 상대 계산이 새 Rest Pose 기준으로 깨졌을 가능성 (다만 "이상하게 움직인다"가 아니라 "전혀 안 움직임"이라 이 가설과 안 맞을 수 있음 — 원인 미확정)

### 팀원 공유용 "생메시" FBX 추출 절차 (완료, 8번 항목과 동일 작업의 상세 버전)
1. `Dog` 메쉬 오브젝트 선택 → 오브젝트 데이터 프로퍼티스(초록 삼각형 아이콘) → 버텍스 그룹 패널
2. 버텍스 그룹 197개를 UI에서 하나씩 지우지 말고 **Scripting 탭 파이썬 콘솔에서 `bpy.data.objects["Dog"].vertex_groups.clear()`로 일괄 삭제**
3. 모디파이어 프로퍼티스(렌치 아이콘)에서 Armature 모디파이어 제거
4. File → Export → FBX, 오브젝트 유형에서 "메시"만 체크(아마츄어 체크 해제)하고 익스포트
5. 작업 완료 직후 **Ctrl+Z로 전부 되돌려서 원본 `dog3.blend`는 리깅/웨이트가 살아있는 상태로 안전하게 보존됨.** 팀원에게 넘긴 생메시 FBX는 원본과 별개 산출물이며, 팀원 결과물을 다시 합칠 계획이 있다면 병합 방식을 사전에 논의할 것.

**주의**: 아웃라이너에서 초록 역삼각형 "data" 항목을 직접 삭제하면 메시 지오메트리 자체가 통째로 사라짐(버텍스 그룹만 지우는 게 아님) — 반드시 속성 에디터의 버텍스 그룹 패널(또는 파이썬 콘솔)에서 그룹만 지울 것.

### 이 세션(Roll/방향 정렬) 진행 상황 — 아직 미해결
위 "쿠키가 Walk 재생 시 안 움직임" 문제와는 별개로, 그 이전 단계인 **본의 Roll·방향(Head→Tail 벡터)을 라�떼와 맞추는 작업**도 별도 세션에서 진행함:
- `copy_bone_roll.py`(Roll 값만 복사)는 Rest Pose 상태에서 재실행 결과 이미 완전히 적용되어 있었음(dry run 0개 변경) — Roll 자체는 문제가 아니었음.
- Roll만으로는 부족하다는 사용자 지적에 따라 `align_bone_direction_and_roll.py`를 작성 — 본의 Head→Tail 방향(정규화 벡터)까지 라떼와 일치시킴(길이는 쿠키 기존 값 유지). dry run에서 39개 중 36개 본이 방향 5도 이상 차이(R Foot 39°, R HorseLink 36°, R Forearm 32° 등 팔다리 말단으로 갈수록 오차 큼) → 실제 적용 완료.
- 방향까지 맞춘 후에도 애니메이션 재생 결과는 육안상 크게 달라지지 않음. 사용자가 직접 비교해본 결과 "본 방향 자체는 라떼와 제법 잘 맞는다"고 판단.
- 현재 가설: 본의 **크기(길이)·위치(Head 좌표, 체인 간격)**가 라떼와 달라서, 로컬 회전 F-curve의 회전 중심점이 어긋나 부모→자식으로 갈수록 오차가 누적되는 것으로 추정. 단, Neck 이상(Head/Eye/Mouth/Tongue/Nose/Ear)은 건드리지 않고 팔다리·척추·꼬리만 간격을 쿠키 비율에 맞게 조정하는 방향으로 좁혀짐 — 이는 위 `retarget_limbs_scaled.py`(v4, 체인당 통일 배율)와 사실상 같은 방향성.
- **중요**: 이 단계에서 작성한 `scale_bone_length_to_cookie.py`(부모 Tail=자식 Head 강제 재연결 방식)는 위 "v1(폐기)" 접근법과 동일한 방식이다 — 이미 실패가 확인된 접근이므로 실행하지 말고, 대신 이미 검증된 v4(체인당 통일 배율, `retarget_limbs_scaled.py`) 로직을 이 체인들에도 적용하는 방향으로 가야 함.

**How to apply:** 다음 세션에서 리깅 재개 시 최우선 과제는 (1) "쿠키 애니메이션 미재생" 원인 진단, (2) 그 다음 본 크기/위치를 v4 방식(체인당 통일 배율)으로 재조정. `scale_bone_length_to_cookie.py`(부모-자식 강제 재연결) 방식은 사용하지 말 것. 이 두 문제를 해결하기 전까지는 Weight Paint 단계로 넘어가지 말 것.

## 다음에 할 일

1. **cookie 애니메이션 미재생 원인 진단** (7번 항목, 최우선 — 이게 안 풀리면 이후 웨이트 보정도 눈으로 확인 불가)
2. Auto-Rig Pro 구매 여부 결정 (또는 무료 Bone Constraint 방법 택일)
3. LowPoly GoldenRetriever FBX를 cookie 리그와 같은 Blender 씬으로 Import
4. 본 매핑 (주요 관절: Root, Spine, Neck, Head, Tail, 다리 4관절 x4 위주. 발가락/얼굴 세부는 생략 가능)
5. Walk/Run/Sit/Bark/Idle 클립 리타겟
6. cookie 리그 기준으로 FBX 재익스포트 (Path Mode: Copy, Deform Bones Only 옵션 동일하게 적용)
7. Unity에서 cookie 전용 새 Animator Controller 생성, State에 리타겟된 클립 연결
8. `LowPolyDogAnimationController` + `DogTestInput`을 cookie 오브젝트에 붙여서 테스트
