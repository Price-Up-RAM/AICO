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
            if (ChatModeManager.Instance != null)
            {
                // 본편: 모드 경유 토글 (채팅 차단/타이머 UI까지 일관 — 직접 착석 토글은 모드와 desync됨)
                ChatModeManager.Instance.ToggleMode(ChatMode.Pomodoro);
            }
            else if (ChillModeManager.Instance != null)
            {
                // 데모씬: ChatModeManager가 없으므로 착석만 토글
                ChillModeManager.Instance.ToggleChillMode();
            }
        }
#endif
    }
}
