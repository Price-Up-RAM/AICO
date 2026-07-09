using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// EquipPlaceholder 커스텀 인스펙터: 표면 스냅 드래그("중력" 스냅) + 라이브 미리보기.
// 이동 툴로 드래그하면 무차원 좌표를 캡처하고, 스냅이 켜져 있으면 캡슐 표면(radiusScale=1)을 미끄러진다.
[CustomEditor(typeof(EquipPlaceholder))]
public class EquipPlaceholderEditor : Editor
{
    private static bool snapToSurface = true;   // 표면 스냅 토글 (세션 공유)

    private EquipCatalog catalog;               // 미리보기용 카탈로그
    private string[] keyList;                   // 카탈로그 키 목록
    private int keyIndex;                       // 선택 키
    private bool livePreview;                   // 미리보기 on/off
    private GameObject previewInstance;         // 미리보기 인스턴스
    private Vector3 lastLocalPos;               // 드래그 감지용

    private void OnEnable()
    {
        LoadCatalog();

        EquipPlaceholder ph = (EquipPlaceholder)target;
        lastLocalPos = ph.transform.localPosition;
    }

    private void OnDisable()
    {
        DestroyPreview();
    }

    public override void OnInspectorGUI()
    {
        EquipPlaceholder ph = (EquipPlaceholder)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        snapToSurface = EditorGUILayout.ToggleLeft("표면 스냅 (드래그 시 캡슐 테두리를 미끄러짐)", snapToSurface);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("표면에 스냅 (radiusScale=1)"))
        {
            Undo.RecordObject(ph.transform, "Snap Placeholder");
            Undo.RecordObject(ph, "Snap Placeholder");
            ph.CaptureFromTransform();
            ph.radiusScale = 1f;
            ph.ApplyToTransform();
            EditorUtility.SetDirty(ph);
        }
        if (GUILayout.Button("좌표→Transform 재적용"))
        {
            Undo.RecordObject(ph.transform, "Apply Placeholder");
            ph.ApplyToTransform();
        }
        EditorGUILayout.EndHorizontal();

        // 값 직접 편집 시 Transform 동기화
        if (GUI.changed)
        {
            ph.ApplyToTransform();
        }

        // ── 라이브 미리보기 ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("라이브 미리보기 (저장 안 됨)", EditorStyles.boldLabel);

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
    }

    // 씬 드래그 처리: 이동 감지 → 좌표 캡처(+스냅) → 미리보기 재핏
    private void OnSceneGUI()
    {
        EquipPlaceholder ph = (EquipPlaceholder)target;

        if (ph.transform.localPosition != lastLocalPos)
        {
            Undo.RecordObject(ph, "Move Placeholder");
            ph.CaptureFromTransform();

            if (snapToSurface)
            {
                ph.radiusScale = 1f;
                ph.ApplyToTransform();
            }

            lastLocalPos = ph.transform.localPosition;
            EditorUtility.SetDirty(ph);

            RefitPreview(ph);
        }

        // 시각화: 소켓 캡슐 표면 접원 + 축 최근접점→placeholder 선
        EquipSocket socket = ph.OwnerSocket;
        if (socket != null)
        {
            CapsuleCollider cap = socket.SizingVolume as CapsuleCollider;
            if (cap != null)
            {
                Handles.color = new Color(0.3f, 0.9f, 1f, 0.8f);
                Vector3 axisWorld = socket.transform.TransformDirection(EquipCapsuleMath.AxisVector(cap));
                float half = EquipCapsuleMath.HalfSegmentLength(cap);
                float axisT = ph.axisT;
                Vector3 closestLocal = cap.center + EquipCapsuleMath.AxisVector(cap) * (axisT * half);
                Vector3 closestWorld = socket.transform.TransformPoint(closestLocal);

                Handles.DrawDottedLine(closestWorld, ph.transform.position, 4f);
                float rWorld = cap.radius * EquipCapsuleMath.LossyAvg(socket.transform) * ph.radiusScale;
                Handles.DrawWireDisc(closestWorld, axisWorld, rWorld);
            }
        }
    }

    // 카탈로그 로드/키 목록
    private void LoadCatalog()
    {
        if (catalog == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:EquipCatalog");
            if (guids.Length > 0)
            {
                catalog = AssetDatabase.LoadAssetAtPath<EquipCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
        BuildKeyList();
    }

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

    // 미리보기 생성/재핏 (실제 장착과 동일한 FitToPlaceholder 사용 = WYSIWYG)
    private void RebuildPreview()
    {
        DestroyPreview();

        EquipEntry entry = GetSelectedEntry();
        EquipPlaceholder ph = (EquipPlaceholder)target;
        EquipSocket socket = ph.OwnerSocket;
        if (entry == null || entry.prefab == null || socket == null)
        {
            return;
        }

        previewInstance = (GameObject)Instantiate(entry.prefab);
        previewInstance.name = "__EquipPreview__";
        previewInstance.hideFlags = HideFlags.HideAndDontSave;

        EquipPlacement.FitToPlaceholder(previewInstance, socket, ph, entry);
    }

    private void RefitPreview(EquipPlaceholder ph)
    {
        if (livePreview == false || previewInstance == null)
        {
            return;
        }

        EquipEntry entry = GetSelectedEntry();
        EquipSocket socket = ph.OwnerSocket;
        if (entry == null || socket == null)
        {
            return;
        }
        EquipPlacement.FitToPlaceholder(previewInstance, socket, ph, entry);
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
