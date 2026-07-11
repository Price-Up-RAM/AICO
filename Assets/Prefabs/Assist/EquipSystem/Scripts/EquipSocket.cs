using UnityEngine;

// EquipSystem 전용 소켓 (Accessory 시스템과 완전 독립). 본 하위 GO에 붙는 "자리의 이름표" —
// 실제 부착점(위치/크기 기준 refDist)은 자식 placeholder가 가진다.
[DisallowMultipleComponent]
public class EquipSocket : MonoBehaviour
{
    public string slotId;  // 슬롯 식별자 ("hairpin" 등) — 카탈로그·전파·해석 사다리의 열쇠

    // 이 소켓의 placeholder를 id로 탐색 (하위, 없으면 null)
    public EquipPlaceholder FindPlaceholder(string placeholderId)
    {
        if (string.IsNullOrEmpty(placeholderId))
        {
            return null;
        }

        EquipPlaceholder[] placeholders = GetComponentsInChildren<EquipPlaceholder>(true);
        foreach (EquipPlaceholder ph in placeholders)
        {
            if (ph != null && NormalizePlaceholderId(ph.placeholderId) == NormalizePlaceholderId(placeholderId))
            {
                return ph;
            }
        }
        return null;
    }

    // 부착점 id 별칭: 구 규약 "spot" = 신 규약 "placeholder" (기존에 구운 프리팹 데이터 호환).
    // placeholderId를 직접 비교하는 모든 코드는 이 정규화를 거쳐야 한다 (전파 매칭 포함).
    public static string NormalizePlaceholderId(string placeholderId)
    {
        if (placeholderId == "spot")
        {
            return "placeholder";
        }
        return placeholderId;
    }

    // 캐릭터 계층에서 slotId로 소켓 탐색 (없으면 null)
    public static EquipSocket Find(GameObject character, string slotId)
    {
        if (character == null || string.IsNullOrEmpty(slotId))
        {
            return null;
        }

        EquipSocket[] sockets = character.GetComponentsInChildren<EquipSocket>(true);
        foreach (EquipSocket socket in sockets)
        {
            if (socket != null && socket.slotId == slotId)
            {
                // slotId 일치 소켓 반환
                return socket;
            }
        }

        return null;
    }
}

// 소켓에 장착된 악세서리 인스턴스 표식 (해제 시 이 표식이 붙은 자식만 제거)
public class EquipMarker : MonoBehaviour
{
}
