using UnityEngine;
using UnityEngine.EventSystems;

// 포모도로 모드 복귀 수단: 캐릭터가 화면에서 극소/비가시 상태여도 메뉴를 열 수 있게 하는 전역 입력 폴백.
// 콜라이더/Desk_Set 프리팹 수정 없이, 이미 존재하는 신호 3개의 조합으로 발동 영역을 판정한다:
//   1) ChatModeManager.IsPomodoroMode() — 포모도로 중에만
//   2) TransparentWindow.IsOnOpaquePixel — 커서가 렌더된(보이는) 픽셀 위 (앱 클릭 캡처와 동일 기준)
//   3) !EventSystem.IsPointerOverGameObject() — UI/캐릭터(PhysicsRaycaster 콜라이더 포함) 위가 아님
// 셋 다 참 = "포모도로 중 디오라마(책상 등)의 빈 픽셀 위" → 기존 캐릭터 메뉴와 동일한 제스처로 발동:
//   우클릭 즉시 / 더블클릭(0.3초) / 좌클릭 0.5초 홀드 (menutrigger와 동일 판정값)
// 발동 시 현재 캐릭터의 MenuTrigger.OpenMenu()로 포워딩(완전히 같은 메뉴가 커서 위치에 뜸),
// 포워딩 불가 시 SetMode(Chat)로 포모도로를 직접 해제(최후의 탈출구).
// ChillModeManager 프리팹에 부착 — 데모 씬은 ChatModeManager가 없어 게이트에서 자동 비활성.
public class PomodoroMenuFallback : MonoBehaviour
{
    private const float HoldTime = 0.5f;        // menutrigger의 좌클릭 홀드 판정과 동일
    private const float DoubleClickTime = 0.3f; // menutrigger의 더블클릭 판정과 동일

    private bool isLeftClickHeld;
    private float leftClickHoldTime;
    private float lastClickTime;
    private int clickCount;

    private void Update()
    {
        if (!IsPomodoroActive())
        {
            ResetLeftClick();
            clickCount = 0;
            return;
        }

        // 우클릭 즉시
        if (Input.GetMouseButtonDown(1) && IsCursorOnDeskArea())
        {
            OpenMenu();
            return;
        }

        // 좌클릭: 더블클릭 판정 + 홀드 시작
        if (Input.GetMouseButtonDown(0) && IsCursorOnDeskArea())
        {
            if (Time.time - lastClickTime < DoubleClickTime && clickCount == 1)
            {
                clickCount = 0;
                ResetLeftClick();
                OpenMenu();
                return;
            }
            clickCount = 1;
            lastClickTime = Time.time;
            isLeftClickHeld = true;
            leftClickHoldTime = 0f;
        }

        if (Input.GetMouseButtonUp(0))
        {
            ResetLeftClick();
        }

        // 좌클릭 홀드 — 드래그 중에는 무효 (menutrigger와 동일)
        if (isLeftClickHeld && (StatusManager.Instance == null || !StatusManager.Instance.IsDragging))
        {
            leftClickHoldTime += Time.deltaTime;
            if (leftClickHoldTime >= HoldTime)
            {
                ResetLeftClick();
                if (IsCursorOnDeskArea()) // 발동 시점에도 영역 재확인 (홀드 중 커서 이동 대비)
                {
                    OpenMenu();
                }
            }
        }

        // 더블클릭 시간 초과 시 카운트 리셋
        if (clickCount > 0 && Time.time - lastClickTime > DoubleClickTime)
        {
            clickCount = 0;
        }
    }

    private void ResetLeftClick()
    {
        isLeftClickHeld = false;
        leftClickHoldTime = 0f;
    }

    private bool IsPomodoroActive()
    {
        return ChatModeManager.Instance != null && ChatModeManager.Instance.IsPomodoroMode();
    }

    // "포모도로 중 보이는 디오라마의 빈 픽셀 위" 판정 — 근거는 클래스 주석 참고
    private bool IsCursorOnDeskArea()
    {
        // UI 패널/풍선/캐릭터 콜라이더(PhysicsRaycaster) 위면 각자의 핸들러가 처리한다
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        // 렌더된 픽셀 위인지 — 빌드에서는 투명 픽셀 클릭이 창을 통과하므로 이 가드는 주로 에디터용
        TransparentWindow window = TransparentWindow.Instance;
        if (window != null && !window.IsOnOpaquePixel)
        {
            return false;
        }
        return true;
    }

    private void OpenMenu()
    {
        // 현재 캐릭터의 메뉴로 포워딩 — 캐릭터가 안 보여도 컴포넌트는 살아 있어 동일한 메뉴가 뜬다
        GameObject character = CharManager.Instance != null ? CharManager.Instance.GetCurrentCharacter() : null;
        MenuTrigger menuTrigger = character != null ? character.GetComponent<MenuTrigger>() : null;
        if (menuTrigger != null)
        {
            menuTrigger.OpenMenu();
            return;
        }

        // 최후의 탈출구 — 메뉴를 열 수 없으면 포모도로를 해제해 조작 불능 상태를 방지
        Debug.LogWarning("[PomodoroMenuFallback] 캐릭터 MenuTrigger를 찾지 못해 포모도로를 해제합니다.");
        ChatModeManager.Instance.SetMode(ChatMode.Chat);
    }
}
