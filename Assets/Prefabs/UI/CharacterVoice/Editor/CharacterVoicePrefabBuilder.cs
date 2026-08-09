#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterVoicePrefabBuilder
{
    private const string ResourceFolder =
        "Assets/Prefabs/UI/CharacterVoice/Resources/CharacterVoice";
    private const string AlarmPath = ResourceFolder + "/CharacterVoiceAlarmView.prefab";
    private const string ConfirmPath = ResourceFolder + "/CharacterVoiceAlarmConfirmView.prefab";
    private const string PomodoroPath = ResourceFolder + "/CharacterVoicePomodoroView.prefab";
    private const string PomodoroConfirmPath =
        ResourceFolder + "/CharacterVoicePomodoroConfirmView.prefab";
    private const string PomodoroCatalogPath =
        "Assets/Prefabs/UI/CharacterVoice/Resources/CharacterPomodoroVoiceCatalog.asset";
    private const string SelectionCheckmarkPath =
        "Assets/GUIPackCartoon/Demo/Sprites/Icons/Icons White/Basic/Checkmark.png";

    public static void BuildPrefabs()
    {
        EnsureFolders();
        EnsurePomodoroCatalog();
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite selectionCheckmark =
            AssetDatabase.LoadAssetAtPath<Sprite>(SelectionCheckmarkPath);
        if (selectionCheckmark == null)
        {
            throw new System.InvalidOperationException(
                "[CharacterVoice] Missing selection checkmark sprite: " +
                SelectionCheckmarkPath);
        }
        TMP_FontAsset font = FindDefaultFont();
        BuildAlarm(sprite, font);
        BuildConfirm(sprite, font, selectionCheckmark);
        BuildPomodoro(sprite, font);
        BuildPomodoroConfirm(sprite, font, selectionCheckmark);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        PrefabDragHandlerInjector.ApplyToPrefab(AlarmPath);
        PrefabDragHandlerInjector.ApplyToPrefab(ConfirmPath);
        PrefabDragHandlerInjector.ApplyToPrefab(PomodoroPath);
        PrefabDragHandlerInjector.ApplyToPrefab(PomodoroConfirmPath);
        Debug.Log("[CharacterVoice] Alarm/Pomodoro views and confirm views baked.");
    }

    public static void BuildPomodoroPrefab()
    {
        EnsureFolders();
        EnsurePomodoroCatalog();
        Sprite sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd");
        TMP_FontAsset font = FindDefaultFont();
        BuildPomodoro(sprite, font);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        PrefabDragHandlerInjector.ApplyToPrefab(PomodoroPath);
        Debug.Log(
            "[CharacterVoice] CharacterVoicePomodoroView prefab baked.");
    }

    public static void ValidatePrefabs()
    {
        ValidatePrefab<CharacterVoiceAlarmView>(
            AlarmPath, "Header", "ConceptRow", "DirectAddRow", "AlarmVoiceBody",
            "AlarmVoiceRowTemplate", "DeleteButton", "TrashIcon", "EditInput");
        ValidateChildComponent<CanvasGroup>(
            AlarmPath,
            "AlarmVoiceRowTemplate");
        ValidatePrefab<CharacterVoiceAlarmConfirmView>(
            ConfirmPath, "Header", "AlarmConfirmBody", "AlarmCandidateRowTemplate",
            "SelectionCheckmarkImage", "RegenerateButton", "PlayButton");
        ValidateChildImageSprite(
            ConfirmPath,
            "SelectionCheckmarkImage");
        ValidatePrefab<CharacterVoicePomodoroView>(
            PomodoroPath, "Header", "ConceptRow", "DirectAddRow", "PomodoroVoiceBody",
            "PomodoroVoiceRowTemplate", "DeleteButton", "TrashIcon", "EditInput",
            "SituationDropdown");
        ValidateChildComponent<CanvasGroup>(
            PomodoroPath,
            "PomodoroVoiceRowTemplate");
        ValidateChildComponent<TMP_Dropdown>(
            PomodoroPath,
            "SituationDropdown");
        ValidatePrefab<CharacterVoicePomodoroConfirmView>(
            PomodoroConfirmPath, "Header", "AlarmConfirmBody", "AlarmCandidateRowTemplate",
            "SelectionCheckmarkImage", "RegenerateButton", "PlayButton");
        ValidateChildImageSprite(
            PomodoroConfirmPath,
            "SelectionCheckmarkImage");
        Debug.Log("[CharacterVoice] Prefab validation passed.");
    }

    private static void BuildAlarm(Sprite sprite, TMP_FontAsset font)
    {
        GameObject root = NewRoot("CharacterVoiceAlarmView");
        try
        {
            root.AddComponent<CharacterVoiceAlarmView>().EditorBuild(sprite, font);
            PrefabUtility.SaveAsPrefabAsset(root, AlarmPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildConfirm(
        Sprite sprite,
        TMP_FontAsset font,
        Sprite selectionCheckmark)
    {
        GameObject root = NewRoot("CharacterVoiceAlarmConfirmView");
        try
        {
            root.AddComponent<CharacterVoiceAlarmConfirmView>()
                .EditorBuild(sprite, font, false, selectionCheckmark);
            PrefabUtility.SaveAsPrefabAsset(root, ConfirmPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildPomodoro(Sprite sprite, TMP_FontAsset font)
    {
        GameObject root = NewRoot("CharacterVoicePomodoroView");
        try
        {
            root.AddComponent<CharacterVoicePomodoroView>().EditorBuild(sprite, font);
            PrefabUtility.SaveAsPrefabAsset(root, PomodoroPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildPomodoroConfirm(
        Sprite sprite,
        TMP_FontAsset font,
        Sprite selectionCheckmark)
    {
        GameObject root = NewRoot("CharacterVoicePomodoroConfirmView");
        try
        {
            root.AddComponent<CharacterVoicePomodoroConfirmView>()
                .EditorBuildPomodoro(sprite, font, selectionCheckmark);
            PrefabUtility.SaveAsPrefabAsset(root, PomodoroConfirmPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void EnsurePomodoroCatalog()
    {
        if (AssetDatabase.LoadAssetAtPath<CharacterPomodoroVoiceCatalog>(
                PomodoroCatalogPath) != null)
        {
            return;
        }

        CharacterPomodoroVoiceCatalog catalog =
            ScriptableObject.CreateInstance<CharacterPomodoroVoiceCatalog>();
        AssetDatabase.CreateAsset(catalog, PomodoroCatalogPath);
    }

    private static GameObject NewRoot(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        root.layer = 5;
        return root;
    }

    private static void ValidatePrefab<T>(string path, params string[] requiredNames)
        where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null || prefab.GetComponent<T>() == null)
        {
            throw new System.InvalidOperationException(
                "[CharacterVoice] Missing prefab or component: " + path);
        }

        for (int i = 0; i < requiredNames.Length; i++)
        {
            if (FindDeep(prefab.transform, requiredNames[i]) == null)
            {
                throw new System.InvalidOperationException(
                    "[CharacterVoice] Missing baked object '" + requiredNames[i] + "': " + path);
            }
        }
    }

    private static void ValidateChildComponent<T>(
        string path,
        string childName)
        where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Transform child =
            prefab != null ? FindDeep(prefab.transform, childName) : null;
        if (child == null || child.GetComponent<T>() == null)
        {
            throw new System.InvalidOperationException(
                "[CharacterVoice] Missing " + typeof(T).Name +
                " on '" + childName + "': " + path);
        }
    }

    private static void ValidateChildImageSprite(
        string path,
        string childName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Transform child =
            prefab != null ? FindDeep(prefab.transform, childName) : null;
        Image image = child != null ? child.GetComponent<Image>() : null;
        RectTransform rect = child as RectTransform;
        if (image == null ||
            image.sprite == null ||
            AssetDatabase.GetAssetPath(image.sprite) != SelectionCheckmarkPath ||
            rect == null ||
            Vector2.Distance(rect.anchorMin, new Vector2(0.15f, 0.15f)) > 0.001f ||
            Vector2.Distance(rect.anchorMax, new Vector2(0.85f, 0.85f)) > 0.001f)
        {
            throw new System.InvalidOperationException(
                "[CharacterVoice] Invalid checkmark Image on '" +
                childName + "': " + path);
        }
    }

    private static TMP_FontAsset FindDefaultFont()
    {
        TextMeshProUGUI text = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/CharacterDetail/CharacterDetail.prefab")
            ?.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null && text.font != null)
        {
            return text.font;
        }

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        return guids.Length == 0
            ? null
            : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Prefabs/UI/CharacterVoice", "Resources");
        EnsureFolder("Assets/Prefabs/UI/CharacterVoice/Resources", "CharacterVoice");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string full = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Transform FindDeep(Transform parent, string targetName)
    {
        if (parent == null) return null;
        if (parent.name == targetName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }
}

[InitializeOnLoad]
public static class CharacterVoicePrefabAutoBaker
{
    private const string AlarmPath =
        "Assets/Prefabs/UI/CharacterVoice/Resources/CharacterVoice/CharacterVoiceAlarmView.prefab";
    private const string ConfirmPath =
        "Assets/Prefabs/UI/CharacterVoice/Resources/CharacterVoice/CharacterVoiceAlarmConfirmView.prefab";
    private const string PomodoroPath =
        "Assets/Prefabs/UI/CharacterVoice/Resources/CharacterVoice/CharacterVoicePomodoroView.prefab";
    private const string PomodoroConfirmPath =
        "Assets/Prefabs/UI/CharacterVoice/Resources/CharacterVoice/CharacterVoicePomodoroConfirmView.prefab";

    static CharacterVoicePrefabAutoBaker()
    {
        EditorApplication.delayCall += BakeIfNeeded;
    }

    private static void BakeIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BakeIfNeeded;
            return;
        }

        GameObject alarm = AssetDatabase.LoadAssetAtPath<GameObject>(AlarmPath);
        GameObject confirm = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmPath);
        GameObject pomodoro = AssetDatabase.LoadAssetAtPath<GameObject>(PomodoroPath);
        GameObject pomodoroConfirm =
            AssetDatabase.LoadAssetAtPath<GameObject>(PomodoroConfirmPath);
        Transform alarmTitle =
            alarm != null ? alarm.transform.Find("Header/TitleText") : null;
        LayoutElement alarmTitleLayout =
            alarmTitle != null ? alarmTitle.GetComponent<LayoutElement>() : null;
        Transform alarmDirectAdd =
            alarm != null ? FindDeep(alarm.transform, "DirectAddRow") : null;
        Transform alarmNotice =
            alarm != null ? FindDeep(alarm.transform, "NoticeText") : null;
        TMP_Text alarmRegenerateLabel = alarm != null
            ? FindDeep(alarm.transform, "RegenerateButton")?.GetComponentInChildren<TMP_Text>(true)
            : null;
        TMP_Text alarmPlayLabel = alarm != null
            ? FindDeep(alarm.transform, "PlayButton")?.GetComponentInChildren<TMP_Text>(true)
            : null;
        TMP_Text alarmAddLabel = alarm != null
            ? FindDeep(alarm.transform, "DirectAddRow")
                ?.Find("AddButton")
                ?.GetComponentInChildren<TMP_Text>(true)
            : null;
        TMP_Text pomodoroAddLabel = pomodoro != null
            ? FindDeep(pomodoro.transform, "DirectAddRow")
                ?.Find("AddButton")
                ?.GetComponentInChildren<TMP_Text>(true)
            : null;
        ScrollRect alarmScroll = alarm != null
            ? FindDeep(alarm.transform, "AlarmVoiceBody")?.GetComponent<ScrollRect>()
            : null;
        CanvasGroup alarmRowCanvasGroup = alarm != null
            ? FindDeep(alarm.transform, "AlarmVoiceRowTemplate")
                ?.GetComponent<CanvasGroup>()
            : null;
        CanvasGroup pomodoroRowCanvasGroup = pomodoro != null
            ? FindDeep(pomodoro.transform, "PomodoroVoiceRowTemplate")
                ?.GetComponent<CanvasGroup>()
            : null;
        bool pomodoroSituationDropdownMissing =
            pomodoro == null ||
            FindDeep(pomodoro.transform, "SituationDropdown")
                ?.GetComponent<TMP_Dropdown>() == null;
        Transform confirmRegenerate =
            confirm != null ? FindDeep(confirm.transform, "RegenerateButton") : null;
        Transform confirmPlay =
            confirm != null ? FindDeep(confirm.transform, "PlayButton") : null;
        TMP_Text confirmButtonLabel = confirm != null
            ? FindDeep(confirm.transform, "ConfirmButton")?.GetComponentInChildren<TMP_Text>(true)
            : null;
        Image confirmCheckmark = confirm != null
            ? FindDeep(confirm.transform, "SelectionCheckmarkImage")
                ?.GetComponent<Image>()
            : null;
        Image pomodoroConfirmCheckmark = pomodoroConfirm != null
            ? FindDeep(pomodoroConfirm.transform, "SelectionCheckmarkImage")
                ?.GetComponent<Image>()
            : null;
        if (alarm == null ||
            confirm == null ||
            pomodoro == null ||
            pomodoroConfirm == null ||
            alarm.transform.Find("Header/AlarmSamplePlayButton") == null ||
            alarm.transform.Find("Header/AlarmGenerateButton") != null ||
            alarm.transform.Find("ConceptRow/ConceptInput") == null ||
            alarm.transform.Find("ConceptRow/RandomConceptButton") == null ||
            alarm.transform.Find("ConceptRow/AlarmGenerateButton") == null ||
            alarm.transform.Find("DirectAddRow/AddInput") == null ||
            alarm.transform.Find("DirectAddRow/AddButton") == null ||
            FindDeep(alarm.transform, "DeleteButton") == null ||
            FindDeep(alarm.transform, "TrashIcon") == null ||
            FindDeep(alarm.transform, "EditInput") == null ||
            alarmTitleLayout == null ||
            alarmTitleLayout.minWidth < 179f ||
            alarmDirectAdd == null ||
            alarmNotice == null ||
            alarmNotice.GetSiblingIndex() <= alarmDirectAdd.GetSiblingIndex() ||
            alarmRegenerateLabel == null ||
            alarmRegenerateLabel.text != "재생성" ||
            alarmPlayLabel == null ||
            alarmPlayLabel.text != "듣기" ||
            alarmAddLabel == null ||
            alarmAddLabel.text != "추가" ||
            pomodoroAddLabel == null ||
            pomodoroAddLabel.text != "추가" ||
            alarmScroll == null ||
            Mathf.Abs(alarmScroll.scrollSensitivity - 20f) > 0.1f ||
            alarmRowCanvasGroup == null ||
            pomodoroRowCanvasGroup == null ||
            confirmRegenerate == null ||
            confirmPlay == null ||
            confirmButtonLabel == null ||
            confirmButtonLabel.text != "선택 항목 추가" ||
            confirmCheckmark == null ||
            confirmCheckmark.sprite == null ||
            pomodoroConfirmCheckmark == null ||
            pomodoroConfirmCheckmark.sprite == null ||
            pomodoro.transform.Find("Header/PomodoroGenerateButton") != null ||
            pomodoro.transform.Find("ConceptRow/ConceptInput") == null ||
            pomodoro.transform.Find("ConceptRow/RandomConceptButton") == null ||
            pomodoro.transform.Find("ConceptRow/PomodoroGenerateButton") == null ||
            pomodoro.transform.Find("DirectAddRow/AddInput") == null ||
            pomodoro.transform.Find("DirectAddRow/AddButton") == null ||
            FindDeep(pomodoro.transform, "DeleteButton") == null ||
            FindDeep(pomodoro.transform, "TrashIcon") == null ||
            FindDeep(pomodoro.transform, "EditInput") == null ||
            FindDeep(pomodoroConfirm.transform, "RegenerateButton") == null ||
            FindDeep(pomodoroConfirm.transform, "PlayButton") == null)
        {
            CharacterVoicePrefabBuilder.BuildPrefabs();
        }
        else if (pomodoroSituationDropdownMissing)
        {
            CharacterVoicePrefabBuilder.BuildPomodoroPrefab();
        }

        GameObject detail = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/CharacterDetail/CharacterDetail.prefab");
        Transform customSection =
            detail != null ? FindDeep(detail.transform, "CustomVoiceSection") : null;
        Transform customTitle =
            detail != null ? FindDeep(detail.transform, "CustomVoiceTitleText") : null;
        Transform customScroll =
            detail != null ? FindDeep(detail.transform, "AlarmVoiceListScroll") : null;
        Transform customScrollbar =
            detail != null ? FindDeep(detail.transform, "CustomVoiceScrollbar") : null;
        TMP_Text customTitleText =
            customTitle != null ? customTitle.GetComponent<TMP_Text>() : null;
        RectTransform customSectionRect = customSection as RectTransform;
        RectTransform customScrollRect = customScroll as RectTransform;
        RectTransform customScrollbarRect = customScrollbar as RectTransform;
        CharacterDetailController detailController =
            detail != null ? detail.GetComponent<CharacterDetailController>() : null;
        bool customVoiceCatalogsMissing = true;
        if (detailController != null)
        {
            SerializedObject serializedController =
                new SerializedObject(detailController);
            SerializedProperty alarmCatalog =
                serializedController.FindProperty("characterAlarmVoiceCatalog");
            SerializedProperty pomodoroCatalog =
                serializedController.FindProperty("characterPomodoroVoiceCatalog");
            customVoiceCatalogsMissing =
                alarmCatalog == null ||
                alarmCatalog.objectReferenceValue == null ||
                pomodoroCatalog == null ||
                pomodoroCatalog.objectReferenceValue == null;
        }
        bool customVoiceLayoutOutdated =
            customVoiceCatalogsMissing ||
            customSection == null ||
            customSectionRect == null ||
            customSectionRect.anchorMax.x < 0.99f ||
            customSectionRect.sizeDelta.y < 155f ||
            customTitleText == null ||
            customTitleText.fontSize < 17.9f ||
            customScrollRect == null ||
            customScrollRect.anchorMax.x < 0.99f ||
            customScrollRect.sizeDelta.y < 125f ||
            Mathf.Abs(customScrollRect.anchoredPosition.y + 26f) > 0.1f ||
            customScrollbarRect == null ||
            customScrollbarRect.offsetMax.x > -6f;
        if (detail != null && customVoiceLayoutOutdated)
        {
            CharacterDetailAlarmVoiceUiTools.Setup();
        }

        CharacterVoicePrefabBuilder.ValidatePrefabs();
        CharacterDetailAlarmVoiceUiTools.ValidateBatch();
    }

    private static Transform FindDeep(Transform parent, string targetName)
    {
        if (parent == null) return null;
        if (parent.name == targetName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
