# EquipSystem — 메시-퍼스트 최종 스펙

> 2026-07-09. 워크플로우 산출물: 메시 글라이드 설계 → 적대 그릴 → 종합. 철학 전환 확정본.

# 메시-퍼스트 최종 스펙

설계와 그릴(적대 검증)을 종합한 구현 확정본. 그릴의 코드 인용 검증은 전수 통과했고, 본 스펙 작성 시 `EquipPlaceholder.cs` / `EquipPlacement.cs` / `EquipPlaceholderEditor.cs` / `EquipAuthoringUtil.cs` / `EquipPhysicsBoneFilter.cs`를 재대조해 시그니처·라인 참조가 현행 코드와 일치함을 확인했다.

---

## 1. 철학 판정 (죽는 것 / 사는 것 — 정직하게)

**"캡슐 철학은 전부 개똥철학" 자평은 틀렸다.** 죽는 것은 캡슐이라는 *물건*이지 철학의 *골격*이 아니다.

### 죽는 것
- **캡슐 = 저작 표면 프록시.** 글라이드가 실제 메시 표면을 직접 미끄러지므로 "캡슐 테두리를 미끄러진다"는 근사가 소멸. "스타트 지점 보조"조차 불필요 — 표준 시드도 메시 레이로 놓는 게 더 정확하다(M4).
- **캐릭터별 캡슐 피팅 노동.** radius/height/center를 그 캐릭터 머리에 맞추는 수작업이 메시 레이캐스트("표면에서 직접 읽기")로 대체된다. "피팅 0" 가설은 헤어 스파이크 방어(다중 레이 중앙값)와 무히트 폴백 사다리를 갖추는 조건 하에 성립.
- **axisT/dirLocal/radiusScale 좌표계의 1급 지위.** 원본 인코딩 자리를 메시 인코딩에 내주고 폴백으로 강등(M2).

### 사는 것 (사용자 철학의 골격 — 메시 기반으로 정화되어 승계)
- **"표면점 + 방향 + 반경배율"이라는 무차원 인코딩 발상.** M2의 `dirRootFrame + distScale`은 `dirLocal + radiusScale`의 동형 사상이다. radiusScale 1=표면 / 1.6=부유(천사링) 시맨틱이 distScale에 문자 그대로 이식된다.
- **orientation(SurfaceAligned/SocketFrame), contactAnchor(BottomAlign), rotationOffsetEuler, radius-배수 오프셋, RadiusRelative 사이징** — 전부 기질(substrate)이 캡슐이든 메시든 불변이라 그대로 산다. `EquipPlacement.FitToPlaceholder`의 BottomAlign/offsetRadii 로직은 한 글자도 안 바뀐다.
- **"Transform은 파생 캐시, 무차원 좌표가 원본"이라는 아키텍처 결정.** 바로 이 결정 덕분에 hitDist를 저작 시점에 베이크하면 **런타임에서 메시 레이캐스터가 완전히 제거**된다(레이캐스터 = 100% Editor 전용). 런타임 `FitToPlaceholder`가 캡슐에서 얻는 것은 `rWorld` 하나뿐(EquipPlacement.cs:31)이라는 코드 사실이 이를 뒷받침한다.

### 공격 처리 대장

| 그릴 공격 | 판정 | 처리 |
|---|---|---|
| 1. hierarchyChanged × RefitPreview 리페어런팅 = 캐시 스래시 | **유효 (치명)** | §2.1 무효화 재설계 — M1 필수 |
| 2. 헤일로 이름 패턴 1개 의존 | **유효 (치명)** | M1: 패턴 확장 + public 승격. 구조적 필터(지배 본 기반)는 M2 규칙 B 전제 조건 |
| 3. 규칙 B 최외곽+K=2.0 전역 정책 양방향 취약 | **유효 (중대)** | M2: placeholder별 `hitPolicy` enum (§3) |
| 4. 좌우 비대칭 헤어 → side_l/r 크기 불일치 | **유효 (중대)** | M2: `sizeBasis`에 `SocketShared` 추가 (§3) |
| 5. 스키닝 배열 가드 부재 (bindposes≠bones, boneIndex 범위 밖) | **유효 (크래시)** | §2.1 가드 + 렌더러 단위 격리 — M1 필수 |
| 6. M4 캡슐 미생성이 살아있는 레거시 폴백 폭파 | **유효 (중대)** | M4 보류. 캡슐 필수 생성 유지 (§4) |
| 7. MeshRay에서 ApplyToTransform 시맨틱 미정의 | **유효** | M2: no-op 명문화 + 에디터 라우팅 (§3) |
| 8. BeginChangeCheck / Undo 그룹 / AcquireSocketGo Undo / charRoot 폴백 | **유효 (디테일)** | §2.2, §2.3에 전부 반영 |

