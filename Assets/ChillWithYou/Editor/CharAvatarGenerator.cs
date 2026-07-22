#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Char_toon(Generic 리그) 캐릭터들의 착석용 휴머노이드 Avatar를 일괄 생성해 CharAvatarSO에 매핑한다.
///
/// 생성 경로 2단계:
///  (1) importer 경로 — 원본 FBX를 임시 복사 → Humanoid 임포트(유니티 자동 매핑/T포즈 보정) →
///      생성된 Avatar를 독립 .asset으로 복제 후 임시 FBX 삭제. (사용자가 CH0069B로 수동 검증한 절차의 자동화)
///  (2) builder 경로 — 임포터 아바타의 본 경로가 런타임 프리팹 계층과 다르면(래퍼 노드 등:
///      Momoi_Original류/CH0293/CH0334) 프리팹 계층에서 직접 AvatarBuilder.BuildHumanAvatar로 생성.
///      본 매핑은 (1)의 자동 매핑 결과를 재사용. 프리팹 저장 포즈가 T포즈가 아니면 품질이 떨어질 수
///      있으므로 로그에 route=builder로 표시해 시각 확인 대상임을 알린다.
///
/// 산출물: Assets/CharAvatars/&lt;charcode&gt;Avatar.asset 일괄 + CharAvatarSO.asset(폴백=SimpleBAAvatar)
///        + ChillModeManager.prefab의 charAvatarData 배선.
/// 멱등: 재실행 시 아바타 재생성(갱신), SO 엔트리 upsert, 폴백 이동/배선은 이미 됐으면 통과.
/// batchmode: -executeMethod CharAvatarGenerator.GenerateBatch
/// </summary>
public static class CharAvatarGenerator
{
    private const string AvatarFolder = "Assets/CharAvatars";
    private const string SoPath = "Assets/ChillWithYou/ScriptableObjects/CharAvatarSO.asset";
    private const string LegacyFallbackAvatarPath = "Assets/Char/CH0069/CH0069BAvatar.asset";
    private const string FallbackAvatarPath = AvatarFolder + "/SimpleBAAvatar.asset";
    private const string CharToonFolder = "Assets/Prefabs/Char_toon";
    private const string ChillManagerPrefabPath = "Assets/ChillWithYou/Prefabs/ChillModeManager.prefab";

    private class Target
    {
        public string charcode;
        public string prefabPath;
        public string modelPath;
    }

    [MenuItem("Tools/ChillWithYou/5. Generate Char Avatars (CharAvatarSO)")]
    public static void Generate()
    {
        EnsureFolder();

        Avatar fallback = EnsureFallbackAvatar();
        if (fallback == null)
        {
            throw new System.Exception("[CharAvatar] 폴백 아바타(SimpleBAAvatar) 확보 실패 — 중단합니다.");
        }

        CharAvatarSO so = EnsureSO(fallback);

        List<Target> targets = CollectTargets();
        StringBuilder report = new StringBuilder();
        int okCount = 0, failCount = 0;
        foreach (Target target in targets)
        {
            string result;
            try
            {
                result = GenerateForTarget(target, so);
            }
            catch (System.Exception e)
            {
                result = "FAIL: 예외 — " + e.Message;
            }
            if (result.StartsWith("OK")) okCount++; else failCount++;
            string line = string.Format("{0,-14} {1,-34} {2}", target.charcode,
                System.IO.Path.GetFileName(target.prefabPath), result);
            report.AppendLine(line);
            Debug.Log("[CharAvatar] " + line);
        }

        WireManagerPrefab(so);

        EditorUtility.SetDirty(so);
        AssetDatabase.SaveAssets();

        Debug.Log("[CharAvatar] ==== 결과 요약: OK " + okCount + " / FAIL " + failCount + " (대상 " + targets.Count + ") ====\n" + report);
    }

    /// <summary>batchmode 진입점.</summary>
    public static void GenerateBatch()
    {
        Generate();
    }

    // ---------------------------------------------------------------- 대상 수집

