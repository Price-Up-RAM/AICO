using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Store(상점) 메인 컨트롤러 (이중 모드).
///  - 베이크된 프리팹 계층("HeaderBar" 존재)이 있으면 BindExisting으로 참조만 연결하고, 없으면 코드로 자가 구축.
///  - 표시·숨김은 반드시 CanvasGroup(alpha/interactable/blocksRaycasts)만 조작한다 (SetActive 금지).
///  - 정적 크롬(헤더/탭/페이지바/판매존)은 고정 앵커, 레이아웃 그룹은 동적 콘텐츠인 GoodsGrid에만 허용.
///
/// 탭: 데이터 주도 — StoreCatalog(태그 레지스트리)의 태그 목록이 곧 탭 목록이고, 태그별 상품은
///     각 태그의 StoreTagCatalog가 소유한다(카탈로그 부재 시 DefaultTabs 폴백).
///     폴더 감성 — 탭이 본문(Body) 위에 살짝 겹쳐 얹히고, 선택 탭은 본문과 같은 색으로 이어진다.
///     슬롯은 최대 6개(TabButton_0~5), 태그 없는 슬롯은 비활성. 한도 초과 태그는 경고 후 절단.
/// 페이징: 그리드는 페이지당 6장(3x2) 고정, 하단 [ &lt; n / m &gt; ] 페이지바로 이동 (InventoryView 방식).
///
/// 재화 루프
///  - 구매: 카드 클릭 → StoreConfirmView(Buy 모드, 수량/최종금액 확인 팝업) → 확인 시 SpendGold → AddToMain(key, 수량).
///          AddToMain 실패 시 전액 환불. 장착물 탭 구매 성공 시 MissionList.Report("AF0005", 수량).
///  - 판매: StoreSellZone(IDropHandler)이 드롭 검증 후 RequestSell 호출 → StoreConfirmView(Sell 모드,
///          수량 선택) → 확인 시 ExecuteSale(스택 차감 + EarnGold) → NotifySold.
///  - 골드 표시: InventoryManager.InventoryChanged 구독. 보유 수: InventoryEvents.OnStoreChanged 구독.
/// </summary>
public class StoreView : MonoBehaviour
{
    // ── 다크 팔레트 (Store_Design.md 참조) ─────────────────────────────────────
    private static readonly Color RootBg = new Color(0.09f, 0.09f, 0.11f, 0.96f);
    private static readonly Color HeaderBg = new Color(0.125f, 0.141f, 0.173f, 1f);
    private static readonly Color PanelBg = new Color(0.137f, 0.157f, 0.196f, 1f);
    private static readonly Color ButtonBg = new Color(0.22f, 0.25f, 0.31f, 1f);
    private static readonly Color AccentBlueHi = new Color(0.306f, 0.404f, 0.608f, 1f);
    private static readonly Color Track = new Color(0.047f, 0.055f, 0.071f, 0.9f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);
    private static readonly Color GoldYellow = new Color(0.95f, 0.78f, 0.30f, 1f);
    private static readonly Color GoldFlashRed = new Color(0.95f, 0.35f, 0.35f, 1f);

    // ── 탭 (카탈로그의 태그 목록이 곧 탭 — 아래 상수는 리롤/미션 판정용 이름 겸 폴백 구성) ──
    private const string TabEquip = "장착물";
    private const string TabPose = "포즈";
    private const string TabFx = "이펙트";
    private const string TabGift = "선물";
    private const string TabMisc = "잡화";
    // 카탈로그 부재 시 폴백 탭 목록
    private static readonly string[] DefaultTabs = { TabEquip, TabPose, TabFx, TabGift, TabMisc };
    private const int MaxTabSlots = 6;

    // ── 고정 앵커 배치 상수 ──────────────────────────────────────────────────────
    private const float PanelWidth = 520f;
    private const float PanelHeight = 560f;
    private const float HeaderHeight = 40f;
    private const float TabTop = 44f;          // 탭 상단 (헤더 40 + 4)
    private const float TabHeight = 36f;       // 탭 표시 높이
    private const float TabOverlap = 6f;       // 본문 위로 겹치는 깊이 (폴더 감성)
    private const float TabUnselectedInset = 6f; // 비선택 탭이 낮아지는 양
    private const float BodyTop = TabTop + TabHeight;          // 80
    private const float BodyBottom = 114f;     // 판매존 84 + 토스트 밴드 30
    private const float PageBarHeight = 22f;   // Body 내부 하단 페이지바
    private const float SellZoneHeight = 84f;
    private const float Margin = 10f;
    private const int CardsPerPage = 6;        // 3열 x 2행

    [Header("Style")]
    [Tooltip("비워두면 TMP 기본 폰트를 사용한다. 베이크 시 SUIT-Bold가 지정된다.")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("둥근 모서리용 9-slice 스프라이트. 베이크 시 빌트인 UISprite가 지정된다.")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("포즈 리롤 버튼 아이콘. 비워두면 'R' 텍스트로 폴백. 베이크 시 StoreTools가 지정한다.")]
    [SerializeField] private Sprite rerollIconSprite;

    [Header("Data")]
    [SerializeField] private StoreCatalog catalog;      // 미지정 시 Awake에서 Resources/StoreCatalog 폴백
    [SerializeField] private GameObject confirmPrefab;  // 구매 확인 팝업 프리팹 (베이크 시 주입, 없으면 코드 자가 구축)

    private bool built;
    private bool tabOverflowWarned;   // 태그가 슬롯 한도를 초과할 때 경고를 1회만 남기는 래치
    private string currentTab = TabEquip;
    private int currentPage;
    private Sprite roundedSprite;
    private TMP_FontAsset boundFont;
    private InventoryManager subscribedWallet;
    private StoreManager subscribedStoreManager;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI goldText;
    private TextMeshProUGUI toastText;
    private TextMeshProUGUI pageLabel;
    private Button pagePrevButton;
    private Button pageNextButton;
    private Button poseRerollButton;
    private readonly Image[] tabImages = new Image[MaxTabSlots];
    private readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[MaxTabSlots];
    private RectTransform goodsGrid;
    private GameObject cardTemplate;
    private StoreSellZone sellZone;
    private StoreConfirmView confirmView;