**기각된 공격 (사유 명시):**

| 공격 후보 | 기각 사유 |
|---|---|
| 반투명 헤어카드 겹침이 커서 글라이드를 망친다 | 규칙 A 최근접 + Alt+휠 히트 사이클 + 깊이비율 클램프로 방어. 헤어카드를 대상에 포함시킨 판단(모자는 머리카락 위)이 옳다 |
| 치마 안쪽/이중면 오배치 | 최근접 히트 + "레이 대면 노멀 강제 플립"이 원천 차단 — 커서가 보는 면이 곧 저작면 |
| 음수 스케일/미러 립에서 winding 반전 | winding 컬링을 아예 포기했으므로 무해. 수동 스키닝 위치 계산은 음수 스케일 행렬에서도 정확 (`LossyAvg`가 Abs를 쓰는 것이 음수 스케일 실재의 증거 — EquipAuthoringUtil.cs:361) |
| 본 피벗 높이 차(Bip001 vs mixamorig)가 이식을 깨뜨림 | 메시 인코딩은 레이가 그 캐릭터의 실제 표면에서 종결되므로 피벗 차는 접선 방향 오차로만 남음 — 현행 `rootDirFromBone×height`보다 오히려 강건 |
| BakeMesh가 더 간단하다 | 기각 유지 — 루트1/본lossy35/루트20000 혼합 환경에서 출력 공간이 `useScale` 인자에 따라 모호 + 호출마다 Mesh 할당. 수동 스키닝은 공간이 명시적(→월드) |
| 브루트포스 성능 부족 → BVH 필요 | 2단 AABB 조기 탈락(렌더러+1024-tri 청크)으로 캐스트당 1~2ms — BVH는 과설계로 기각 |
| 지배 본이 물리 본(hair)이 되는 사고 | `IsPhysicsSuspect` 재사용 + 차순위→조상 승격 사다리로 방어. 루트 도달 시 종료 가드만 명시 (§2.1.6에 반영) |
| M1의 캡슐 인코딩 캡처가 메시 글라이드 위치를 손실시킴 | `Encode`는 axisT 클램프 잔여를 radial 벡터가 흡수해 Decode가 원점을 정확 복원(EquipCapsuleMath.cs:45-97) — 메시 위 어디로 글라이드해도 라운드트립 무손실 (그릴 검증 완료) |
| Job/Burst 필요 | 캐시 빌드 30~80ms는 선택 진입 시 1회 — `Progress` 표시로 충분, 과설계 기각 |

---

## 2. M1 구현 스펙 — 글라이드 저작 (이번에 만들 것)

**범위 원칙: 데이터 모델 불변.** 캡처는 여전히 캡슐 인코딩(`CaptureFromTransform` 그대로), 메시는 순수 저작 표면. 런타임 어셈블리 파일은 **1개도 수정하지 않는다.** vibe 규칙(삼항 금지, 중괄호 if-else, `//` 한줄 주석, 지연초기화 싱글톤) 전 파일 적용.

| 파일 | 상태 | 책임 |
|---|---|---|
| `Editor/EquipMeshRaycaster.cs` | **신규** | 스키닝 캐시 + 레이캐스트(규칙 A) + 지배 본 질의 |
| `Editor/EquipPlaceholderEditor.cs` | 수정 | SnapMode 3모드, 글라이드 드래그, 히트 사이클, Esc/Undo |
| `Editor/EquipSocketAuthorWindow.cs` | 수정 | [표면 클릭] 소켓 생성 모드 |
| `Editor/EquipAuthoringUtil.cs` | 수정 | 제외 패턴 public 승격+확장, `ResolveCharRoot` 추가 |

### 2.1 `EquipMeshRaycaster.cs` (Editor 전용, 지연초기화 싱글톤)

#### 2.1.1 공개 API

```csharp
public struct EquipMeshHit
{
    public Vector3 point;       // 월드
    public Vector3 normal;      // 월드, 레이 대면으로 플립·정규화됨
    public float distance;
    public Renderer renderer;
    public int triangleIndex;   // 렌더러-로컬 삼각형 인덱스
}

public class EquipMeshRaycaster
{
    private static EquipMeshRaycaster instance;
    public static EquipMeshRaycaster Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new EquipMeshRaycaster();
            }
            return instance;
        }
    }

    // 규칙 A: 모든 전방 히트를 거리 오름차순 수집. true = 1개 이상 히트
    public bool RaycastAll(Transform charRoot, Ray ray, List<EquipMeshHit> results);

    // 편의: hitIndex번째 히트 선택(범위 밖이면 클램프). hitCount로 사이클 UI 지원
    public bool RaycastCursor(Transform charRoot, Ray ray, int hitIndex, out EquipMeshHit hit, out int hitCount);

    // 히트 삼각형의 지배 본 (물리 필터 승격 사다리 포함). 비스킨 Entry면 renderer.transform 기반
    public Transform QueryDominantBone(Transform charRoot, EquipMeshHit hit);

    public bool HasCache(Transform charRoot);   // 캐시 빌드 가능 여부 (SnapMode 자동 강등 판단용)
    public void Invalidate();                   // 명시적 전체 무효화 ([캐시 갱신] 버튼)
}
```

