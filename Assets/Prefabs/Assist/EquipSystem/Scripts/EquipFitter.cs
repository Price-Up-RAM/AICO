using UnityEngine;

// 악세서리를 refDist 크기 기준에 맞추기 위한 순수 계산 (EquipSystem 전용, 완전 독립)
public static class EquipFitter
{
    // 고유 크기 + 중심 + 반치수(extents) 측정 — 배치 보정을 결정적으로 계산하기 위한 것.
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
