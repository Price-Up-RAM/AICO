using UnityEngine;
using UnityEngine.EventSystems;
using UnityWeld.Binding;

[Binding] 
public class DragUIHandler : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    private Canvas _canvas;

    private RectTransform dragTarget;
    private Vector2 startPointerPos;
    private Vector3 startUIPos;
    private bool dragReady;

    void Start()
    {
        _canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragReady = false;
        if (eventData == null || _canvas == null || transform.parent == null)
        {
            return;
        }

        dragTarget = transform.parent.GetComponent<RectTransform>();
        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (dragTarget == null || canvasRect == null)
        {
            return;
        }

        // UI 최초 위치 (로컬 포지션 사용)
        startUIPos = dragTarget.localPosition;

        Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;
        dragReady = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventCamera,
            out startPointerPos);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragReady == false || eventData == null || _canvas == null || dragTarget == null)
        {
            return;
        }

        RectTransform canvasRect = _canvas.transform as RectTransform;
        Camera eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _canvas.worldCamera;
        if (canvasRect == null ||
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                eventData.position,
                eventCamera,
                out Vector2 pos) == false)
        {
            return;
        }

        // 이동 오프셋 계산 및 적용
        Vector3 offset = pos - startPointerPos;
        dragTarget.localPosition = startUIPos + offset;
    }

    private void OnDisable()
    {
        dragReady = false;
        dragTarget = null;
    }
}
