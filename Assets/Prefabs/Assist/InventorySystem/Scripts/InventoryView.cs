using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인벤토리 창 섹션 종류 (창 1개 = 스토어 1개)
public enum InventorySection
{
    Main,   // 유저 공용(MAIN) 스토어
    Char    // 활성 캐릭터 스토어
}

// InventorySystem UI 창. 창 1개가 스토어 1개(MAIN 또는 활성 캐릭터)를 표시한다.
// - 베이크된 프리팹(InventoryPanel.prefab) 전용: 모든 UI 참조는 프리팹에 직렬화한다 (런타임 이름 탐색/자가 구축 없음)
// - 표시·숨김은 반드시 CanvasGroup(alpha/interactable/blocksRaycasts)만 조작한다 (SetActive 금지)
// - 그리드: 8열 x 6행 = 48칸 고정 (빈 칸 포함), 푸터의 < > 로 페이지 이동
// - 드래그 앤 드롭: 칸 = 위치 이동/스왑/병합, 반대 섹션 창 = 이동, 캐릭터(3D) = 이동 + 장착
// - 우클릭 = 컨텍스트 메뉴 (상세 | 장착/해제), 헤더 버튼 = 정렬(종류→이름)/닫기
public class InventoryView : MonoBehaviour
{
    // ── 그리드 규격 ──────────────────────────────────────────────
    private const int Columns = 8;                       // 가로 칸 수
    private const int Rows = 6;                          // 세로 칸 수
    private const int PageSize = Columns * Rows;         // 페이지당 칸 수 (48)

    // ── 직렬화 참조 (베이크된 프리팹에서 연결됨) ─────────────────
    [SerializeField] private InventorySection section = InventorySection.Main;  // 이 창이 표시하는 스토어
    [SerializeField] private CanvasGroup canvasGroup;         // 표시/숨김 제어
    [SerializeField] private TMP_Text headerText;             // 창 타이틀
    [SerializeField] private Transform grid;                  // 슬롯 그리드 (8열 고정)
    [SerializeField] private InventorySlotView slotTemplate;  // 비활성 셀 템플릿
    [SerializeField] private Button sortButton;               // 헤더 정렬 버튼
    [SerializeField] private Button closeButton;              // 헤더 닫기 버튼
    [SerializeField] private Button prevButton;               // 푸터 이전 페이지 버튼
    [SerializeField] private Button nextButton;               // 푸터 다음 페이지 버튼
    [SerializeField] private TMP_Text pageLabel;              // 푸터 페이지 표시 ("1 / 1")
    [SerializeField] private GameObject mainCurrencyArea;     // MAIN 전용: +100 디버그 + 현재 골드
    [SerializeField] private Button debugGoldButton;
    [SerializeField] private TMP_Text goldBalanceText;

    private int currentPage;  // 현재 페이지 (0부터)
    private CurrencyManager subscribedWallet;

    public InventorySection Section
    {
        get
        {
            return section;
        }
    }

    // 섹션 지정 (데모씬 빌더 등 외부에서 인스턴스별 오버라이드).
    // 이미 활성 상태에서 바뀌면 즉시 다시 그린다 — 활성화 후 섹션 지정 시 구 섹션 잔상 방지
    public void ConfigureSection(InventorySection newSection)
    {
        if (section == newSection)
        {
            return;
        }

        section = newSection;

        // 에디터 빌더 경로에서는 다시 그리지 않는다 (셀 인스턴스가 씬에 직렬화되는 사고 방지)
        if (Application.isPlaying && isActiveAndEnabled)
        {
            Rebuild();
        }
    }

    // 이 창의 스토어 ownerId ("MAIN" 또는 활성 charcode. 활성 캐릭터 없으면 null)
    public string OwnerId()
    {
        if (section == InventorySection.Main)
        {
            return InventorySystemManager.MainOwnerId;
        }

        InventorySystemManager manager = InventorySystemManager.Instance;
        return manager != null ? manager.ActiveCharcode : null;
    }