#### 2.1.2 수집 대상 / 제외 규칙

`charRoot.GetComponentsInChildren<Renderer>(false)` (활성 GO만) 후 제외:
- `r.enabled == false` (컴포넌트 disable 방식 대체 의상 — `MeasureBounds`와 동일 규칙)
- `EquipAuthoringUtil.ExcludeNamePatterns` 이름 매칭 — **기존 private `BoundsExcludePatterns`를 public으로 승격하고 확장**: `{ "halo", "ハロ", "光輪", "天使の輪" }` (그릴 공격 2 부분 반영. "circle"/"wa"는 Contains 매칭에서 오탐 위험이 커서 제외 — 구조적 필터는 M2 규칙 B에서)
- `r.GetComponentInParent<EquipMarker>() != null` (장착된 악세서리)
- `EquipAuthoringUtil.IsSocketOrChildOfSocket(r.transform)` (소켓/placeholder 하위)
- 이름에 `__EquipPreview__` 포함 (라이브 미리보기)
- `MeshRenderer`인데 `MeshFilter.sharedMesh == null`
- 서브메시 topology가 `MeshTopology.Triangles`가 아니면 해당 서브메시만 스킵

반투명 헤어카드는 **포함** (모자는 머리카락 위에 얹히는 게 정답).

#### 2.1.3 캐시 구조

```csharp
private class MeshEntry
{
    public Renderer renderer;
    public Vector3[] worldVerts;     // 스키닝 완료 월드 정점
    public int[] tris;               // 서브메시 이어붙인 삼각형 인덱스 (렌더러-로컬)
    public Bounds worldBounds;       // 조기 탈락 1차
    public Bounds[] chunkBounds;     // 1024-tri 청크 AABB — 조기 탈락 2차
    public bool skinned;             // SMR 여부
    public int[] weightStart;        // 정점별 BoneWeight1 스트림 시작 오프셋 (누적합) — 지배 본 질의용
    public Vector3 boneSentinel;     // 샘플 본 4개 position 합 (수동 포즈 변경 감지)
}

private class CharCache
{
    public int rootInstanceId;
    public List<MeshEntry> entries;
    public int rendererSetHash;      // 수집 대상 렌더러 instanceID 정렬 해시
    public bool dirty;               // hierarchyChanged 시 파기 대신 이것만 set
    public float charHeight;         // EquipAuthoringUtil.MeasureCharHeight — epsilon 기준
}
```

캐시는 static 딕셔너리가 아니라 **마지막 charRoot 1개만 유지** (`private CharCache cache;` — LRU 불필요). 메모리 ~2MB 수준.

#### 2.1.4 무효화 재설계 (그릴 공격 1 반영 — M1 성립의 전제)

`EquipPlacement.FitToPlaceholder`는 매 호출마다 미리보기를 `SetParent(null)`→`SetParent(placeholder)`로 재부모화한다(EquipPlacement.cs:18, 62). 글라이드 드래그 중 매 히트마다 `RefitPreview`가 돌므로, hierarchyChanged에서 즉시 파기하면 **드래그 이벤트당 30~80ms 풀 리스키닝**으로 죽는다. 따라서:

- `EditorApplication.hierarchyChanged` → **`cache.dirty = true`만 세운다. 절대 즉시 파기하지 않는다.**
- 다음 `RaycastAll` 진입 시 dirty면:
  - `GUIUtility.hotControl != 0` (드래그 세션 중) → **검사 자체를 유예**하고 기존 캐시 사용.
  - 아니면 현재 수집 대상 렌더러 목록(§2.1.2 필터 적용 후)의 instanceID 정렬 해시를 재계산 → `rendererSetHash`와 같으면 dirty 해제하고 캐시 유지, 다르면 리빌드. `__EquipPreview__`/`EquipMarker` 하위는 수집에서 제외되므로 **미리보기 리페어런팅은 해시를 바꾸지 못한다** — 자기 유발 변경이 자동 무시된다.
- `Undo.undoRedoPerformed` → 전체 무효화 (본 위치가 바뀔 수 있음).
- `PrefabStage.prefabStageOpened` / `prefabStageClosing` → 전체 무효화.
- **센티널**: 드래그 세션이 아닐 때의 캐스트 직전, Entry별 샘플 본 4개 position 합을 `boneSentinel`과 비교 — 불일치 시 **그 Entry만** 재스키닝 (사용자가 본을 손으로 움직인 케이스의 저비용 안전망).
- 명시적 `Invalidate()` — placeholder 인스펙터의 [메시 캐시 갱신] 버튼.

