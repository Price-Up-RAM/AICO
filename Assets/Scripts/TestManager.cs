using Cleverous.VaultInventory;
using UnityEngine;

public class TestManager : MonoBehaviour
{
    [Header("[Item Pickup Test] 6~9번 키로 순서대로 획득 (인벤토리에 추가)")]
    [SerializeField] private AccessoryItem[] pickupTestItems = new AccessoryItem[4];

    [Header("[Inventory UI Toggle Test] I키로 인벤토리 창 열기/닫기")]
    [SerializeField] private CanvasGroup inventoryPanel; // SetActive로 끄면 자식의 InventoryUi.Awake()가 씹혀서 OnPlayerSpawn 구독을 놓친다. CanvasGroup으로만 표시를 껐다 켠다

    private void Update()
    {
        // 인벤토리 창 열기/닫기 테스트 (I키)
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (inventoryPanel != null)
            {
                bool show = inventoryPanel.alpha == 0f;
                inventoryPanel.alpha = show ? 1f : 0f;
                inventoryPanel.interactable = show;
                inventoryPanel.blocksRaycasts = show;
            }
            else
            {
                Debug.LogWarning("inventoryPanel이 비어있습니다!");
            }
        }

        // 아이템 획득 테스트 (6~9번 키 -> pickupTestItems[0~3]을 인벤토리에 추가)
        for (int i = 0; i < pickupTestItems.Length && i < 4; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha6 + i))
            {
                GameObject currentCharacter = CharManager.Instance.GetCurrentCharacter();

                if (currentCharacter == null)
                {
                    Debug.LogWarning("현재 로드된 캐릭터가 없습니다!");
                    continue;
                }

                if (pickupTestItems[i] == null)
                {
                    Debug.LogWarning($"pickupTestItems[{i}]가 비어있습니다!");
                    continue;
                }

                Inventory inventory = currentCharacter.GetComponent<Inventory>();
                if (inventory == null)
                {
                    Debug.LogWarning("현재 캐릭터에 Inventory가 없습니다!");
                    continue;
                }

                inventory.DoAdd(new RootItemStack(pickupTestItems[i], 1));
                Debug.Log($"아이템 획득 테스트 실행 ({pickupTestItems[i].accessoryName})");
            }
        }
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