    private void Awake()
    {
        // 빌드/바인드 직전에 카탈로그를 확보하고 currentTab을 태그 목록에 정렬한다
        EnsureCatalog();
        AlignCurrentTab();

        if (HasBakedHierarchy())
        {
            // 프리팹에 UI가 이미 구워져 있으면 기존 자식에 연결만 한다.
            BindExisting();
        }
        else
        {
            // 런타임 코드 조립은 하지 않는다 — UI는 베이크된 프리팹이 완결 상태여야 한다.
            // built가 false로 남아 Refresh/Show 등 공개 API는 전부 무동작한다.
            Debug.LogError("[Store][StoreView] 베이크된 UI 계층이 없습니다. 'Tools/Store/Setup All'로 프리팹을 베이크한 뒤 사용하세요.");
        }
    }

    // 첫 페인트는 Start에서 — InventorySystemManager.Awake(카탈로그 로드) 이후를 보장
    private void Start()
    {
        // 프리뷰(포즈/이펙트) 캡처 완료 브로드캐스트 구독 (에디트 모드에서는 Instance가 null)
        subscribedStoreManager = StoreManager.Instance;
        if (subscribedStoreManager != null)
        {
            subscribedStoreManager.IconReady += OnPreviewIconReady;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        // 종료 중 Instance getter를 부르면 매니저가 재생성될 수 있어, 구독 당시 참조로만 해제한다
        if (subscribedStoreManager != null)
        {
            subscribedStoreManager.IconReady -= OnPreviewIconReady;
            subscribedStoreManager = null;
        }
    }

    private void OnEnable()
    {
        InventoryEvents.OnStoreChanged += HandleStoreChanged;

        if (Application.isPlaying)
        {
            subscribedWallet = InventoryManager.Instance;
            if (subscribedWallet != null)
            {
                subscribedWallet.InventoryChanged += HandleWalletChanged;
            }
        }

        if (built)
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        InventoryEvents.OnStoreChanged -= HandleStoreChanged;

        if (subscribedWallet != null)
        {
            subscribedWallet.InventoryChanged -= HandleWalletChanged;
            subscribedWallet = null;
        }
    }

    private void HandleWalletChanged()
    {
        RefreshGold();
    }

    // MAIN 스토어 변경 → 보유 수 갱신 (구매/판매/이동 모두)
    private void HandleStoreChanged(string ownerId)
    {
        if (ownerId == InventorySystemManager.MainOwnerId)
        {
            RebuildGrid();
        }
    }

    // ── 공개 API (표시/숨김은 CanvasGroup만 조작) ──────────────────────────────
    public void Show()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        Refresh();
    }

