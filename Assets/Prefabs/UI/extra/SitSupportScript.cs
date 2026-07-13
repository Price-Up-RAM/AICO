using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SitSupport — ChillWithYou 착석 오프셋 튜닝 패널 (디버그용, 본편/데모 겸용).
/// 자체 착석 로직은 없다: 착석/복귀/오프셋 적용은 전부 ChillModeManager가 수행하고,
/// 이 패널은 슬라이더 값을 SetCharacterOffset/SetChairOffset과 ChillSitData 책상 필드로 기록한다.
/// - 본편: menutrigger의 Dev → SitSupport로 TogglePanel(). 착석 토글은 ChatModeManager.ToggleMode(Pomodoro) 경유
///   (타이머 UI 표시/채팅 차단 포함). 대상 캐릭터는 ChillModeManager/CharManager에서 자동 결정.
/// - 데모(ChillWithYouSample): ChatModeManager가 없으므로 ChillModeManager.ToggleChillMode() 직접 호출.
/// - 책상 좌표 모델(시트 앵커): "위치 X/Y" = 착석 지점(chairSeatPoint)의 목표 좌표.
///   deskPositionOffset = 앵커 − R(각도)×(시트 데스크로컬 × 배율)을 해석적으로 계산해,
///   회전(턴테이블/±15°)·전체 크기가 바뀌어도 X/Y 값은 불변, 캐릭터가 그 좌표에 고정된 채 자전한다.
///   ※ 책상 원본 transform이 항등(0/identity/1)이라는 전제를 사용한다(본편/데모 모두 해당).
/// UI 참조는 프리팹 베이크 시(SitSupportBuilder) 주입된다.
/// </summary>
public class SitSupportScript : MonoBehaviour
{
    public static SitSupportScript Instance { get; private set; }

    [Header("패널")]
    public GameObject panel; // 표시/숨김 대상 (루트 캔버스는 항상 활성)

    [Header("UI - 모드/재생")]
    public Button enterButton;
    public TMP_Text enterButtonLabel;
    public Button pauseButton;
    public TMP_Text pauseButtonLabel;

    [Header("UI - 캐릭터 착석 오프셋")]
    public Slider charXSlider;
    public TMP_Text charXValueLabel;
    public Slider charYSlider;
    public TMP_Text charYValueLabel;
    public Slider charZSlider;
    public TMP_Text charZValueLabel;
    public Slider charScaleSlider;
    public TMP_Text charScaleValueLabel;
    public Slider charRotYSlider;
    public TMP_Text charRotYValueLabel;

    [Header("UI - 의자 오프셋")]
    public Slider chairXSlider;
    public TMP_Text chairXValueLabel;
    public Slider chairYSlider;
    public TMP_Text chairYValueLabel;
    public Slider chairZSlider;
    public TMP_Text chairZValueLabel;

    [Header("UI - 책상 (시트 앵커 기준)")]
    public Slider deskXSlider;
    public TMP_Text deskXValueLabel;
    public Slider deskYSlider;
    public TMP_Text deskYValueLabel;
    public Slider deskScaleSlider;
    public TMP_Text deskScaleValueLabel;
    public TMP_Text angleValueLabel;
    public Button turntableButton;
    public TMP_Text turntableButtonLabel;
    public Button frontViewButton;
    public Button yawMinusButton;
    public Button yawPlusButton;

    [Header("UI - 기타")]
    public Button logButton;
    public Button saveButton;
    public Button resetButton;

    private const float TurntableSpeedDegPerSec = 30f;

    private ChillModeManager _chillManager;
    private ChillSitData _sitData;

    private bool _turntable;
    private bool _paused;
    private bool _syncingUI; // 슬라이더 값 코드 세팅 중 리스너 무시용
    private bool _slidersInteractable = true; // 비착석 중 슬라이더 잠금 상태 캐시
    private string _lastCharcode = ""; // charcode 변경 감지(캐릭터 교체 시 슬라이더 자동 리로드)
    private bool _baselineCaptured;

    // 시트 앵커: 착석 지점의 목표 좌표(책상 부모 로컬). "위치 X/Y" 슬라이더가 이 값을 편집한다.
    private Vector3 _seatAnchor;

