using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 스냅 모드: 메시(기본 — 실제 표면 글라이드) / 자유(띄우기용 미세조정)
public enum EquipSnapMode
{
    Mesh,
    Free,
}

// EquipPlaceholder 커스텀 인스펙터: 메시 표면 글라이드 + 라이브 미리보기 + Size Ratio 직편집.
// 메시 모드: 씬의 구체 핸들을 드래그하면 마우스 레이를 캐릭터 메시에 캐스트해 히트점을 미끄러진다(up=노멀).
// 드래그를 놓는 순간 크기 기준(refDist)이 재베이크된다. Free 이동은 재베이크하지 않는다.
[CustomEditor(typeof(EquipPlaceholder))]
public class EquipPlaceholderEditor : Editor
{
    private static EquipSnapMode snapMode = EquipSnapMode.Mesh;  // 세션 공유, 기본 메시
    private static int hitIndex;                                  // 앞뒤 표면 사이클 인덱스

    private EquipCatalog catalog;        // 미리보기용 카탈로그
    private string[] keyList;            // 카탈로그 키 목록
    private int keyIndex;                // 선택 키
    private bool livePreview;            // 미리보기 on/off
    private GameObject previewInstance;  // 미리보기 인스턴스

    private Vector3 lastLocalPos;        // 이동 감지용
    private bool dragging;               // 드래그 세션 중
    private int undoGroup;               // 드래그 시작 시점 Undo 그룹
    private Vector3 preDragLocalPos;     // Esc 복원용
    private Quaternion preDragLocalRot;
    private bool surfaceMissed;          // 실루엣 이탈 피드백
    private int lastHitCount;            // 배지 표시용

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

