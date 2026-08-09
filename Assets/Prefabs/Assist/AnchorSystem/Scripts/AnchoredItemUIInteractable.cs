using UnityEngine;
using UnityEngine.EventSystems;

public enum AnchoredItemUI
{
    Pomodoro,
    Jukebox,
}

/// <summary>
/// Opens or closes the UI represented by a physical anchored item.
/// Interact is public so a future XR interaction event can invoke the same behavior.
/// </summary>
public sealed class AnchoredItemUIInteractable : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private AnchoredItemUI targetUI;
    [SerializeField] private bool leftClickOnly = true;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        if (leftClickOnly && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (eventData.dragging)
        {
            return;
        }

        Interact();
    }

    public void Interact()
    {
        UIManager uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            Debug.LogWarning($"[AnchorSystem] UIManager is missing; '{name}' cannot open {targetUI}.", this);
            return;
        }

        switch (targetUI)
        {
            case AnchoredItemUI.Pomodoro:
                uiManager.TogglePomodoro();
                break;

            case AnchoredItemUI.Jukebox:
                uiManager.ToggleJukebox();
                break;
        }
    }
}
