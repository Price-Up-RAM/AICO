using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] private RectTransform target;

    private Canvas canvas;
    private Vector2 startPointerPosition;
    private Vector3 startTargetPosition;

    public void SetTarget(RectTransform newTarget)
    {
        target = newTarget;
    }

    private void Awake()
    {
        ResolveReferences();
    }

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

    private void ResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (target == null)
        {
            target = transform.parent as RectTransform;
        }
    }
}