    // 베이크된 직렬화 참조에 버튼 동작만 연결한다.
    private void Awake()
    {
        if (slotTemplate == null)
        {
            // 런타임 코드 조립은 하지 않는다 — UI는 베이크된 프리팹이 완결 상태여야 한다.
            // 참조가 비면 이후 로직은 전부 null 가드로 무동작한다.
            Debug.LogError("[InventorySystem][InventoryView] 베이크된 UI 계층이 없습니다. InventoryPanel.prefab을 사용하세요.");
        }

        // 버튼은 런타임 리스너로 배선 (베이크 프리팹에는 퍼시스턴트 리스너가 없음)
        if (sortButton != null)
        {
            sortButton.onClick.AddListener(OnSortClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }

        if (prevButton != null)
        {
            prevButton.onClick.AddListener(OnPrevPageClicked);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextPageClicked);
        }

        if (debugGoldButton != null)
        {
            debugGoldButton.onClick.AddListener(OnDebugGoldClicked);
        }
    }

    // 이벤트 구독 + 최초 그리드 구성
    private void OnEnable()
    {
        InventoryEvents.OnStoreChanged += HandleStoreChanged;
        InventoryEvents.OnActiveOwnerChanged += HandleActiveOwnerChanged;

        if (Application.isPlaying)
        {
            subscribedWallet = CurrencyManager.Instance;
            if (subscribedWallet != null)
            {
                subscribedWallet.CurrencyChanged += HandleCurrencyChanged;
            }
        }

        Rebuild();
    }

    // 이벤트 해제 (구독과 짝 맞춤)
    private void OnDisable()
    {
        InventoryEvents.OnStoreChanged -= HandleStoreChanged;
        InventoryEvents.OnActiveOwnerChanged -= HandleActiveOwnerChanged;

        if (subscribedWallet != null)
        {
            subscribedWallet.CurrencyChanged -= HandleCurrencyChanged;
            subscribedWallet = null;
        }
    }

    // 스토어 변경 → 내 스토어일 때만 갱신 (장착 토글 하이라이트 포함)
    private void HandleStoreChanged(string ownerId)
    {
        if (ownerId == OwnerId())
        {
            Rebuild();
        }
    }

    // 활성 캐릭터 변경 → 갱신 (CHAR 창 내용 + 타이틀)
    private void HandleActiveOwnerChanged(string charcode)
    {
        currentPage = 0;
        Rebuild();
    }

    private void HandleCurrencyChanged(string currencyKey)
    {
        if (currencyKey == CurrencyManager.GoldKey)
        {
            RefreshGoldBalance();
        }
    }

    // ── 표시/숨김 (CanvasGroup만 조작) ───────────────────────────

    // 현재 표시 상태 (UIManager 등 외부의 토글 판정용)
    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.5f;

    public static bool IsSectionVisible(InventorySection targetSection)
    {
        InventoryView[] views = Resources.FindObjectsOfTypeAll<InventoryView>();
        for (int i = 0; i < views.Length; i++)
        {
            InventoryView view = views[i];
            if (view == null || view.gameObject.scene.IsValid() == false)
            {
                continue;
            }

            if (view.section == targetSection && view.gameObject.activeInHierarchy && view.IsVisible)
            {
                return true;
            }
        }

        return false;
    }

    // 패널 표시
    public void Show()
    {
        if (canvasGroup == null)
        {
            return;
        }

        TranslateBakedLabels();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        Rebuild();
    }

    // 패널 숨김
    public void Hide()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    // 패널 토글
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

    // ── 그리드 재구성 (48칸 고정 + 페이지) ───────────────────────

    // 타이틀 + 페이지 라벨 + 48칸(빈 칸 포함)을 이 창의 스토어 내용으로 다시 채운다
    public void Rebuild()
    {
        // 셀이 파괴되면 PointerExit이 오지 않으므로 여기서 툴팁을 정리한다
        InventoryTooltip.Hide();

        ClearGrid(grid);

        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null || slotTemplate == null || grid == null)
        {
            return;
        }

        bool isMain = section == InventorySection.Main;
        if (mainCurrencyArea != null)
        {
            mainCurrencyArea.SetActive(isMain);
        }

