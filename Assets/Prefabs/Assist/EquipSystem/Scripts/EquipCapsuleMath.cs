using UnityEngine;

// 캡슐 표면 좌표 수학 (순수 정적). placeholder의 무차원 인코딩(axisT/dirLocal/radiusScale)과
// 캡슐 로컬 좌표 사이의 변환. 캡슐 부피를 조절하면 Decode 결과가 따라 움직인다(placeholder 재활용의 핵심).
public static class EquipCapsuleMath
{
    // Transform lossyScale 절대값 평균 (0 방지) — 런타임 공용
    public static float LossyAvg(Transform t)
    {
        Vector3 ls = t.lossyScale;
        float avg = (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f;
        if (avg <= 1e-8f)
        {
            return 1f;
        }
        return avg;
    }

    // 캡슐 축 단위벡터 (로컬)
    public static Vector3 AxisVector(CapsuleCollider cap)
    {
        if (cap.direction == 0)
        {
            return Vector3.right;
        }
        if (cap.direction == 2)
        {
            return Vector3.forward;
        }
        return Vector3.up;
    }

    // 캡슐 내부 세그먼트 반길이 (구는 0으로 퇴화)
    public static float HalfSegmentLength(CapsuleCollider cap)
    {
        float half = cap.height * 0.5f - cap.radius;
        if (half < 0f)
        {
            return 0f;
        }
        return half;
    }

    // 캡슐 로컬 점 → 무차원 표면 좌표 (axisT: 축 위 [-1..1], dirLocal: 축 최근접점→점 방향, radiusScale: 거리/반경)
    public static void Encode(CapsuleCollider cap, Vector3 localPoint, out float axisT, out Vector3 dirLocal, out float radiusScale)
    {
        Vector3 axis = AxisVector(cap);
        float halfLen = HalfSegmentLength(cap);

        Vector3 rel = localPoint - cap.center;
        float tRaw = Vector3.Dot(rel, axis);
        if (tRaw > halfLen)
        {
            tRaw = halfLen;
        }
        if (tRaw < -halfLen)
        {
            tRaw = -halfLen;
        }

        if (halfLen > 1e-8f)
        {
            axisT = tRaw / halfLen;
        }
        else
        {
            axisT = 0f;
        }

        Vector3 closest = cap.center + axis * tRaw;
        Vector3 radial = localPoint - closest;
        float dist = radial.magnitude;

        if (dist > 1e-8f)
        {
            dirLocal = radial / dist;
        }
        else
        {
            // 점이 축 위에 있으면 축 수직 방향으로 폴백
            dirLocal = Vector3.Cross(axis, Vector3.right);
            if (dirLocal.sqrMagnitude < 1e-8f)
            {
                dirLocal = Vector3.Cross(axis, Vector3.forward);
            }
            dirLocal = dirLocal.normalized;
        }

        if (cap.radius > 1e-8f)
        {
            radiusScale = dist / cap.radius;
        }
        else
        {
            radiusScale = 0f;
        }
    }

    // 무차원 표면 좌표 → 캡슐 로컬 점 (radiusScale 1=표면, >1=부유, 0=축)
    public static Vector3 Decode(CapsuleCollider cap, float axisT, Vector3 dirLocal, float radiusScale)
    {
        Vector3 axis = AxisVector(cap);
        float halfLen = HalfSegmentLength(cap);

        Vector3 closest = cap.center + axis * (axisT * halfLen);
        Vector3 dir = dirLocal;
        if (dir.sqrMagnitude < 1e-8f)
        {
            dir = Vector3.up;
        }
        return closest + dir.normalized * (cap.radius * radiusScale);
    }

    // 점을 캡슐 표면(radiusScale=1)으로 스냅
    public static Vector3 SnapToSurface(CapsuleCollider cap, Vector3 localPoint)
    {
        float axisT;
        Vector3 dirLocal;
        float radiusScale;
        Encode(cap, localPoint, out axisT, out dirLocal, out radiusScale);
        return Decode(cap, axisT, dirLocal, 1f);
    }
}
