using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JarvisCalendarToggleDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform target;
    private Toggle toggle;
    private Canvas canvas;
    private Vector2 startPointerPosition;
    private Vector3 startTargetPosition;
    private bool isDragging;

    public void SetTarget(RectTransform newTarget)
    {
        target = newTarget;
    }

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ResolveReferences();
        if (canvas == null || target == null)
        {
            return;
        }

        isDragging = true;
        if (toggle != null)
        {
            toggle.interactable = false;
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

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        StartCoroutine(RestoreToggleInteractable());
    }

    private IEnumerator RestoreToggleInteractable()
    {
        yield return null;
        if (toggle != null)
        {
            toggle.interactable = true;
        }
    }

    private void ResolveReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (toggle == null)
        {
            toggle = GetComponent<Toggle>();
        }

        if (target == null)
        {
            target = transform.parent as RectTransform;
        }
    }
}
