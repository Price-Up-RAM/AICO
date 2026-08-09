using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// CharacterDetail 태그/셀렉터 UI 베이크 — 기능 태그 스키마 v2(bool 4종 + tagSpecials) 프리팹 반영분.
// 1) FormText(형태 : 2D) 오브젝트 삭제 — 형태 표시 폐기
// 2) 상태 영역(상태/사용가능/다운로드필요) → 의상 셀렉터(< 의상명 >) 변환 + 컨트롤러 참조 배선
//    (태그 슬롯은 8개 유지 — bool 4종 + specials 표시에 충분)
// 실행: 메뉴 Tools/CharacterDetail/Bake Tag UI (또는 batchmode -executeMethod ...BatchSetup)
// ※ 2026-08 YAML 직접 베이크로 이미 프리팹에 반영됨 — 본 툴은 개발 PC 재실행용 멱등 안전망
public static class CharacterDetailTagUiTools
{
    private const string PrefabPath = "Assets/Prefabs/UI/CharacterDetail/CharacterDetail.prefab";
    private static readonly Color SelectorButtonDark = new Color(0.047f, 0.055f, 0.071f, 1f);

    [MenuItem("Tools/CharacterDetail/Bake Tag UI (형태 제거 + 의상 셀렉터)")]
    public static void SetupAll()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            bool removed = RemoveFormText(root);
            bool converted = ConvertStatusAreaToClothesSelector(root);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterDetail][TagUiTools] 베이크 완료 — FormText 제거: {removed}, 의상 셀렉터 변환: {converted}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // batchmode -executeMethod 진입점 (다이얼로그 없음)
    public static void BatchSetup()
    {
        SetupAll();
    }

    // FormText 오브젝트 삭제 (이미 없으면 no-op — 직렬화 참조 필드는 스키마 v2에서 삭제됨)
    private static bool RemoveFormText(GameObject root)
    {
        Transform formText = FindDeep(root.transform, "FormText");
        if (formText == null)
        {
            Debug.Log("[CharacterDetail][TagUiTools] FormText가 이미 없습니다 — 건너뜀.");
            return false;
        }

        Object.DestroyImmediate(formText.gameObject);
        return true;
    }

    // 상태 영역 → 의상 셀렉터 변환 (이미 ClothesSelector가 있으면 컨트롤러 참조 배선만 보정)
    private static bool ConvertStatusAreaToClothesSelector(GameObject root)
    {
        bool converted = false;
        Transform selector = FindDeep(root.transform, "ClothesSelector");

        if (selector == null)
        {
            Transform container = FindDeep(root.transform, "StatusTagContainer");
            Transform label = FindDeep(root.transform, "StatusLabelText");
            Transform left = FindDeep(root.transform, "StatusTag_Available");
            Transform right = FindDeep(root.transform, "StatusTag_DownloadRequired");
            if (container == null || label == null || left == null || right == null)
            {
                Debug.LogError("[CharacterDetail][TagUiTools] 상태 영역 오브젝트를 찾지 못했습니다 — 변환 건너뜀.");
                return false;
            }

            // 컨테이너: 구 라벨/칩 2줄 영역의 중간 높이로 재배치
            container.name = "ClothesSelector";
            SetRect(container, new Vector2(20f, -440f), new Vector2(300f, 34f));

            // 좌 버튼 (구 사용가능 칩)
            left.name = "ClothesLeftButton";
            SetRect(left, new Vector2(0f, 0f), new Vector2(34f, 34f));
            SetupSelectorButton(left, "<");

            // 가운데 의상명 텍스트 (구 상태 라벨을 셀렉터 하위로 이동)
            label.name = "ClothesText";
            label.SetParent(container, false);
            label.SetSiblingIndex(1);
            SetRect(label, new Vector2(42f, 0f), new Vector2(216f, 34f));
            TMP_Text labelText = label.GetComponent<TMP_Text>();
            if (labelText != null)
            {
                labelText.text = "-";
                labelText.horizontalAlignment = HorizontalAlignmentOptions.Center;
            }

            // 우 버튼 (구 다운로드필요 칩 — 기본 비활성이었으므로 활성화)
            right.name = "ClothesRightButton";
            right.gameObject.SetActive(true);
            SetRect(right, new Vector2(266f, 0f), new Vector2(34f, 34f));
            SetupSelectorButton(right, ">");

            converted = true;
        }

        // 컨트롤러 직렬화 참조 배선 (변환 여부와 무관하게 보정)
        selector = FindDeep(root.transform, "ClothesSelector");
        CharacterDetailController controller = root.GetComponent<CharacterDetailController>();
        if (selector != null && controller != null)
        {
            Transform left = FindDeep(selector, "ClothesLeftButton");
            Transform right = FindDeep(selector, "ClothesRightButton");
            Transform text = FindDeep(selector, "ClothesText");

            SerializedObject serializedController = new SerializedObject(controller);
            SetRef(serializedController, "clothesLeftButton", left != null ? left.GetComponent<Button>() : null);
            SetRef(serializedController, "clothesRightButton", right != null ? right.GetComponent<Button>() : null);
            SetRef(serializedController, "clothesText", text != null ? text.GetComponent<TMP_Text>() : null);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        return converted;
    }

    // 셀렉터 버튼 공통 세팅 — 어두운 배경 + Button + 화살표 텍스트
    private static void SetupSelectorButton(Transform chip, string arrow)
    {
        Image background = chip.GetComponent<Image>();
        if (background != null)
        {
            background.color = SelectorButtonDark;
        }

        Button button = chip.GetComponent<Button>();
        if (button == null)
        {
            button = chip.gameObject.AddComponent<Button>();
        }
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;

        TMP_Text arrowText = chip.GetComponentInChildren<TMP_Text>(true);
        if (arrowText != null)
        {
            arrowText.gameObject.name = chip.name + "_Text";
            arrowText.text = arrow;
            arrowText.fontSize = 20f;
            arrowText.horizontalAlignment = HorizontalAlignmentOptions.Center;
            SetRect(arrowText.transform, new Vector2(0f, 0f), new Vector2(34f, 34f));
        }
    }

    private static void SetRect(Transform target, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform rect = target as RectTransform;
        if (rect == null)
        {
            return;
        }

        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void SetRef(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
