// Phase 4-A 입력 계약 — 공급자 2 / 판정기 1 (MR_Phase4A_Input_Plan.md §2)
//
// 핵심 규칙: **공급자는 판정하지 않는다.**
// 조준 결과와 원시 핀치 상태만 내보내고, 탭/더블탭/홀드 구분은 MRIntentRouter 한 곳에만 둔다.
// 두 경로(시선 / 손 레이)의 더블탭 타이밍이 갈리는 것을 원천 차단하기 위해서다
// (Port Plan §10 검증 항목).

using UnityEngine;

public enum MRHandSide
{
    Left,
    Right
}

/// <summary>한 채널의 조준 결과. 공급자가 매 프레임 갱신한다.</summary>
public struct MRAimResult
{
    /// <summary>이번 프레임에 조준을 계산할 수 있었는가(트래킹 유효 등).</summary>
    public bool valid;

    /// <summary>조준선이 캐릭터를 향하고 있는가. false면 "빈 공간"이다.</summary>
    public bool onCharacter;

    /// <summary>조준점(월드). 레이 채널은 히트 지점, 시선 채널은 캐릭터 위치.</summary>
    public Vector3 point;

    public static readonly MRAimResult None = new MRAimResult();
}

/// <summary>조준 채널 하나. 시선(MRGazeProvider) / 손 레이(MRRayProvider)가 구현한다.</summary>
public interface IMRAimProvider
{
    /// <summary>이 채널이 지금 평가 대상인가. 예: 시선 채널은 palm-up 자세일 때만 true.</summary>
    bool IsChannelActive { get; }

    /// <summary>원시 핀치 상태. **여기서 탭/홀드를 판정하지 않는다.**</summary>
    bool IsPressed { get; }

    MRAimResult Aim { get; }

    MRHandSide Side { get; }

    /// <summary>탭 이동 거리 판정용 기준점(핀치 중 손 위치). 없으면 Aim.point를 쓴다.</summary>
    Vector3 PressPoint { get; }
}

/// <summary>
/// 근접 스킨십(볼 당기기·쓰다듬기 등)이 손을 점유하고 있는지 알려주는 공급자.
///
/// **구현체는 아직 없다.** VRM 스킨십은 이 씬에 미구현이다 —
/// `MRHandInteractionRouter`는 `MRSpineCharacterController` 전용이라 쓸 수 없다
/// (Kickoff Guide §4-14). 구현체가 없으면 `MRIntentRouter`는 "점유 없음"으로 보고
/// 조작 레이어를 그대로 평가한다.
///
/// 규칙(Port Plan §1-1 "손이 인터랙션 콜라이더에 닿아 있으면 스킨십이 무조건 우선")을
/// 코드에 살려두는 이유는, 나중에 VRM 스킨십을 붙일 때 우선순위를 다시 설계하지 않기
/// 위해서다. **"아무 일도 안 하니 버그"로 오인해 지우지 말 것.**
/// </summary>
public interface IMRSkinshipContactProvider
{
    bool IsHandEngaged(MRHandSide side);
}