    // Char_toon 프리팹 중 "루트 CharAttributes(charcode) + 루트 Animator + 아바타 없음"인 것만 대상.
    // (본편 착석 게이트와 동일 조건 — 루트 Animator가 없으면 어차피 EnterChillMode 대상이 아니다)
    private static List<Target> CollectTargets()
    {
        var targets = new List<Target>();
        var seen = new HashSet<string>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { CharToonFolder });
        foreach (string path in guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.Contains("_POC"))
            {
                continue;  // 데모 복사본 — charcode가 원본과 같아 원본 매핑으로 커버됨
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            CharAttributes attrs = prefab.GetComponent<CharAttributes>();
            if (attrs == null || string.IsNullOrEmpty(attrs.charcode))
            {
                Debug.LogWarning("[CharAvatar] 루트 CharAttributes/charcode 없음 → 건너뜀: " + path);
                continue;
            }

            Animator animator = prefab.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("[CharAvatar] 루트 Animator 없음 → 건너뜀: " + path);
                continue;
            }
            if (animator.avatar != null)
            {
                Debug.Log("[CharAvatar] 자체 아바타 보유 → 건너뜀: " + name + " (charcode=" + attrs.charcode + ")");
                continue;  // arona 계열/CH0273_Blender_Hum/mari_pajama 등 — 이미 착석 가능
            }

            if (!seen.Add(attrs.charcode))
            {
                continue;  // 동일 charcode의 변형 프리팹은 첫 항목만 (매핑은 charcode 단위)
            }

            string modelPath = FindModelPath(prefab);
            if (string.IsNullOrEmpty(modelPath))
            {
                Debug.LogWarning("[CharAvatar] 모델(FBX) 경로를 찾지 못함 → 건너뜀: " + path);
                continue;
            }