        // 타이틀 갱신
        if (headerText != null)
        {
            RectTransform titleRect = headerText.rectTransform;
            if (isMain)
            {
                headerText.text = TranslateUi("INVENTORY");
                headerText.alignment = TextAlignmentOptions.MidlineLeft;
                titleRect.offsetMin = new Vector2(40f, 0f);
                titleRect.offsetMax = new Vector2(-310f, 0f);
            }
            else
            {
                string charcode = manager.ActiveCharcode;
                headerText.text = string.IsNullOrEmpty(charcode)
                    ? TranslateUi("INVENTORY")
                    : string.Format(TranslateUi("INVENTORY ({0})"), charcode);
                headerText.alignment = TextAlignmentOptions.Center;
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;
            }
        }

        RefreshGoldBalance();

        // 페이지 계산 (스토어가 없어도 빈 1페이지는 그린다)
        InvStore store = section == InventorySection.Main ? manager.GetMainStore() : manager.GetActiveCharStore();
        int totalPages = GetTotalPages(store);
        currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

        if (pageLabel != null)
        {
            pageLabel.text = $"{currentPage + 1} / {totalPages}";
        }

        // 48칸 고정 스폰 (아이템 없는 칸은 빈 칸으로)
        for (int i = 0; i < PageSize; i++)
        {
            int slotIndex = currentPage * PageSize + i;
            InvItemStack stack = store != null ? store.FindBySlot(slotIndex) : null;

            InventorySlotView cell = Instantiate(slotTemplate, grid);
            cell.gameObject.SetActive(true);

            if (stack == null)
            {
                cell.gameObject.name = "Cell_" + slotIndex;
                cell.Setup(this, slotIndex, null, 0, null, false);
            }
            else
            {
                cell.gameObject.name = "Cell_" + slotIndex + "_" + stack.key;
                ItemEntry meta = manager.Catalog != null ? manager.Catalog.Get(stack.key) : null;
                bool equipped = section == InventorySection.Char && manager.IsEquippedOnActive(stack.key);
                cell.Setup(this, slotIndex, stack.key, stack.count, meta, equipped);
            }
        }
    }

    // 스토어의 총 페이지 수 (가장 뒤 칸 기준, 최소 1)
    private static int GetTotalPages(InvStore store)
    {
        if (store == null || store.stacks == null || store.stacks.Count == 0)
        {
            return 1;
        }

        int maxSlot = 0;
        foreach (InvItemStack stack in store.stacks)
        {
            if (stack != null && stack.slot > maxSlot)
            {
                maxSlot = stack.slot;
            }
        }

        return maxSlot / PageSize + 1;
    }

    // 그리드의 기존 셀 전부 제거 (템플릿은 그리드 밖에 있어 안전)
    private void ClearGrid(Transform targetGrid)
    {
        if (targetGrid == null)
        {
            return;
        }

        for (int i = targetGrid.childCount - 1; i >= 0; i--)
        {
            // Destroy는 프레임 말 지연 파괴라 레이아웃에 한 프레임 남는다 —
            // 먼저 그리드에서 분리해 레이아웃/렌더에서 즉시 제외한 뒤 파괴한다.
            Transform child = targetGrid.GetChild(i);
            child.SetParent(null, false);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    // ── 페이지 이동 ──────────────────────────────────────────────

    // 이전 페이지
    private void OnPrevPageClicked()
    {
        currentPage = currentPage - 1;
        Rebuild(); // 범위는 Rebuild에서 클램프
    }

    // 다음 페이지
    private void OnNextPageClicked()
    {
        currentPage = currentPage + 1;
        Rebuild();
    }

    // 장착 불가 캐릭터 사전 차단 — 차단 시 캐릭터 안내 대사 1회 (판정은 매니저의 주입 resolver 경유)
    // 소유 이동(MoveMainToChar 등)은 막지 않고 장착으로 이어지는 동작만 이 게이트를 거친다.
    private bool BlockEquipIfUnsupported()
    {
        if (InventorySystemManager.Instance == null || InventorySystemManager.Instance.CanEquipOnActive())
        {
            return false;
        }

        if (ScenarioCommonManager.Instance != null)
        {
            StartCoroutine(ScenarioCommonManager.Instance.Run_C90_equip_unsupported());
        }
        return true;
    }

    // ── 슬롯 클릭 위임 (InventorySlotView가 호출) ────────────────

    // 좌클릭: MAIN = 활성 캐릭터로 1개 이동 / CHAR = 장착 가능하면 장착·해제 토글
    public void OnSlotLeftClicked(string key)
    {
        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null)
        {
            return;
        }

        ItemEntry clickedItem = manager.Catalog != null ? manager.Catalog.Get(key) : null;
        if (clickedItem != null && clickedItem.useType == ItemUseType.Anchor)
        {
            return;
        }

        if (section == InventorySection.Main && clickedItem != null && clickedItem.isMainOnly)
        {
            return;
        }

        if (section == InventorySection.Main)
        {
            if (IsSectionVisible(InventorySection.Char) == false)
            {
                return;
            }

            if (string.IsNullOrEmpty(manager.ActiveCharcode))
            {
                Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 이동할 수 없습니다.");
                return;
            }

            manager.MoveMainToChar(manager.ActiveCharcode, key, 1);
        }
        else
        {
            if (manager.IsEquippable(key))
            {
                // 장착 시도(미장착 상태)만 사전 차단 — 해제 토글은 허용
                if (manager.IsEquippedOnActive(key) == false && BlockEquipIfUnsupported())
                {
                    return;
                }
                manager.ToggleEquip(key);
            }
            else
            {
                Debug.Log($"[InventoryView] 장착 불가 아이템: {key}");
            }
        }
    }

    // hover 진입: 미니 툴팁 (상세의 축소판 — 이름 + 수량·분류 + 짧은 설명)
    public void OnSlotHoverEnter(string key, int slotIndex, Vector2 screenPos)
    {
        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null)
        {
            return;
        }

        ItemEntry meta = manager.Catalog != null ? manager.Catalog.Get(key) : null;
        InvStore store = section == InventorySection.Main ? manager.GetMainStore() : manager.GetActiveCharStore();
        InvItemStack stack = store != null ? store.FindBySlot(slotIndex) : null;
        int count = stack != null ? stack.count : 0;

        string title = TranslateUi(meta != null && string.IsNullOrEmpty(meta.displayName) == false ? meta.displayName : key);

        string body = string.Format(TranslateUi("수량 {0}"), count);
        string category = manager.Catalog != null ? manager.Catalog.CategoryForKey(key) : null;
        if (string.IsNullOrEmpty(category) == false)
        {
            body += " · " + TranslateUi(category);
        }

        if (meta != null && string.IsNullOrEmpty(meta.description) == false)
        {
            body += "\n" + TranslateUi(meta.description);
        }

        InventoryTooltip.Show(RootCanvas(), screenPos, title, body, MenuFont());
    }

    // 우클릭: 컨텍스트 메뉴 (상세 | 장착/해제 | 이동)
    public void OnSlotRightClicked(string key, int slotIndex, Vector2 screenPos)
    {
        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null)
        {
            return;
        }

        InventoryTooltip.Hide();  // 메뉴가 열리는 동안 툴팁 정리

        ItemEntry meta = manager.Catalog != null ? manager.Catalog.Get(key) : null;

        List<InventoryMenuEntry> entries = new List<InventoryMenuEntry>();

        // 1) 상세
        entries.Add(new InventoryMenuEntry
        {
            label = TranslateUi("상세"),
            action = () => ShowDetail(manager, key, slotIndex, screenPos, meta)
        });

        // 2) Anchor 배치
        if (meta != null && meta.useType == ItemUseType.Anchor)
        {
            entries.Add(new InventoryMenuEntry
            {
                label = TranslateUi("배치"),
                action = () => AnchorManager.Instance.TryPlaceAtDefault(
                    OwnerId(),
                    slotIndex,
                    key)
            });
        }

        // 3) 장착/해제 (Equip 타입이면서 장착 가능한 아이템만)
        if (manager.IsEquippable(key) && (section != InventorySection.Main || meta == null || meta.isMainOnly == false))
        {
            if (section == InventorySection.Char)
            {
                bool equipped = manager.IsEquippedOnActive(key);
                entries.Add(new InventoryMenuEntry
                {
                    label = TranslateUi(equipped ? "해제" : "장착"),
                    action = () =>
                    {
                        // 장착 시도만 사전 차단 — 해제는 허용
                        if (equipped == false && BlockEquipIfUnsupported())
                        {
                            return;
                        }
                        manager.ToggleEquip(key);
                    }
                });
            }
            else
            {
                // MAIN에서 장착 = 캐릭터로 1개 이동 후 장착
                entries.Add(new InventoryMenuEntry
                {
                    label = TranslateUi("장착"),
                    action = () =>
                    {
                        string charcode = manager.ActiveCharcode;
                        if (string.IsNullOrEmpty(charcode))
                        {
                            Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 장착할 수 없습니다.");
                            return;
                        }

                        // 장착 불가 캐릭터는 이동 전에 차단 — 아이템이 조용히 CHAR 스토어로 옮겨지는 것 방지
                        if (BlockEquipIfUnsupported())
                        {
                            return;
                        }

                        if (manager.MoveMainToChar(charcode, key, 1))
                        {
                            manager.EquipKey(key);
                        }
                    }
                });
            }
        }

        // 4) 이동 (스택 통째: MAIN → CHAR / CHAR → MAIN, 목적지 빈 칸 자동 배치)
        if (section == InventorySection.Main && (meta == null || meta.isMainOnly == false))
        {
            entries.Add(new InventoryMenuEntry
            {
                label = TranslateUi("CHAR로 이동"),
                action = () =>
                {
                    string charcode = manager.ActiveCharcode;
                    if (string.IsNullOrEmpty(charcode))
                    {
                        Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 이동할 수 없습니다.");
                        return;
                    }

                    MoveWithAmountPrompt(manager, InventorySystemManager.MainOwnerId, slotIndex, charcode, -1,
                        string.Format(TranslateUi("'{0}' 이동 수량"), key));
                }
            });
        }
        else
        {
            entries.Add(new InventoryMenuEntry
            {
                label = TranslateUi("MAIN으로 이동"),
                action = () =>
                {
                    string charcode = manager.ActiveCharcode;
                    if (string.IsNullOrEmpty(charcode))
                    {
                        return;
                    }

                    MoveWithAmountPrompt(manager, charcode, slotIndex, InventorySystemManager.MainOwnerId, -1,
                        string.Format(TranslateUi("'{0}' 이동 수량"), key));
                }
            });
        }

        InventoryMenu.Show(RootCanvas(), screenPos, entries, MenuFont());
    }

    // 크로스 스토어 이동 공통: 1개면 즉시 이동, 여러 개면 수량 선택 모달(기본값 전량) 후 확정 수량만 이동.
    // 모달이 뜬 사이 스택이 변해도 MoveStackAmount가 현재 수량으로 클램프한다.
    private void MoveWithAmountPrompt(InventorySystemManager manager, string fromOwner, int fromSlot, string toOwner, int toSlot, string title)
    {
        InvStore store;
        if (fromOwner == InventorySystemManager.MainOwnerId)
        {
            store = manager.GetMainStore();
        }
        else
        {
            store = manager.GetCharStore(fromOwner);
        }
        if (store == null)
        {
            return;
        }

        InvItemStack stack = store.FindBySlot(fromSlot);
        if (stack == null)
        {
            return;
        }

        if (stack.count <= 1)
        {
            manager.MoveStack(fromOwner, fromSlot, toOwner, toSlot);
            return;
        }

        InventoryConfirmView.Show(RootCanvas(), title, stack.count, MenuFont(), n =>
        {
            manager.MoveStackAmount(fromOwner, fromSlot, toOwner, toSlot, n);
        });
    }

    // 상세 팝업 열기 (이름/설명/수량/분류)
    private void ShowDetail(InventorySystemManager manager, string key, int slotIndex, Vector2 screenPos, ItemEntry meta)
    {
        InvStore store = section == InventorySection.Main ? manager.GetMainStore() : manager.GetActiveCharStore();
        InvItemStack stack = store != null ? store.FindBySlot(slotIndex) : null;
        int count = stack != null ? stack.count : 0;

        string title = TranslateUi(meta != null && string.IsNullOrEmpty(meta.displayName) == false ? meta.displayName : key);
        string body = "";
        if (meta != null && string.IsNullOrEmpty(meta.description) == false)
        {
            body += TranslateUi(meta.description) + "\n\n";
        }

        body += string.Format(TranslateUi("수량: {0}"), count);
        string category = manager.Catalog != null ? manager.Catalog.CategoryForKey(key) : null;
        if (string.IsNullOrEmpty(category) == false)
        {
            body += "\n" + string.Format(TranslateUi("분류: {0}"), TranslateUi(category));
        }

        body += "\n" + string.Format(TranslateUi("키: {0}"), key);

        InventoryMenu.ShowDetail(RootCanvas(), screenPos, title, body, MenuFont());
    }

    // ── 드래그 앤 드롭 처리 (InventorySlotView.OnEndDrag가 호출) ─

    // 드롭 해석: 셀 = 칸 단위 이동/스왑/병합 · 창 = 빈 칸 자동 배치 · UI 밖 + 캐릭터 위 = 이동 + 장착
    public void HandleSlotDrop(int fromSlot, string key, InventorySlotView targetCell, InventoryView targetView, Vector2 screenPos)
    {
        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null)
        {
            return;
        }

        string fromOwner = OwnerId();
        if (string.IsNullOrEmpty(fromOwner))
        {
            return;
        }

        // 1) 셀 위에 드롭 → 그 칸으로 정밀 배치 (같은 스토어 = 이동/스왑/병합, 다른 스토어 = 통째 이동)
        if (targetCell != null && targetCell.Owner != null)
        {
            string toOwner = targetCell.Owner.OwnerId();
            if (string.IsNullOrEmpty(toOwner))
            {
                Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 이동할 수 없습니다.");
                return;
            }

            if (toOwner == fromOwner)
            {
                // 같은 스토어 = 배치(이동/스왑/병합) — 수량 질의 없음
                manager.MoveStack(fromOwner, fromSlot, toOwner, targetCell.SlotIndex);
            }
            else
            {
                MoveWithAmountPrompt(manager, fromOwner, fromSlot, toOwner, targetCell.SlotIndex, $"'{key}' 이동 수량");
            }
            return;
        }

        // 2) 창(헤더/여백 등) 위에 드롭 → 그 창 스토어의 빈 칸에 자동 배치
        if (targetView != null)
        {
            if (targetView == this)
            {
                return;
            }

            string toOwner = targetView.OwnerId();
            if (string.IsNullOrEmpty(toOwner))
            {
                Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 이동할 수 없습니다.");
                return;
            }

            MoveWithAmountPrompt(manager, fromOwner, fromSlot, toOwner, -1, $"'{key}' 이동 수량");
            return;
        }

        ItemEntry worldItem = manager.Catalog != null ? manager.Catalog.Get(key) : null;
        if (worldItem == null)
        {
            return;
        }

        // Anchor와 Equip은 서로의 대상을 절대 처리하지 않는다.
        if (worldItem.useType == ItemUseType.Anchor)
        {
            AnchorManager.Instance.TryPlaceAtScreenPosition(
                fromOwner,
                fromSlot,
                key,
                screenPos);
            return;
        }

        if (worldItem.useType != ItemUseType.Equip || manager.IsEquippable(key) == false)
        {
            return;
        }

        // Equip 아이템이 Anchor 대상에 닿으면 캐릭터 이동/장착으로 폴백하지 않는다.
        if (AnchorManager.Instance.IsPointerOverAnchor(screenPos))
        {
            return;
        }

        // 3) UI 밖 Equip 드롭 → 캐릭터(3D) 위인지 스크린 바운드로 판정
        if (IsPointerOverCharacter(manager, screenPos) == false)
        {
            return;
        }

        // 장착 불가 캐릭터는 이동 전에 차단 — 아이템이 조용히 CHAR 스토어로 옮겨지는 것 방지 + 안내 대사
        if (BlockEquipIfUnsupported())
        {
            return;
        }

        if (section == InventorySection.Main)
        {
            // MAIN → 캐릭터: 장착 의도가 명확하므로 1개만 이동 + (장착 가능하면) 즉시 장착 (수량 질의 없음)
            string charcode = manager.ActiveCharcode;
            if (string.IsNullOrEmpty(charcode))
            {
                Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 이동할 수 없습니다.");
                return;
            }

            if (manager.MoveMainToChar(charcode, key, 1))
            {
                if (manager.IsEquippable(key))
                {
                    manager.EquipKey(key);
                }
            }
        }
        else
        {
            // CHAR → 캐릭터: 이미 소유 중이므로 장착만
            if (manager.IsEquippable(key))
            {
                manager.EquipKey(key);
            }
        }
    }

    // 활성 캐릭터의 렌더러 바운드를 화면에 투영해 포인터 포함 여부 판정 (콜라이더 불필요)
    private static bool IsPointerOverCharacter(InventorySystemManager manager, Vector2 screenPos)
    {
        GameObject target = manager.ActiveTarget;
        if (target == null)
        {
            return false;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return false;
        }

        Renderer[] rs = target.GetComponentsInChildren<Renderer>();
        if (rs == null || rs.Length == 0)
        {
            return false;
        }

        // 전체 바운드 합치기
        bool has = false;
        Bounds b = new Bounds();
        foreach (Renderer r in rs)
        {
            if (r == null)
            {
                continue;
            }

            if (has == false)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        if (has == false)
        {
            return false;
        }

        // 바운드 8꼭짓점을 스크린에 투영해 사각 영역 계산
        Vector2 smin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 smax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;

        for (int xi = 0; xi < 2; xi++)
        {
            for (int yi = 0; yi < 2; yi++)
            {
                for (int zi = 0; zi < 2; zi++)
                {
                    Vector3 corner = new Vector3(
                        xi == 0 ? b.min.x : b.max.x,
                        yi == 0 ? b.min.y : b.max.y,
                        zi == 0 ? b.min.z : b.max.z);

                    Vector3 sp = cam.WorldToScreenPoint(corner);
                    if (sp.z <= 0f)
                    {
                        continue; // 카메라 뒤
                    }

                    any = true;
                    smin = Vector2.Min(smin, new Vector2(sp.x, sp.y));
                    smax = Vector2.Max(smax, new Vector2(sp.x, sp.y));
                }
            }
        }

        if (any == false)
        {
            return false;
        }

        return screenPos.x >= smin.x && screenPos.x <= smax.x
            && screenPos.y >= smin.y && screenPos.y <= smax.y;
    }

    // 정렬 버튼: 이 창의 스토어를 종류→이름 순으로 정렬 + 1페이지부터 재배치 (결과는 저장됨)
    private void OnSortClicked()
    {
        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null)
        {
            return;
        }

        string ownerId = OwnerId();
        if (string.IsNullOrEmpty(ownerId))
        {
            return;
        }

        currentPage = 0;
        manager.SortStore(ownerId);
    }

    private void OnDebugGoldClicked()
    {
        if (section != InventorySection.Main)
        {
            return;
        }

        CurrencyManager wallet = CurrencyManager.Instance;
        if (wallet != null)
        {
            wallet.Earn(CurrencyManager.GoldKey, 100);
        }
    }

    private void RefreshGoldBalance()
    {
        if (goldBalanceText == null)
        {
            return;
        }

        CurrencyManager wallet = CurrencyManager.Instance;
        int gold = wallet != null ? wallet.Gold : 0;
        goldBalanceText.text = $"{gold:N0} G";
    }

    // 이 창이 속한 최상위 캔버스
    private Canvas RootCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas : null;
    }

    // 컨텍스트 메뉴용 한글 지원 폰트 (베이크된 헤더의 SUIT-Bold를 그대로 사용)
    private TMP_FontAsset MenuFont()
    {
        return headerText != null ? headerText.font : null;
    }

    private void TranslateBakedLabels()
    {
        foreach (TMP_Text target in GetComponentsInChildren<TMP_Text>(true))
        {
            if (target != null && !string.IsNullOrEmpty(target.text))
            {
                target.text = TranslateUi(target.text);
            }
        }
    }

    private static string TranslateUi(string text)
    {
        if (string.IsNullOrEmpty(text) || SettingManager.Instance == null ||
            SettingManager.Instance.settings == null ||
            string.IsNullOrEmpty(SettingManager.Instance.settings.ui_language))
        {
            return text;
        }

        return LanguageDataInventory.Translate(text, SettingManager.Instance.settings.ui_language);
    }
}
