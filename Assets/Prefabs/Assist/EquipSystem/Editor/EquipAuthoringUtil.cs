using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 저작 도구 공용 유틸 (스탬퍼/저작 창이 공유): 계산 + 기본 템플릿 접근.
public static class EquipAuthoringUtil
{
    // 바운드/레이캐스트에서 제외할 오브젝트 이름 패턴 (헤일로 등 캐릭터 표면이 아닌 부속물)
    public static readonly string[] ExcludeNamePatterns = { "halo", "ハロ", "光輪", "天使の輪" };

    // 이름이 제외 패턴에 걸리는지 (공용)
    public static bool IsExcludedName(string goName)
    {
        string lower = goName.ToLowerInvariant();
        foreach (string pattern in ExcludeNamePatterns)
        {
            if (lower.Contains(pattern.ToLowerInvariant()))
            {
                return true;
            }
        }
        return false;
    }

    // 대상이 속한 캐릭터 루트 결정: Animator 우선, 없으면 렌더러 보유 조상 중
    // "이웃 캐릭터(Animator 2개 이상)를 삼키기 직전"의 마지막 조상 (공용 부모 씬 방어)
    public static Transform ResolveCharRoot(Transform t)
    {
        if (t == null)
        {
            return null;
        }

        Animator anim = t.GetComponentInParent<Animator>();
        if (anim != null)
        {
            return anim.transform;
        }

        Transform best = t;
        Transform cur = t.parent;
        while (cur != null)
        {
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

    // 캐릭터 전체 렌더러 바운드 (월드). 활성 렌더러 한정 + 제외 패턴 적용.
    public static bool MeasureBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(false); // 활성만
        bool has = false;

        foreach (Renderer r in rs)
        {
            if (r == null)
            {
                continue;
            }

            // 컴포넌트 disable 방식의 대체 의상/이펙트 메시 제외
            if (r.enabled == false)
            {
                continue;
            }

            if (IsBoundsExcluded(r.gameObject.name))
            {
                continue;
            }

            if (has == false)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        // 활성 렌더러가 하나도 없으면 비활성 포함 재시도 (프리팹 상태에 따라 전부 꺼져있을 수 있음)
        if (has == false)
        {
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in all)
            {
                if (r == null)
                {
                    continue;
                }

                if (IsBoundsExcluded(r.gameObject.name))
                {
                    continue;
                }

                if (has == false)
                {
                    bounds = r.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
        }

        return has;
    }

    // 이름이 바운드 제외 패턴에 걸리는지 (공용 IsExcludedName에 위임)
    private static bool IsBoundsExcluded(string goName)
    {
        return IsExcludedName(goName);
    }

    // 캐릭터 키(월드 높이). 실패 시 0.
    public static float MeasureCharHeight(GameObject root)
    {
        Bounds b;
        if (MeasureBounds(root, out b))
        {
            return b.size.y;
        }
        return 0f;
    }

    // 이름으로 하위 Transform 탐색 (루트 포함 전체 계층)
    public static Transform FindByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (t.name == name)
            {
                return t;
            }
        }
        return null;
    }

    // slotId로 소켓 탐색 (루트 전체 계층 — 부모 무관, 중복 생성 방지용)
    public static EquipSocket FindSocketBySlotId(Transform root, string slotId)
    {
        if (root == null || string.IsNullOrEmpty(slotId))
        {
            return null;
        }

        EquipSocket[] sockets = root.GetComponentsInChildren<EquipSocket>(true);
        foreach (EquipSocket s in sockets)
        {
            if (s != null && s.slotId == slotId)
            {
                return s;
            }
        }
        return null;
    }

    // 모든 SkinnedMeshRenderer의 본 집합 수집 (리그 이름 무관)
    public static HashSet<Transform> CollectSkinBones(Transform root)
    {
        HashSet<Transform> set = new HashSet<Transform>();
        SkinnedMeshRenderer[] smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        foreach (SkinnedMeshRenderer smr in smrs)
        {
            if (smr == null || smr.bones == null)
            {
                continue;
            }

            foreach (Transform b in smr.bones)
            {
                if (b != null)
                {
                    set.Add(b);
                }
            }
        }
        return set;
    }

    // Transform의 lossyScale 절대값 평균 (0 방지)
    public static float LossyAvg(Transform t)
    {
        // 단일 구현으로 위임 (EquipMath — 캡슐 무관 공용 수학)
        return EquipMath.LossyAvg(t);
    }

    // 이름 일치 Transform 개수 (동명 본 감지용)
    public static int CountByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
        {
            return 0;
        }

        int count = 0;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (t.name == name)
            {
                count = count + 1;
            }
        }
        return count;
    }

    // Transform이 소켓 계열(EquipSocket 부착 GO 또는 그 자손)인지 — 본 후보에서 제외용
    public static bool IsSocketOrChildOfSocket(Transform t)
    {
        if (t == null)
        {
            return false;
        }
        return t.GetComponentInParent<EquipSocket>() != null;
    }
}