            targets.Add(new Target { charcode = attrs.charcode, prefabPath = path, modelPath = modelPath });
        }
        return targets;
    }

    // 프리팹의 SkinnedMeshRenderer가 참조하는 메시의 소스 FBX 경로
    private static string FindModelPath(GameObject prefab)
    {
        foreach (SkinnedMeshRenderer smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null)
            {
                continue;
            }
            string path = AssetDatabase.GetAssetPath(smr.sharedMesh);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- 캐릭터별 아바타 생성

    private static string GenerateForTarget(Target target, CharAvatarSO so)
    {
        string tempPath = AvatarFolder + "/Temp_" + target.charcode + ".fbx";
        AssetDatabase.DeleteAsset(tempPath);  // 이전 실패 잔재 정리

        try
        {
            if (!AssetDatabase.CopyAsset(target.modelPath, tempPath))
            {
                return "FAIL: FBX 임시 복사 실패 (" + target.modelPath + ")";
            }

            ModelImporter importer = AssetImporter.GetAtPath(tempPath) as ModelImporter;
            if (importer == null)
            {
                return "FAIL: ModelImporter 획득 실패";
            }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            Avatar importedAvatar = AssetDatabase.LoadAllAssetsAtPath(tempPath).OfType<Avatar>().FirstOrDefault();
            if (importedAvatar == null || !importedAvatar.isValid || !importedAvatar.isHuman)
            {
                return "FAIL: Humanoid 자동 매핑 실패 — 수동 생성 대상 (T포즈/본 구조 확인 필요)";
            }

            HumanBone[] humanBones = importedAvatar.humanDescription.human;

            // 경로 검증: 임포터 아바타는 FBX 계층 기준 경로를 쓰므로, 런타임 프리팹(루트 Animator 기준)과
            // 사람 본 경로가 전부 일치해야 그대로 재사용할 수 있다
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(target.prefabPath);
            GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(tempPath);
            bool pathsMatch = true;
            foreach (HumanBone humanBone in humanBones)
            {
                string prefabBonePath = FindPathByName(prefab.transform, humanBone.boneName);
                string modelBonePath = FindPathByName(modelRoot.transform, humanBone.boneName);
                if (prefabBonePath == null)
                {
                    return "FAIL: 프리팹에 매핑 본 없음: " + humanBone.boneName;
                }
                if (prefabBonePath != modelBonePath)
                {
                    pathsMatch = false;
                    break;
                }
            }

            Avatar finalAvatar;
            string route;
            if (pathsMatch)
            {
                finalAvatar = Object.Instantiate(importedAvatar);
                route = "importer";
            }
            else
            {
                finalAvatar = BuildFromPrefab(target.prefabPath, humanBones);
                route = "builder";
                if (finalAvatar == null || !finalAvatar.isValid || !finalAvatar.isHuman)
                {
                    return "FAIL: 본 경로 불일치 + AvatarBuilder 생성 실패 — 수동 생성 대상";
                }
            }

            finalAvatar.name = target.charcode + "Avatar";
            string assetPath = AvatarFolder + "/" + target.charcode + "Avatar.asset";
            AssetDatabase.DeleteAsset(assetPath);  // 재실행 갱신
            AssetDatabase.CreateAsset(finalAvatar, assetPath);
            so.SetEntry(target.charcode, finalAvatar);
            return "OK(" + route + ")";
        }
        finally
        {
            AssetDatabase.DeleteAsset(tempPath);
        }
    }

    // 프리팹 계층에서 직접 휴머노이드 아바타 생성 (본 경로가 FBX와 다른 래퍼 구조용).
    // 스켈레톤 기본 포즈 = 프리팹 저장 포즈. 사람 본 매핑은 임포터 자동 매핑 결과를 재사용.
    private static Avatar BuildFromPrefab(string prefabPath, HumanBone[] humanBones)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            Animator animator = instance.GetComponent<Animator>();
            GameObject root = animator != null ? animator.gameObject : instance;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            var skeleton = new SkeletonBone[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                skeleton[i] = new SkeletonBone
                {
                    name = all[i].name,
                    position = all[i].localPosition,
                    rotation = all[i].localRotation,
                    scale = all[i].localScale,
                };
            }

            var human = new List<HumanBone>();
            foreach (HumanBone humanBone in humanBones)
            {
                if (FindPathByName(root.transform, humanBone.boneName) == null)
                {
                    continue;
                }
                human.Add(new HumanBone
                {
                    boneName = humanBone.boneName,
                    humanName = humanBone.humanName,
                    limit = new HumanLimit { useDefaultValues = true },
                });
            }

            HumanDescription description = new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton,
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };
            return AvatarBuilder.BuildHumanAvatar(root, description);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[CharAvatar] AvatarBuilder 예외 (" + prefabPath + "): " + e.Message);
            return null;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // ---------------------------------------------------------------- 폴백/SO/프리팹 배선

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(AvatarFolder))
        {
            AssetDatabase.CreateFolder("Assets", "CharAvatars");
        }
    }

    // CH0069BAvatar → Assets/CharAvatars/SimpleBAAvatar.asset 이동+개명 (GUID 보존, 멱등)
    private static Avatar EnsureFallbackAvatar()
    {
        Avatar fallback = AssetDatabase.LoadAssetAtPath<Avatar>(FallbackAvatarPath);
        if (fallback != null)
        {
            return fallback;
        }

        if (AssetDatabase.LoadAssetAtPath<Avatar>(LegacyFallbackAvatarPath) != null)
        {
            string error = AssetDatabase.MoveAsset(LegacyFallbackAvatarPath, FallbackAvatarPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("[CharAvatar] SimpleBAAvatar 이동 실패: " + error);
                return null;
            }
            fallback = AssetDatabase.LoadAssetAtPath<Avatar>(FallbackAvatarPath);
            fallback.name = "SimpleBAAvatar";
            EditorUtility.SetDirty(fallback);
            Debug.Log("[CharAvatar] CH0069BAvatar → " + FallbackAvatarPath + " 이동/개명 완료");
            return fallback;
        }

        Debug.LogError("[CharAvatar] 폴백 아바타 원본이 없습니다: " + LegacyFallbackAvatarPath);
        return null;
    }

    private static CharAvatarSO EnsureSO(Avatar fallback)
    {
        CharAvatarSO so = AssetDatabase.LoadAssetAtPath<CharAvatarSO>(SoPath);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<CharAvatarSO>();
            AssetDatabase.CreateAsset(so, SoPath);
            Debug.Log("[CharAvatar] CharAvatarSO 생성: " + SoPath);
        }
        so.fallbackAvatar = fallback;
        return so;
    }

    private static void WireManagerPrefab(CharAvatarSO so)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ChillManagerPrefabPath);
        try
        {
            ChillModeManager manager = root.GetComponent<ChillModeManager>();
            if (manager == null)
            {
                Debug.LogError("[CharAvatar] ChillModeManager 컴포넌트를 찾을 수 없습니다: " + ChillManagerPrefabPath);
                return;
            }
            if (manager.charAvatarData != so)
            {
                manager.charAvatarData = so;
                PrefabUtility.SaveAsPrefabAsset(root, ChillManagerPrefabPath);
                Debug.Log("[CharAvatar] ChillModeManager.prefab에 charAvatarData 배선 완료");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- 경로 유틸

    // root 하위에서 이름이 일치하는 첫 트랜스폼의 root 기준 상대 경로 (root 자신 제외)
    private static string FindPathByName(Transform root, string name)
    {
        foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
        {
            if (tr == root || tr.name != name)
            {
                continue;
            }
            return GetRelativePath(root, tr);
        }
        return null;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        var segments = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            segments.Add(current.name);
            current = current.parent;
        }
        if (current != root)
        {
            return null;
        }
        segments.Reverse();
        return string.Join("/", segments);
    }
}
#endif