#### 2.1.5 수동 스키닝 (BakeMesh 금지 유지 + 그릴 공격 5 가드)

```csharp
Mesh mesh = smr.sharedMesh;
Matrix4x4[] bind = mesh.bindposes;
Transform[] bones = smr.bones;

// [가드 1] Gmod/MMD 립: bindposes.Length ≠ bones.Length 실재 → 큰 쪽으로 잡고 짝 없는 슬롯은 폴백
int skinCount = Mathf.Max(bones.Length, bind.Length);
Matrix4x4[] skin = new Matrix4x4[skinCount];
Transform fallbackBone = smr.rootBone;
if (fallbackBone == null)
{
    fallbackBone = smr.transform;
}
for (int i = 0; i < skinCount; i++)
{
    Matrix4x4 bp = Matrix4x4.identity;
    if (i < bind.Length)
    {
        bp = bind[i];
    }
    if (i < bones.Length && bones[i] != null)
    {
        skin[i] = bones[i].localToWorldMatrix * bp;   // 메시(바인드) 공간 → 월드
    }
    else
    {
        skin[i] = fallbackBone.localToWorldMatrix * bp;  // 삭제된 물리 본 등
    }
}

// Read/Write 플래그 무관하게 에딧모드 안전 + 무할당
using (Mesh.MeshDataArray mda = Mesh.AcquireReadOnlyMeshData(mesh))
{
    // GetVertices + GetBonesPerVertex/GetAllBoneWeights(BoneWeight1 스트림, 4본 제한 없음)
    // 정점별:
    //   acc = Σk skin[bw.boneIndex] · p · bw.weight   (단 [가드 2] boneIndex >= skinCount면 그 인플루언스 스킵)
    //   wsum > 1e-4f → worldVerts[v] = acc / wsum     (MMD 립 웨이트 합≠1 정규화)
    //   아니면       → smr.transform 행렬로 직변환    (무웨이트 정점 폴백)
    // 스트림 오프셋 누적합을 weightStart[]에 저장 (지배 본 질의용)
}
```

- **[가드 3] 렌더러 단위 try-격리**: Entry 1개 빌드가 예외를 던지면 경고 로그 후 그 Entry만 버리고 나머지로 동작 — 불량 립 1개가 글라이드 전체를 봉쇄하지 못하게.
- 비스킨 `MeshRenderer`: `transform.localToWorldMatrix.MultiplyPoint3x4(v)` 일괄 변환, 같은 Entry 구조.
- 블렌드셰이프: 에딧모드 weight 0 전제로 **무시** (치비 표정용 — 부착 표면과 무관). 한계로 문서화.
- 빌드 시간 30~80ms(10만 정점) — `UnityEditor.Progress` 표시, Job/Burst 없음.

#### 2.1.6 레이캐스트 — Möller–Trumbore, 규칙 A만 (규칙 B는 M2)

수치 환경(월드 키 ~240유닛, 행렬 원소 ~2e4)에서 float 안전. **epsilon은 절대값 금지, 스케일 상대**:

```csharp
// 삼각형별 (2단 AABB 조기 탈락 통과 후)
Vector3 e1 = v1 - v0;
Vector3 e2 = v2 - v0;
Vector3 p = Vector3.Cross(dir, e2);          // dir은 정규화 보장
float det = Vector3.Dot(e1, p);
if (Mathf.Abs(det) <= 1e-9f * e1.magnitude * e2.magnitude)
{
    continue;  // 평행/퇴화 — winding 컬링은 하지 않는다 (음수 스케일 립에서 반전되므로)
}
// u/v 배리센트릭 검사 후:
float t = Vector3.Dot(e2, q) / det;
if (t <= 1e-5f * cache.charHeight)
{
    continue;  // 자기 표면 재히트 방지 하한
}
Vector3 n = Vector3.Cross(e1, e2);
if (Vector3.Dot(n, dir) > 0f)
{
    n = -n;    // 레이 대면 강제 플립 — 이중면/미러 립 원천 방어
}
```

- 조기 탈락: ① `Entry.worldBounds.IntersectRay(ray)` ② 통과 시 1024-tri 청크 AABB → 통과 청크만 M–T. 전형 5~15% 검사 → 캐스트당 1~2ms.
- 결과는 거리 오름차순 정렬 리스트. `RaycastCursor`는 `hitIndex` 클램프 선택.

#### 2.1.7 지배 본 질의 (`QueryDominantBone`)

