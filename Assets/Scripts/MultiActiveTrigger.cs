using System;
using UnityEngine;

public class AverageFollower : MonoBehaviour
{
    [Header("추적할 두 오브젝트 (A, B)")]
    public Transform targetA;
    public Transform targetB;

    private void Awake()
    {
        // 시작할 때 A와 B에 상태 감지용(OnEnable/OnDisable) 보조 스크립트를 몰래 붙여줍니다.
        if (targetA != null) AttachNotifier(targetA.gameObject);
        if (targetB != null) AttachNotifier(targetB.gameObject);
        
        // 초기 활성화 상태 한 번 체크
        CheckState();
    }

    private void AttachNotifier(GameObject targetObj)
    {
        var notifier = targetObj.AddComponent<ActiveNotifier>();
        notifier.onStateChanged += CheckState;
    }

    // A나 B가 켜지거나 꺼질 때'만' 딱 한 번씩 호출됨
    private void CheckState()
    {
        if (targetA == null || targetB == null) return;

        // 둘 다 켜져 있을 때만 이 오브젝트(C)를 활성화
        bool isBothActive = targetA.gameObject.activeInHierarchy && targetB.gameObject.activeInHierarchy;
        
        if (gameObject.activeSelf != isBothActive)
        {
            gameObject.SetActive(isBothActive);
        }
    }

    // 이 Update는 C가 켜져 있을 때(A와 B가 모두 켜져 있을 때)만 실행됩니다!
    private void Update()
    {
        if (targetA == null || targetB == null) return;

        // 1. 위치 평균 (A와 B의 포지션을 더한 뒤 반으로 나눔)
        transform.position = (targetA.parent.position + targetB.parent.position) * 0.5f;

        // 2. 회전 평균 (Slerp를 0.5비율로 섞으면 정확히 중간 회전값이 됩니다)
        transform.rotation = Quaternion.Slerp(targetA.parent.rotation, targetB.parent.rotation, 0.5f);
    }

    // ==========================================
    // 내부에서만 사용하는 상태 감지용 보조 클래스
    // ==========================================
    public class ActiveNotifier : MonoBehaviour
    {
        public event Action onStateChanged;

        private void OnEnable() => onStateChanged?.Invoke();
        private void OnDisable() => onStateChanged?.Invoke();
    }
}