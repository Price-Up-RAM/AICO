// 이 스크립트는 반드시 "Editor" 폴더 안에 있어야 합니다.
using UnityEngine;
using UnityEditor;
using Spine.Unity;

public class BoneFollowerAutoLinker
{
    [MenuItem("Tools/Spine/Auto-Link BoneFollower Using Alias")]
    private static void AutoLinkBoneFollowers()
    {
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("자동으로 연결할 게임오브젝트를 선택해주세요.");
            return;
        }

        int linkedCount = 0;
        foreach (GameObject obj in Selection.gameObjects)
        {
            // BoneFollower와 BoneFollowerAlias 컴포넌트를 함께 찾습니다.
            BoneFollower follower = obj.GetComponent<BoneFollower>();
            BoneFollowerAlias alias = obj.GetComponent<BoneFollowerAlias>();

            if (follower == null || alias == null)
            {
                // 둘 중 하나라도 없으면 건너뜁니다.
                continue;
            }
            
            // 이름 목록이 비어있으면 경고를 표시합니다.
            if (alias.PossibleBoneNames.Count == 0)
            {
                Debug.LogWarning($"{obj.name}의 BoneFollowerAlias에 설정된 이름이 없습니다.");
                continue;
            }

            SkeletonRenderer skeletonAnimation = follower.skeletonRenderer;
            if (skeletonAnimation == null)
            {
                Debug.LogWarning($"{obj.name}에서 SkeletonAnimation을 찾을 수 없습니다.");
                continue;
            }

            if (skeletonAnimation.Skeleton == null)
            {
                skeletonAnimation.Initialize(false);
            }

            bool isLinked = false;
            // BoneFollowerAlias에 입력된 이름 목록을 순회합니다.
            foreach (string boneName in alias.PossibleBoneNames)
            {
                if (string.IsNullOrEmpty(boneName)) continue;

                // 스켈레톤에 해당 Bone이 있는지 확인하고 연결을 시도합니다.
                if (skeletonAnimation.Skeleton.FindBone(boneName) != null)
                {
                    Undo.RecordObject(follower, "Auto-Link BoneFollower");
                    follower.boneName = boneName;
                    follower.Initialize();
                    EditorUtility.SetDirty(follower);

                    Debug.Log($"{obj.name} -> '{boneName}' Bone에 성공적으로 연결했습니다.");
                    isLinked = true;
                    linkedCount++;
                    break; // 하나를 찾았으면 더 이상 찾지 않고 다음 게임오브젝트로 넘어감
                }
            }

            if (!isLinked)
            {
                Debug.LogWarning($"{obj.name}에서 후보 목록에 있는 Bone을 하나도 찾지 못했습니다: [{string.Join(", ", alias.PossibleBoneNames)}]");
            }
        }
        
        Debug.Log($"총 {linkedCount}개의 BoneFollower를 자동으로 연결했습니다.");
    }
}