1. 히트 삼각형 정점 3개 → `weightStart` 오프셋으로 BoneWeight1 스트림 접근 → 본 인덱스별 웨이트 합산 → 내림차순 후보 목록.
2. `EquipPhysicsBoneFilter.CollectPhysicsBones(charRoot)` + `IsPhysicsSuspect`로 필터: 첫 비의심 후보 채택 (헤어카드 클릭 → 대개 hair 본이 1위지만 head가 2위로 살아있음).
3. 후보 전멸 시 최대 웨이트 본의 `parent` 사다리로 첫 비의심 조상 승격. **charRoot 도달 시 종료** — 그 경우 최대 웨이트 본을 그대로 반환하고 경고 로그 (그릴 지적 가드).
4. 비스킨 Entry: `renderer.transform`이 부착 본. 그것도 물리 의심이면 3의 사다리.

### 2.2 `EquipPlaceholderEditor.cs` 확장 — 글라이드

- `private static bool snapToSurface` → `public enum EquipSnapMode { Mesh, Capsule, Free }` + `private static EquipSnapMode snapMode = EquipSnapMode.Mesh;` (세션 공유, 기본 Mesh).
- `HasCache()` 실패(수집 렌더러 0개 등) 시: 인스펙터 HelpBox "메시 없음 — 캡슐 모드로 전환됨" + Capsule 자동 강등. **Capsule 모드는 현행 코드 경로 그대로(변경 0), Free는 스냅 없음.**
- charRoot 결정 — `Editor/EquipAuthoringUtil.cs`에 추가 (`?.`/삼항 금지 vibe 준수, 그릴 공격 8d 반영):

```csharp
// placeholder가 속한 캐릭터 루트: Animator 우선, 없으면 렌더러 보유 조상 중
// "이웃 캐릭터를 삼키기 직전"의 마지막 조상 (여러 마스코트 공용 부모 방어)
public static Transform ResolveCharRoot(Transform t)
{
    Animator anim = t.GetComponentInParent<Animator>();
    if (anim != null)
    {
        return anim.transform;
    }
    Transform best = t;
    Transform cur = t.parent;
    while (cur != null)
    {
        // 조상이 Animator를 2개 이상 품으면 캐릭터 경계를 넘은 것 — 중단
        if (cur.GetComponentsInChildren<Animator>(true).Length >= 2)
        {
            break;
        }
        if (cur.GetComponentInChildren<Renderer>(false) != null)
        {
            best = cur;
        }
        cur = cur.parent;
    }
    return best;
}
```

- **드래그 처리 (Mesh 모드)** — 그릴 공격 8a 반영: `EventType.MouseDrag` 직접 검사는 FreeMoveHandle이 이벤트를 소비한 뒤라 잡히지 않으므로 `BeginChangeCheck`로:

```csharp
// OnSceneGUI — snapMode == Mesh
Event e = Event.current;
int id = GUIUtility.GetControlID(FocusType.Passive);

EditorGUI.BeginChangeCheck();
Handles.FreeMoveHandle(id, ph.transform.position,
    HandleUtility.GetHandleSize(ph.transform.position) * 0.1f, Vector3.zero, Handles.SphereHandleCap);
bool handleMoved = EditorGUI.EndChangeCheck();

// 이동 툴 병행: 기존 lastLocalPos 감지 경로 유지하되 위치를 그대로 쓰지 않고 같은 커서 레이로 재투영
bool toolMoved = ph.transform.localPosition != lastLocalPos;

if (handleMoved || toolMoved)
{
    Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
    EquipMeshHit hit;
    int hitCount;
    if (EquipMeshRaycaster.Instance.RaycastCursor(charRoot, ray, hitIndex, out hit, out hitCount))
    {
        Undo.RecordObject(ph.transform, "Glide Placeholder");
        ph.transform.position = hit.point;
        ph.transform.rotation = Quaternion.LookRotation(TangentOf(hit.normal), hit.normal);  // up = 노멀
        lastLocalPos = ph.transform.localPosition;
        RefitPreview(ph);   // WYSIWYG — 기존 흐름 재사용, 캐시는 §2.1.4로 스래시 면역
    }
    else
    {
        // 실루엣 이탈: 직전 유효 위치 유지 + 빨간 점선 피드백. 캡슐 폴백 순간이동 금지
        surfaceMissed = true;
    }
}
```

  `TangentOf(up)`: `Cross(up, Vector3.right)`, 퇴화 시 `Cross(up, Vector3.forward)` — `ComputeBaseRotation`과 동일 규약(라운드트립 일관성).
