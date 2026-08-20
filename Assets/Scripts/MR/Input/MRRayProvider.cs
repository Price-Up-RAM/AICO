// 손 레이 조준 채널 (MR_Phase4A_Input_Plan.md §2-3)
//
// `MRSpatialAnchorEditor.UpdateRayPose()`를 이식했다. 원본 함수 자체는 깨끗하다 —
// GetPointerPose → 검지-손목 폴백 → 컨트롤러 폴백이 각각 return으로 끊겨 있어
// 폴백이 주 경로를 덮어쓰지 않는다. 다만 **두 가지를 고쳐서 가져왔다.**
//
//   원본                                        | 여기
//   -------------------------------------------|--------------------------------------
//   rayHand가 인스펙터 직렬화 Hand 필드          | FindObjectsByType<Hand>() 런타임 획득(§4-19)
//   컨트롤러 폴백이 Camera.main으로 리그 변환    | ResolveEye() 기반 리그 참조(§4-28)
//
// **가져오지 않은 것**(Port Plan §3-4): HandlePinchStart의 우선순위 구조,
// FinalizeAnchorDrag, 앵커 생명주기. 앵커 에디터의 "선택 실패 → 신규 생성" 폴백이
// 4-A로 새면 조준이 빗나갈 때마다 뭔가가 생긴다. 캐릭터 드래그에는 생성 개념이 없다 —
// **빗나가면 no-op이다.**
//
// 핀치 판정은 hand.GetFingerIsPinching(HandFinger.Index)를 쓴다.
// OVRInput.Axis1D.*IndexTrigger는 순수 핸드트래킹에서 항상 0이다(§4-19).

using UnityEngine;
using Oculus.Interaction.Input;

public class MRRayProvider : MonoBehaviour, IMRAimProvider
{
    [Header("손")]
    [SerializeField] private MRHandSide side = MRHandSide.Right;

    [Header("레이")]
    [Tooltip("이 거리(m)까지만 조준한다.")]
    [SerializeField] private float maxRayDistance = 8f;

    [Tooltip("캐릭터 콜라이더가 있는 레이어. Nothing/Everything이면 런타임에 'Char'로 좁힌다.")]
    [SerializeField] private LayerMask characterLayers;

    [Tooltip("캐릭터 판정 보조 — 레이가 캐릭터 중심에서 이 반경(m) 안을 지나면 맞은 것으로 본다. " +
             "콜라이더가 얇거나 없는 캐릭터를 위한 폴백이다. 0이면 쓰지 않는다.")]
    [SerializeField] private float characterProximityRadius = 0.25f;

    [Header("대상")]
    [SerializeField] private MRCharacterWorldRoot characterRoot;

    [Header("레이 시각화 (Port Plan §3-4 '가져올 것' — UpdateRayVisual)")]
    // 이게 없으면 사용자가 **무엇을 겨누고 있는지 알 수 없다.** 기능이 아니라 검증의 전제다
    // (2026-08-19: 이것이 없어서 진리표의 캐릭터 4칸을 검증하지 못했다. 설계서 §8-1).
    [Tooltip("손 레이를 선으로 그린다.")]
    [SerializeField] private bool showRayLine = true;

    [Tooltip("캐릭터를 맞혔을 때만 선을 보여준다. 끄면 항상 보인다.\n" +
             "켜면 시야가 깨끗해지는 대신, 아직 못 맞췄을 때 어디를 가리키는지 알 수 없다 — " +
             "그 역할은 발밑 링(MRAimHighlight)이 대신한다.")]
    [SerializeField] private bool showRayLineOnlyOnCharacter = true;

    [Tooltip("비우면 자식으로 자동 생성한다.")]
    [SerializeField] private LineRenderer rayLine;

    [SerializeField] private float rayWidth = 0.003f;

    [Tooltip("빈 공간을 겨눌 때의 색.")]
    [SerializeField] private Color idleColor = new Color(0f, 0.8f, 1f, 0.5f);

