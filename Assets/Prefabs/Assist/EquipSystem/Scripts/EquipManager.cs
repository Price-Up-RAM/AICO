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
            catalog = Resources.Load<EquipCatalog>("EquipCatalog");
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

        // 소켓 해석 사다리: ① key와 같은 이름 ② targetSlotId ③ fallbackSlotIds 순서대로 ④ 장착 불가
        string slotId;
        int priority;
        EquipSocket socket = EquipSlotResolver.Resolve(target, entry, out slotId, out priority);
        if (socket == null)
        {
            string candidates = string.Join("/", EquipSlotResolver.Candidates(entry));
            Debug.LogWarning($"[EquipManager] 장착 불가: '{key}' — 후보 소켓({candidates}) 모두 없음 on {target.name}");
            return;
        }

        // placeholder 별칭 (overhead 레거시): targetSlotId가 overhead였고 head로 해석됐으면 top placeholder
        string placeholderId = entry.targetPlaceholderId;
        if (entry.targetSlotId == "overhead" && slotId == "head" && string.IsNullOrEmpty(placeholderId))
        {
            placeholderId = "top";
        }

        if (string.IsNullOrEmpty(placeholderId))
        {
            // 신모델 소켓(캡슐 없음): "placeholder" 부착점으로 자동 라우팅(구명 "spot"도 별칭 호환), 없으면 즉시 거부 (사용자 확정 정책)
            if (socket.SizingVolume == null)
            {
                EquipPlaceholder spot = socket.FindPlaceholder("placeholder");
                if (spot == null)
                {
                    Debug.LogWarning($"[EquipManager] '{slotId}' 소켓에 placeholder(부착점) 없음 — 장착 거부 (구 캐릭터는 신모델 재저작 필요).");
                    return;
                }

                ClearEquipped(spot.transform);
                GameObject spotInst = Instantiate(entry.prefab);
                EquipPlacement.FitToPlaceholder(spotInst, socket, spot, entry);
                if (spotInst != null)
                {
                    spotInst.AddComponent<EquipMarker>();
                }
                return;
            }

            // 레거시 경로: 소켓 직부착 (placeholder 하위 장착물은 보존)
            ClearEquippedSocketOnly(socket);

            GameObject inst = Instantiate(entry.prefab);
            EquipPlacement.Fit(inst, socket, entry.fitBias, entry.positionOffset, entry.rotationOffset);
            if (inst != null)
            {
                inst.AddComponent<EquipMarker>();
            }
            return;
        }

        // placeholder 경로
        EquipPlaceholder ph = socket.FindPlaceholder(placeholderId);
        if (ph == null)
        {
            // placeholder 미저작 캐릭터 폴백: 소켓 직부착 + 경고
            Debug.LogWarning($"[EquipManager] placeholder 없음: '{slotId}/{placeholderId}' on {target.name} — 소켓 직부착 폴백");
            ClearEquippedSocketOnly(socket);

            GameObject fallback = Instantiate(entry.prefab);
            EquipPlacement.Fit(fallback, socket, entry.fitBias, entry.positionOffset, entry.rotationOffset);
            if (fallback != null)
            {
                fallback.AddComponent<EquipMarker>();
            }
            return;
        }

        // placeholder 단위 교체 — 다른 placeholder의 장착물은 유지 (모자+헤어핀+링 동시 장착)
        ClearEquipped(ph.transform);

        GameObject phInst = Instantiate(entry.prefab);
        EquipPlacement.FitToPlaceholder(phInst, socket, ph, entry);
        if (phInst != null)
        {
            phInst.AddComponent<EquipMarker>();
        }
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

    // 소켓 직부착 장착물만 해제 (placeholder 하위는 보존)
    private void ClearEquippedSocketOnly(EquipSocket socket)
    {
        EquipMarker[] marks = socket.GetComponentsInChildren<EquipMarker>(true);
        foreach (EquipMarker mark in marks)
        {
            if (mark == null)
            {
                continue;
            }

            if (mark.GetComponentInParent<EquipPlaceholder>() != null)
            {
                continue;
            }
            Destroy(mark.gameObject);
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
