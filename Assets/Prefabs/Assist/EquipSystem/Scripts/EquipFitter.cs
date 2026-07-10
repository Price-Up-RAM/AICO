using UnityEngine;

// 악세서리를 소켓 볼륨(콜라이더)에 맞추기 위한 순수 계산 (EquipSystem 전용, 완전 독립)
public static class EquipFitter
{
    // 콜라이더의 가장 긴 치수 (center 무시, 소켓 로컬 단위)
    public static float GetVolumeLength(Collider col)
    {
        if (col is CapsuleCollider cap)
        {
            // 캡슐: 높이와 지름 중 큰 값
            return Mathf.Max(cap.height, 2f * cap.radius);
        }

        if (col is BoxCollider box)
        {
            // 박스: 세 축 중 최대
            return Mathf.Max(box.size.x, Mathf.Max(box.size.y, box.size.z));
        }

        if (col is SphereCollider sph)
        {
            // 구: 지름
            return 2f * sph.radius;
        }

        return 0f;
    }

    // 콜라이더 center (소켓 로컬)
    public static Vector3 GetVolumeCenter(Collider col)
    {
        if (col is CapsuleCollider cap)
        {
            return cap.center;
        }

        if (col is BoxCollider box)
        {
            return box.center;
        }

        if (col is SphereCollider sph)
        {
            return sph.center;
        }

        return Vector3.zero;
    }

    // 원점/identity/scale1 상태 인스턴스의 렌더러 바운드 측정 (고유 크기). Renderer 없으면 false.
    public static bool MeasureNatural(GameObject inst, out float longest, out Vector3 center)
    {
        Vector3 extents;
        return MeasureNaturalFull(inst, out longest, out center, out extents);
    }

    // 고유 크기 + 중심 + 반치수(extents)까지 측정 — 배치 보정을 결정적으로 계산하기 위한 확장판.
    // (Renderer.bounds는 이동 직후 같은 프레임에 stale할 수 있어, 이 값을 TRS로 환산해 쓰는 것이 안전)
    public static bool MeasureNaturalFull(GameObject inst, out float longest, out Vector3 center, out Vector3 extents)
    {
        longest = 0f;
        center = Vector3.zero;
        extents = Vector3.zero;

        if (inst == null)
        {
            return false;
        }

        Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        bool has = false;
        Bounds bounds = new Bounds();

        // 모든 렌더러 바운드 합치기
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

        if (has == false)
        {
            return false;
        }

        longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        center = bounds.center;
        extents = bounds.extents;
        return true;
    }

    // 볼륨-핏 스케일 (uniform, 왜곡 없음). 볼륨 길이 / 고유 길이.
    public static float ComputeFitScale(float volumeLength, float naturalLength)
    {
        if (naturalLength <= 1e-6f || volumeLength <= 1e-6f)
        {
            return 1f;
        }

        return volumeLength / naturalLength;
    }
}
