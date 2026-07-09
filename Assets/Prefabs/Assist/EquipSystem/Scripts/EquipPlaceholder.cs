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
    BottomAlign,  // 악세서리 바운드의 바닥(-up 접면)을 placeholder 점에 정렬 — 파묻힘 방지 기본
    Center,       // 바운드 중심을 placeholder 점에 정렬
}

// placeholder = 소켓 캡슐 테두리(또는 근처)의 부착점. 악세서리는 여기에 붙는다.
// 위치는 무차원 표면 좌표(axisT/dirLocal/radiusScale)가 원본이고 Transform은 파생 캐시 —
// 캡슐 부피를 조절하면 ApplyToTransform으로 표면을 따라 재배치(재활용)된다.
[DisallowMultipleComponent]
public class EquipPlaceholder : MonoBehaviour
{
    public string placeholderId;  // "top", "side_l", "side_r", "halo" 등

    // 무차원 표면 좌표 (원본)
    public float axisT;                         // 캡슐 축 위 위치 [-1..1]
    public Vector3 dirLocal = Vector3.up;       // 소켓 로컬, 축 최근접점→점 방향
    public float radiusScale = 1f;              // 1=표면, >1=부유(천사링), 0=축 위

    public EquipPlaceholderOrientation orientation = EquipPlaceholderOrientation.SurfaceAligned;
    public Vector3 rotationOffsetEuler;         // 규약 기준 회전 보정
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
