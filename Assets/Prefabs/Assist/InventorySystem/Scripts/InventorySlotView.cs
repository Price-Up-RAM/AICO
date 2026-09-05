using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 인벤토리 그리드 셀 1칸: 빈 칸 또는 아이템(아이콘/이름/수량/장착표시)을 표시하고
// 좌클릭(퀵액션)/우클릭(컨텍스트 메뉴)/드래그(위치 이동·창 간 이동·캐릭터 장착)를 InventoryView로 위임.
// 클릭은 자식 Background(Image)가 레이캐스트를 받고, 이벤트 시스템이 부모의 이 핸들러로 버블링한다.
// (셀이 IDragHandler를 구현하므로 아이템 셀 위 드래그는 창 이동보다 아이템 드래그가 우선.
//  빈 칸 드래그는 아무 동작도 하지 않는다.)
public class InventorySlotView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    // ── 다크 팔레트 ──────────────────────────────────────────────
    private static readonly Color SlotBg = new Color(0.16f, 0.16f, 0.20f, 1f);        // 아이템 칸 배경
    private static readonly Color SlotEmptyBg = new Color(0.12f, 0.12f, 0.15f, 1f);   // 빈 칸 배경
    private static readonly Color EquippedCyan = new Color(0.25f, 0.85f, 0.90f, 1f);  // 장착 표시(시안)

    private static Sprite noImageSprite;
    private static bool noImageLoadTried;

    // ── 직렬화 참조 (베이크 프리팹에서 연결됨) ───────────────────
    [SerializeField] private Image background;    // "Background" 칸 배경 (레이캐스트 수신)
    [SerializeField] private Image iconImage;     // "Icon" 아이콘 (없으면 비활성)
    [SerializeField] private TMP_Text nameText;   // "Name" 아이콘 없을 때만 활성
    [SerializeField] private TMP_Text countText;  // "Count" 우하단 수량
    [SerializeField] private Image equippedMark;  // "EquippedMark" 좌상단 장착 표시 (기본 비활성)

    private InventoryView owner;      // 클릭/드래그 위임 대상
    private string key;               // 아이템 키 (빈 칸이면 null)
    private int slotIndex = -1;       // 이 셀의 그리드 칸 인덱스
    private bool hasItem;             // 아이템 존재 여부
    private RectTransform dragGhost;  // 드래그 중 포인터를 따라다니는 고스트

    public InventoryView Owner
    {
        get
        {
            return owner;
        }
    }

    public int SlotIndex
    {
        get
        {
            return slotIndex;
        }
    }

    public bool HasItem
    {
        get
        {
            return hasItem;
        }
    }

    public string Key
    {
        get
        {
            return key;
        }
    }

    // 외부 드롭 프로토콜: 외부 드롭 존(IDropHandler)이 드롭을 처리했으면 true로 세워
    // 소스 셀의 HandleSlotDrop(이동/장착)을 건너뛰게 한다 (OnDrop이 OnEndDrag보다 먼저 실행됨)
    public static bool DropConsumed;

    // 셀 데이터 주입. key가 null/빈 문자열이면 빈 칸 (meta null 허용 — 그때는 key를 이름으로 표시)
    public void Setup(InventoryView owner, int slotIndex, string key, int count, ItemEntry meta, bool equipped)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
        this.key = key;
        hasItem = string.IsNullOrEmpty(key) == false;

        // 배경: 아이템 유무에 따라 톤 구분 (빈 칸도 레이캐스트는 받아 드롭 타깃이 된다)
        if (background != null)
        {
            background.color = hasItem ? SlotBg : SlotEmptyBg;
        }

        Sprite icon = hasItem && meta != null ? meta.icon : null;
        if (hasItem && icon == null)
        {
            icon = ResolveNoImageSprite();
        }

        // 아이콘: 실제 아이콘이 없으면 상점과 같은 NO IMAGE 스프라이트, 그것도 없으면 이름 텍스트로 대체
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
        {
            string display = key;
            if (meta != null && string.IsNullOrEmpty(meta.displayName) == false)
            {
                display = meta.displayName;
            }

            nameText.text = hasItem ? display : "";
            nameText.enabled = hasItem && icon == null;
        }

        // 수량: 1 이하이면 숨김
        if (countText != null)
        {
            countText.text = count.ToString();
            countText.enabled = hasItem && count > 1 && (meta == null || meta.isCountable);
        }

        // 장착 표시 (캐릭터 섹션에서만 owner가 true를 넘긴다)
        if (equippedMark != null)
        {
            equippedMark.enabled = hasItem && equipped;
        }
    }

    private static Sprite ResolveNoImageSprite()
    {
        if (noImageLoadTried == false)
        {
            noImageLoadTried = true;
            noImageSprite = Resources.Load<Sprite>("StoreNoImage");
        }

        return noImageSprite;
    }

    // hover 진입: 미니 툴팁 표시 (드래그 중에는 표시하지 않음)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null || hasItem == false || eventData.dragging)
        {
            return;
        }

        owner.OnSlotHoverEnter(key, slotIndex, eventData.position);
    }

    // hover 이탈: 미니 툴팁 숨김
    public void OnPointerExit(PointerEventData eventData)
    {
        InventoryTooltip.Hide();
    }

    // 클릭 분기: 섹션별 동작은 InventoryView가 판단한다 (드래그가 발생했으면 클릭은 발동하지 않음)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null || hasItem == false)
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner.OnSlotLeftClicked(key);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            owner.OnSlotRightClicked(key, slotIndex, eventData.position);
        }
    }

    // ── 드래그 앤 드롭 ───────────────────────────────────────────

    // 드래그 시작: 아이템이 있을 때만 고스트 생성 (툴팁은 숨김)
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null || hasItem == false)
        {
            return;
        }

        DropConsumed = false;
        InventoryTooltip.Hide();
        CreateGhost(eventData);
    }

    // 드래그 중: 고스트가 포인터를 따라감
    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            SetGhostScreenPosition(eventData.position);
        }
    }

    // 드래그 종료: 고스트 제거 + 드롭 위치 해석을 InventoryView에 위임
    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            Destroy(dragGhost.gameObject);
            dragGhost = null;
        }

        if (owner == null || hasItem == false)
        {
            return;
        }

        // 외부 드롭 존이 이미 소비한 드롭이면 자체 처리 생략
        if (!DropConsumed)
        {
            // 고스트는 raycastTarget=false라 포인터 아래의 실제 UI가 잡힌다
            GameObject hovered = eventData.pointerCurrentRaycast.gameObject;
            InventorySlotView targetCell = hovered != null ? hovered.GetComponentInParent<InventorySlotView>() : null;
            InventoryView targetView = hovered != null ? hovered.GetComponentInParent<InventoryView>() : null;
            owner.HandleSlotDrop(slotIndex, key, targetCell, targetView, eventData.position);
        }
    }

    // 드래그 도중 셀이 비활성화되면(외부 드롭 처리로 그리드가 즉시 재구축되는 경우 등)
    // OnEndDrag가 오지 않으므로, 고스트는 셀 수명에 맞춰 여기서 정리한다
    private void OnDisable()
    {
        if (dragGhost != null)
        {
            Destroy(dragGhost.gameObject);
            dragGhost = null;
        }
    }

    // 드래그 고스트 생성 (최상위 캔버스 아래, 레이캐스트 통과)
    private void CreateGhost(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject go = new GameObject("DragGhost", typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(canvas.rootCanvas.transform, false);

        // 부모 캔버스의 LayoutGroup에 편입되지 않게 한다.
        // InventoryPanel 루트에 VerticalLayoutGroup이 있고, MR 전환(Tools 6)으로 패널 자체가
        // 루트 캔버스가 되면서 런타임 생성 UI가 그 레이아웃 안으로 떨어진다 (§4-18 계열의 후속 함정).
        go.AddComponent<LayoutElement>().ignoreLayout = true;

        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;

        Sprite icon = iconImage != null ? iconImage.sprite : null;
        if (icon != null)
        {
            img.sprite = icon;
            img.preserveAspect = true;
            img.color = new Color(1f, 1f, 1f, 0.85f);
        }
        else
        {
            img.color = new Color(SlotBg.r, SlotBg.g, SlotBg.b, 0.7f);
        }

        dragGhost = (RectTransform)go.transform;
        dragGhost.sizeDelta = new Vector2(56f, 56f);
        dragGhost.SetAsLastSibling();
        SetGhostScreenPosition(eventData.position);
    }

    private void SetGhostScreenPosition(Vector2 screenPosition)
    {
        if (dragGhost == null)
        {
            return;
        }

        Canvas rootCanvas = dragGhost.GetComponentInParent<Canvas>();
        RectTransform canvasRect = rootCanvas != null
            ? rootCanvas.transform as RectTransform
            : null;
        Camera uiCamera = rootCanvas != null &&
                          rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                uiCamera,
                out Vector2 localPosition))
        {
            dragGhost.anchoredPosition = localPosition;
        }
    }
}
