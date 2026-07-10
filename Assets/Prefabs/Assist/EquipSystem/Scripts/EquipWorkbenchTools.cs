using System.Collections.Generic;
using UnityEngine;

// 워크벤치 도구 모음 (완전 standalone): 스모크 회귀 테스트 / 코디(전부 장착·전부 해제·랜덤) / 스케일 테스트.
// 장착은 반드시 EquipManager.Instance.Equip(target, key, out reason) 경유 — 사유가 그대로 리포트에 남는다.
public static class EquipWorkbenchTools
{
    // 스케일 테스트용 원본 백업 (캐릭터 루트 → 최초 적용 전 localScale)
    private static Dictionary<GameObject, Vector3> savedScales = new Dictionary<GameObject, Vector3>();

    // 카탈로그 로드 — EquipManager와 같은 규약 (Resources/EquipCatalog)
    private static EquipCatalog LoadCatalog()
    {
        return Resources.Load<EquipCatalog>("EquipCatalog");
    }

    // 스모크: 전 로스터 × 전 카탈로그 엔트리 장착 시도 → 성공/사유 리포트 반환. 캐릭터별 시도 후 장착물 전부 해제해 복구.
    public static List<string> RunSmokeTest(List<GameObject> roster)
    {
        List<string> report = new List<string>();

        // 데모 가드: 씬에 EquipManager 없음 안내
        if (EquipManager.Instance == null)
        {
            report.Add("[스모크] 씬에 EquipManager 없음 — 빈 GameObject에 EquipManager를 추가하세요");
            LogReport(report);
            return report;
        }

        EquipCatalog catalog = LoadCatalog();
        if (catalog == null)
        {
            report.Add("[스모크] Resources/EquipCatalog 없음 — 카탈로그 에셋을 확인하세요");
            LogReport(report);
            return report;
        }

        if (roster == null || roster.Count == 0)
        {
            report.Add("[스모크] 로스터 비어 있음 — 씬에 EquipSocket 보유 캐릭터를 놓고 Refresh 하세요");
            LogReport(report);
            return report;
        }

        int pass = 0;
        int fail = 0;
        report.Add($"[스모크] 시작 — 로스터 {roster.Count} × 엔트리 {catalog.Entries.Count}");

        foreach (GameObject character in roster)
        {
            if (character == null)
            {
                continue;
            }

            foreach (EquipEntry entry in catalog.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                string reason;
                bool ok = EquipManager.Instance.Equip(character, entry.key, out reason);
                if (ok)
                {
                    pass++;
                    report.Add($"OK   {character.name} ← {entry.key}");
                }
                else
                {
                    fail++;
                    report.Add($"FAIL {character.name} ← {entry.key} — {reason}");
                }
            }

            // 시도 후 복구: 이 캐릭터의 장착물 전부 해제 (Destroy는 프레임 말 지연이라 개수는 근사치)
            int removed = RemoveAllEquipped(character);
            report.Add($"복구  {character.name} — 장착물 {removed}개 해제");
        }

        report.Add($"[스모크] 완료 — 성공 {pass} / 실패 {fail}");
        LogReport(report);
        return report;
    }

    // 코디: 카탈로그 전 엔트리를 순서대로 전부 장착 (같은 자리를 노리는 엔트리는 뒤가 앞을 교체)
    public static List<string> EquipAll(GameObject target)
    {
        List<string> report = new List<string>();

        if (target == null)
        {
            report.Add("[코디] 선택된 캐릭터 없음");
            LogReport(report);
            return report;
        }

        // 데모 가드: 씬에 EquipManager 없음 안내
        if (EquipManager.Instance == null)
        {
            report.Add("[코디] 씬에 EquipManager 없음 — 빈 GameObject에 EquipManager를 추가하세요");
            LogReport(report);
            return report;
        }

        EquipCatalog catalog = LoadCatalog();
        if (catalog == null)
        {
            report.Add("[코디] Resources/EquipCatalog 없음 — 카탈로그 에셋을 확인하세요");
            LogReport(report);
            return report;
        }

        int pass = 0;
        int fail = 0;
        foreach (EquipEntry entry in catalog.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key))
            {
                continue;
            }

