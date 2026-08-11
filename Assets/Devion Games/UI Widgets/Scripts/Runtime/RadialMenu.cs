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
		public Vector2 characterTransformPos;

		private List<MenuItem> itemCache = new List<MenuItem> ();
		private GameObject m_Target;

		protected override void Update ()
		{
			base.Update();
#if UNITY_ANDROID && !UNITY_EDITOR
			// MR: 항목 클릭은 MenuItem(IPointerClickHandler)이 PointableCanvasModule을 통해 이미 받는다.
			// "타겟 위에서 뗐는지" 재확인하는 데스크톱 전용 제스처는 이식하지 않는다 —
			// 남은 것은 "메뉴 밖 선택 시 닫기"뿐이다.
			if (m_CanvasGroup.alpha > 0f) {
				MRPointerBridge.EnsureSubscribed();
				if (MRPointerBridge.ConsumeSelectedOutside(m_RectTransform)) {
					Close();
				}
			}
#else
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
#endif
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
			if (characterTransformPos != Vector2.zero) {
				m_RectTransform.anchoredPosition = characterTransformPos;
			} else {
#if UNITY_ANDROID && !UNITY_EDITOR
				// MR: 마우스가 없다. characterTransformPos를 항상 명시적으로 넘겨줄 것 —
				// 이 폴백은 안전한 기본값(캔버스 로컬 원점)일 뿐이다.
				m_RectTransform.anchoredPosition = Vector2.zero;
#else
				m_RectTransform.position = Input.mousePosition;
#endif
			}
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
