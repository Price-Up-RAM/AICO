// 임시 진단용. Image_ChatBalloon (1) 버튼이 왜 반응 안 하는지 확인하기 위해
// PointableCanvasModule의 정적 이벤트를 전부 구독해서 로그로 남긴다.
// (원인 파악되면 삭제하거나 #if DEBUG로 감쌀 것 — MR_Phase_Kickoff_Guide.md §4에 기록 후 정리)
//
// 씬 아무 오브젝트에나 붙이면 된다(전역 정적 이벤트 구독이라 어디 붙어도 동일하게 동작).
//
// 확인 순서
// --------
// 1. WhenPointerStarted가 한 번도 안 찍히면: 손 자체가 포인터로 등록되지 않는 것 —
//    Interactor(손 쪽) 구성 문제일 가능성.
// 2. WhenPointerStarted는 찍히는데 WhenSelectableHovered가 안 찍히면: 포인터는 있지만
//    레이캐스트가 버튼(또는 캔버스 자체)에 안 맞는 것 — 캔버스/서페이스 크기·위치 문제.
// 2b. AnyGraphicRaycastHit 로그로 "캔버스 자체에는 맞는데 selectable이 없다"인지 구분한다.
// 3. WhenSelectableHovered는 찍히는데 WhenSelected가 안 찍히면: 호버는 되는데 실제
//    선택(클릭 확정) 트리거가 안 걸리는 것 — 핀치/포크 select 임계값 문제.

using Oculus.Interaction;
using UnityEngine;

public class MRPokeDebugProbe : MonoBehaviour
{
    private void OnEnable()
    {
        PointableCanvasModule.WhenPointerStarted += OnPointerStarted;
        PointableCanvasModule.WhenSelectableHovered += OnHovered;
        PointableCanvasModule.WhenSelectableUnhovered += OnUnhovered;
        PointableCanvasModule.WhenSelected += OnSelected;
        PointableCanvasModule.WhenUnselected += OnUnselected;
        Debug.Log("[MRPokeDebugProbe] 구독 시작");
    }

    private void OnDisable()
    {
        PointableCanvasModule.WhenPointerStarted -= OnPointerStarted;
        PointableCanvasModule.WhenSelectableHovered -= OnHovered;
        PointableCanvasModule.WhenSelectableUnhovered -= OnUnhovered;
        PointableCanvasModule.WhenSelected -= OnSelected;
        PointableCanvasModule.WhenUnselected -= OnUnselected;
    }

    private void OnPointerStarted(PointableCanvasModule.Pointer p)
    {
        Debug.Log($"[MRPokeDebugProbe] 포인터 시작 id={p.Identifier}");
    }

    private void OnHovered(PointableCanvasEventArgs args)
    {
        Debug.Log($"[MRPokeDebugProbe] 호버됨: canvas={(args.Canvas != null ? args.Canvas.name : "null")} " +
                  $"hovered={(args.Hovered != null ? args.Hovered.name : "null")}");
    }

    private void OnUnhovered(PointableCanvasEventArgs args)
    {
        Debug.Log($"[MRPokeDebugProbe] 호버 해제: hovered이었던 대상={(args.Hovered != null ? args.Hovered.name : "null")}");
    }

    private void OnSelected(PointableCanvasEventArgs args)
    {
        Debug.Log($"[MRPokeDebugProbe] 선택됨(클릭): canvas={(args.Canvas != null ? args.Canvas.name : "null")} " +
                  $"target={(args.Hovered != null ? args.Hovered.name : "null")}");
    }

    private void OnUnselected(PointableCanvasEventArgs args)
    {
        Debug.Log($"[MRPokeDebugProbe] 선택 해제: target={(args.Hovered != null ? args.Hovered.name : "null")}");
    }
}
