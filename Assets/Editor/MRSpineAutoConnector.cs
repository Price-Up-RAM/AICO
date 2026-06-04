#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Spine.Unity;

public static class MRSpineAutoConnector
{
    // ====== 경로 규칙 ======
    private const string AudiosRoot = "Assets/Audios";  // Assets/Audios/{char(lower)}/...
    private const string SpinesRoot = "Assets/Spines";  // Assets/Spines/{Char}[SkinN]/Standing/...
    private const string StandingFolder = "Standing";

    // ====== 음성 키워드(확정) ======
    private static readonly string[] BallEndKeywords = { "Touch1", "Touch1_1", "Touch1_2" };
    private static readonly string[] PatEndKeywords = { "Touch2", "Touch2_1", "Touch2_2" };
    private static readonly string[] SmashEndKeywords = { "DutchRubEnd2" };

    // ====== 기본(베이스) 보이스 자동 연결 키워드 맵 ======
    private static readonly Dictionary<string, List<string>> BaseVoiceKeywordMap = new()
    {
        { "idleVoices", new List<string>
            {
                "Affinity1_1","Affinity1_2","Affinity1_3",
                "Affinity2","Affinity3","Affinity4","Affinity5","Affinity6","Affinity7",
                "AsideGet","AsideUpgrade",
                "CallPlayer1","CallPlayer2","CallPlayer3","CallPlayer4","CallPlayer5",
                "Hmm1","Hmm2","Hmm3","Hmm4","Hmm5",
                "Joy1","Joy2","Joy3","Joy4","Joy5",
                "Pleasure1","Pleasure2","Pleasure3","Pleasure4","Pleasure5"
            }
        },
        { "eatVoices", new List<string> { "Eat1","Eat2","Eat" } },
        { "smashVoices", new List<string> { "DutchRubEnd1" } },
        { "tickle1Voices", new List<string> { "TickleStart1" } },
        { "tickle2Voices", new List<string> { "TickleDuring1" } },

        { "ballEndVoices", BallEndKeywords.ToList() },
        { "patEndVoices",  PatEndKeywords.ToList() },
        { "smashEndVoices", SmashEndKeywords.ToList() },
    };
    
    // ===== Animation name keywords =====
    private static readonly string[] IdleAnimKeywords =
    {
    "Dance",    "Taunt",    "Singing",    "Rhythm",    "Jump",    "Sing",    "Happy",    "Joke",
    "Proud",    "Sulky",    "Pride",    "Smile",    "Nicesmile",    "Melong",    "Laugh",    "TIckle",
    "Excited",    "Victory",    "Hug",    "Pray",    "Baldo",    "Quiet",    "Pose",    "Salute",
    "Squat",    "Strong",    "Scouter",    "Clap",    "Cook",    "Heart",    "Dehet",    "Smirking",
    "Beam",    "Mirror",    "Spitter",    "Act",    "Glasses",    "Smell",    "Greeting",    "Posing",
    "Annoy",    "Fear",    "Splash",    "Wink",    "Read",    "Write",    "Dash",    "Drift",
    "Drive",    "Jackson",    "Knee",    "Clean",    "Stand",    "Pistol",    "Revolver",    "Rummage",
    "Ganzi",    "Sijeo",    "Villain",    "Loudspeaker",    "Merong",    "Money",    "Yare",    "Bbang",
    "Hungry",    "Ottokhaji",    "Present",    "Sneaky",    "Aside",    "Down",    "Innocent",    "Ouch",
    "Punch",    "Robot",    "Sweat",    "Recorder",    "Scream",    "Oioi",    "Drill",    "Tired",
    "Sleepy",    "Sleep",    "Rest",    "Off",    "Relaxed",    "Shy",    "Shame",    "Sorry",    "Blank",
    "Close",    "Notmyfault",    "Think",    "Thinking",    "Question",    "Hi",    "Idle",    "Talk",
    "Hesitate",    "Nodding",    "Point",    "Worry",    "Check",    "Latte",    "Groggy",    "Laser",
    "Call",    "Lying",    "Urcharyu",    "Dizzy",    "Ignore",    "Help",    "Aiming",    "Closed",
    "Nope",    "Whisper",    "Break",    "Drink",    "Kirat",    "Work",    "Cool",    "Ban",
    "Domo",    "Boring",    "Camera",    "Phone",};

