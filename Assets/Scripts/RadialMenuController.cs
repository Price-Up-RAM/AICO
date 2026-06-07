using DevionGames.UIWidgets;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(MenuItem))]
public class RadialMenuController : MonoBehaviour
{
    [SerializeField]
    private string actionName;

    private MenuItem menuItem;
    private RadialMenu radialMenu;
    private UnityAction triggerAction;

    private void Awake()
    {
        menuItem = GetComponent<MenuItem>();
        radialMenu = GetComponentInParent<RadialMenu>(true);
        triggerAction = OnTrigger;
    }

    private void OnEnable()
    {
        if (menuItem == null)
        {
            menuItem = GetComponent<MenuItem>();
        }

        menuItem.onTrigger.RemoveListener(triggerAction);
        menuItem.onTrigger.AddListener(triggerAction);
    }

    private void OnDisable()
    {
        if (menuItem != null && triggerAction != null)
        {
            menuItem.onTrigger.RemoveListener(triggerAction);
        }
    }

    public void SetActionName(string value)
    {
        actionName = value;
    }

    private void OnTrigger()
    {
        if (radialMenu != null)
        {
            radialMenu.Close();
        }

        UnityAction action = CreateAction(actionName);
        action.Invoke();
    }

    private UnityAction CreateAction(string value)
    {
        switch (value)
        {
            case "dance":
                return () => AnimationManager.Instance.Dance();
            case "goLeft":
                return () => PhysicsManager.Instance.SetWalkLeftState();
            case "goRight":
                return () => PhysicsManager.Instance.SetWalkRightState();
            case "hide":
                return () => AnimationManager.Instance.Hide();
            case "idle":
                return () => PhysicsManager.Instance.SetIdleState();
            case "close":
            case "x":
                return () => { };
            default:
                Debug.LogWarning($"Unknown radial menu action: {value}", this);
                return () => { };
        }
    }
}
