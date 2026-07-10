using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

// 메시 히트 결과
public struct EquipMeshHit
{
    public Vector3 point;       // 월드 히트점
    public Vector3 normal;      // 월드 노멀 (레이 대면으로 플립·정규화)
    public float distance;      // 레이 원점부터 거리
    public Renderer renderer;   // 히트 렌더러
    public int triangleIndex;   // 렌더러-로컬 삼각형 인덱스 (tris/3 단위)
}

// 캐릭터 메시 CPU 레이캐스터 (에디터 전용).
// SkinnedMeshRenderer는 수동 스키닝(BakeMesh 금지 — 스케일 공간 모호), 비스킨은 transform 변환.
// 캐시는 마지막 캐릭터 1개만 유지, hierarchyChanged는 dirty만 세우고 드래그 중엔 검사 유예(스래시 방지).
public class EquipMeshRaycaster
{
    private static EquipMeshRaycaster instance;  // 싱글톤 인스턴스
    public static EquipMeshRaycaster Instance
    {
        get
        {
            if (instance == null)
            {
                // 인스턴스가 없으면 생성 + 에디터 이벤트 구독
                instance = new EquipMeshRaycaster();
                EditorApplication.hierarchyChanged += instance.OnHierarchyChanged;
                Undo.undoRedoPerformed += instance.InvalidateStatic;
                UnityEditor.SceneManagement.PrefabStage.prefabStageOpened += instance.OnStageChanged;
                UnityEditor.SceneManagement.PrefabStage.prefabStageClosing += instance.OnStageChanged;
            }
            return instance;
        }
    }

    private const int ChunkTris = 1024;  // 청크 AABB 단위 (조기 탈락 2차)

    // 렌더러 1개의 스키닝 캐시
    private class MeshEntry
    {
        public Renderer renderer;
        public Vector3[] worldVerts;      // 스키닝 완료 월드 정점
        public int[] tris;                // 서브메시 이어붙인 삼각형 인덱스
        public Bounds worldBounds;        // 조기 탈락 1차
        public Bounds[] chunkBounds;      // 조기 탈락 2차 (ChunkTris 단위)
        public bool skinned;              // SMR 여부
        public Transform[] bones;         // SMR 본 배열 (지배 본 질의)
        public int[] weightStart;         // 정점별 BoneWeight1 스트림 시작 오프셋 (길이 verts+1)
        public BoneWeight1[] weights;     // BoneWeight1 스트림 사본
        public Vector3 boneSentinel;      // 샘플 본 position 합 (수동 포즈 변경 감지)
    }

    // 캐릭터 1개의 캐시
    private class CharCache
    {
        public int rootInstanceId;
        public List<MeshEntry> entries = new List<MeshEntry>();
        public int rendererSetHash;
        public bool dirty;
        public float charHeight;
        public HashSet<Transform> physicsBones;  // 지배 본 필터용
    }

    private CharCache cache;  // 마지막 캐릭터 1개만 유지

    // ── 공개 API ──

    // 캐시 빌드 가능 여부 (수집 렌더러 존재)
    public bool HasCache(Transform charRoot)
    {
        EnsureCache(charRoot);
        return cache != null && cache.entries.Count > 0;
    }

    // 캐시 엔트리 수 (진단용): 0 = 필터 전멸, >0인데 miss = 조준 문제
    public int GetEntryCount(Transform charRoot)
    {
        EnsureCache(charRoot);
        if (cache == null)
        {
            return 0;
        }
        return cache.entries.Count;
    }

    // 전체 무효화 ([메시 캐시 갱신] 버튼 등)
    public void Invalidate()
    {
        cache = null;
    }

