// 판정기 — 탭 / 더블탭 / 홀드를 **여기 한 곳에서만** 판정한다
// (MR_Phase4A_Input_Plan.md §2-4, Port Plan §2-1 진리표 / §3-3 낙관적 실행)
//
// 진리표
// ------
// | 조준 대상 | 채널            | 탭        | 더블탭      | 홀드   |
// |----------|-----------------|-----------|-------------|--------|
// | 캐릭터    | palm-up (시선)   | 음성 입력  | 캐릭터 메뉴  | —      |
// | 캐릭터    | 손 레이 핀치     | 음성 입력  | 캐릭터 메뉴  | 드래그  |
// | 빈 공간   | palm-up (시선)   | 시스템 메뉴 | 닫기        | —      |
// | 빈 공간   | 손 레이 핀치     | —         | —           | —      |
//
// **빈 공간 + 손 레이 핀치가 아무 동작도 하지 않는 것이 안전 설계의 핵심이다.**
// 이 칸을 나중에 "편의 기능"으로 채우고 싶어질 텐데, 채우는 순간 UI 버튼을 누르려던
// 핀치가 전부 오발동한다. 채우지 말 것.
//
// 낙관적 탭 (§3-3)
// ---------------
// MR 핀치는 더블탭 판정에 0.45초가 필요한데, 그대로 기다리면 음성 입력이 매번 늦게 시작된다.
//   1) 첫 탭 → 음성 입력 **즉시** 시작
//   2) 0.45초 내 두 번째 탭 → 녹음을 **버리고**(MicrophoneManager.CancelRecording) 메뉴 오픈
//
// 열린 메뉴 닫기 (2026-08-18 확정)
// ------------------------------
// PointableCanvasModule.WhenSelected는 **허공 선택에 발생하지 않아서**(§4-13)
// DevionGames의 "메뉴 밖 클릭 시 닫기"가 MR에서 성립하지 않는다(실기 확인).
// 진리표가 이미 "빈 공간"을 판정하므로 허공 판정을 여기 한 벌만 두고 그 신호로 닫는다.

using UnityEngine;

public class MRIntentRouter : MonoBehaviour
{
    [Header("공급자 — 비우면 씬에서 찾는다")]
    [SerializeField] private MRGazeProvider gazeProvider;
    [SerializeField] private MRRayProvider rayProvider;

    [Header("소비자 — 비우면 씬에서 찾는다")]
    [SerializeField] private MRCharacterContextMenu characterMenu;
    [SerializeField] private MRSystemContextMenu systemMenu;

    [Tooltip("탭이 발생하면 닫는다. MR에서 툴팁은 조준을 막지 않도록 포인터 대상에서 빠져 있어 " +
             "자기 힘으로는 닫히지 않는다(설계서 §8-2).")]
    [SerializeField] private DevionGames.UIWidgets.Tooltip tooltip;

    [Header("판정 파라미터 (§2-4)")]
    [Tooltip("두 번째 탭을 기다리는 시간(초).")]
    [SerializeField] private float doubleTapWindow = 0.45f;

    [Tooltip("이 거리(m)를 넘게 움직이면 탭이 아니다.")]
    [SerializeField] private float tapMaxMove = 0.03f;

    [Tooltip("이 시간(초)을 넘게 누르고 있으면 홀드다.")]
    [SerializeField] private float holdThreshold = 0.35f;

    [Header("진단")]
    [SerializeField] private bool logIntents = true;

    /// <summary>스킨십 공급자. 구현체가 생기면 여기에 등록한다.
    /// 등록되지 않은 동안은 항상 "점유 없음"이다 — 규칙은 살아 있고 대상만 없는 상태다.</summary>
    public IMRSkinshipContactProvider SkinshipProvider { get; set; }

    /// <summary>홀드가 시작됐다 — 캐릭터 드래그(MRRayDragAdapter)가 구독한다.
    /// 어댑터가 아직 없으면 아무도 듣지 않는다(no-op).</summary>
    public event System.Action<MRRayProvider> OnCharacterHoldStarted;
    public event System.Action OnCharacterHoldEnded;

    // 채널별 상태. 공급자가 아니라 **여기**가 들고 있다.
    private class ChannelState
    {
        public bool wasPressed;
        public float pressTime;
        public Vector3 pressPoint;
        public bool pressedOnCharacter;

        /// <summary>홀드 임계를 넘겼다 — **탭으로 치지 않기 위한** 소비 플래그.
        /// 진리표에서 홀드 칸이 비어 있는 조합에서도 true가 된다.</summary>
        public bool holdFired;