- **Undo 단위** (그릴 공격 8b): 드래그 시작(hotControl == id 최초 감지)에 `undoGroup = Undo.GetCurrentGroup()` + pre-drag 스냅샷(localPosition/localRotation) 저장. **MouseUp = 확정**: `Undo.RecordObject(ph)` → `ph.CaptureFromTransform()`(캡슐 인코딩 — M1 무손실, 그릴 검증됨) → `EditorUtility.SetDirty` → `Undo.CollapseUndoOperations(undoGroup)` (Ctrl+Z 1회 = 드래그 1회).
- **Esc = 취소**: `e.keyCode == KeyCode.Escape && GUIUtility.hotControl == id` → 스냅샷 복원, `GUIUtility.hotControl = 0`, `e.Use()`.
- **앞뒤 중첩 사이클**: 드래그 중 `e.type == EventType.ScrollWheel && e.alt` → `hitIndex` 증감 + `e.Use()`. 히트 개수가 바뀌면 깊이 비율 유지 클램프(귀 뒤를 잡은 채 미끄러질 때 앞면으로 튀지 않게). `Handles.BeginGUI()` 배지로 "표면 2/3" 표시.
- 인스펙터 추가: 스냅 모드 셀렉터 + [메시 캐시 갱신] 버튼.

### 2.3 `EquipSocketAuthorWindow.cs` — [메시에서 소켓 만들기]

- 슬롯 행에 [표면 클릭] 토글. 켜지면 `SceneView.duringSceneGui` 구독 + `HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive))`로 셀렉션 클릭 삼킴. Esc로도 해제.
- 다음 클릭 시퀀스:
  1. 규칙 A 캐스트(최근접) → 히트점 + `QueryDominantBone` (물리 승격 사다리 포함 — 헤어카드 클릭 → head 본).
  2. `AcquireSocketGo` 재사용해 지배 본 밑에 `Socket_<slotId>` 생성 — **private → internal 승격 + `bool interactive` 파라미터 추가** (그릴 공격 8c): interactive 경로는 `Undo.SetTransformParent` + `Undo.RegisterCreatedObjectUndo` 사용 (배치 LoadPrefabContents 경로는 기존 그대로). 소켓 위치 = 본 원점, 회전 = rootRot. 본 GO 낚아채기 방지 로직 그대로.
  3. **캡슐 생성 (M1은 캡슐 인코딩이 원본이므로 필수)**: `hitDist = |hit.point − bone.position|` → `EquipAuthoringUtil.SetCapsuleByWorldLength(socketGo, 3f * hitDist, 1)` — 월드 radius ≈ hitDist가 되어(기존 radius=0.33×height 규약) 캡슐이 실제 메시 치수에서 유도된다. 캡슐 피팅 수작업의 M1식 반감.
  4. placeholder 1개를 히트점에 생성, up = 히트 노멀, 즉시 `CaptureFromTransform()`.
  5. 토글 자동 해제 + "라이브 미리보기로 확인하세요" 로그.
- 기존 [본 자동 제안]/[소켓 생성/이동](본 드래그)은 그대로 공존 — 클릭 생성은 추가 진입로.

### 2.4 M1에서 하지 않는 것

- 규칙 B(본 내부 출발 이식 레이), hitPolicy, sizeBasis, `bakedRefDistLocal`, space 필드 — 전부 M2.
- 데이터 포맷/런타임/카탈로그/스탬퍼 변경 0. 기존 저작물 100% 호환.

---

## 3. M2 예고 — 이식 인코딩 전환 (데이터 모델 diff만, 구현은 다음)

`EquipPlaceholder` 확장분 (그릴 공격 3·4·7 반영 완료):

```csharp
public enum EquipPlacementSpace { MeshRay, Capsule }
public enum EquipSizeBasis { LocalHitDist, CharacterHeight, SocketShared }
//  SocketShared: 소켓 canonical 레이(top) 1개의 hitDist를 좌우 placeholder가 공유 — 비대칭 헤어에서
//  side_l/side_r 크기 불일치(공격 4) 방어. back/origin 기본 = CharacterHeight(공격 3의 back 불안정 방어)
public enum EquipHitPolicy { Outermost, Nearest, NearestNonPhysicsDominant }
//  규칙 B 히트 선택을 placeholder별로: head 기본 Outermost(K캡), back 기본 NearestNonPhysicsDominant
//  (지배 본 질의를 히트 필터로 재사용 — 롱헤어/망토 뒷면을 등 표면으로 오인 방지, 공격 3)

public EquipPlacementSpace space = EquipPlacementSpace.MeshRay;  // 기존 저작물 마이그레이션 기본 = Capsule
public Vector3 dirRootFrame;        // 본 원점→점 방향, 캐릭터 루트 프레임 (정규화)
public float distScale = 1f;        // |점−본| / hitDist. 1=표면, 1.6=부유. 하한 1e-3
public Vector3 normalRootFrame;     // 캡처 시 히트 노멀 — SurfaceAligned의 up 소스
public float bakedRefDistLocal;     // hitDist의 부모-로컬 환산 베이크. 런타임이 소비하는 유일한 신규 값
public EquipSizeBasis sizeBasis = EquipSizeBasis.LocalHitDist;
public EquipHitPolicy hitPolicy = EquipHitPolicy.Outermost;
// 캡슐 인코딩(axisT/dirLocal/radiusScale)은 폴백 전용으로 전량 유지
```

