using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// EquipSocket 커스텀 인스펙터: 카탈로그 키를 골라 실제 악세서리를 소켓에 "라이브 미리보기"로 띄운다.
// 프리팹 모드에서 소켓을 선택하고 CapsuleCollider 핸들/Transform을 드래그하면 악세서리가 즉시 재핏되어 보인다.
// 미리보기 인스턴스는 HideAndDontSave라 저장되지 않는다(콜라이더처럼 눈으로 맞추기 위한 편의).
[CustomEditor(typeof(EquipSocket))]
public class EquipSocketEditor : Editor
{
    private EquipCatalog catalog;   // 미리보기용 카탈로그
    private string[] keyList;       // 카탈로그 키 목록
    private int keyIndex;           // 선택된 키 인덱스
    private bool livePreview;       // 라이브 미리보기 on/off
    private GameObject previewInstance;  // 미리보기 인스턴스

    private void OnEnable()
    {
        LoadCatalog();
    }

    private void OnDisable()
    {
        DestroyPreview();
    }

    // 프로젝트에서 EquipCatalog 에셋 자동 로드 + 키 목록 구성
    private void LoadCatalog()
    {
        if (catalog == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:EquipCatalog");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                catalog = AssetDatabase.LoadAssetAtPath<EquipCatalog>(path);
            }
        }

        BuildKeyList();
    }

    // 카탈로그에서 키 배열 구성
    private void BuildKeyList()
    {
        List<string> keys = new List<string>();
        if (catalog != null)
        {
            foreach (EquipEntry entry in catalog.Entries)
            {
                if (entry != null && string.IsNullOrEmpty(entry.key) == false)
                {
                    keys.Add(entry.key);
                }
            }
        }
        keyList = keys.ToArray();
    }

    public override void OnInspectorGUI()
    {
        // 기본 필드 (slotId/fit/pivot/placeholderAnchor)
        DrawDefaultInspector();

        EquipSocket socket = (EquipSocket)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("라이브 미리보기 (저장 안 됨)", EditorStyles.boldLabel);

        // 콜라이더 없으면 추가 버튼 안내
        if (socket.GetComponent<Collider>() == null)
        {
            EditorGUILayout.HelpBox("사이징 볼륨용 콜라이더가 없습니다. CapsuleCollider를 추가하세요.", MessageType.Warning);
            if (GUILayout.Button("CapsuleCollider 추가 (Trigger)"))
            {
                CapsuleCollider cap = Undo.AddComponent<CapsuleCollider>(socket.gameObject);
                cap.isTrigger = true;
            }
        }

        // 카탈로그 + 키 선택
        EquipCatalog newCatalog = (EquipCatalog)EditorGUILayout.ObjectField("Catalog", catalog, typeof(EquipCatalog), false);
        if (newCatalog != catalog)
        {
            catalog = newCatalog;
            BuildKeyList();
        }

        if (keyList != null && keyList.Length > 0)
        {
            keyIndex = EditorGUILayout.Popup("Accessory Key", keyIndex, keyList);
        }
        else
        {
            EditorGUILayout.HelpBox("카탈로그가 없거나 키가 비어 있습니다.", MessageType.Info);
        }

        // 미리보기 토글
        bool newLive = EditorGUILayout.Toggle("라이브 미리보기", livePreview);
        if (newLive != livePreview)
        {
            livePreview = newLive;
            if (livePreview)
            {
                RebuildPreview();
            }
            else
            {
                DestroyPreview();
            }
        }

        if (GUILayout.Button("미리보기 갱신"))
        {
            RebuildPreview();
        }

        // 인스펙터에서 값이 바뀌면 미리보기 재핏
        if (GUI.changed && livePreview)
        {
            RebuildPreview();
        }
    }

    // 씬에서 캡슐/Transform을 드래그하는 동안 미리보기를 계속 재핏
    private void OnSceneGUI()
    {
        if (livePreview == false || previewInstance == null)
        {
            return;
        }

        EquipSocket socket = (EquipSocket)target;
        EquipEntry entry = GetSelectedEntry();
        if (entry == null)
        {
            return;
        }

        EquipPlacement.Fit(previewInstance, socket, entry.fitBias, entry.positionOffset, entry.rotationOffset);
    }

    // 현재 선택된 키의 엔트리
    private EquipEntry GetSelectedEntry()
    {
        if (catalog == null || keyList == null || keyList.Length == 0)
        {
            return null;
        }
        if (keyIndex < 0 || keyIndex >= keyList.Length)
        {
            return null;
        }
        return catalog.Get(keyList[keyIndex]);
    }

    // 미리보기 재생성 (키/소켓 기준)
    private void RebuildPreview()
    {
        DestroyPreview();

        EquipEntry entry = GetSelectedEntry();
        if (entry == null || entry.prefab == null)
        {
            return;
        }

        EquipSocket socket = (EquipSocket)target;

        previewInstance = (GameObject)Instantiate(entry.prefab);
        previewInstance.name = "__EquipPreview__";
        previewInstance.hideFlags = HideFlags.HideAndDontSave;

        EquipPlacement.Fit(previewInstance, socket, entry.fitBias, entry.positionOffset, entry.rotationOffset);
    }

    // 미리보기 제거
    private void DestroyPreview()
    {
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }
    }
}
