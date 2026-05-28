using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Linq;

namespace DevionGames.UIWidgets
{
	public class RadialMenu : UIWidget
	{
		[SerializeField]
		protected float m_Radius = 100f;
		[SerializeField]
		protected float m_Angle = 360f;
		[Header ("Reference")]
		[SerializeField]
		protected MenuItem m_Item = null;

		private List<MenuItem> itemCache = new List<MenuItem> ();
		private GameObject m_Target;

		public Vector2 characterTransformPos = Vector2.zero;

		protected override void Update ()
		{
			base.Update();
			if (m_CanvasGroup.alpha > 0f && (Input.GetMouseButtonUp (0) || Input.GetMouseButtonUp (1) || Input.GetMouseButtonUp (2))) {

				var pointer = new PointerEventData (EventSystem.current);
				pointer.position = Input.mousePosition;
				var raycastResults = new List<RaycastResult> ();
				EventSystem.current.RaycastAll (pointer, raycastResults);
				List<GameObject> results = raycastResults.Select(x => x.gameObject).ToList();

				if (results.Count > 0 && results.Contains(this.m_Target)) {
					results [0].SendMessage ("Press", SendMessageOptions.DontRequireReceiver);
                }else
					Close ();
			}
		}

        public virtual void Show (GameObject target, Sprite[] icons, UnityAction<int> result)
		{
			if (this.m_Target == target) {
				Close();
				return;
			}
				
			this.m_Target = target;
			for (int i = 0; i < itemCache.Count; i++) {
				itemCache [i].gameObject.SetActive (false);
			}
			Show ();
			for (int i = 0; i < icons.Length; i++) {
				int index = i;
				MenuItem item = AddMenuItem (icons [index]);
				float theta = Mathf.Deg2Rad * (m_Angle / icons.Length) * index;
				Vector3 position = new Vector3 (Mathf.Sin (theta), Mathf.Cos (theta), 0);
				item.transform.localPosition = position * m_Radius;

				item.onTrigger.AddListener (delegate() {
					Close ();
					if (result != null) {
						result.Invoke (index);
					}
				});
			}
		}

        public override void Close()
        {
            base.Close();
			this.m_Target = null;
        }

        public override void Show ()
		{
			
			if (characterTransformPos != Vector2.one) {
				m_RectTransform.anchoredPosition = characterTransformPos;
			} else {
				Canvas _canvas = FindObjectOfType<Canvas>();
				Vector2 charPosition = Input.mousePosition;
				RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas.transform as RectTransform, charPosition, _canvas.worldCamera, out Vector2 pos);
				Vector3 newPos = new Vector3(pos.x, pos.y, -350);  // 유닛앞에 : z=-350
				m_RectTransform.position = _canvas.transform.TransformPoint(newPos);
			}

			// m_RectTransform.position = Input.mousePosition;
			base.Show ();
		}

		protected virtual MenuItem AddMenuItem (Sprite icon)
		{
			MenuItem item = itemCache.Find (x => !x.isActiveAndEnabled);
			if (item == null) {
				item = Instantiate (m_Item) as MenuItem;
				itemCache.Add (item);
			}
			if (item.targetGraphic != null && item.targetGraphic is Image) {
				(item.targetGraphic as Image).overrideSprite = icon;
			}
			item.onTrigger.RemoveAllListeners ();
			item.gameObject.SetActive (true);
			item.transform.SetParent (m_RectTransform, false);
			return item;
		}
	}
}