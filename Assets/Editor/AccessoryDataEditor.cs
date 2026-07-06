using UnityEditor;
using UnityEngine;

// AccessoryData 인스펙터에 Scene에서 잡은 Slot 위치를 바로 기록하는 캡처 버튼 추가
[CustomEditor(typeof(AccessoryData))]
public class AccessoryDataEditor : Editor
{
    // 다른 오브젝트 선택 등으로 Editor 인스턴스가 재생성돼도 값이 유지되도록 EditorPrefs에 저장
    private const string PrefKeyCharacterCode = "AccessoryDataEditor.captureCharacterCode";
    private const string PrefKeySlotName = "AccessoryDataEditor.captureSlotName";
    private const string PrefKeyAccessoryName = "AccessoryDataEditor.captureAccessoryName";

    private string captureCharacterCode;
    private string captureSlotName;
    private string captureAccessoryName;

    private void OnEnable()
    {
        captureCharacterCode = EditorPrefs.GetString(PrefKeyCharacterCode, "");
        captureSlotName = EditorPrefs.GetString(PrefKeySlotName, "hairpin");
        captureAccessoryName = EditorPrefs.GetString(PrefKeyAccessoryName, "hairpin_placeholder");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Slot 캡처 (선택된 Slot_HairPin_R 기준)", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        captureCharacterCode = EditorGUILayout.TextField("Character Code", captureCharacterCode);
        captureSlotName = EditorGUILayout.TextField("Slot Name (부위 식별자)", captureSlotName);
        captureAccessoryName = EditorGUILayout.TextField("Accessory Name", captureAccessoryName);
        if (EditorGUI.EndChangeCheck())
        {
            // 값이 바뀌면 즉시 EditorPrefs에 저장
            EditorPrefs.SetString(PrefKeyCharacterCode, captureCharacterCode);
            EditorPrefs.SetString(PrefKeySlotName, captureSlotName);
            EditorPrefs.SetString(PrefKeyAccessoryName, captureAccessoryName);
        }

        Transform selected = Selection.activeTransform;
        Transform placeholder = null;

        if (selected != null && selected.childCount > 0)
        {
            // Slot의 첫 자식을 악세서리(hairpin_placeholder)로 간주
            placeholder = selected.GetChild(0);
        }

        if (selected == null)
        {
            EditorGUILayout.HelpBox("Hierarchy에서 Slot_HairPin_R을 선택하세요.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("선택된 Slot", selected.name);
            EditorGUILayout.LabelField("Position", selected.localPosition.ToString("F4"));
            EditorGUILayout.LabelField("Rotation", selected.localEulerAngles.ToString("F2"));
            if (placeholder != null)
            {
                EditorGUILayout.LabelField("Placeholder Scale", placeholder.localScale.ToString("F4"));

                if (placeholder.localScale.x < 0 || placeholder.localScale.y < 0 || placeholder.localScale.z < 0)
                {
                    // 음수 스케일은 대부분 Scene에서 축을 잘못 드래그해 생긴 실수이므로 캡처 전에 경고
                    EditorGUILayout.HelpBox("Scale에 음수 값이 있습니다. 의도한 반전이 아니라면 Scene에서 placeholder의 Scale을 양수로 바로잡고 다시 캡처하세요.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Slot 하위에 악세서리(hairpin_placeholder)가 없습니다. Scale은 (1,1,1)로 기록됩니다.", MessageType.Warning);
            }
        }

        using (new EditorGUI.DisabledScope(selected == null || string.IsNullOrEmpty(captureCharacterCode) || string.IsNullOrEmpty(captureSlotName)))
        {
            if (GUILayout.Button("이 값으로 캡처 / 갱신"))
            {
                AccessoryData data = (AccessoryData)target;
                Undo.RecordObject(data, "Capture Accessory Slot");
                data.CaptureSlotTransform(captureCharacterCode, captureSlotName, captureAccessoryName, selected, placeholder);
                EditorUtility.SetDirty(data);
            }
        }
    }
}
