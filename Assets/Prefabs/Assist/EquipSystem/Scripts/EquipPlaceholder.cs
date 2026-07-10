using UnityEngine;

// placeholder 회전 규약
public enum EquipPlaceholderOrientation
{
    SurfaceAligned,  // up = 표면 바깥 방향(dirLocal) — 모자/헤어핀 기본
    SocketFrame,     // 소켓 프레임 그대로 — 천사링 등
}

// 악세서리 바닥 접촉 규약
public enum EquipContactAnchor
{
    BottomAlign,  // 악세서리 바운드의 바닥(-up 접면)을 placeholder 점에 정렬 — 파묻힘 방지
    Center,       // 바운드 중심을 placeholder 점에 정렬
    Pivot,        // 모델 원점(0,0,0)을 placeholder 점에 정렬 — 피벗 기준으로 저작된 악세서리(레거시 equip 감각) 기본
}

// placeholder = 소켓 캡슐 테두리(또는 근처)의 부착점. 악세서리는 여기에 붙는다.
// 위치는 무차원 표면 좌표(axisT/dirLocal/radiusScale)가 원본이고 Transform은 파생 캐시 —
// 캡슐 부피를 조절하면 ApplyToTransform으로 표면을 따라 재배치(재활용)된다.
[DisallowMultipleComponent]
public class EquipPlaceholder : MonoBehaviour
{
    [Tooltip("부착점 이름 — 카탈로그/런타임이 이 이름으로 부착점을 찾습니다. 신모델 규약: placeholder (구명 spot 별칭 호환)")]
    public string placeholderId;  // "placeholder"(신모델 규약), 레거시: "top", "side_l" 등

    // 무차원 표면 좌표 (캡슐 기반 — 캡슐 있는 소켓에서만 사용)
    [Tooltip("캡슐(레거시) 소켓 전용 — 캡슐 축 위 위치 [-1..1]. refDist 신모델에서는 사용되지 않습니다")]
    public float axisT;                         // 캡슐 축 위 위치 [-1..1]
    [Tooltip("캡슐(레거시) 전용 — 축 최근접점→점 방향 (신모델 미사용)")]
    public Vector3 dirLocal = Vector3.up;       // 소켓 로컬, 축 최근접점→점 방향
    [Tooltip("캡슐(레거시) 전용 — 1=표면, >1=부유(천사링), 0=축 위 (신모델 미사용)")]
    public float radiusScale = 1f;              // 1=표면, >1=부유(천사링), 0=축 위

    // 신모델(클릭=소켓) 크기 기준: 본(소켓)→메시 히트점 거리의 부모-로컬 베이크.
    // 부모-로컬이라 캐릭터/본이 커지면 lossyScale을 타고 악세서리 크기가 자동으로 같이 큰다. (0=미베이크)
    [Tooltip("크기 기준 거리(본→표면, 부모-로컬). 악세서리 최장변 = 이 값 × 2 × sizeRatio(카탈로그). 메시 글라이드를 놓는 순간 자동 재측정 — 손으로 고쳐도 됩니다. 0이면 장착 거부")]
    public float bakedRefDistLocal;

    [Tooltip("회전 규약(레거시 참고용): SurfaceAligned=표면 노멀 기준 / SocketFrame=소켓 프레임 그대로. 신모델은 이 Transform의 회전이 그대로 쓰입니다")]
    public EquipPlaceholderOrientation orientation = EquipPlaceholderOrientation.SurfaceAligned;
    [Tooltip("캡슐(레거시) 전용 회전 보정 — 신모델은 부착점 Transform을 직접 돌리거나 카탈로그 rotationOffset을 사용하세요")]
    public Vector3 rotationOffsetEuler;         // 규약 기준 회전 보정
    [Tooltip("접촉 규약: Pivot=모델 원점을 부착점에(신모델 기본), Center=바운드 중심, BottomAlign=바닥면을 표면에 대고 밀어올림(파묻힘 방지)")]
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

    // 무차원 좌표 → Transform 반영 (캡슐 크기 변경 후 재배치에도 사용)
    public void ApplyToTransform()
    {
        EquipSocket socket = OwnerSocket;
        if (socket == null)
        {
            return;
        }

        CapsuleCollider cap = socket.SizingVolume as CapsuleCollider;
        if (cap == null)
        {
            return;
        }

        transform.localPosition = EquipCapsuleMath.Decode(cap, axisT, dirLocal, radiusScale);
        transform.localRotation = ComputeLocalRotation();
        transform.localScale = Vector3.one;
    }

    // 현재 Transform → 무차원 좌표 캡처 (드래그 저작 후 호출)
    public void CaptureFromTransform()
    {
        EquipSocket socket = OwnerSocket;
        if (socket == null)
        {
            return;
        }

        CapsuleCollider cap = socket.SizingVolume as CapsuleCollider;
        if (cap == null)
        {
            return;
        }

        EquipCapsuleMath.Encode(cap, transform.localPosition, out axisT, out dirLocal, out radiusScale);

        // 회전 보정 = 규약 기준 회전에서의 편차
        Quaternion baseRot = ComputeBaseRotation();
        rotationOffsetEuler = (Quaternion.Inverse(baseRot) * transform.localRotation).eulerAngles;
    }

    // 규약 기준 회전 (보정 제외)
    private Quaternion ComputeBaseRotation()
    {
        if (orientation == EquipPlaceholderOrientation.SurfaceAligned)
        {
            Vector3 up = dirLocal;
            if (up.sqrMagnitude < 1e-8f)
            {
                up = Vector3.up;
            }

            Vector3 forward = Vector3.Cross(up, Vector3.right);
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = Vector3.Cross(up, Vector3.forward);
            }
            return Quaternion.LookRotation(forward.normalized, up.normalized);
        }

        return Quaternion.identity;
    }

    // 최종 로컬 회전 = 규약 기준 × 보정
    public Quaternion ComputeLocalRotation()
    {
        return ComputeBaseRotation() * Quaternion.Euler(rotationOffsetEuler);
    }
}
