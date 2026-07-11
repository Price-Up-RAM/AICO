using UnityEngine;

// 악세서리 바닥 접촉 규약
public enum EquipContactAnchor
{
    BottomAlign,  // 악세서리 바운드의 바닥(-up 접면)을 placeholder 점에 정렬 — 파묻힘 방지
    Center,       // 바운드 중심을 placeholder 점에 정렬
    Pivot,        // 모델 원점(0,0,0)을 placeholder 점에 정렬 — 피벗 기준으로 저작된 악세서리(레거시 equip 감각) 기본
}

// placeholder = 소켓의 부착점. 악세서리는 여기에 붙는다.
// 위치·회전 = 이 Transform 그대로, 크기 기준 = bakedRefDistLocal (본→표면 거리의 부모-로컬 베이크 —
// 캐릭터/본이 커지면 lossyScale을 타고 악세서리 크기가 자동으로 같이 큰다).
[DisallowMultipleComponent]
public class EquipPlaceholder : MonoBehaviour
{
    [Tooltip("부착점 이름 — 카탈로그/런타임이 이 이름으로 부착점을 찾습니다. 규약: placeholder (구명 spot 별칭 호환)")]
    public string placeholderId;  // "placeholder" 규약 (구명 "spot" 별칭 호환)

    [Tooltip("크기 기준 거리(본→표면, 부모-로컬). 악세서리 최장변 = 이 값 × 2 × sizeRatio(카탈로그). 메시 글라이드를 놓는 순간 자동 재측정 — 손으로 고쳐도 됩니다. 0이면 장착 거부")]
    public float bakedRefDistLocal;  // 0 = 미베이크 (장착 거부)

    [Tooltip("접촉 규약: Pivot=모델 원점을 부착점에(기본), Center=바운드 중심, BottomAlign=바닥면을 표면에 대고 밀어올림(파묻힘 방지)")]
    public EquipContactAnchor contactAnchor = EquipContactAnchor.BottomAlign;

    // 부모 소켓 (placeholder는 소켓 GO의 자식)
    public EquipSocket OwnerSocket
    {
        get
        {
            if (transform.parent == null)
            {
                return null;
            }
            return transform.parent.GetComponent<EquipSocket>();
        }
    }

}
