# 볼 당기기(Cheek Pull) 리깅 매뉴얼

> CC(Character Creator)/iClone 베이스 스켈레톤 캐릭터에 "볼 당기기" 인터랙션용 본을 추가하는 전체 과정 정리. Blender 작업 → FBX export → Unity 세팅까지 순서대로 따라 하면 됨. kkum 캐릭터로 작업하면서 겪은 함정들을 전부 트러블슈팅 표에 정리해뒀으니, 막히면 거기부터 확인.

---

## 1. 이게 뭘 하는 건가

캐릭터 얼굴에 좌/우 볼 전용 본(`Character_Ball_L`, `Character_Ball_R`)을 새로 추가하고, 볼 살 부위 정점에 가중치를 물린 다음, Unity에서 이 본을 마우스로 드래그하면 볼이 당겨졌다가 탄성 있게 원위치로 돌아오는 인터랙션(`CheekPullHandler.cs`)을 붙이는 작업.

## 2. 적용 대상

- Reallusion Character Creator(CC3/CC4) 기반 스켈레톤 (`CC_Base_Hip`, `CC_Base_Head`, `CC_Base_FacialBone` 등의 본 이름 규칙을 쓰는 캐릭터)
- 현재 프로젝트의 kkum, amber, jonryo2 전부 이 규칙을 따름
- 다른 리그(Mixamo, VRM 휴머노이드 전용 등)를 쓰는 캐릭터는 본 이름/계층 구조가 다르므로 이 매뉴얼을 그대로 적용하면 안 되고 구조 확인부터 다시 해야 함

## 3. 준비물

- Blender 5.x
- 대상 캐릭터의 원본 FBX (CC_Base 스켈레톤 + 얼굴/몸 메쉬 포함)
- Unity 프로젝트 내 `Assets/Scripts/CheekPullHandler.cs` (이미 구현되어 있음, 수정 불필요)

---

## 4. 전체 흐름 요약 (체크리스트)

- [ ] Blender에서 Armature 회전값 정리 (Apply Rotation)
- [ ] 볼 본 2개 생성, 이름을 `Character_Ball_L` / `Character_Ball_R`로 지정
- [ ] `CC_Base_Head`(또는 `CC_Base_FacialBone`)에 부모 연결, Deform 체크
- [ ] **웨이트를 먼저 칠하고 나서** Deform을 켜거나 export할 것 (순서 중요, 6.4절 참고)
- [ ] 좌우 대칭(Symmetrize)으로 반대쪽 본/웨이트 생성
- [ ] 메쉬(m_head 등)의 머티리얼 Link가 "Data"로 되어있는지 확인 (Shape Key 있는 메쉬는 필수)
- [ ] FBX export (씬 전체, Mesh+Armature 포함, Apply Transform 켜서 시도)
- [ ] Unity에 반영 후 회전/스케일/바인드포즈 깨짐 여부 검증 (8절 체크리스트)
- [ ] 프리팹에서 볼 본에 Collider + `CheekPullHandler` 컴포넌트 부착
- [ ] `CharAttributes.featureTags`에 `볼당기기` 태그 추가
- [ ] Play 모드에서 좌/우 각각 드래그 테스트

---

## 5. Part A — Blender: 본 만들기

### 5.1 (가장 먼저) Armature 회전 정리

CC 계열 FBX를 임포트하면 Armature 오브젝트 자체에 축 보정용 회전값이 남아있는 경우가 많음. 이 상태로 새 본을 만들고 나중에 Symmetrize를 돌리면 좌우가 아니라 앞뒤 축으로 미러되는 등 좌표가 꼬임.

1. Object Mode에서 **Armature** 선택 (메쉬도 같이 선택하면 더 안전)
2. N패널(N키) → Item 탭 → Rotation 값 확인
3. 0,0,0이 아니면 3D 뷰포트에 마우스 올린 채 **Ctrl+A → All Transforms**

### 5.2 본 만들기

