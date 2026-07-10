using UnityEditor;
using UnityEngine;

// EquipPlacementRecord 인스펙터: [기록 재현] — 소켓 생성 당시의 고스트 결과(소켓-로컬 TRS)를
// 그대로 월드에 복원해 눈으로 검증한다 (저장 안 됨).
// 주의: FitToPlaceholder를 다시 태우지 않는다 — 기록에는 rotationOffset·sizeRatio가 이미 구워져 있어
// 재적용하면 이중 반영된다. 또한 소켓 밑에 parenting 후 월드 스케일을 localScale에 넣으면
// lossy(대형 캐릭터 ~수만 배)가 한 번 더 곱해져 폭발하므로 반드시 unparented로 복원한다.
[CustomEditor(typeof(EquipPlacementRecord))]
public class EquipPlacementRecordEditor : Editor
{
    private GameObject previewInstance;

    private void OnDisable()
    {
        DestroyPreview();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("이 소켓을 만들 때 고스트가 있던 위치/회전/크기의 기록입니다.\n[기록 재현]으로 그 결과를 다시 띄워 확인할 수 있습니다 (저장 안 됨).", MessageType.Info);

        if (GUILayout.Button("기록 재현 (저장 안 됨)"))
        {
            RebuildPreview();
        }

        if (previewInstance != null)
        {
            if (GUILayout.Button("재현 지우기"))
            {
                DestroyPreview();
            }
        }
    }

    private void RebuildPreview()
    {
        DestroyPreview();

        EquipPlacementRecord record = (EquipPlacementRecord)target;
        Transform socketTr = record.transform;  // Record는 소켓 GO에 붙어 있음

        // 카탈로그에서 기록된 key의 프리팹 조회
        EquipCatalog catalog = null;
        string[] guids = AssetDatabase.FindAssets("t:EquipCatalog");
        if (guids.Length > 0)
        {
            catalog = AssetDatabase.LoadAssetAtPath<EquipCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        EquipEntry entry = null;
        if (catalog != null)
        {
            entry = catalog.Get(record.accessoryKey);
        }
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"[EquipRecord] '{record.accessoryKey}' 프리팹을 카탈로그에서 찾지 못했습니다.");
            return;
        }

        previewInstance = (GameObject)Instantiate(entry.prefab);
        previewInstance.name = "__EquipPreview__Record_" + record.accessoryKey;  // 레이캐스터 제외 규약
        previewInstance.hideFlags = HideFlags.DontSave;

        // 프리팹 스테이지/멀티 씬: 소켓과 같은 씬으로 이동해야 보인다
        if (socketTr.gameObject.scene.IsValid() && previewInstance.scene != socketTr.gameObject.scene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(previewInstance, socketTr.gameObject.scene);
        }

        // 복원 = 기록의 채널별 역연산 (unparented)
        previewInstance.transform.position = socketTr.TransformPoint(record.ghostLocalPosition);
        previewInstance.transform.rotation = socketTr.rotation * Quaternion.Euler(record.ghostLocalEuler);
        previewInstance.transform.localScale = Vector3.one * (record.ghostLocalScale * EquipMath.LossyAvg(socketTr));

        EditorGUIUtility.PingObject(previewInstance);
        Debug.Log($"[EquipRecord] 재현: '{record.accessoryKey}' pos={previewInstance.transform.position} scale={previewInstance.transform.localScale.x:G3} (저장 안 됨)");
    }

    private void DestroyPreview()
    {
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }
    }
}