    private static readonly string[] EatAnimKeywords = { "Eat" };

    // ====== 옵션 ======
    private const bool SetStandingSkeletonAnimationAsset = true;          // 스탠딩 SkeletonAnimation에 base asset 세팅
    private const bool InitializeSkeletonAnimationsBeforeBoneLink = true; // BoneLink 전에 SkeletonAnimation.Initialize(true) 강제
    private const bool RunBoneFollowerAutoLinkAtEnd = true;

    // ====== 스킨 정리 옵션 ======
    private const bool PruneEmptySkins = true;   // 비어있는 스킨 항목 제거
    private const bool KeepAtLeastOneBaseSkin = true;

    [MenuItem("Tools/MR Spine/Auto Connect Selected Characters")]
    public static void AutoConnectSelectedCharacters()
    {
        var roots = Selection.gameObjects;
        if (roots == null || roots.Length == 0)
        {
            Debug.LogWarning("[MRSpineAutoConnector] 선택된 오브젝트가 없습니다. 캐릭터 루트를 선택하세요.");
            return;
        }

        int processed = 0;
        int skipped = 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var root in roots)
        {
            if (root == null) continue;

            // MR 프로젝트 스크립트로 교체 (MRSpineCharacterController)
            var controllers = root.GetComponentsInChildren<MRSpineCharacterController>(true);
            if (controllers == null || controllers.Length == 0)
            {
                Debug.LogWarning($"[MRSpineAutoConnector] Skip '{root.name}': 자식에 MRSpineCharacterController가 없습니다.");
                skipped++;
                continue;
            }
            if (controllers.Length > 1)
            {
                Debug.LogWarning($"[MRSpineAutoConnector] Skip '{root.name}': MRSpineCharacterController가 {controllers.Length}개입니다. 유일해야 합니다.");
                skipped++;
                continue;
            }

            var controller = controllers[0];
            if (controller == null)
            {
                skipped++;
                continue;
            }

            string characterName = controller.gameObject.name;               // 예: Kidian
            string characterLower = characterName.ToLowerInvariant();       // 예: kidian

            // (MR 포팅) Vuforia mTrackableName 로직 제거 완료

            // 1) 오디오 인덱스 구축
            var voiceIndex = BuildVoiceIndex(characterName, characterLower);

            // 2) 컨트롤러 base 보이스 리스트 연결
            ConnectBaseVoiceLists(controller, voiceIndex);

            // 3) skins: SkeletonDataAsset 연결 + override 보이스 연결 + (옵션) 빈 스킨 제거
            var baseSkeletonAsset = ConnectSkinsAndOverridesAndMaybePrune(controller, characterName, voiceIndex);

            // 4) 스탠딩 SkeletonAnimation에도 base asset 적용(옵션)
            if (SetStandingSkeletonAnimationAsset && baseSkeletonAsset != null)
                ApplyStandingSkeletonDataAsset(controller, baseSkeletonAsset);

            // 5) BoneLink 전: SkeletonAnimation 강제 Initialize(true) (핵심)
            if (InitializeSkeletonAnimationsBeforeBoneLink)
                EnsurePrimarySkeletonInitialized(controller);
            AutoFillIdleEatAnimationNameLists(controller);
            
            // 6) BoneFollower auto-link
            if (RunBoneFollowerAutoLinkAtEnd)
                AutoLinkBoneFollowersUnderController(controller);

            EditorUtility.SetDirty(controller);
            processed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MRSpineAutoConnector] 완료. processed={processed}, skipped={skipped}");
    }

    // =========================================================
    // Voice Index: Assets/Audios/{lower}/에서 Voice_{Char}_ 접두 제거 인덱스 (대소문자 무시)
    // =========================================================
    private static Dictionary<string, AudioClip> BuildVoiceIndex(string characterName, string characterLower)
    {
        string folder = $"{AudiosRoot}/{characterLower}";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning($"[MRSpineAutoConnector] 오디오 폴더 없음: {folder}");
            return new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        }

        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });

        // 핵심: 대소문자 무시
        var dict = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;

            string key = TrimVoiceNameToKey(clip.name);

            if (!dict.ContainsKey(key))
                dict[key] = clip;
        }

        return dict;
    }

    private static string TrimVoiceNameToKey(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return clipName;

        if (clipName.StartsWith("Voice_", StringComparison.OrdinalIgnoreCase))
            clipName = clipName.Substring("Voice_".Length);

        int idx = clipName.IndexOf('_');
        if (idx >= 0 && idx + 1 < clipName.Length)
            clipName = clipName.Substring(idx + 1);

        return clipName;
    }

    // =========================================================
    // Controller base voice lists 연결
    // =========================================================
    private static void ConnectBaseVoiceLists(MRSpineCharacterController controller, Dictionary<string, AudioClip> voiceIndex)
    {
        var so = new SerializedObject(controller);
        so.Update();

        foreach (var kv in BaseVoiceKeywordMap)
        {
            string listFieldName = kv.Key;
            var keywords = kv.Value;

            var listProp = so.FindProperty(listFieldName);
            if (listProp == null || !listProp.isArray)
                continue;

            Undo.RecordObject(controller, $"AutoConnect {listFieldName}");
            listProp.ClearArray();

            foreach (var keyword in keywords)
            {
                if (voiceIndex.TryGetValue(keyword, out var clip) && clip != null)
                {
                    int newIndex = listProp.arraySize;
                    listProp.InsertArrayElementAtIndex(newIndex);
                    listProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = clip;
                }
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    // =========================================================
    // skins: SkeletonDataAsset + override voices + prune empty skins
    // =========================================================
    private static SkeletonDataAsset ConnectSkinsAndOverridesAndMaybePrune(
        MRSpineCharacterController controller,
        string characterName,
        Dictionary<string, AudioClip> voiceIndex)
    {
        var so = new SerializedObject(controller);
        so.Update();

        var skinsProp = so.FindProperty("skins");
        if (skinsProp == null || !skinsProp.isArray)
            return null;

        SkeletonDataAsset baseAsset = null;

        for (int i = 0; i < skinsProp.arraySize; i++)
        {
            var skinElem = skinsProp.GetArrayElementAtIndex(i);
            if (skinElem == null) continue;

            var idProp = skinElem.FindPropertyRelative("id");
            if (idProp == null) continue;

            string id = (idProp.stringValue ?? "").Trim();
            if (string.IsNullOrEmpty(id))
                continue;

            int skinNumber = ParseSkinNumber(id);

            var skeletonProp = skinElem.FindPropertyRelative("skeletonDataAsset");
            SkeletonDataAsset foundAsset = FindSkeletonDataAsset(characterName, skinNumber);

            if (skeletonProp != null)
            {
                Undo.RecordObject(controller, "Assign SkeletonDataAsset");
                skeletonProp.objectReferenceValue = foundAsset;
                if (skinNumber < 0)
                    baseAsset = foundAsset;
            }

            FillOverrideList(skinElem.FindPropertyRelative("ballEndVoicesOverride"), voiceIndex, BallEndKeywords, skinNumber, $"{characterName}({id}) ballEnd");
            FillOverrideList(skinElem.FindPropertyRelative("patEndVoicesOverride"), voiceIndex, PatEndKeywords, skinNumber, $"{characterName}({id}) patEnd");
            FillOverrideList(skinElem.FindPropertyRelative("smashEndVoicesOverride"), voiceIndex, SmashEndKeywords, skinNumber, $"{characterName}({id}) smashEnd");
        }

        if (PruneEmptySkins)
        {
            int baseCount = 0;
            for (int i = 0; i < skinsProp.arraySize; i++)
            {
                var idProp = skinsProp.GetArrayElementAtIndex(i).FindPropertyRelative("id");
                if (idProp != null && string.Equals((idProp.stringValue ?? "").Trim(), "Base", StringComparison.OrdinalIgnoreCase))
                    baseCount++;
            }

            for (int i = skinsProp.arraySize - 1; i >= 0; i--)
            {
                var skinElem = skinsProp.GetArrayElementAtIndex(i);
                var idProp = skinElem.FindPropertyRelative("id");
                string id = (idProp?.stringValue ?? "").Trim();
                int skinNumber = ParseSkinNumber(id);

                var skeletonProp = skinElem.FindPropertyRelative("skeletonDataAsset");
                bool skeletonMissing = (skeletonProp == null) || (skeletonProp.objectReferenceValue == null);

                var ballProp = skinElem.FindPropertyRelative("ballEndVoicesOverride");
                var patProp = skinElem.FindPropertyRelative("patEndVoicesOverride");
                var smashProp = skinElem.FindPropertyRelative("smashEndVoicesOverride");

                int ballCount = (ballProp != null && ballProp.isArray) ? ballProp.arraySize : 0;
                int patCount = (patProp != null && patProp.isArray) ? patProp.arraySize : 0;
                int smashCount = (smashProp != null && smashProp.isArray) ? smashProp.arraySize : 0;

                bool overridesAllEmpty = (ballCount == 0 && patCount == 0 && smashCount == 0);
                bool shouldDelete = skeletonMissing && overridesAllEmpty;

                bool shouldWarn = (!shouldDelete) && (skeletonMissing ^ overridesAllEmpty);

                if (shouldWarn)
                {
                    Debug.LogWarning(
                        $"[MRSpineAutoConnector] Skin '{id}' is partial for '{characterName}': " +
                        $"skeletonMissing={skeletonMissing}, overridesEmpty={overridesAllEmpty} (ball={ballCount}, pat={patCount}, smash={smashCount})");
                }

                if (shouldDelete)
                {
                    if (KeepAtLeastOneBaseSkin && skinNumber < 0 && baseCount <= 1)
                    {
                        Debug.LogWarning($"[MRSpineAutoConnector] Base skin is empty for '{characterName}', but kept (minimum 1).");
                        continue;
                    }

                    if (skinNumber < 0) baseCount--;

                    Debug.Log($"[MRSpineAutoConnector] Removing empty skin '{id}' from '{characterName}'.");
                    Undo.RecordObject(controller, "Prune Empty SkinVariant");
                    skinsProp.DeleteArrayElementAtIndex(i);
                }
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        return baseAsset;
    }

    private static void FillOverrideList(SerializedProperty listProp, Dictionary<string, AudioClip> voiceIndex, string[] keywords, int skinNumber, string context)
    {
        if (listProp == null || !listProp.isArray) return;

        listProp.ClearArray();

        for (int k = 0; k < keywords.Length; k++)
        {
            string baseKey = keywords[k];
            string key = (skinNumber < 0) ? baseKey : $"{baseKey}_Skin{skinNumber}";

            if (voiceIndex.TryGetValue(key, out var clip) && clip != null)
            {
                int newIndex = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(newIndex);
                listProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = clip;
            }
            else
            {
                Debug.LogWarning($"[MRSpineAutoConnector] Missing voice: '{key}' for {context}");
            }
        }
    }

    private static int ParseSkinNumber(string id)
    {
        if (string.Equals(id, "Base", StringComparison.OrdinalIgnoreCase))
            return -1;

        var digits = new string((id ?? "").Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out int n))
            return n;

        return -999;
    }

    private static SkeletonDataAsset FindSkeletonDataAsset(string characterName, int skinNumber)
    {
        string folder;
        string assetName;

        if (skinNumber < 0)
        {
            folder = $"{SpinesRoot}/{characterName}/{StandingFolder}";
            assetName = $"{characterName}_SkeletonData";
        }
        else
        {
            folder = $"{SpinesRoot}/{characterName}Skin{skinNumber}/{StandingFolder}";
            assetName = $"{characterName}Skin{skinNumber}_SkeletonData";
        }

        if (!AssetDatabase.IsValidFolder(folder))
            return null;

        var guids = AssetDatabase.FindAssets($"t:SkeletonDataAsset {assetName}", new[] { folder });
        if (guids == null || guids.Length == 0)
        {
            var fallback = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { folder });
            if (fallback != null && fallback.Length == 1)
                guids = fallback;
        }

        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
    }

    // =========================================================
    // Standing SkeletonAnimation 적용 + 리로드
    // =========================================================
    private static void ApplyStandingSkeletonDataAsset(MRSpineCharacterController controller, SkeletonDataAsset baseAsset)
    {
        if (baseAsset == null) return;

        var skeletonAnim = controller.GetComponentsInChildren<SkeletonAnimation>(true).FirstOrDefault();
        if (skeletonAnim == null)
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: 자식에서 SkeletonAnimation을 못 찾음");
            return;
        }

        Undo.RecordObject(skeletonAnim, "Set SkeletonDataAsset");
        skeletonAnim.skeletonDataAsset = baseAsset;
        EditorUtility.SetDirty(skeletonAnim);

        skeletonAnim.Initialize(true);
    }

    private static SkeletonAnimation GetPrimarySkeletonAnimation(MRSpineCharacterController controller)
    {
        if (controller == null) return null;
        if (controller.transform.childCount <= 0) return null;

        var spineGo = controller.transform.GetChild(0);
        if (spineGo == null) return null;

        return spineGo.GetComponent<SkeletonAnimation>();
    }

    private static void EnsurePrimarySkeletonInitialized(MRSpineCharacterController controller)
    {
        var sa = GetPrimarySkeletonAnimation(controller);
        if (sa == null)
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: Child(0)에서 SkeletonAnimation을 못 찾음");
            return;
        }

        if (sa.skeletonDataAsset == null)
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: SkeletonAnimation.skeletonDataAsset이 null");
            return;
        }

        try
        {
            sa.Initialize(true);
            EditorUtility.SetDirty(sa);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: SkeletonAnimation Initialize 실패: {e.Message}");
        }
    }

    private static void AutoFillIdleEatAnimationNameLists(MRSpineCharacterController controller)
    {
        if (controller == null) return;

        var sa = GetPrimarySkeletonAnimation(controller);
        if (sa == null)
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: Primary SkeletonAnimation(Child0) 없음 - 애니메이션 자동 연결 스킵");
            return;
        }

        if (sa.Skeleton == null)
        {
            if (sa.skeletonDataAsset != null) sa.Initialize(true);
        }

        var skeleton = sa.Skeleton;
        var animList = skeleton?.Data?.Animations;
        if (animList == null)
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: Skeleton.Data.Animations 접근 실패 - 애니메이션 자동 연결 스킵");
            return;
        }

        var idleNames = new List<string>();
        var eatNames = new List<string>();
        var seenIdle = new HashSet<string>(StringComparer.Ordinal);
        var seenEat = new HashSet<string>(StringComparer.Ordinal);

        static bool IsKeyword_Number(string name, string keyword)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(keyword))
                return false;

            if (!name.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                return false;

            int i = keyword.Length;
            if (i >= name.Length || name[i] != '_')
                return false;

            i++;
            if (i >= name.Length)
                return false;

            return char.IsDigit(name[i]);
        }

        static bool MatchesAnyKeyword_Number(string name, string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (IsKeyword_Number(name, keywords[i]))
                    return true;
            }
            return false;
        }

        foreach (var a in animList)
        {
            if (a == null) continue;
            string n = a.Name;
            if (string.IsNullOrEmpty(n)) continue;

            if (MatchesAnyKeyword_Number(n, IdleAnimKeywords))
            {
                if (seenIdle.Add(n)) idleNames.Add(n);
            }

            if (MatchesAnyKeyword_Number(n, EatAnimKeywords))
            {
                if (seenEat.Add(n)) eatNames.Add(n);
            }
        }

        var so = new SerializedObject(controller);
        so.Update();

        var idleProp = so.FindProperty("idleAnimations");
        if (idleProp != null && idleProp.isArray)
        {
            idleProp.ClearArray();
            for (int i = 0; i < idleNames.Count; i++)
            {
                int idx = idleProp.arraySize;
                idleProp.InsertArrayElementAtIndex(idx);
                idleProp.GetArrayElementAtIndex(idx).stringValue = idleNames[i];
            }
        }
        else
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: 'idleAnimations' 필드를 찾지 못함");
        }

        var eatProp = so.FindProperty("eatAnims");
        if (eatProp != null && eatProp.isArray)
        {
            eatProp.ClearArray();
            for (int i = 0; i < eatNames.Count; i++)
            {
                int idx = eatProp.arraySize;
                eatProp.InsertArrayElementAtIndex(idx);
                eatProp.GetArrayElementAtIndex(idx).stringValue = eatNames[i];
            }
        }
        else
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: 'eatAnims' 필드를 찾지 못함");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        if (idleNames.Count == 0)
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: Idle 애니메이션 매칭 결과 0개");
        if (eatNames.Count == 0)
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: Eat 애니메이션 매칭 결과 0개");
    }

    private static void AutoLinkBoneFollowersUnderController(MRSpineCharacterController controller)
    {
        var primary = GetPrimarySkeletonAnimation(controller);
        if (primary == null)
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: Primary SkeletonAnimation(Child0) 없음");
            return;
        }

        if (primary.Skeleton == null)
        {
            if (primary.skeletonDataAsset != null)
                primary.Initialize(true);
        }

        if (primary.Skeleton == null)
        {
            Debug.LogWarning($"[MRSpineAutoConnector] {controller.name}: Primary Skeleton이 null");
            return;
        }

        var followers = controller.GetComponentsInChildren<BoneFollower>(true);
        if (followers == null || followers.Length == 0)
        {
            Debug.Log($"[MRSpineAutoConnector] {controller.name}: BoneFollower 없음");
            return;
        }

        int candidates = 0;
        int linked = 0;

        foreach (var follower in followers)
        {
            if (follower == null) continue;

            var alias = follower.GetComponent<BoneFollowerAlias>();
            if (alias == null) continue;
            candidates++;

            bool ok = false;
            foreach (var boneName in alias.PossibleBoneNames)
            {
                if (string.IsNullOrEmpty(boneName)) continue;

                if (primary.Skeleton.FindBone(boneName) != null)
                {
                    Undo.RecordObject(follower, "Auto-Link BoneFollower");
                    follower.boneName = boneName;
                    follower.Initialize();
                    EditorUtility.SetDirty(follower);

                    ok = true;
                    linked++;
                    break;
                }
            }

            if (!ok)
            {
                Debug.LogWarning($"[MRSpineAutoConnector] {follower.name}: 후보 bone 매칭 실패 [{string.Join(", ", alias.PossibleBoneNames)}]");
            }
        }

        Debug.Log($"[MRSpineAutoConnector] BoneFollower auto-link: candidates={candidates}, linked={linked} (Primary='{primary.gameObject.name}')");
    }
}
#endif