        EditorGUILayout.HelpBox("부착점: 위치·회전 = 이 Transform 그대로, 크기 = Baked Ref Dist × 2 × Size Ratio(카탈로그).\n메시 모드에서 구체 핸들 드래그 = 표면 글라이드 — 놓는 순간 크기 기준(refDist)이 재측정됩니다. Free 이동은 재측정하지 않습니다.", MessageType.Info);

        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("placeholderId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bakedRefDistLocal"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("contactAnchor"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        // 스냅 모드: Mesh/Free 2지선다
        Transform charRoot = EquipAuthoringUtil.ResolveCharRoot(ph.transform);
        string[] options = new string[] { "Mesh (표면 글라이드)", "Free (자유 이동 — 띄우기용)" };
        int idx = 0;
        if (snapMode == EquipSnapMode.Free)
        {
            idx = 1;
        }
        int newIdx = EditorGUILayout.Popup("스냅 모드", idx, options);
        if (newIdx == 1)
        {
            snapMode = EquipSnapMode.Free;
        }
        else
        {
            snapMode = EquipSnapMode.Mesh;
        }

        if (snapMode == EquipSnapMode.Mesh)
        {
            if (charRoot == null || EquipMeshRaycaster.Instance.HasCache(charRoot) == false)
            {
                EditorGUILayout.HelpBox("메시를 찾지 못해 자유 이동으로 동작합니다 — [메시 캐시 갱신]을 시도하세요.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("씬의 구체 핸들을 드래그 — 실제 메시 표면을 미끄러집니다 (up=노멀).\n겹친 표면: 드래그 중 Alt+휠로 앞뒤 전환. Esc=취소.", MessageType.Info);
            }
        }

        if (GUILayout.Button("메시 캐시 갱신"))
        {
            EquipMeshRaycaster.Instance.Invalidate();
        }

        // 고스트 재조정: 글라이드(회전 리셋됨)와 달리 고스트 픽으로 위치·회전·refDist를 한 번에 다시 잡는다
        using (new EditorGUI.DisabledScope(Application.isPlaying || ph.OwnerSocket == null))
        {
            if (GUILayout.Button("Socket Maker에서 고스트 재조정"))
            {
                EquipSocketMakerWindow.BeginRepick(ph);
            }
        }
        if (ph.OwnerSocket == null)
        {
            EditorGUILayout.HelpBox("부모에 EquipSocket이 없어 재조정할 수 없습니다.", MessageType.Warning);
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

        // 크기 조절: 카탈로그 sizeRatio 직접 편집 (미리보기 즉시 반영 = 실제 장착과 동일. 아이템 공용 값)
        EquipEntry sizeEntry = GetSelectedEntry();
        if (sizeEntry != null)
        {
            EditorGUI.BeginChangeCheck();
            float newRatio = EditorGUILayout.FloatField("Size Ratio (카탈로그 저장)", sizeEntry.sizeRatio);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(catalog, "Edit Size Ratio");
                sizeEntry.sizeRatio = Mathf.Max(0.01f, newRatio);
                EditorUtility.SetDirty(catalog);
                RefitPreview((EquipPlaceholder)target);
            }
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

    // 씬 상호작용: 모드별 드래그 처리 + 시각화
    private void OnSceneGUI()
    {
        EquipPlaceholder ph = (EquipPlaceholder)target;
        Event e = Event.current;

        Transform charRoot = EquipAuthoringUtil.ResolveCharRoot(ph.transform);
        bool meshMode = snapMode == EquipSnapMode.Mesh && charRoot != null && EquipMeshRaycaster.Instance.HasCache(charRoot);

        // Esc = 드래그 취소 (원위치 복원)
        if (dragging && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            ph.transform.localPosition = preDragLocalPos;
            ph.transform.localRotation = preDragLocalRot;
            lastLocalPos = ph.transform.localPosition;
            GUIUtility.hotControl = 0;
            dragging = false;
            surfaceMissed = false;
            e.Use();
            RefitPreview(ph);
            return;
        }

        if (meshMode)
        {
            HandleMeshGlide(ph, charRoot, e);
        }
        else
        {
            HandleFreeMove(ph);
        }

        DrawVisuals(ph);
    }

    // 메시 글라이드: 구체 핸들/이동툴 이동 → 커서 레이를 메시에 캐스트 → 히트점으로 이동 (up=노멀)
    private void HandleMeshGlide(EquipPlaceholder ph, Transform charRoot, Event e)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);

        EditorGUI.BeginChangeCheck();
        Handles.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Handles.FreeMoveHandle(id, ph.transform.position,
            HandleUtility.GetHandleSize(ph.transform.position) * 0.1f, Vector3.zero, Handles.SphereHandleCap);
        bool handleMoved = EditorGUI.EndChangeCheck();

        // 이동 툴(W) 병행: 위치를 그대로 쓰지 않고 같은 커서 레이로 재투영
        bool toolMoved = ph.transform.localPosition != lastLocalPos;

        // 앞뒤 표면 사이클 (드래그 중 Alt+휠)
        bool cycled = false;
        if (dragging && e.type == EventType.ScrollWheel && e.alt)
        {
            if (e.delta.y > 0f)
            {
                hitIndex = hitIndex + 1;
            }
            else
            {
                hitIndex = hitIndex - 1;
            }
            if (hitIndex < 0)
            {
                hitIndex = 0;
            }
            cycled = true;
            e.Use();
        }

        if (handleMoved || toolMoved || cycled)
        {
            // 드래그 세션 시작
            if (dragging == false)
            {
                dragging = true;
                undoGroup = Undo.GetCurrentGroup();
                preDragLocalPos = ph.transform.localPosition;
                preDragLocalRot = ph.transform.localRotation;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            EquipMeshHit hit;
            int hitCount;

            if (EquipMeshRaycaster.Instance.RaycastCursor(charRoot, ray, hitIndex, out hit, out hitCount))
            {
                lastHitCount = hitCount;
                if (hitIndex >= hitCount)
                {
                    hitIndex = hitCount - 1;
                }

                Undo.RecordObject(ph.transform, "Glide Placeholder");
                ph.transform.position = hit.point;
                ph.transform.rotation = Quaternion.LookRotation(TangentOf(hit.normal), hit.normal);
                lastLocalPos = ph.transform.localPosition;
                surfaceMissed = false;

                RefitPreview(ph);
            }
            else
            {
                // 실루엣 이탈: 직전 유효 위치 유지 (순간이동 금지)
                ph.transform.localPosition = lastLocalPos;
                surfaceMissed = true;
            }
        }

        // 드래그 종료(마우스 놓음) = 확정: 크기 기준 refDist를 새 위치로 재베이크 + Undo 1회로 접기
        if (dragging && GUIUtility.hotControl == 0 && e.type != EventType.Used)
        {
            Undo.RecordObject(ph, "Glide Placeholder");

            EquipSocket ownerSocket = ph.OwnerSocket;
            if (ownerSocket != null)
            {
                float d = (ph.transform.position - ownerSocket.transform.position).magnitude;
                if (d > 1e-6f)
                {
                    ph.bakedRefDistLocal = d / EquipMath.LossyAvg(ownerSocket.transform);
                }
            }

            EditorUtility.SetDirty(ph);
            Undo.CollapseUndoOperations(undoGroup);
            dragging = false;
            surfaceMissed = false;

            RefitPreview(ph);  // 재베이크 반영 즉시 확인 (WYSIWYG)
        }
    }

    // 자유 이동 모드: 위치만 추적 (refDist 재측정 없음 — 띄우기용)
    private void HandleFreeMove(EquipPlaceholder ph)
    {
        if (ph.transform.localPosition != lastLocalPos)
        {
            lastLocalPos = ph.transform.localPosition;
            EditorUtility.SetDirty(ph);
            RefitPreview(ph);
        }
    }

    // 시각화: refDist 와이어 원 / 이탈 피드백 / 표면 사이클 배지
    private void DrawVisuals(EquipPlaceholder ph)
    {
        // refDist 가시화 (float가 안 보이는 문제 보완 — 소켓 중심의 와이어 원 + 수치)
        EquipSocket owner = ph.OwnerSocket;
        if (owner != null && ph.bakedRefDistLocal > 1e-12f)
        {
            float rWorld = ph.bakedRefDistLocal * EquipMath.LossyAvg(owner.transform);
            Handles.color = new Color(1f, 0.8f, 0.3f, 0.7f);
            Handles.DrawWireDisc(owner.transform.position, ph.transform.up, rWorld);
            Handles.Label(owner.transform.position + ph.transform.up * rWorld, $"refDist {rWorld:F2}");
        }

        if (surfaceMissed)
        {
            // 이탈 피드백: 빨간 점 표시
            Handles.color = Color.red;
            float size = HandleUtility.GetHandleSize(ph.transform.position) * 0.15f;
            Handles.DrawWireDisc(ph.transform.position, SceneView.currentDrawingSceneView.camera.transform.forward, size);
        }

        if (dragging && lastHitCount > 1)
        {
            // 표면 사이클 배지
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 220, 24));
            GUILayout.Label($"표면 {Mathf.Min(hitIndex + 1, lastHitCount)}/{lastHitCount}  (Alt+휠 전환)", EditorStyles.helpBox);
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }

    // 노멀에 수직인 접선 (up=노멀, 접선 규약 — Socket Maker 고스트와 동일)
    private static Vector3 TangentOf(Vector3 up)
    {
        Vector3 t = Vector3.Cross(up, Vector3.right);
        if (t.sqrMagnitude < 1e-6f)
        {
            t = Vector3.Cross(up, Vector3.forward);
        }
        return t.normalized;
    }

    // ── 카탈로그/미리보기 ──

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

    // 미리보기 생성/재핏 (실제 장착과 동일한 FitToPlaceholder = WYSIWYG)
    private void RebuildPreview()
    {
        DestroyPreview();

        EquipEntry entry = GetSelectedEntry();
        EquipPlaceholder ph = (EquipPlaceholder)target;
        EquipSocket socket = ph.OwnerSocket;

        if (entry == null)
        {
            Debug.LogWarning("[EquipPreview] 카탈로그 키가 선택되지 않음.");
            return;
        }
        if (entry.prefab == null)
        {
            Debug.LogWarning($"[EquipPreview] '{entry.key}' 엔트리에 prefab이 비어 있음.");
            return;
        }
        if (socket == null)
        {
            Debug.LogWarning("[EquipPreview] placeholder의 부모에 EquipSocket이 없음.");
            return;
        }

        previewInstance = (GameObject)Instantiate(entry.prefab);
        previewInstance.name = "__EquipPreview__";
        previewInstance.hideFlags = HideFlags.DontSave;

        bool fitted = EquipPlacement.FitToPlaceholder(previewInstance, socket, ph, entry);
        if (fitted == false)
        {
            // 크기 기준 부재(refDist 미베이크)로 장착 거부 — 인스턴스는 내부에서 파괴됨
            previewInstance = null;
            return;
        }

        Debug.Log($"[EquipPreview] '{entry.key}' → {socket.slotId}/{ph.placeholderId} (localScale {previewInstance.transform.localScale.x:G3})");
        EditorGUIUtility.PingObject(previewInstance);
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

        bool fitted = EquipPlacement.FitToPlaceholder(previewInstance, socket, ph, entry);
        if (fitted == false)
        {
            previewInstance = null;
        }
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