        /// <summary>그 홀드가 실제 캐릭터 드래그인가. `holdFired`와 반드시 구분해야 한다 —
        /// 합쳐 두면 **빈 공간을 길게 눌렀다 떼도 드래그 종료 이벤트가 발사된다**
        /// (2026-08-19 로그로 발견. 설계서 §8-4).</summary>
        public bool holdIsDrag;

        public float lastTapTime = -999f;
        public bool lastTapOnCharacter;
    }

    private readonly ChannelState _gaze = new ChannelState();
    private readonly ChannelState _ray = new ChannelState();

    private void Update()
    {
        ResolveRefs();

        if (gazeProvider != null) Evaluate(gazeProvider, _gaze, isRayChannel: false);
        if (rayProvider != null) Evaluate(rayProvider, _ray, isRayChannel: true);
    }

    // =========================================================
    // 채널 판정
    // =========================================================
    private void Evaluate(IMRAimProvider provider, ChannelState st, bool isRayChannel)
    {
        // 최우선 규칙: 스킨십 접촉 중이면 조작 레이어를 **평가하지 않는다**(Port Plan §1-1).
        if (IsSkinshipEngaged(provider.Side))
        {
            ReleaseHoldIfAny(st, isRayChannel);
            st.wasPressed = false;
            return;
        }

        // 채널이 닫혀 있으면(예: palm-up이 아님) 누르고 있던 것도 정리한다.
        if (!provider.IsChannelActive)
        {
            ReleaseHoldIfAny(st, isRayChannel);
            st.wasPressed = false;
            return;
        }

        bool pressed = provider.IsPressed;

        // --- 누름 시작 ---
        if (pressed && !st.wasPressed)
        {
            st.pressTime = Time.unscaledTime;
            st.pressPoint = provider.PressPoint;
            st.pressedOnCharacter = provider.Aim.valid && provider.Aim.onCharacter;
            st.holdFired = false;
        }

        // --- 누르는 중 ---
        if (pressed && st.wasPressed && !st.holdFired)
        {
            bool longEnough = Time.unscaledTime - st.pressTime >= holdThreshold;

            // 홀드는 **캐릭터 + 손 레이**에서만 의미가 있다(진리표).
            if (longEnough && isRayChannel && st.pressedOnCharacter)
            {
                st.holdFired = true;
                st.holdIsDrag = true;
                Log("홀드 시작 — 캐릭터 드래그");
                OnCharacterHoldStarted?.Invoke(rayProvider);
            }
            else if (longEnough)
            {
                // 홀드 칸이 비어 있는 조합. 탭으로도 치지 않도록 소비만 해둔다.
                st.holdFired = true;
            }
        }

        // --- 뗌 ---
        if (!pressed && st.wasPressed)
        {
            if (st.holdFired)
            {
                ReleaseHoldIfAny(st, isRayChannel);
            }
            else
            {
                float moved = Vector3.Distance(provider.PressPoint, st.pressPoint);
                if (moved <= tapMaxMove) HandleTap(st, isRayChannel);
                else Log($"탭 아님 — {moved * 100f:F1} cm 움직임");
            }
        }

        st.wasPressed = pressed;
    }

    private void ReleaseHoldIfAny(ChannelState st, bool isRayChannel)
    {
        if (!st.holdFired) return;

        bool wasDrag = st.holdIsDrag;
        st.holdFired = false;
        st.holdIsDrag = false;

        // 진리표에서 홀드 칸이 빈 조합(빈 공간 등)은 시작을 알린 적이 없다.
        // 종료도 알리지 않는다 — 안 그러면 드래그 어댑터가 유령 드롭을 받는다.
        if (!wasDrag) return;

        Log("홀드 종료");
        OnCharacterHoldEnded?.Invoke();
    }

    // =========================================================
    // 탭 / 더블탭
    // =========================================================
    private void HandleTap(ChannelState st, bool isRayChannel)
    {
        // 어떤 탭이든 툴팁부터 닫는다 (§8-2).
        // 진입점이 여기 하나뿐이라 캐릭터/빈 공간, 시선/레이, UI 버튼 클릭까지 전부 덮인다 —
        // UI 버튼을 누르는 것도 결국 손 레이 핀치라 이 판정을 거치기 때문이다.
        CloseTooltip();

        bool onCharacter = st.pressedOnCharacter;
        float now = Time.unscaledTime;

        bool isDouble = now - st.lastTapTime <= doubleTapWindow &&
                        st.lastTapOnCharacter == onCharacter;

        if (isDouble)
        {
            st.lastTapTime = -999f;   // 세 번째 탭이 또 더블로 잡히지 않게 소비한다
            OnDoubleTap(onCharacter, isRayChannel);
            return;
        }

        st.lastTapTime = now;
        st.lastTapOnCharacter = onCharacter;
        OnSingleTap(onCharacter, isRayChannel);
    }

