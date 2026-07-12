using UnityEngine;

// 칠윗유 모드 테스트용 : 숫자 7 입력 시 토글
public class ChillModeTestManager : MonoBehaviour
{
    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            Debug.Log("[ChillMode] 7 키 입력 감지됨");
            ChillModeManager.Instance.ToggleChillMode();
        }
#endif
    }
}
