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

    // slotId 리네임 동기화: GO명(Socket_ 규칙) + 카탈로그의 옛 slotId 참조를 새 이름으로 —
    // "리네임하면 카탈로그도 손으로 고쳐야 하는" 끊김 사고 방지
    private void SyncSlotIdRename(EquipSocket socket, string oldSlotId, string newSlotId)
    {
        if (string.IsNullOrEmpty(newSlotId))
        {
            return;
        }

        // GO명: 기존 규칙(Socket_...)을 따르던 경우에만 자동 추종 (손으로 지은 이름은 존중)
        if (socket.gameObject.name.StartsWith("Socket_"))
        {
            Undo.RecordObject(socket.gameObject, "Rename Socket GO");
            string suffix = newSlotId;
            if (suffix.StartsWith("socket_"))
            {
                suffix = suffix.Substring("socket_".Length);
            }
            socket.gameObject.name = "Socket_" + suffix;
        }

        // 카탈로그: 옛 slotId를 가리키던 엔트리 전부 새 이름으로
        if (catalog != null && string.IsNullOrEmpty(oldSlotId) == false)
        {
            int moved = 0;
            foreach (EquipEntry entry in catalog.Entries)
            {
                if (entry != null && entry.targetSlotId == oldSlotId)
                {
                    Undo.RecordObject(catalog, "Relink Catalog Entry (Rename)");
                    entry.targetSlotId = newSlotId;
                    moved = moved + 1;
                }
            }
            if (moved > 0)
            {
                EditorUtility.SetDirty(catalog);
                Debug.Log($"[EquipSocket] slotId 리네임 동기화: '{oldSlotId}' → '{newSlotId}' (GO명 + 카탈로그 {moved}개 엔트리)");
            }
        }
    }

    // 이 소켓이 신모델(refDist 베이크 placeholder 보유)인지
    private static bool HasRefDistPlaceholder(EquipSocket socket)
    {
        EquipPlaceholder[] placeholders = socket.GetComponentsInChildren<EquipPlaceholder>(true);
        foreach (EquipPlaceholder ph in placeholders)
        {
            if (ph != null && ph.bakedRefDistLocal > 1e-12f)
            {
                return true;
            }
        }
        return false;
    }

    public override void OnInspectorGUI()
    {
        EquipSocket socket = (EquipSocket)target;

        // 기본 필드 (slotId/fit/pivot/placeholderAnchor) — slotId 변경 감지해 GO명/카탈로그 동기화
        string prevSlotId = socket.slotId;
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck() && socket.slotId != prevSlotId)
        {
            SyncSlotIdRename(socket, prevSlotId, socket.slotId);
        }
        bool newModel = socket.GetComponent<Collider>() == null && HasRefDistPlaceholder(socket);

        // 미리네임 경고: socket_N은 임시 이름 — slotId가 카탈로그/전파의 열쇠
        if (string.IsNullOrEmpty(socket.slotId) == false && socket.slotId.StartsWith("socket_"))
        {
            EditorGUILayout.HelpBox($"아직 임시 이름입니다 ('{socket.slotId}'). slotId는 카탈로그·전파가 이 자리를 찾는 열쇠 — 의미 있는 이름(head, ribbon 등)으로 바꾸세요.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // 콜라이더 안내: 신모델(refDist)이면 캡슐 불필요, 아니면 추가 버튼
        if (socket.GetComponent<Collider>() == null)
        {
            if (newModel)
            {
                EditorGUILayout.HelpBox("신모델 소켓 (refDist 기반) — 캡슐 불필요. 미리보기/조정은 부착점(placeholder) 인스펙터에서, 장착 테스트는 Socket Maker 현황판의 [테스트] 버튼으로.", MessageType.Info);
                EquipPlaceholder firstPh = socket.FindPlaceholder("placeholder");
                if (firstPh != null)
                {
                    if (GUILayout.Button("부착점 선택 (미리보기/조정)"))
                    {
                        Selection.activeGameObject = firstPh.gameObject;
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("콜라이더도 부착점도 없습니다 — Socket Maker에서 고스트 클릭 배치로 소켓을 만드는 것을 권장합니다. (레거시 캡슐 방식을 쓰려면 아래 버튼)", MessageType.Warning);
                if (GUILayout.Button("CapsuleCollider 추가 (Trigger)"))
                {
                    CapsuleCollider cap = Undo.AddComponent<CapsuleCollider>(socket.gameObject);
                    cap.isTrigger = true;
                }
            }
        }

        // 라이브 미리보기 = 캡슐 볼륨-핏(레거시 직부착) 시각화 도구 — 캡슐 있는 소켓에서만 의미 있음.
        // 신모델 소켓은 Fit이 즉시 거부(인스턴스 파괴)하므로 UI 자체를 숨긴다.
        if (socket.GetComponent<Collider>() != null)
        {
            EditorGUILayout.LabelField("라이브 미리보기 (캡슐 볼륨-핏, 저장 안 됨)", EditorStyles.boldLabel);

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
        }

        // ── Placeholder 관리 (소켓=부위, placeholder=부착점) ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placeholders (부착점)", EditorStyles.boldLabel);

        bool hasCapsule = socket.GetComponent<Collider>() != null;
        EquipPlaceholder[] placeholders = socket.GetComponentsInChildren<EquipPlaceholder>(true);
        foreach (EquipPlaceholder ph in placeholders)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("· " + ph.placeholderId, GUILayout.Width(100));
            if (hasCapsule)
            {
                EditorGUILayout.LabelField($"axisT {ph.axisT:F2}  rs {ph.radiusScale:F2}", GUILayout.Width(140));
            }
            else
            {
                EditorGUILayout.LabelField($"refDist {ph.bakedRefDistLocal:F4}", GUILayout.Width(140));
            }
            if (GUILayout.Button("선택", GUILayout.Width(40)))
            {
                Selection.activeGameObject = ph.gameObject;
            }
            EditorGUILayout.EndHorizontal();
        }

        // 캡슐 좌표 기반 버튼들 — 캡슐 소켓 전용 (신모델은 배치가 클릭/글라이드라 버튼 자체가 무의미 → 숨김)
        if (hasCapsule)
        {
            EditorGUILayout.BeginHorizontal();
            if (socket.slotId == "head")
            {
                if (GUILayout.Button("표준 시드 (top/side_l/side_r/halo)"))
                {
                    CreatePlaceholder(socket, "top", 1f, AxisDir(socket), 1f, EquipPlaceholderOrientation.SurfaceAligned, EquipContactAnchor.BottomAlign);
                    CreatePlaceholder(socket, "side_l", 0.3f, new Vector3(-1f, 0f, 0f), 1f, EquipPlaceholderOrientation.SurfaceAligned, EquipContactAnchor.BottomAlign);
                    CreatePlaceholder(socket, "side_r", 0.3f, new Vector3(1f, 0f, 0f), 1f, EquipPlaceholderOrientation.SurfaceAligned, EquipContactAnchor.BottomAlign);
                    CreatePlaceholder(socket, "halo", 1f, AxisDir(socket), 1.6f, EquipPlaceholderOrientation.SocketFrame, EquipContactAnchor.Center);
                }
            }
            if (GUILayout.Button("Placeholder 추가"))
            {
                CreatePlaceholder(socket, "new", 1f, AxisDir(socket), 1f, EquipPlaceholderOrientation.SurfaceAligned, EquipContactAnchor.BottomAlign);
            }
            if (GUILayout.Button("재배치 (캡슐 변경 반영)"))
            {
                foreach (EquipPlaceholder ph in placeholders)
                {
                    Undo.RecordObject(ph.transform, "Reapply Placeholder");
                    ph.ApplyToTransform();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // 인스펙터에서 값이 바뀌면 미리보기 재핏
        if (GUI.changed && livePreview)
        {
            RebuildPreview();
        }
    }

    // 캡슐 축 방향 (로컬)
    private static Vector3 AxisDir(EquipSocket socket)
    {
        CapsuleCollider cap = socket.SizingVolume as CapsuleCollider;
        if (cap != null)
        {
            return EquipCapsuleMath.AxisVector(cap);
        }
        return Vector3.up;
    }

    // placeholder 생성 (같은 id가 있으면 스킵)
    private static void CreatePlaceholder(EquipSocket socket, string id, float axisT, Vector3 dirLocal, float radiusScale, EquipPlaceholderOrientation orientation, EquipContactAnchor anchor)
    {
        if (socket.FindPlaceholder(id) != null)
        {
            Debug.Log($"[EquipSocket] placeholder '{id}' 이미 존재 — 보존.");
            return;
        }

        GameObject go = new GameObject("PH_" + id);
        Undo.RegisterCreatedObjectUndo(go, "Create Placeholder");
        go.transform.SetParent(socket.transform, false);

        EquipPlaceholder ph = go.AddComponent<EquipPlaceholder>();
        ph.placeholderId = id;
        ph.axisT = axisT;
        ph.dirLocal = dirLocal.normalized;
        ph.radiusScale = radiusScale;
        ph.orientation = orientation;
        ph.contactAnchor = anchor;
        ph.ApplyToTransform();

        EditorUtility.SetDirty(go);
    }

    // 씬에서 캡슐/Transform을 드래그하는 동안 미리보기를 계속 재핏
    private void OnSceneGUI()
    {
        if (livePreview == false || previewInstance == null)
        {
            return;
        }

        EquipSocket socket = (EquipSocket)target;
        if (socket.SizingVolume == null)
        {
            return;
        }

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

        // 방어: 캡슐 없는 소켓은 Fit이 인스턴스를 즉시 파괴 — UI를 숨겨도 남은 상태(체크 켠 채 소켓 전환)에서 스팸 방지
        EquipSocket owner = (EquipSocket)target;
        if (owner.SizingVolume == null)
        {
            return;
        }

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