1. Armature 선택 → **Tab**으로 Edit Mode
2. Numpad 1(정면 뷰)로 전환, 얼굴 확대
3. `CC_Base_Head`(볼 본의 부모로 쓸 본) 클릭해서 선택
4. 선택된 채로 **Shift+A → Single Bone**
5. 본의 head/tail 포인트를 각각 선택 후 **G**로 이동해서 왼쪽 볼 살 부위 안쪽에 배치
   - 정밀 배치가 필요하면 상단 툴바 자석 아이콘(스냅) 켜고 **Vertex**로 설정 → 이동 중 메쉬 표면에 붙음

### 5.3 이름 / 부모 설정

1. 새 본 이름을 N패널 Item 탭에서 **`Character_Ball_L`**로 변경
   - ⚠️ 이 이름 그대로 써야 함. `CheekPullHandler.cs` 스크립트 주석과 실제 코드가 이 이름을 기본값으로 가정하고 있어서, 이름을 맞추면 Unity에서 별도 설정 없이 바로 인식됨
2. N패널 → Bone 탭 → Relations → Parent를 `CC_Base_Head`로 지정 (Connected 체크 해제)
3. 같은 Bone 탭에서 **Deform 체크박스는 아직 켜지 마세요** → 6.4절 참고 (웨이트 칠하기 전에 켜면 export가 깨짐)

### 5.4 좌우 대칭 (Symmetrize)

1. Edit Mode, `Character_Ball_L` 본만 선택
2. 상단 **Armature 메뉴 → Symmetrize**
3. 반대편에 `Character_Ball_R`이 자동 생성되는지 확인 (이름의 `_L_`/`_R_` 패턴을 Blender가 자동 인식해서 미러 시 이름도 바꿔줌)
4. 이상하게 미러되면(반대 축으로 뒤집힘 등) → 5.1의 Armature 회전 정리가 안 된 상태일 가능성이 큼, 다시 확인

---

## 6. Part B — Blender: 가중치(웨이트) 페인트

### 6.1 버텍스 그룹 만들기

1. 메쉬(얼굴/머리 오브젝트, 보통 `m_head`) 선택
2. Object Data Properties(초록 삼각형 아이콘) → Vertex Groups
3. `+`로 `Character_Ball_L`, `Character_Ball_R` 이름의 빈 그룹 생성 (본 이름과 철자 완전 일치 필수)

### 6.2 웨이트 칠하기

1. 메쉬 선택 → **Ctrl+Tab → Weight Paint** 모드
2. 활성 Vertex Group이 `Character_Ball_L`인지 확인
3. 상단 Tool 설정에서 **Auto Normalize 켜기** (안 켜면 기존 `CC_Base_Head` 가중치와 충돌해서 본을 움직여도 메쉬가 안 따라옴)
4. Draw 브러시(Weight 1.0)로 볼 살 부위 칠하기, 경계는 Strength 낮춰서 0.3~0.7 정도로 페더링
5. `Character_Ball_R` 그룹으로 바꿔서 반대쪽도 칠하기 (또는 Vertex Groups 패널의 드롭다운 메뉴 → **Mirror Vertex Group**으로 왼쪽 웨이트를 오른쪽에 자동 복사)

### 6.3 테스트

Pose Mode에서 `Character_Ball_L`/`R` 본을 선택해 G로 당겨보면서 볼이 자연스럽게 늘어나는지 확인. 이상하면 Weight Paint로 돌아가 경계 다듬기.

### 6.4 ⚠️ 웨이트 다 칠친 뒤에 Deform 켜기 (중요)

Bone Properties에서 **Deform 체크박스를 웨이트를 실제로 칠하기 전에 켜두면 안 됨.** Deform이 켜진 상태로 해당 본의 Vertex Group에 가중치가 하나도 없으면, export 시 그 본에 대한 스킨 클러스터가 **Indexes/Weights 데이터 없이 불완전한 형태**로 fbx에 기록됨. 이 상태의 fbx를 Unity에서 열면 **그 메쉬 전체가 화면에 안 보이게 됨** (본 하나 추가했을 뿐인데 캐릭터가 통째로 사라지는 버그의 원인, 8.3절 참고).

