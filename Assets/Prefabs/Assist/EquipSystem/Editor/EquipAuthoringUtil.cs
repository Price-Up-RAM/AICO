using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 저작 도구 공용 유틸 (스탬퍼/저작 창이 공유): 계산 + 기본 템플릿 접근.
public static class EquipAuthoringUtil
{
    // ── 기본 템플릿 에셋 (Editor/Templates — Resources 금지) ──
    private const string TemplateDir = "Assets/Prefabs/Assist/EquipSystem/Editor/Templates";
    private const string DefaultTemplatePath = "Assets/Prefabs/Assist/EquipSystem/Editor/Templates/EquipSlotTemplate_Default.asset";

    // 기본 템플릿 로드/생성
    public static EquipSlotTemplate GetOrCreateDefaultTemplate()
    {
        if (AssetDatabase.IsValidFolder(TemplateDir) == false)
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/Assist/EquipSystem/Editor", "Templates");
        }

        EquipSlotTemplate template = AssetDatabase.LoadAssetAtPath<EquipSlotTemplate>(DefaultTemplatePath);
        if (template == null)
        {
            template = ScriptableObject.CreateInstance<EquipSlotTemplate>();
            AssetDatabase.CreateAsset(template, DefaultTemplatePath);
        }
        return template;
    }

    // Humanoid 본 해석 (에딧모드에서 GetBoneTransform이 null이면 humanDescription 이름 매핑 폴백)
    public static Transform ResolveHumanoidBone(GameObject root, int humanoidBone)
    {
        if (humanoidBone < 0 || humanoidBone >= (int)HumanBodyBones.LastBone)
        {
            return null;
        }

        Animator anim = root.GetComponentInChildren<Animator>(true);
        if (anim == null || anim.avatar == null || anim.avatar.isHuman == false)
        {
            return null;
        }

        // 1) 직접 시도
        Transform t = null;
        try
        {
            t = anim.GetBoneTransform((HumanBodyBones)humanoidBone);
        }
        catch (System.Exception)
        {
            t = null;
        }
        if (t != null)
        {
            return t;
        }

        // 2) humanDescription의 humanName→boneName 매핑으로 이름 탐색
        try
        {
            string humanName = HumanTrait.BoneName[humanoidBone];
            HumanBone[] humans = anim.avatar.humanDescription.human;
            foreach (HumanBone hb in humans)
            {
                if (hb.humanName == humanName)
                {
                    return FindByName(root.transform, hb.boneName);
                }
            }
        }
        catch (System.Exception)
        {
            // avatar 데이터 접근 실패 — 폴백 없음
        }

        return null;
    }
    // 바운드 측정에서 제외할 오브젝트 이름 패턴 (헤일로 등 캐릭터 키를 오염시키는 부속물)
    private static readonly string[] BoundsExcludePatterns = { "halo" };

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

    // 이름이 바운드 제외 패턴에 걸리는지
    private static bool IsBoundsExcluded(string goName)
    {
        string lower = goName.ToLowerInvariant();
        foreach (string pattern in BoundsExcludePatterns)
        {
            if (lower.Contains(pattern))
            {
                return true;
            }
        }
        return false;
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

    // 본 이름을 소문자 영숫자 토큰으로 분해 ("Bip001 Head" → [bip001, head], "mixamorig:Head" → [mixamorig, head])
    public static List<string> TokenizeBoneName(string boneName)
    {
        List<string> tokens = new List<string>();
        if (string.IsNullOrEmpty(boneName))
        {
            return tokens;
        }

        string lower = boneName.ToLowerInvariant();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        foreach (char c in lower)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Length = 0;
                }
            }
        }
        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }
        return tokens;
    }

    // 본 이름이 별칭 목록 중 하나와 토큰 일치하는지 (부분 문자열이 아니라 토큰 단위 — headtop은 head에 불일치)
    public static bool BoneMatchesAlias(string boneName, List<string> aliases)
    {
        if (aliases == null || aliases.Count == 0)
        {
            return false;
        }

        List<string> tokens = TokenizeBoneName(boneName);
        foreach (string alias in aliases)
        {
            if (string.IsNullOrEmpty(alias))
            {
                continue;
            }

            string a = alias.ToLowerInvariant();
            foreach (string token in tokens)
            {
                if (token == a)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // 별칭에 토큰 일치하는 본들 중 최적 후보 선택 (이름이 짧은 쪽 = 루트에 가까운 canonical 본 우선)
    public static Transform FindBoneByAlias(IEnumerable<Transform> bones, List<string> aliases, out int matchCount)
    {
        Transform best = null;
        matchCount = 0;

        foreach (Transform b in bones)
        {
            if (b == null)
            {
                continue;
            }

            if (BoneMatchesAlias(b.name, aliases))
            {
                matchCount = matchCount + 1;
                if (best == null)
                {
                    best = b;
                }
                else
                {
                    if (b.name.Length < best.name.Length)
                    {
                        best = b;
                    }
                }
            }
        }
        return best;
    }

    // targetPos에 가장 가까운 본 (excluded 집합 제외)
    public static Transform FindNearestBone(IEnumerable<Transform> bones, Vector3 targetPos, HashSet<Transform> excluded)
    {
        Transform nearest = null;
        float best = float.MaxValue;

        foreach (Transform b in bones)
        {
            if (b == null)
            {
                continue;
            }

            if (excluded != null && excluded.Contains(b))
            {
                continue;
            }

            float d = (b.position - targetPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = b;
            }
        }
        return nearest;
    }

    // Transform의 lossyScale 절대값 평균 (0 방지)
    public static float LossyAvg(Transform t)
    {
        Vector3 ls = t.lossyScale;
        float avg = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
        if (avg <= 1e-8f)
        {
            return 1f;
        }
        return avg;
    }

    // 소켓의 캡슐 월드 길이 (콜라이더 로컬 치수 × lossy 평균)
    public static float CapsuleWorldLength(EquipSocket socket)
    {
        Collider col = socket.SizingVolume;
        if (col == null)
        {
            return 0f;
        }
        return EquipFitter.GetVolumeLength(col) * LossyAvg(socket.transform);
    }

    // 소켓 GO에 캡슐을 월드 길이 기준으로 세팅 (로컬 환산). worldLength<=0이면 0크기 캡슐을 만들지 않고 null 반환.
    public static CapsuleCollider SetCapsuleByWorldLength(GameObject socketGo, float worldLength, int direction)
    {
        if (worldLength <= 1e-12f)
        {
            return null;
        }

        float lossyAvg = LossyAvg(socketGo.transform);
        float capLocal = worldLength / lossyAvg;

        CapsuleCollider cap = socketGo.GetComponent<CapsuleCollider>();
        if (cap == null)
        {
            cap = socketGo.AddComponent<CapsuleCollider>();
        }
        cap.isTrigger = true;
        cap.direction = direction;
        cap.center = Vector3.zero;
        cap.height = capLocal;
        cap.radius = capLocal * 0.33f;
        return cap;
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
