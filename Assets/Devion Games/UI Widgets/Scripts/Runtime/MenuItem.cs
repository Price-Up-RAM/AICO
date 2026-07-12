using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DevionGames.UIWidgets
{
	public class MenuItem : Selectable, IPointerClickHandler
	{

		private UnityEvent m_Trigger = new UnityEvent ();

		public UnityEvent onEnter = new UnityEvent();
		public UnityEvent onExit = new UnityEvent();
		public bool isSubMenuTrigger = false;
		public bool isLocked = false;
		private Color originalColor = Color.white;
		private bool originalColorSet = false;

		protected override void Awake() {
			base.Awake();
			if (targetGraphic != null && !originalColorSet) {
				originalColor = targetGraphic.color;
				originalColorSet = true;
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			isLocked = false;
			if (targetGraphic != null && originalColorSet) {
				if (IsInteractable()) {
					targetGraphic.color = originalColor;
					DoStateTransition(SelectionState.Normal, true);
				} else {
					DoStateTransition(SelectionState.Disabled, true);
				}
			}
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			isLocked = false;
			if (targetGraphic != null && originalColorSet) {
				targetGraphic.color = originalColor;
				if (IsInteractable()) {
					DoStateTransition(SelectionState.Normal, true);
				}
			}
		}

		public UnityEvent onTrigger {
			get {
				if (m_Trigger == null) {
					m_Trigger = new UnityEvent ();
				}
				return this.m_Trigger;
			}
			set {
				this.m_Trigger = value;
			}
		}

		private void Press ()
		{
			if (!IsActive () || !IsInteractable ())
				return;

			onTrigger.Invoke ();
		}

		public void OnPointerClick (PointerEventData eventData)
		{
			Press ();
		}


		public void ResetColor() {
			if (targetGraphic != null && !isLocked) {
				if (IsInteractable()) {
					targetGraphic.color = originalColor;
				} else {
					DoStateTransition(SelectionState.Disabled, false);
				}
			}
		}

		public void SetLockedColor() {
			if (targetGraphic != null) targetGraphic.color = new Color(0.5f, 0.5f, 0.5f, 1f);
		}

		public void RefreshVisualState() {
			ResetColor();
		}

		public override void OnPointerEnter (PointerEventData eventData)
		{
			base.OnPointerEnter (eventData);
			if (IsInteractable()) {
				if (targetGraphic != null && !isLocked) targetGraphic.color = new Color(0.5f, 0.5f, 0.5f, 1f);
			}
			if (onEnter != null) onEnter.Invoke();
		}

		public override void OnPointerExit (PointerEventData eventData)
		{
			base.OnPointerExit (eventData);
			ResetColor();
			if (onExit != null) onExit.Invoke();
		}
	}
}
