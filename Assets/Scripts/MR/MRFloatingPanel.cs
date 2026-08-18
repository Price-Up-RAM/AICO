using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 공간 고정형 UI 패널(설정·캐릭터 목록·기록 등)의 공통 베이스.
///
/// 이 클래스가 담당하는 것: 표시(캔버스·CanvasGroup), 소환 위치 결정, Y축 빌보드,
/// 그리고 레거시 열기 경로(UIManager.SetActive)와의 접합.
///
/// 이 클래스가 담당하지 않는 것:
///  - 손 상호작용(Poke/Ray) — Tools → MR → 5(MRWorldUIInteraction)로 캔버스에 붙인다.
///  - 패널 이동(grab) — ISDK 정식 컴포넌트가 담당한다(Kickoff Guide §4-22~§4-24).
///    예전에 여기 있던 OVRInput 기반 드래그는 제거했다. 순수 핸드트래킹에서
///    OVRInput.Axis1D.*IndexTrigger는 항상 0이라(§4-19) 동작하지도 않으면서
///    매 프레임 Update를 돌던 죽은 코드였다.
///  - 패널 내용물(버튼 배선 등) — 각 패널의 기존 매니저가 그대로 담당한다.
///
/// 캔버스 설정은 확정 레시피를 따른다 (MR_Phase3-2_Canvas_Plan.md §7):
///   Canvas.renderMode = World Space, localScale = 0.001, CanvasScaler.dynamicPixelsPerUnit = 3
/// 이 스크립트는 스케일을 강제하지 않는다 — Tools → MR → 6/9가 캔버스 세팅까지 맞춰준다.
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

    [Header("레거시 열기 경로")]
    [Tooltip("OnEnable 시 자동으로 표시할지. 레거시 UIManager가 SetActive(true)로 여는 패널은 켜둔다. " +
             "스스로 숨었다 나타나는 특수 패널만 끈다.")]
    [SerializeField] private bool showOnEnable = true;

    [Header("이벤트")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    private Camera _cam;
    private bool _isOpen;

    // 한 번이라도 공간에 배치된 적이 있는가.
    // 두 번째부터의 Open()은 배치를 건너뛰어 사용자가 둔 자리를 지킨다 (§4-27).
    private bool _hasBeenPlaced;

    // 비활성화 직전의 월드 포즈. 아래 OnDisable/OnEnable 주석 참고.
    private bool _hasSavedPose;
    private Vector3 _savedPosition;
    private Quaternion _savedRotation;

    public bool IsOpen => _isOpen;
    public bool HasBeenPlaced => _hasBeenPlaced;

    private void Awake()
    {
        _cam = Camera.main;

        if (panelRoot == null) panelRoot = transform;

        if (panelCanvas == null)
        {
            panelCanvas = GetComponentInChildren<Canvas>(true);

            // 자동 탐색은 깊이우선이라, 자기 오브젝트에 Canvas가 없으면
            // **엉뚱한 자식 패널의 캔버스**를 집어온다. 그 상태로 두면
            //  ① Awake에서 남의 패널 캔버스를 꺼버리고
            //  ② Open()이 panelRoot(= 이 오브젝트) 계층 전체를 눈앞으로 옮긴다.
            // 실제로 MR 루트에 이 컴포넌트가 잘못 붙어 WorldUI 계층 전체가 이동 대상이 된 적이 있다
            // (2026-08-18). 동작을 막지는 않되 반드시 눈에 띄게 알린다.
            if (panelCanvas != null && panelCanvas.gameObject != gameObject)
            {
                Debug.LogError($"[MRFloatingPanel] '{name}'에 Canvas가 없어 자식 " +
                               $"'{panelCanvas.name}'의 캔버스를 가져왔습니다. " +
                               "이 컴포넌트는 자기 자신이 패널(Canvas 보유)인 오브젝트에 붙여야 합니다. " +
                               "그룹/루트 오브젝트에 붙었다면 제거하세요 — 계층 전체가 이동 대상이 됩니다.", this);
            }
        }

        if (panelCanvas != null) panelCanvas.enabled = false;
    }

    /// <summary>비활성화 직전의 월드 포즈를 기억한다.
    ///
    /// 왜 필요한가 — 레거시 UIManager.ShowSimpleUI()가 이렇게 동작한다:
    ///
    ///     if (!target.activeSelf)
    ///         targetRect.position = UIPositionManager.Instance.GetMenuPosition(menuName);
    ///     target.SetActive(true);
    ///
    /// 즉 **비활성 상태에서 위치를 덮어쓴 뒤** 활성화한다. 데스크톱 캔버스 기준 픽셀
    /// 좌표라 MR에서는 수백 m 밖으로 날아간다. 좌표 대입이 SetActive보다 **먼저**이므로,
    /// OnEnable에서 이 값으로 되돌리면 공용 파일을 고치지 않고 무력화할 수 있다.
    ///
    /// grab으로 옮긴 위치도 이 경로로 자동 보존된다 — 옮긴 결과가 곧 비활성 직전의 포즈다.</summary>
    private void OnDisable()
    {
        if (panelRoot == null) return;

        _savedPosition = panelRoot.position;
        _savedRotation = panelRoot.rotation;
        _hasSavedPose = true;
    }

    /// <summary>레거시 SetActive(true) 경로의 접합점.
    /// **표시만 담당하고 배치는 하지 않는다.** 배치까지 하면 무언가가 패널을 재활성화할 때마다
    /// 눈앞으로 끌려와 §4-27("내가 옮기면 그 자리에 남는다")과 충돌한다.</summary>
    private void OnEnable()
    {
        if (_hasSavedPose && panelRoot != null)
        {
            // 레거시가 덮어쓴 데스크톱 좌표를 되돌린다.
            panelRoot.position = _savedPosition;
            panelRoot.rotation = _savedRotation;
        }
        else if (panelRoot != null)
        {
            // 최초 표시. 위치는 호출자(UIPositionManager의 MR 분기)가 정한 값을 그대로 쓰고,
            // **회전만** 사용자를 향하게 맞춘다.
            //
            // 씬에 저장된 회전은 에디터에서 패널을 배치할 때의 방향이라, 사용자가 지금
            // 어느 쪽을 보고 있는지와 아무 상관이 없다. 그대로 두면 패널이 등을 돌린 것처럼
            // 보인다 — 실기에서 "모든 UI가 180도 돌아서 나온다"로 나타났다(2026-08-18).
            //
            // Show()는 의도적으로 배치를 하지 않으므로(§4-27), 여기서 최초 1회만 처리한다.
            FaceCameraYAxisOnly();
            _hasBeenPlaced = true;
        }

        if (!showOnEnable) return;

        Show();
    }

    /// <summary>표시만 한다. 위치는 건드리지 않는다.</summary>
    public void Show()
    {
        // GameObject 활성 상태를 레거시와 일치시킨다 — 아래 Close() 주석 참고.
        // 이미 활성이면 no-op이고, OnEnable에서 불린 경우에도 안전하다.
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        _isOpen = true;

        if (panelCanvas != null) panelCanvas.enabled = true;

        ShowUIWidgetIfPresent(true);

        onOpened?.Invoke();
    }

    /// <summary>연다. **최초 소환일 때만** 사용자 정면에 배치하고,
    /// 이후에는 사용자가 둔 자리를 유지한다.</summary>
    public void Open()
    {
        Show();

        if (_hasBeenPlaced) return;

        PlaceInFront();
    }

    /// <summary>지정한 월드 위치에 연다 (예: 손목/손 근처 소환).</summary>
    public void OpenAt(Vector3 worldPosition)
    {
        Show();
        PlaceAt(worldPosition);
    }

    /// <summary>사용자 정면으로 강제 재배치한다. 시야 밖으로 밀려난 패널 회수용.
    /// 표시 상태는 바꾸지 않는다.</summary>
    public void PlaceInFront()
    {
        Transform eye = ResolveEye();

        // 카메라를 못 찾아도 표시는 이미 Show()가 끝냈다.
        // 예전에는 Open()이 여기서 그냥 return해버려서 패널이 영영 안 보였다 (§4-29).
        if (eye == null || panelRoot == null) return;

        Vector3 spawnPos = eye.position + eye.forward * spawnDistance;
        spawnPos.y += spawnHeightOffset;
        PlaceAt(spawnPos);
    }

    private void PlaceAt(Vector3 worldPosition)
    {
        if (panelRoot == null) return;

        panelRoot.position = worldPosition;
        FaceCameraYAxisOnly();

        _hasBeenPlaced = true;

        // 언팩 직후 계층 드래그로 자식 스케일이 튀는 함정(§4-3) 대비.
        if (panelCanvas != null && panelCanvas.transform.localScale.x > 0.1f)
        {
            Debug.LogWarning($"[MRFloatingPanel] '{name}' 캔버스 스케일이 비정상입니다 " +
                              $"({panelCanvas.transform.localScale}). 0.001로 재확인하세요.");
        }
    }

    /// <summary>닫는다.
    ///
    /// **캔버스만 끄지 않고 GameObject까지 비활성화한다.** 레거시 UIManager가
    /// CloseSimpleUI()에서 SetActive(false)를 쓰기 때문이다 — 여기서 GameObject를 활성인 채로
    /// 두면, 이후 레거시 ShowSimpleUI()의 SetActive(true)가 no-op이 되어 OnEnable이 발화하지
    /// 않고 패널이 영영 안 열린다. 두 경로의 "닫힘" 표현이 같아야 한다.
    ///
    /// 비활성화되면서 OnDisable이 월드 포즈를 기억하므로 다시 열 때 자리도 지켜진다.</summary>
    public void Close()
    {
        _isOpen = false;
        ShowUIWidgetIfPresent(false);
        if (panelCanvas != null) panelCanvas.enabled = false;
        onClosed?.Invoke();

        // 마지막에 비활성화한다 — OnDisable이 이 시점의 포즈를 저장한다.
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    /// <summary>빌보드·소환 기준이 되는 "눈" 트랜스폼.
    ///
    /// Camera.main은 OVR 리그에서 null인 프레임이 있어 CenterEyeAnchor를 우선한다.
    ///
    /// ⚠ 마지막 폴백 FindFirstObjectByType&lt;Camera&gt;()는 **컴포넌트가 disabled여도 반환**한다.
    /// 좌안 카메라나 PortraitCamera를 집을 수 있으므로, 조준·빌보드가 미묘하게 어긋나면
    /// 여기부터 의심할 것 (Kickoff Guide §4-28 정정).</summary>
    private Transform ResolveEye()
    {
        if (_cam != null && _cam.isActiveAndEnabled) return _cam.transform;

        var byName = GameObject.Find("CenterEyeAnchor");
        if (byName != null) return byName.transform;

        _cam = Camera.main;
        if (_cam != null) return _cam.transform;

        _cam = FindFirstObjectByType<Camera>();
        if (_cam == null) return null;

        return _cam.transform;
    }

    /// <summary>DevionGames UIWidget이 붙은 패널을 MR에서 보이게/숨기게 만든다.
    ///
    /// 왜 UIWidget.Show()를 직접 부르지 않는가
    /// -------------------------------------
    /// UIWidget.Show()는 `TweenTransformScale(..., Vector3.one)`으로 **스케일을 1로 되돌린다.**
    /// 월드 스페이스 패널의 정상 스케일은 0.0005 수준이라 그대로 부르면 2000배로 부푼다
    /// (실기 확인 2026-08-15). Close()도 스케일을 0으로 트윈해 다시 열 때 꼬인다.
    ///
    /// 그래서 UIWidget이 표시/숨김에 실제로 쓰는 상태값만 직접 세팅한다:
    /// CanvasGroup의 alpha / interactable / blocksRaycasts. 스케일은 건드리지 않는다.</summary>
    private void ShowUIWidgetIfPresent(bool show)
    {
        var widget = GetComponent<DevionGames.UIWidgets.UIWidget>();
        if (widget == null) return;

        var group = GetComponent<CanvasGroup>();
        if (group != null)
        {
            float alpha = 0f;
            if (show) alpha = 1f;

            group.alpha = alpha;
            group.interactable = show;
            group.blocksRaycasts = show;
        }

        if (show && transform.localScale.x <= Mathf.Epsilon)
        {
            transform.localScale = Vector3.one * RecoveredCanvasScale;
            Debug.LogWarning($"[MRFloatingPanel] '{name}' 스케일이 0이라 {RecoveredCanvasScale}로 복구했습니다. " +
                              "UIWidget.Awake()가 alpha!=1인 위젯의 스케일을 0으로 만들기 때문입니다 — " +
                              "원하는 크기가 다르면 인스펙터에서 직접 맞추세요.");
        }
    }

    // UIWidget이 스케일을 0으로 만들어버린 경우의 복구값. 확정 캔버스 레시피 기준.
    private const float RecoveredCanvasScale = 0.0005f;

    private void FaceCameraYAxisOnly()
    {
        Transform eye = ResolveEye();
        if (eye == null || panelRoot == null) return;

        Vector3 dirToCam = panelRoot.position - eye.position;
        dirToCam.y = 0f;
        if (dirToCam.sqrMagnitude > 0.001f)
        {
            panelRoot.rotation = Quaternion.LookRotation(dirToCam);
        }
    }
}
