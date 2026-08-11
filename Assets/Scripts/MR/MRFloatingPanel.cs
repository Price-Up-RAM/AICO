using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 공간 고정형 UI 패널(설정·캐릭터 목록·기록 등)의 공통 베이스.
///
/// MRCharacterMenu가 이미 실기(Quest 3S)에서 검증한 패턴을 그대로 일반화했다
/// (MR_Phase3-2_Canvas_Plan.md §3-2-C — "새로 설계하지 않는다. MRCharacterMenu 패턴을 그대로 복제한다").
///
/// 이 클래스가 담당하는 것: 사용자 정면 소환, 핀치로 패널 근처를 잡아 드래그 이동, 빌보드(Y축만).
/// 이 클래스가 담당하지 않는 것: 손 상호작용(Poke/Ray) 부착 — Tools → MR → 5(MRWorldUIInteraction)로
/// 캔버스에 붙인다. 패널 내용물(버튼 배선 등)도 각 패널의 기존 매니저가 그대로 담당한다 —
/// 이 컴포넌트는 같은 오브젝트에 나란히 붙여서 "어디에 뜨고 어떻게 옮겨지는가"만 넘겨받는다.
///
/// 캔버스 설정은 확정 레시피를 따른다 (MR_Phase3-2_Canvas_Plan.md §7):
///   Canvas.renderMode = World Space, localScale = 0.001, CanvasScaler.dynamicPixelsPerUnit = 3
/// 이 스크립트는 스케일을 강제하지 않는다 — Tools → MR → "선택 오브젝트를 플로팅 패널로 변환"이
/// 캔버스 세팅까지 같이 맞춰준다.
/// </summary>
public class MRFloatingPanel : MonoBehaviour
{
    [Header("References")]
    [Tooltip("패널 전체를 옮기기 위한 최상위 Transform. 보통 Canvas의 부모(또는 Canvas 본인).")]
    [SerializeField] private Transform panelRoot;
    [SerializeField] private Canvas panelCanvas;

    [Header("소환")]
    [Tooltip("열릴 때 사용자 정면 이 거리(m)에 소환한다.")]
    [SerializeField] private float spawnDistance = 0.6f;
    [Tooltip("소환 높이 보정(m). 눈높이 대비 오프셋.")]
    [SerializeField] private float spawnHeightOffset = -0.1f;

    [Header("드래그")]
    [Tooltip("패널 중심으로부터 이 반경(m) 안에서 핀치하면 드래그를 시작한다.")]
    [SerializeField] private float dragGrabRadius = 0.15f;
    [Tooltip("핀치 판정 임계값 (OVRInput.Axis1D.PrimaryIndexTrigger 기준)")]
    [SerializeField] private float pinchThreshold = 0.5f;

    [Header("이벤트")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    private Camera _cam;
    private bool _isOpen;

    private bool _isDragging;
    private OVRInput.Controller _dragController;
    private Vector3 _dragOffset;
    private float _dragDistance;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        _cam = Camera.main;

        if (panelRoot == null) panelRoot = transform;
        if (panelCanvas == null) panelCanvas = GetComponentInChildren<Canvas>(true);

        if (panelCanvas != null) panelCanvas.enabled = false;
    }

    private void Update()
    {
        if (!_isOpen || panelCanvas == null || _cam == null) return;

        bool leftPinch = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > pinchThreshold;
        bool rightPinch = OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) > pinchThreshold;

        if (!_isDragging)
        {
            if (leftPinch || rightPinch)
            {
                OVRInput.Controller ctrl = leftPinch ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
                Vector3 handPos = OVRInput.GetLocalControllerPosition(ctrl);
                // 컨트롤러 로컬 좌표를 카메라 리그 기준 월드로 변환 (MRCharacterMenu와 동일 관례)
                Transform rigRoot = _cam.transform.parent != null ? _cam.transform.parent : _cam.transform;
                Vector3 handWorldPos = rigRoot.TransformPoint(handPos);

                if (Vector3.Distance(handWorldPos, panelRoot.position) < dragGrabRadius)
                {
                    _isDragging = true;
                    _dragController = ctrl;
                    _dragDistance = Vector3.Distance(_cam.transform.position, panelRoot.position);
                    _dragOffset = panelRoot.position - handWorldPos;
                }
            }
        }
        else
        {
            bool stillPinching = _dragController == OVRInput.Controller.LTouch ? leftPinch : rightPinch;
            if (!stillPinching)
            {
                _isDragging = false;
            }
            else
            {
                Transform rigRoot = _cam.transform.parent != null ? _cam.transform.parent : _cam.transform;
                Vector3 handWorldPos = rigRoot.TransformPoint(OVRInput.GetLocalControllerPosition(_dragController));
                Vector3 targetPos = handWorldPos + _dragOffset;

                Vector3 dirFromCam = (targetPos - _cam.transform.position).normalized;
                panelRoot.position = _cam.transform.position + dirFromCam * _dragDistance;

                FaceCameraYAxisOnly();
            }
        }
    }

    /// <summary>사용자 정면에 소환하며 연다. 이미 열려 있으면 위치만 갱신한다.</summary>
    public void Open()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || panelRoot == null) return;

        Vector3 spawnPos = _cam.transform.position + _cam.transform.forward * spawnDistance;
        spawnPos.y += spawnHeightOffset;
        OpenAt(spawnPos);
    }

    /// <summary>지정한 월드 위치에 연다 (예: 손목/손 근처 소환).</summary>
    public void OpenAt(Vector3 worldPosition)
    {
        if (_cam == null) _cam = Camera.main;
        _isOpen = true;

        if (panelCanvas != null) panelCanvas.enabled = true;

        if (panelRoot != null)
        {
            panelRoot.position = worldPosition;
            FaceCameraYAxisOnly();

            // 언팩 직후 계층 드래그로 자식 스케일이 튀는 함정(§4-3) 대비 — 캔버스 스케일만 강제 확인.
            if (panelCanvas != null && panelCanvas.transform.localScale.x > 0.1f)
            {
                Debug.LogWarning($"[MRFloatingPanel] '{name}' 캔버스 스케일이 비정상입니다 " +
                                  $"({panelCanvas.transform.localScale}). 0.001로 재확인하세요.");
            }
        }

        onOpened?.Invoke();
    }

    public void Close()
    {
        _isOpen = false;
        _isDragging = false;
        if (panelCanvas != null) panelCanvas.enabled = false;
        onClosed?.Invoke();
    }

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    private void FaceCameraYAxisOnly()
    {
        if (_cam == null || panelRoot == null) return;
        Vector3 dirToCam = panelRoot.position - _cam.transform.position;
        dirToCam.y = 0f;
        if (dirToCam.sqrMagnitude > 0.001f)
        {
            panelRoot.rotation = Quaternion.LookRotation(dirToCam);
        }
    }
}
