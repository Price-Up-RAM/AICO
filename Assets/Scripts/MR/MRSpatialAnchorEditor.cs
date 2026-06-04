using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Meta.XR.MRUtilityKit;
using Oculus.Interaction.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MR 환경에서 Spatial Anchor를 생성/편집/삭제하는 에디터.
/// 에딧 모드에서 손에서 레이를 쏘고, 핀치하면 바닥에 앵커가 생성됩니다.
/// 벽에 레이가 닿으면 벽에서 수직으로 내려온 바닥 위치에 생성됩니다.
/// 앵커는 PlayerPrefs에 UUID와 이름이 저장되어 앱을 재시작해도 복원됩니다.
/// </summary>
public class MRSpatialAnchorEditor : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject anchorPrefab;

    [Header("Anchor Menu UI (앵커 선택 시 표시)")]
    [SerializeField] private GameObject anchorMenuUI;
    [SerializeField] private TMP_InputField anchorNameInput;
    [SerializeField] private Button deleteButton;
    [SerializeField] private Button closeMenuButton;

    [Header("Keyboard Binder (이름 필드 선택 시 시스템 키보드 호출)")]
    [Tooltip("씬에 배치된 MRTMPVirtualKeyboardBinder. Quest 시스템 키보드(TouchScreenKeyboard)로 입력합니다.")]
    [SerializeField] private MRTMPVirtualKeyboardBinder keyboardBinder;

    [Header("Ray Visual")]
    [Tooltip("레이 시각화용 LineRenderer가 붙은 오브젝트 (없으면 자동 생성)")]
    [SerializeField] private LineRenderer rayLine;

    [Header("Hand Reference")]
    [Tooltip("Interaction SDK의 Hand 컴포넌트 (오른손 권장)")]
    [SerializeField] private Hand rayHand;

    [Header("Layer Settings")]
    [Tooltip("앵커 프리팹이 사용하는 레이어")]
    [SerializeField] private LayerMask anchorLayer;
    [Tooltip("MRUK 바닥/벽 등 씬 메쉬 레이어 (Everything으로 두면 모든 것과 충돌)")]
    [SerializeField] private LayerMask sceneLayer = ~0;

    [Header("MRUK Settings")]
    [SerializeField] private EffectMesh effectMesh;

    [Header("Settings")]
    [SerializeField] private float maxRayDistance = 10f;
    [SerializeField] private float anchorGroundY = 0f;

    // === PlayerPrefs 키 ===
    private const string PREF_ANCHOR_COUNT = "MRAnchor_Count";
    private const string PREF_ANCHOR_UUID = "MRAnchor_UUID_";
    private const string PREF_ANCHOR_NAME = "MRAnchor_Name_";

    // === 내부 상태 ===
    private bool _isEditMode = false;
    private bool _wasPinching = false;
    private bool _isDragging = false;

    private GameObject _activeAnchorNode;
    private List<AnchorData> _createdAnchors = new List<AnchorData>();

    // 레이 포즈 캐시
    private Vector3 _rayOrigin;
    private Vector3 _rayDir;
    private bool _hasRayPose;

    /// <summary>
    /// 앵커 데이터: 프리팹 인스턴스 + 이름 + Spatial Anchor 참조
    /// </summary>
    [System.Serializable]
    public class AnchorData
    {
        public GameObject gameObject;
        public OVRSpatialAnchor spatialAnchor;
        public string anchorName = "";
        public Guid uuid;
    }

    // =============================================
    // 초기화: 저장된 앵커 복원
    // =============================================
    async void Start()
    {
        if (anchorMenuUI != null) anchorMenuUI.SetActive(false);

        // 레이 라인이 없으면 자동 생성
        if (rayLine == null)
        {
            var go = new GameObject("AnchorRayLine");
            go.transform.SetParent(transform);
            rayLine = go.AddComponent<LineRenderer>();
            rayLine.startWidth = 0.003f;
            rayLine.endWidth = 0.003f;
            rayLine.material = new Material(Shader.Find("Sprites/Default"));
            rayLine.startColor = new Color(0f, 0.8f, 1f, 0.8f);
            rayLine.endColor = new Color(0f, 0.8f, 1f, 0.2f);
            rayLine.positionCount = 2;
        }

        if (deleteButton != null) deleteButton.onClick.AddListener(DeleteActiveAnchor);
        if (closeMenuButton != null) closeMenuButton.onClick.AddListener(CloseAnchorMenu);
        if (anchorNameInput != null) anchorNameInput.onEndEdit.AddListener(OnAnchorNameChanged);

        // OVR Virtual Keyboard 자동 연동: 이름 필드를 클릭하면 키보드가 떠오르도록
        if (keyboardBinder != null && anchorNameInput != null)
        {
            keyboardBinder.RegisterInputField(anchorNameInput);
        }

        SetEditMode(false);

        // 저장된 앵커 복원 (약간 지연 후)
        await Task.Delay(2000); // MRUK/Scene 로드 대기
        LoadSavedAnchors();
    }

    // =============================================
    // 1. 버튼에 연결: 편집 모드 토글
    // =============================================
    public void ToggleEditMode()
    {
        SetEditMode(!_isEditMode);
    }

    private void SetEditMode(bool enable)
    {
        _isEditMode = enable;

        if (effectMesh != null)
        {
            effectMesh.HideMesh = !_isEditMode;
        }

        if (rayLine != null) rayLine.enabled = _isEditMode;

        // 모든 기존 앵커의 시각/충돌만 토글 (GameObject는 활성 유지 → Spatial Anchor 트래킹 지속)
        SetAllAnchorsVisible(_isEditMode);

        if (!_isEditMode)
        {
            CloseAnchorMenu();
            _activeAnchorNode = null;
            _isDragging = false;
        }

        Debug.Log($"[AnchorEditor] 편집 모드: {(_isEditMode ? "ON" : "OFF")}");
    }

    /// <summary>
    /// 앵커들의 Renderer/Collider만 토글합니다. OVRSpatialAnchor는 켜둔 채로 트래킹은 유지.
    /// </summary>
    private void SetAllAnchorsVisible(bool visible)
    {
        foreach (var data in _createdAnchors)
        {
            if (data == null || data.gameObject == null) continue;
            ApplyAnchorVisibility(data.gameObject, visible);
        }
    }

    private static void ApplyAnchorVisibility(GameObject anchorGo, bool visible)
    {
        if (anchorGo == null) return;

        var renderers = anchorGo.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = visible;

        var colliders = anchorGo.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = visible;
    }

    // =============================================
    // 2. 버튼에 연결: 방 스캔 재시작
    // =============================================
    public async void LaunchSceneCapture()
    {
        Debug.Log("[AnchorEditor] 방 스캔(Space Setup) 요청 중...");

        // OVRScene.RequestSpaceSetup()은 비동기 태스크를 반환합니다.
        // await하면 사용자가 방 스캔을 완료/취소할 때까지 기다립니다.
        var result = await OVRScene.RequestSpaceSetup();

        Debug.Log($"[AnchorEditor] 방 스캔 결과: {(result ? "성공" : "실패/취소")}");

        // 스캔 완료 후 EffectMesh를 리빌드
        if (result)
        {
            RebuildEffectMesh();
        }
    }

    // =============================================
    // 3. 버튼에 연결: 경계선 리빌드
    // =============================================
    public void RebuildEffectMesh()
    {
        if (effectMesh != null)
        {
            effectMesh.DestroyMesh();
            effectMesh.CreateMesh();
            effectMesh.HideMesh = !_isEditMode;
        }
    }

    // =============================================
    // Update: 레이 + 핀치 상호작용
    // =============================================
    void Update()
    {
        if (!_isEditMode) return;

        // 가상 키보드를 사용 중이라면(키 입력 중) 앵커 핀치 처리는 스킵
        bool keyboardActive = keyboardBinder != null && IsKeyboardOpen();
        if (keyboardActive)
        {
            // 키보드가 떠 있을 때는 ray도 표시하지 않음 (UI 상호작용에 집중)
            if (rayLine != null) rayLine.enabled = false;
            _wasPinching = false; // 키보드 닫힐 때 즉시 핀치 시작으로 오인되지 않도록
            return;
        }

        UpdateRayPose();
        UpdateRayVisual();

        // 핀치 감지
        bool isPinching = false;
        if (rayHand != null && rayHand.IsTrackedDataValid)
        {
            isPinching = rayHand.GetFingerIsPinching(HandFinger.Index);
        }
        else
        {
            isPinching = OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) > 0.5f
                      || OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > 0.5f;
        }

        bool justPinched = isPinching && !_wasPinching;
        bool justReleased = !isPinching && _wasPinching;

        if (justPinched && _hasRayPose)
        {
            HandlePinchStart();
        }

        if (_isDragging && isPinching && _hasRayPose)
        {
            UpdateAnchorDrag();
        }

        if (justReleased && _isDragging)
        {
            _isDragging = false;
            // OVRSpatialAnchor를 옮길 수는 없으므로, 기존 앵커를 erase하고 새 위치에서 다시 생성
            FinalizeAnchorDrag();
        }

        _wasPinching = isPinching;
    }

    // =============================================
    // 레이 포즈: Interaction SDK Hand의 PointerPose 사용
    // =============================================
    private void UpdateRayPose()
    {
        _hasRayPose = false;

        if (rayHand != null && rayHand.IsTrackedDataValid)
        {
            if (rayHand.GetPointerPose(out Pose pointerPose))
            {
                _rayOrigin = pointerPose.position;
                _rayDir = pointerPose.rotation * Vector3.forward;
                _hasRayPose = true;
                return;
            }

            if (rayHand.GetJointPose(HandJointId.HandIndexTip, out Pose indexPose))
            {
                rayHand.GetJointPose(HandJointId.HandWristRoot, out Pose wristPose);
                _rayOrigin = indexPose.position;
                _rayDir = (indexPose.position - wristPose.position).normalized;
                _hasRayPose = true;
                return;
            }
        }

        // 컨트롤러 fallback
        Vector3 ctrlPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        Quaternion ctrlRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

        Camera cam = Camera.main;
        if (cam != null && cam.transform.parent != null)
        {
            Transform rig = cam.transform.parent;
            ctrlPos = rig.TransformPoint(ctrlPos);
            ctrlRot = rig.rotation * ctrlRot;
        }

        _rayOrigin = ctrlPos;
        _rayDir = ctrlRot * Vector3.forward;
        _hasRayPose = true;
    }

    // =============================================
    // 레이 시각화
    // =============================================
    private void UpdateRayVisual()
    {
        if (rayLine == null || !_hasRayPose)
        {
            if (rayLine != null) rayLine.enabled = false;
            return;
        }

        rayLine.enabled = true;
        Ray ray = new Ray(_rayOrigin, _rayDir);

        Vector3 endPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, sceneLayer | anchorLayer))
        {
            endPoint = hit.point;
        }
        else
        {
            endPoint = _rayOrigin + _rayDir * maxRayDistance;
        }

        rayLine.SetPosition(0, _rayOrigin);
        rayLine.SetPosition(1, endPoint);
    }

    // =============================================
    // 핀치 시작: 기존 앵커 선택 or 새 앵커 생성
    // =============================================
    private void HandlePinchStart()
    {
        Ray ray = new Ray(_rayOrigin, _rayDir);

        // 우선순위 1: 기존 앵커를 찍었는가?
        if (Physics.Raycast(ray, out RaycastHit anchorHit, maxRayDistance, anchorLayer))
        {
            // 매 핀치마다 SelectAnchor를 호출해 InputField 텍스트를 항상 현재 이름으로 갱신
            SelectAnchor(anchorHit.collider.gameObject);
            BeginAnchorDrag();
            return;
        }

        // 우선순위 2: 씬(바닥/벽)을 찍었는가? → 새 앵커 생성
        if (Physics.Raycast(ray, out RaycastHit sceneHit, maxRayDistance, sceneLayer))
        {
            Vector3 spawnPos = ProjectToFloor(sceneHit.point, sceneHit.normal);
            CreateNewAnchor(spawnPos);
            return;
        }
    }

    /// <summary>
    /// 드래그 시작: OVRSpatialAnchor의 transform 동기화를 멈춰야 자유 이동 가능.
    /// </summary>
    private void BeginAnchorDrag()
    {
        _isDragging = true;
        if (_activeAnchorNode == null) return;

        var anchor = _activeAnchorNode.GetComponent<OVRSpatialAnchor>();
        if (anchor != null && anchor.enabled)
        {
            // 컴포넌트를 끄면 매 프레임 transform 덮어쓰기가 멈춘다
            anchor.enabled = false;
        }
    }

    // =============================================
    // 벽 → 바닥 투영
    // =============================================
    private Vector3 ProjectToFloor(Vector3 hitPoint, Vector3 hitNormal)
    {
        bool isFloorOrCeiling = Mathf.Abs(hitNormal.y) > 0.5f;

        if (isFloorOrCeiling)
        {
            return hitPoint;
        }
        else
        {
            Ray downRay = new Ray(hitPoint, Vector3.down);
            if (Physics.Raycast(downRay, out RaycastHit floorHit, 5f, sceneLayer))
            {
                return floorHit.point;
            }
            else
            {
                return new Vector3(hitPoint.x, anchorGroundY, hitPoint.z);
            }
        }
    }

    // =============================================
    // 앵커 생성
    // =============================================
    private void CreateNewAnchor(Vector3 position)
    {
        if (anchorPrefab == null)
        {
            Debug.LogError("[AnchorEditor] anchorPrefab이 비어있습니다!");
            return;
        }

        GameObject newObj = Instantiate(anchorPrefab, position, Quaternion.identity);
        ApplyAnchorVisibility(newObj, _isEditMode);

        var data = new AnchorData
        {
            gameObject = newObj,
            anchorName = $"Anchor_{_createdAnchors.Count}"
        };

        var spatialAnchor = newObj.GetComponent<OVRSpatialAnchor>();
        if (spatialAnchor != null)
        {
            data.spatialAnchor = spatialAnchor;
            // OVRSpatialAnchor가 Created 상태가 될 때까지 기다린 후 저장
            WaitAndSaveAnchor(data);
        }

        _createdAnchors.Add(data);
        _activeAnchorNode = newObj;
        SelectAnchor(newObj);

        Debug.Log($"[AnchorEditor] 새 앵커 생성: {data.anchorName} at {position}");
    }

    /// <summary>
    /// 앵커가 Created 되면 UUID를 저장하고 PlayerPrefs에 기록
    /// </summary>
    private async void WaitAndSaveAnchor(AnchorData data)
    {
        if (data.spatialAnchor == null) return;

        // Created가 될 때까지 대기
        while (!data.spatialAnchor.Created) await Task.Yield();

        data.uuid = data.spatialAnchor.Uuid;
        Debug.Log($"[AnchorEditor] 앵커 UUID 획득: {data.uuid}");

        // 기기 저장소에 저장 (앱 재시작 후 복원 가능)
        var result = await data.spatialAnchor.SaveAnchorAsync();
        Debug.Log($"[AnchorEditor] 앵커 저장: {(result.Success ? "성공" : "실패")}");

        // PlayerPrefs에 UUID + 이름 매핑 저장
        SaveAllAnchorsToPrefs();
    }

    /// <summary>
    /// 단일 앵커 재저장 (드래그 후 등)
    /// </summary>
    private async void SaveSingleAnchorAsync(OVRSpatialAnchor anchor)
    {
        if (anchor == null || !anchor.Created) return;
        var result = await anchor.SaveAnchorAsync();
        Debug.Log($"[AnchorEditor] 앵커 재저장: {(result.Success ? "성공" : "실패")}");
    }

    // =============================================
    // PlayerPrefs 저장/로드
    // =============================================
    private void SaveAllAnchorsToPrefs()
    {
        // 유효한 앵커만 필터
        var validAnchors = _createdAnchors.Where(a => a.gameObject != null && a.uuid != Guid.Empty).ToList();

        PlayerPrefs.SetInt(PREF_ANCHOR_COUNT, validAnchors.Count);
        for (int i = 0; i < validAnchors.Count; i++)
        {
            PlayerPrefs.SetString(PREF_ANCHOR_UUID + i, validAnchors[i].uuid.ToString());
            PlayerPrefs.SetString(PREF_ANCHOR_NAME + i, validAnchors[i].anchorName);
        }
        PlayerPrefs.Save();
        Debug.Log($"[AnchorEditor] {validAnchors.Count}개 앵커 정보를 PlayerPrefs에 저장");
    }

    private async void LoadSavedAnchors()
    {
        int count = PlayerPrefs.GetInt(PREF_ANCHOR_COUNT, 0);
        if (count == 0)
        {
            Debug.Log("[AnchorEditor] 저장된 앵커가 없습니다.");
            return;
        }

        Debug.Log($"[AnchorEditor] {count}개 앵커 복원 시도...");

        // UUID와 이름 복원
        var uuids = new List<Guid>();
        var names = new Dictionary<Guid, string>();

        for (int i = 0; i < count; i++)
        {
            string uuidStr = PlayerPrefs.GetString(PREF_ANCHOR_UUID + i, "");
            string name = PlayerPrefs.GetString(PREF_ANCHOR_NAME + i, $"Anchor_{i}");

            if (Guid.TryParse(uuidStr, out Guid uuid) && uuid != Guid.Empty)
            {
                uuids.Add(uuid);
                names[uuid] = name;
            }
        }

        if (uuids.Count == 0)
        {
            Debug.Log("[AnchorEditor] 유효한 UUID가 없습니다.");
            return;
        }

        // SDK를 통해 저장된 앵커를 로드
        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
        var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, unboundAnchors);

        if (!loadResult.TryGetValue(out var loadedAnchors))
        {
            Debug.LogWarning($"[AnchorEditor] 앵커 로드 실패: {loadResult.Status}");
            return;
        }

        Debug.Log($"[AnchorEditor] {loadedAnchors.Count}개 UnboundAnchor 로드 성공");

        // 각 UnboundAnchor를 Localize → BindTo → 프리팹 생성
        foreach (var unbound in loadedAnchors)
        {
            bool localized = await unbound.LocalizeAsync();
            if (!localized)
            {
                Debug.LogWarning($"[AnchorEditor] 앵커 {unbound.Uuid} Localize 실패");
                continue;
            }

            // 위치 획득
            if (!unbound.TryGetPose(out Pose pose))
            {
                Debug.LogWarning($"[AnchorEditor] 앵커 {unbound.Uuid} 위치를 가져올 수 없습니다.");
                continue;
            }

            // 프리팹 인스턴스 생성
            GameObject obj = Instantiate(anchorPrefab, pose.position, pose.rotation);
            ApplyAnchorVisibility(obj, _isEditMode);

            // OVRSpatialAnchor에 바인딩
            var spatialAnchor = obj.GetComponent<OVRSpatialAnchor>();
            if (spatialAnchor == null)
            {
                spatialAnchor = obj.AddComponent<OVRSpatialAnchor>();
            }
            unbound.BindTo(spatialAnchor);

            string anchorName = names.ContainsKey(unbound.Uuid) ? names[unbound.Uuid] : $"Anchor_{_createdAnchors.Count}";

            var data = new AnchorData
            {
                gameObject = obj,
                spatialAnchor = spatialAnchor,
                anchorName = anchorName,
                uuid = unbound.Uuid
            };

            _createdAnchors.Add(data);
            Debug.Log($"[AnchorEditor] 앵커 복원 완료: {anchorName} ({unbound.Uuid})");
        }
    }

    // =============================================
    // 앵커 선택: 메뉴 UI 표시
    // =============================================
    private void SelectAnchor(GameObject anchorNode)
    {
        _activeAnchorNode = anchorNode;

        if (anchorMenuUI != null)
        {
            anchorMenuUI.SetActive(true);
            anchorMenuUI.transform.position = anchorNode.transform.position + Vector3.up * 5f;

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 lookDir = anchorMenuUI.transform.position - cam.transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.001f)
                    anchorMenuUI.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }

        // InputField에 현재 앵커의 이름을 기본값으로 채워넣기
        if (anchorNameInput != null)
        {
            var data = FindAnchorData(anchorNode);
            string currentName = (data != null && !string.IsNullOrEmpty(data.anchorName))
                ? data.anchorName
                : (data != null ? $"Anchor_{_createdAnchors.IndexOf(data)}" : "");

            // SetTextWithoutNotify로 onValueChanged를 막아 콜백이 잘못 발화되지 않도록 함
            // (단, 일부 TMP_InputField 버전은 SetTextWithoutNotify 미지원이므로 안전하게 fallback)
            try { anchorNameInput.SetTextWithoutNotify(currentName); }
            catch { anchorNameInput.text = currentName; }

            // TMP InputField가 다음 프레임에 caret 위치를 잡도록 강제 갱신
            anchorNameInput.ForceLabelUpdate();
        }
    }

    // =============================================
    // 드래그 중 앵커 위치 업데이트
    // =============================================
    private void UpdateAnchorDrag()
    {
        if (_activeAnchorNode == null) return;

        Ray ray = new Ray(_rayOrigin, _rayDir);

        // 드래그 시 ray가 앵커 자기 자신(또는 다른 앵커)에 막히지 않도록
        // sceneLayer에서 anchorLayer 비트를 제외한 마스크로 raycast
        int dragMask = sceneLayer & ~anchorLayer.value;
        if (dragMask == 0) dragMask = sceneLayer; // 안전장치: 둘 다 동일 레이어로 잘못 설정된 경우

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, dragMask))
        {
            Vector3 newPos = ProjectToFloor(hit.point, hit.normal);
            _activeAnchorNode.transform.position = newPos;

            if (anchorMenuUI != null && anchorMenuUI.activeSelf)
            {
                anchorMenuUI.transform.position = newPos + Vector3.up * 0.25f;
            }
        }
    }

    /// <summary>
    /// 드래그 종료 처리: OVRSpatialAnchor는 같은 위치에 고정이므로
    /// 기존 앵커를 erase한 뒤 새 위치에서 새 컴포넌트를 만들어 다시 저장한다.
    /// </summary>
    private async void FinalizeAnchorDrag()
    {
        if (_activeAnchorNode == null) return;

        var data = FindAnchorData(_activeAnchorNode);
        if (data == null) return;

        Vector3 finalPos = _activeAnchorNode.transform.position;
        Quaternion finalRot = _activeAnchorNode.transform.rotation;
        string oldName = data.anchorName;
        Guid oldUuid = data.uuid;
        var oldAnchor = data.spatialAnchor;

        // 1. 기존 OVRSpatialAnchor erase + Destroy (새 컴포넌트를 같은 GO에 붙이기 위해)
        if (oldAnchor != null)
        {
            try
            {
                if (oldAnchor.Created)
                {
                    var eraseResult = await oldAnchor.EraseAnchorAsync();
                    Debug.Log($"[AnchorEditor] 기존 앵커 erase: {(eraseResult.Success ? "성공" : "실패")}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AnchorEditor] erase 중 예외 (무시): {e.Message}");
            }
            Destroy(oldAnchor);
        }

        // 한 프레임 대기 - Destroy(컴포넌트)가 실제로 적용되도록
        await Task.Yield();

        // 2. transform 위치를 다시 한 번 강제 설정 (혹시 모를 보정)
        if (_activeAnchorNode == null) return;
        _activeAnchorNode.transform.SetPositionAndRotation(finalPos, finalRot);

        // 3. 새 OVRSpatialAnchor 추가 (활성 시 자동으로 CreateSpatialAnchor 호출)
        var newAnchor = _activeAnchorNode.AddComponent<OVRSpatialAnchor>();
        data.spatialAnchor = newAnchor;
        data.uuid = Guid.Empty; // 잠시 비워두고 Created 후 갱신

        // 4. Created → Save → PlayerPrefs 업데이트
        try
        {
            bool created = await newAnchor.WhenCreatedAsync();
            if (!created)
            {
                Debug.LogWarning("[AnchorEditor] 새 앵커 생성 실패 - 드래그 결과를 저장할 수 없습니다.");
                return;
            }
            data.uuid = newAnchor.Uuid;

            var saveResult = await newAnchor.SaveAnchorAsync();
            Debug.Log($"[AnchorEditor] 드래그 후 새 앵커 저장: {(saveResult.Success ? "성공" : "실패")} (이름 유지: {oldName}, 이전 UUID: {oldUuid})");

            SaveAllAnchorsToPrefs();
        }
        catch (Exception e)
        {
            Debug.LogError($"[AnchorEditor] 드래그 종료 처리 중 예외: {e}");
        }
    }

    // =============================================
    // 이름 변경 콜백
    // =============================================
    private void OnAnchorNameChanged(string newName)
    {
        if (_activeAnchorNode == null) return;
        var data = FindAnchorData(_activeAnchorNode);
        if (data != null)
        {
            data.anchorName = newName;
            SaveAllAnchorsToPrefs();
            Debug.Log($"[AnchorEditor] 앵커 이름 변경: {newName}");
        }
    }

    // =============================================
    // 앵커 삭제
    // =============================================
    public async void DeleteActiveAnchor()
    {
        if (_activeAnchorNode == null) return;

        var data = FindAnchorData(_activeAnchorNode);

        if (data != null && data.spatialAnchor != null && data.spatialAnchor.Created)
        {
            var result = await data.spatialAnchor.EraseAnchorAsync();
            Debug.Log($"[AnchorEditor] 앵커 삭제: {(result.Success ? "성공" : "실패")}");
        }

        if (data != null) _createdAnchors.Remove(data);
        Destroy(_activeAnchorNode);
        _activeAnchorNode = null;
        _isDragging = false;
        CloseAnchorMenu();

        SaveAllAnchorsToPrefs();
    }

    // =============================================
    // 앵커 전체 초기화 (모든 앵커 삭제 + 방 재스캔)
    // =============================================
    public async void ResetAllAnchors()
    {
        Debug.Log("[AnchorEditor] 모든 앵커 초기화...");

        // 모든 앵커 삭제
        foreach (var data in _createdAnchors)
        {
            if (data.spatialAnchor != null && data.spatialAnchor.Created)
            {
                await data.spatialAnchor.EraseAnchorAsync();
            }
            if (data.gameObject != null) Destroy(data.gameObject);
        }
        _createdAnchors.Clear();
        _activeAnchorNode = null;
        CloseAnchorMenu();

        // PlayerPrefs 초기화
        PlayerPrefs.SetInt(PREF_ANCHOR_COUNT, 0);
        PlayerPrefs.Save();

        Debug.Log("[AnchorEditor] 앵커 초기화 완료. 방 재스캔을 시작합니다.");
        LaunchSceneCapture();
    }

    // =============================================
    // 메뉴 닫기
    // =============================================
    public void CloseAnchorMenu()
    {
        if (anchorMenuUI != null) anchorMenuUI.SetActive(false);

        // 키보드도 함께 닫기
        if (keyboardBinder != null) keyboardBinder.HideKeyboard();
    }

    // =============================================
    // 유틸리티
    // =============================================
    private AnchorData FindAnchorData(GameObject obj)
    {
        foreach (var data in _createdAnchors)
        {
            if (data.gameObject == obj) return data;
        }
        return null;
    }

    /// <summary>
    /// 외부에서 앵커 이름으로 위치를 검색 (캐릭터 이동용)
    /// </summary>
    public Vector3? GetAnchorPositionByName(string name)
    {
        foreach (var data in _createdAnchors)
        {
            if (data.anchorName == name && data.gameObject != null)
                return data.gameObject.transform.position;
        }
        return null;
    }

    /// <summary>
    /// 생성된 앵커 이름 목록 (UI 드롭다운 등에 사용)
    /// </summary>
    public List<string> GetAllAnchorNames()
    {
        var namesList = new List<string>();
        foreach (var data in _createdAnchors)
        {
            if (data.gameObject != null)
                namesList.Add(data.anchorName);
        }
        return namesList;
    }

    public bool IsEditMode => _isEditMode;

    /// <summary>
    /// 가상 키보드 활성 상태 확인
    /// </summary>
    private bool IsKeyboardOpen()
    {
        return keyboardBinder != null && keyboardBinder.IsOpen;
    }
}