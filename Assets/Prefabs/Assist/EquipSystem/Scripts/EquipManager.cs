using UnityEngine;

// EquipSystem 매니저 (완전 독립 standalone). 소켓 + placeholder(refDist)로 악세서리를 캐릭터에 장착/해제.
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
            catalog = Resources.Load<EquipCatalog>("EquipCatalog");
        }
    }

    // 카탈로그 키로 target 캐릭터의 슬롯에 악세서리 장착 (같은 슬롯 기존 장착물 교체)
    public void Equip(GameObject target, string key)
    {
        string reason;
        Equip(target, key, out reason);
    }

    // 장착 + 실패 사유 반환 — 데모/외부 UI가 사유를 화면에 표시할 수 있게. 성공 시 true, reason = null.
    public bool Equip(GameObject target, string key, out string reason)
    {
        reason = null;

        if (target == null)
        {
            reason = "target이 없습니다";
            return false;
        }

        if (catalog == null)
        {
            reason = "Catalog가 지정되지 않았습니다";
            Debug.LogWarning("[EquipManager] " + reason);
            return false;
        }

        EquipEntry entry = catalog.Get(key);
        if (entry == null || entry.prefab == null)
        {
            reason = $"카탈로그에 키 없음/프리팹 없음: {key}";
            Debug.LogWarning("[EquipManager] " + reason);
            return false;
        }

        // 소켓 해석 사다리: ① key와 같은 이름 ② targetSlotId ③ fallbackSlotIds 순서대로 ④ 장착 불가
        string slotId;
        int priority;
        EquipSocket socket = EquipSlotResolver.Resolve(target, entry, out slotId, out priority);
        if (socket == null)
        {
            string candidates = string.Join("/", EquipSlotResolver.Candidates(entry));
            reason = $"후보 소켓({candidates}) 모두 없음 — 캐릭터에 그 이름의 소켓을 만들어주세요";
            Debug.LogWarning($"[EquipManager] 장착 불가: '{key}' — {reason} on {target.name}");
            return false;
        }

        // 부착점 규약: 소켓당 "placeholder" 1개 (구명 "spot" 별칭 호환). 없으면 즉시 거부 (확정 정책).
        EquipPlaceholder ph = socket.FindPlaceholder("placeholder");
        if (ph == null)
        {
            reason = $"'{slotId}' 소켓에 placeholder(부착점) 없음 — Socket Maker로 재저작 필요";
            Debug.LogWarning("[EquipManager] " + reason);
            return false;
        }

        // placeholder 단위 교체 — 다른 소켓의 장착물은 유지 (모자+헤어핀+링 동시 장착)
        ClearEquipped(ph.transform);

        GameObject inst = Instantiate(entry.prefab);
        bool fitted = EquipPlacement.FitToPlaceholder(inst, socket, ph, entry);
        if (fitted == false)
        {
            // 배치 함수가 크기 기준 부재(refDist 미베이크)로 거부하면 인스턴스를 스스로 파괴함
            reason = "배치 거부(refDist 미베이크) — 콘솔 경고 확인";
            return false;
        }
        inst.AddComponent<EquipMarker>();
        return true;
    }

    // 특정 placeholder의 장착물만 해제
    public void Unequip(GameObject target, string slotId, string placeholderId)
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

        EquipPlaceholder ph = socket.FindPlaceholder(placeholderId);
        if (ph != null)
        {
            ClearEquipped(ph.transform);
        }
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
