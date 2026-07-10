using UnityEngine;

// 소켓 생성(클릭=배치) 순간의 고스트 결과를 소켓-로컬 TRS로 기록하는 데이터 컴포넌트.
// "이 소켓은 어떤 악세서리를 어떤 위치/회전/크기로 보면서 만들었나"가 남아
// 이후 재현·전파 검수·미세조정 시작값 등으로 활용할 수 있다. (동작 없음 — 순수 데이터)
[DisallowMultipleComponent]
public class EquipPlacementRecord : MonoBehaviour
{
    public string accessoryKey;         // 배치에 쓴 카탈로그 key
    public float sizeRatioAtPlacement;  // 배치 시점의 sizeRatio
    public Vector3 ghostLocalPosition;  // 고스트 최종 위치 (소켓-로컬)
    public Vector3 ghostLocalEuler;     // 고스트 최종 회전 (소켓-로컬 오일러)
    public float ghostLocalScale;       // 고스트 최종 uniform 스케일 (소켓 lossy 대비)
}
