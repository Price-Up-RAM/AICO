// 캐릭터 부착형 말풍선(8종)을 캐릭터의 월드 위치를 따라다니는 빌보드로 만든다.
//
// 왜 8개 파일을 포크하지 않았는가
// ------------------------------
// ChatBalloonManager, AnswerBalloonManager, AnswerBalloonSimpleManager, AskBalloonManager,
// EmotionBalloonManager, NoticeBalloonManager, PortraitBalloonSimpleManager,
// SubAnswerBalloonManager, SubChatBalloonManager는 전부 데스크톱과 공유하는 파일이고,
// 전부 같은 패턴을 쓴다 — 자기 RectTransform의 anchoredPosition을
// "characterTransform.anchoredPosition + 오프셋" 으로 Update()/Show()에서 매 프레임 계산한다.
//
// 9개 파일을 각각 포크해 월드 트랜스폼 기준으로 다시 쓰는 대신, 이 컴포넌트 하나를
// 각 말풍선의 (독립 월드 스페이스 캔버스가 된) 루트에 붙이는 쪽을 택했다. 이유:
//   1) 원본 파일을 건드리지 않으므로 데스크톱 회귀 위험이 0이다 (Kickoff Guide §3 공용 스크립트 경고).
//   2) LateUpdate에서 최종 위치를 덮어쓰므로 원본의 anchoredPosition 계산과 충돌하지 않는다
//      (Unity는 모든 Update()가 끝난 뒤에 LateUpdate()를 실행한다 — 순서를 보장받기 위해
//      DefaultExecutionOrder를 쓸 필요도 없다).
//   3) 캐릭터 참조 소스가 하나(MRCharacterWorldRoot)로 통일된다.
//
// 사용
// ----
// Tools → MR → 7. 선택 오브젝트를 캐릭터 부착 말풍선으로 변환
// (런타임에 Instantiate되는 EmotionBalloon 같은 경우 프리팹 자체에 미리 붙여둔다.)
//
// 한계
// ----
// 서브 캐릭터(Aropla 모드의 두 번째 캐릭터) 추적은 아직 다루지 않는다 — 메인 캐릭터
// (MRCharacterWorldRoot.CurrentCharacter)만 지원한다. 서브 캐릭터 월드 배치 자체가
// 아직 설계되지 않았다(Phase 2/3-1은 메인 캐릭터만 다뤘다). 필요해지면 explicitTarget에
// 서브 캐릭터 Transform을 런타임에 할당하는 방식으로 확장한다.

using UnityEngine;

public class MRBalloonWorldFollow : MonoBehaviour
{
    [Tooltip("따라다닐 대상을 명시적으로 지정한다. 비워두면 MRCharacterWorldRoot의 " +
             "현재 메인 캐릭터를 자동으로 쓴다.")]
    [SerializeField] private Transform explicitTarget;

    [Tooltip("캐릭터 기준 오프셋(m). 기본값은 머리 위쪽.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.35f, 0f);

    [Tooltip("Y축만 회전해 사용자를 향하게 한다. 완전 빌보드(모든 축)는 어지러워서 쓰지 않는다 " +
             "(MR_Phase3-2_Canvas_Plan.md §3-2-B step 4 확정 사항).")]
    [SerializeField] private bool billboardYOnly = true;

    [Tooltip("true면 매 프레임 계속 따라다닌다(기존 6종 말풍선). false면 활성화되는 " +
             "순간(소환 시점) 딱 한 번만 위치/회전을 맞추고 그 뒤로는 손대지 않는다 " +
             "(Image_ChatBalloon처럼 원본 스크립트가 이후 자체 위치 로직을 갖고 있는 경우).")]
    [SerializeField] private bool continuousFollow = true;

    private static MRCharacterWorldRoot _worldRootCache;
    private Camera _cam;
    private bool _warnedNoTarget;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void OnEnable()
    {
        // 소환(=SetActive(true)) 시점에 한 번은 항상 맞춰준다. continuousFollow=true인
        // 경우에도 첫 프레임 LateUpdate 전에 미리 맞춰서 초기 프레임 깜빡임을 줄인다.
        ApplyFollow();
    }

    private void LateUpdate()
    {
        if (!continuousFollow) return;
        ApplyFollow();
    }

    private void ApplyFollow()
    {
        if (_cam == null) _cam = Camera.main;

        Transform target = ResolveTarget();
        if (target == null)
        {
            if (!_warnedNoTarget)
            {
                _warnedNoTarget = true;
                Debug.LogWarning($"[MRBalloonWorldFollow] '{name}' — 따라다닐 캐릭터를 찾지 못했습니다. " +
                                  "MRCharacterWorldRoot가 씬에 있고 캐릭터가 스폰됐는지 확인하세요.");
            }
            return;
        }

        transform.position = target.position + worldOffset;

        if (billboardYOnly && _cam != null)
        {
            Vector3 dir = transform.position - _cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }

    private Transform ResolveTarget()
    {
        if (explicitTarget != null) return explicitTarget;

        if (_worldRootCache == null)
        {
            _worldRootCache = FindFirstObjectByType<MRCharacterWorldRoot>();
        }

        if (_worldRootCache != null && _worldRootCache.CurrentCharacter != null)
        {
            return _worldRootCache.CurrentCharacter.transform;
        }

        return null;
    }

    /// <summary>서브 캐릭터 등으로 대상을 런타임에 바꿔야 할 때 호출한다.</summary>
    public void SetTarget(Transform t)
    {
        explicitTarget = t;
        _warnedNoTarget = false;
    }
}