    // 모든 전방 히트를 거리 오름차순 수집. true = 1개 이상
    public bool RaycastAll(Transform charRoot, Ray ray, List<EquipMeshHit> results)
    {
        results.Clear();
        EnsureCache(charRoot);
        if (cache == null || cache.entries.Count == 0)
        {
            return false;
        }

        Vector3 dir = ray.direction.normalized;
        float tMin = 1e-5f * Mathf.Max(cache.charHeight, 1e-3f);  // 자기 표면 재히트 방지 (스케일 상대)

        foreach (MeshEntry entry in cache.entries)
        {
            // 조기 탈락 1차: 렌더러 AABB
            if (entry.worldBounds.IntersectRay(ray) == false)
            {
                continue;
            }

            int triCount = entry.tris.Length / 3;
            int chunkCount = entry.chunkBounds.Length;

            for (int c = 0; c < chunkCount; c++)
            {
                // 조기 탈락 2차: 청크 AABB
                if (entry.chunkBounds[c].IntersectRay(ray) == false)
                {
                    continue;
                }

                int triStart = c * ChunkTris;
                int triEnd = Mathf.Min(triStart + ChunkTris, triCount);

                for (int t = triStart; t < triEnd; t++)
                {
                    int i0 = entry.tris[t * 3];
                    int i1 = entry.tris[t * 3 + 1];
                    int i2 = entry.tris[t * 3 + 2];

                    Vector3 v0 = entry.worldVerts[i0];
                    Vector3 v1 = entry.worldVerts[i1];
                    Vector3 v2 = entry.worldVerts[i2];

                    // Möller–Trumbore (winding 컬링 없음 — 미러 립 대응)
                    Vector3 e1 = v1 - v0;
                    Vector3 e2 = v2 - v0;
                    Vector3 p = Vector3.Cross(dir, e2);
                    float det = Vector3.Dot(e1, p);

                    if (Mathf.Abs(det) <= 1e-9f * e1.magnitude * e2.magnitude)
                    {
                        continue;  // 평행/퇴화
                    }

                    float invDet = 1f / det;
                    Vector3 s = ray.origin - v0;
                    float u = Vector3.Dot(s, p) * invDet;
                    if (u < 0f || u > 1f)
                    {
                        continue;
                    }

                    Vector3 q = Vector3.Cross(s, e1);
                    float v = Vector3.Dot(dir, q) * invDet;
                    if (v < 0f || u + v > 1f)
                    {
                        continue;
                    }

                    float dist = Vector3.Dot(e2, q) * invDet;
                    if (dist <= tMin)
                    {
                        continue;
                    }

                    Vector3 n = Vector3.Cross(e1, e2);
                    if (Vector3.Dot(n, dir) > 0f)
                    {
                        n = -n;  // 레이 대면 강제 플립 — 이중면/미러 원천 방어
                    }

                    EquipMeshHit hit = new EquipMeshHit();
                    hit.point = ray.origin + dir * dist;
                    hit.normal = n.normalized;
                    hit.distance = dist;
                    hit.renderer = entry.renderer;
                    hit.triangleIndex = t;
                    results.Add(hit);
                }
            }
        }

        results.Sort((a, b) => a.distance.CompareTo(b.distance));
        return results.Count > 0;
    }

    private readonly List<EquipMeshHit> scratch = new List<EquipMeshHit>();  // RaycastCursor 재사용 버퍼

    // hitIndex번째 히트 선택 (범위 밖이면 클램프). hitCount로 사이클 UI 지원
    public bool RaycastCursor(Transform charRoot, Ray ray, int hitIndex, out EquipMeshHit hit, out int hitCount)
    {
        hit = new EquipMeshHit();
        hitCount = 0;

        if (RaycastAll(charRoot, ray, scratch) == false)
        {
            return false;
        }

        hitCount = scratch.Count;
        int idx = hitIndex;
        if (idx < 0)
        {
            idx = 0;
        }
        if (idx >= hitCount)
        {
            idx = hitCount - 1;
        }
        hit = scratch[idx];
        return true;
    }

