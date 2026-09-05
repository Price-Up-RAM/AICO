// 라디얼 메뉴의 "왼쪽으로 걷기 / 오른쪽으로 걷기"를 MR에서 대체한다.
//
// 왜 필요한가
// ----------
// PhysicsManager.MoveLeft/Right(데스크톱 원본)는 캐릭터를 캔버스 로컬 X축(anchoredPosition.x)
// 으로 움직이고, 회전도 -90/-270 같은 화면 기준 절대값으로 고정한다. 사용자가 캐릭터
// 주위 어디에 서 있든 결과가 항상 같은 절대 방향이라, MR에서는 "왼쪽" 버튼이 실제로는
// 등 뒤로 걸어가는 것처럼 보일 수 있다.
//
// 이 어댑터는 그 두 케이스를 MR에서 대신 받아, "버튼을 누른 시점의 카메라 right 벡터"
// 방향으로 캐릭터를 이동시킨다 — 시야를 돌려도 이미 시작된 이동 방향은 바뀌지 않는다
// (누른 순간 고정, 확정 사항).
//
// 왜 Update()에서 매 프레임 절대 위치를 다시 계산하는가
// ------------------------------------------------------
// 이전 두 차례 시도의 실패 원인:
//   1차: character.transform.position(캐릭터 자신의 좌표)을 읽어 그대로
//        SetCharacterPosition()에 넘겼다. 그런데 SetCharacterPosition이 실제로 옮기는
//        대상은 캐릭터가 아니라 픽셀 공간 래퍼(CharacterMoveTarget)라서, "캐릭터 좌표계에서
//        잰 값"과 "래퍼가 기대하는 절대 위치"가 어긋나 순간이동했다.
//   2차: moveTarget.position + delta를 코루틴에서 계산했는데, 실기에서 "위치는 안 바뀌고
//        모션(회전 애니메이션)만 좌우로 재생되는" 증상이 났다 — 코루틴이 참조를 캐시한 채
//        여러 프레임 동안 매번 같은 시작점 + 델타를 계산해서 실질적으로 위치가 누적되지
//        않았던 것으로 추정된다.
//
// 그래서 이번엔 코루틴 대신 MonoBehaviour.Update()에서, "시작 시점 캐릭터 위치 +
// 그동안 흐른 총 이동 거리"로 매 프레임 절대 위치를 새로 계산해 SetCharacterPosition에
// 넘긴다. 상태를 델타 누적이 아니라 "시작점 + 경과 거리"로 들고 있으면, 중간에 어떤
// 프레임을 놓치거나 값을 다시 읽어도 결과가 항상 같은 절대 좌표로 수렴한다.

using UnityEngine;

public class MRWalkAdapter : MonoBehaviour
{
    [Header("참조 — 비우면 씬에서 찾는다")]
    [SerializeField] private MRCharacterWorldRoot characterRoot;

    [Header("이동")]
    [Tooltip("걷는 속도(m/s).")]
    [SerializeField] private float moveSpeed = 0.5f;

    [Tooltip("사용자로부터 이 거리(m)를 넘어가면 멈춘다.")]
    [SerializeField] private float maxDistanceFromUser = 2.5f;

    [Header("회전")]
    [Tooltip("걷는 방향으로 캐릭터를 돌린다.")]
    [SerializeField] private bool faceMoveDirection = true;

    [Header("진단")]
    [SerializeField] private bool logWalk = true;

    private bool _walking;
    private Vector3 _direction;   // 버튼 누른 시점의 시야 기준 방향(고정)
    private Vector3 _startPos;    // 걷기 시작 시점의 캐릭터 월드 위치
    private Vector3 _userPos;     // 버튼 누른 시점의 사용자 위치(거리 제한 기준)
    private float _traveled;      // 시작점에서부터 이동한 거리(m)

    private Animator _animator;
    private GameObject _animatorOwner;

    public void GoLeft() => StartWalk(isLeft: true);
    public void GoRight() => StartWalk(isLeft: false);

    public void StopWalking()
    {
        _walking = false;
        SetWalkAnimator(false);
    }

    private void StartWalk(bool isLeft)
    {
        ResolveRefs();
        if (characterRoot == null || characterRoot.CurrentCharacter == null)
        {
            Debug.LogWarning("[MRWalk] 캐릭터가 없어 걷기를 시작할 수 없습니다.");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[MRWalk] Camera.main을 찾지 못했습니다.");
            return;
        }

        Vector3 camRight = cam.transform.right;
        camRight.y = 0f;
        if (camRight.sqrMagnitude < 0.0001f) camRight = Vector3.right;
        camRight.Normalize();

        _direction = isLeft ? -camRight : camRight;
        _userPos = cam.transform.position;
        _startPos = characterRoot.CurrentCharacter.transform.position;
        _traveled = 0f;
        _walking = true;

        Transform moveTarget = characterRoot.CharacterMoveTarget;
        if (faceMoveDirection && moveTarget != null)
        {
            moveTarget.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        }

        SetWalkAnimator(true);

        if (logWalk)
        {
            Debug.Log($"[MRWalk] {(isLeft ? "왼쪽" : "오른쪽")} 걷기 시작 — 시작위치 {_startPos}, 방향 {_direction}");
        }
    }

    private void Update()
    {
        if (!_walking) return;

        if (characterRoot == null || characterRoot.CurrentCharacter == null)
        {
            StopWalking();
            return;
        }

        _traveled += moveSpeed * Time.deltaTime;
        Vector3 targetPos = _startPos + _direction * _traveled;

        Vector3 flatDelta = targetPos - _userPos;
        flatDelta.y = 0f;
        if (flatDelta.magnitude > maxDistanceFromUser)
        {
            StopWalking();
            return;
        }

        // 캐릭터 자신의 목표 위치(targetPos)를, 실제로 옮겨야 하는 래퍼(CharacterMoveTarget)
        // 좌표로 변환한다. 래퍼와 캐릭터 사이에 오프셋이 없으면 그대로 같은 값이다.
        GameObject character = characterRoot.CurrentCharacter;
        Transform moveTarget = characterRoot.CharacterMoveTarget;
        if (moveTarget == null)
        {
            StopWalking();
            return;
        }

        Vector3 currentCharacterPos = character.transform.position;
        Vector3 correction = targetPos - currentCharacterPos;
        characterRoot.SetCharacterPosition(moveTarget.position + correction);

        if (logWalk)
        {
            Debug.Log($"[MRWalk] 진행 {_traveled:F2}m — 목표 {targetPos}, 실제캐릭터위치(적용전) {currentCharacterPos}");
        }
    }

    private void SetWalkAnimator(bool isWalking)
    {
        Animator animator = ResolveAnimator();
        if (animator == null) return;
        if (HasParameter(animator, "isWalk"))
        {
            animator.SetBool("isWalk", isWalking);
        }
    }

    private Animator ResolveAnimator()
    {
        GameObject character = characterRoot != null ? characterRoot.CurrentCharacter : null;
        if (character == null) return null;

        if (_animatorOwner != character)
        {
            _animatorOwner = character;
            _animator = character.GetComponentInChildren<Animator>(true);
        }

        return _animator;
    }

    private bool HasParameter(Animator animator, string parameterName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName) return true;
        }
        return false;
    }

    private void ResolveRefs()
    {
        if (characterRoot == null) characterRoot = FindFirstObjectByType<MRCharacterWorldRoot>();
    }
}
