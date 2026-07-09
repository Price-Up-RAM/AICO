using System;
using System.Collections.Generic;
using UnityEngine;

// 키 → 지급 아이템 키 매핑 (데모에서 숫자키로 MAIN에 지급)
[Serializable]
public class InventoryGrantBinding
{
    public KeyCode key;    // 입력 키
    public string itemKey; // 카탈로그 키
}

// InventorySystem 데모 컨트롤러: 시작 시 활성 캐릭터 지정 + 키 입력으로 아이템 지급/패널 토글 (데모씬 테스트용)
public class InventoryDemoController : MonoBehaviour
{
    public GameObject target;              // 데모 캐릭터
    public string charcode = "arona_poc";  // 데모 캐릭터 charcode
    public InventoryView mainView;         // MAIN 인벤토리 창
    public InventoryView charView;         // CHAR 인벤토리 창
    public KeyCode toggleKey = KeyCode.I;  // 창 토글 키 (두 창 동시)
    public List<InventoryGrantBinding> grants = new List<InventoryGrantBinding>();  // 숫자키 → MAIN 지급

    // 시작 시 활성 캐릭터 지정
    private void Start()
    {
        if (InventorySystemManager.Instance == null)
        {
            Debug.LogWarning("[InventoryDemoController] InventorySystemManager가 씬에 없습니다.");
            return;
        }

        InventorySystemManager.Instance.SetActiveOwner(charcode, target);
    }

    // 키 입력 처리 (지급 + 토글)
    private void Update()
    {
        foreach (InventoryGrantBinding grant in grants)
        {
            if (grant != null && Input.GetKeyDown(grant.key))
            {
                DoGrant(grant.itemKey);
            }
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (mainView != null)
            {
                mainView.Toggle();
            }

            if (charView != null)
            {
                charView.Toggle();
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
        Debug.Log($"[InventoryDemoController] AddToMain('{itemKey}') → {ok}");
    }
}