    // 리셋/정면 복귀용 시작 시점 기준값
    private Vector3 _baseSeatAnchor;
    private Vector3 _baseDeskRotationOffset;
    private float _baseDeskScaleMultiplier = 1f;
    private readonly Dictionary<string, ChillSitData.CharacterSitOffset> _snapshots =
        new Dictionary<string, ChillSitData.CharacterSitOffset>(); // charcode별 시작 시점 착석값

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (enterButton != null) enterButton.onClick.AddListener(ToggleChill);
        if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
        if (turntableButton != null) turntableButton.onClick.AddListener(ToggleTurntable);
        if (frontViewButton != null) frontViewButton.onClick.AddListener(ResetDeskPose);
        if (yawMinusButton != null) yawMinusButton.onClick.AddListener(() => AddDeskYaw(-15f));
        if (yawPlusButton != null) yawPlusButton.onClick.AddListener(() => AddDeskYaw(15f));
        if (logButton != null) logButton.onClick.AddListener(LogValues);
        if (saveButton != null) saveButton.onClick.AddListener(SaveSitData);
        if (resetButton != null) resetButton.onClick.AddListener(ResetAll);

        HookSlider(charXSlider, OnCharacterOffsetChanged);
        HookSlider(charYSlider, OnCharacterOffsetChanged);
        HookSlider(charZSlider, OnCharacterOffsetChanged);
        HookSlider(charScaleSlider, OnCharacterOffsetChanged);
        HookSlider(charRotYSlider, OnCharacterOffsetChanged);
        HookSlider(chairXSlider, OnChairOffsetChanged);
        HookSlider(chairYSlider, OnChairOffsetChanged);
        HookSlider(chairZSlider, OnChairOffsetChanged);
        HookSlider(deskXSlider, OnDeskAnchorChanged);
        HookSlider(deskYSlider, OnDeskAnchorChanged);
        HookSlider(deskScaleSlider, OnDeskScaleChanged);

