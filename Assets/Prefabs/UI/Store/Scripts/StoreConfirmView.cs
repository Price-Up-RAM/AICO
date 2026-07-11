using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 확인 팝업의 거래 방향: Buy(구매) / Sell(판매)
public enum StoreConfirmMode
{
    Buy,
    Sell
}

/// <summary>
/// 구매/판매 확인 팝업 (이중 모드, 별도 프리팹).
///  - StorePanel 루트 아래에 인스턴스로 붙어 패널 전체를 덮는 모달: 수량(1~max) 조절 + 최종금액 표시 후
///    확인받고 콜백으로 수량을 넘긴다. 결제/판매 실행 자체는 StoreView가 담당.
///  - Buy 모드는 골드 부족 시 합계를 빨갛게, Sell 모드는 수입이므로 항상 노랑.
///  - 표시·숨김은 CanvasGroup만 조작 (SetActive 금지). 베이크 기본 상태 = 숨김.
/// </summary>
public class StoreConfirmView : MonoBehaviour
{
    private static readonly Color BackdropDim = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    private static readonly Color ButtonBg = new Color(0.22f, 0.25f, 0.31f, 1f);
    private static readonly Color AccentBlueHi = new Color(0.306f, 0.404f, 0.608f, 1f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);
    private static readonly Color GoldYellow = new Color(0.95f, 0.78f, 0.30f, 1f);
    private static readonly Color TotalRed = new Color(0.95f, 0.35f, 0.35f, 1f);   // 골드 부족 표시

    private const int MinQty = 1;

