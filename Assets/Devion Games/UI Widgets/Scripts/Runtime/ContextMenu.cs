using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace DevionGames.UIWidgets
{
	public class ContextMenu : UIWidget, IPointerEnterHandler, IPointerExitHandler
	{
		public bool keepOpen = false;
		public MenuItem currentSubMenuTrigger;
		public bool isPointerOver = false;

		private ContextMenu m_SubMenuCache;
		private ContextMenu GetSubMenu() {
			if (m_SubMenuCache == null) m_SubMenuCache = WidgetUtility.Find<ContextMenu>("ContextMenuSub");
			return m_SubMenuCache;
		}

		private ContextMenu m_MainMenuCache;
		private ContextMenu GetMainMenu() {
			if (m_MainMenuCache == null) m_MainMenuCache = WidgetUtility.Find<ContextMenu>("ContextMenu");
			return m_MainMenuCache;
		}

		[Header ("Reference")]
		[SerializeField]
		protected MenuItem m_MenuItemPrefab= null;
		protected List<MenuItem> itemCache = new List<MenuItem> ();
		protected Canvas m_Canvas;


		public override void Show ()
		{
			base.Show ();
#if UNITY_ANDROID || UNITY_EDITOR
			MRPointerBridge.Flush();
#endif
		}

		public override void Close ()
		{
			base.Close ();
			keepOpen = false;
			if (currentSubMenuTrigger != null) {
				currentSubMenuTrigger.isLocked = false;
				currentSubMenuTrigger.ResetColor();
			}
			currentSubMenuTrigger = null;
			isPointerOver = false;

			for (int i = 0; i < itemCache.Count; i++) {
				if (itemCache[i] != null) {
					itemCache[i].isLocked = false;
					itemCache[i].ResetColor();
				}
			}
			
			ContextMenu subMenu = GetSubMenu();
			if (subMenu != null && subMenu != this && subMenu.IsVisible) {
				subMenu.Close();
			}
		}

		public void OnPointerEnter(PointerEventData eventData) {
			isPointerOver = true;
		}

		public void OnPointerExit(PointerEventData eventData) {
			isPointerOver = false;
		}

		private float hoverOutTimer = 0f;

		protected override void Update ()
		{
			base.Update();

			ContextMenu subMenu = GetSubMenu();
			if (subMenu != null && subMenu != this && subMenu.IsVisible && !subMenu.keepOpen) {
				bool isOverMenus = this.isPointerOver || subMenu.isPointerOver;
				
				if (!isOverMenus) {
					hoverOutTimer += Time.deltaTime;
					if (hoverOutTimer > 0.15f) {
						subMenu.Close();
						hoverOutTimer = 0f;
					}
				} else {
					hoverOutTimer = 0f;
				}
			} else {
				hoverOutTimer = 0f;
			}

#if UNITY_ANDROID || UNITY_EDITOR
			// MR: 마우스가 없다. 메뉴 항목 클릭 자체는 MenuItem이 IPointerClickHandler라
			// PointableCanvasModule이 알아서 배달해준다 — 여기서 다시 다룰 필요가 없다.
			// 남은 건 "메뉴 밖 선택 시 닫기"뿐이라 MRPointerBridge로만 대체한다.
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
			if (m_CanvasGroup.alpha > 0f && (Input.GetMouseButtonDown (0) || Input.GetMouseButtonDown (1) || Input.GetMouseButtonDown (2))) {

				var pointer = new PointerEventData (EventSystem.current);
				pointer.position = Input.mousePosition;
				var raycastResults = new List<RaycastResult> ();
				EventSystem.current.RaycastAll (pointer, raycastResults);

				for (int i = 0; i < raycastResults.Count; i++) {
					MenuItem item = raycastResults [i].gameObject.GetComponentInParent<MenuItem> ();
					if (item != null) {
						if (item.transform.IsChildOf (m_RectTransform)) {
							item.OnPointerClick (pointer);
						}
						return;
					}
				}

				Close ();
			}
#endif
		}

		public virtual void Clear ()
		{
			for (int i = 0; i < itemCache.Count; i++) {
				itemCache [i].gameObject.SetActive (false);
				itemCache [i].isLocked = false;
				itemCache [i].ResetColor();
			}

			ContextMenu subMenu = WidgetUtility.Find<ContextMenu> ("ContextMenuSub");
			if (subMenu != null && subMenu != this) {
				subMenu.Close ();
			}
		}

		public virtual MenuItem AddMenuItem (string text, UnityAction used)
		{
			MenuItem item = itemCache.Find (x => !x.gameObject.activeSelf);

			if (item == null) {
				// Debug.Log(text);
				item = Instantiate (m_MenuItemPrefab) as MenuItem;
				itemCache.Add (item);
			}
			Text itemText = item.GetComponentInChildren<Text> ();

			if (itemText != null) {
				itemText.text = text;
			}
			item.onTrigger.RemoveAllListeners ();
			item.onEnter.RemoveAllListeners ();
			item.onExit.RemoveAllListeners ();
			item.isSubMenuTrigger = false;

			item.onEnter.AddListener(delegate() {
				ContextMenu subMenu = GetSubMenu();
				if (subMenu != null && subMenu != this && !item.isSubMenuTrigger && !subMenu.keepOpen) {
					subMenu.Close();
				}
			});

			item.interactable = used != null;
			item.gameObject.SetActive (true);
			item.transform.SetParent (m_RectTransform, false);
			SetArrowVisible (item, false);
			if (used != null) {
				item.onTrigger.AddListener (delegate() {
					Close ();
					used.Invoke ();
				});
			}
			return item;
		}

		public virtual MenuItem AddSubMenuItem (string text, List<(string, UnityAction)> subMenuItems)
		{
			MenuItem item = AddMenuItem (text, delegate() {});
			item.onTrigger.RemoveAllListeners ();
			item.isSubMenuTrigger = true;
			item.interactable = subMenuItems != null && subMenuItems.Count > 0;
			SetArrowVisible (item, item.interactable);
			if (item.interactable) {
				UnityAction showSubMenu = delegate() {
					ContextMenu subMenu = GetSubMenu();
					if (subMenu == null || subMenu == this) {
						return;
					}

					if (subMenu.currentSubMenuTrigger == item && subMenu.IsVisible) {
						return;
					}

					if (subMenu.currentSubMenuTrigger != null && subMenu.currentSubMenuTrigger != item) {
						subMenu.currentSubMenuTrigger.isLocked = false;
						subMenu.currentSubMenuTrigger.ResetColor();
					}

					subMenu.currentSubMenuTrigger = item;

					subMenu.Clear ();
					for (int i = 0; i < subMenuItems.Count; i++) {
						string itemText = subMenuItems [i].Item1;
						UnityAction itemAction = subMenuItems [i].Item2;
						UnityAction wrappedAction = null;
						if (itemAction != null) {
							wrappedAction = delegate() {
								ContextMenu main = GetMainMenu();
								if (main != null) main.Close();
								
								ContextMenu sub = GetSubMenu();
								if (sub != null) sub.Close();

								itemAction.Invoke ();
							};
						}
						subMenu.AddMenuItem (itemText, wrappedAction);
					}

					subMenu.ShowNextTo (item);
				};

#if UNITY_ANDROID || UNITY_EDITOR
				// MR: hover로 서브메뉴를 열지 않는다. **클릭(onTrigger)만 쓴다.**
				//
				// 실기 2026-08-19: 하위 메뉴가 있는 항목을 레이로 겨누면 조준점이 0.5초 주기로
				// 튀어 항목을 고를 수 없었다. onEnter로 열린 서브메뉴의 판정 면이 곧바로 레이를
				// 가로채 부모 항목에 onExit가 발생하고, 그러면 서브메뉴가 닫혀 레이가 되돌아오며
				// 다시 onEnter가 뜨는 **진동 루프**다.
				//
				// 데스크톱은 커서가 서브메뉴 위로 "이동"하면 되지만 MR의 레이는 그 사이를
				// 연속적으로 지날 수 없다. §4-13("PointableCanvasModule에는 전역 마우스 좌표가 없다")과
				// 같은 뿌리다 — hover 의미론을 그대로 옮길 수 없다.
#else
				item.onEnter.AddListener (delegate() {
					ContextMenu subMenu = GetSubMenu();
					if (subMenu != null && !subMenu.keepOpen) {
						showSubMenu.Invoke();
					}
				});
#endif

				item.onTrigger.AddListener (delegate() {
					ContextMenu subMenu = GetSubMenu();
					if (subMenu != null) {
						subMenu.keepOpen = true;
						item.isLocked = true;
						item.SetLockedColor();
						showSubMenu.Invoke();
					}
				});
			}
			return item;
		}

		protected virtual void SetArrowVisible (MenuItem item, bool visible)
		{
			if (item == null) {
				return;
			}

			Transform arrow = item.transform.Find ("Arrow");
			if (arrow != null) {
				arrow.gameObject.SetActive (visible);
			}
		}

		public virtual void ShowAt (Vector3 position)
		{
			m_RectTransform.position = position;
			base.Show ();
		}

		public virtual void ShowNextTo (MenuItem item)
		{
			RectTransform itemTransform = item.GetComponent<RectTransform> ();
			if (itemTransform == null) {
				ShowAt (item.transform.position);
				return;
			}

			gameObject.SetActive (true);
			Canvas.ForceUpdateCanvases ();

			Vector3[] itemCorners = new Vector3[4];
			itemTransform.GetWorldCorners (itemCorners);
			Vector3 itemBottomRight = itemCorners [3];

			Vector3 subMenuBottomLeftOffset = new Vector3 (
				-m_RectTransform.pivot.x * m_RectTransform.rect.width,
				-m_RectTransform.pivot.y * m_RectTransform.rect.height,
				0f
			);

#if UNITY_ANDROID || UNITY_EDITOR
			// MR: 서브메뉴를 부모 항목과 **같은 회전**으로 맞춘 뒤 그 평면 위에서 옆으로 민다.
			//
			// 원본은 오프셋을 자기 부모(Widgets 그룹)의 회전으로 변환한다. 데스크톱에서는
			// 두 메뉴가 같은 캔버스 안이라 그게 곧 부모 항목의 평면이었다. MR에서는 메뉴마다
			// 독립 월드 캔버스라 회전이 제각각이고, 그 결과 서브메뉴가 **뒤로 꺾여** 생긴다
			// (실기 2026-08-19: 오른쪽에 뜨는데 몸 쪽으로 접혀 레이 패널에 가려짐).
			m_RectTransform.rotation = itemTransform.rotation;
			subMenuBottomLeftOffset = m_RectTransform.TransformVector (subMenuBottomLeftOffset);
#else
			if (m_RectTransform.parent != null) {
				subMenuBottomLeftOffset = m_RectTransform.parent.TransformVector (subMenuBottomLeftOffset);
			}
#endif

			m_RectTransform.position = itemBottomRight - subMenuBottomLeftOffset;
			m_IsShowing = false;
			m_CanvasGroup.alpha = 0f;
#if UNITY_ANDROID || UNITY_EDITOR
			// MR: 스케일을 0으로 만들지 않는다 — UIWidget.OnEnable(§4-39)과 같은 이유다.
			//
			// MR 포크는 UIWidget.Show()의 스케일 복원 트윈을 제거했다(월드 패널의 정상 스케일이
			// 0.001 수준이라 Vector3.one으로 트윈하면 1000배로 부푼다, §4-30).
			// 그래서 여기서 0으로 만들면 **되돌릴 코드가 없어 서브메뉴가 영구히 보이지 않는다.**
			// 실기 2026-08-19: 캐릭터 메뉴의 Chat / Mode를 눌러도 서브메뉴가 뜨지 않았다.
			//
			// UIWidget.cs 쪽은 이미 가드돼 있었는데 이 호출부만 빠져 있었다 —
			// **같은 처방을 호출부마다 따로 해야 하는 형태**라 놓치기 쉽다.
#else
			m_RectTransform.localScale = Vector3.zero;
#endif
			base.Show ();
		}

		public virtual void ShowAtScreenPosition (Vector2 screenPosition)
		{
			SetPositionFromScreenPoint (screenPosition);
			base.Show ();
		}

		protected virtual void SetPositionFromScreenPoint (Vector2 screenPosition)
		{
			if (this.m_Canvas == null) {
				this.m_Canvas = GetComponentInParent<Canvas> ();
			}

			if (this.m_Canvas == null) {
				m_RectTransform.position = screenPosition;
				return;
			}

			Vector2 localPosition;
			RectTransform canvasTransform = this.m_Canvas.transform as RectTransform;
			Camera canvasCamera = this.m_Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : this.m_Canvas.worldCamera;
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle (canvasTransform, screenPosition, canvasCamera, out localPosition)) {
				m_RectTransform.position = canvasTransform.TransformPoint (localPosition);
			} else {
				m_RectTransform.position = screenPosition;
			}
		}
	}
}