    [Tooltip("캐릭터를 겨눌 때의 색.")]
    [SerializeField] private Color characterColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    [Header("진단")]
    // 레이가 실제로 무엇을 맞히는지 모르면 "캐릭터 조준이 안 된다"의 원인을 특정할 수 없다.
    // EvaluateAim은 ① 콜라이더 히트에서 곧바로 return하므로, 캐릭터 앞뒤에 다른 콜라이더
    // (방 메시 / 바닥 / 패널의 GrabPlate)가 하나라도 걸리면 ② 근접 폴백이 실행되지 않는다.
    // 그 상황인지 아닌지를 추측하지 말고 이 로그로 가른다.
    [Tooltip("조준 결과(맞힌 콜라이더 이름·거리·캐릭터 판정)를 주기적으로 로그에 찍는다.")]
    [SerializeField] private bool logAimDiagnostics;

    [Tooltip("진단 로그 주기(초).")]
    [SerializeField] private float aimLogInterval = 0.5f;

    private Hand _hand;
    private Transform _eye;
    private float _aimLogTimer;
    private bool _loggedCharacterOnce;

    private Vector3 _rayOrigin;
    private Vector3 _rayDir;
    private bool _hasRayPose;

    public bool IsChannelActive => _hasRayPose;
    public bool IsPressed { get; private set; }
    public MRAimResult Aim { get; private set; }
    public MRHandSide Side => side;
    public Vector3 PressPoint => _rayOrigin;

    /// <summary>드래그 어댑터가 쓸 현재 레이. `MRIntentRouter`가 홀드를 판정한 뒤 넘겨준다.</summary>
    public bool TryGetRay(out Ray ray)
    {
        ray = new Ray(_rayOrigin, _rayDir);
        return _hasRayPose;
    }

    private void Awake()
    {
        // 씬에 저장된 값이 Everything이면 방 메시·바닥·패널 GrabPlate가 캐릭터보다 앞에서
        // 레이를 가로챈다. Char로 좁혀야 ①이 캐릭터를 안정적으로 잡는다.
        characterLayers = MRCharacterBounds.ResolveCharacterMask(characterLayers, this);
    }

    private void Update()
    {
        ResolveHand();

        // 핀치 판정과 포즈 조회를 분리한다(§4-19 주의 ①).
        IsPressed = ReadPinch();

        UpdateRayPose();

        Aim = _hasRayPose ? EvaluateAim() : MRAimResult.None;

        DrawRayLine();
    }

    // ---------------------------------------------------------
    // 레이 시각화 — MRSpatialAnchorEditor.UpdateRayVisual() 이식
    //
    // 원본과 한 곳이 다르다: 원본은 끝점을 구하려고 Physics.Raycast를 **한 번 더** 돌린다.
    // 여기서는 이미 계산해 둔 Aim.point를 그대로 쓴다 — 같은 프레임에 같은 레이캐스트를
    // 두 번 돌 이유가 없고, 무엇보다 **선과 판정이 어긋날 수 없다.**
    // (§4-47의 교훈 — 같은 것을 재는 코드가 둘이면 반드시 어긋난다.)
    // ---------------------------------------------------------
    private void DrawRayLine()
    {
        if (!showRayLine)
        {
            if (rayLine != null) rayLine.enabled = false;
            return;
        }

        EnsureRayLine();
        if (rayLine == null) return;

        if (!_hasRayPose)
        {
            rayLine.enabled = false;
            return;
        }

        // ISDK의 레이 인터랙션처럼 "무언가를 겨눴을 때만" 보이게 한다.
        if (showRayLineOnlyOnCharacter && !Aim.onCharacter)
        {
            rayLine.enabled = false;
            return;
        }

        rayLine.enabled = true;
        rayLine.SetPosition(0, _rayOrigin);
        rayLine.SetPosition(1, Aim.point);

        Color color = idleColor;
        if (Aim.onCharacter) color = characterColor;

        rayLine.startColor = color;

        // 끝으로 갈수록 옅게 — 어디를 가리키는지는 시작점 쪽이 더 중요하다.
        Color tail = color;
        tail.a = color.a * 0.25f;
        rayLine.endColor = tail;
    }