    public void Hide()
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (confirmView != null)
        {
            confirmView.Close();
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void Toggle()
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (canvasGroup.alpha > 0.5f)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    // 현재 탭 상품 그리드 재구성 + 골드 갱신
    public void Refresh()
    {
        if (built == false)
        {
            return;
        }

        RefreshGold();
        RefreshTabVisuals();
        RebuildGrid();
        UpdatePoseRerollVisibility();
    }

    // 리롤 버튼은 포즈 탭에서만 노출. 이 SetActive는 TabBar 예비 슬롯과 같은 "탭 개별 표시" 예외
    // 계열이라 창 표시·숨김의 CanvasGroup 규칙 위반이 아니다. Refresh가 유일한 안전 토글 지점 —
    // SelectTab 훅만으로는 Start/OnEnable/Show 경로를 놓친다.
    private void UpdatePoseRerollVisibility()
    {
        if (poseRerollButton != null)
        {
            poseRerollButton.gameObject.SetActive(currentTab == TabPose);
        }
    }

    private void OnPoseRerollClicked()
    {
        if (StoreManager.Instance != null)
        {
            StoreManager.Instance.RerollPoses();
        }
    }

    public void SelectTab(string tab)
    {
        if (string.IsNullOrEmpty(tab) || tab == currentTab)
        {
            return;
        }

        currentTab = tab;
        currentPage = 0;
        Refresh();
    }

    // ── 판매가 튜닝 노브 — 판매가 밸런스 조정은 아래 두 상수만 만지면 된다 ──────
    private const int SellPricePercent = 50;   // 카탈로그 아이템: 구매가 대비 판매가 비율(%)
    private const int DefaultSellPrice = 10;   // 카탈로그에 없는 아이템의 기본 판매가(G)

    // 판매가 규칙: 카탈로그에 있으면 구매가의 SellPricePercent%(최소 1G), 없으면 DefaultSellPrice
    public int GetSellPrice(string key)
    {
        if (catalog != null && catalog.Contains(key))
        {
            return Mathf.Max(1, catalog.Get(key).price * SellPricePercent / 100);
        }

        return DefaultSellPrice;
    }

    // SellZone이 판매 완료 시 호출 → 토스트 + 골드 갱신
    public void NotifySold(string displayName, int count, int gold)
    {
        ShowToast($"{displayName} x{count} 판매 +{gold:N0} G");
        RefreshGold();
    }

    // 토스트 표시 (2초 후 자동 소거)
    public void ShowToast(string message)
    {
        if (toastText == null)
        {
            return;
        }

        toastText.text = message ?? string.Empty;
        CancelInvoke(nameof(ClearToast));
        Invoke(nameof(ClearToast), 2f);
    }

    private void ClearToast()
    {
        if (toastText != null)
        {
            toastText.text = string.Empty;
        }
    }

    // ── 구매 흐름: 카드 클릭 → 확인 팝업(수량/최종금액) → 확정 시 결제 ──────────
    private void OnCardClicked(StoreEntry entry)
    {
        if (entry == null || Application.isPlaying == false)
        {
            return;
        }

        InventoryEntry meta = ResolveMeta(entry.key);
        string displayName = ResolveDisplayName(entry, meta);

        // 모달용 아이콘: 실아이콘(카탈로그 → 프리뷰 캐시) → NoImage 폴백
        Sprite icon = null;
        if (StoreManager.Instance != null)
        {
            icon = StoreManager.Instance.ResolveIcon(entry.key);
            if (icon == null)
            {
                icon = StoreManager.Instance.NoImageSprite;
            }
        }

        // 보유 한도 여유분이 없으면 팝업을 열지 않는다
        int maxStack = meta != null ? meta.maxStack : 99;
        int room = maxStack - OwnedCount(entry.key);
        if (room <= 0)
        {
            ShowToast("보유 한도가 가득 찼습니다");
            return;
        }

        EnsureConfirmView();
        if (confirmView == null)
        {
            // 확인 없는 거래는 하지 않는다 — 프리팹 베이크 누락은 도구 재실행으로 해결할 문제
            ShowToast("확인 팝업이 없어 구매할 수 없습니다 (Tools/Store 리베이크 필요)");
            return;
        }

        StoreEntry captured = entry;
        confirmView.Open(StoreConfirmMode.Buy, entry.key, displayName, icon, entry.price, Mathf.Min(99, room), qty => ExecutePurchase(captured, qty));
    }

    // 결제 실행: SpendGold(총액) → AddToMain(key, 수량). 지급 실패 시 전액 환불.
    private void ExecutePurchase(StoreEntry entry, int quantity)
    {
        if (entry == null || quantity < 1 || Application.isPlaying == false)
        {
            return;
        }

        InventoryManager wallet = InventoryManager.Instance;
        if (wallet == null)
        {
            return;
        }

        // AddToMain은 최대 스택 초과분을 조용히 버리고도 true를 반환한다 — 결제 전에 수용량을 선검증
        InventoryEntry meta = ResolveMeta(entry.key);
        int maxStack = meta != null ? meta.maxStack : 99;
        int room = maxStack - OwnedCount(entry.key);
        if (quantity > room)
        {
            ShowToast($"보유 한도 초과 (추가 가능 {Mathf.Max(0, room)}개)");
            return;
        }

        int total = entry.price * quantity;
        if (wallet.SpendGold(total) == false)
        {
            ShowToast("골드가 부족합니다");
            FlashGoldRed();
            return;
        }

        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null || manager.AddToMain(entry.key, quantity) == false)
        {
            // 지급 실패 → 전액 환불 (AddToMain이 카탈로그 검증/최대 스택에서 거부할 수 있다)
            // 실패한 결제의 되돌림이라 RefundGold — earned/spent 누적을 펌핑하지 않는다
            wallet.RefundGold(total);
            ShowToast("아이템 지급에 실패해 환불되었습니다");
            Debug.LogWarning($"[Store][StoreView] AddToMain 실패로 환불: {entry.key} x{quantity} ({total} G)");
            return;
        }

        ShowToast($"{ResolveDisplayName(entry, ResolveMeta(entry.key))} x{quantity} 구매 -{total:N0} G");

        // 장착물(액세서리) 구매 미션 (CH0007 골드 소비는 SpendGold만으로 자동 진행).
        // StoreEntry는 태그를 갖지 않으므로 레지스트리에서 키의 소속 태그를 역조회한다.
        if (catalog != null && catalog.TagForKey(entry.key) == TabEquip && MissionList.Instance != null)
        {
            MissionList.Instance.Report("AF0005", quantity);
        }
    }

    // ── 판매 흐름: SellZone 드롭 → 확인 팝업(Sell 모드, 수량 선택) → 확정 시 스택 차감 ──
    // StoreSellZone이 드롭 검증을 마친 뒤 호출한다.
    public void RequestSell(string key, int slotIndex, int stackCount)
    {
        string displayName = ResolveSellDisplayName(key);

        // 모달용 아이콘: 구매 경로와 같은 체인(실아이콘 → NoImage 폴백). 판매 대상은 카드가 만들어진 적
        // 없는 키일 수 있어(다른 탭에서 드래그) 프리뷰 키면 여기서 캡처를 요청한다 — 완료 시
        // IconReady → UpdateIcon으로 열린 모달에 반영된다.
        Sprite icon = null;
        if (StoreManager.Instance != null)
        {
            icon = StoreManager.Instance.ResolveIcon(key);
            if (icon == null)
            {
                icon = StoreManager.Instance.NoImageSprite;
                if (StoreManager.Instance.IsPreviewKey(key))
                {
                    StoreManager.Instance.RequestPreview(key);
                }
            }
        }

        EnsureConfirmView();
        if (confirmView == null)
        {
            // 확인 없는 거래는 하지 않는다 (구매 경로와 동일 규칙)
            ShowToast("확인 팝업이 없어 판매할 수 없습니다 (Tools/Store 리베이크 필요)");
            return;
        }

        confirmView.Open(StoreConfirmMode.Sell, key, displayName, icon, GetSellPrice(key), stackCount, qty => ExecuteSale(key, slotIndex, qty));
    }

    // 판매 실행: 스택에서 수량만큼 차감(전량이면 스택 제거) → 저장 + 이벤트 + 골드 지급.
    // 판매 확정은 드래그가 끝난 뒤(모달 확인 시점)에 실행되므로, 판매 중 그리드 리빌드가
    // 진행 중인 드래그를 깨뜨리던 문제는 이 경로에는 없다.
    private void ExecuteSale(string key, int slotIndex, int quantity)
    {
        if (Application.isPlaying == false)
        {
            return;
        }

        InventorySystemManager manager = InventorySystemManager.Instance;
        InventoryManager wallet = InventoryManager.Instance;
        if (manager == null || wallet == null)
        {
            Debug.LogWarning("[Store][StoreView] 매니저/지갑 참조가 없어 판매를 처리할 수 없습니다.");
            return;
        }

        // 모달이 떠 있는 동안 스토어가 바뀌었을 수 있으므로 슬롯을 다시 확인한다
        InvStore store = manager.GetMainStore();
        InvItemStack stack = store != null ? store.FindBySlot(slotIndex) : null;
        if (stack == null || stack.key != key)
        {
            ShowToast("판매 대상이 변경되어 취소되었습니다");
            return;
        }

        quantity = Mathf.Clamp(quantity, 1, stack.count);
        if (quantity >= stack.count)
        {
            store.stacks.Remove(stack);
        }
        else
        {
            stack.count = stack.count - quantity;
        }

        manager.SaveStore(store);
        InventoryEvents.OnStoreChanged?.Invoke(InventorySystemManager.MainOwnerId);

        int total = GetSellPrice(key) * quantity;
        wallet.EarnGold(total);
        NotifySold(ResolveSellDisplayName(key), quantity, total);
        Debug.Log($"[Store][StoreView] 판매: {key} x{quantity} → +{total}G");
    }

