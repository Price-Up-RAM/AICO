using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AlarmListItemView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text alarmTimeText;
    [SerializeField] private Image enabledIndicatorImage;
    [SerializeField] private Image enabledKnobImage;
    [SerializeField] private Button enabledToggleButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Color enabledColor = new Color(0.35f, 0.8f, 0.45f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.52f, 0.54f, 0.58f, 1f);
    [SerializeField] private Color knobColor = Color.white;
    [SerializeField] private float knobOnX = 8f;
    [SerializeField] private float knobOffX = -8f;
    [SerializeField] private float longPressSeconds = 0.65f;
    [SerializeField] private float cancelMovePixels = 30f;

    private AlarmItem alarm;
    private bool toggleDisplayOn;
    private Action<AlarmItem> selectAction;
    private Action<AlarmItem> toggleEnabledAction;
    private Action<AlarmItem> deleteAction;
    private ScrollRect parentScrollRect;
    private bool pointerDown;
    private bool longPressTriggered;
    private bool movedTooFar;
    private bool forwardingScrollDrag;
    private bool pointerStartedOnButton;
    private float pointerDownTime;
    private Vector2 pointerDownPosition;

    private void Awake()
    {
        parentScrollRect = GetComponentInParent<ScrollRect>();

        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(Delete);
        }

        if (enabledToggleButton != null)
        {
            enabledToggleButton.onClick.AddListener(ToggleEnabled);
        }
    }

    private void Update()
    {
        if (!pointerDown || longPressTriggered || movedTooFar)
        {
            return;
        }

        if (Time.unscaledTime - pointerDownTime < longPressSeconds)
        {
            return;
        }

        longPressTriggered = true;
        if (alarm != null && toggleEnabledAction != null)
        {
            ToggleEnabled();
        }
    }

    public void Setup(
        AlarmItem targetAlarm,
        string title,
        string type,
        string alarmTime,
        bool toggleOn,
        Action<AlarmItem> onSelect,
        Action<AlarmItem> onToggleEnabled,
        Action<AlarmItem> onDelete)
    {
        alarm = targetAlarm;
        selectAction = onSelect;
        toggleEnabledAction = onToggleEnabled;
        deleteAction = onDelete;
        toggleDisplayOn = toggleOn;

        SetText(titleText, title);
        SetText(typeText, type);
        SetText(alarmTimeText, alarmTime);
        RefreshEnabledIndicator();
    }

    public void RefreshDisplay(string title, string type, string alarmTime, bool toggleOn)
    {
        toggleDisplayOn = toggleOn;
        SetText(titleText, title);
        SetText(typeText, type);
        SetText(alarmTimeText, alarmTime);
        RefreshEnabledIndicator();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDown = true;
        longPressTriggered = false;
        movedTooFar = false;
        forwardingScrollDrag = false;
        pointerStartedOnButton = IsButtonTarget(eventData.pointerEnter);
        pointerDownTime = Time.unscaledTime;
        pointerDownPosition = eventData.position;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (pointerStartedOnButton || parentScrollRect == null)
        {
            return;
        }

        movedTooFar = true;
        forwardingScrollDrag = true;
        parentScrollRect.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Vector2.Distance(pointerDownPosition, eventData.position) > cancelMovePixels)
        {
            movedTooFar = true;
        }

        if (forwardingScrollDrag && parentScrollRect != null)
        {
            parentScrollRect.OnDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (forwardingScrollDrag && parentScrollRect != null)
        {
            parentScrollRect.OnEndDrag(eventData);
        }

        forwardingScrollDrag = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        bool shouldSelect = pointerDown && !longPressTriggered && !movedTooFar;
        pointerDown = false;

        if (shouldSelect && alarm != null && selectAction != null)
        {
            selectAction.Invoke(alarm);
        }
    }

    private void Delete()
    {
        pointerDown = false;
        if (alarm == null || deleteAction == null)
        {
            return;
        }

        deleteAction.Invoke(alarm);
    }

    private void ToggleEnabled()
    {
        pointerDown = false;
        if (alarm == null || toggleEnabledAction == null)
        {
            return;
        }

        toggleEnabledAction.Invoke(alarm);
    }

    private void RefreshEnabledIndicator()
    {
        if (enabledIndicatorImage == null || alarm == null)
        {
            return;
        }

        if (toggleDisplayOn)
        {
            enabledIndicatorImage.color = enabledColor;
        }
        else
        {
            enabledIndicatorImage.color = disabledColor;
        }

        if (enabledKnobImage != null)
        {
            enabledKnobImage.color = knobColor;
            RectTransform knobRect = enabledKnobImage.rectTransform;
            knobRect.anchoredPosition = new Vector2(toggleDisplayOn ? knobOnX : knobOffX, knobRect.anchoredPosition.y);
        }
    }

    private bool IsButtonTarget(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        Button targetButton = target.GetComponentInParent<Button>();
        if (targetButton == null)
        {
            return false;
        }

        return targetButton == deleteButton || targetButton == enabledToggleButton;
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