        TryCaptureBaseline();
        RefreshAllFromData();
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf)
        {
            // 패널이 닫혀 있어도 턴테이블 상태만은 정지시켜 둔다
            _turntable = false;
            return;
        }

        TryCaptureBaseline();

        // 캐릭터 교체 감지 → 슬라이더 자동 리로드 (+ 멈추기 상태 초기화: 옛 캐릭터는 파괴/교체됨)
        string charcode = CurrentCharcode();
        if (charcode != _lastCharcode)
        {
            _lastCharcode = charcode;
            LoadSlidersFromSitData(charcode);
            ResetPause(false);
        }

        // 비착석 중에는 ApplyDeskOffset이 무시되므로 회전하지 않는다
        if (_turntable && Manager != null && Manager.IsChillMode)
        {
            AddDeskYaw(TurntableSpeedDegPerSec * Time.deltaTime);
        }

        UpdateEnterLabel(); // menutrigger/7키 토글과도 동기
    }

    // ---------------------------------------------------------------- 패널 표시

    public void TogglePanel()
    {
        if (panel == null) return;
        if (panel.activeSelf) HidePanel();
        else ShowPanel();
    }

    public void ShowPanel()
    {
        if (panel == null) return;
        panel.SetActive(true);
        TryCaptureBaseline();
        RefreshAllFromData();
    }

    public void HidePanel()
    {
        if (panel == null) return;
        _turntable = false;
        if (turntableButtonLabel != null) turntableButtonLabel.text = "턴테이블";
        ResetPause(true); // 패널을 닫으며 캐릭터를 얼려두지 않는다
        panel.SetActive(false);
    }

    // ---------------------------------------------------------------- 참조 해석

    private ChillModeManager Manager
    {
        get
        {
            if (_chillManager == null) _chillManager = ChillModeManager.Instance;
            return _chillManager;
        }
    }

    private ChillSitData SitData
    {
        get
        {
            if (_sitData == null && Manager != null) _sitData = Manager.chillSitData;
            return _sitData;
        }
    }

    /// <summary>튜닝 대상 캐릭터: 착석 중이면 그 캐릭터, 아니면 지정 대상(데모) 또는 현재 캐릭터(본편).</summary>
    private GameObject CurrentTargetCharacter()
    {
        if (Manager == null) return null;
        if (Manager.CurrentChillCharacter != null) return Manager.CurrentChillCharacter;
        if (Manager.overrideCharacter != null) return Manager.overrideCharacter;
        if (CharManager.Instance != null) return CharManager.Instance.GetCurrentCharacter();
        return null;
    }

    private string CurrentCharcode()
    {
        GameObject target = CurrentTargetCharacter();
        if (target == null) return "";
        CharAttributes attrs = target.GetComponent<CharAttributes>();
        if (attrs != null && !string.IsNullOrEmpty(attrs.charcode)) return attrs.charcode;
        return target.name;
    }

    /// <summary>책상 기준값(리셋/정면용)을 SO 기준으로 1회 캡처. 매니저가 늦게 뜨는 씬 대비 지연 캡처.</summary>
    private void TryCaptureBaseline()
    {
        if (_baselineCaptured || Manager == null || SitData == null) return;
        // 책상 배치의 단일 출처는 ChillSitData — 매니저 필드를 SO 값으로 정렬 후 앵커 계산
        Manager.deskPositionOffset = SitData.deskPositionOffset;
        Manager.deskRotationOffset = SitData.deskRotationOffset;
        Manager.deskScaleMultiplier = SitData.deskScaleMultiplier;
        _seatAnchor = ComputeSeatAnchor();
        _baseSeatAnchor = _seatAnchor;
        _baseDeskRotationOffset = Manager.deskRotationOffset;
        _baseDeskScaleMultiplier = Manager.deskScaleMultiplier;
        _baselineCaptured = true;
    }

    private void RefreshAllFromData()
    {
        _lastCharcode = CurrentCharcode();
        LoadSlidersFromSitData(_lastCharcode);
        if (_baselineCaptured)
        {
            _seatAnchor = ComputeSeatAnchor();
            SyncDeskSliders();
            UpdateAngleLabel();
        }
        UpdateEnterLabel();
    }

    private void HookSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider != null) slider.onValueChanged.AddListener(action);
    }

    // ---------------------------------------------------------------- 모드/재생

    /// <summary>착석 토글 — 본편은 Pomodoro 모드(타이머 UI/채팅 차단 포함), 데모는 착석만.</summary>
    private void ToggleChill()
    {
        if (ChatModeManager.Instance != null)
        {
            ChatModeManager.Instance.ToggleMode(ChatMode.Pomodoro);
        }
        else if (Manager != null)
        {
            Manager.ToggleChillMode();
        }

        if (Manager != null && Manager.IsChillMode)
        {
            RefreshAllFromData(); // 착석 직후 SO 실제값과 재동기화 (Enter가 SO에서 책상 값을 로드함)
        }
        UpdateEnterLabel();
    }

    private void UpdateEnterLabel()
    {
        if (Manager == null) return;
        bool seated = Manager.IsChillMode;
        if (enterButtonLabel != null)
        {
            string want = seated ? "일어나기" : "착석";
            if (enterButtonLabel.text != want) enterButtonLabel.text = want;
        }
        // 비착석 중 슬라이더 조작은 매니저가 무시(무음 폐기)하므로 입력 자체를 잠근다
        if (_slidersInteractable != seated)
        {
            _slidersInteractable = seated;
            SetSlidersInteractable(seated);
            if (!seated)
            {
                ResetPause(true); // 기립 시 멈추기 해제 (menutrigger/7키 등 외부 경로 포함)
            }
        }
    }

    /// <summary>멈추기 상태 해제. restoreSpeed=true면 현재 대상 캐릭터의 Animator.speed도 복원.</summary>
    private void ResetPause(bool restoreSpeed)
    {
        if (!_paused) return;
        _paused = false;
        if (pauseButtonLabel != null) pauseButtonLabel.text = "멈추기";
        if (restoreSpeed)
        {
            GameObject target = CurrentTargetCharacter();
            Animator animator = target != null ? target.GetComponentInChildren<Animator>(true) : null;
            if (animator != null) animator.speed = 1f;
        }
    }

    private void SetSlidersInteractable(bool on)
    {
        Slider[] sliders =
        {
            charXSlider, charYSlider, charZSlider, charScaleSlider, charRotYSlider,
            chairXSlider, chairYSlider, chairZSlider,
            deskXSlider, deskYSlider, deskScaleSlider,
        };
        foreach (Slider slider in sliders)
        {
            if (slider != null) slider.interactable = on;
        }
    }

    private void TogglePause()
    {
        _paused = !_paused;
        if (pauseButtonLabel != null) pauseButtonLabel.text = _paused ? "재생" : "멈추기";
        GameObject target = CurrentTargetCharacter();
        if (target == null) return;
        Animator animator = target.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.speed = _paused ? 0f : 1f;
    }

    // ---------------------------------------------------------------- 오프셋 → ChillSitData

    private void OnCharacterOffsetChanged(float _)
    {
        RefreshCharacterLabels();
        if (_syncingUI || Manager == null) return;
        // 슬라이더에 없는 회전 X/Z는 기존 데이터 보존
        ChillSitData.CharacterSitOffset cur = SitData != null ? SitData.GetOffset(CurrentCharcode()) : null;
        Vector3 rot = new Vector3(
            cur != null ? cur.rotationOffset.x : 0f,
            charRotYSlider != null ? charRotYSlider.value : 180f,
            cur != null ? cur.rotationOffset.z : 0f);
        Manager.SetCharacterOffset(CurrentCharPosition(), rot,
            charScaleSlider != null ? charScaleSlider.value : 1f);
    }

    private void OnChairOffsetChanged(float _)
    {
        RefreshChairLabels();
        if (_syncingUI || Manager == null) return;
        ChillSitData.CharacterSitOffset cur = SitData != null ? SitData.GetOffset(CurrentCharcode()) : null;
        Vector3 rot = cur != null ? cur.chairLocalRotation : Vector3.zero; // 회전은 데이터 보존
        Manager.SetChairOffset(CurrentChairPosition(), rot);
        // 의자가 움직이면 시트의 데스크 로컬 좌표가 달라지므로, 캐릭터를 앵커에 다시 고정(책상이 미끄러진다)
        ApplyDeskPose();
    }

    private Vector3 CurrentCharPosition()
    {
        return new Vector3(
            charXSlider != null ? charXSlider.value : 0f,
            charYSlider != null ? charYSlider.value : 0f,
            charZSlider != null ? charZSlider.value : 0f);
    }

    private Vector3 CurrentChairPosition()
    {
        return new Vector3(
            chairXSlider != null ? chairXSlider.value : 0f,
            chairYSlider != null ? chairYSlider.value : 0f,
            chairZSlider != null ? chairZSlider.value : 0f);
    }

    private void RefreshCharacterLabels()
    {
        if (charXValueLabel != null && charXSlider != null) charXValueLabel.text = charXSlider.value.ToString("0.00");
        if (charYValueLabel != null && charYSlider != null) charYValueLabel.text = charYSlider.value.ToString("0.00");
        if (charZValueLabel != null && charZSlider != null) charZValueLabel.text = charZSlider.value.ToString("0.00");
        if (charScaleValueLabel != null && charScaleSlider != null) charScaleValueLabel.text = charScaleSlider.value.ToString("0.00");
        if (charRotYValueLabel != null && charRotYSlider != null) charRotYValueLabel.text = charRotYSlider.value.ToString("0") + "°";
    }

    private void RefreshChairLabels()
    {
        if (chairXValueLabel != null && chairXSlider != null) chairXValueLabel.text = chairXSlider.value.ToString("0.00");
        if (chairYValueLabel != null && chairYSlider != null) chairYValueLabel.text = chairYSlider.value.ToString("0.00");
        if (chairZValueLabel != null && chairZSlider != null) chairZValueLabel.text = chairZSlider.value.ToString("0.00");
    }

    private void LoadSlidersFromSitData(string charcode)
    {
        if (SitData == null) return;
        ChillSitData.CharacterSitOffset offset = SitData.GetOffset(charcode);
        if (offset == null) return;

        EnsureSnapshot(charcode, offset);

        _syncingUI = true;
        SetSliderValueSilently(charXSlider, offset.positionOffset.x);
        SetSliderValueSilently(charYSlider, offset.positionOffset.y);
        SetSliderValueSilently(charZSlider, offset.positionOffset.z);
        SetSliderValueSilently(charScaleSlider, offset.scaleMultiplier);
        SetSliderValueSilently(charRotYSlider, NormalizeAngle(offset.rotationOffset.y));
        SetSliderValueSilently(chairXSlider, offset.chairLocalPosition.x);
        SetSliderValueSilently(chairYSlider, offset.chairLocalPosition.y);
        SetSliderValueSilently(chairZSlider, offset.chairLocalPosition.z);
        _syncingUI = false;

        RefreshCharacterLabels();
        RefreshChairLabels();
    }

    /// <summary>시작 시점(리셋 기준) 착석값을 charcode별로 1회 보존.</summary>
    private void EnsureSnapshot(string charcode, ChillSitData.CharacterSitOffset offset)
    {
        if (string.IsNullOrEmpty(charcode) || _snapshots.ContainsKey(charcode)) return;
        _snapshots[charcode] = new ChillSitData.CharacterSitOffset
        {
            charcode = charcode,
            positionOffset = offset.positionOffset,
            rotationOffset = offset.rotationOffset,
            scaleMultiplier = offset.scaleMultiplier,
            chairLocalPosition = offset.chairLocalPosition,
            chairLocalRotation = offset.chairLocalRotation,
        };
    }

    /// <summary>범위 밖 값(수동 편집된 데이터)도 클램프 없이 반영되도록 필요 시 범위를 넓혀 설정.</summary>
    private static void SetSliderValueSilently(Slider slider, float value)
    {
        if (slider == null) return;
        if (value < slider.minValue) slider.minValue = value;
        if (value > slider.maxValue) slider.maxValue = value;
        slider.SetValueWithoutNotify(value);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    // ---------------------------------------------------------------- 책상 (시트 앵커 모델)

    /// <summary>시트의 데스크 루트 로컬 좌표 (루트 스케일 제외 값 — InverseTransformPoint가 스케일을 나눠준다).</summary>
    private bool TryGetSeatLocal(out Vector3 seatLocal)
    {
        seatLocal = Vector3.zero;
        if (Manager == null || Manager.chairSeatPoint == null || Manager.deskSetRoot == null) return false;
        seatLocal = Manager.deskSetRoot.InverseTransformPoint(Manager.chairSeatPoint.position);
        return true;
    }

    /// <summary>현재 매니저 값이 만들어내는 시트 위치(책상 부모 로컬) = 앵커 역산.</summary>
    private Vector3 ComputeSeatAnchor()
    {
        if (!TryGetSeatLocal(out Vector3 seatLocal)) return Vector3.zero;
        return Manager.deskPositionOffset
            + Quaternion.Euler(Manager.deskRotationOffset) * (seatLocal * Manager.deskScaleMultiplier);
    }

    /// <summary>앵커/각도/배율로부터 deskPositionOffset을 해석적으로 계산해 적용하고 SO에 기록.
    /// 회전·배율이 바뀌어도 시트(캐릭터)는 앵커 좌표에 고정된다.</summary>
    private void ApplyDeskPose()
    {
        if (Manager == null || !TryGetSeatLocal(out Vector3 seatLocal)) return;
        Manager.deskPositionOffset = _seatAnchor
            - Quaternion.Euler(Manager.deskRotationOffset) * (seatLocal * Manager.deskScaleMultiplier);
        Manager.ApplyDeskOffset();

        // 책상 배치는 SO가 단일 출처 — 즉시 SO에 반영(디스크 저장은 [데이터 저장])
        if (SitData != null)
        {
            SitData.deskPositionOffset = Manager.deskPositionOffset;
            SitData.deskRotationOffset = Manager.deskRotationOffset;
            SitData.deskScaleMultiplier = Manager.deskScaleMultiplier;
        }
    }

    private void OnDeskAnchorChanged(float _)
    {
        RefreshDeskLabels();
        if (_syncingUI || Manager == null) return;
        if (deskXSlider != null) _seatAnchor.x = deskXSlider.value;
        if (deskYSlider != null) _seatAnchor.y = deskYSlider.value;
        ApplyDeskPose();
    }

    private void OnDeskScaleChanged(float _)
    {
        RefreshDeskLabels();
        if (_syncingUI || Manager == null) return;
        if (deskScaleSlider != null) Manager.deskScaleMultiplier = deskScaleSlider.value;
        ApplyDeskPose(); // 앵커 고정 확대/축소 (책상+캐릭터 함께)
    }

    private void AddDeskYaw(float delta)
    {
        if (Manager == null || !Manager.IsChillMode) return;
        Vector3 rot = Manager.deskRotationOffset;
        rot.y += delta;
        Manager.deskRotationOffset = rot;
        ApplyDeskPose(); // 앵커 고정 제자리 회전 — X/Y 값 불변
        UpdateAngleLabel();
    }

    private void ToggleTurntable()
    {
        _turntable = !_turntable;
        if (turntableButtonLabel != null) turntableButtonLabel.text = _turntable ? "회전 멈춤" : "턴테이블";
    }

    /// <summary>정면: 책상 포즈(앵커/회전/배율)를 시작 시점 튜닝값으로 복귀.</summary>
    private void ResetDeskPose()
    {
        _turntable = false;
        if (turntableButtonLabel != null) turntableButtonLabel.text = "턴테이블";
        if (Manager == null || !_baselineCaptured) return;
        _seatAnchor = _baseSeatAnchor;
        Manager.deskRotationOffset = _baseDeskRotationOffset;
        Manager.deskScaleMultiplier = _baseDeskScaleMultiplier;
        ApplyDeskPose();
        SyncDeskSliders();
        UpdateAngleLabel();
    }

    private void SyncDeskSliders()
    {
        if (Manager == null) return;
        _syncingUI = true;
        SetSliderValueSilently(deskXSlider, _seatAnchor.x);
        SetSliderValueSilently(deskYSlider, _seatAnchor.y);
        SetSliderValueSilently(deskScaleSlider, Manager.deskScaleMultiplier);
        _syncingUI = false;
        RefreshDeskLabels();
    }

    private void RefreshDeskLabels()
    {
        if (deskXValueLabel != null && deskXSlider != null) deskXValueLabel.text = deskXSlider.value.ToString("0");
        if (deskYValueLabel != null && deskYSlider != null) deskYValueLabel.text = deskYSlider.value.ToString("0");
        if (deskScaleValueLabel != null && deskScaleSlider != null) deskScaleValueLabel.text = deskScaleSlider.value.ToString("0");
    }

    private void UpdateAngleLabel()
    {
        if (angleValueLabel == null || Manager == null) return;
        angleValueLabel.text = Mathf.RoundToInt(NormalizeAngle(Manager.deskRotationOffset.y)) + "°";
    }

    // ---------------------------------------------------------------- 리셋

    /// <summary>모든 값을 시작 시점으로 복원: 현재 캐릭터의 착석/의자 오프셋 + 책상 포즈 + 턴테이블 해제.</summary>
    private void ResetAll()
    {
        string charcode = CurrentCharcode();
        if (Manager != null && SitData != null && _snapshots.TryGetValue(charcode, out ChillSitData.CharacterSitOffset snap))
        {
            if (Manager.IsChillMode)
            {
                Manager.SetCharacterOffset(snap.positionOffset, snap.rotationOffset, snap.scaleMultiplier);
                Manager.SetChairOffset(snap.chairLocalPosition, snap.chairLocalRotation);
            }
            else
            {
                // 비착석 중에는 Set API가 무시되므로 SO에 직접 복원 (다음 착석 때 적용)
                ChillSitData.CharacterSitOffset entry = SitData.GetOrCreateOffset(charcode);
                entry.positionOffset = snap.positionOffset;
                entry.rotationOffset = snap.rotationOffset;
                entry.scaleMultiplier = snap.scaleMultiplier;
                entry.chairLocalPosition = snap.chairLocalPosition;
                entry.chairLocalRotation = snap.chairLocalRotation;
            }
        }

        ResetDeskPose(); // 책상 포즈 + 턴테이블 해제 포함
        LoadSlidersFromSitData(charcode);
        Debug.Log("[SitSupport] 리셋 완료 (" + charcode + " 착석값 + 책상 포즈)");
    }

    // ---------------------------------------------------------------- 저장/로그

    private void SaveSitData()
    {
        if (SitData == null)
        {
            Debug.LogWarning("[SitSupport] ChillSitData가 연결되지 않았습니다.");
            return;
        }
        // 책상 배치 최신값 동기화 후 저장 (착석/의자 값은 Set API가 이미 SO에 기록)
        if (Manager != null)
        {
            SitData.deskPositionOffset = Manager.deskPositionOffset;
            SitData.deskRotationOffset = Manager.deskRotationOffset;
            SitData.deskScaleMultiplier = Manager.deskScaleMultiplier;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(SitData);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("[SitSupport] ChillSitData 저장 완료 (" + CurrentCharcode() + " 착석값 + 책상 배치)");
#else
        Debug.LogWarning("[SitSupport] 빌드에서는 ChillSitData가 디스크에 저장되지 않습니다 (세션 한정 반영).");
#endif
    }

    private void LogValues()
    {
        string desk = Manager != null
            ? string.Format("seatAnchor={0} deskPos={1} deskRot={2} deskScale={3}",
                _seatAnchor.ToString("F1"),
                Manager.deskPositionOffset.ToString("F1"),
                Manager.deskRotationOffset.ToString("F2"),
                Manager.deskScaleMultiplier.ToString("0.##"))
            : "(매니저 없음)";
        Debug.Log(string.Format(
            "[SitSupport] charcode={0} charPos={1} scale={2} rotY={3} chairPos={4} / {5}",
            CurrentCharcode(),
            CurrentCharPosition().ToString("F3"),
            charScaleSlider != null ? charScaleSlider.value.ToString("0.00") : "?",
            charRotYSlider != null ? charRotYSlider.value.ToString("0") : "?",
            CurrentChairPosition().ToString("F3"),
            desk));
    }
}
