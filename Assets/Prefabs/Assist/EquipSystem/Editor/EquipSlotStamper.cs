using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 스탬프 결과 리포트 1행
public class EquipStampEntry
{
    public string prefabPath;   // 대상 프리팹 경로
    public string slotId;       // 슬롯
    public string status;       // OK / KEEP_TUNED / KEEP_MANUAL / NO_BONE / MODEL_SKIP / SELF / NO_HEIGHT / NO_SOCKETS / ERROR
    public string method;       // DONOR / NAME / HUMANOID / ALIAS / NEAREST / ROOT
    public string boneName;     // 부착된 본 이름
    public string note;         // 부가 정보 (AMBIGUOUS, 물리의심, SCALE_CONV 등)
    public bool isWarning;      // 검수 필요 표시
}

// 슬롯 스탬퍼: 캡처(골든→템플릿) / Donor 직접 복사(같은 스켈레톤) / 템플릿 스탬프(크로스 캐릭터) / 배치 프리팹 IO.
public static class EquipSlotStamper
{
    // ── 캡처: 골든 캐릭터의 소켓들을 템플릿에 기록 (slotId 기준 merge — 별칭/휴머노이드 설정은 보존) ──
    // 표준 5종(chest/back/head/overhead/origin) 외 슬롯은 기본 제외 (템플릿 오염 방지 — 비표준 def는 크로스 힌트가 없어 NEAREST로 얼굴 본 등에 오부착됨)
    public static int CaptureTemplate(GameObject charRoot, EquipSlotTemplate template, bool includeNonStandard = false)
    {
        if (charRoot == null || template == null)
        {
            return 0;
        }

        Transform rootT = charRoot.transform;
        Quaternion rootRot = rootT.rotation;

        float height = EquipAuthoringUtil.MeasureCharHeight(charRoot);
        if (height <= 1e-6f)
        {
            Debug.LogError("[EquipStamper] 캐릭터 키 측정 실패 — 캡처 중단: " + charRoot.name);
            return 0;
        }

        Bounds bounds;
        EquipAuthoringUtil.MeasureBounds(charRoot, out bounds);

        // 골든 소켓이 물리 본에 붙어있는지 경고용
        HashSet<Transform> physicsBones = EquipPhysicsBoneFilter.CollectPhysicsBones(rootT);

        EquipSocket[] sockets = charRoot.GetComponentsInChildren<EquipSocket>(true);
        int captured = 0;

        foreach (EquipSocket socket in sockets)
        {
            if (socket == null || string.IsNullOrEmpty(socket.slotId))
            {
                continue;
            }

            // 표준 슬롯 화이트리스트 (옵트인 없으면 비표준 스킵)
            if (includeNonStandard == false && EquipSlotTemplate.IsStandardSlot(socket.slotId) == false)
            {
                Debug.Log($"[EquipStamper] 비표준 슬롯 '{socket.slotId}' 캡처 제외 (표준: {string.Join("/", EquipSlotTemplate.StandardSlotIds)})");
                continue;
            }

            // 기존 def가 있으면 geometry만 갱신 (aliases/humanoidBone은 보존), 없으면 기본값으로 생성
            EquipSlotDef def = template.Find(socket.slotId);
            if (def == null)
            {
                def = new EquipSlotDef();
                def.slotId = socket.slotId;
                def.boneAliases = EquipSlotTemplate.DefaultAliases(socket.slotId);
                def.humanoidBone = EquipSlotTemplate.DefaultHumanoidBone(socket.slotId);
                template.slots.Add(def);
            }

            def.socketName = socket.gameObject.name;

            Transform parent = socket.transform.parent;
            if (parent == null || parent == rootT)
            {
                // 루트 직속 → origin류
                def.attachToRoot = true;
                def.boneName = "";
                def.rootDirFromBone = Quaternion.Inverse(rootRot) * (socket.transform.position - rootT.position) / height;
            }
            else
            {
                def.attachToRoot = false;
                def.boneName = parent.name;
                def.rootDirFromBone = Quaternion.Inverse(rootRot) * (socket.transform.position - parent.position) / height;

                // 골든 소켓이 물리 본에 붙어있으면 경고 (템플릿 전파 전체가 오염될 수 있음)
                if (EquipPhysicsBoneFilter.IsPhysicsSuspect(parent, physicsBones))
                {
                    Debug.LogWarning($"[EquipStamper] 골든 소켓 '{socket.slotId}'가 물리 의심 본 '{parent.name}'에 부착됨 — 전파 전 확인 필요.");
                }
            }

            def.rootFrameEuler = (Quaternion.Inverse(rootRot) * socket.transform.rotation).eulerAngles;

            // 캡슐 비율 (월드 길이 / 캐릭터 키). 캡슐이 없으면 기존값 보존 또는 기본값 + 경고.
            float worldLen = EquipAuthoringUtil.CapsuleWorldLength(socket);
            if (worldLen > 1e-12f)
            {
                def.capsuleHeightRatio = worldLen / height;
            }
            else
            {
                if (def.capsuleHeightRatio <= 1e-12f)
                {
                    def.capsuleHeightRatio = 0.05f;
                }
                Debug.LogWarning($"[EquipStamper] '{socket.slotId}' 소켓에 사이징 캡슐 없음 — 비율 {def.capsuleHeightRatio:F3} 사용.");
            }

            CapsuleCollider cap = socket.GetComponent<CapsuleCollider>();
            if (cap != null)
            {
                def.capsuleDirection = cap.direction;
            }

            // NEAREST 폴백용 바운드 비율 위치 — 루트-로컬 프레임 (회전된 루트에서도 방향 유지)
            Vector3 rel = Quaternion.Inverse(rootRot) * (socket.transform.position - bounds.center);
            def.normalizedBoundsPos = new Vector3(
                rel.x / Mathf.Max(bounds.size.x, 1e-6f),
                rel.y / Mathf.Max(bounds.size.y, 1e-6f),
                rel.z / Mathf.Max(bounds.size.z, 1e-6f));

            captured = captured + 1;
        }

        EditorUtility.SetDirty(template);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EquipStamper] 캡처 완료: {captured}개 슬롯 → {template.name}");
        return captured;
    }

    // ── Donor 직접 복사: 같은 스켈레톤(의상 전파) — 본 이름 일치 + 본-로컬 값 복사 (lossy 편차 시 환산) ──
    public static List<EquipStampEntry> StampDonorToInstance(GameObject donorRoot, GameObject targetRoot, string sourceName, bool apply, string prefabPath)
    {
        List<EquipStampEntry> report = new List<EquipStampEntry>();

        EquipSocket[] donorSockets = donorRoot.GetComponentsInChildren<EquipSocket>(true);
        if (donorSockets.Length == 0)
        {
            report.Add(MakeEntry(prefabPath, "", "NO_SOCKETS", "", "", "도너에 소켓이 없음", true));
            return report;
        }

        foreach (EquipSocket donorSocket in donorSockets)
        {
            if (donorSocket == null || string.IsNullOrEmpty(donorSocket.slotId))
            {
                continue;
            }

            string slotId = donorSocket.slotId;

            // 손보정/수동 소켓 보호 (+순정 스탬프면 재사용 대상 반환)
            EquipSocket reusable;
            EquipStampEntry guard = CheckExistingSocket(targetRoot.transform, slotId, prefabPath, out reusable);
            if (guard != null)
            {
                report.Add(guard);
                continue;
            }

            // 부모 본 결정: 도너 소켓의 부모 이름을 대상에서 정확 일치 탐색
            Transform donorParent = donorSocket.transform.parent;
            Transform targetBone = null;
            string note = "";
            bool warning = false;

            if (donorParent == null || donorParent == donorRoot.transform)
            {
                targetBone = targetRoot.transform;
            }
            else
            {
                targetBone = EquipAuthoringUtil.FindByName(targetRoot.transform, donorParent.name);

                // 동명 본 감지
                int nameCount = EquipAuthoringUtil.CountByName(targetRoot.transform, donorParent.name);
                if (nameCount > 1)
                {
                    note = $"AMBIGUOUS_NAME({nameCount})";
                    warning = true;
                }
            }

            if (targetBone == null)
            {
                string missing = "";
                if (donorParent != null)
                {
                    missing = donorParent.name;
                }
                report.Add(MakeEntry(prefabPath, slotId, "NO_BONE", "DONOR", missing, "같은 이름 본 없음 — 크로스 스탬프(Template 모드) 필요", true));
                continue;
            }

            // lossyScale 편차 감지 → 월드 환산 계수 (같은 스켈레톤이면 1.0)
            Transform donorLossyBase = donorSocket.transform.parent;
            if (donorLossyBase == null)
            {
                donorLossyBase = donorRoot.transform;
            }
            float donorLossy = EquipAuthoringUtil.LossyAvg(donorLossyBase);
            float targetLossy = EquipAuthoringUtil.LossyAvg(targetBone);
            float factor = donorLossy / targetLossy;
            if (Mathf.Abs(factor - 1f) > 0.01f)
            {
                if (string.IsNullOrEmpty(note) == false)
                {
                    note = note + ", ";
                }
                note = note + $"SCALE_CONV(x{factor:F3})";
                warning = true;
            }
            else
            {
                factor = 1f;
            }

            if (apply)
            {
                // 소켓 GO 확보: 순정 스탬프 재사용 → 없으면 신규 생성 (본 GO 낚아채기 금지)
                GameObject socketGo = AcquireSocketGo(reusable, targetBone, donorSocket.gameObject.name);

                socketGo.transform.localPosition = donorSocket.transform.localPosition * factor;
                socketGo.transform.localRotation = donorSocket.transform.localRotation;
                socketGo.transform.localScale = donorSocket.transform.localScale;

                CopyCapsule(donorSocket.gameObject, socketGo, factor);
                CopySocketFields(donorSocket, socketGo, report, prefabPath);
                WriteStamp(socketGo, sourceName, "DONOR");
            }

            report.Add(MakeEntry(prefabPath, slotId, "OK", "DONOR", targetBone.name, note, warning));
        }

        return report;
    }

    // ── 템플릿 스탬프: 크로스 캐릭터 — 본 해석 사다리(NAME→HUMANOID→ALIAS→물리필터 NEAREST) ──
    public static List<EquipStampEntry> StampTemplateToInstance(EquipSlotTemplate template, GameObject targetRoot, bool apply, string prefabPath)
    {
        List<EquipStampEntry> report = new List<EquipStampEntry>();

        Transform rootT = targetRoot.transform;
        Quaternion rootRot = rootT.rotation;

        float height = EquipAuthoringUtil.MeasureCharHeight(targetRoot);
        if (height <= 1e-6f)
        {
            report.Add(MakeEntry(prefabPath, "", "NO_HEIGHT", "", "", "렌더러 바운드 측정 실패 (2D/빈 프리팹?)", true));
            return report;
        }

        Bounds bounds;
        EquipAuthoringUtil.MeasureBounds(targetRoot, out bounds);

        HashSet<Transform> skinBones = EquipAuthoringUtil.CollectSkinBones(rootT);
        HashSet<Transform> physicsBones = EquipPhysicsBoneFilter.CollectPhysicsBones(rootT);

        foreach (EquipSlotDef def in template.slots)
        {
            if (def == null || string.IsNullOrEmpty(def.slotId))
            {
                continue;
            }

            // 폐지/비표준 슬롯(구 overhead 등) 스킵 — 오래된 템플릿 에셋의 잔존 def 방어
            if (EquipSlotTemplate.IsStandardSlot(def.slotId) == false)
            {
                report.Add(MakeEntry(prefabPath, def.slotId, "SKIP_NONSTANDARD", "", "", "표준 슬롯 아님 (overhead는 head placeholder로 흡수됨)", false));
                continue;
            }

            // 손보정/수동 소켓 보호 (+순정 스탬프면 재사용 대상 반환)
            EquipSocket reusable;
            EquipStampEntry guard = CheckExistingSocket(rootT, def.slotId, prefabPath, out reusable);
            if (guard != null)
            {
                report.Add(guard);
                continue;
            }

            // 본 해석 사다리 (공용 ResolveBone — Socket Author 창과 공유)
            string method;
            string note;
            bool warning;
            Transform bone = ResolveBone(def, targetRoot, skinBones, physicsBones, bounds, height, out method, out note, out warning);

            if (bone == null)
            {
                string failNote = note;
                if (string.IsNullOrEmpty(failNote))
                {
                    failNote = "사다리 전 단계 실패";
                }
                report.Add(MakeEntry(prefabPath, def.slotId, "NO_BONE", "", def.boneName, failNote, true));
                continue;
            }

            if (apply)
            {
                string socketName = def.socketName;
                if (string.IsNullOrEmpty(socketName))
                {
                    socketName = "Socket_" + def.slotId;
                }

                // 소켓 GO 확보: 순정 스탬프 재사용(재부모화) → 없으면 신규 생성 (본 GO 낚아채기 금지)
                bool created = (reusable == null);
                GameObject socketGo = AcquireSocketGo(reusable, bone, socketName);

                // 배치: 위치/회전은 루트 프레임 기준 (리그마다 본 로컬 축이 달라 본-로컬 이식은 방향이 틀어짐)
                socketGo.transform.position = bone.position + rootRot * (def.rootDirFromBone * height);
                socketGo.transform.rotation = rootRot * Quaternion.Euler(def.rootFrameEuler);
                socketGo.transform.localScale = Vector3.one;

                CapsuleCollider cap = EquipAuthoringUtil.SetCapsuleByWorldLength(socketGo, def.capsuleHeightRatio * height, def.capsuleDirection);
                if (cap == null)
                {
                    warning = true;
                    if (string.IsNullOrEmpty(note))
                    {
                        note = "NO_CAPSULE";
                    }
                    else
                    {
                        note = note + ", NO_CAPSULE";
                    }
                }

                EquipSocket socket = socketGo.GetComponent<EquipSocket>();
                if (socket == null)
                {
                    socket = socketGo.AddComponent<EquipSocket>();
                    created = true;
                }
                socket.slotId = def.slotId;

                // fit/pivot은 신규 생성 시에만 기본값 (재사용 시 사용자의 필드 편집 보존)
                if (created)
                {
                    socket.fit = EquipFitMode.ContainUniform;
                    socket.pivot = EquipAnchorPivot.VolumeCenter;
                }

                WriteStamp(socketGo, template.name, method);
            }

            report.Add(MakeEntry(prefabPath, def.slotId, "OK", method, bone.name, note, warning));
        }

        return report;
    }

    // ── 배치 실행: 프리팹 목록에 Donor 복사 ──
    public static List<EquipStampEntry> RunDonorBatch(GameObject donorPrefab, List<GameObject> targets, bool apply)
    {
        List<EquipStampEntry> report = new List<EquipStampEntry>();

        string donorPath = AssetDatabase.GetAssetPath(donorPrefab);
        if (string.IsNullOrEmpty(donorPath))
        {
            report.Add(MakeEntry("", "", "ERROR", "", "", "도너가 프리팹 에셋이 아님", true));
            return report;
        }

        GameObject donorRoot = PrefabUtility.LoadPrefabContents(donorPath);
        try
        {
            foreach (GameObject target in targets)
            {
                ProcessPrefabTarget(target, donorPath, report, (instanceRoot, path) =>
                {
                    return StampDonorToInstance(donorRoot, instanceRoot, donorPrefab.name, apply, path);
                }, apply);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(donorRoot);
        }

        return report;
    }

    // ── 배치 실행: 프리팹 목록에 템플릿 스탬프 ──
    public static List<EquipStampEntry> RunTemplateBatch(EquipSlotTemplate template, List<GameObject> targets, bool apply)
    {
        List<EquipStampEntry> report = new List<EquipStampEntry>();

        foreach (GameObject target in targets)
        {
            ProcessPrefabTarget(target, null, report, (instanceRoot, path) =>
            {
                return StampTemplateToInstance(template, instanceRoot, apply, path);
            }, apply);
        }

        return report;
    }

    // 프리팹 1개 처리 공통 (Model 스킵 / 자기자신 스킵 / LoadPrefabContents→작업→저장→해제).
    // 로드 실패도 ERROR 리포트로 격리해 배치 전체가 중단되지 않게 한다.
    private static void ProcessPrefabTarget(GameObject target, string selfPath, List<EquipStampEntry> report, System.Func<GameObject, string, List<EquipStampEntry>> op, bool apply)
    {
        if (target == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(target);
        if (string.IsNullOrEmpty(path))
        {
            report.Add(MakeEntry(target.name, "", "ERROR", "", "", "프리팹 에셋이 아님 (씬 오브젝트?)", true));
            return;
        }

        if (string.IsNullOrEmpty(selfPath) == false && path == selfPath)
        {
            report.Add(MakeEntry(path, "", "SELF", "", "", "도너 자신 — 스킵", false));
            return;
        }

        if (PrefabUtility.GetPrefabAssetType(target) == PrefabAssetType.Model)
        {
            report.Add(MakeEntry(path, "", "MODEL_SKIP", "", "", "Model(.fbx) 프리팹은 수정 불가 — Variant/일반 프리팹으로 감싸서 사용", true));
            return;
        }

        GameObject instanceRoot = null;
        try
        {
            instanceRoot = PrefabUtility.LoadPrefabContents(path);

            List<EquipStampEntry> entries = op(instanceRoot, path);
            report.AddRange(entries);

            if (apply)
            {
                bool anyOk = false;
                foreach (EquipStampEntry e in entries)
                {
                    if (e.status == "OK")
                    {
                        anyOk = true;
                        break;
                    }
                }

                if (anyOk)
                {
                    PrefabUtility.SaveAsPrefabAsset(instanceRoot, path);
                }
            }
        }
        catch (System.Exception ex)
        {
            report.Add(MakeEntry(path, "", "ERROR", "", "", ex.Message, true));
        }
        finally
        {
            if (instanceRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(instanceRoot);
            }
        }
    }

    // 기존 소켓 보호 검사. 반환: 스킵 엔트리(보호) 또는 null(진행 가능).
    // 진행 가능이면서 순정 스탬프 소켓이 있으면 reusable로 반환 — 호출측은 반드시 이 GO를 재사용해야 중복 소켓이 안 생긴다.
    private static EquipStampEntry CheckExistingSocket(Transform rootT, string slotId, string prefabPath, out EquipSocket reusable)
    {
        reusable = null;

        EquipSocket existing = EquipAuthoringUtil.FindSocketBySlotId(rootT, slotId);
        if (existing == null)
        {
            return null;
        }

        EquipSocketStamp stamp = existing.GetComponent<EquipSocketStamp>();
        if (stamp == null)
        {
            // 스탬프 없는 소켓 = 손으로 저작한 것 → 존중
            return MakeEntry(prefabPath, slotId, "KEEP_MANUAL", "", ParentName(existing.transform), "수동 저작 소켓 존재 — 보존", false);
        }

        if (stamp.IsHandTuned())
        {
            // 스탬프 후 손보정됨 → 보존
            return MakeEntry(prefabPath, slotId, "KEEP_TUNED", stamp.resolvedBy, ParentName(existing.transform), "손보정 감지 — 보존", false);
        }

        // 순정 스탬프 → 재스탬프 허용, 기존 GO를 재사용하도록 반환 (본 해석이 바뀌어도 중복 생성 없이 이동)
        reusable = existing;
        return null;
    }

    // 소켓 GO 확보: 재사용 대상이 있으면 이름/부모 갱신해 재사용, 없으면 신규 생성.
    // 신규 생성 시 이름이 같은 자식이 있어도 EquipSocket이 붙은 GO만 재사용 — 본 GO를 낚아채 스켈레톤을 훼손하는 사고 방지.
    private static GameObject AcquireSocketGo(EquipSocket reusable, Transform bone, string socketName)
    {
        if (reusable != null)
        {
            GameObject go = reusable.gameObject;
            go.name = socketName;
            if (go.transform.parent != bone)
            {
                go.transform.SetParent(bone, false);
            }
            return go;
        }

        for (int i = 0; i < bone.childCount; i++)
        {
            Transform child = bone.GetChild(i);
            if (child.name == socketName && child.GetComponent<EquipSocket>() != null)
            {
                return child.gameObject;
            }
        }

        GameObject created = new GameObject(socketName);
        created.transform.SetParent(bone, false);
        return created;
    }

    // 본 해석 사다리 (공용): attachToRoot → NAME → HUMANOID → ALIAS → 물리필터 NEAREST.
    // Socket Author 창(본 자동 제안)과 템플릿 스탬프가 공유한다. 실패 시 null + note에 사유.
    public static Transform ResolveBone(EquipSlotDef def, GameObject targetRoot, HashSet<Transform> skinBones, HashSet<Transform> physicsBones, Bounds bounds, float height, out string method, out string note, out bool warning)
    {
        method = "";
        note = "";
        warning = false;

        Transform rootT = targetRoot.transform;
        Quaternion rootRot = rootT.rotation;
        Transform bone = null;

        if (def.attachToRoot)
        {
            method = "ROOT";
            return rootT;
        }

        if (string.IsNullOrEmpty(def.boneName) == false)
        {
            // 1순위: 정확 이름 (+동명 본 경고)
            bone = EquipAuthoringUtil.FindByName(rootT, def.boneName);
            if (bone != null)
            {
                method = "NAME";
                int nameCount = EquipAuthoringUtil.CountByName(rootT, def.boneName);
                if (nameCount > 1)
                {
                    note = $"AMBIGUOUS_NAME({nameCount})";
                    warning = true;
                }
            }
        }

        if (bone == null && def.humanoidBone >= 0)
        {
            // 2순위: Humanoid 본 (에딧모드 폴백 포함)
            bone = EquipAuthoringUtil.ResolveHumanoidBone(targetRoot, def.humanoidBone);
            if (bone != null)
            {
                method = "HUMANOID";
            }
        }

        if (bone == null && def.boneAliases != null && def.boneAliases.Count > 0)
        {
            // 3순위: 별칭 토큰 일치 (스킨 본 우선, 없으면 전체 Transform — 단 소켓 GO 제외)
            int matchCount;
            IEnumerable<Transform> candidates;
            if (skinBones.Count > 0)
            {
                candidates = skinBones;
            }
            else
            {
                List<Transform> all = new List<Transform>();
                foreach (Transform t in rootT.GetComponentsInChildren<Transform>(true))
                {
                    if (EquipAuthoringUtil.IsSocketOrChildOfSocket(t) == false)
                    {
                        all.Add(t);
                    }
                }
                candidates = all;
            }

            bone = EquipAuthoringUtil.FindBoneByAlias(candidates, def.boneAliases, out matchCount);
            if (bone != null)
            {
                method = "ALIAS";
                if (matchCount > 1)
                {
                    note = $"AMBIGUOUS({matchCount})";
                    warning = true;
                }
            }
        }

        if (bone == null)
        {
            // 4순위: 최근접 스킨 본 — 크로스 힌트(별칭/휴머노이드)가 전혀 없는 def는 진입 금지 (엉뚱한 얼굴 본 방지)
            bool hasCrossHints = def.humanoidBone >= 0;
            if (hasCrossHints == false && def.boneAliases != null && def.boneAliases.Count > 0)
            {
                hasCrossHints = true;
            }

            if (hasCrossHints == false)
            {
                note = "크로스 힌트 없음(별칭/휴머노이드) — 수동 지정 필요";
                warning = true;
                return null;
            }

            if (skinBones.Count > 0)
            {
                // 루트-로컬 정규화 좌표 → 월드 목표점 (캡처와 동일 프레임)
                Vector3 rel = new Vector3(
                    def.normalizedBoundsPos.x * bounds.size.x,
                    def.normalizedBoundsPos.y * bounds.size.y,
                    def.normalizedBoundsPos.z * bounds.size.z);
                Vector3 guess = bounds.center + rootRot * rel;

                bone = FindNearestNonPhysicsBone(skinBones, guess, physicsBones);
                if (bone != null)
                {
                    method = "NEAREST";
                    warning = true;
                    note = "검수 필요";
                }
            }
        }

        if (bone == null)
        {
            return null;
        }

        // 물리 본 의심 경고 (이름 패턴 포함)
        if (EquipPhysicsBoneFilter.IsPhysicsSuspect(bone, physicsBones))
        {
            warning = true;
            if (string.IsNullOrEmpty(note))
            {
                note = "물리 본 의심";
            }
            else
            {
                note = note + ", 물리 본 의심";
            }
        }

        return bone;
    }

    // 물리 본을 제외한 최근접 본 탐색
    private static Transform FindNearestNonPhysicsBone(HashSet<Transform> bones, Vector3 guess, HashSet<Transform> physicsBones)
    {
        Transform nearest = null;
        float best = float.MaxValue;

        foreach (Transform b in bones)
        {
            if (b == null)
            {
                continue;
            }

            if (EquipPhysicsBoneFilter.IsPhysicsSuspect(b, physicsBones))
            {
                continue;
            }

            float d = (b.position - guess).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = b;
            }
        }
        return nearest;
    }

    // 캡슐 콜라이더 값 복사 (도너 → 대상). factor = lossy 편차 환산 계수 (같은 스켈레톤이면 1)
    private static void CopyCapsule(GameObject from, GameObject to, float factor)
    {
        CapsuleCollider src = from.GetComponent<CapsuleCollider>();
        if (src == null)
        {
            return;
        }

        CapsuleCollider dst = to.GetComponent<CapsuleCollider>();
        if (dst == null)
        {
            dst = to.AddComponent<CapsuleCollider>();
        }
        dst.isTrigger = true;
        dst.center = src.center * factor;
        dst.radius = src.radius * factor;
        dst.height = src.height * factor;
        dst.direction = src.direction;
    }

    // EquipSocket 설정 복사 (+PlaceholderChild 앵커 자식 복제)
    private static void CopySocketFields(EquipSocket from, GameObject to, List<EquipStampEntry> report, string prefabPath)
    {
        EquipSocket dst = to.GetComponent<EquipSocket>();
        if (dst == null)
        {
            dst = to.AddComponent<EquipSocket>();
        }
        dst.slotId = from.slotId;
        dst.fit = from.fit;
        dst.pivot = from.pivot;

        // PlaceholderChild 앵커 복제: 앵커가 도너 소켓의 자식이면 같은 이름/로컬값으로 복제해 연결
        if (from.pivot == EquipAnchorPivot.PlaceholderChild && from.placeholderAnchor != null)
        {
            if (from.placeholderAnchor.IsChildOf(from.transform))
            {
                Transform anchor = null;
                for (int i = 0; i < to.transform.childCount; i++)
                {
                    if (to.transform.GetChild(i).name == from.placeholderAnchor.name)
                    {
                        anchor = to.transform.GetChild(i);
                        break;
                    }
                }

                if (anchor == null)
                {
                    GameObject anchorGo = new GameObject(from.placeholderAnchor.name);
                    anchorGo.transform.SetParent(to.transform, false);
                    anchor = anchorGo.transform;
                }

                anchor.localPosition = from.placeholderAnchor.localPosition;
                anchor.localRotation = from.placeholderAnchor.localRotation;
                anchor.localScale = from.placeholderAnchor.localScale;
                dst.placeholderAnchor = anchor;
            }
            else
            {
                // 소켓 밖 앵커는 이식 불가 → VolumeCenter로 강등 + 리포트
                dst.pivot = EquipAnchorPivot.VolumeCenter;
                report.Add(MakeEntry(prefabPath, from.slotId, "OK", "DONOR", "", "placeholderAnchor가 소켓 외부 — VolumeCenter로 강등", true));
            }
        }
    }

    // 스탬프 마커 기록 (+스냅샷)
    private static void WriteStamp(GameObject socketGo, string sourceName, string method)
    {
        EquipSocketStamp stamp = socketGo.GetComponent<EquipSocketStamp>();
        if (stamp == null)
        {
            stamp = socketGo.AddComponent<EquipSocketStamp>();
        }
        stamp.sourceName = sourceName;
        stamp.resolvedBy = method;
        stamp.TakeSnapshot();
    }

    // 부모 이름 (null 안전)
    private static string ParentName(Transform t)
    {
        if (t.parent == null)
        {
            return "";
        }
        return t.parent.name;
    }

    // 리포트 엔트리 생성
    private static EquipStampEntry MakeEntry(string prefabPath, string slotId, string status, string method, string boneName, string note, bool isWarning)
    {
        EquipStampEntry e = new EquipStampEntry();
        e.prefabPath = prefabPath;
        e.slotId = slotId;
        e.status = status;
        e.method = method;
        e.boneName = boneName;
        e.note = note;
        e.isWarning = isWarning;
        return e;
    }
}