    // 판매 표시 이름: 인벤토리 카탈로그 메타 → 상점 카탈로그 엔트리 → 키 순서로 폴백
    private string ResolveSellDisplayName(string key)
    {
        InventoryEntry meta = ResolveMeta(key);
        if (meta != null && string.IsNullOrEmpty(meta.displayName) == false)
        {
            return meta.displayName;
        }

        if (catalog != null && catalog.Contains(key))
        {
            StoreEntry entry = catalog.Get(key);
            if (entry != null && string.IsNullOrEmpty(entry.displayName) == false)
            {
                return entry.displayName;
            }
        }

        return key;
    }

    // 구매 확인 팝업 확보: 프리팹에 베이크된 자식(BindExisting에서 연결)이 기본.
    // 자식이 없는 구버전 프리팹만 참조 프리팹을 1회 인스턴스한다 — 코드 자가 구축 폴백은 없다.
    private void EnsureConfirmView()
    {
        if (confirmView != null || confirmPrefab == null)
        {
            return;
        }

        GameObject go = Instantiate(confirmPrefab, transform);
        go.name = "StoreConfirm";
        confirmView = go.GetComponent<StoreConfirmView>();
        if (confirmView == null)
        {
            Destroy(go);  // 컴포넌트 없는 잘못된 프리팹 방어
        }
    }

    // 골드 부족 시 GoldText 0.5초 빨강 플래시
    private void FlashGoldRed()
    {
        if (goldText == null)
        {
            return;
        }

        goldText.color = GoldFlashRed;
        CancelInvoke(nameof(RestoreGoldColor));
        Invoke(nameof(RestoreGoldColor), 0.5f);
    }

    private void RestoreGoldColor()
    {
        if (goldText != null)
        {
            goldText.color = GoldYellow;
        }
    }

    // ── 갱신 ───────────────────────────────────────────────────────────────────
    private void RefreshGold()
    {
        if (goldText == null)
        {
            return;
        }

        int gold = 0;
        if (Application.isPlaying && InventoryManager.Instance != null)
        {
            gold = InventoryManager.Instance.Gold;
        }

        goldText.text = $"{gold:N0} G";
    }

    // 폴더 감성: 선택 탭 = 본문색 + 전체 높이(본문과 이어짐), 비선택 = 어둡게 + 낮게
    private void RefreshTabVisuals()
    {
        // 탭 목록은 루프 밖에서 1회만 해석 — 슬롯마다 ResolveTabs를 다시 부르면 리스트 재구축이 중복된다
        string[] tabs = ResolveTabs();

        for (int i = 0; i < MaxTabSlots; i++)
        {
            if (tabImages[i] == null)
            {
                continue;
            }

            string tabName = i < tabs.Length ? tabs[i] : null;

            // 태그 없는 예비 슬롯 숨김 — TabBar 예비 슬롯과 같은 "탭 개별 표시" 예외 계열
            tabImages[i].gameObject.SetActive(tabName != null);
            if (tabName == null)
            {
                continue;
            }

            // 카탈로그 태그가 리베이크 없이 바뀌어도 라벨이 따라가도록 재동기화
            SetText(tabLabels[i], tabName);

            bool selected = tabName == currentTab;
            tabImages[i].color = selected ? PanelBg : HeaderBg;

            // 선택 탭만 본문 위 겹침 구간(TabOverlap)까지 내려와 본문과 이어진다.
            // 비선택 탭은 본문 상단에서 끝나고(offsetMin) 위도 낮게(offsetMax) — 폴더 뒷장 느낌.
            RectTransform rect = tabImages[i].rectTransform;
            rect.offsetMax = new Vector2(rect.offsetMax.x, selected ? 0f : -TabUnselectedInset);
            rect.offsetMin = new Vector2(rect.offsetMin.x, selected ? 0f : TabOverlap);

            if (tabLabels[i] != null)
            {
                tabLabels[i].color = selected ? TextWhite : TextMuted;
            }
        }
    }

    // 카탈로그 폴백 확보 (Awake/Build/RebuildGrid 공용). EditorBuild 경로는 Awake를 거치지
    // 않으므로 Build() 진입부에서도 호출해야 BuildTabBar가 태그를 읽을 수 있다.
    private void EnsureCatalog()
    {
        if (catalog == null)
        {
            catalog = Resources.Load<StoreCatalog>("StoreCatalog");
        }
    }

    // 탭 목록 결정: 카탈로그(태그 레지스트리)의 태그 목록 우선, 없으면 DefaultTabs 폴백.
    // 슬롯 한도(MaxTabSlots) 초과분은 경고 로그 1회 후 절단한다.
    private string[] ResolveTabs()
    {
        if (catalog != null)
        {
            List<string> tags = catalog.Tabs();
            if (tags != null && tags.Count > 0)
            {
                if (tags.Count > MaxTabSlots)
                {
                    if (tabOverflowWarned == false)
                    {
                        tabOverflowWarned = true;
                        Debug.LogWarning($"[Store][StoreView] 태그 {tags.Count}종이 탭 슬롯 한도({MaxTabSlots})를 초과해 앞 {MaxTabSlots}종만 표시합니다.");
                    }

                    tags = tags.GetRange(0, MaxTabSlots);
                }

                return tags.ToArray();
            }
        }

        return DefaultTabs;
    }