    private void OnSingleTap(bool onCharacter, bool isRayChannel)
    {
        if (onCharacter)
        {
            // 낙관적 실행 — 더블탭 판정을 기다리지 않고 바로 시작한다.
            Log("탭(캐릭터) → 음성 입력 시작");
            StartVoice();
            return;
        }

        // 빈 공간 + 손 레이 = 아무 동작 없음. **의도된 공백이다.**
        if (isRayChannel)
        {
            Log("탭(빈 공간, 손 레이) → 무시 (안전 설계)");
            return;
        }

        // 빈 공간 + 시선: 메뉴가 열려 있으면 그것부터 닫는다.
        // "메뉴 밖을 눌러 닫는" 데스크톱 습관이 그대로 통해야 하고,
        // 메뉴를 끄려던 탭이 시스템 메뉴를 여는 것도 막는다.
        if (characterMenu != null && characterMenu.IsAnyOpen)
        {
            Log("탭(빈 공간) → 열린 메뉴 닫기");
            characterMenu.CloseAll();
            return;
        }

        Log("탭(빈 공간) → 시스템 메뉴");
        OpenSystemMenu();
    }

    private void OnDoubleTap(bool onCharacter, bool isRayChannel)
    {
        if (onCharacter)
        {
            Log("더블탭(캐릭터) → 녹음 취소 + 캐릭터 메뉴");
            CancelVoice();
            if (characterMenu != null) characterMenu.Show();
            return;
        }

        if (isRayChannel) return;   // 빈 공간 + 손 레이는 더블탭도 무시

        Log("더블탭(빈 공간) → 전부 닫기");
        CloseEverything();
    }

    // =========================================================
    // 동작
    // =========================================================
    private void StartVoice()
    {
        if (MicrophoneManager.Instance == null)
        {
            Debug.LogWarning("[MRIntent] MicrophoneManager.Instance가 없습니다.");
            return;
        }
        MicrophoneManager.Instance.StartRecording();
    }

    private void CancelVoice()
    {
        if (MicrophoneManager.Instance == null) return;
        if (!MicrophoneManager.Instance.IsRecording()) return;
        MicrophoneManager.Instance.CancelRecording();
    }

    // 시스템 메뉴는 2026-08-19에 MRFloatingPanel 판에서 DevionGames ContextMenu로 바뀌었다
    // (MR_Phase4A_SystemMenu_Design.md §2). 역할을 "MR 전용 제어 + 패널 진입점"으로 좁히니
    // 항목이 전부 버튼이 되어 슬라이더를 담을 판이 필요 없어졌다.
    // 배치는 위젯의 placeInFrontOnShow가 담당하므로 여기서 손대지 않는다.
    private void OpenSystemMenu()
    {
        if (systemMenu == null)
        {
            Debug.LogWarning("[MRIntent] MRSystemContextMenu가 씬에 없습니다 — 시스템 메뉴를 열 수 없습니다.");
            return;
        }

        systemMenu.Show();
    }

    /// <summary>툴팁은 `UIWidget` 계열이라 "닫힘"이 alpha 0이다(§4-44).
    /// 이미 닫혀 있으면 부르지 않는다 — 매 탭마다 트윈을 다시 시작할 이유가 없다.</summary>
    private void CloseTooltip()
    {
        if (tooltip == null) return;
        if (!tooltip.IsVisible) return;

        Log("탭 → 툴팁 닫기");
        tooltip.Close();
    }

    private void CloseEverything()
    {
        if (characterMenu != null) characterMenu.CloseAll();

        if (systemMenu != null) systemMenu.CloseAll();
    }

    // =========================================================
    private bool IsSkinshipEngaged(MRHandSide side)
    {
        if (SkinshipProvider == null) return false;
        return SkinshipProvider.IsHandEngaged(side);
    }

    private void ResolveRefs()
    {
        if (gazeProvider == null) gazeProvider = FindFirstObjectByType<MRGazeProvider>();
        if (rayProvider == null) rayProvider = FindFirstObjectByType<MRRayProvider>();
        if (characterMenu == null) characterMenu = FindFirstObjectByType<MRCharacterContextMenu>();
        if (systemMenu == null) systemMenu = FindFirstObjectByType<MRSystemContextMenu>();
        if (tooltip == null) tooltip = FindFirstObjectByType<DevionGames.UIWidgets.Tooltip>();
    }

    private void Log(string message)
    {
        if (!logIntents) return;
        Debug.Log($"[MRIntent] {message}");
    }
}
