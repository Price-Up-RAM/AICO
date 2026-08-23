using UnityEngine;
using Oculus.Interaction.Input;

public class MRWristSummoner : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("탭(Tap)으로 인식할 거리(m)")]
    public float tapDistanceThreshold = 0.05f;
    [Tooltip("더블 탭 간격(초)")]
    public float doubleTapWindow = 0.3f;
    
    [Header("홀로그램 오프셋")]
    [Tooltip("팔꿈치 방향으로 얼마나 이동할지(m). 보통 0.05면 5cm 이동.")]
    public float elbowOffset = 0.05f;
    [Tooltip("손등 위로 얼마나 띄울지(m). -0.03은 기존보다 5cm 낮춘 값.")]
    public float heightOffset = -0.03f;

    [Tooltip("위치 스무딩 속도")]
    public float smoothingSpeed = 15f;

    [Header("참조")]
    public MRHologramPortrait hologramPortrait;
    
    [Tooltip("체크하면 거리 탭 감지 등 손목 홀로그램 관련 디버그 로그가 출력됩니다.")]
    public bool debugLog = true;

    private Hand _leftHand;
    private Hand _rightHand;
    
    private GameObject _hologramInstance;
    private MRIntentRouter _router;

    private bool _isTapping = false;
    private float _lastTapTime = -999f;

    private void Start()
    {
        FindHands();
        
        _router = Object.FindFirstObjectByType<MRIntentRouter>();
        if (_router != null)
        {
            _router.OnCharacterHoldStarted += HandleCharacterGrabbed;
        }
    }

    private void OnDestroy()
    {
        if (_router != null)
        {
            _router.OnCharacterHoldStarted -= HandleCharacterGrabbed;
        }
    }

    private void HandleCharacterGrabbed(MRRayProvider provider)
    {
        if (hologramPortrait != null && hologramPortrait.gameObject.activeSelf)
        {
            hologramPortrait.gameObject.SetActive(false);
            if(debugLog)
                Debug.Log("[MRWristSummoner] 홀로그램을 밖으로 꺼내어 숨김 처리됨");
        }
    }

    private void FindHands()
    {
        Hand[] hands = FindObjectsByType<Hand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var h in hands)
        {
            if (!h.IsTrackedDataValid) continue;
            
            if (h.Handedness == Handedness.Left) 
            {
                _leftHand = h;
                if(debugLog)
                    Debug.Log($"[MRWristSummoner] 왼손 바인딩: {h.gameObject.name}");
            }
            else if (h.Handedness == Handedness.Right) 
            {
                _rightHand = h;
                if(debugLog)
                    Debug.Log($"[MRWristSummoner] 오른손 바인딩: {h.gameObject.name}");
            }
        }
    }

    private float _logTimer = 0f;
    private float _handSearchLogTimer = 0f;

    private void Update()
    {
        if (_leftHand == null || _rightHand == null || !_leftHand.IsTrackedDataValid || !_rightHand.IsTrackedDataValid)
        {
            FindHands();
            if (_leftHand == null || _rightHand == null)
            {
                _handSearchLogTimer += Time.deltaTime;
                if (_handSearchLogTimer >= 2.0f)
                {
                    _handSearchLogTimer = 0f;
                    if(debugLog)
                        Debug.Log($"[MRWristSummoner] 유효한 트래킹 손을 찾는 중... (왼손: {_leftHand != null}, 오른손: {_rightHand != null})");
                }
                return;
            }
        }

        if (!_leftHand.IsTrackedDataValid) return;

        bool isLeftWristValid = _leftHand.GetJointPose(HandJointId.HandWristRoot, out Pose leftWristPose);
        if (!isLeftWristValid) return;

        // 손등 법선 벡터 (왼손 기준 +Y)
        Vector3 backOfHandNormal = leftWristPose.rotation * Vector3.up;
        float wristAngle = Vector3.Angle(backOfHandNormal, Vector3.up);

        bool isHologramActive = hologramPortrait != null && hologramPortrait.gameObject.activeSelf;

        // 홀로그램 해제 조건: 손등이 너무 많이 기울어짐 (90도 이상)
        if (isHologramActive)
        {
            if (wristAngle > 90f)
            {
                hologramPortrait.gameObject.SetActive(false);
                isHologramActive = false;
                if(debugLog)
                    Debug.Log("[MRWristSummoner] 손목이 90도 이상 뒤집혀 홀로그램 해제");
            }
        }

        // 로그 출력 (1초에 1번)
        _logTimer += Time.deltaTime;
        bool shouldLog = _logTimer >= 1.0f;
        if (shouldLog) _logTimer = 0f;

        // 탭 소환 판정 (거리 및 Latch(눌림 상태) 활용)
        if (_rightHand.IsTrackedDataValid && wristAngle <= 90f)
        {
            if (_rightHand.GetJointPose(HandJointId.HandIndexTip, out Pose rightIndexTipPose))
            {
                float distance = Vector3.Distance(leftWristPose.position, rightIndexTipPose.position);
                
                if (shouldLog&&debugLog)
                {
                    Debug.Log($"[MRWristSummoner] 손목(Angle:{wristAngle:F1}), 거리:{distance:F3}m (임계값:{tapDistanceThreshold}m), 왼쪽손목:{leftWristPose.position}, 오른쪽검지:{rightIndexTipPose.position}");
                }

                if (distance <= tapDistanceThreshold)
                {
                    if (!_isTapping)
                    {
                        _isTapping = true;
                        OnWristTapped(leftWristPose);
                    }
                }
                else if (distance > tapDistanceThreshold + 0.02f)
                {
                    // 거리가 충분히 멀어지면 탭 상태 초기화
                    _isTapping = false;
                }
            }
        }
        else
        {
            if (shouldLog && wristAngle > 90f&&debugLog)
            {
                Debug.Log($"[MRWristSummoner] 손등이 아래를 향함 (Angle:{wristAngle:F1}) - 탭 무시");
            }
            _isTapping = false;
        }
        
        // 위치 갱신
        if (hologramPortrait != null && hologramPortrait.gameObject.activeSelf)
        {
            // 왼손 기준 -Z가 팔꿈치 방향, +Y가 손등 위쪽
            Vector3 localOffset = new Vector3(0, heightOffset, -elbowOffset);
            Vector3 targetPos = leftWristPose.position + (leftWristPose.rotation * localOffset);
            
            // 부드럽게 위치 이동 (스무딩)
            hologramPortrait.transform.position = Vector3.Lerp(hologramPortrait.transform.position, targetPos, Time.deltaTime * smoothingSpeed);
            
            // 회전은 무조건 Upright (Y축만 카메라를 보도록 처리하는 것은 MRHologramPortrait에 위임)
            hologramPortrait.transform.rotation = Quaternion.identity; 
        }
    }

    private void OnWristTapped(Pose leftWristPose)
    {
        if (hologramPortrait == null) return;
        
        bool isActive = hologramPortrait.gameObject.activeSelf;
        float now = Time.unscaledTime;

        if (!isActive)
        {
            // 소환
            hologramPortrait.gameObject.SetActive(true);
            
            // 켤 때는 즉시 타겟 위치로 스냅 (0,0,0에서 날아오는 현상 방지)
            Vector3 localOffset = new Vector3(0, heightOffset, -elbowOffset);
            hologramPortrait.transform.position = leftWristPose.position + (leftWristPose.rotation * localOffset);
            
            _lastTapTime = now;
            if(debugLog)
                Debug.Log("[MRWristSummoner] 탭 감지: 홀로그램 소환");
        }
        else
        {
            // 켜진 상태에서의 탭은 캐릭터 조작으로 전달
            if (now - _lastTapTime <= doubleTapWindow)
            {
                // 더블 탭
                _lastTapTime = -999f; // 창 초기화
                if (_router != null)
                {
                    _router.SimulateCharacterDoubleTap();
                }
                if(debugLog)
                    Debug.Log("[MRWristSummoner] 손목 더블 탭 -> 캐릭터 메뉴 실행");
            }
            else
            {
                // 싱글 탭
                _lastTapTime = now;
                if (_router != null)
                {
                    _router.SimulateCharacterSingleTap();
                }
                if(debugLog)
                    Debug.Log("[MRWristSummoner] 손목 싱글 탭 -> 캐릭터 음성 대화 시작");
            }
        }
    }
}
