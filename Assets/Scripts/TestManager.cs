using UnityEngine;

public class TestManager : MonoBehaviour
{
    private void Update()
    {
        // 악세서리 장착/해제 테스트 진행
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            GameObject currentCharacter = CharManager.Instance.GetCurrentCharacter();
            
            if (currentCharacter != null)
            {
                AccessoryManager.Instance.Equip(currentCharacter, "arona_a_chipao");
                Debug.Log("악세서리 장착 테스트 실행 (arona_a_chipao)");
            }
            else
            {
                Debug.LogWarning("현재 로드된 캐릭터가 없습니다!");
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            GameObject currentCharacter = CharManager.Instance.GetCurrentCharacter();
            
            if (currentCharacter != null)
            {
                AccessoryManager.Instance.Equip(currentCharacter, "arona_a_idolfrontribbon");
                Debug.Log("악세서리 장착 테스트 실행 (arona_a_idolfrontribbon)");
            }
            else
            {
                Debug.LogWarning("현재 로드된 캐릭터가 없습니다!");
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            GameObject currentCharacter = CharManager.Instance.GetCurrentCharacter();
            
            if (currentCharacter != null)
            {
                AccessoryManager.Instance.Equip(currentCharacter, "arona_a_pareo");
                Debug.Log("악세서리 장착 테스트 실행 (arona_a_pareo)");
            }
            else
            {
                Debug.LogWarning("현재 로드된 캐릭터가 없습니다!");
            }
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            GameObject currentCharacter = CharManager.Instance.GetCurrentCharacter();
            
            if (currentCharacter != null)
            {
                // arona_a_accessory 악세서리를 장착 (이미 있으면 제거되도록 AccessoryManager에 구현됨)
                AccessoryManager.Instance.UnEquip(currentCharacter, "Slot_Head_1");
                Debug.Log("악세서리 장착 테스트 실행 (arona_a_chipao)");
            }
            else
            {
                Debug.LogWarning("현재 로드된 캐릭터가 없습니다!");
            }
        }
    }
}
