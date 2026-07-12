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
// - 베이크된 프리팹(InventoryPanel.prefab) 전용: BindExisting으로 참조만 연결한다 (런타임 자가 구축 없음)
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

    private int currentPage;  // 현재 페이지 (0부터)

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

    // 참조가 비어 있으면 이름 기반 바인딩 → 버튼 배선 (베이크된 프리팹 전용)
    private void Awake()
    {
        BindExisting();

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
    }

    // 이벤트 구독 + 최초 그리드 구성
    private void OnEnable()
    {
        InventoryEvents.OnStoreChanged += HandleStoreChanged;
        InventoryEvents.OnActiveOwnerChanged += HandleActiveOwnerChanged;
        Rebuild();
    }

    // 이벤트 해제 (구독과 짝 맞춤)
    private void OnDisable()
    {
        InventoryEvents.OnStoreChanged -= HandleStoreChanged;
        InventoryEvents.OnActiveOwnerChanged -= HandleActiveOwnerChanged;
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

    // ── 표시/숨김 (CanvasGroup만 조작) ───────────────────────────

    // 현재 표시 상태 (UIManager 등 외부의 토글 판정용)
    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.5f;

    // 패널 표시
    public void Show()
    {
        if (canvasGroup == null)
        {
            return;
        }

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

        // 타이틀 갱신
        if (headerText != null)
        {
            if (section == InventorySection.Main)
            {
                headerText.text = "INVENTORY - MAIN";
            }
            else
            {
                string charcode = manager.ActiveCharcode;
                headerText.text = "INVENTORY - CHAR" + (string.IsNullOrEmpty(charcode) ? "" : $" ({charcode})");
            }
        }

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
                InventoryEntry meta = manager.Catalog != null ? manager.Catalog.Get(stack.key) : null;
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

    // ── 슬롯 클릭 위임 (InventorySlotView가 호출) ────────────────

    // 좌클릭: MAIN = 활성 캐릭터로 1개 이동 / CHAR = 장착 가능하면 장착·해제 토글
    public void OnSlotLeftClicked(string key)
    {
        InventorySystemManager manager = InventorySystemManager.Instance;
        if (manager == null)
        {
            return;
        }

        if (section == InventorySection.Main)
        {
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

        InventoryEntry meta = manager.Catalog != null ? manager.Catalog.Get(key) : null;
        InvStore store = section == InventorySection.Main ? manager.GetMainStore() : manager.GetActiveCharStore();
        InvItemStack stack = store != null ? store.FindBySlot(slotIndex) : null;
        int count = stack != null ? stack.count : 0;

        string title = meta != null && string.IsNullOrEmpty(meta.displayName) == false ? meta.displayName : key;

        string body = $"수량 {count}";
        if (meta != null && string.IsNullOrEmpty(meta.category) == false)
        {
            body += $" · {meta.category}";
        }

        if (meta != null && string.IsNullOrEmpty(meta.description) == false)
        {
            body += "\n" + meta.description;
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

        InventoryEntry meta = manager.Catalog != null ? manager.Catalog.Get(key) : null;

        List<InventoryMenuEntry> entries = new List<InventoryMenuEntry>();

        // 1) 상세
        entries.Add(new InventoryMenuEntry
        {
            label = "상세",
            action = () => ShowDetail(manager, key, slotIndex, screenPos, meta)
        });

        // 2) 장착/해제 (장착 가능한 아이템만)
        if (manager.IsEquippable(key))
        {
            if (section == InventorySection.Char)
            {
                bool equipped = manager.IsEquippedOnActive(key);
                entries.Add(new InventoryMenuEntry
                {
                    label = equipped ? "해제" : "장착",
                    action = () => manager.ToggleEquip(key)
                });
            }
            else
            {
                // MAIN에서 장착 = 캐릭터로 1개 이동 후 장착
                entries.Add(new InventoryMenuEntry
                {
                    label = "장착",
                    action = () =>
                    {
                        string charcode = manager.ActiveCharcode;
                        if (string.IsNullOrEmpty(charcode))
                        {
                            Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 장착할 수 없습니다.");
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

        // 3) 이동 (스택 통째: MAIN → CHAR / CHAR → MAIN, 목적지 빈 칸 자동 배치)
        if (section == InventorySection.Main)
        {
            entries.Add(new InventoryMenuEntry
            {
                label = "CHAR로 이동",
                action = () =>
                {
                    string charcode = manager.ActiveCharcode;
                    if (string.IsNullOrEmpty(charcode))
                    {
                        Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 이동할 수 없습니다.");
                        return;
                    }

                    manager.MoveStack(InventorySystemManager.MainOwnerId, slotIndex, charcode, -1);
                }
            });
        }
        else
        {
            entries.Add(new InventoryMenuEntry
            {
                label = "MAIN으로 이동",
                action = () =>
                {
                    string charcode = manager.ActiveCharcode;
                    if (string.IsNullOrEmpty(charcode))
                    {
                        return;
                    }

                    manager.MoveStack(charcode, slotIndex, InventorySystemManager.MainOwnerId, -1);
                }
            });
        }

        InventoryMenu.Show(RootCanvas(), screenPos, entries, MenuFont());
    }

    // 상세 팝업 열기 (이름/설명/수량/분류)
    private void ShowDetail(InventorySystemManager manager, string key, int slotIndex, Vector2 screenPos, InventoryEntry meta)
    {
        InvStore store = section == InventorySection.Main ? manager.GetMainStore() : manager.GetActiveCharStore();
        InvItemStack stack = store != null ? store.FindBySlot(slotIndex) : null;
        int count = stack != null ? stack.count : 0;

        string title = meta != null && string.IsNullOrEmpty(meta.displayName) == false ? meta.displayName : key;
        string body = "";
        if (meta != null && string.IsNullOrEmpty(meta.description) == false)
        {
            body += meta.description + "\n\n";
        }

        body += $"수량: {count}";
        if (meta != null && string.IsNullOrEmpty(meta.category) == false)
        {
            body += $"\n분류: {meta.category}";
        }

        body += $"\n키: {key}";

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

            manager.MoveStack(fromOwner, fromSlot, toOwner, targetCell.SlotIndex);
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

            manager.MoveStack(fromOwner, fromSlot, toOwner, -1);
            return;
        }

        // 3) UI 밖 드롭 → 캐릭터(3D) 위인지 스크린 바운드로 판정
        if (IsPointerOverCharacter(manager, screenPos) == false)
        {
            return;
        }

        if (section == InventorySection.Main)
        {
            // MAIN → 캐릭터: 스택 통째 이동 + (장착 가능하면) 즉시 장착
            string charcode = manager.ActiveCharcode;
            if (string.IsNullOrEmpty(charcode))
            {
                Debug.LogWarning("[InventoryView] 활성 캐릭터가 없어 이동할 수 없습니다.");
                return;
            }

            if (manager.MoveStack(fromOwner, fromSlot, charcode, -1))
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

    // ── 이름 기반 바인딩 (베이크된 프리팹용) ─────────────────────

    // 비어 있는 참조만 자식 이름 탐색으로 채운다
    private void BindExisting()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (headerText == null)
        {
            headerText = FindDeepComponent<TMP_Text>("Header");
        }

        if (grid == null)
        {
            grid = FindDeepChild(transform, "Grid");
        }

        if (slotTemplate == null)
        {
            slotTemplate = FindDeepComponent<InventorySlotView>("SlotTemplate");
        }

        if (sortButton == null)
        {
            sortButton = FindDeepComponent<Button>("SortButton");
        }

        if (closeButton == null)
        {
            closeButton = FindDeepComponent<Button>("CloseButton");
        }

        if (prevButton == null)
        {
            prevButton = FindDeepComponent<Button>("PrevButton");
        }

        if (nextButton == null)
        {
            nextButton = FindDeepComponent<Button>("NextButton");
        }

        if (pageLabel == null)
        {
            pageLabel = FindDeepComponent<TMP_Text>("PageLabel");
        }
    }

    // 이름으로 자손 트랜스폼 탐색 (비활성 포함)
    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

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

    // 이름으로 자손에서 컴포넌트 탐색
    private T FindDeepComponent<T>(string name) where T : Component
    {
        Transform found = FindDeepChild(transform, name);
        return found != null ? found.GetComponent<T>() : null;
    }
}
