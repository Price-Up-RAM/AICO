using Cleverous.VaultInventory;
using UnityEngine.EventSystems;

// 임시 구현: 좌클릭 시 아이템을 바로 사용(장착/해제)한다.
// 추후 드래그 앤 드롭 + 레이캐스트 방식으로 교체될 때까지의 임시 트리거.
public class ClickToUseItemUiPlug : ItemUiPlug
{
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            Interact();
            return;
        }

        base.OnPointerClick(eventData);

        RootItem item = GetReferenceVaultItemData();
        if (item is UseableItem useable)
        {
            useable.UseBegin(Ui.TargetInventory.InventoryOwner);
        }
    }
}
