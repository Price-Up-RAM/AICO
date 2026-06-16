using UnityEngine;
using UnityEngine.EventSystems;

public class JarvisTodoRowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private JarvisTodoItemRow row;

    public void Bind(JarvisTodoItemRow todoRow)
    {
        row = todoRow;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (row != null)
        {
            row.BeginHandleDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (row != null)
        {
            row.UpdateHandleDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (row != null)
        {
            row.EndHandleDrag(eventData);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
    }
}