**순서**: 본 생성 → (Deform 끄고) 부모/이름 설정 → 웨이트 페인트로 가중치 실제로 칠하기 → 그 다음에 Deform 켜기 → export.

---

## 7. Part C — (선택) 얼굴 메쉬 해상도 올리기

볼이 당겨질 때 더 부드럽게 보이게 하려면 폴리곤을 늘릴 수 있음. **Sculpt 모드의 Dynamic Topology/Remesh는 쓰지 말 것** — UV, 버텍스 그룹, 셰이프키가 전부 날아감.

- **방법 A (비파괴, 추천)**: 메쉬에 Modifier Properties(렌치 아이콘) → Add Modifier → Generate → **Subdivision Surface** 추가. Armature 모디파이어가 있다면 그게 Subdivision Surface보다 위에 오도록 순서 정렬. Unity로 export할 때는 FBX Export 창에서 **Apply Modifiers** 체크해야 늘어난 폴리곤이 실제로 저장됨.
- **방법 B (영구 적용)**: Edit Mode에서 전체 선택(A) → 우클릭 → **Subdivide**. 가중치를 칠하기 **전에** 하는 걸 추천 (이미 칠했다면 Blender가 자동 보간은 해주지만 경계 다듬기가 필요할 수 있음).

---

## 8. Part D — FBX Export 및 알려진 버그

### 8.1 Export 설정

1. Object Mode에서 Armature + 메쉬 전부 선택 (또는 아예 **Limit to Selected Objects 체크 해제**해서 씬 전체를 내보내는 게 제일 안전 — 오브젝트 선택 누락으로 메쉬가 통째로 빠지는 실수를 방지)
2. Include → **Object Types**에서 Mesh, Armature 체크 확인
3. File > Export > FBX, 원본 파일 덮어쓰지 말고 새 이름으로 저장

### 8.2 ⚠️ 알려진 Blender 버그: Armature가 100배 스케일로 export됨

