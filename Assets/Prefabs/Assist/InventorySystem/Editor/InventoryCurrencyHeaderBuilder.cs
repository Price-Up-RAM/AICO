using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 커밋된 InventoryPanel 프리팹에 MAIN 전용 재화 헤더를 반복 가능하게 베이크한다.
// 전체 인벤토리 빌더가 제거된 뒤의 소규모 업그레이드 도구라 기존 계층은 보존한다.
public static class InventoryCurrencyHeaderBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Assist/InventorySystem/InventoryPanel.prefab";
    private const string BagIconPath = "Assets/GUIPackCartoon/Demo/Sprites/Icons/Icons Colored/Storage/Bag.png";
    private const string GoldIconPath = "Assets/GUIPackCartoon/Sources/Icons/Icons Colored/PSD/Coin.psd";

    private static readonly Color ButtonBg = new Color(0.16f, 0.16f, 0.20f, 1f);
    private static readonly Color TooltipBg = new Color(0.07f, 0.07f, 0.09f, 0.98f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted = new Color(0.70f, 0.72f, 0.76f, 1f);
    private static readonly Color GoldYellow = new Color(0.95f, 0.78f, 0.30f, 1f);

    // 열려 있는 에디터에서도 이번 스키마 업그레이드가 자동 적용된다. 이미 베이크됐으면 읽기만 하고 종료한다.
    [InitializeOnLoadMethod]
    private static void BuildMissingHeaderAfterReload()
    {
        EditorApplication.delayCall += () =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform area = prefab != null ? prefab.transform.Find("HeaderBar/MainCurrencyArea") : null;
            Transform balance = area != null ? area.Find("GoldBalanceButton") : null;
            Transform titleIcon = area != null ? area.Find("InventoryTitleIcon") : null;
            Transform tooltip = area != null ? area.Find("GoldInfoTooltip") : null;
            bool needsBake = area == null ||
                balance == null ||
                titleIcon == null ||
                tooltip == null ||
                tooltip.GetComponent<Canvas>() == null ||
                tooltip.Find("StorePrice") != null ||
                balance.GetComponent<InventoryGoldInfoHover>() == null;
            if (prefab != null && needsBake)
            {
                Build();
            }
        };
    }

    [MenuItem("Tools/InventorySystem/Build Currency Header")]
    public static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            InventoryView view = root.GetComponent<InventoryView>();
            Transform headerBar = root.transform.Find("HeaderBar");
            TMP_Text headerText = headerBar != null ? headerBar.Find("Header")?.GetComponent<TMP_Text>() : null;
            if (view == null || headerBar == null || headerText == null)
            {
                Debug.LogError("[InventoryCurrencyHeaderBuilder] InventoryPanel의 HeaderBar/Header 계층을 찾지 못했습니다.");
                return;
            }

            RectTransform headerRect = headerText.rectTransform;
            headerRect.anchorMin = Vector2.zero;
            headerRect.anchorMax = Vector2.one;
            headerRect.offsetMin = new Vector2(40f, 0f);
            headerRect.offsetMax = new Vector2(-310f, 0f);
            headerText.alignment = TextAlignmentOptions.MidlineLeft;

            Sprite bagSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BagIconPath);
            Sprite goldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoldIconPath);

            // MAIN 전용 영역 자체를 헤더 전체에 베이크한다. 아이콘도 이 자식이므로
            // 별도 InventoryView 변수 없이 currencyArea 활성 상태를 그대로 따른다.
            GameObject currencyArea = GetOrCreate("MainCurrencyArea", headerBar);
            ConfigureRect(currencyArea.GetComponent<RectTransform>(),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

            Transform oldTitleIcon = headerBar.Find("InventoryTitleIcon");
            if (oldTitleIcon != null && oldTitleIcon.parent != currencyArea.transform)
            {
                oldTitleIcon.SetParent(currencyArea.transform, false);
            }

            GameObject titleIcon = GetOrCreate("InventoryTitleIcon", currencyArea.transform);
            ConfigureRect(titleIcon.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(8f, 0f), new Vector2(24f, 24f));
            Image titleImage = GetOrAdd<Image>(titleIcon);
            titleImage.sprite = bagSprite;
            titleImage.preserveAspect = true;
            titleImage.raycastTarget = false;

            Button debugButton = CreateButton(currencyArea.transform, "DebugGoldButton", "골드 +100", headerText.font);
            ConfigureRect(debugButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-214f, 0f), new Vector2(92f, 26f));

            Button balanceButton = CreateButton(currencyArea.transform, "GoldBalanceButton", string.Empty, headerText.font);
            ConfigureRect(balanceButton.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-88f, 0f), new Vector2(120f, 26f));

            GameObject coinIcon = GetOrCreate("GoldIcon", balanceButton.transform);
            ConfigureRect(coinIcon.GetComponent<RectTransform>(),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(5f, 0f), new Vector2(22f, 22f));
            Image coinImage = GetOrAdd<Image>(coinIcon);
            coinImage.sprite = goldSprite;
            coinImage.preserveAspect = true;
            coinImage.raycastTarget = false;

            TextMeshProUGUI balanceText = GetOrCreateText(
                balanceButton.transform, "GoldBalanceText", "0 G", headerText.font, 14f, GoldYellow);
            RectTransform balanceRect = balanceText.rectTransform;
            balanceRect.anchorMin = Vector2.zero;
            balanceRect.anchorMax = Vector2.one;
            balanceRect.offsetMin = new Vector2(31f, 0f);
            balanceRect.offsetMax = new Vector2(-6f, 0f);
            balanceText.alignment = TextAlignmentOptions.MidlineRight;

            GameObject tooltip = GetOrCreate("GoldInfoTooltip", currencyArea.transform);
            tooltip.transform.SetAsLastSibling();
            ConfigureRect(tooltip.GetComponent<RectTransform>(),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-88f, -4f), new Vector2(300f, 74f));
            Image tooltipImage = GetOrAdd<Image>(tooltip);
            tooltipImage.color = TooltipBg;
            tooltipImage.raycastTarget = false;

            // HeaderBar가 Grid보다 먼저 렌더링되므로 sibling 순서만으로는 툴팁이 앞으로 오지 않는다.
            // 툴팁 자체의 정렬을 덮어써서 같은 루트 Canvas 안에서 항상 인벤토리보다 앞에 그린다.
            Canvas tooltipCanvas = GetOrAdd<Canvas>(tooltip);
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = 1000;

            RemoveChild(tooltip.transform, "Title");
            RemoveChild(tooltip.transform, "StorePrice");
            CreateTooltipText(tooltip.transform, "ClickGold", "마우스클릭 : +2G", headerText.font, 12f, TextWhite, -7f);
            CreateTooltipText(tooltip.transform, "LocalGold", "대화(Local) : +10G", headerText.font, 12f, TextMuted, -27f);
            CreateTooltipText(tooltip.transform, "MissionGold", "기타 : 미션 달성", headerText.font, 12f, TextMuted, -47f);

            InventoryGoldInfoHover hover = GetOrAdd<InventoryGoldInfoHover>(balanceButton.gameObject);
            SerializedObject hoverSo = new SerializedObject(hover);
            Assign(hoverSo, "tooltipPanel", tooltip);
            hoverSo.ApplyModifiedPropertiesWithoutUndo();
            tooltip.SetActive(false);

            SerializedObject viewSo = new SerializedObject(view);
            Assign(viewSo, "mainCurrencyArea", currencyArea);
            Assign(viewSo, "debugGoldButton", debugButton);
            Assign(viewSo, "goldBalanceText", balanceText);
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[InventoryCurrencyHeaderBuilder] MAIN 재화 헤더 베이크 완료: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font)
    {
        GameObject go = GetOrCreate(name, parent);
        Image image = GetOrAdd<Image>(go);
        image.color = ButtonBg;
        image.raycastTarget = true;

        Button button = GetOrAdd<Button>(go);
        button.targetGraphic = image;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        if (string.IsNullOrEmpty(label) == false)
        {
            TextMeshProUGUI text = GetOrCreateText(go.transform, "Label", label, font, 12f, TextWhite);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(5f, 0f);
            rect.offsetMax = new Vector2(-5f, 0f);
            text.alignment = TextAlignmentOptions.Center;
        }

        return button;
    }

    private static TextMeshProUGUI GetOrCreateText(
        Transform parent, string name, string content, TMP_FontAsset font, float fontSize, Color color)
    {
        GameObject go = GetOrCreate(name, parent);
        TextMeshProUGUI text = GetOrAdd<TextMeshProUGUI>(go);
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void CreateTooltipText(
        Transform parent,
        string name,
        string content,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        float topOffset)
    {
        TextMeshProUGUI text = GetOrCreateText(parent, name, content, font, fontSize, color);
        ConfigureRect(text.rectTransform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(12f, topOffset), new Vector2(276f, 20f));
        text.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static GameObject GetOrCreate(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void RemoveChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void Assign(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }
}
