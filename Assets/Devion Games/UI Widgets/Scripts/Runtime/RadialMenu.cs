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
#if UNITY_ANDROID || UNITY_EDITOR
			// MR: 항목 클릭은 MenuItem(IPointerClickHandler)이 PointableCanvasModule을 통해 이미 받는다.
			// "타겟 위에서 뗐는지" 재확인하는 데스크톱 전용 제스처는 이식하지 않는다 —
			// 남은 것은 "메뉴 밖 선택 시 닫기"뿐이다.
			// MR: 위젯 쪽 "메뉴 밖 선택 시 닫기"는 **의도적으로 비웠다** (2026-08-18 결정).
			//
			// PointableCanvasModule.WhenSelected는 **허공(비-UI) 선택에는 발생하지 않는다**(§4-13).
			// 그래서 이 경로로는 "빈 공간을 눌러 닫기"가 원리적으로 불가능하고, 반대로
			// 손 레이가 **다른 캔버스를 스치기만 해도** "바깥 선택"으로 잡혀 메뉴가 제멋대로 닫힌다.
			// 실측(2026-08-18): 라디얼 메뉴가 열리자마자 Update()에서 반복 Close() —
			// 콜스택 RadialMenu:Update() → Close() 로 확인.
			//
			// Flush()나 유예 시간으로 우회하려 했으나 둘 다 실패했다. 근본적으로
			// **허공 판정을 여기서 할 수 없다**는 것이 문제다.
			//
			// 닫기는 Phase 4-A의 MRIntentRouter가 담당한다 — 진리표가 이미 "빈 공간 + 탭"을
			// 판정하므로 허공 판정을 거기 한 벌만 둔다. 항목 클릭으로 닫히는 경로는 그대로 살아 있다.
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
#if UNITY_ANDROID || UNITY_EDITOR
			// (임시·진단) 라디얼 메뉴가 열자마자 사라지는 원인을 특정하기 위한 로그.
			// Unity가 Debug.Log에 콜스택을 함께 찍으므로 **누가 Close를 불렀는지**가 그대로 드러난다.
			// 후보: ① Update의 바깥 선택 판정 ② ContextMenu.Close()의 서브메뉴 연쇄
			//       ③ MenuItem.onTrigger 리스너 ④ 그 외
			// 원인 확인 후 이 로그는 제거할 것.
			UnityEngine.Debug.Log($"[MRRadialClose] Close() 호출됨. alpha={m_CanvasGroup.alpha:F2} " +
			                      $"showing={IsVisible} target={(m_Target != null ? m_Target.name : "null")}");
#endif
            base.Close();
			this.m_Target = null;
        }

        public override void Show ()
		{
			if (characterTransformPos != Vector2.zero) {
				m_RectTransform.anchoredPosition = characterTransformPos;
			} else {
#if UNITY_ANDROID || UNITY_EDITOR
				// MR: 마우스가 없다. characterTransformPos를 항상 명시적으로 넘겨줄 것 —
				// 이 폴백은 안전한 기본값(캔버스 로컬 원점)일 뿐이다.
				m_RectTransform.anchoredPosition = Vector2.zero;
#else
				m_RectTransform.position = Input.mousePosition;
#endif
			}
			base.Show ();
#if UNITY_ANDROID || UNITY_EDITOR
			// 이 메뉴를 연 클릭이 닫기 신호로 소비되지 않게 한다.
			// Flush만으로는 부족하다 — WhenSelected가 클릭 콜백 **뒤에** 발행되므로
			// 유예 시간을 함께 둔다.
			MRPointerBridge.Flush();
#endif
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
