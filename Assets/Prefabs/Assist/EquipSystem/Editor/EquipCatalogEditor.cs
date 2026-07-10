using UnityEditor;
using UnityEngine;

// EquipCatalog 인스펙터: 장착 소켓 해석 사다리(3단) 안내 + 기본 필드.
// 1단은 별도 필드가 없어(엔트리의 Key가 곧 1단) 구조가 안 보인다는 혼선을 막기 위한 설명 헤더.
[CustomEditor(typeof(EquipCatalog))]
public class EquipCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "장착 소켓 해석 사다리 (캐릭터에서 이 순서로 소켓을 찾음):\n" +
            "① Key와 같은 이름의 소켓 — 필드 없음, 자동. 캐릭터에 아이템 전용 자리를 주고 싶으면 그 캐릭터에 Key 이름 소켓만 만들면 최우선으로 이김\n" +
            "② Target Slot Id — 아이템 부류의 특정 자리 (예: hairpin)\n" +
            "③ Fallback Slot Ids — 위가 없을 때 순서대로 시도할 범용 자리 (예: head, chest, origin)\n" +
            "④ 전부 없으면 장착 불가 + 경고",
            MessageType.Info);

        DrawDefaultInspector();
    }
}
