// DevionGames UI Widgets(ContextMenu / RadialMenu 등)가 데스크톱 마우스를 직접 폴링하던 부분을
// MR(ISDK 손 레이/포크)에서도 동작하게 중계하는 다리.
//
// 왜 별도 폴더 + 별도 asmdef인가
// ----------------------------
// Assets/Devion Games/UI Widgets/.../DevionGames.UIWidgets.asmdef는 독립 어셈블리라
// Assembly-CSharp(Assets/Scripts/MR/의 나머지 스크립트가 속한 기본 어셈블리)을 참조할 수 없다
// (컴파일 순서상 커스텀 asmdef가 Assembly-CSharp보다 먼저 컴파일되므로 원천적으로 불가능하다).
// 이 파일만 별도의 작은 asmdef(AICO.MR.PointerBridge)로 분리해 DevionGames.UIWidgets.asmdef가
// 참조할 수 있게 했다. Assets/Scripts/MR/의 다른 스크립트들(게임 매니저 타입에 의존)은
// 그대로 Assembly-CSharp에 둔다 — 전부 옮기면 CharManager/UIManager 등 수백 개 참조가 깨진다.
//
// 배경 (MR_Phase3-2_Canvas_Plan.md §3-2-D)
// --------------------------------------
// DevionGames 위젯은 렌더링·레이아웃은 이미 World Space에 대응돼 있다(ContextMenu.cs:303 참고).
// 깨지는 부분은 딱 하나 — "포인터 위치를 어디서 얻는가"다. 그중에서도 실제 버튼 클릭은
// MenuItem이 IPointerClickHandler를 구현하고 있어 PointableCanvasModule이 알아서 배달해준다
// (건드릴 필요 없음). 문제는 "메뉴 밖을 클릭하면 닫는다"처럼 EventSystem 표준 이벤트에
// 대응하는 콜백이 없는 폴링 로직뿐이다.
//
// 왜 전역 마우스 좌표를 대신 계산하지 않는가
// ----------------------------------------
// PointableCanvasModule은 포인터마다 임시 가상 카메라를 만들어 캔버스별로 다른 스크린 좌표계를
// 쓴다(PointableCanvasModule.cs UpdateRaycasts 참고). 즉 "전역 마우스 좌표" 같은 단일 값이 없다.
// 대신 이 클래스는 PointableCanvasModule.WhenSelected 정적 이벤트(어떤 캔버스든 핀치/포크로
// 선택이 발생했다는 신호)만 구독해 "이번 프레임에 무언가가 선택됐는가 / 어디를 선택했는가"만
// 기록한다. "메뉴 밖 클릭 감지" 용도로는 이 정도 정보로 충분하다.
//
// 한계
// ----
// 허공(3D 월드, UI가 없는 곳)을 포크/핀치해도 이 이벤트는 발생하지 않는다 — ISDK가 캔버스
// 상호작용에만 이 이벤트를 쏘기 때문이다. 즉 "메뉴 밖 허공을 찔러도 안 닫히는" 경우가 있을 수
// 있다. 데스크톱의 "화면 아무 데나 클릭하면 닫힘"과 완전히 동일하지는 않다.
// 완전한 등가가 필요해지면 닫기 버튼이나 타임아웃을 병행하는 편이 낫다(설계서 §3-2-D 참고).
//
// 데스크톱 동작은 이 파일이 존재해도 전혀 바뀌지 않는다 — 호출부는 #if UNITY_ANDROID로
// 감싸져 있어 이 브릿지는 MR 빌드에서만 쓰인다.

using UnityEngine;
using Oculus.Interaction;

public static class MRPointerBridge
{
    public static bool SelectedThisFrame { get; private set; }
    public static GameObject LastSelected { get; private set; }

    private static bool _subscribed;

    // 여러 위젯이 각자 호출해도 구독은 한 번만 된다.
    public static void EnsureSubscribed()
    {
        if (_subscribed) return;
        PointableCanvasModule.WhenSelected += OnSelected;
        _subscribed = true;
    }

    private static void OnSelected(PointableCanvasEventArgs args)
    {
        SelectedThisFrame = true;
        LastSelected = args.Hovered; // null일 수 있다 — 캔버스 안이지만 선택 가능한 요소가 아닌 지점
    }

    /// <summary>
    /// 이번 프레임에 "이 rectTransform 바깥"에서 선택이 발생했으면 true를 반환하고 플래그를 소비한다.
    /// 호출부(위젯의 Update)에서 프레임당 한 번씩 불러 쓰는 것을 전제로 한다.
    /// </summary>
    public static bool ConsumeSelectedOutside(RectTransform bounds)
    {
        if (!SelectedThisFrame) return false;
        SelectedThisFrame = false;

        if (LastSelected == null) return false; // 어디를 선택했는지 알 수 없으면 안전하게 "바깥 아님"으로 취급
        if (bounds == null) return true;

        return !LastSelected.transform.IsChildOf(bounds);
    }
}
