using System;
using System.Collections.Generic;
using UnityEngine;

// 키 → 악세서리 키 매핑 (데모에서 숫자키로 교체)
[Serializable]
public class EquipBinding
{
    public KeyCode key;         // 입력 키
    public string accessoryKey; // 카탈로그 키
}

// EquipSystem 데모 컨트롤러: 대상 캐릭터에 키 입력으로 악세서리 장착·교체·해제 (데모씬 테스트용)
public class EquipDemoController : MonoBehaviour
{
    public GameObject target;  // 장착 대상 캐릭터
    public List<EquipBinding> bindings = new List<EquipBinding>();  // 숫자키 → 악세서리 교체
    public KeyCode unequipKey = KeyCode.J;  // 해제 키
    public string unequipSlotId = "head1";  // 해제할 슬롯

    public bool equipOnStart = false;  // 시작 시 자동 장착
    public string accessoryKey = "hairpin_placeholder";  // 시작/기본 장착 키
    public string slotId = "hairpin";  // (호환용) 기본 슬롯

    // 시작 시 자동 장착
    private void Start()
    {
        if (equipOnStart)
        {
            DoEquip(accessoryKey);
        }
    }

    // 키 입력 처리 (바인딩 교체 + 해제)
    private void Update()
    {
        foreach (EquipBinding binding in bindings)
        {
            if (binding != null && Input.GetKeyDown(binding.key))
            {
                DoEquip(binding.accessoryKey);
            }
        }

        if (Input.GetKeyDown(unequipKey))
        {
            DoUnequip(unequipSlotId);
        }
    }

    // 장착 실행
    private void DoEquip(string key)
    {
        if (target == null)
        {
            return;
        }

        EquipManager.Instance.Equip(target, key);
        Debug.Log($"[EquipDemoController] Equip('{key}') on {target.name}");
    }

    // 해제 실행
    private void DoUnequip(string slot)
    {
        if (target == null)
        {
            return;
        }

        EquipManager.Instance.Unequip(target, slot);
        Debug.Log($"[EquipDemoController] Unequip('{slot}') on {target.name}");
    }
}