    // 히트 삼각형의 지배 본 (물리 의심 본은 차순위→조상 승격 사다리)
    public Transform QueryDominantBone(Transform charRoot, EquipMeshHit hit)
    {
        EnsureCache(charRoot);
        if (cache == null)
        {
            return null;
        }

        MeshEntry entry = null;
        foreach (MeshEntry e in cache.entries)
        {
            if (e.renderer == hit.renderer)
            {
                entry = e;
                break;
            }
        }
        if (entry == null)
        {
            return null;
        }

        // 비스킨: 렌더러 transform이 부착 기준
        if (entry.skinned == false || entry.bones == null)
        {
            Transform t = entry.renderer.transform;
            if (EquipPhysicsBoneFilter.IsPhysicsSuspect(t, cache.physicsBones))
            {
                return PromoteToNonPhysicsAncestor(t, charRoot);
            }
            return t;
        }

        // 히트 삼각형 정점 3개의 본 웨이트 합산
        Dictionary<int, float> sums = new Dictionary<int, float>();
        for (int k = 0; k < 3; k++)
        {
            int vi = entry.tris[hit.triangleIndex * 3 + k];
            int start = entry.weightStart[vi];
            int end = entry.weightStart[vi + 1];

            for (int w = start; w < end; w++)
            {
                BoneWeight1 bw = entry.weights[w];
                if (bw.boneIndex < 0 || bw.boneIndex >= entry.bones.Length)
                {
                    continue;
                }

                if (sums.ContainsKey(bw.boneIndex))
                {
                    sums[bw.boneIndex] = sums[bw.boneIndex] + bw.weight;
                }
                else
                {
                    sums[bw.boneIndex] = bw.weight;
                }
            }
        }

        // 내림차순 후보에서 첫 비의심 본 채택
        List<KeyValuePair<int, float>> ordered = new List<KeyValuePair<int, float>>(sums);
        ordered.Sort((a, b) => b.Value.CompareTo(a.Value));

        Transform topBone = null;
        foreach (KeyValuePair<int, float> pair in ordered)
        {
            Transform bone = entry.bones[pair.Key];
            if (bone == null)
            {
                continue;
            }
            if (topBone == null)
            {
                topBone = bone;
            }

            if (EquipPhysicsBoneFilter.IsPhysicsSuspect(bone, cache.physicsBones) == false)
            {
                return bone;
            }
        }

        // 후보 전멸 → 최대 웨이트 본의 조상 승격
        if (topBone != null)
        {
            return PromoteToNonPhysicsAncestor(topBone, charRoot);
        }
        return null;
    }

    // 물리 의심 본의 첫 비의심 조상 (charRoot 도달 시 원본 반환 + 경고)
    private Transform PromoteToNonPhysicsAncestor(Transform bone, Transform charRoot)
    {
        Transform cur = bone.parent;
        while (cur != null && cur != charRoot)
        {
            if (EquipPhysicsBoneFilter.IsPhysicsSuspect(cur, cache.physicsBones) == false)
            {
                return cur;
            }
            cur = cur.parent;
        }

        Debug.LogWarning($"[EquipMeshRaycaster] '{bone.name}'의 비물리 조상을 찾지 못함 — 원본 본 사용 (물리 본 의심).");
        return bone;
    }

    // ── 캐시 관리 ──

    private void OnHierarchyChanged()
    {
        // 즉시 파기 금지 — dirty만 (드래그 중 미리보기 재부모화가 매 프레임 발생)
        if (cache != null)
        {
            cache.dirty = true;
        }
    }

    private void InvalidateStatic()
    {
        cache = null;
    }

    private void OnStageChanged(UnityEditor.SceneManagement.PrefabStage stage)
    {
        cache = null;
    }

    // 캐시 확보 (dirty면 해시 비교 후 유지/리빌드, 드래그 중엔 검사 유예)
    private void EnsureCache(Transform charRoot)
    {
        if (charRoot == null)
        {
            cache = null;
            return;
        }

        if (cache != null && cache.rootInstanceId == charRoot.GetInstanceID())
        {
            if (cache.dirty)
            {
                if (GUIUtility.hotControl != 0)
                {
                    // 드래그 세션 중 — 검사 유예, 기존 캐시 사용
                    return;
                }

                int newHash = ComputeRendererSetHash(charRoot);
                if (newHash == cache.rendererSetHash)
                {
                    cache.dirty = false;  // 자기 유발 변경(미리보기 등) — 유지
                }
                else
                {
                    Build(charRoot);
                    return;
                }
            }

            // 센티널: 본 수동 이동 감지 → 해당 Entry만 재스키닝
            if (GUIUtility.hotControl == 0)
            {
                foreach (MeshEntry entry in cache.entries)
                {
                    if (entry.skinned && SentinelOf(entry) != entry.boneSentinel)
                    {
                        RebuildEntryVerts(entry);
                    }
                }
            }
            return;
        }

        Build(charRoot);
    }

