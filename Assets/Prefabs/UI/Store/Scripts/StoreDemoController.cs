using System;
using System.Collections.Generic;
using UnityEngine;

// 키 → 지급 아이템 키 매핑 (숫자키로 MAIN에 지급, 판매 시연용)
[Serializable]
public class StoreDemoGrant
{
    public KeyCode key;    // 입력 키
    public string itemKey; // 카탈로그 키
}

// Store 데모 컨트롤러: S=상점 토글 / I=인벤토리 토글 / G=+500G / 숫자키=아이템 지급 / 5=포즈 리롤 (레거시 Input)
public class StoreDemoController : MonoBehaviour
{
    [SerializeField] private StoreView storeView;              // S: 상점 창 토글
    [SerializeField] private InventoryView inventoryView;      // I: 인벤토리 창 토글
    [SerializeField] private KeyCode toggleStoreKey = KeyCode.S;
    [SerializeField] private KeyCode toggleInventoryKey = KeyCode.I;
    [SerializeField] private KeyCode grantGoldKey = KeyCode.G;  // +500G
    [SerializeField] private KeyCode rerollPoseKey = KeyCode.Alpha5;  // 포즈 프리뷰 강제 재캡처
    [SerializeField] private List<StoreDemoGrant> grants = new List<StoreDemoGrant>();  // 숫자키 → MAIN 지급

    // 씬에 참조가 비어 있으면 찾아서 채움
    private void Start()
    {
        if (storeView == null)
        {
            storeView = FindFirstObjectByType<StoreView>();
        }

        if (inventoryView == null)
        {
            inventoryView = FindFirstObjectByType<InventoryView>();
        }
    }

    // 키 입력 처리 (토글 + 골드 + 지급)
    private void Update()
    {
        if (Input.GetKeyDown(toggleStoreKey) && storeView != null)
        {
            storeView.Toggle();
        }

        if (Input.GetKeyDown(toggleInventoryKey) && inventoryView != null)
        {
            inventoryView.Toggle();
        }

        if (Input.GetKeyDown(grantGoldKey))
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddGold(500);
                Debug.Log("[Store][StoreDemoController] +500G");
            }
        }

        if (Input.GetKeyDown(rerollPoseKey))
        {
            if (StoreManager.Instance != null)
            {
                StoreManager.Instance.RerollPoses();
                Debug.Log("[Store][StoreDemoController] 포즈 리롤 요청");
            }
        }

        foreach (StoreDemoGrant grant in grants)
        {
            if (grant != null && Input.GetKeyDown(grant.key))
            {
                DoGrant(grant.itemKey);
            }
        }
    }

    // MAIN 지급 실행
    private void DoGrant(string itemKey)
    {
        if (InventorySystemManager.Instance == null)
        {
            return;
        }

        bool ok = InventorySystemManager.Instance.AddToMain(itemKey, 1);
        Debug.Log($"[Store][StoreDemoController] AddToMain('{itemKey}') → {ok}");
    }

#if UNITY_EDITOR
    // 데모 씬 빌더가 직렬화 참조를 연결할 때 사용 (에디터 전용)
    public void EditorSet(StoreView store, InventoryView inventory)
    {
        storeView = store;
        inventoryView = inventory;
    }
#endif
}
