using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 상점 판매 존: 인벤토리 슬롯(InventorySlotView)을 여기로 드래그하면 판매 확인 팝업을 연다.
// 드롭 검증(소스/빈 칸/MAIN 전용/스택 조회)까지만 담당하고, 실제 판매(수량 선택 → 차감/골드)는
// StoreView.RequestSell → StoreConfirmView(Sell 모드) → ExecuteSale이 처리한다.
// OnDrop은 소스 셀의 OnEndDrag보다 먼저 실행되므로, DropConsumed 플래그로
// 소스 셀의 HandleSlotDrop(재장착/이동)을 차단한다.
public class StoreSellZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color AccentBlue = new Color(0.243f, 0.325f, 0.502f, 1f);  // 호버 하이라이트

    [SerializeField] private StoreView owner;   // 토스트/판매가 질의 대상

    private Image background;        // 자기 배경 (호버 하이라이트용)
    private Color normalColor;       // 하이라이트 전 원래 색
    private bool hasNormalColor;     // 원래 색 캐시 여부
    private bool highlighted;        // 현재 하이라이트 상태

    // StoreView가 Build/BindExisting에서 호출해 소유자를 연결한다
    public void Configure(StoreView view)
    {
        owner = view;
    }

    private void Awake()
    {
        background = GetComponent<Image>();
    }

    // 드래그 중 진입: 판매 가능한 드래그(인벤토리 슬롯)일 때만 하이라이트
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData.dragging == false || eventData.pointerDrag == null)
        {
            return;
        }

        InventorySlotView slot = eventData.pointerDrag.GetComponent<InventorySlotView>();
        if (slot == null || slot.HasItem == false)
        {
            return;
        }

        SetHighlight(true);
    }

    // 이탈: 하이라이트 복원
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlight(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        SetHighlight(false);

        // 1. 드래그 소스가 인벤토리 슬롯인지
        InventorySlotView slot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<InventorySlotView>() : null;
        if (slot == null)
        {
            return;
        }

        // 2. 빈 칸 드래그 무시
        if (slot.HasItem == false)
        {
            return;
        }

        // 3. 활성 캐릭터 소지품만 판매 허용.
        //
        // 2026-08-26까지는 "MAIN 인벤토리만 허용"이었고, 그 금지 사유가
        // **"CHAR 보관함 판매는 장착 꼬임 방지"**였다. 공용 창을 걷어내 캐릭터 소지품이
        // 유일한 보관함이 됐으므로 대상은 바꾸되, 그 사유는 그대로 살려서
        // **착용 중인 아이템은 아래에서 따로 막는다.** 규칙을 옮기면서 이유까지 버리면 안 된다.
        string sourceOwnerId = slot.Owner != null ? slot.Owner.OwnerId() : null;
        string activeOwnerId = InventorySystemManager.Instance != null
            ? InventorySystemManager.Instance.ActiveCharcode
            : null;

        if (string.IsNullOrEmpty(activeOwnerId) || sourceOwnerId != activeOwnerId)
        {
            if (owner != null)
            {
                owner.ShowToast("이 인벤토리의 아이템만 판매할 수 있습니다");
            }

            Debug.LogWarning($"[Store][StoreSellZone] 판매 거부 — 출처={sourceOwnerId} 활성={activeOwnerId}");
            InventorySlotView.DropConsumed = true;
            return;
        }

        if (owner == null || InventorySystemManager.Instance == null)
        {
            Debug.LogWarning("[Store][StoreSellZone] owner/매니저 참조가 없어 판매를 처리할 수 없습니다.");
            return;
        }

        // 4. 슬롯 인덱스로 실제 스택 조회
        InvStore store = InventorySystemManager.Instance.GetActiveCharStore();
        InvItemStack stack = store != null ? store.FindBySlot(slot.SlotIndex) : null;
        if (stack == null)
        {
            return;
        }

        // 4-1. 착용 중인 아이템은 팔 수 없다.
        //
        // 옛 규칙이 캐릭터 보관함 판매를 통째로 막아 이 경우를 덮고 있었다.
        // 이제 캐릭터 소지품이 판매원이 됐으므로 여기서 직접 막는다 —
        // 팔린 뒤에도 소켓에 모델이 남아 "없는 물건을 쓰고 있는" 상태가 된다.
        if (InventorySystemManager.Instance.IsEquippedOnActive(stack.key))
        {
            if (owner != null)
            {
                owner.ShowToast("착용 중인 아이템은 벗은 뒤에 판매할 수 있습니다");
            }

            Debug.Log($"[Store][StoreSellZone] 판매 거부 — 착용 중: {stack.key}");
            InventorySlotView.DropConsumed = true;
            return;
        }

        // 5. 소스 셀의 HandleSlotDrop 실행 차단 (판매 대기 아이템 재장착 버그 방지)
        InventorySlotView.DropConsumed = true;

        // 6. 여기서는 아무것도 변경하지 않는다 — 수량 선택/차감은 확인 팝업 확정 시 StoreView가 처리
        owner.RequestSell(stack.key, slot.SlotIndex, stack.count);
    }

    // 배경 하이라이트 on/off (원래 색은 최초 하이라이트 시점에 캐시)
    private void SetHighlight(bool on)
    {
        if (background == null || highlighted == on)
        {
            return;
        }

        if (on)
        {
            if (hasNormalColor == false)
            {
                normalColor = background.color;
                hasNormalColor = true;
            }

            background.color = AccentBlue;
        }
        else if (hasNormalColor)
        {
            background.color = normalColor;
        }

        highlighted = on;
    }
}
