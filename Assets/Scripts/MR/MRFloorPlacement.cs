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

    private bool _hasPlacedInitially = false;
    private float _verticalVelocity = 0f;
    private bool _wasPicking = false;
    private GameObject _trackedCharacter;

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
            _trackedCharacter = currentCharacter;
            _hasPlacedInitially = false;
        }

        bool isPicking = StatusManager.Instance != null && StatusManager.Instance.IsPicking;

        // 드래그 종료 시 위치 저장
        if (!isPicking && _wasPicking)
        {
            SavePosition();
        }
        _wasPicking = isPicking;

        if (isPicking)
        {
            _verticalVelocity = 0f;
            return;
        }

        Transform moveTarget = characterRoot.CharacterMoveTarget;
        if (moveTarget == null) return;

        Bounds bounds;
        if (!MRCharacterBounds.TryGet(characterRoot, out bounds)) return;

        Vector3 footPos = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        float targetY = GetSurfaceY(footPos);

        // 초기 배치 (로드)
        if (!_hasPlacedInitially)
        {
            if (TryLoadPosition(out Vector3 loadedPos))
            {
                footPos.x = loadedPos.x;
                footPos.z = loadedPos.z;
                // 높이는 Raycast/Floor로 다시 잰다
                targetY = GetSurfaceY(footPos);
            }
            footPos.y = targetY;
            Vector3 offset = moveTarget.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            characterRoot.SetCharacterPosition(footPos + offset);
            _hasPlacedInitially = true;
            _verticalVelocity = 0f;
            return;
        }

        // 낙하 로직
        if (footPos.y > targetY + 0.001f)
        {
            if (useGravity)
            {
                _verticalVelocity -= gravityAccel * Time.deltaTime;
                float drop = _verticalVelocity * Time.deltaTime;
                
                if (footPos.y + drop <= targetY)
                {
                    footPos.y = targetY;
                    _verticalVelocity = 0f;
                    SavePosition();
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
                    SavePosition();
                }
            }

            Vector3 offset = moveTarget.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            characterRoot.SetCharacterPosition(footPos + offset);
        }
        else if (footPos.y < targetY - 0.05f) 
        {
            Vector3 offset = moveTarget.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            footPos.y = targetY;
            characterRoot.SetCharacterPosition(footPos + offset);
            _verticalVelocity = 0f;
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
        
        Debug.Log($"[MRFloorPlacement] 위치 저장됨: {localPos}");
    }

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
        return true;
    }

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

        return 0f;
    }
}
