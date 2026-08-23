using UnityEngine;
using Meta.XR.MRUtilityKit;

public class MRFloorPlacement : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private MRCharacterWorldRoot characterRoot;

    [Header("낙하 (Gravity)")]
    [SerializeField] private float fallSpeed = 5f;
    [SerializeField] private float gravityAccel = 9.8f;
    [SerializeField] private bool useGravity = true;

    [Header("위치 영속화 (Persistence)")]
    [SerializeField] private bool usePersistence = true;
    private const string PREF_HAS_POS = "MRCharacter_HasPos";
    private const string PREF_POS_X = "MRCharacter_PosX";
    private const string PREF_POS_Z = "MRCharacter_PosZ";

    [Header("디버그")]
    [SerializeField] private bool verboseLog = true;

    // 임계값 상수 — 매직 넘버 제거
    private const float FallStartThreshold = 0.001f;   // 이 높이 이상 떠 있으면 낙하 시작
    private const float SnapUpThreshold    = 0.05f;    // 바닥보다 이만큼 아래면 끌어올림

    private bool _hasPlacedInitially = false;
    private float _verticalVelocity = 0f;
    private bool _wasPicking = false;
    private bool _wasWalking = false;
    private bool _isFalling = false;
    private GameObject _trackedCharacter;
    private bool _everHadCharacter = false;   // 앱 시작 후 최초 캐릭터인지 구분

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = FindFirstObjectByType<MRCharacterWorldRoot>();
    }

    private void Update()
    {
        if (MRUK.Instance == null || MRUK.Instance.GetCurrentRoom() == null) return;
        if (characterRoot == null || characterRoot.CurrentCharacter == null) return;

        GameObject currentCharacter = characterRoot.CurrentCharacter;
        if (_trackedCharacter != currentCharacter)
        {
            bool isSwap = _everHadCharacter;
            _trackedCharacter = currentCharacter;
            
            // 캐릭터 교체가 아닐 때(앱 최초 진입 등)만 배치 초기화(저장 위치 불러오기)를 수행한다.
            // 교체 시에는 기존에 서 있던 PixelSpace의 위치와 낙하 상태를 그대로 상속받는다.
            if (!isSwap)
            {
                _hasPlacedInitially = false;
            }
            
            _isFalling = false;
            _everHadCharacter = true;
        }

        bool isPicking = StatusManager.Instance != null && StatusManager.Instance.IsPicking;
        bool isWalking = StatusManager.Instance != null && StatusManager.Instance.IsWalking;

        // 드래그 종료 시 위치 저장
        if (!isPicking && _wasPicking)
        {
            SavePosition();
        }
        _wasPicking = isPicking;

        // 자율 이동 시작/종료 시 위치 저장
        if (isWalking != _wasWalking)
        {
            SavePosition();
        }
        _wasWalking = isWalking;

        if (isPicking)
        {
            if (_isFalling)
            {
                _isFalling = false;
                if (verboseLog)
                    Debug.Log("[MRFloor/낙하] 드래그 시작 → 낙하 중단");
            }
            _verticalVelocity = 0f;
            return;
        }

        Transform moveTarget = characterRoot.CharacterMoveTarget;
        if (moveTarget == null) return;

        Bounds bounds;
        if (!MRCharacterBounds.TryGet(characterRoot, out bounds)) return;

        Vector3 footPos = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        float targetY = GetSurfaceY(footPos);

        // 초기 배치
        if (!_hasPlacedInitially)
        {
            bool restored = false;

            // 앱 최초 시작 및 캐릭터 교체 시 모두 저장 위치를 복원 시도한다.
            // 단, 저장된 위치가 너무 멀면(방 앵커 변경 등) 안전장치에 의해 복원 실패 처리됨.
            if (TryLoadPosition(out Vector3 loadedPos))
            {
                footPos.x = loadedPos.x;
                footPos.z = loadedPos.z;
                targetY = GetSurfaceY(footPos);
                restored = true;
            }

            footPos.y = targetY;
            Vector3 offset = moveTarget.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            characterRoot.SetCharacterPosition(footPos + offset);
            _hasPlacedInitially = true;
            _verticalVelocity = 0f;

            Debug.Log($"[MRFloor/배치] 초기 배치 완료 — " +
                      $"모드: {(restored ? "저장 위치 복원" : "현재 위치(사용자 앞) 유지")}, " +
                      $"발 위치: {footPos}, 표면 Y: {targetY:F4}");
            return;
        }

        // 낙하 로직
        if (footPos.y > targetY + FallStartThreshold)
        {
            if (!_isFalling)
            {
                _isFalling = true;
                if (verboseLog)
                    Debug.Log($"[MRFloor/낙하] 낙하 시작 — 현재 Y: {footPos.y:F4}, " +
                              $"목표 Y: {targetY:F4}, 차이: {footPos.y - targetY:F4}m, " +
                              $"방식: {(useGravity ? "중력 가속도" : "선형")}");
            }

            if (useGravity)
            {
                _verticalVelocity -= gravityAccel * Time.deltaTime;
                float drop = _verticalVelocity * Time.deltaTime;
                
                if (footPos.y + drop <= targetY)
                {
                    footPos.y = targetY;
                    _verticalVelocity = 0f;
                    _isFalling = false;
                    SavePosition();

                    if (verboseLog)
                        Debug.Log($"[MRFloor/낙하] 착지 — 표면 Y: {targetY:F4}");
                }
                else
                {
                    footPos.y += drop;
                }
            }
            else
            {
                footPos.y = Mathf.MoveTowards(footPos.y, targetY, fallSpeed * Time.deltaTime);
                if (footPos.y <= targetY)
                {
                    _isFalling = false;
                    SavePosition();

                    if (verboseLog)
                        Debug.Log($"[MRFloor/낙하] 착지 — 표면 Y: {targetY:F4}");
                }
            }

            Vector3 offset = moveTarget.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            characterRoot.SetCharacterPosition(footPos + offset);
        }
        else if (footPos.y < targetY - SnapUpThreshold) 
        {
            if (verboseLog)
                Debug.Log($"[MRFloor/배치] 바닥 아래 보정 — 현재 Y: {footPos.y:F4}, " +
                          $"표면 Y: {targetY:F4}, 임계: {SnapUpThreshold}m");

            Vector3 offset = moveTarget.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            footPos.y = targetY;
            characterRoot.SetCharacterPosition(footPos + offset);
            _verticalVelocity = 0f;
            _isFalling = false;
        }
    }

    private void SavePosition()
    {
        if (!usePersistence) return;
        if (MRUK.Instance == null) return;
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return;
        
        MRUKAnchor floor = room.GetFloorAnchor();
        if (floor == null) return;

        Transform moveTarget = characterRoot.CharacterMoveTarget;
        if (moveTarget == null) return;

        Bounds bounds;
        if (!MRCharacterBounds.TryGet(characterRoot, out bounds)) return;
        
        Vector3 footPos = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3 localPos = floor.transform.InverseTransformPoint(footPos);

        PlayerPrefs.SetInt(PREF_HAS_POS, 1);
        PlayerPrefs.SetFloat(PREF_POS_X, localPos.x);
        PlayerPrefs.SetFloat(PREF_POS_Z, localPos.z);
        PlayerPrefs.Save();
        
        Debug.Log($"[MRFloor/저장] 위치 저장 — " +
                  $"로컬: ({localPos.x:F3}, {localPos.z:F3}), " +
                  $"Floor Anchor 월드: {floor.transform.position}");
    }

    /// <summary>
    /// 저장된 위치를 로드한다.
    /// </summary>
    private bool TryLoadPosition(out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (!usePersistence || PlayerPrefs.GetInt(PREF_HAS_POS, 0) == 0) return false;
        
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return false;
        
        MRUKAnchor floor = room.GetFloorAnchor();
        if (floor == null) return false;

        Vector3 localPos = new Vector3(
            PlayerPrefs.GetFloat(PREF_POS_X, 0f),
            0f,
            PlayerPrefs.GetFloat(PREF_POS_Z, 0f)
        );

        worldPos = floor.transform.TransformPoint(localPos);

        if (verboseLog)
            Debug.Log($"[MRFloor/복원] 저장 위치 로드 — " +
                      $"로컬: ({localPos.x:F3}, {localPos.z:F3}), " +
                      $"→ 월드: {worldPos}, " +
                      $"Floor Anchor: {floor.transform.position}");

        return true;
    }

    // 매 프레임 호출된다. 로그는 경고 상황에서만 찍는다.
    // 정상 경로 정보는 호출부(초기 배치, 낙하)에서 이미 로그하고 있다.
    private float GetSurfaceY(Vector3 currentFootPos)
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return 0f;

        LabelFilter filter = new LabelFilter(~MRUKAnchor.SceneLabels.WALL_FACE);
        Ray ray = new Ray(currentFootPos + Vector3.up * 0.1f, Vector3.down);
        
        if (room.Raycast(ray, 10f, filter, out RaycastHit hit, out MRUKAnchor anchor))
        {
            return hit.point.y;
        }

        MRUKAnchor floor = room.GetFloorAnchor();
        if (floor != null)
        {
            return floor.transform.position.y;
        }

        Debug.LogWarning("[MRFloor/표면탐색] Raycast·Floor Anchor 모두 실패 → Y=0 사용");
        return 0f;
    }
}
