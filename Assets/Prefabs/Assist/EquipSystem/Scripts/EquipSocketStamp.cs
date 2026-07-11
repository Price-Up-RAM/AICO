using UnityEngine;

// 스탬프 마커: 이 소켓이 도너 전파로 생성됐음을 기록 (재스탬프 시 손보정 보호에 사용).
// 값 스냅샷과 현재 값이 다르면 사용자가 손으로 보정한 것으로 간주해 덮어쓰지 않는다.
public class EquipSocketStamp : MonoBehaviour
{
    public string sourceName;    // 출처 (도너 프리팹 이름)
    public string resolvedBy;    // 본 해석 방법 라벨 (DONOR 등)

    public Vector3 stampedLocalPosition;   // 스탬프 시점 로컬 위치
    public Vector3 stampedLocalEuler;      // 스탬프 시점 로컬 회전
    public float stampedRefDistLocal;      // 스탬프 시점 placeholder refDist (로컬 단위 잣대)

    // 현재 소켓 값이 스냅샷에서 벗어났는지 (= 사용자가 손으로 보정했는지).
    // 절대 오차는 극단 스케일(본 lossy 35 / 루트 20000)에서 무력화되므로,
    // 스탬프 시점 refDist(같은 로컬 단위)를 잣대로 한 "상대 오차"로 판정한다 — 스케일 불변.
    public bool IsHandTuned()
    {
        // 잣대: 스탬프 refDist, 없으면 스탬프 위치 크기 (둘 다 로컬 단위)
        float yardstick = Mathf.Max(stampedRefDistLocal, stampedLocalPosition.magnitude);

        float posDiff = (transform.localPosition - stampedLocalPosition).magnitude;
        float rotDiff = Quaternion.Angle(transform.localRotation, Quaternion.Euler(stampedLocalEuler));

        if (yardstick > 1e-12f)
        {
            // 위치가 잣대의 2% 이상 이동 = 손보정
            if (posDiff > yardstick * 0.02f)
            {
                return true;
            }
        }
        else
        {
            // 잣대가 없으면(원점 소켓 + refDist 미기록) 미세 절대 오차만 허용
            if (posDiff > 1e-6f)
            {
                return true;
            }
        }

        // 회전은 각도라 스케일 불변
        if (rotDiff > 0.5f)
        {
            return true;
        }

        // placeholder refDist가 스냅샷 대비 1% 이상 변경 = 손보정 (크기 튜닝도 보호)
        EquipPlaceholder ph = GetComponentInChildren<EquipPlaceholder>(true);
        if (ph != null && stampedRefDistLocal > 1e-12f)
        {
            if (Mathf.Abs(ph.bakedRefDistLocal - stampedRefDistLocal) > stampedRefDistLocal * 0.01f)
            {
                return true;
            }
        }

        return false;
    }

    // 현재 소켓 값을 스냅샷으로 기록
    public void TakeSnapshot()
    {
        stampedLocalPosition = transform.localPosition;
        stampedLocalEuler = transform.localRotation.eulerAngles;

        stampedRefDistLocal = 0f;
        EquipPlaceholder ph = GetComponentInChildren<EquipPlaceholder>(true);
        if (ph != null)
        {
            stampedRefDistLocal = ph.bakedRefDistLocal;
        }
    }
}
