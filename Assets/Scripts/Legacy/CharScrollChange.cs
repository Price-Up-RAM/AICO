using UnityEngine;
using UnityEngine.EventSystems;

/**
스크롤로 캐릭터 변경 -> Test용
*/
public class CharScrollChange : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private bool isPointerOverCharacter = true;

    void Update()
    {
        if (isPointerOverCharacter)
        {
            // 마우스 휠 스크롤 감지
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (scroll > 0f)
            {
                // 마우스 휠이 위로 스크롤 될 때 다음 캐릭터로 변경
                Debug.Log("go next");
                CharManager.Instance.ChangeNextChar();
            }
            else if (scroll < 0f)
            {
                // 마우스 휠이 아래로 스크롤 될 때 이전 캐릭터로 변경
                Debug.Log("go back");
                CharManager.Instance.ChangeBackChar();
            }
        }
    }

    // 마우스가 캐릭터 위에 들어왔을 때 호출
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Debug.Log("on");
        isPointerOverCharacter = true;
    }

    // 마우스가 캐릭터 위에서 나갔을 때 호출
    public void OnPointerExit(PointerEventData eventData)
    {
        // Debug.Log("exit");
        isPointerOverCharacter = false;
    }
}
