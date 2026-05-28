using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UniversalCalendarDay : MonoBehaviour
{
	public static Action<DateTime> onDayPressed;

	private Button m_btnDay;
	private DateTime m_timeSlot;

	[SerializeField] private Text txtDay;
	[SerializeField] private Text txtTodoCount; // "완료/전체" 표시, 없으면 무시

	private void OnEnable()
	{
		EnsureComponents();
		m_btnDay.onClick.AddListener(PressDay);
	}

	private void OnDisable()
	{
		m_btnDay.onClick.RemoveListener(PressDay);
	}

	private void Awake()
	{
		EnsureComponents();
	}

	private void EnsureComponents()
	{
		if (m_btnDay == null) m_btnDay = GetComponent<Button>();
		if (txtDay == null) txtDay = GetComponentInChildren<Text>();
	}

	public void InitButton(DateTime p_newTimeSlot, string p_newText, bool p_isClickable, Color? p_newColor = null)
	{
		EnsureComponents();
		m_btnDay.interactable = p_isClickable;
		m_timeSlot = p_newTimeSlot;
		txtDay.text = p_newText;
		txtDay.color = p_newColor ?? Color.black;

		if (txtTodoCount != null)
		{
			if (p_isClickable && TODOManager.Instance != null)
			{
				var items = TODOManager.Instance.GetItemsByDate(p_newTimeSlot);
				if (items.Count > 0)
				{
					int completed = items.FindAll(i => i.isCompleted).Count;
					txtTodoCount.text = $"{completed}/{items.Count}";
					txtTodoCount.gameObject.SetActive(true);
				}
				else
				{
					txtTodoCount.gameObject.SetActive(false);
				}
			}
			else
			{
				txtTodoCount.gameObject.SetActive(false);
			}
		}
	}

	void PressDay()
	{
		onDayPressed?.Invoke(m_timeSlot);
	}
}