    private void EnsureRayLine()
    {
        if (rayLine != null) return;

        var go = new GameObject("MRRayLine");
        go.transform.SetParent(transform, false);

        rayLine = go.AddComponent<LineRenderer>();
        rayLine.useWorldSpace = true;
        rayLine.positionCount = 2;
        rayLine.startWidth = rayWidth;
        rayLine.endWidth = rayWidth;
        rayLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rayLine.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null) rayLine.material = new Material(shader);
    }

    private bool ReadPinch()
    {
        if (_hand == null || !_hand.IsTrackedDataValid) return false;
        return _hand.GetFingerIsPinching(HandFinger.Index);
    }

    private MRAimResult EvaluateAim()
    {
        var ray = new Ray(_rayOrigin, _rayDir);

        GameObject character = ResolveCharacter();

        // ① 콜라이더 히트
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, characterLayers))
        {
            bool isCharacter = character != null &&
                               hit.collider.transform.IsChildOf(character.transform);

            // ① 에서 곧바로 return하므로 ② 폴백은 아무것도 안 맞았을 때만 돈다.
            //   레이어를 Char로 좁혔기 때문에 여기 걸리는 건 캐릭터뿐이다 —
            //   방 메시·바닥·패널 GrabPlate가 가로채던 문제가 사라졌다.
            LogAim($"① 콜라이더 '{hit.collider.name}' ({hit.distance:F2} m) → 캐릭터={isCharacter}");

            return new MRAimResult { valid = true, onCharacter = isCharacter, point = hit.point };
        }

        // ② 캡슐을 살짝 빗나갔을 때의 근접 폴백.
        //    기준점은 **콜라이더 경계의 중심**이다 — transform.position(피벗)은 발밑일 수도
        //    허리일 수도 있어 믿을 수 없다. 시선 채널과 같은 기준을 쓴다.
        if (character != null && characterProximityRadius > 0f &&
            MRCharacterBounds.TryGet(characterRoot, out Bounds bounds))
        {
            Vector3 center = bounds.center;
            Vector3 toChar = center - _rayOrigin;
            float along = Vector3.Dot(toChar, _rayDir);

            if (along > 0f && along <= maxRayDistance)
            {
                Vector3 closest = _rayOrigin + _rayDir * along;
                float miss = Vector3.Distance(closest, center);

                LogAim($"② 근접 폴백 — 판정 부피 중심에서 {miss:F2} m (반경 {characterProximityRadius:F2} m)");

                if (miss <= characterProximityRadius)
                {
                    return new MRAimResult
                    {
                        valid = true,
                        onCharacter = true,
                        point = center
                    };
                }
            }
        }

        // ③ 빈 공간. **여기서 아무것도 만들지 않는다.**
        LogAim("③ 빈 공간 (콜라이더 히트 없음)");

        return new MRAimResult
        {
            valid = true,
            onCharacter = false,
            point = _rayOrigin + _rayDir * maxRayDistance
        };
    }

    // ---------------------------------------------------------
    // 레이 포즈 — MRSpatialAnchorEditor.UpdateRayPose() 이식
    // ---------------------------------------------------------
    private void UpdateRayPose()
    {
        _hasRayPose = false;

        if (_hand != null && _hand.IsTrackedDataValid)
        {
            if (_hand.GetPointerPose(out Pose pointerPose))
            {
                _rayOrigin = pointerPose.position;
                _rayDir = pointerPose.rotation * Vector3.forward;
                _hasRayPose = true;
                return;
            }

            if (_hand.GetJointPose(HandJointId.HandIndexTip, out Pose indexPose) &&
                _hand.GetJointPose(HandJointId.HandWristRoot, out Pose wristPose))
            {
                _rayOrigin = indexPose.position;
                _rayDir = (indexPose.position - wristPose.position).normalized;
                _hasRayPose = true;
                return;
            }
        }

        // 컨트롤러 폴백. Camera.main이 아니라 ResolveEye 기반 리그를 쓴다(§4-28).
        OVRInput.Controller controller = side == MRHandSide.Left
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;

        if (!OVRInput.IsControllerConnected(controller)) return;

        Vector3 ctrlPos = OVRInput.GetLocalControllerPosition(controller);
        Quaternion ctrlRot = OVRInput.GetLocalControllerRotation(controller);

        Transform eye = ResolveEye();
        if (eye != null && eye.parent != null)
        {
            Transform rig = eye.parent;
            ctrlPos = rig.TransformPoint(ctrlPos);
            ctrlRot = rig.rotation * ctrlRot;
        }

        _rayOrigin = ctrlPos;
        _rayDir = ctrlRot * Vector3.forward;
        _hasRayPose = true;
    }

    // ---------------------------------------------------------
    private void ResolveHand()
    {
        if (_hand != null) return;

        Handedness want = side == MRHandSide.Left ? Handedness.Left : Handedness.Right;

        Hand[] hands = FindObjectsByType<Hand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < hands.Length; i++)
        {
            if (hands[i].Handedness != want) continue;
            _hand = hands[i];
            return;
        }
    }

    private GameObject ResolveCharacter()
    {
        if (characterRoot == null) characterRoot = FindFirstObjectByType<MRCharacterWorldRoot>();
        if (characterRoot == null) return null;

        GameObject character = characterRoot.CurrentCharacter;
        LogCharacterOnce(character);
        return character;
    }

    /// <summary>캐릭터가 처음 잡힌 순간 콜라이더 유무를 한 번만 찍는다.
    /// 콜라이더가 0개면 EvaluateAim ①은 절대 캐릭터를 맞히지 못하고,
    /// 판정은 전적으로 ② 근접 폴백에 달린다 — 그걸 로그 없이 알 방법이 없다.</summary>
    private void LogCharacterOnce(GameObject character)
    {
        if (!logAimDiagnostics) return;
        if (_loggedCharacterOnce) return;
        if (character == null) return;

        _loggedCharacterOnce = true;

        Collider[] cols = character.GetComponentsInChildren<Collider>(true);
        Debug.Log($"[MRRay] 캐릭터 '{character.name}' 콜라이더 {cols.Length}개, " +
                  $"루트 위치 {character.transform.position}");

        for (int i = 0; i < cols.Length && i < 8; i++)
        {
            Bounds cb = cols[i].bounds;
            Debug.Log($"[MRRay]   - {cols[i].name} ({cols[i].GetType().Name}, " +
                      $"enabled={cols[i].enabled}, trigger={cols[i].isTrigger}, layer={cols[i].gameObject.layer}) " +
                      $"y={cb.min.y:F2}~{cb.max.y:F2} 가로={cb.size.x:F2}x{cb.size.z:F2}");
        }

        // 콜라이더가 캐릭터의 **어디까지** 덮는지가 핵심이다.
        // 렌더러 경계(=눈에 보이는 몸 전체)와 나란히 찍어 비교한다.
        // 콜라이더 max.y가 렌더러 max.y보다 한참 낮으면 얼굴·가슴이 판정 밖이라는 뜻이고,
        // 그러면 "조준이 안 된다"의 원인은 조준 방식이 아니라 **판정 부피**다.
        Renderer[] rends = character.GetComponentsInChildren<Renderer>(false);
        if (rends.Length == 0) return;

        Bounds rb = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) rb.Encapsulate(rends[i].bounds);

        Debug.Log($"[MRRay] 렌더러 경계 y={rb.min.y:F2}~{rb.max.y:F2} 가로={rb.size.x:F2}x{rb.size.z:F2} " +
                  $"| 루트 y={character.transform.position.y:F2} " +
                  $"(루트가 렌더러 바닥과 같으면 피벗이 발밑이다)");
    }

    private void LogAim(string message)
    {
        if (!logAimDiagnostics) return;

        _aimLogTimer -= Time.unscaledDeltaTime;
        if (_aimLogTimer > 0f) return;

        _aimLogTimer = aimLogInterval;
        Debug.Log($"[MRRay] {message}");
    }

    private Transform ResolveEye()
    {
        if (_eye != null) return _eye;

        var byName = GameObject.Find("CenterEyeAnchor");
        if (byName != null) { _eye = byName.transform; return _eye; }

        if (Camera.main != null) { _eye = Camera.main.transform; return _eye; }

        var any = FindFirstObjectByType<Camera>();
        if (any != null) _eye = any.transform;
        return _eye;
    }
}