    // currentTab이 현재 탭 목록에 없으면 첫 요소로 정렬한다 (목록에 이미 있으면 유지)
    private void AlignCurrentTab()
    {
        string[] tabs = ResolveTabs();
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] == currentTab)
            {
                return;
            }
        }

        if (tabs.Length > 0)
        {
            currentTab = tabs[0];
        }
    }

    private string TabNameForSlot(int slot)
    {
        string[] tabs = ResolveTabs();
        return slot >= 0 && slot < tabs.Length ? tabs[slot] : null;
    }

    private void OnTabSlotClicked(int slot)
    {
        string tab = TabNameForSlot(slot);
        if (string.IsNullOrEmpty(tab) == false)
        {
            SelectTab(tab);
        }
    }

    private void OnPagePrevClicked()
    {
        if (currentPage > 0)
        {
            currentPage = currentPage - 1;
            RebuildGrid();
        }
    }

    private void OnPageNextClicked()
    {
        currentPage = currentPage + 1;
        RebuildGrid();  // 상한 클램프는 RebuildGrid가 처리
    }

    // 현재 탭·페이지 상품으로 그리드 재구성 + 페이지바 갱신
    private void RebuildGrid()
    {
        if (goodsGrid == null || cardTemplate == null)
        {
            return;
        }

        ClearGrid();

        // 카탈로그는 Awake(런타임)/EditorBuild(베이크)가 확보한다 — 여기서는 확인만
        if (catalog == null)
        {
            Debug.LogWarning("[Store][StoreView] StoreCatalog이 없습니다 (Resources/StoreCatalog 또는 인스펙터 지정).");
            currentPage = 0;
            if (pageLabel != null)
            {
                pageLabel.text = "1 / 1";
            }
            if (pagePrevButton != null)
            {
                pagePrevButton.interactable = false;
            }
            if (pageNextButton != null)
            {
                pageNextButton.interactable = false;
            }
            return;
        }

        List<StoreEntry> list = catalog.EntriesForTab(currentTab);
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)CardsPerPage));
        currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);

        int start = currentPage * CardsPerPage;
        int end = Mathf.Min(list.Count, start + CardsPerPage);
        for (int i = start; i < end; i++)
        {
            CreateCard(list[i]);
        }

        if (pageLabel != null)
        {
            pageLabel.text = $"{currentPage + 1} / {pageCount}";
        }

        if (pagePrevButton != null)
        {
            pagePrevButton.interactable = currentPage > 0;
        }

        if (pageNextButton != null)
        {
            pageNextButton.interactable = currentPage < pageCount - 1;
        }
    }

    // 그리드의 기존 카드 전부 제거 (템플릿은 그리드 밖에 있어 안전)
    private void ClearGrid()
    {
        for (int i = goodsGrid.childCount - 1; i >= 0; i--)
        {
            Transform child = goodsGrid.GetChild(i);

            if (Application.isPlaying)
            {
                // Destroy는 지연 파괴라 레이아웃에 한 프레임 남는다 — 먼저 분리해 즉시 제외
                child.SetParent(null, false);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    // 카드 1장 생성: 아이콘은 상점 카탈로그 소유(StoreManager.ResolveIcon — Inventory와 별개),
    // 이름/maxStack만 InventoryCatalog 메타 우선에 StoreEntry 폴백
    private void CreateCard(StoreEntry entry)
    {
        GameObject card = Instantiate(cardTemplate, goodsGrid);
        card.name = "Card_" + entry.key;
        card.SetActive(true);

        InventoryEntry meta = ResolveMeta(entry.key);

        Image icon = FindChildComponent<Image>(card.transform, "CardIcon");
        if (icon != null)
        {
            StoreManager storeManager = StoreManager.Instance;
            Sprite resolved = storeManager != null ? storeManager.ResolveIcon(entry.key) : null;
            if (resolved != null)
            {
                icon.sprite = resolved;
                icon.color = Color.white;
                icon.enabled = true;
            }
            else if (storeManager != null)
            {
                // 실아이콘이 아직 없으면 NoImage를 우선 깔고(플레이스홀더 부재 시 숨김), 프리뷰(포즈/이펙트)
                // 키면 비동기 캡처를 요청한다. 캡처 완료는 IconReady 브로드캐스트 → OnPreviewIconReady로 반영.
                Sprite placeholder = storeManager.NoImageSprite;
                if (placeholder != null)
                {
                    icon.sprite = placeholder;
                    icon.color = Color.white;
                    icon.enabled = true;
                }
                else
                {
                    icon.enabled = false;
                }

                if (storeManager.IsPreviewKey(entry.key))
                {
                    storeManager.RequestPreview(entry.key);
                }
            }
            else
            {
                // 에디트 모드 베이크(매니저 없음) → 이름 텍스트로만 표시
                icon.enabled = false;
            }
        }

        SetText(FindChildComponent<TextMeshProUGUI>(card.transform, "CardName"), ResolveDisplayName(entry, meta));
        SetText(FindChildComponent<TextMeshProUGUI>(card.transform, "CardPrice"), $"{entry.price:N0} G");
        SetText(FindChildComponent<TextMeshProUGUI>(card.transform, "CardSub"), string.IsNullOrEmpty(entry.detailText) ? string.Empty : entry.detailText);
        SetText(FindChildComponent<TextMeshProUGUI>(card.transform, "CardOwned"), "보유 " + OwnedCount(entry.key));

        Button button = card.GetComponent<Button>();
        if (button != null)
        {
            StoreEntry captured = entry;
            button.onClick.AddListener(() => OnCardClicked(captured));
        }
    }

    // 프리뷰(포즈/이펙트) 캡처 완료 브로드캐스트 수신. 캡처가 끝나는 사이 뷰가 파괴되거나
    // 그리드가 리빌드(탭 이동/페이지 이동)되어 카드가 사라졌을 수 있으므로,
    // 콜백 시점에 "Card_"+key 카드를 다시 찾아 존재할 때만 아이콘을 반영한다.
    private void OnPreviewIconReady(string key, Sprite sprite)
    {
        if (this == null || sprite == null || goodsGrid == null)
        {
            return;
        }

        Transform card = FindDeepChild(goodsGrid, "Card_" + key);
        if (card != null)
        {
            Image icon = FindChildComponent<Image>(card, "CardIcon");
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = Color.white;
                icon.enabled = true;
            }
        }

        // 확인 팝업이 같은 키를 NoImage로 열어둔 채일 수 있어, 카드 유무와 무관하게 전달한다
        if (confirmView != null)
        {
            confirmView.UpdateIcon(key, sprite);
        }
    }

    private static InventoryEntry ResolveMeta(string key)
    {
        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null || manager.Catalog == null)
        {
            return null;
        }

        return manager.Catalog.Get(key);
    }

    private static string ResolveDisplayName(StoreEntry entry, InventoryEntry meta)
    {
        if (meta != null && string.IsNullOrEmpty(meta.displayName) == false)
        {
            return meta.displayName;
        }

        if (string.IsNullOrEmpty(entry.displayName) == false)
        {
            return entry.displayName;
        }

        return entry.key;
    }

    // MAIN 스토어 보유 수 (널가드)
    private static int OwnedCount(string key)
    {
        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null)
        {
            return 0;
        }

        InvStore store = manager.GetMainStore();
        return store != null ? store.CountOf(key) : 0;
    }

    // ── 베이크된 프리팹 연결 ─────────────────────────────────────────────────────
    private bool HasBakedHierarchy()
    {
        return FindDeepChild(transform, "HeaderBar") != null;
    }

    // 이름 기반 바인딩. 빌드 시 AddListener한 클릭은 프리팹에 직렬화되지 않으므로 여기서 반드시 재배선한다.
    private void BindExisting()
    {
        built = true;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        titleText = FindComponent<TextMeshProUGUI>("TitleText");
        goldText = FindComponent<TextMeshProUGUI>("GoldText");
        toastText = FindComponent<TextMeshProUGUI>("ToastText");
        pageLabel = FindComponent<TextMeshProUGUI>("PageLabel");

        // 베이크 이후 SellPricePercent가 바뀌었어도 판매존 라벨이 상수와 일치하도록 갱신
        TextMeshProUGUI zoneLabel = FindComponent<TextMeshProUGUI>("SellZoneText");
        if (zoneLabel != null)
        {
            zoneLabel.text = $"판매: 인벤토리 아이템을 여기로 드래그 (구매가의 {SellPricePercent}%)";
        }
        pagePrevButton = FindComponent<Button>("PagePrevButton");
        pageNextButton = FindComponent<Button>("PageNextButton");
        // 리롤 도입 전에 베이크된 구버전 프리팹에는 없을 수 있다 — null 허용
        poseRerollButton = FindComponent<Button>("PoseRerollButton");
        goodsGrid = FindComponent<RectTransform>("GoodsGrid");

        for (int i = 0; i < MaxTabSlots; i++)
        {
            tabImages[i] = FindComponent<Image>("TabButton_" + i);
            if (tabImages[i] == null)
            {
                continue;
            }

            tabLabels[i] = FindChildComponent<TextMeshProUGUI>(tabImages[i].transform, "Text");

            Button tabButton = tabImages[i].GetComponent<Button>();
            if (tabButton != null)
            {
                int captured = i;
                tabButton.onClick.RemoveAllListeners();
                tabButton.onClick.AddListener(() => OnTabSlotClicked(captured));
            }
        }

        Transform template = FindDeepChild(transform, "CardTemplate");
        cardTemplate = template != null ? template.gameObject : null;

        // 프리팹에 베이크된 확인 팝업 자식 연결 (없는 구버전 프리팹은 EnsureConfirmView가 프리팹 참조로 보충)
        confirmView = FindComponent<StoreConfirmView>("StoreConfirm");

        // 베이크된 폰트(SUIT-Bold) 캡처 — 런타임 생성 텍스트가 같은 폰트를 쓰도록
        if (titleText != null)
        {
            boundFont = titleText.font;
        }

        // SellZone 컴포넌트 재해결 + 소유자 재연결
        Transform zone = FindDeepChild(transform, "SellZone");
        if (zone != null)
        {
            sellZone = zone.GetComponent<StoreSellZone>();
            if (sellZone == null)
            {
                sellZone = zone.gameObject.AddComponent<StoreSellZone>();
            }

            sellZone.Configure(this);
        }

        BindButton("CloseButton", Hide);
        BindButton("PagePrevButton", OnPagePrevClicked);
        BindButton("PageNextButton", OnPageNextClicked);
        BindButton("PoseRerollButton", OnPoseRerollClicked);
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

    private T FindComponent<T>(string name) where T : Component
    {
        Transform t = FindDeepChild(transform, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static T FindChildComponent<T>(Transform root, string name) where T : Component
    {
        Transform t = FindDeepChild(root, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    // 이름으로 자손 트랜스폼 탐색 (비활성 포함)
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
    // 에디터 베이크 전용: 전체 UI 계층을 코드로 생성해 프리팹에 굽는다.
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

        // 에디트 모드 베이크는 Awake를 거치지 않는다 — 태그 기반 탭 구성을 위해 여기서 정렬
        EnsureCatalog();
        AlignCurrentTab();
        Build();
        Refresh();
    }

    // 확인 팝업 프리팹 주입 (StoreTools가 베이크 순서상 팝업 프리팹을 먼저 굽고 넣는다)
    public void EditorSetConfirmPrefab(GameObject prefab)
    {
        confirmPrefab = prefab;
    }

    // 리롤 버튼 아이콘 주입 (StoreTools가 EditorBuild 전에 호출)
    public void EditorSetRerollSprite(Sprite sprite)
    {
        rerollIconSprite = sprite;
    }

    // 카탈로그 참조 베이크 주입 — 런타임 Resources 폴백을 Awake의 1회로 최소화하기 위한 직렬화 고정
    public void EditorSetCatalog(StoreCatalog registry)
    {
        catalog = registry;
    }
#endif

#if UNITY_EDITOR
    // ── UI 구성 (에디터 베이크 전용 — 런타임은 베이크된 프리팹을 BindExisting으로 연결만 한다) ──
    // 고정 앵커, 레이아웃 그룹은 GoodsGrid만.
    private void Build()
    {
        if (built)
        {
            return;
        }

        built = true;

        // EditorBuild 경로는 Awake를 거치지 않는다 — BuildTabBar가 태그를 읽으려면 필수
        EnsureCatalog();

        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        gameObject.layer = 5; // UI 레이어

        Image rootBg = GetOrAdd<Image>(gameObject);
        ApplyRounded(rootBg, RootBg);
        rootBg.raycastTarget = true;

        canvasGroup = GetOrAdd<CanvasGroup>(gameObject);

        BuildHandler(transform);
        BuildHeaderBar(transform);
        BuildBody(transform);      // Body를 먼저 — TabBar가 뒤 형제로 와야 본문 위에 겹쳐 그려진다 (폴더 감성)
        BuildTabBar(transform);
        BuildCardTemplate(transform);
        BuildSellZone(transform);
        BuildToast(transform);
    }

    // 패널 전체를 덮는 투명 드래그 표면. 첫 자식이라 다른 컨트롤 뒤에 깔리고, 빈 곳을 잡으면 창이 끌린다.
    private void BuildHandler(Transform parent)
    {
        GameObject handler = CreateUIObject("Handler", parent);
        SetStretch(handler, Vector4.zero);
        Image img = handler.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        handler.AddComponent<DragUIHandler>();
    }

    private void BuildHeaderBar(Transform parent)
    {
        GameObject header = CreatePanel("HeaderBar", parent, HeaderBg);
        TopStretch(header, 0f, 0f, 0f, HeaderHeight);

        titleText = CreateText("TitleText", header.transform, "상점", 16, TextWhite, TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(14f, 0f);
        titleRect.offsetMax = new Vector2(-180f, 0f);

        goldText = CreateText("GoldText", header.transform, "0 G", 15, GoldYellow, TextAlignmentOptions.MidlineRight);
        AnchorRight(goldText.gameObject, -44f, 0f, 130f, 26f);

        Button close = CreateButton("CloseButton", header.transform, "X", ButtonBg, 15);
        AnchorRight(close.gameObject, -6f, 0f, 28f, 28f);
        close.onClick.AddListener(Hide);
    }

    // 폴더 탭 바: 본문 상단에 겹쳐 얹히는 컨테이너 (슬롯 6개, 카탈로그 태그 수만큼 사용)
    private void BuildTabBar(Transform parent)
    {
        GameObject tabBar = CreateUIObject("TabBar", parent);
        TopStretch(tabBar, Margin, Margin, TabTop, TabHeight + TabOverlap);

        string[] tabs = ResolveTabs();
        for (int i = 0; i < MaxTabSlots; i++)
        {
            string tabName = i < tabs.Length ? tabs[i] : null;
            string label = tabName ?? string.Empty;

            Button button = CreateButton("TabButton_" + i, tabBar.transform, label, HeaderBg, 13);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(i / (float)MaxTabSlots, 0f);
            rect.anchorMax = new Vector2((i + 1) / (float)MaxTabSlots, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(2f, 0f);
            rect.offsetMax = new Vector2(-2f, -TabUnselectedInset);

            tabImages[i] = button.GetComponent<Image>();
            tabLabels[i] = FindChildComponent<TextMeshProUGUI>(button.transform, "Text");

            // 리스너는 예비 슬롯에도 건다 — 런타임에 카탈로그 태그가 늘어나 RefreshTabVisuals가
            // 슬롯을 켜면 즉시 클릭 가능해야 한다 (핸들러가 슬롯→태그를 매번 재해석하므로 안전)
            int captured = i;
            button.onClick.AddListener(() => OnTabSlotClicked(captured));

            if (tabName == null)
            {
                // 예비 슬롯 — 자리만 확보하고 숨김 (패널 표시·숨김은 CanvasGroup 규칙, 탭 개별은 예외)
                button.gameObject.SetActive(false);
            }
        }
    }

    // Body = 고정 그리드(3x2) + 내부 하단 페이지바. 스크롤 대신 페이징.
    private void BuildBody(Transform parent)
    {
        GameObject body = CreatePanel("Body", parent, PanelBg);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.offsetMin = new Vector2(Margin, BodyBottom);
        bodyRect.offsetMax = new Vector2(-Margin, -BodyTop);

        GameObject grid = CreateUIObject("GoodsGrid", body.transform);
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = Vector2.zero;
        gridRect.anchorMax = Vector2.one;
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.offsetMin = new Vector2(0f, PageBarHeight);
        gridRect.offsetMax = Vector2.zero;

        GridLayoutGroup gridLayout = grid.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(150f, 160f);
        gridLayout.spacing = new Vector2(8f, 8f);
        gridLayout.padding = new RectOffset(8, 8, 8, 8);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        // UpperCenter는 미완성 행(예: 7번째 상품 홀로 있는 페이지)을 가운데로 밀어 1번 자리가 비어 보인다
        gridLayout.childAlignment = TextAnchor.UpperLeft;

        goodsGrid = gridRect;

        // 페이지바 [ < n / m > ] — Body 내부 하단 (InventoryView 푸터 방식)
        GameObject pageBar = CreateUIObject("PageBar", body.transform);
        RectTransform pageBarRect = pageBar.GetComponent<RectTransform>();
        pageBarRect.anchorMin = new Vector2(0f, 0f);
        pageBarRect.anchorMax = new Vector2(1f, 0f);
        pageBarRect.pivot = new Vector2(0.5f, 0f);
        pageBarRect.offsetMin = Vector2.zero;
        pageBarRect.offsetMax = new Vector2(0f, PageBarHeight);

        pagePrevButton = CreateButton("PagePrevButton", pageBar.transform, "<", ButtonBg, 12);
        AnchorCenter(pagePrevButton.gameObject, -70f, 0f, 26f, 18f);
        pagePrevButton.onClick.AddListener(OnPagePrevClicked);

        pageLabel = CreateText("PageLabel", pageBar.transform, "1 / 1", 12, TextMuted, TextAlignmentOptions.Center);
        AnchorCenter(pageLabel.gameObject, 0f, 0f, 100f, 18f);

        pageNextButton = CreateButton("PageNextButton", pageBar.transform, ">", ButtonBg, 12);
        AnchorCenter(pageNextButton.gameObject, 70f, 0f, 26f, 18f);
        pageNextButton.onClick.AddListener(OnPageNextClicked);

        // 포즈 리롤 버튼 — 포즈 탭에서만 노출 (UpdatePoseRerollVisibility가 토글)
        poseRerollButton = CreateButton("PoseRerollButton", pageBar.transform, rerollIconSprite == null ? "R" : "", ButtonBg, 10);
        AnchorCenter(poseRerollButton.gameObject, 235f, 0f, 20f, 20f);
        if (rerollIconSprite != null)
        {
            GameObject rerollIconGo = CreateUIObject("Icon", poseRerollButton.transform);
            Image rerollIconImage = rerollIconGo.AddComponent<Image>();
            rerollIconImage.sprite = rerollIconSprite;
            rerollIconImage.raycastTarget = false;
            SetStretch(rerollIconGo, new Vector4(3f, 3f, 3f, 3f));
        }
        poseRerollButton.onClick.AddListener(OnPoseRerollClicked);
    }

    // 카드 원형 (루트 직속, 비활성, Grid 밖 — ClearGrid에 안전). 전체가 Button(확인 팝업 열기).
    private void BuildCardTemplate(Transform parent)
    {
        GameObject card = CreatePanel("CardTemplate", parent, ButtonBg);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(150f, 160f);

        Button button = card.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();

        GameObject iconGo = CreateUIObject("CardIcon", card.transform);
        TopStretch(iconGo, 20f, 20f, 6f, 70f);
        Image icon = iconGo.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI nameLabel = CreateText("CardName", card.transform, "이름", 13, TextWhite, TextAlignmentOptions.Center);
        TopStretch(nameLabel.gameObject, 6f, 6f, 80f, 20f);

        TextMeshProUGUI price = CreateText("CardPrice", card.transform, "100 G", 12, GoldYellow, TextAlignmentOptions.Center);
        TopStretch(price.gameObject, 6f, 6f, 102f, 17f);

        TextMeshProUGUI sub = CreateText("CardSub", card.transform, string.Empty, 11, AccentBlueHi, TextAlignmentOptions.Center);
        TopStretch(sub.gameObject, 6f, 6f, 120f, 14f);

        TextMeshProUGUI owned = CreateText("CardOwned", card.transform, "보유 0", 11, TextMuted, TextAlignmentOptions.Center);
        TopStretch(owned.gameObject, 6f, 6f, 136f, 15f);

        card.SetActive(false);
        cardTemplate = card;
    }

    // 판매 존 (하단 고정 스트립). 드롭 판정은 StoreSellZone(IDropHandler)이 담당한다.
    private void BuildSellZone(Transform parent)
    {
        GameObject zone = CreateUIObject("SellZone", parent);
        RectTransform zoneRect = zone.GetComponent<RectTransform>();
        zoneRect.anchorMin = new Vector2(0f, 0f);
        zoneRect.anchorMax = new Vector2(1f, 0f);
        zoneRect.pivot = new Vector2(0.5f, 0f);
        zoneRect.offsetMin = Vector2.zero;
        zoneRect.offsetMax = new Vector2(0f, SellZoneHeight);

        Image zoneImg = zone.AddComponent<Image>();
        ApplyRounded(zoneImg, Track);
        zoneImg.raycastTarget = true;

        sellZone = zone.AddComponent<StoreSellZone>();
        sellZone.Configure(this);

        // SellPricePercent 상수와 자동 동기화 (BindExisting에서도 같은 문자열로 갱신)
        TextMeshProUGUI zoneText = CreateText("SellZoneText", zone.transform, $"판매: 인벤토리 아이템을 여기로 드래그 (구매가의 {SellPricePercent}%)", 13, TextMuted, TextAlignmentOptions.Center);
        SetStretch(zoneText.gameObject, new Vector4(12f, 8f, 12f, 8f));
    }

    // 토스트 (SellZone 위 고정앵커, 기본 빈 문자열)
    private void BuildToast(Transform parent)
    {
        toastText = CreateText("ToastText", parent, string.Empty, 13, TextWhite, TextAlignmentOptions.Center);
        RectTransform toastRect = toastText.rectTransform;
        toastRect.anchorMin = new Vector2(0f, 0f);
        toastRect.anchorMax = new Vector2(1f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.offsetMin = new Vector2(Margin, SellZoneHeight + 4f);
        toastRect.offsetMax = new Vector2(-Margin, SellZoneHeight + 28f);
    }

    // ── 팩토리 헬퍼 (JukeboxDownloaderView/SkillView와 동일 관용구) ─────────────
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

        TextMeshProUGUI text = CreateText("Text", root.transform, label, fontSize, TextWhite, TextAlignmentOptions.Center);
        SetStretch(text.gameObject, Vector4.zero);
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

    private static void SetStretch(GameObject go, Vector4 padding)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding.x, padding.y);
        rect.offsetMax = new Vector2(-padding.z, -padding.w);
    }

    // 상단 기준 가로 스트레치 바: 패널 top에서 top만큼 내려온 위치에 height 높이로 배치.
    private static RectTransform TopStretch(GameObject go, float left, float right, float top, float height)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -(top + height));
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    // 중앙 기준 고정 크기 배치 (페이지바 버튼/라벨)
    private static void AnchorCenter(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
    }

    // 우측 중앙 기준 고정 크기 배치 (헤더의 골드 라벨/닫기 버튼)
    private static void AnchorRight(GameObject go, float x, float y, float w, float h)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
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

    private static void SetText(TextMeshProUGUI t, string v)
    {
        if (t != null)
        {
            t.text = v;
        }
    }
}
