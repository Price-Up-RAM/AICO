using UnityEngine;

// EquipSystem 공용 순수 수학 — 캡슐 세계와 무관한 거처 (P4 캡슐 철거 후에도 남는다)
public static class EquipMath
{
    // lossyScale 3축 절대값 평균 — 스케일 정규화의 공용 기준 (0 근처면 1로 방어)
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
}
