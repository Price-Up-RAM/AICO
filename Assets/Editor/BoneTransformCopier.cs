using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BoneTransformCopier : EditorWindow
{
    public GameObject sourceRoot;
    public GameObject targetRoot;

    [MenuItem("Tools/Bone Transform Copier")]
    public static void ShowWindow()
    {
        GetWindow<BoneTransformCopier>("Bone Copier");
    }

    private void OnGUI()
    {
        sourceRoot = (GameObject)EditorGUILayout.ObjectField("Source (Clone)", sourceRoot, typeof(GameObject), true);
        targetRoot = (GameObject)EditorGUILayout.ObjectField("Target (Original)", targetRoot, typeof(GameObject), true);

        EditorGUILayout.Space();

        if (GUILayout.Button("Copy Transforms (상세 로그 포함)"))
        {
            if (sourceRoot != null && targetRoot != null)
                CopyTransforms();
            else
                Debug.LogError("Source와 Target을 모두 지정해주세요!");
        }
    }

    private void CopyTransforms()
    {
        Undo.RegisterFullObjectHierarchyUndo(targetRoot, "Copy Bone Transforms");

        Transform[] sourceTransforms = sourceRoot.GetComponentsInChildren<Transform>(true);
        Transform[] targetTransforms = targetRoot.GetComponentsInChildren<Transform>(true);
        
        // Target 본들을 이름(공백 제거)으로 딕셔너리에 저장
        Dictionary<string, Transform> targetDict = new Dictionary<string, Transform>();
        foreach (var t in targetTransforms)
        {
            string cleanName = t.name.Trim();
            if (!targetDict.ContainsKey(cleanName))
                targetDict.Add(cleanName, t);
        }

        int successCount = 0;
        int failCount = 0;

        Debug.Log($"<color=cyan>=== 본 복사 시작: {sourceRoot.name} -> {targetRoot.name} ===</color>");

        foreach (var sourceT in sourceTransforms)
        {
            string sourceName = sourceT.name.Trim();
            
            // 루트 오브젝트 자체는 건너뛰고 싶다면 아래 주석 해제 (보통은 포함하는 게 맞습니다)
            // if (sourceT == sourceRoot.transform) continue;

            if (targetDict.TryGetValue(sourceName, out Transform targetT))
            {
                // 좌표 복사
                targetT.localPosition = sourceT.localPosition;
                targetT.localRotation = sourceT.localRotation;
                targetT.localScale = sourceT.localScale;
                
                // 상세 로그 (너무 많으면 콘솔이 지저분해질 수 있음)
                // Debug.Log($"[성공] {sourceName} 복사 완료");
                successCount++;
            }
            else
            {
                Debug.LogWarning($"[실패] Target에서 '{sourceName}'을(를) 찾을 수 없습니다.");
                failCount++;
            }
        }

        Debug.Log($"<color=yellow>=== 작업 완료: 성공 {successCount}개 / 실패 {failCount}개 ===</color>");
        
        // 씬 뷰 갱신
        EditorUtility.SetDirty(targetRoot);
    }
}