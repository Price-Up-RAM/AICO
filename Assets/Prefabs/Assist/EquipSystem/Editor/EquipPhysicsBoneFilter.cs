using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 물리 시뮬 본 감지 (NEAREST 폴백의 안전망 전용 — 사용자 결정: 흔들림 연출은 비목표).
// 2단 수집: (1) MagicaCloth류의 rootBones 경로 참조 우선 → (2) 공집합이면 Transform 참조 전량 폴백.
// 수집된 물리 루트가 스킨 본 과반을 자손으로 갖으면(=본체 스켈레톤 오염) 그 루트는 버린다.
public static class EquipPhysicsBoneFilter
{
    // 물리 본으로 의심할 이름 패턴 (소문자 비교)
    private static readonly string[] PhysicsNamePatterns =
    {
        "hair", "skirt", "tail", "breast", "bust", "sleeve", "ribbon",
        "髪", "スカート", "胸", "裾", "リボン", "尻尾",
    };

    // 캐릭터 계층에서 물리 본 집합 수집 (자손 확장 + 과반 오염 가드)
    public static HashSet<Transform> CollectPhysicsBones(Transform root)
    {
        HashSet<Transform> rootBoneRefs = new HashSet<Transform>();   // rootBones 경로 참조
        HashSet<Transform> anyRefs = new HashSet<Transform>();        // 모든 Transform 참조 (폴백)

        // 1) MagicaCloth류 컴포넌트 순회 (타입명 문자열 매칭 — 컴파일 의존 없음)
        MonoBehaviour[] comps = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour comp in comps)
        {
            if (comp == null)
            {
                continue;
            }

            string typeName = comp.GetType().FullName;
            if (typeName == null || typeName.Contains("MagicaCloth") == false)
            {
                continue;
            }

            CollectTransformRefs(comp, root, rootBoneRefs, anyRefs);
        }

        // 2) rootBones 참조 우선, 공집합이면 전량 폴백
        HashSet<Transform> physicsRoots = rootBoneRefs;
        if (physicsRoots.Count == 0)
        {
            physicsRoots = anyRefs;
        }

        // 3) 자손 확장 + 과반 오염 가드 (스킨 본의 50% 이상을 덮는 루트는 스켈레톤 상위 참조로 보고 버림)
        HashSet<Transform> skinBones = EquipAuthoringUtil.CollectSkinBones(root);
        int guardLimit = Mathf.Max(1, skinBones.Count / 2);

        HashSet<Transform> result = new HashSet<Transform>();
        foreach (Transform pr in physicsRoots)
        {
            Transform[] descendants = pr.GetComponentsInChildren<Transform>(true);

            // 이 루트가 덮는 스킨 본 수 계산
            int covered = 0;
            foreach (Transform d in descendants)
            {
                if (skinBones.Contains(d))
                {
                    covered = covered + 1;
                }
            }

            if (skinBones.Count > 0 && covered >= guardLimit)
            {
                // 스켈레톤 상위(hips/루트 등) 참조 — 물리 루트로 오인하면 전 골격이 오염되므로 버림
                continue;
            }

            foreach (Transform d in descendants)
            {
                result.Add(d);
            }
        }

        return result;
    }

    // 컴포넌트의 SerializedProperty를 순회하며 root 하위 Transform 참조 수집.
    // rootBones 경로 참조는 rootBoneRefs에, 그 외 전부는 anyRefs에. (대형 배열/문자열은 진입 생략)
    private static void CollectTransformRefs(MonoBehaviour comp, Transform root, HashSet<Transform> rootBoneRefs, HashSet<Transform> anyRefs)
    {
        using (SerializedObject so = new SerializedObject(comp))
        {
            SerializedProperty prop = so.GetIterator();

            bool enter = true;
            while (prop.Next(enter))
            {
                enter = true;

                // 성능 가드: 문자열/대형 배열 내부로 진입하지 않음 (MeshCloth 정점 데이터 등)
                if (prop.propertyType == SerializedPropertyType.String)
                {
                    enter = false;
                    continue;
                }
                if (prop.isArray && prop.propertyType == SerializedPropertyType.Generic && prop.arraySize > 2048)
                {
                    enter = false;
                    continue;
                }

                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Transform t = prop.objectReferenceValue as Transform;
                    if (t != null && t.IsChildOf(root))
                    {
                        anyRefs.Add(t);
                        if (prop.propertyPath.Contains("rootBones"))
                        {
                            rootBoneRefs.Add(t);
                        }
                    }
                    // 오브젝트 참조 내부로는 들어가지 않음
                    enter = false;
                }
            }
        }
    }

    // 본 하나가 물리 본으로 의심되는지 (수집 집합 + 이름 패턴 병행)
    public static bool IsPhysicsSuspect(Transform bone, HashSet<Transform> physicsBones)
    {
        if (bone == null)
        {
            return false;
        }

        if (physicsBones != null && physicsBones.Contains(bone))
        {
            return true;
        }

        string lower = bone.name.ToLowerInvariant();
        foreach (string pattern in PhysicsNamePatterns)
        {
            if (lower.Contains(pattern))
            {
                return true;
            }
        }

        return false;
    }
}