Blender FBX exporter의 **미해결 버그** (Blender 개발자 트래커 [#96332](https://developer.blender.org/T96332)). "Apply Transform" 옵션을 켜면 메쉬 스케일은 맞게 나오는데, **Armature 자체는 그대로 100배 스케일로 export되는 문제**가 있음. Scene Unit Scale, Object Scale, Export Scale을 전부 1.0으로 맞춰도 소용없음 — Blender 쪽에서 export 설정으로 못 고치는 버그임.

**증상**: Unity에서 Avatar Rig Configuration mismatch 에러가 뜨거나, 캐릭터가 화면에서 완전히 안 보이게 됨 (스키닝 바인드 포즈까지 깨짐).

**대응**: 이 버그는 Blender 쪽에서 근본적으로 못 고침. Export 후 Unity에서 문제가 재현되면, 파일을 담당자(Claude 세션 로그 참고 또는 아래 "복구 스크립트 로직" 참고)에게 전달해서 사후 보정을 받는 게 현재까지는 제일 확실한 방법. 사후 보정 내용:
- Armature/메쉬 오브젝트의 `Lcl Rotation`, `Lcl Scaling` 속성을 원본 파일 값과 비교해서 정규화
- 스킨 클러스터의 `TransformLink`(바인드 포즈) 행렬도 같은 배율로 재조정

### 8.3 Export 후 체크리스트 (Unity에서 열기 전에 알아둘 것)

| 증상 | 원인 | 확인/대응 |
|---|---|---|
| Avatar Rig Configuration mismatch 에러 | Armature 회전값이 원본(iClone) 컨벤션과 다름, 또는 8.2의 스케일 버그 | Rig 탭에서 Animation Type을 None → Humanoid로 재설정해서 캐시 초기화. 그래도 안 되면 8.2 대응 |
| 본 추가 직후 캐릭터가 안 보임 | 6.4절 — Deform 켜진 미가중치 본이 클러스터를 깨뜨림 | 웨이트 다시 확인, 순서 지켜서 재export |
| 캐릭터가 통째로 안 보임(본 문제 없어도) | 8.2 스케일 버그로 바인드 포즈까지 깨짐 | 8.2 대응 |
| 재질이 비어있거나 새까맣게 나옴 | 아래 8.4절 | 8.4절 참고 |

### 8.4 ⚠️ Shape Key 있는 메쉬는 머티리얼이 export에서 빠질 수 있음

`m_head`, `m_body`처럼 Shape Key(Key.001 등)가 있는 메쉬는 Blender FBX exporter가 머티리얼을 통째로 안 담아버리는 경우가 있음 (Shape Key가 없는 `ribbon`, `antenna` 같은 오브젝트는 정상). 확인 순서:

1. 머티리얼 슬롯에 실제로 이름이 들어있는지 (Material Properties 탭)
2. Edit Mode에서 그 슬롯 선택 → **Select** 버튼 눌러서 전체 면이 선택되는지 (일부만 선택되면 재할당 필요: 전체 선택 후 **Assign**)
3. 머티리얼 이름 옆 Link 설정이 **Data**로 되어있는지 (Object로 되어있으면 Data로 변경 — Shape Key 메쉬는 Object 링크일 때 export가 깨지는 게 Blender의 알려진 제약)
4. 위 세 개 다 정상인데도 안 되면: FBX Export 창의 **Apply Modifiers** 체크를 반대로 바꿔서 재시도, 또는 실제로 안 쓰는 Shape Key라면 삭제 후 재export

---

## 9. Part E — Unity 세팅

### 9.1 Import 설정

1. Rig 탭 → **Optimize Game Objects 체크 해제** (본 GameObject가 하이러키에 실제로 노출되어야 CheekPullHandler가 참조 가능)
2. Materials 탭 → Material Creation Mode: **Import via MaterialDescription**
3. Materials 탭 → Location: **Use External Materials (Legacy)**, Search: **Recursive-Up** (기존 Toon 셰이더 머티리얼 애셋과 이름으로 자동 매칭됨)

### 9.2 프리팹에서 본 찾기

프리팹 하이러키에서 `Armature > ... > CC_Base_Head` 밑에 `Character_Ball_L`, `Character_Ball_R`이 보이는지 확인.

### 9.3 CheekPullHandler 부착 (좌/우 각각 따로, 총 2번)

각 본에 대해 동일하게 반복:

1. `Character_Ball_L`(또는 `_R`) 선택
2. Add Component → **Sphere Collider**, Radius를 볼 크기에 맞게 조절 (0.02~0.05부터 시작해서 Scene 뷰 보면서 조정)
3. Add Component → **Cheek Pull Handler**
   - `Cheek Bone`: 비워두면 자기 자신을 움직임 (콜라이더를 본에 직접 달았으면 비워도 됨)
   - `Max Pull Distance`: 기본 0.06, 캐릭터 스케일에 맞게 조절
   - `Return Duration`: 기본 0.2초
   - `Required Feature Tag`: 기본값 `볼당기기`
   - `Pull Emotion` / `Release Emotion`: 기본값 `><` / `idle` (이미 프로젝트에 있는 표정, 새로 만들 필요 없음)
   - `Animator Bool Name`: 기본값 `isCheekPull` (Animator Controller에 같은 이름의 Bool 파라미터가 없으면 그냥 무시되니 없어도 에러 안 남)

좌/우는 이 컴포넌트를 각자 오브젝트에 따로 붙이는 것만으로 완전히 독립적으로 동작함 (서로 값을 공유하지 않음).

### 9.4 CharAttributes 태그 추가 (필수, 캐릭터 루트에 한 번만)

1. 프리팹 루트(`CharAttributes` 컴포넌트가 있는 최상위) 선택
2. `Feature Tags` 리스트에 `+`로 **`볼당기기`** 추가
3. 이걸 안 하면 `Start()`에서 좌/우 두 본 모두 자동으로 비활성화됨 (컴포넌트 자체와 콜라이더까지 꺼짐)

### 9.5 3D 드래그 인프라 확인 (신규 씬/프리팹일 때만)

기존 `Root260616.prefab` 기반 씬에는 이미 세팅되어 있음. 완전히 새 테스트 씬에서 작업 중이라면:

1. 씬에 **EventSystem** 오브젝트가 있는지 확인 (없으면 GameObject > UI > Event System)
2. 사용 중인 카메라에 **Physics Raycaster** 컴포넌트가 붙어있는지 확인

### 9.6 테스트

1. Play 모드
2. 볼 콜라이더 위치에서 마우스 좌클릭 드래그 → 당겨지고 놓으면 탄성 있게 복귀하는지 확인
3. 좌/우 각각 따로 테스트해서 서로 독립적으로 동작하는지 확인
4. `Character_Ball_L`/`R` 선택한 채로 Inspector에서 Collider·CheekPullHandler 컴포넌트 체크박스가 켜져 있는지 확인 (꺼져 있으면 9.4의 태그 문제)

---

## 10. 트러블슈팅 표 (전체 요약)

| 증상 | 원인 | 해결 |
|---|---|---|
| Symmetrize가 좌우가 아니라 앞뒤로 미러됨 | Armature 오브젝트에 회전값이 안 지워짐 | 5.1절 — Apply Rotation |
| 본 추가 직후 캐릭터가 화면에서 사라짐 | Deform 켜진 미가중치 본 | 6.4절 — 웨이트 먼저 칠하고 Deform 켜기 |
| Avatar Rig Configuration mismatch 에러 | 회전/스케일 컨벤션 불일치 | 8.2, 8.3절 |
| 재질이 비거나 새까맣게 나옴 | Shape Key + 머티리얼 Object 링크 | 8.4절 |
| Unity에 새 fbx 올려도 예전 파일이 계속 잡힘 | 파일명 중복 시 캐시됨 | 매번 새 파일명 사용 |
| 볼 오브젝트에 콜라이더 붙였는데 클릭해도 반응 없음 | `볼당기기` 태그 누락 / EventSystem·PhysicsRaycaster 없음 | 9.4, 9.5절 |
| Blender 뷰포트엔 잘 보이는데 Unity에서 이상함 | Blender 뷰포트 표시와 export 결과는 별개 — 항상 export 후 Unity에서 직접 확인 | — |

---

## 11. 부록 — CheekPullHandler.cs 필드 레퍼런스

| 필드 | 설명 | 기본값 |
|---|---|---|
| `cheekBone` | 실제로 움직일 본. 비우면 이 컴포넌트가 붙은 오브젝트 자신 | 비움 |
| `maxPullDistance` | 본 로컬 기준 최대 이동 거리(미터) | 0.06 |
| `returnDuration` | 놓았을 때 복귀 시간(초), ease-out-back으로 살짝 튕기며 복귀 | 0.2 |
| `followSpeed` | 드래그 중 마우스 추종 부드러움 | 30 |
| `requiredFeatureTag` | `CharAttributes.featureTags`에 이 태그가 있어야 동작 | `볼당기기` |
| `pullEmotion` | 당기는 동안 표정 | `><` |
| `releaseEmotion` | 놓았을 때 표정 | `idle` |
| `animatorBoolName` | 애니메이터 Bool 파라미터 (없으면 무시) | `isCheekPull` |

스크립트 자체는 수정할 필요 없음 — 본 이름을 `Character_Ball_L`/`Character_Ball_R`로 맞추고 위 세팅만 따라 하면 그대로 동작하도록 이미 작성되어 있음.

## 12. 참고 링크

- Blender 개발자 트래커 #96332 — Armature scale export 버그: https://developer.blender.org/T96332
- Blender 개발자 포럼 — Armature 회전/스케일 관련: https://devtalk.blender.org/t/export-fbx-the-armatures-rotation-and-scale/6054
