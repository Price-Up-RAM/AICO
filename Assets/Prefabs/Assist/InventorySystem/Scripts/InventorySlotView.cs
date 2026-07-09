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
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);     // 이름/수량 텍스트
    private static readonly Color EquippedCyan = new Color(0.25f, 0.85f, 0.90f, 1f);  // 장착 표시(시안)

    // ── 직렬화 참조 (BuildTemplate/베이크 프리팹에서 연결됨) ─────
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

    // 셀 데이터 주입. key가 null/빈 문자열이면 빈 칸 (meta null 허용 — 그때는 key를 이름으로 표시)
    public void Setup(InventoryView owner, int slotIndex, string key, int count, InventoryEntry meta, bool equipped)
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

        // 아이콘: 있으면 표시, 없으면(아이템은 있는데 아이콘만 없으면) 이름 텍스트로 대체
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
            countText.enabled = hasItem && count > 1;
        }

        // 장착 표시 (캐릭터 섹션에서만 owner가 true를 넘긴다)
        if (equippedMark != null)
        {
            equippedMark.enabled = hasItem && equipped;
        }
    }

    // 드래그 중에는 고스트를 매 프레임 커서에 고정 (OnDrag 이벤트 단위 갱신은 간격/끊김이 생긴다)
    private void Update()
    {
        if (dragGhost != null)
        {
            dragGhost.position = Input.mousePosition;
        }
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

        InventoryTooltip.Hide();
        CreateGhost(eventData);
    }

    // 드래그 중: 고스트가 포인터를 따라감
    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
        {
            dragGhost.position = eventData.position;  // ScreenSpaceOverlay 기준
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

        // 고스트는 raycastTarget=false라 포인터 아래의 실제 UI가 잡힌다
        GameObject hovered = eventData.pointerCurrentRaycast.gameObject;
        InventorySlotView targetCell = hovered != null ? hovered.GetComponentInParent<InventorySlotView>() : null;
        InventoryView targetView = hovered != null ? hovered.GetComponentInParent<InventoryView>() : null;
        owner.HandleSlotDrop(slotIndex, key, targetCell, targetView, eventData.position);
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
        dragGhost.position = eventData.position;
    }

    // ── 템플릿 자가 구축 ─────────────────────────────────────────

    // 비활성 셀 템플릿을 코드로 생성 (같은 클래스라 private 직렬화 필드 직접 할당 가능).
    // InventoryView.BuildHierarchy에서 호출된다.
    public static InventorySlotView BuildTemplate(Transform parent)
    {
        GameObject go = new GameObject("SlotTemplate", typeof(RectTransform));
        go.layer = 5; // UI 레이어

        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        ((RectTransform)go.transform).sizeDelta = new Vector2(64f, 64f);

        InventorySlotView slot = go.AddComponent<InventorySlotView>();

        // Background: 전체 채움 + 레이캐스트 수신 (클릭/드롭 진입점)
        slot.background = CreateImage(go.transform, "Background", SlotEmptyBg);
        Stretch(slot.background.rectTransform, 0f);
        slot.background.raycastTarget = true;

        // Icon: 여백 두고 채움 (스프라이트 지정 전까지 비활성)
        slot.iconImage = CreateImage(go.transform, "Icon", Color.white);
        Stretch(slot.iconImage.rectTransform, 7f);
        slot.iconImage.raycastTarget = false;
        slot.iconImage.preserveAspect = true;
        slot.iconImage.enabled = false;

        // Name: 아이콘 없을 때만 활성 (중앙 정렬)
        slot.nameText = CreateText(go.transform, "Name", 11f, TextAlignmentOptions.Center, TextWhite);
        Stretch(slot.nameText.rectTransform, 3f);
        slot.nameText.enabled = false;

        // Count: 우하단 수량
        slot.countText = CreateText(go.transform, "Count", 12f, TextAlignmentOptions.BottomRight, TextWhite);
        RectTransform countRt = slot.countText.rectTransform;
        countRt.anchorMin = new Vector2(1f, 0f);
        countRt.anchorMax = new Vector2(1f, 0f);
        countRt.pivot = new Vector2(1f, 0f);
        countRt.anchoredPosition = new Vector2(-3f, 3f);
        countRt.sizeDelta = new Vector2(36f, 18f);
        slot.countText.enabled = false;

        // EquippedMark: 좌상단 작은 시안색 점 (기본 비활성)
        slot.equippedMark = CreateImage(go.transform, "EquippedMark", EquippedCyan);
        RectTransform markRt = slot.equippedMark.rectTransform;
        markRt.anchorMin = new Vector2(0f, 1f);
        markRt.anchorMax = new Vector2(0f, 1f);
        markRt.pivot = new Vector2(0f, 1f);
        markRt.anchoredPosition = new Vector2(3f, -3f);
        markRt.sizeDelta = new Vector2(10f, 10f);
        slot.equippedMark.raycastTarget = false;
        slot.equippedMark.enabled = false;

        // 템플릿은 비활성 상태로 대기 (그리드에 Instantiate 후 활성화)
        go.SetActive(false);
        return slot;
    }

    // 자식 Image 생성 헬퍼
    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    // 자식 TMP 텍스트 생성 헬퍼
    private static TMP_Text CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    // RectTransform을 부모 전체에 맞춰 늘리기 (inset = 사방 여백)
    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
