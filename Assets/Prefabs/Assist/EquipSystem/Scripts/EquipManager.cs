using UnityEngine;

// EquipSystem 매니저 (완전 독립 standalone). 소켓 + 볼륨-핏으로 악세서리를 캐릭터에 장착/해제.
// 기존 Accessory/CharManager 등 어떤 스크립트에도 의존하지 않음.
public class EquipManager : MonoBehaviour
{
    private static EquipManager instance;  // 싱글톤 인스턴스
    public static EquipManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 인스턴스가 없으면 찾아서 할당
                instance = FindObjectOfType<EquipManager>();
            }

            return instance;
        }
    }

    [SerializeField] private EquipCatalog catalog;  // 아이템 카탈로그 (key→프리팹). 인스펙터 지정 우선.

    // 카탈로그 미지정 시 Resources에서 자동 로드 (인스펙터 지정이 우선)
    private void Awake()
    {
        if (catalog == null)
        {
            catalog = Resources.Load<EquipCatalog>("EquipCatalog_Demo");
        }
    }

    // 카탈로그 키로 target 캐릭터의 슬롯에 악세서리 장착 (같은 슬롯 기존 장착물 교체)
    public void Equip(GameObject target, string key)
    {
        if (target == null)
        {
            return;
        }

        if (catalog == null)
        {
            Debug.LogWarning("[EquipManager] Catalog가 지정되지 않았습니다.");
            return;
        }

        EquipEntry entry = catalog.Get(key);
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"[EquipManager] 카탈로그에 키 없음/프리팹 없음: {key}");
            return;
        }

        EquipSocket socket = EquipSocket.Find(target, entry.targetSlotId);
        if (socket == null)
        {
            Debug.LogWarning($"[EquipManager] 소켓 없음: slotId='{entry.targetSlotId}' on {target.name}");
            return;
        }

        // 이 소켓의 기존 장착물(표식) 제거
        ClearEquipped(socket.transform);

        // 프리팹 인스턴스화 + 볼륨-핏 배치 (런타임/에디터 공유 로직)
        GameObject inst = Instantiate(entry.prefab);
        EquipPlacement.Fit(inst, socket, entry.fitBias, entry.positionOffset, entry.rotationOffset);

        // 장착물 표식 부착 (해제 시 식별용)
        inst.AddComponent<EquipMarker>();
    }

    // target의 지정 슬롯 장착물 해제
    public void Unequip(GameObject target, string slotId)
    {
        if (target == null)
        {
            return;
        }

        EquipSocket socket = EquipSocket.Find(target, slotId);
        if (socket == null)
        {
            return;
        }

        ClearEquipped(socket.transform);
    }

    // 소켓 하위에서 장착물(표식)만 제거 (placeholder 등 다른 자식 보존)
    private void ClearEquipped(Transform socketTransform)
    {
        EquipMarker[] marks = socketTransform.GetComponentsInChildren<EquipMarker>(true);
        foreach (EquipMarker mark in marks)
        {
            if (mark != null)
            {
                Destroy(mark.gameObject);
            }
        }
    }
}
