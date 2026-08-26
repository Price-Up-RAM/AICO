using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    // =========================================================
    // 위치 영속화 — 3단 구조 (2026-08-26 확정)
    // =========================================================
    // 이 앱은 비서다. "방에 두고 나갔다 와도 그 자리에 있다"가 요구사항이다.
    // 예전 구현은 저장 슬롯이 하나뿐이라 A방 좌표를 B방 바닥에 그대로 찍었고,
    // 검증이 없어서 방을 다시 스캔하면 캐릭터가 엉뚱한 곳에 나타났다.
    // (코드 주석은 "너무 멀면 안전장치가 막는다"고 돼 있었지만 그런 코드는 없었다.)
    //
    //   1단  OVRSpatialAnchor  — 재부팅·재스캔·다른 방까지 견딘다. 주 저장소.
    //   2단  방 UUID별 바닥 로컬 (x,z) — 앵커 localize가 실패했을 때. 같은 방·같은 스캔에서만 유효.
    //   3단  눈앞 소환 — 둘 다 실패. 소환 직후 곧바로 1·2단에 다시 저장한다.
    //
    // 사용자와의 거리는 검증하지 않는다. 방 반대편에 서 있는 것은 정상이고,
    // 그것을 오류로 처리하면 "방에 두고 온다"는 요구 자체가 깨진다.
    [Header("위치 영속화 (Persistence)")]
    [SerializeField] private bool usePersistence = true;

    [Tooltip("공간 앵커 localize를 기다리는 최대 시간(초). 이 시간을 넘기면 2단(방 좌표)으로 내려간다. " +
             "실측상 앱 시작 후 캐릭터 스폰까지 4.3초가 걸리므로 2초는 체감되지 않는다.")]
    [SerializeField] private float anchorLocalizeTimeout = 2f;

    private const string PREF_ANCHOR_UUID = "MRCharacter_AnchorUuid";
    private const string PREF_HAS_POS = "MRCharacter_HasPos";
    private const string PREF_POS_X = "MRCharacter_PosX";
    private const string PREF_POS_Z = "MRCharacter_PosZ";

    // 앵커를 캐릭터에 직접 붙이지 않는다.
    // OVRSpatialAnchor는 붙은 오브젝트의 transform을 직접 제어하는데,
    // 캐릭터는 이 스크립트의 중력·바닥 스냅이 움직이므로 서로 싸운다.
    // 빈 홀더가 앵커를 들고, 캐릭터는 홀더의 위치를 '읽기만' 한다.
    private GameObject _anchorHolder;
    private OVRSpatialAnchor _anchor;

    // 복원 상태 기계 — 앵커가 확정될 때까지 초기 배치를 보류한다.
    private bool _restoreStarted;
    private bool _restoreDone;
    private bool _hasRestoredPos;
    private Vector3 _restoredPos;
    private string _restorePath = "미정";

    // 앵커 저장은 비동기라 연속 호출이 겹치면 앵커가 여러 개 생긴다.
    // 실측(2026-08-26 logcat): 0.5초 간격으로 3연속 저장이 찍혔다.
    private bool _saveInFlight;
    private bool _savePending;
    private bool _hasAnchoredPos;
    private Vector3 _lastAnchoredPos;

    // 이만큼 못 움직였으면 앵커는 다시 저장하지 않는다. 2단(PlayerPrefs)은 그래도 매번 갱신된다.
    private const float AnchorResaveDistance = 0.05f;

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
            // 앵커 복원이 끝날 때까지 배치를 보류한다.
            // 먼저 눈앞에 띄웠다가 순간이동시키면 사용자가 그 이동을 보게 되므로,
            // 확정된 뒤 한 번만 놓는다 (최대 anchorLocalizeTimeout 초).
            if (!_restoreDone)
            {
                if (!_restoreStarted)
                {
                    _restoreStarted = true;
                    BeginRestore();
                }
                return;
            }

            if (_hasRestoredPos)
            {
                footPos.x = _restoredPos.x;
                footPos.z = _restoredPos.z;
                targetY = GetSurfaceY(footPos);
            }

            footPos.y = targetY;
            Vector3 offset = moveTarget.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            characterRoot.SetCharacterPosition(footPos + offset);
            _hasPlacedInitially = true;
            _verticalVelocity = 0f;

            Debug.Log($"[MRFloor/배치] 초기 배치 완료 | 경로={_restorePath} | " +
                      $"발 위치={footPos} | 표면 Y={targetY:F4}");

            // 3단(눈앞 소환)으로 왔다면 그 자리를 곧바로 저장해 다음 실행부터는 1단이 먹게 한다.
            // bounds를 다시 읽지 않고 방금 확정한 footPos를 그대로 넘긴다.
            // SetCharacterPosition 직후의 bounds는 아직 이동 전 값이다.
            if (!_hasRestoredPos)
            {
                SaveFootPosition(footPos);
            }
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

    // 현재 방의 고유 키. 방마다 저장 슬롯을 나누기 위한 것이다.
    // MRUKRoom.Anchor.Uuid는 재스캔하지 않는 한 세션을 넘어 유지된다.
    private static string RoomKey(MRUKRoom room)
    {
        if (room == null) return "";
        return room.Anchor.Uuid.ToString();
    }

    // =========================================================
    // 복원 (3단)
    // =========================================================
    private async void BeginRestore()
    {
        float t0 = Time.realtimeSinceStartup;
        MRUKRoom room = null;
        if (MRUK.Instance != null)
        {
            room = MRUK.Instance.GetCurrentRoom();
        }
        string roomKey = RoomKey(room);
        string reason = "";

        _hasRestoredPos = false;
        _restorePath = "눈앞";

        if (!usePersistence)
        {
            reason = "usePersistence=false";
        }
        else
        {
            // --- 1단: 공간 앵커 ---
            string uuidStr = PlayerPrefs.GetString(PREF_ANCHOR_UUID, "");
            Guid savedGuid;
            if (string.IsNullOrEmpty(uuidStr))
            {
                reason = "저장된 앵커 UUID 없음";
            }
            else if (!Guid.TryParse(uuidStr, out savedGuid))
            {
                reason = $"앵커 UUID 파싱 실패('{uuidStr}')";
            }
            else
            {
                Vector3 anchorPos;
                bool got = await TryLocalizeAnchor(savedGuid, r => reason = r);
                if (got && TryReadHolderPosition(out anchorPos))
                {
                    _restoredPos = anchorPos;
                    _hasRestoredPos = true;
                    _restorePath = "앵커";
                    _hasAnchoredPos = true;
                    _lastAnchoredPos = anchorPos;
                }
            }

            // --- 2단: 방 UUID별 바닥 로컬 좌표 ---
            if (!_hasRestoredPos && room != null && !string.IsNullOrEmpty(roomKey))
            {
                Vector3 roomPos;
                if (TryLoadRoomPosition(room, roomKey, out roomPos))
                {
                    // 방 밖으로 나가는 좌표는 버린다. 예전에 없던 검증이다.
                    if (room.IsPositionInRoom(roomPos, false))
                    {
                        _restoredPos = roomPos;
                        _hasRestoredPos = true;
                        _restorePath = "방좌표";
                    }
                    else
                    {
                        reason += " / 방 좌표가 방 밖";
                    }
                }
                else
                {
                    reason += " / 이 방 저장 없음";
                }
            }
        }

        _restoreDone = true;

        string posText = "(없음)";
        if (_hasRestoredPos)
        {
            posText = _restoredPos.ToString("F3");
        }

        string roomText = roomKey;
        if (string.IsNullOrEmpty(roomKey))
        {
            roomText = "(방없음)";
        }

        string reasonText = "";
        if (_restorePath != "앵커")
        {
            reasonText = $" | 사유={reason}";
        }

        Debug.Log($"[MRFloor/복원] 경로={_restorePath} | roomUuid={roomText} | " +
                  $"복원좌표={posText} | 소요={(Time.realtimeSinceStartup - t0):F2}s{reasonText}");
    }

    // 앵커를 불러와 홀더에 바인딩한다. 성공하면 _anchorHolder의 transform이 앵커 위치를 가리킨다.
    private async OVRTask<bool> TryLocalizeAnchor(Guid uuid, Action<string> onFail)
    {
        List<Guid> uuids = new List<Guid> { uuid };
        List<OVRSpatialAnchor.UnboundAnchor> unbound = new List<OVRSpatialAnchor.UnboundAnchor>();

        var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, unbound);
        if (!loadResult.Success || unbound.Count == 0)
        {
            onFail?.Invoke($"앵커 조회 실패(Success={loadResult.Success}, 개수={unbound.Count})");
            return false;
        }

        // timeout을 넘기면 false를 반환한다. 0이면 무한 대기라 반드시 값을 넘긴다.
        bool localized = await unbound[0].LocalizeAsync(anchorLocalizeTimeout);
        if (!localized)
        {
            onFail?.Invoke($"앵커 localize 시간초과({anchorLocalizeTimeout:F1}s)");
            return false;
        }

        EnsureAnchorHolder();
        if (_anchor != null) Destroy(_anchor);
        _anchor = _anchorHolder.AddComponent<OVRSpatialAnchor>();
        unbound[0].BindTo(_anchor);
        return true;
    }

    private bool TryReadHolderPosition(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (_anchorHolder == null) return false;
        pos = _anchorHolder.transform.position;
        // 바인딩 직후 pose가 아직 안 들어왔으면 원점이 찍힌다. 그건 성공으로 치지 않는다.
        if (pos == Vector3.zero) return false;
        return true;
    }

    private void EnsureAnchorHolder()
    {
        if (_anchorHolder != null) return;
        _anchorHolder = new GameObject("AICO Character Anchor");
    }

    private bool TryLoadRoomPosition(MRUKRoom room, string roomKey, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (PlayerPrefs.GetInt(PREF_HAS_POS + "_" + roomKey, 0) == 0) return false;

        MRUKAnchor floor = room.GetFloorAnchor();
        if (floor == null) return false;

        Vector3 localPos = new Vector3(
            PlayerPrefs.GetFloat(PREF_POS_X + "_" + roomKey, 0f),
            0f,
            PlayerPrefs.GetFloat(PREF_POS_Z + "_" + roomKey, 0f)
        );

        worldPos = floor.transform.TransformPoint(localPos);
        return true;
    }

    // =========================================================
    // 저장 (1단 + 2단 동시)
    // =========================================================
    private void SavePosition()
    {
        if (!usePersistence) return;
        if (MRUK.Instance == null) return;
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return;

        MRUKAnchor floor = room.GetFloorAnchor();
        if (floor == null) return;

        if (characterRoot == null || characterRoot.CharacterMoveTarget == null) return;

        Bounds bounds;
        if (!MRCharacterBounds.TryGet(characterRoot, out bounds)) return;

        Vector3 footPos = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        SaveFootPosition(footPos);
    }

    // 실제 저장. 발 위치를 호출부가 이미 알고 있을 때는 이쪽을 직접 부른다.
    private void SaveFootPosition(Vector3 footPos)
    {
        if (!usePersistence) return;
        if (MRUK.Instance == null) return;
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return;

        MRUKAnchor floor = room.GetFloorAnchor();
        if (floor == null) return;

        // 2단: 방별 좌표는 동기라 바로 쓴다.
        string roomKey = RoomKey(room);
        Vector3 localPos = floor.transform.InverseTransformPoint(footPos);
        PlayerPrefs.SetInt(PREF_HAS_POS + "_" + roomKey, 1);
        PlayerPrefs.SetFloat(PREF_POS_X + "_" + roomKey, localPos.x);
        PlayerPrefs.SetFloat(PREF_POS_Z + "_" + roomKey, localPos.z);
        PlayerPrefs.Save();

        Debug.Log($"[MRFloor/저장] 2단 방좌표 저장 | roomUuid={roomKey} | " +
                  $"로컬=({localPos.x:F3}, {localPos.z:F3}) | 발위치={footPos.ToString("F3")}");

        // 1단: 앵커. 거의 안 움직였으면 건너뛴다.
        if (_hasAnchoredPos && Vector3.Distance(_lastAnchoredPos, footPos) < AnchorResaveDistance)
        {
            return;
        }

        // 비동기라 진행 중이면 예약만 하고, 끝난 뒤 한 번 더 돈다.
        if (_saveInFlight)
        {
            _savePending = true;
            return;
        }
        SaveAnchorAt(footPos);
    }

    private async void SaveAnchorAt(Vector3 worldPos)
    {
        _saveInFlight = true;
        string result;

        EnsureAnchorHolder();
        _anchorHolder.transform.position = worldPos;
        _anchorHolder.transform.rotation = Quaternion.identity;

        // 예전 앵커는 지운다. 지우지 않으면 헤드셋에 앵커가 계속 쌓인다.
        if (_anchor != null)
        {
            await _anchor.EraseAnchorAsync();
            Destroy(_anchor);
            _anchor = null;
            // Destroy는 프레임 끝에 반영된다. 한 프레임 넘기지 않으면
            // 같은 오브젝트에 OVRSpatialAnchor가 두 개 붙는 순간이 생긴다.
            await Task.Yield();
        }

        _anchor = _anchorHolder.AddComponent<OVRSpatialAnchor>();
        if (!await _anchor.WhenCreatedAsync())
        {
            result = "앵커 생성 실패";
        }
        else
        {
            var saveResult = await _anchor.SaveAnchorAsync();
            if (saveResult.Success)
            {
                PlayerPrefs.SetString(PREF_ANCHOR_UUID, _anchor.Uuid.ToString());
                PlayerPrefs.Save();
                _hasAnchoredPos = true;
                _lastAnchoredPos = worldPos;
                result = $"성공 uuid={_anchor.Uuid}";
            }
            else
            {
                result = $"앵커 저장 실패({saveResult.Status})";
            }
        }

        Debug.Log($"[MRFloor/저장] 1단 앵커 저장 | {result} | 위치={worldPos.ToString("F3")}");

        _saveInFlight = false;
        if (_savePending)
        {
            _savePending = false;
            SavePosition();
        }
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