            string reason;
            bool ok = EquipManager.Instance.Equip(target, entry.key, out reason);
            if (ok)
            {
                pass++;
            }
            else
            {
                fail++;
                report.Add($"FAIL {entry.key} — {reason}");
            }
        }

        report.Add($"[코디] 전부 장착 {target.name} — 성공 {pass} / 실패 {fail}");
        LogReport(report);
        return report;
    }

    // 코디: 선택 캐릭터의 장착물 전부 해제
    public static string UnequipAll(GameObject target)
    {
        if (target == null)
        {
            return "[코디] 선택된 캐릭터 없음";
        }

        int removed = RemoveAllEquipped(target);
        string msg = $"[코디] 전부 해제 {target.name} — {removed}개";
        Debug.Log("[EquipWorkbenchTools] " + msg);
        return msg;
    }

    // 코디: 랜덤 — 기존 장착물을 비우고 엔트리마다 50% 확률로 장착 (하나도 안 뽑히면 1개 보장)
    public static List<string> EquipRandom(GameObject target)
    {
        List<string> report = new List<string>();

        if (target == null)
        {
            report.Add("[코디] 선택된 캐릭터 없음");
            LogReport(report);
            return report;
        }

        // 데모 가드: 씬에 EquipManager 없음 안내
        if (EquipManager.Instance == null)
        {
            report.Add("[코디] 씬에 EquipManager 없음 — 빈 GameObject에 EquipManager를 추가하세요");
            LogReport(report);
            return report;
        }

        EquipCatalog catalog = LoadCatalog();
        if (catalog == null)
        {
            report.Add("[코디] Resources/EquipCatalog 없음 — 카탈로그 에셋을 확인하세요");
            LogReport(report);
            return report;
        }

        List<EquipEntry> valid = new List<EquipEntry>();
        foreach (EquipEntry entry in catalog.Entries)
        {
            if (entry != null && string.IsNullOrEmpty(entry.key) == false)
            {
                valid.Add(entry);
            }
        }

        if (valid.Count == 0)
        {
            report.Add("[코디] 카탈로그에 유효한 엔트리 없음");
            LogReport(report);
            return report;
        }

        // 랜덤 시작은 깨끗하게 — 기존 장착물 전부 해제
        RemoveAllEquipped(target);

        List<EquipEntry> picked = new List<EquipEntry>();
        foreach (EquipEntry entry in valid)
        {
            if (Random.value < 0.5f)
            {
                picked.Add(entry);
            }
        }

        if (picked.Count == 0)
        {
            picked.Add(valid[Random.Range(0, valid.Count)]);
        }

        int pass = 0;
        int fail = 0;
        foreach (EquipEntry entry in picked)
        {
            string reason;
            bool ok = EquipManager.Instance.Equip(target, entry.key, out reason);
            if (ok)
            {
                pass++;
                report.Add($"OK   {entry.key}");
            }
            else
            {
                fail++;
                report.Add($"FAIL {entry.key} — {reason}");
            }
        }

        report.Add($"[코디] 랜덤 {target.name} — 뽑기 {picked.Count} / 성공 {pass} / 실패 {fail}");
        LogReport(report);
        return report;
    }

    // 스케일 테스트: 선택 캐릭터 루트 스케일에 배율 적용 (원본은 최초 1회 백업 — 여러 번 곱해도 복원은 한 번에)
    public static string ApplyScale(GameObject target, float multiplier)
    {
        if (target == null)
        {
            return "[스케일] 선택된 캐릭터 없음";
        }

        if (multiplier <= 0f)
        {
            return "[스케일] 배율은 0보다 커야 합니다";
        }

        if (savedScales.ContainsKey(target) == false)
        {
            savedScales.Add(target, target.transform.localScale);
        }

        target.transform.localScale = target.transform.localScale * multiplier;
        string msg = $"[스케일] {target.name} ×{multiplier} → localScale {target.transform.localScale.x:G4}";
        Debug.Log("[EquipWorkbenchTools] " + msg);
        return msg;
    }

    // 스케일 테스트: 백업해둔 원본 스케일로 복원
    public static string RestoreScale(GameObject target)
    {
        if (target == null)
        {
            return "[스케일] 선택된 캐릭터 없음";
        }

        Vector3 original;
        if (savedScales.TryGetValue(target, out original) == false)
        {
            return $"[스케일] 백업 없음 — {target.name}에 배율을 적용한 적 없음";
        }

        target.transform.localScale = original;
        savedScales.Remove(target);
        string msg = $"[스케일] {target.name} 복원 → localScale {original.x:G4}";
        Debug.Log("[EquipWorkbenchTools] " + msg);
        return msg;
    }

    // 스케일 백업 보유 여부 — 워크벤치 UI가 복원 버튼 표시 판단에 사용
    public static bool HasSavedScale(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        return savedScales.ContainsKey(target);
    }

    // 캐릭터 하위 장착물(EquipMarker) 전부 제거. Destroy는 프레임 말 지연 파괴 — 중복 호출 무해, 반환 개수는 근사치.
    private static int RemoveAllEquipped(GameObject target)
    {
        if (target == null)
        {
            return 0;
        }

        EquipMarker[] marks = target.GetComponentsInChildren<EquipMarker>(true);
        int removed = 0;
        foreach (EquipMarker mark in marks)
        {
            if (mark != null)
            {
                Object.Destroy(mark.gameObject);
                removed++;
            }
        }

        return removed;
    }

    // 리포트 전 줄을 콘솔에도 남긴다 (링버퍼 UI가 밀어내도 콘솔에는 전체가 남게)
    private static void LogReport(List<string> report)
    {
        foreach (string line in report)
        {
            Debug.Log("[EquipWorkbenchTools] " + line);
        }
    }
}

// EquipWorkbenchMarkers는 전용 파일(Scripts/EquipWorkbenchMarkers.cs)로 분리 —
// 에디트 모드 씬 베이크(AddComponent+직렬화)는 파일명=클래스명인 MonoBehaviour만 가능하기 때문.
