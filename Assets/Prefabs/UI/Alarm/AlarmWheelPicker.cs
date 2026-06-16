using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class AlarmWheelPicker : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TMP_Text previousText;
    [SerializeField] private TMP_Text currentText;
    [SerializeField] private TMP_Text nextText;
    [SerializeField] private int minValue;
    [SerializeField] private int maxValue = 59;
    [SerializeField] private int currentValue;
    [SerializeField] private bool padTwoDigits = true;
    [SerializeField] private float dragStepPixels = 26f;
    [SerializeField] private float minStepInterval = 0.08f;
    [SerializeField] private int maxDragStepsPerEvent = 8;
    [SerializeField] private float snapDuration = 0.14f;
    [SerializeField] private Ease snapEase = Ease.OutCubic;
    [SerializeField] private string[] labels;

    public event Action<int> ValueChanged;

    private float dragProgress;
    private float lastStepTime = -100f;
    private RectTransform previousRect;
    private RectTransform currentRect;
    private RectTransform nextRect;
    private Vector2 previousBasePosition;
    private Vector2 currentBasePosition;
    private Vector2 nextBasePosition;
    private Tween snapTween;
    private int dragStartValue;

    private void Awake()
    {
        CacheTextRects();
        SetValue(currentValue, false);
    }

    private void OnDisable()
    {
        KillSnapTween();
        ResetTextPositions();
    }

    public int GetValue()
    {
        return currentValue;
    }

    public void ConfigureRange(int min, int max, bool useTwoDigits)
    {
        minValue = min;
        maxValue = max;
        padTwoDigits = useTwoDigits;
        SetValue(currentValue, false);
    }

    public void ConfigureLabels(string[] values)
    {
        labels = values;
        minValue = 0;
        maxValue = 0;
        if (labels != null && labels.Length > 0)
        {
            maxValue = labels.Length - 1;
        }

        SetValue(currentValue, false);
    }

    public void SetValue(int value)
    {
        SetValue(value, false);
    }

    public void SetValue(int value, bool notify)
    {
        KillSnapTween();
        dragProgress = 0f;
        currentValue = WrapValue(value);
        RefreshTexts();
        ResetTextPositions();

        if (notify && ValueChanged != null)
        {
            ValueChanged.Invoke(currentValue);
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!CanStep())
        {
            return;
        }

        if (eventData.scrollDelta.y > 0f)
        {
            StepAnimated(-1);
        }
        else if (eventData.scrollDelta.y < 0f)
        {
            StepAnimated(1);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        KillSnapTween();
        dragProgress = 0f;
        dragStartValue = currentValue;
        ResetTextPositions();
    }

    public void OnDrag(PointerEventData eventData)
    {
        dragProgress += eventData.delta.y;
        ApplyDragOffset(dragProgress);

        int stepCount = 0;
        while (Mathf.Abs(dragProgress) >= dragStepPixels && stepCount < maxDragStepsPerEvent)
        {
            int delta = -1;
            if (dragProgress > 0f)
            {
                delta = 1;
            }

            Step(delta, false);
            dragProgress -= Mathf.Sign(dragProgress) * dragStepPixels;
            stepCount++;
            ApplyDragOffset(dragProgress);
        }

        if (stepCount >= maxDragStepsPerEvent)
        {
            dragProgress = Mathf.Sign(dragProgress) * Mathf.Min(Mathf.Abs(dragProgress), dragStepPixels - 1f);
            ApplyDragOffset(dragProgress);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float halfStep = dragStepPixels * 0.5f;
        if (Mathf.Abs(dragProgress) >= halfStep)
        {
            int delta = -1;
            if (dragProgress > 0f)
            {
                delta = 1;
            }

            Step(delta, false);
        }

        dragProgress = 0f;
        AnimateDragOffsetToZero();
        NotifyValueChangedIfNeeded();
    }

    private void Step(int delta)
    {
        Step(delta, true);
    }

    private void Step(int delta, bool notify)
    {
        lastStepTime = Time.unscaledTime;
        currentValue = WrapValue(currentValue + delta);
        RefreshTexts();

        if (notify && ValueChanged != null)
        {
            ValueChanged.Invoke(currentValue);
        }
    }

    private void StepAnimated(int delta)
    {
        lastStepTime = Time.unscaledTime;
        currentValue = WrapValue(currentValue + delta);
        RefreshTexts();

        float startOffset = dragStepPixels;
        if (delta > 0)
        {
            startOffset = -dragStepPixels;
        }

        ApplyDragOffset(startOffset);
        AnimateDragOffsetToZero();

        if (ValueChanged != null)
        {
            ValueChanged.Invoke(currentValue);
        }
    }

    private bool CanStep()
    {
        if (Time.unscaledTime - lastStepTime < minStepInterval)
        {
            return false;
        }

        return true;
    }

    private int WrapValue(int value)
    {
        if (maxValue < minValue)
        {
            return minValue;
        }

        int range = maxValue - minValue + 1;
        if (range <= 0)
        {
            return minValue;
        }

        while (value < minValue)
        {
            value += range;
        }

        while (value > maxValue)
        {
            value -= range;
        }

        return value;
    }

    private void RefreshTexts()
    {
        SetText(previousText, FormatValue(WrapValue(currentValue - 1)));
        SetText(currentText, FormatValue(currentValue));
        SetText(nextText, FormatValue(WrapValue(currentValue + 1)));
    }

    private string FormatValue(int value)
    {
        if (labels != null && value >= 0 && value < labels.Length)
        {
            return labels[value];
        }

        if (padTwoDigits)
        {
            return value.ToString("D2");
        }

        return value.ToString();
    }

    private void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.alignment = TextAlignmentOptions.Center;
            target.text = value;
        }
    }

    private void CacheTextRects()
    {
        if (previousText != null)
        {
            previousRect = previousText.GetComponent<RectTransform>();
        }

        if (currentText != null)
        {
            currentRect = currentText.GetComponent<RectTransform>();
        }

        if (nextText != null)
        {
            nextRect = nextText.GetComponent<RectTransform>();
        }

        if (previousRect != null)
        {
            previousBasePosition = previousRect.anchoredPosition;
        }

        if (currentRect != null)
        {
            currentBasePosition = currentRect.anchoredPosition;
        }

        if (nextRect != null)
        {
            nextBasePosition = nextRect.anchoredPosition;
        }
    }

    private void ApplyDragOffset(float offset)
    {
        if (previousRect != null)
        {
            previousRect.anchoredPosition = previousBasePosition + new Vector2(0f, offset);
        }

        if (currentRect != null)
        {
            currentRect.anchoredPosition = currentBasePosition + new Vector2(0f, offset);
        }

        if (nextRect != null)
        {
            nextRect.anchoredPosition = nextBasePosition + new Vector2(0f, offset);
        }
    }

    private void ResetTextPositions()
    {
        ApplyDragOffset(0f);
    }

    private void AnimateDragOffsetToZero()
    {
        KillSnapTween();
        float startOffset = 0f;
        if (currentRect != null)
        {
            startOffset = currentRect.anchoredPosition.y - currentBasePosition.y;
        }

        snapTween = DOTween.To(() => startOffset, ApplyDragOffset, 0f, snapDuration)
            .SetEase(snapEase)
            .SetUpdate(true)
            .SetTarget(this);
    }

    private void KillSnapTween()
    {
        if (snapTween != null)
        {
            snapTween.Kill();
            snapTween = null;
        }
    }

    private void NotifyValueChangedIfNeeded()
    {
        if (currentValue == dragStartValue)
        {
            return;
        }

        if (ValueChanged != null)
        {
            ValueChanged.Invoke(currentValue);
        }
    }
}