    // 수집 대상 렌더러 열거 (필터 적용)
    private List<Renderer> CollectRenderers(Transform charRoot)
    {
        List<Renderer> list = new List<Renderer>();
        Renderer[] all = charRoot.GetComponentsInChildren<Renderer>(false);

        foreach (Renderer r in all)
        {
            if (r == null || r.enabled == false)
            {
                continue;
            }
            if (EquipAuthoringUtil.IsExcludedName(r.gameObject.name))
            {
                continue;
            }
            if (r.gameObject.name.Contains("__EquipPreview__"))
            {
                continue;
            }
            if (r.GetComponentInParent<EquipMarker>() != null)
            {
                continue;  // 장착된 악세서리
            }
            if (EquipAuthoringUtil.IsSocketOrChildOfSocket(r.transform))
            {
                continue;
            }

            if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null)
                {
                    continue;
                }
            }
            else
            {
                if (r is MeshRenderer)
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null)
                    {
                        continue;
                    }
                }
                else
                {
                    continue;  // 그 외 렌더러(파티클 등) 제외
                }
            }

            list.Add(r);
        }
        return list;
    }

    // 렌더러 집합 해시 (instanceID 정렬 XOR/롤)
    private int ComputeRendererSetHash(Transform charRoot)
    {
        List<Renderer> list = CollectRenderers(charRoot);
        List<int> ids = new List<int>(list.Count);
        foreach (Renderer r in list)
        {
            ids.Add(r.GetInstanceID());
        }
        ids.Sort();

        int hash = 17;
        foreach (int id in ids)
        {
            hash = hash * 31 + id;
        }
        return hash;
    }

    // 샘플 본 4개 position 합 (포즈 변경 감지)
    private Vector3 SentinelOf(MeshEntry entry)
    {
        Vector3 sum = Vector3.zero;
        if (entry.bones == null)
        {
            return sum;
        }

        int step = Mathf.Max(1, entry.bones.Length / 4);
        for (int i = 0; i < entry.bones.Length; i = i + step)
        {
            if (entry.bones[i] != null)
            {
                sum = sum + entry.bones[i].position;
            }
        }
        return sum;
    }

    // 전체 캐시 빌드
    private void Build(Transform charRoot)
    {
        cache = new CharCache();
        cache.rootInstanceId = charRoot.GetInstanceID();
        cache.charHeight = EquipAuthoringUtil.MeasureCharHeight(charRoot.gameObject);
        if (cache.charHeight <= 1e-6f)
        {
            cache.charHeight = 1f;
        }
        cache.physicsBones = EquipPhysicsBoneFilter.CollectPhysicsBones(charRoot);

        List<Renderer> renderers = CollectRenderers(charRoot);
        foreach (Renderer r in renderers)
        {
            // 렌더러 단위 try-격리: 불량 립 1개가 전체를 봉쇄하지 못하게
            try
            {
                MeshEntry entry = BuildEntry(r);
                if (entry != null)
                {
                    cache.entries.Add(entry);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[EquipMeshRaycaster] '{r.name}' 캐시 빌드 실패 — 제외: {ex.Message}");
            }
        }

        cache.rendererSetHash = ComputeRendererSetHash(charRoot);
        cache.dirty = false;
    }

    // 렌더러 1개 캐시 빌드
    private MeshEntry BuildEntry(Renderer r)
    {
        MeshEntry entry = new MeshEntry();
        entry.renderer = r;

        Mesh mesh = null;
        if (r is SkinnedMeshRenderer smr)
        {
            mesh = smr.sharedMesh;
            entry.skinned = true;
        }
        else
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            mesh = mf.sharedMesh;
            entry.skinned = false;
        }

        // 삼각형 서브메시만 이어붙임
        List<int> tris = new List<int>();
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            if (mesh.GetTopology(s) != MeshTopology.Triangles)
            {
                continue;
            }
            tris.AddRange(mesh.GetTriangles(s));
        }
        if (tris.Count == 0)
        {
            return null;
        }
        entry.tris = tris.ToArray();

        // 정점 월드 변환
        if (entry.skinned)
        {
            BuildSkinnedData(entry, (SkinnedMeshRenderer)r, mesh);
            RebuildEntryVerts(entry);
        }
        else
        {
            Vector3[] verts = mesh.vertices;
            entry.worldVerts = new Vector3[verts.Length];
            Matrix4x4 m = r.transform.localToWorldMatrix;
            for (int i = 0; i < verts.Length; i++)
            {
                entry.worldVerts[i] = m.MultiplyPoint3x4(verts[i]);
            }
            BuildBoundsOf(entry);
        }

        return entry;
    }

    // SMR 스키닝 정적 데이터 (웨이트 스트림/본) 준비
    private class SkinnedData
    {
        public Vector3[] localVerts;
        public Matrix4x4[] bindposes;
        public SkinnedMeshRenderer smr;
    }

    private readonly Dictionary<MeshEntry, SkinnedData> skinnedData = new Dictionary<MeshEntry, SkinnedData>();

    private void BuildSkinnedData(MeshEntry entry, SkinnedMeshRenderer smr, Mesh mesh)
    {
        SkinnedData data = new SkinnedData();
        data.localVerts = mesh.vertices;
        data.bindposes = mesh.bindposes;
        data.smr = smr;
        skinnedData[entry] = data;

        entry.bones = smr.bones;

        // BoneWeight1 스트림 (4본 제한 없음) + 정점별 시작 오프셋 누적합
        NativeArray<byte> bonesPerVertex = mesh.GetBonesPerVertex();
        NativeArray<BoneWeight1> allWeights = mesh.GetAllBoneWeights();

        entry.weightStart = new int[data.localVerts.Length + 1];
        int offset = 0;
        for (int v = 0; v < data.localVerts.Length; v++)
        {
            entry.weightStart[v] = offset;
            if (v < bonesPerVertex.Length)
            {
                offset = offset + bonesPerVertex[v];
            }
        }
        entry.weightStart[data.localVerts.Length] = offset;
        entry.weights = allWeights.ToArray();
    }

    // SMR 정점 재스키닝 (빌드 + 센티널 불일치 시)
    private void RebuildEntryVerts(MeshEntry entry)
    {
        SkinnedData data;
        if (skinnedData.TryGetValue(entry, out data) == false)
        {
            return;
        }

        Transform[] bones = entry.bones;
        Matrix4x4[] bind = data.bindposes;

        // [가드] bindposes ≠ bones 길이 (Gmod/MMD 립 실재) — 큰 쪽으로 잡고 짝 없는 슬롯은 폴백
        int skinCount = Mathf.Max(bones.Length, bind.Length);
        Matrix4x4[] skin = new Matrix4x4[skinCount];

        Transform fallbackBone = data.smr.rootBone;
        if (fallbackBone == null)
        {
            fallbackBone = data.smr.transform;
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
                skin[i] = bones[i].localToWorldMatrix * bp;
            }
            else
            {
                skin[i] = fallbackBone.localToWorldMatrix * bp;
            }
        }

        Vector3[] local = data.localVerts;
        if (entry.worldVerts == null || entry.worldVerts.Length != local.Length)
        {
            entry.worldVerts = new Vector3[local.Length];
        }

        Matrix4x4 smrMatrix = data.smr.transform.localToWorldMatrix;

        for (int v = 0; v < local.Length; v++)
        {
            int start = entry.weightStart[v];
            int end = entry.weightStart[v + 1];

            Vector3 acc = Vector3.zero;
            float wsum = 0f;

            for (int w = start; w < end; w++)
            {
                BoneWeight1 bw = entry.weights[w];
                if (bw.boneIndex < 0 || bw.boneIndex >= skinCount)
                {
                    continue;  // [가드] 범위 밖 인플루언스 스킵
                }
                acc = acc + skin[bw.boneIndex].MultiplyPoint3x4(local[v]) * bw.weight;
                wsum = wsum + bw.weight;
            }

            if (wsum > 1e-4f)
            {
                entry.worldVerts[v] = acc / wsum;  // MMD 웨이트 합≠1 정규화
            }
            else
            {
                entry.worldVerts[v] = smrMatrix.MultiplyPoint3x4(local[v]);  // 무웨이트 폴백
            }
        }

        entry.boneSentinel = SentinelOf(entry);
        BuildBoundsOf(entry);
    }

    // Entry의 전체/청크 AABB 계산
    private void BuildBoundsOf(MeshEntry entry)
    {
        int triCount = entry.tris.Length / 3;
        int chunkCount = (triCount + ChunkTris - 1) / ChunkTris;
        entry.chunkBounds = new Bounds[chunkCount];

        bool hasTotal = false;
        Bounds total = new Bounds();

        for (int c = 0; c < chunkCount; c++)
        {
            int triStart = c * ChunkTris;
            int triEnd = Mathf.Min(triStart + ChunkTris, triCount);

            bool has = false;
            Bounds b = new Bounds();

            for (int t = triStart; t < triEnd; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = entry.worldVerts[entry.tris[t * 3 + k]];
                    if (has == false)
                    {
                        b = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        b.Encapsulate(p);
                    }
                }
            }

            entry.chunkBounds[c] = b;

            if (hasTotal == false)
            {
                total = b;
                hasTotal = true;
            }
            else
            {
                total.Encapsulate(b);
            }
        }

        entry.worldBounds = total;
    }
}
