using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace DevionGames.UIWidgets
{
    public class ContextMenu : UIWidget
    {
        [Header("Reference")]
        [SerializeField]
        protected MenuItem m_MenuItemPrefab = null;

        protected List<MenuItem> itemCache = new List<MenuItem>();
        private ContextMenu _contextMenuSub;

        private ContextMenu m_ContextMenuSub
        {
            get
            {
                if (_contextMenuSub == null)
                {
                    _contextMenuSub = WidgetUtility.Find<ContextMenu>("ContextMenuSub");
                    if (_contextMenuSub == null)
                    {
                        Debug.LogError("[ContextMenu] 'ContextMenuSub' 프리팹이 씬에 존재하지 않습니다.");
                    }
                }
                return _contextMenuSub;
            }
        }

        // Hover 상태 확인을 위한 전역 변수
        private bool isHoveringSubMenu = false;
        
        // 현재 열린 submenu의 부모 항목 추적
        private string currentOpenSubMenuParent = null;
        
        // 클릭으로 고정된 상태인지 추적
        private bool isClickFixed = false;
        
		// 각각 sub메뉴마다 저장
		private class SubMenuData
		{
			public int Count;
			public List<(string label, UnityAction action)> Items;
		}
		private Dictionary<string, SubMenuData> subMenuMap = new Dictionary<string, SubMenuData>();


        public override void Show()
		{
			// 보여질 메뉴 수 계산
			int menu_num = 0;
			foreach (var item in itemCache)
				if (item.gameObject.activeSelf) menu_num++;

			// 마우스 좌표를 캔버스 기준 좌표로 변환
            Canvas _canvas = FindObjectOfType<Canvas>();
			RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, _canvas.worldCamera, out Vector2 pos);

			// 위치 보정
			float bottomBound = -canvasRect.rect.height / 2;
			float heightBound = canvasRect.rect.height / 2;
			pos.y = Mathf.Clamp(pos.y + (50 * menu_num) + 10, bottomBound, heightBound);
			Vector3 newPos = new Vector3(pos.x, pos.y, -350);
			m_RectTransform.position = _canvas.transform.TransformPoint(newPos);

			base.Show();
		}

        // 서브메뉴 > parent 이동 후 위치 계산
        public void ShowSubFromParent(RectTransform parentItem, int subMenuCount)
        {
            if (m_RectTransform == null)
                m_RectTransform = GetComponent<RectTransform>();

            // 서브메뉴를 부모 메뉴 항목의 자식으로 설정
            m_RectTransform.SetParent(parentItem, false);

            // 피벗을 좌하단으로 고정 (0, 0)
            m_RectTransform.pivot = new Vector2(0f, 0f);

            // 좌하단 기준 오프셋 설정
            // Vector3 localPos = new Vector3(82f, -15f, -350f);
            Vector3 localPos = new Vector3(parentItem.rect.width/2 + 2f, parentItem.rect.height/-2, 0f);
            m_RectTransform.localPosition = localPos;

            base.ShowWithoutScaling();
        }

        public virtual MenuItem AddSubMenuItem(string text, List<(string label, UnityAction action)> subItems)
        {
            MenuItem parentItem = AddMenuItem(text, null);

            // Arrow 오브젝트 찾기 및 활성화
            Transform arrowTransform = parentItem.transform.Find("Arrow");
            if (arrowTransform != null)
            {
                arrowTransform.gameObject.SetActive(true);
            }

            // 서브 메뉴 매핑
            subMenuMap[text] = new SubMenuData
            {
                Count = subItems.Count,
                Items = subItems
            };

            var trigger = parentItem.gameObject.GetComponent<EventTrigger>() ?? parentItem.gameObject.AddComponent<EventTrigger>();

            // 공통 submenu 열기 동작 (hover)
            UnityAction<BaseEventData> showSubMenuAction = (data) =>
            {
                if (!subMenuMap.TryGetValue(text, out var mappedItems)) return;

                // 이미 같은 submenu가 열려있으면 무시
                if (currentOpenSubMenuParent == text && m_ContextMenuSub.IsVisible)
                {
                    return;
                }

                m_ContextMenuSub.Clear();
                foreach (var (label, action) in mappedItems.Items)
                    m_ContextMenuSub.AddMenuItem(label, action);

                isHoveringSubMenu = true;
                currentOpenSubMenuParent = text; // 현재 열린 submenu의 부모 추적
                m_ContextMenuSub.ShowSubFromParent(parentItem.GetComponent<RectTransform>(), mappedItems.Count);
            };

            // 클릭으로 submenu 고정/해제 동작
            UnityAction<BaseEventData> toggleSubMenuAction = (data) =>
            {
                if (!subMenuMap.TryGetValue(text, out var mappedItems)) return;

                // 이미 클릭으로 고정된 상태에서 같은 항목을 다시 클릭하면 닫기
                if (m_ContextMenuSub.IsVisible && currentOpenSubMenuParent == text && isClickFixed)
                {
                    m_ContextMenuSub.Close();
                    currentOpenSubMenuParent = null;
                    isClickFixed = false;
                    return;
                }

                // hover로 열린 상태거나 다른 submenu → 클릭으로 고정
                if (m_ContextMenuSub.IsVisible && currentOpenSubMenuParent == text)
                {
                    // 이미 열린 상태를 클릭으로 고정
                    isClickFixed = true;
                }
                else
                {
                    // 다른 submenu 열기 및 클릭으로 고정
                    showSubMenuAction(data);
                    isClickFixed = true;
                }
            };

            // 공통: hover로 submenu 전환
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener((data) => 
            {
                // hover로 다른 submenu로 전환하면 클릭 고정 해제
                if (currentOpenSubMenuParent != text)
                {
                    isClickFixed = false;
                }
                showSubMenuAction(data);
            });
            trigger.triggers.Add(enter);

            // 공통: exit 시 hover 상태 해제 (클릭으로 고정된 것은 그대로 유지)
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener((data) =>
            {
                isHoveringSubMenu = false;
                // 클릭으로 고정된 상태가 아닐 때만 자동 닫힘 시도
                parentItem.StartCoroutine(DelayedCloseIfNotHovering(m_ContextMenuSub));
            });
            trigger.triggers.Add(exit);

            // 공통: 클릭으로 고정/해제
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(toggleSubMenuAction);
            trigger.triggers.Add(click);

            return parentItem;
        }

        private IEnumerator DelayedCloseIfNotHovering(ContextMenu subMenu)
        {
            // 짧은 시간 대기
            yield return new WaitForSeconds(0.15f);

            // 대기 중 다시 Hover 중이거나 클릭으로 고정된 상태라면 닫지 않음
            if (!isHoveringSubMenu && !isClickFixed)
            {
                subMenu.Close();
                currentOpenSubMenuParent = null;
            }
        }

        public virtual void Clear()
        {
            // 메뉴 캐시 모두 비활성화
            foreach (var item in itemCache)
            {
                // 화살표 비활성화 (서브메뉴 흔적 제거)
                var arrow = item.transform.Find("Arrow");
                if (arrow != null)
                {
                    arrow.gameObject.SetActive(false);
                }

                // 기존 이벤트 제거
                var trigger = item.gameObject.GetComponent<EventTrigger>();
                if (trigger != null)
                {
                    trigger.triggers.Clear();
                }

                item.onTrigger.RemoveAllListeners();
                item.gameObject.SetActive(false);
            }

            // 현재 열린 submenu 추적 초기화
            currentOpenSubMenuParent = null;
            isClickFixed = false;

            // 서브메뉴도 클리어 및 닫기
            if (_contextMenuSub != null)
            {
                _contextMenuSub.Clear();
                _contextMenuSub.Close();
            }
        }

        public virtual MenuItem AddMenuItem(string text, UnityAction used)
        {
            // 비활성화된 아이템 재사용 또는 새로 생성
            MenuItem item = itemCache.Find(x => !x.gameObject.activeSelf);
            if (item == null)
            {
                item = Instantiate(m_MenuItemPrefab);
                itemCache.Add(item);
            }

            // 텍스트 및 색상 설정
            Text labelText = item.GetComponentInChildren<Text>();
            labelText.text = text;

            // 기본 흰색 (활성 메뉴)
            labelText.color = Color.white;

            item.onTrigger.RemoveAllListeners();
            item.gameObject.SetActive(true);
            item.transform.SetParent(m_RectTransform, false);


            if (used != null) // 이벤트 등록
            {
                item.onTrigger.AddListener(() =>
                {
                    Close();

                    // 메인 메뉴 찾은 후 닫기
                    var mainMenu = WidgetUtility.Find<ContextMenu>("ContextMenu");
                    if (mainMenu != null && mainMenu.IsVisible)
                    {
                        mainMenu.Close();
                    }

                    used?.Invoke();
                });
            }   
            else  // 이벤트도 없고, Arrow Object도 없으면 회색처리
            { 
                // Arrow 오브젝트 찾기 및 활성화
                Transform arrowTransform = item.transform.Find("Arrow");
                if (arrowTransform == null) // 서브메뉴를 가진 메뉴는 무반응
                {
                    // 이벤트 없으면 비활성 스타일 (회색 글씨, 클릭 없음)
                    labelText.color = new Color(0.5f, 0.5f, 0.5f);  // 회색
                    item.onTrigger.RemoveAllListeners(); // 혹시 모르니 재차 비움
                }
            }

            return item;
        }

        protected override void Update()
        {
            // 기본 UIWidget 업데이트
            base.Update();

            // 클릭 외부 감지 → 닫기
            if (m_CanvasGroup.alpha > 0f && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)))
            {
                PointerEventData pointer = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };
                var raycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycastResults);

                foreach (var result in raycastResults)
                {
                    MenuItem item = result.gameObject.GetComponent<MenuItem>();
                    if (item != null)
                    {
                        item.OnPointerClick(pointer);
                        return;
                    }
                }

                Close();
            }
        }
    }
}