계약 명문화 (공격 7): **`ApplyToTransform()`은 `space == MeshRay`면 명시적 no-op**(주석 포함) — MeshRay 재계산은 Editor 측 `EquipMeshPlacementUtil.Reapply(ph)` 전담. `EquipSocketEditor` [재배치]·`EquipPlaceholderEditor`의 `GUI.changed`/[좌표→Transform 재적용] 호출부는 space 분기로 라우팅(스테일 캡슐 좌표 텔레포트 차단). 런타임 유일 수정은 `FitToPlaceholder`의 rWorld 소스 3줄(`bakedRefDistLocal × LossyAvg(parent)` 우선, 캡슐 폴백) — 이후 `2f*rWorld*sizeRatio`/BottomAlign/offsetRadii는 불변, head 계열 sizeRatio 기존 튜닝값 의미 보존(hitDist≈캡슐 반경). M3에서 `EquipSlotDef.placeholders`(EquipPlaceholderDef 리스트) + 규칙 B 다중 레이(반각 8° 콘 5발 중앙값, 최대/중앙 비>1.5 → `MESH_MULTI`) + 무히트 폴백 사다리(레이 반전→캡슐→`NO_HIT`) + 헤일로 구조적 제외(지배 본이 head 계열 스킨 본이 아닌 분리 부속이면 규칙 B 대상 제외 — 공격 2 완결).

---

## 4. 캡슐 / 기존 Phase A 산출물의 최종 지위

**캡슐에 남는 역할**: ① 폴백 표면(규칙 B 무히트 슬롯, 캐시 실패 캐릭터) ② 레거시 호환(동결) ③ 가시화 보조(선택). **죽는 역할**: 저작 표면 프록시, 크로스 캐릭터 크기/좌표 근거.

**단, M4(신규 소켓 캡슐 미생성 기본)는 그릴 공격 6 해소 전까지 보류.** `EquipManager.Equip()`의 소켓 직부착 폴백은 "동결된 레거시"가 아니라 **현역 안전망**이다(EquipManager.cs:88-95) — 캡슐 없는 소켓에서 `Fit`이 `scale = 1×fitBias`로 떨어지면 본 lossy 35~루트 20000 환경에서 악세서리가 수만 배 크기로 폭발한다(EquipPlacement.cs:142-147). M4 착수 조건 = 폴백 경로를 "캡슐 없으면 장착 거부 + 경고"로 안전화한 뒤. 그때까지 **모든 신규 소켓은 캡슐 필수 생성 유지**(M1 클릭 생성도 §2.3-3처럼 캡슐을 만든다).

| Phase A 산출물 | 운명 |
|---|---|
| `EquipCapsuleMath.cs` | 유지 (폴백 + `LossyAvg` 공용) — 삭제 금지 |
| `EquipPlaceholder.cs` | M1 무수정 / M2 확장 (캡슐 필드 유지) |
| `EquipPlacement.FitToPlaceholder` | M1 무수정 / M2 rWorld 3줄만 |
| `EquipManager` / `EquipCatalog` | **전 마일스톤 무수정** |
| `EquipSlotStamper` ResolveBone 사다리 / KEEP_* 보호 / 배치 IO | 무수정 재사용 (M3에서 소비) |
| `EquipPhysicsBoneFilter` | 재사용 + 지배 본 필터로 소비처 확대 (M1) |
| `EquipAuthoringUtil` | 재사용 + `ExcludeNamePatterns` public 승격/확장 + `ResolveCharRoot` (M1) |
| `EquipPlaceholderEditor` / `EquipSocketAuthorWindow` | M1 확장 |
| `EquipSocketEditor` 표준 시드 | M4에서 메시 레이화 (보류 대상) |

---

## 5. 검증 계획 (M1을 사용자가 확인하는 방법)

에이전트 측: 구현 완료 시 표준 절차대로 **에디터 닫기 → batchmode 1회(컴파일 검증) → GUI 재오픈**, 로그에서 컴파일 에러 0 확인.

