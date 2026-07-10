using UnityEngine;

// 볼륨-핏 방식: 악세서리를 소켓 볼륨(콜라이더)에 맞추는 방법
public enum EquipFitMode
{
    ContainUniform,  // 콜라이더 볼륨에 왜곡 없이 맞춤
    None             // 스케일 자동조정 안 함 (fitBias만)
}

// 악세서리 정렬 기준
public enum EquipAnchorPivot
{
    VolumeCenter,     // 콜라이더 볼륨 center 정렬
    PlaceholderChild  // placeholderAnchor 정렬
}

// EquipSystem 전용 소켓 (Accessory 시스템과 완전 독립). 본 하위 GO에 붙이고 같은 GO의 콜라이더를 사이징 볼륨으로 사용.
[DisallowMultipleComponent]
public class EquipSocket : MonoBehaviour
{
    public string slotId;  // 슬롯 식별자 ("hairpin" 등)
    public EquipFitMode fit = EquipFitMode.ContainUniform;  // 핏 방식
    public EquipAnchorPivot pivot = EquipAnchorPivot.VolumeCenter;  // 정렬 기준
    public Transform placeholderAnchor;  // PlaceholderChild일 때 정렬 기준

    // 사이징 볼륨으로 쓸 콜라이더 (같은 GO)
    public Collider SizingVolume
    {
        get
        {
            return GetComponent<Collider>();
        }
    }

    // 이 소켓의 placeholder를 id로 탐색 (직계 자식, 없으면 null)
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
