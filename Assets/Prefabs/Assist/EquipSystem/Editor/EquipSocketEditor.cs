using UnityEditor;
using UnityEngine;

// EquipSocket 커스텀 인스펙터: slotId 리네임 동기화(GO명+카탈로그) + 부착점 상태 안내/목록.
// 미리보기·장착 테스트는 부착점(placeholder) 인스펙터와 Socket Maker 현황판의 [테스트]가 담당한다.
[CustomEditor(typeof(EquipSocket))]
public class EquipSocketEditor : Editor
{
    private EquipCatalog catalog;   // 리네임 동기화용 카탈로그

    private void OnEnable()
    {
        LoadCatalog();
    }

    // 프로젝트에서 EquipCatalog 에셋 자동 로드
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
    }

    public override void OnInspectorGUI()
    {
        EquipSocket socket = (EquipSocket)target;

        // 기본 필드 (slotId) — 변경 감지해 GO명/카탈로그 동기화
        string prevSlotId = socket.slotId;
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck() && socket.slotId != prevSlotId)
        {
            SyncSlotIdRename(socket, prevSlotId, socket.slotId);
        }

        // 미리네임 경고: socket_N은 임시 이름 — slotId가 카탈로그/전파의 열쇠
        if (string.IsNullOrEmpty(socket.slotId) == false && socket.slotId.StartsWith("socket_"))
        {
            EditorGUILayout.HelpBox($"아직 임시 이름입니다 ('{socket.slotId}'). slotId는 카탈로그·전파가 이 자리를 찾는 열쇠 — 의미 있는 이름(head, ribbon 등)으로 바꾸세요.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // 상태 안내: 부착점 유무 → refDist 베이크 여부 → 정상
        EquipPlaceholder firstPh = socket.FindPlaceholder("placeholder");
        if (firstPh == null)
        {
            EditorGUILayout.HelpBox("부착점(placeholder) 없음 — 장착이 거부됩니다. Socket Maker의 고스트 클릭 배치로 재저작하세요.", MessageType.Warning);
        }
        else
        {
            if (firstPh.bakedRefDistLocal <= 1e-12f)
            {
                EditorGUILayout.HelpBox("refDist 미베이크 — 장착이 거부됩니다. 부착점을 메시 글라이드로 움직여 재베이크하거나 refDist를 직접 입력하세요.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("refDist 소켓 — 미리보기/조정은 부착점 인스펙터에서, 장착 테스트는 Socket Maker 현황판의 [테스트] 버튼으로.", MessageType.Info);
            }
        }

        if (firstPh != null)
        {
            if (GUILayout.Button("부착점 선택 (미리보기/조정)"))
            {
                Selection.activeGameObject = firstPh.gameObject;
            }
            if (GUILayout.Button("고스트 재조정 (Socket Maker)"))
            {
                EquipSocketMakerWindow.BeginRepick(firstPh);
            }
        }

        // Placeholder 목록 (refDist 표시)
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placeholders (부착점)", EditorStyles.boldLabel);
        EquipPlaceholder[] placeholders = socket.GetComponentsInChildren<EquipPlaceholder>(true);
        foreach (EquipPlaceholder ph in placeholders)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("· " + ph.placeholderId, GUILayout.Width(100));
            EditorGUILayout.LabelField($"refDist {ph.bakedRefDistLocal:F4}", GUILayout.Width(140));
            if (GUILayout.Button("선택", GUILayout.Width(40)))
            {
                Selection.activeGameObject = ph.gameObject;
            }
            EditorGUILayout.EndHorizontal();
        }
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
}
