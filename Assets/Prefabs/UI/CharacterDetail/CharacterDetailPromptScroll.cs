using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterDetailPromptScroll : MonoBehaviour, IScrollHandler, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
{
    [SerializeField] private TMP_InputField inputField;
    private Coroutine restoreScrollbarCoroutine;

    private void Awake()
    {
        if (inputField == null)
        {
            inputField = GetComponentInParent<TMP_InputField>(true);
        }
    }

    public void SetInputField(TMP_InputField target)
    {
        inputField = target;
    }

    public void OnScroll(PointerEventData eventData)
    {
        TMP_InputField target = inputField != null ? inputField : GetComponentInParent<TMP_InputField>(true);
        if (target == null)
        {
            return;
        }

        target.OnScroll(eventData);
        eventData.Use();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsPromptScrollbar())
        {
            return;
        }

        TMP_InputField target = ResolveInputField();
        if (target == null)
        {
            return;
        }

        target.DeactivateInputField();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        RestorePromptScrollbarAfterDrag();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        RestorePromptScrollbarAfterDrag();
    }

    private TMP_InputField ResolveInputField()
    {
        return inputField != null ? inputField : GetComponentInParent<TMP_InputField>(true);
    }

    private bool IsPromptScrollbar()
    {
        TMP_InputField target = ResolveInputField();
        if (target == null || target.verticalScrollbar == null)
        {
            return false;
        }

        Transform scrollbarTransform = target.verticalScrollbar.transform;
        return transform == scrollbarTransform || transform.IsChildOf(scrollbarTransform);
    }

    private void RestorePromptScrollbarAfterDrag()
    {
        if (!IsPromptScrollbar())
        {
            return;
        }

        TMP_InputField target = ResolveInputField();
        Scrollbar scrollbar = target != null ? target.verticalScrollbar : null;
        if (scrollbar == null)
        {
            return;
        }

        float value = scrollbar.value;
        if (restoreScrollbarCoroutine != null)
        {
            StopCoroutine(restoreScrollbarCoroutine);
        }

        restoreScrollbarCoroutine = StartCoroutine(RestorePromptScrollbarNextFrame(value));
    }

    private IEnumerator RestorePromptScrollbarNextFrame(float value)
    {
        yield return null;

        TMP_InputField target = ResolveInputField();
        Scrollbar scrollbar = target != null ? target.verticalScrollbar : null;
        if (target == null || scrollbar == null)
        {
            restoreScrollbarCoroutine = null;
            yield break;
        }

        target.DeactivateInputField();

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == target.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Canvas.ForceUpdateCanvases();

        value = Mathf.Clamp01(value);
        float nudgedValue = value < 0.999f ? Mathf.Min(1f, value + 0.0001f) : Mathf.Max(0f, value - 0.0001f);
        if (!Mathf.Approximately(nudgedValue, value))
        {
            scrollbar.value = nudgedValue;
        }

        scrollbar.value = value;
        Canvas.ForceUpdateCanvases();
        restoreScrollbarCoroutine = null;
    }
}
