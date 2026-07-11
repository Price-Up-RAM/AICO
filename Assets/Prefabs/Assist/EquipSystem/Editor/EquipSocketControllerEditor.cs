using UnityEditor;
using UnityEngine;

// EquipSocketController 인스펙터: 이 캐릭터의 소켓 현황 + origin 소켓 재생성 버튼.
// (컴포넌트 추가 순간 Reset이 origin 소켓을 자동 생성 — 지웠다가 다시 만들 때 이 버튼 사용)
[CustomEditor(typeof(EquipSocketController))]
public class EquipSocketControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EquipSocketController controller = (EquipSocketController)target;

        EditorGUILayout.HelpBox("캐릭터의 EquipSystem 진입점. 추가하는 순간 원점(0,0,0)에 origin 소켓이 자동 생성됩니다.\n표면 소켓(머리핀 등)은 Tools → EquipSystem → Socket Maker로 만드세요.", MessageType.Info);

        // 이 캐릭터의 소켓 현황
        EquipSocket[] sockets = controller.GetComponentsInChildren<EquipSocket>(true);
        EditorGUILayout.LabelField($"소켓 {sockets.Length}개", EditorStyles.boldLabel);
        foreach (EquipSocket socket in sockets)
        {
            if (socket == null)
            {
                continue;
            }

            EditorGUILayout.BeginHorizontal();
            string boneName = "(부모 없음)";
            if (socket.transform.parent != null)
            {
                boneName = socket.transform.parent.name;
            }
            EditorGUILayout.LabelField("· " + socket.slotId, GUILayout.Width(140));
            EditorGUILayout.LabelField("본: " + boneName);
            if (GUILayout.Button("선택", GUILayout.Width(40)))
            {
                Selection.activeGameObject = socket.gameObject;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();

        // origin 소켓이 없으면(지웠다면) 재생성 버튼
        bool hasOrigin = EquipSocket.Find(controller.gameObject, EquipSocketController.OriginSlotId) != null;
        using (new EditorGUI.DisabledScope(hasOrigin))
        {
            string label = "origin 소켓 생성 (0,0,0)";
            if (hasOrigin)
            {
                label = "origin 소켓 있음";
            }
            if (GUILayout.Button(label))
            {
                EquipSocket created = controller.CreateOriginSocket();
                if (created != null)
                {
                    Undo.RegisterCreatedObjectUndo(created.gameObject, "Create Origin Socket");
                    EditorUtility.SetDirty(controller.gameObject);
                    Selection.activeGameObject = created.gameObject;
                }
            }
        }
    }
}