    [Header("Style")]
    [Tooltip("비워두면 TMP 기본 폰트. 베이크 시 SUIT-Bold가 지정된다.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite panelSprite;

    private bool built;
    private Sprite roundedSprite;
    private TMP_FontAsset boundFont;

    private CanvasGroup canvasGroup;
    private Image itemIcon;
    private TextMeshProUGUI itemNameText;
    private TextMeshProUGUI qtyText;
    private TextMeshProUGUI totalText;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI messageText;
    private TextMeshProUGUI confirmButtonLabel;
    private Button minusButton;
    private Button plusButton;

    // 현재 확인 중인 거래 상태
    private StoreConfirmMode mode = StoreConfirmMode.Buy;
    private string currentKey;   // 열려 있는 동안의 아이템 키 — UpdateIcon 대상 판별용
    private int unitPrice;
    private int maxQty = MinQty;
    private int quantity = MinQty;
    private System.Action<int> onConfirm;

    private void Awake()
    {
        if (HasBakedHierarchy())
        {
            BindExisting();
            HideInternal();
        }
        else
        {
            // 런타임 코드 조립은 하지 않는다 — 팝업은 베이크된 프리팹이 완결 상태여야 한다
            Debug.LogError("[Store][StoreConfirmView] 베이크된 팝업 계층이 없습니다. 'Tools/Store/2. Build UI Prefab'로 리베이크하세요.");
        }
    }

    // ── 공개 API ────────────────────────────────────────────────────────────────
    // 팝업 열기: 수량 1로 초기화하고 아이템 정보 표시. confirm 시 콜백으로 수량 전달.
    // maxQty = 이번 거래에서 허용되는 최대 수량 (구매: 보유 한도 여유분, 판매: 스택 수량).
    // itemKey = 카탈로그 키 — 프리뷰 캡처가 팝업 오픈 뒤에 완료되는 경우 UpdateIcon 대상 판별에 쓴다.
    public void Open(StoreConfirmMode mode, string itemKey, string displayName, Sprite icon, int unitPrice, int maxQty, System.Action<int> confirmCallback)
    {
        this.mode = mode;
        currentKey = itemKey;
        this.unitPrice = Mathf.Max(0, unitPrice);
        this.maxQty = Mathf.Max(1, maxQty);
        quantity = MinQty;
        onConfirm = confirmCallback;

        bool sell = mode == StoreConfirmMode.Sell;
        if (titleText != null)
        {
            titleText.text = sell ? "판매 확인" : "구매 확인";
        }

        if (messageText != null)
        {
            messageText.text = sell ? "정말 판매하시겠습니까?" : "정말 계산하시겠습니까?";
        }

        if (confirmButtonLabel != null)
        {
            confirmButtonLabel.text = sell ? "판매하기" : "계산하기";
        }

        if (itemNameText != null)
        {
            itemNameText.text = displayName ?? string.Empty;
        }

        if (itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.enabled = icon != null;
        }

        RefreshAmount();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void Close()
    {
        onConfirm = null;
        HideInternal();
    }

    // 프리뷰(포즈/이펙트) 캡처 완료가 팝업 오픈보다 늦게 도착한 경우의 지연 아이콘 반영 (StoreView가 중계).
    // 열려 있고 같은 키를 표시 중일 때만 스프라이트를 교체한다 (enabled는 건드리지 않는다).
    public void UpdateIcon(string key, Sprite sprite)
    {
        if (canvasGroup == null || canvasGroup.alpha <= 0.5f)
        {
            return;
        }

        if (currentKey != key || itemIcon == null || sprite == null)
        {
            return;
        }

        itemIcon.sprite = sprite;
    }

    private void HideInternal()
    {
        currentKey = null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    // ── 수량/금액 ───────────────────────────────────────────────────────────────
    private void OnMinusClicked()
    {
        quantity = Mathf.Max(MinQty, quantity - 1);
        RefreshAmount();
    }

    private void OnPlusClicked()
    {
        quantity = Mathf.Min(maxQty, quantity + 1);
        RefreshAmount();
    }

    private void OnConfirmClicked()
    {
        System.Action<int> callback = onConfirm;
        int qty = quantity;
        Close();
        callback?.Invoke(qty);
    }

    private void RefreshAmount()
    {
        quantity = Mathf.Clamp(quantity, MinQty, maxQty);

        if (qtyText != null)
        {
            qtyText.text = $"{quantity} / {maxQty}";
        }

        if (minusButton != null)
        {
            minusButton.interactable = quantity > MinQty;
        }

        if (plusButton != null)
        {
            plusButton.interactable = quantity < maxQty;
        }

        if (totalText != null)
        {
            int total = unitPrice * quantity;
            totalText.text = $"합계 {total:N0} G";

            // Buy: 보유 골드로 감당 불가하면 빨강 (결제 시도는 StoreView가 최종 판정)
            // Sell: 수입이므로 항상 노랑
            bool affordable = true;
            if (mode == StoreConfirmMode.Buy && Application.isPlaying && InventoryManager.Instance != null)
            {
                affordable = InventoryManager.Instance.Gold >= total;
            }

            totalText.color = affordable ? GoldYellow : TotalRed;
        }
    }

    // ── 베이크된 프리팹 연결 ─────────────────────────────────────────────────────
    private bool HasBakedHierarchy()
    {
        return FindDeepChild(transform, "ConfirmPanel") != null;
    }

    private void BindExisting()
    {
        built = true;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        itemIcon = FindComponent<Image>("ItemIcon");
        itemNameText = FindComponent<TextMeshProUGUI>("ItemNameText");
        qtyText = FindComponent<TextMeshProUGUI>("QtyText");
        totalText = FindComponent<TextMeshProUGUI>("TotalText");
        titleText = FindComponent<TextMeshProUGUI>("ConfirmTitle");
        messageText = FindComponent<TextMeshProUGUI>("ConfirmMessageText");
        minusButton = FindComponent<Button>("QtyMinusButton");
        plusButton = FindComponent<Button>("QtyPlusButton");

        // ConfirmButton의 라벨(자식 "Text") — 모드별 문구 교체용
        Transform confirmTransform = FindDeepChild(transform, "ConfirmButton");
        if (confirmTransform != null)
        {
            Transform label = FindDeepChild(confirmTransform, "Text");
            confirmButtonLabel = label != null ? label.GetComponent<TextMeshProUGUI>() : null;
        }

        if (itemNameText != null)
        {
            boundFont = itemNameText.font;
        }

        BindButton("Backdrop", Close);
        BindButton("QtyMinusButton", OnMinusClicked);
        BindButton("QtyPlusButton", OnPlusClicked);
        BindButton("CancelButton", Close);
        BindButton("ConfirmButton", OnConfirmClicked);
    }

    private void BindButton(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = FindComponent<Button>(name);
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

#if UNITY_EDITOR
    // 에디터 베이크 전용 (StoreTools가 호출)
    public void EditorBuild(Sprite roundedSpriteAsset, TMP_FontAsset fontAsset)
    {
        if (roundedSpriteAsset != null)
        {
            panelSprite = roundedSpriteAsset;
        }

        if (fontAsset != null)
        {
            font = fontAsset;
        }

        Build();
        HideInternal();  // 베이크 기본 상태 = 숨김
    }
#endif

#if UNITY_EDITOR
    // ── UI 구성 (에디터 베이크 전용 — 런타임은 베이크된 계층을 BindExisting으로 연결만 한다) ──
    private void Build()
    {
        if (built)
        {
            return;
        }

        built = true;

        // 루트 = 부모(StorePanel) 전체를 덮는 컨테이너
        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        gameObject.layer = 5;

        canvasGroup = GetOrAdd<CanvasGroup>(gameObject);

        // 백드롭: 어둡게 깔고 클릭 시 닫힘 (아래 상점 UI 입력 차단)
        Button backdrop = CreateButton("Backdrop", transform, string.Empty, BackdropDim, 12);
        SetStretch(backdrop.gameObject, Vector4.zero);
        backdrop.onClick.AddListener(Close);

        // 확인 패널 (중앙 340x280)
        GameObject panel = CreatePanel("ConfirmPanel", transform, PanelBg);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(340f, 280f);

        titleText = CreateText("ConfirmTitle", panel.transform, "구매 확인", 16, TextWhite, TextAlignmentOptions.Center);
        TopStretch(titleText.gameObject, 12f, 12f, 12f, 24f);

        GameObject iconGo = CreateUIObject("ItemIcon", panel.transform);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -44f);
        iconRect.sizeDelta = new Vector2(56f, 56f);
        itemIcon = iconGo.AddComponent<Image>();
        itemIcon.preserveAspect = true;
        itemIcon.raycastTarget = false;

        itemNameText = CreateText("ItemNameText", panel.transform, "이름", 14, TextWhite, TextAlignmentOptions.Center);
        TopStretch(itemNameText.gameObject, 12f, 12f, 104f, 22f);

        // 수량 조절 행: [-] [n / max] [+]
        minusButton = CreateButton("QtyMinusButton", panel.transform, "-", ButtonBg, 16);
        AnchorCenterTop(minusButton.gameObject, -64f, -140f, 36f, 30f);

        qtyText = CreateText("QtyText", panel.transform, "1 / 1", 15, TextWhite, TextAlignmentOptions.Center);
        AnchorCenterTop(qtyText.gameObject, 0f, -140f, 60f, 30f);

        plusButton = CreateButton("QtyPlusButton", panel.transform, "+", ButtonBg, 16);
        AnchorCenterTop(plusButton.gameObject, 64f, -140f, 36f, 30f);

        totalText = CreateText("TotalText", panel.transform, "합계 0 G", 15, GoldYellow, TextAlignmentOptions.Center);
        TopStretch(totalText.gameObject, 12f, 12f, 178f, 22f);

        messageText = CreateText("ConfirmMessageText", panel.transform, "정말 계산하시겠습니까?", 13, TextMuted, TextAlignmentOptions.Center);
        TopStretch(messageText.gameObject, 12f, 12f, 202f, 20f);

        // 하단 버튼 행: [취소] [계산하기/판매하기]
        Button cancel = CreateButton("CancelButton", panel.transform, "취소", ButtonBg, 14);
        AnchorBottom(cancel.gameObject, -78f, 12f, 140f, 34f);
        cancel.onClick.AddListener(Close);

        Button confirm = CreateButton("ConfirmButton", panel.transform, "계산하기", AccentBlueHi, 14);
        AnchorBottom(confirm.gameObject, 78f, 12f, 140f, 34f);
        confirm.onClick.AddListener(OnConfirmClicked);

        Transform confirmLabel = FindDeepChild(confirm.transform, "Text");
        confirmButtonLabel = confirmLabel != null ? confirmLabel.GetComponent<TextMeshProUGUI>() : null;

        minusButton.onClick.AddListener(OnMinusClicked);
        plusButton.onClick.AddListener(OnPlusClicked);
    }

    // ── 팩토리 헬퍼 (StoreView와 동일 관용구) ───────────────────────────────────
    private GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        Image image = go.AddComponent<Image>();
        ApplyRounded(image, color);
        return go;
    }

    private Button CreateButton(string name, Transform parent, string label, Color background, float fontSize)
    {
        GameObject root = CreatePanel(name, parent, background);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        button.colors = colors;

        if (string.IsNullOrEmpty(label) == false)
        {
            TextMeshProUGUI text = CreateText("Text", root.transform, label, fontSize, TextWhite, TextAlignmentOptions.Center);
            SetStretch(text.gameObject, Vector4.zero);
        }

        return button;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        TMP_FontAsset resolved = ResolveFont();
        if (resolved != null)
        {
            text.font = resolved;
        }

        return text;
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (go.layer < 0)
        {
            go.layer = 5;
        }

        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one;
        return go;
    }

    private void ApplyRounded(Image image, Color color)
    {
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
    }

    private Sprite GetRoundedSprite()
    {
        if (panelSprite != null)
        {
            return panelSprite;
        }

        if (roundedSprite == null)
        {
            roundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        }

        return roundedSprite;
    }

    private TMP_FontAsset ResolveFont()
    {
        if (font != null)
        {
            return font;
        }

        if (boundFont != null)
        {
            return boundFont;
        }

        return TMP_Settings.defaultFontAsset;
    }
#endif

    private T FindComponent<T>(string name) where T : Component
    {
        Transform t = FindDeepChild(transform, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }

            Transform found = FindDeepChild(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    private static void SetStretch(GameObject go, Vector4 padding)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding.x, padding.y);
        rect.offsetMax = new Vector2(-padding.z, -padding.w);
    }

    private static void TopStretch(GameObject go, float left, float right, float top, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -(top + height));
        rect.offsetMax = new Vector2(-right, -top);
    }

    // 상단 중앙 기준 고정 크기 배치 (수량 조절 행)
    private static void AnchorCenterTop(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
    }

    // 하단 중앙 기준 고정 크기 배치 (취소/계산 버튼)
    private static void AnchorBottom(GameObject go, float x, float bottom, float w, float h)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, bottom);
        rect.sizeDelta = new Vector2(w, h);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
        {
            component = go.AddComponent<T>();
        }

        return component;
    }
#endif
}
