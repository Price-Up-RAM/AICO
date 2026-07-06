using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 스위치형(iOS 스타일) on/off 토글. 트랙(둥근 pill) + 노브(흰 원)로 구성된다.
/// Button/Toggle 대신 IPointerClickHandler로 클릭을 직접 처리해 커스텀 색/노브 위치를 제어한다.
/// SkillView(CrudRow on/off)와 리스트 행에서 공용으로 쓴다.
///
/// 노브 위치는 트랙 폭에 의존하지 않도록 앵커로 좌/우 끝에 붙인다.
///  - ON  : 우측 앵커 + padding
///  - OFF : 좌측 앵커 + padding
/// </summary>
public class SkillSlideToggle : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image track;
    [SerializeField] private RectTransform knob;
    [SerializeField] private Color onColor = new Color(0.24f, 0.45f, 0.31f, 1f);
    [SerializeField] private Color offColor = new Color(0.34f, 0.30f, 0.30f, 1f);
    [SerializeField] private float knobPadding = 3f;
    [SerializeField] private bool isOn = true;
    [SerializeField] private bool interactable = true;

    public event Action<bool> ValueChanged;

    public bool IsOn => isOn;

    public void Configure(Image track, RectTransform knob, Color onColor, Color offColor)
    {
        this.track = track;
        this.knob = knob;
        this.onColor = onColor;
        this.offColor = offColor;
        ApplyVisual();
    }

    public void SetOn(bool on, bool notify = false)
    {
        isOn = on;
        ApplyVisual();
        if (notify)
        {
            ValueChanged?.Invoke(isOn);
        }
    }

    public void SetInteractable(bool on)
    {
        interactable = on;
        ApplyVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable)
        {
            return;
        }
        isOn = !isOn;
        ApplyVisual();
        ValueChanged?.Invoke(isOn);
    }

    private void OnEnable()
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (track != null)
        {
            Color c = isOn ? onColor : offColor;
            if (!interactable)
            {
                c = new Color(c.r, c.g, c.b, 0.5f);
            }
            track.color = c;
        }

        if (knob != null)
        {
            if (isOn)
            {
                knob.anchorMin = new Vector2(1f, 0.5f);
                knob.anchorMax = new Vector2(1f, 0.5f);
                knob.pivot = new Vector2(1f, 0.5f);
                knob.anchoredPosition = new Vector2(-knobPadding, 0f);
            }
            else
            {
                knob.anchorMin = new Vector2(0f, 0.5f);
                knob.anchorMax = new Vector2(0f, 0.5f);
                knob.pivot = new Vector2(0f, 0.5f);
                knob.anchoredPosition = new Vector2(knobPadding, 0f);
            }
        }
    }
}
