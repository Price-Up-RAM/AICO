using UnityEngine;
using UnityEngine.EventSystems;

// 인벤토리 창 전역 드래그 핸들러 (프로젝트 확립 패턴: JarvisCalendarToggleDragHandler 미러).
// 패널 루트에 붙이면 창의 아무 곳이나 잡고 끌어 이동할 수 있다.
// 슬롯 위에서 시작한 드래그도 부모로 버블링되어 창이 끌리고, 짧은 클릭은 그대로 슬롯 클릭으로 동작한다.
public class InventoryWindowDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] private RectTransform target;  // 이동 대상 (비우면 자기 자신)

    private Canvas canvas;                   // 좌표 변환 기준 캔버스
    private Vector2 startPointerPosition;    // 드래그 시작 시 포인터 위치 (캔버스 로컬)
    private Vector3 startTargetPosition;     // 드래그 시작 시 대상 위치

    private void Awake()
    {
        ResolveReferences();
    }

    // 드래그 시작: 기준 위치 기록
    public void OnBeginDrag(PointerEventData eventData)
    {
        ResolveReferences();
        if (canvas == null || target == null)
        {
            return;
        }

        startTargetPosition = target.localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out startPointerPosition);
    }

    // 드래그 중: 포인터 이동량만큼 창 이동
    public void OnDrag(PointerEventData eventData)
    {
        ResolveReferences();
        if (canvas == null || target == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 currentPointerPosition);

        Vector2 offset = currentPointerPosition - startPointerPosition;
        target.localPosition = startTargetPosition + new Vector3(offset.x, offset.y, 0f);
    }

    // 참조 지연 해석 (프리팹 안전: 씬 참조를 직렬화하지 않는다)
    private void ResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (target == null)
        {
            target = transform as RectTransform;
        }
    }
}
