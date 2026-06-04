using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 서브 메뉴 관리를 위한 클래스 (인스펙터에서 편집 가능하도록 Serializable 속성 부여)
[System.Serializable]
public class SubMenu
{
    public string menuName;      // 관리용 이름 (예: "오클루더", "캐릭터")
    public Button menuButton;    // 해당 메뉴를 열기 위한 탭 버튼
    public GameObject menuPanel; // 해당 메뉴의 내용이 담긴 패널
}

public class SettingsMenuController : MonoBehaviour
{
    [Header("메뉴 리스트")]
    public List<SubMenu> subMenus = new List<SubMenu>();

    [Header("버튼 상태 스프라이트")]
    public Sprite selectSprite;   // 눌린 상태의 스프라이트
    public Sprite deselectSprite; // 안 눌린 상태의 스프라이트

    void Start()
    {
        // 각 메뉴 버튼에 클릭 이벤트(리스너) 자동 할당
        for (int i = 0; i < subMenus.Count; i++)
        {
            int index = i; // 클로저(Closure) 이슈 방지를 위해 로컬 변수에 저장
            subMenus[i].menuButton.onClick.AddListener(() => OnMenuButtonClicked(index));
        }

        // 게임 시작 시 기본으로 첫 번째 메뉴를 활성화 (필요 시 수정 가능)
        if (subMenus.Count > 0)
        {
            OnMenuButtonClicked(0);
        }
    }

    // 버튼 클릭 시 실행되는 함수
    public void OnMenuButtonClicked(int selectedIndex)
    {
        for (int i = 0; i < subMenus.Count; i++)
        {
            bool isSelected = (i == selectedIndex);

            // 1. 해당 서브 메뉴 패널 켜기/끄기
            if (subMenus[i].menuPanel != null)
            {
                subMenus[i].menuPanel.SetActive(isSelected);
            }

            // 2. 버튼 스프라이트 교체 (Select / Deselect)
            if (subMenus[i].menuButton != null)
            {
                Image buttonImage = subMenus[i].menuButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.sprite = isSelected ? selectSprite : deselectSprite;
                }
            }
        }
    }
}