using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 스탬프 결과 리포트 1행
public class EquipStampEntry
{
    public string prefabPath;   // 대상 프리팹 경로
    public string slotId;       // 슬롯
    public string status;       // OK / KEEP_TUNED / KEEP_MANUAL / NO_BONE / MODEL_SKIP / SELF / NO_SOCKETS / ERROR
    public string method;       // 본 해석 방법 (DONOR)
    public string boneName;     // 부착된 본 이름
    public string note;         // 부가 정보 (AMBIGUOUS, 물리의심, SCALE_CONV 등)
    public bool isWarning;      // 검수 필요 표시
}

// 슬롯 스탬퍼: Donor 직접 복사(같은 스켈레톤 의상 전파) + 배치 프리팹 IO.
// Template 크로스 캐릭터 전파는 캡슐 시대와 함께 삭제 — P3에서 메시 레이 기반으로 재구축 예정 (git 이력 참조).
public static class EquipSlotStamper
{
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
                report.Add(MakeEntry(prefabPath, slotId, "NO_BONE", "DONOR", missing, "같은 이름 본 없음 — 크로스 캐릭터 전파는 P3 예정", true));
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

                CopySocketFields(donorSocket, socketGo);
                ClonePlaceholders(donorSocket, socketGo, factor);  // 신모델 부착점(placeholder 등)도 전파
                WriteStamp(socketGo, sourceName, "DONOR");
            }

            report.Add(MakeEntry(prefabPath, slotId, "OK", "DONOR", targetBone.name, note, warning));
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

    // 도너 소켓의 EquipPlaceholder 자식들을 대상 소켓에 복제 (같은 스켈레톤 — 본-로컬 값 ×factor)
    private static void ClonePlaceholders(EquipSocket donorSocket, GameObject targetSocketGo, float factor)
    {
        EquipPlaceholder[] donorPhs = donorSocket.GetComponentsInChildren<EquipPlaceholder>(true);
        foreach (EquipPlaceholder src in donorPhs)
        {
            if (src == null || string.IsNullOrEmpty(src.placeholderId))
            {
                continue;
            }

            // 같은 id 재사용 또는 생성
            EquipPlaceholder dst = null;
            EquipPlaceholder[] targetPhs = targetSocketGo.GetComponentsInChildren<EquipPlaceholder>(true);
            foreach (EquipPlaceholder t in targetPhs)
            {
                if (t != null && EquipSocket.NormalizePlaceholderId(t.placeholderId) == EquipSocket.NormalizePlaceholderId(src.placeholderId))
                {
                    dst = t;
                    break;
                }
            }

            if (dst == null)
            {
                GameObject dstGo = new GameObject(src.gameObject.name);
                dstGo.transform.SetParent(targetSocketGo.transform, false);
                dst = dstGo.AddComponent<EquipPlaceholder>();
            }

            dst.transform.localPosition = src.transform.localPosition * factor;
            dst.transform.localRotation = src.transform.localRotation;
            dst.transform.localScale = src.transform.localScale;

            dst.placeholderId = src.placeholderId;
            dst.contactAnchor = src.contactAnchor;
            dst.bakedRefDistLocal = src.bakedRefDistLocal * factor;  // 신모델 전파의 핵심 — 크기 기준
        }
    }

    // EquipSocket 설정 복사 (slotId만 — fit/pivot/anchor는 캡슐 시대와 함께 삭제)
    private static void CopySocketFields(EquipSocket from, GameObject to)
    {
        EquipSocket dst = to.GetComponent<EquipSocket>();
        if (dst == null)
        {
            dst = to.AddComponent<EquipSocket>();
        }
        dst.slotId = from.slotId;
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
