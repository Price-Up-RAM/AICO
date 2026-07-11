using UnityEngine;

// 캐릭터 쪽 EquipSystem 진입점: 캐릭터 루트에 이 컴포넌트를 "추가하는 순간"(에디터 Reset 콜백)
// 원점(0,0,0)에 origin 소켓 + placeholder가 자동 생성된다.
// 용도: 표면이 없는 자리(오오라·이펙트류)의 시작점이자, "이 캐릭터는 장착 지원"의 표식.
// 크기 기준: 표면 히트가 없으므로 캐릭터 키의 5%를 refDist로 베이크 (원하면 placeholder에서 직접 수정).
[DisallowMultipleComponent]
public class EquipSocketController : MonoBehaviour
{
    public const string OriginSlotId = "origin";  // 부트스트랩 소켓의 slotId

    // 에디터에서 컴포넌트가 추가될 때 1회 호출 — origin 소켓 부트스트랩
    private void Reset()
    {
        CreateOriginSocket();
    }

    // origin 소켓 생성 (이미 있으면 보존하고 반환)
    public EquipSocket CreateOriginSocket()
    {
        EquipSocket existing = EquipSocket.Find(gameObject, OriginSlotId);
        if (existing != null)
        {
            Debug.Log($"[EquipSocketController] origin 소켓이 이미 있어 보존합니다 ({name}).");
            return existing;
        }

        GameObject socketGo = new GameObject("Socket_origin");
        socketGo.transform.SetParent(transform, false);
        socketGo.transform.localPosition = Vector3.zero;
        socketGo.transform.localRotation = Quaternion.identity;
        socketGo.transform.localScale = Vector3.one;

        EquipSocket socket = socketGo.AddComponent<EquipSocket>();
        socket.slotId = OriginSlotId;

        GameObject phGo = new GameObject("placeholder");
        phGo.transform.SetParent(socketGo.transform, false);
        EquipPlaceholder ph = phGo.AddComponent<EquipPlaceholder>();
        ph.placeholderId = "placeholder";
        ph.contactAnchor = EquipContactAnchor.Pivot;

        // refDist = 캐릭터 키 5% (월드) → 부모-로컬 환산 (캐릭터가 커지면 같이 커지는 규약 유지)
        float heightWorld = MeasureHeightWorld();
        float lossy = EquipMath.LossyAvg(socketGo.transform);
        ph.bakedRefDistLocal = heightWorld * 0.05f / lossy;

        Debug.Log($"[EquipSocketController] origin 소켓 생성: {name} → 원점(0,0,0), refDist≈{heightWorld * 0.05f:F2} 월드. placeholder에서 크기/위치를 조정하세요.");
        return socket;
    }

    // 캐릭터 키 측정 (활성 렌더러 월드 바운드 높이, 없으면 1)
    private float MeasureHeightWorld()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool has = false;
        Bounds bounds = new Bounds();

        foreach (Renderer r in renderers)
        {
            if (r == null)
            {
                continue;
            }

            if (has == false)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (has == false || bounds.size.y <= 1e-6f)
        {
            return 1f;
        }
        return bounds.size.y;
    }
}
