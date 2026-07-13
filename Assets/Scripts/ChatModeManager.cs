using UnityEngine;

// ChatMode 열거형 - 대화 모드 종류
public enum ChatMode
{
    Chat,       // 기본: 메인 캐릭터 1:1 대화
    Aropla,     // 아로나+프라나 3자 대화
    Operator,   // Operator(아로나)만 표시, 메인 캐릭터 숨김
    Pomodoro    // 포모도로(집중): 캐릭터 착석 + 타이머 UI 표시 + 채팅 차단
}

// ChatMode 중앙 관리자 (라우팅 전용)
// 모드 전환 요청을 받아 해당 Manager에 위임
// 외부에서는 이 Manager만 호출해야 함
public class ChatModeManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    private static ChatModeManager instance;
    public static ChatModeManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ChatModeManager>();
            }
            return instance;
        }
    }

    // 현재 모드
    public ChatMode CurrentMode { get; private set; } = ChatMode.Chat;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    // 모드 설정 (전환) - 유일한 진입점
    public void SetMode(ChatMode newMode)
    {
        if (CurrentMode == newMode) return;

        ChatMode previousMode = CurrentMode;
        Debug.Log($"[ChatModeManager] Switching mode: {previousMode} → {newMode}");

        // 1. 현재 모드 종료 (해당 Manager에 위임) — Exit 실패로 모드 상태가 영구히 꼬이지 않도록 방호
        try
        {
            ExitMode(previousMode);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChatModeManager] ExitMode({previousMode}) 예외 — 기본 모드로 강제 복귀: {e}");
        }

        // 2. 기본 모드(Chat) 기저 상태를 경유 — 모드 간 직접 전환으로 인한 상태 꼬임 방지
        //    (Enter 도중 CurrentMode를 조회하는 코드가 이전 모드를 보지 않도록 보장)
        CurrentMode = ChatMode.Chat;

        // 3. 새 모드 진입 (해당 Manager에 위임) — 실패하면 Chat 기저 상태 유지
        bool entered = EnterMode(newMode);
        CurrentMode = entered ? newMode : ChatMode.Chat;
        if (!entered)
        {
            Debug.LogWarning($"[ChatModeManager] EnterMode({newMode}) 실패 — Chat 모드로 유지");
        }
    }

    // 특정 모드 토글 (이미 해당 모드면 Chat으로 복귀)
    public void ToggleMode(ChatMode targetMode)
    {
        if (CurrentMode == targetMode)
        {
            SetMode(ChatMode.Chat);
        }
        else
        {
            SetMode(targetMode);
        }
    }

    // 모드 종료 처리 (위임)
    private void ExitMode(ChatMode mode)
    {
        switch (mode)
        {
            case ChatMode.Chat:
                // Chat 모드는 종료 시 특별한 처리 없음
                break;
            case ChatMode.Aropla:
                // ApiMultiConversationManager에 위임
                APIAroPlaManager.Instance.StopAroplaChannel();
                // ApiMultiConversationManager.Instance.StopMultiConversation();
                break;
            case ChatMode.Operator:
                // OperatorModeManager에 위임
                OperatorModeManager.Instance.ExitOperatorMode();
                break;
            case ChatMode.Pomodoro:
                // 착석 해제 + 타이머 UI 숨김 (타이머 로직은 건드리지 않음 — 표시만 제어)
                if (ChillModeManager.Instance != null)
                {
                    ChillModeManager.Instance.ExitChillMode();
                }
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ClosePomodoro();
                }
                break;
        }
    }

    // 모드 진입 처리 (위임). 실패 시 false 반환 → SetMode가 Chat 기저 상태를 유지한다.
    private bool EnterMode(ChatMode mode)
    {
        switch (mode)
        {
            case ChatMode.Chat:
                // Chat 모드는 진입 시 특별한 처리 없음 (각 Exit에서 복원 처리)
                break;
            case ChatMode.Aropla:
                // ApiMultiConversationManager에 위임
                APIAroPlaManager.Instance.StartAroplaChannel();
                // ApiMultiConversationManager.Instance.StartMultiConversation();
                break;
            case ChatMode.Operator:
                // OperatorModeManager에 위임
                OperatorModeManager.Instance.EnterOperatorMode();
                break;
            case ChatMode.Pomodoro:
                // ChillModeManager에 착석 위임 + 타이머 UI 표시 (시작은 유저 수동).
                // 착석 실패 시 모드를 확정하지 않는다 (착석 없이 채팅만 차단되는 유령 상태 방지).
                if (ChillModeManager.Instance == null)
                {
                    Debug.LogWarning("[ChatModeManager] Pomodoro 진입 실패: ChillModeManager가 씬에 없음");
                    return false;
                }
                ChillModeManager.Instance.EnterChillMode();
                if (!ChillModeManager.Instance.IsChillMode)
                {
                    Debug.LogWarning("[ChatModeManager] Pomodoro 진입 실패: 착석 불가 (참조/캐릭터 확인 필요)");
                    return false;
                }
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowPomodoro();
                }
                break;
        }
        return true;
    }

    // ============ 편의 메서드 ============
    public bool IsOperatorMode() => CurrentMode == ChatMode.Operator;
    public bool IsAroplaMode() => CurrentMode == ChatMode.Aropla;
    public bool IsChatMode() => CurrentMode == ChatMode.Chat;
    public bool IsPomodoroMode() => CurrentMode == ChatMode.Pomodoro;
}