사용자 측 체크리스트 (캐릭터 프리팹을 프리팹 스테이지로 열고):
1. **글라이드 기본기**: placeholder 선택 → 인스펙터에 스냅 모드 셀렉터(기본 Mesh) 확인 → 씬의 구체 핸들 드래그 → 점이 **캡슐이 아니라 실제 머리/머리카락 표면**을 미끄러지고 up이 표면 노멀을 따라 기우는지.
2. **스래시 회귀 (공격 1 검증)**: 라이브 미리보기를 **켠 상태로** 드래그 — 모자가 실시간 재핏되며 끊김(수십 ms 히칭 연발)이 없는지. 콘솔에 캐시 리빌드 로그가 드래그 중 반복되면 실패.
3. **중첩 사이클**: 귀 뒤/헤어카드 틈에서 드래그 중 Alt+휠 → 씬 좌상단 "표면 n/m" 배지와 함께 앞뒤 표면 전환.
4. **실루엣 이탈**: 커서를 캐릭터 밖으로 → placeholder가 멈추고 빨간 점선 피드백, 순간이동 없음.
5. **Undo/Esc**: 드래그 후 Ctrl+Z 1회에 드래그 전체가 되돌아가는지(픽셀 단위 되감김이면 실패), 드래그 중 Esc로 원위치 복귀.
6. **표면 클릭 소켓 생성**: Socket Author에서 [표면 클릭] → **머리카락을 클릭**했을 때 소켓이 hair 물리 본이 아닌 head 본 밑에 생기는지 + 캡슐 크기가 머리에 얼추 맞는지.
7. **이종 립 크래시 내성**: Bip001 립과 mixamorig 립 각각에서 1~6 반복 — 콘솔 IndexOutOfRange 0건 (공격 5 검증). Entry 격리 경고가 떠도 글라이드는 계속 동작해야 함.
8. **호환성**: 기존 저작 placeholder를 Capsule 모드로 두고 종전 드래그 → 현행과 동일 동작(회귀 0).

---

## 6. 리스크 잔존 목록

| # | 리스크 | 상태 |
|---|---|---|
| 1 | 헤일로 명명 변형이 확장 패턴(`halo/ハロ/光輪/天使の輪`)을 빗나가는 립 | M1은 커서 글라이드라 사용자가 눈으로 회피 가능. **M2 규칙 B 전에 구조적 필터(지배 본 판별) 필수** — 미해결 상태로 M2 진입 금지 |
| 2 | MagicaCloth 런타임 흔들림 vs 에딧모드 정적 포즈 괴리 — 헤어 표면에 저작한 부착물이 런타임에 허공에 남을 수 있음 | 구조적 한계. M2의 `NearestNonPhysicsDominant`가 완화하나, "물리 메시 위 저작"은 저작자 책임으로 문서화 |
| 3 | 블렌드셰이프 무시 — 에딧모드 기본 weight≠0인 립이 오면 표면이 어긋남 | M1 한계로 문서화. 필요 시 `GetBlendShapeFrameVertices` delta 가산으로 확장 |
| 4 | 30만 정점급 립의 캐시 빌드 100~200ms 히칭 | Progress 표시로 수용. Burst 도입은 실사용 불만 접수 후 |
| 5 | `ResolveCharRoot` 휴리스틱 — Animator 없는 캐릭터가 공용 부모 밑에 다수 있는 씬에서 오판 가능 | Animator 2개 가드로 대부분 방어. 오판 시 [캐시 갱신]+프리팹 스테이지 저작 권장으로 회피 |
| 6 | 규칙 B의 K 캡/히트 정책 실측 미검증 (본 원점이 표면에 가까운 립의 K 배제 케이스 포함) | M2 리스크. `hitPolicy` enum으로 정책을 데이터화해 코드 수정 없이 조정 가능하게 설계됨 |
| 7 | 월드 수천 유닛 카메라에서 float 절대오차 ~1e-3유닛 | 부착점 용도로 충분 — 수용 |
| 8 | 레거시 폴백 지뢰(공격 6)는 "회피"만 된 상태(캡슐 필수 생성 유지)이지 "해체"되지 않음 | M4 착수 조건으로 명시 — 폴백 안전화(캡슐 없으면 장착 거부) 전까지 캡슐 미생성 금지 |

**마일스톤 순서 확정: M1(본 스펙) → M2 → M3 → [공격 6 해소] → M4.** 각 단계 종료 시 batchmode 1회 검증, 각각 독립 배포 가능.

관련 파일: `d:\unity\AICO\Assets\Prefabs\Assist\EquipSystem\Editor\EquipMeshRaycaster.cs`(신규), `Editor\EquipPlaceholderEditor.cs`, `Editor\EquipSocketAuthorWindow.cs`, `Editor\EquipAuthoringUtil.cs`, `Editor\EquipPhysicsBoneFilter.cs`, `Scripts\EquipPlaceholder.cs`, `Scripts\EquipPlacement.cs`, `Scripts\EquipManager.cs`, `Scripts\EquipCapsuleMath.cs`, (M2 신규 예정: `Editor\EquipMeshPlacementUtil.cs`)