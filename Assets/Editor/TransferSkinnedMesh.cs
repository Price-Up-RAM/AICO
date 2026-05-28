using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BodyOnlySwapper : EditorWindow
{
    public SkinnedMeshRenderer sourceMesh; // 블렌더에서 가져온 새 바디
    public SkinnedMeshRenderer targetMesh; // 원래 캐릭터의 바디 (교체될 대상)

    [MenuItem("Tools/AICO/Body Only Swapper")]
    public static void ShowWindow() => GetWindow<BodyOnlySwapper>("Body Swapper");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("뼈대는 그대로 유지하고 Mesh와 Bone 연결 정보만 교체합니다.", MessageType.Info);
        sourceMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Source (Blender Mesh)", sourceMesh, typeof(SkinnedMeshRenderer), true);
        targetMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Target (Original Body)", targetMesh, typeof(SkinnedMeshRenderer), true);

        if (GUILayout.Button("Body 데이터만 이식") && sourceMesh != null && targetMesh != null)
        {
            SwapBodyData();
        }
    }

    void SwapBodyData()
    {
        Undo.RecordObject(targetMesh, "Swap Body Data");

        // 1. Target(현재 캐릭터)의 전체 뼈대 맵핑
        Transform root = targetMesh.transform.root;
        Transform[] targetBones = root.GetComponentsInChildren<Transform>(true);
        Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();
        foreach (var b in targetBones) if (!boneMap.ContainsKey(b.name)) boneMap.Add(b.name, b);

        // 2. 메쉬 및 머티리얼 데이터 덮어쓰기
        targetMesh.sharedMesh = sourceMesh.sharedMesh;
        targetMesh.sharedMaterials = sourceMesh.sharedMaterials;

        // 3. 본 배열(Bones)을 Target의 뼈대로 재연결
        // 중요: SkinnedMeshRenderer는 내부적으로 Transform 배열을 가집니다. 
        // 이를 Target 캐릭터 내부의 Transform들로 교체해줘야 애니메이션이 작동합니다.
        Transform[] sourceBones = sourceMesh.bones;
        Transform[] newBones = new Transform[sourceBones.Length];

        for (int i = 0; i < sourceBones.Length; i++)
        {
            if (boneMap.TryGetValue(sourceBones[i].name, out Transform matchingBone))
                newBones[i] = matchingBone;
            else
                Debug.LogWarning($"[본 매칭 실패] {sourceBones[i].name}을(를) 찾을 수 없습니다.");
        }

        targetMesh.bones = newBones;

        // 4. 루트 본 설정
        if (sourceMesh.rootBone != null && boneMap.TryGetValue(sourceMesh.rootBone.name, out Transform newRoot))
            targetMesh.rootBone = newRoot;

        EditorUtility.SetDirty(targetMesh);
        Debug.Log($"<color=green>{targetMesh.name}의 바디 데이터 교체 완료!</color>");
    }
}