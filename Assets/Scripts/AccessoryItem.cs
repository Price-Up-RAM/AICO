using Cleverous.VaultInventory;
using UnityEngine;

[CreateAssetMenu(fileName = "New AccessoryItem", menuName = "AICO/Accessory Item")]
public class AccessoryItem : UseableItem
{
    [Header("[Accessory]")]
    public string accessoryName; // AccessoryData의 AccessorySlotItem.accessoryName과 매칭
    public string slotName; // 비워두면 Equip이 아이템 검색으로 슬롯을 자동으로 찾음

    public override void UseBegin(IUseInventory user)
    {
        GameObject target = user.MyTransform.gameObject;
        string characterCode = target.GetComponent<CharAttributes>()?.charcode;

        // 이미 이 악세서리가 장착 중이면 벗기고, 아니면 장착 (인벤토리 클릭 = 토글)
        if (AccessoryManager.Instance.IsEquipped(characterCode, accessoryName, out string equippedSlotName))
        {
            AccessoryManager.Instance.UnEquip(target, equippedSlotName);
        }
        else
        {
            AccessoryManager.Instance.Equip(target, accessoryName, slotName);
        }
    }

    public override void UseCancel(IUseInventory user)
    {
        throw new System.NotImplementedException();
    }

    public override void UseFinish(IUseInventory user)
    {
        throw new System.NotImplementedException();
    }
}
