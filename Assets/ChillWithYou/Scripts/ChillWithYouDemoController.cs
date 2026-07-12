using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ChillWithYouSample 데모 메뉴(좌측 상단) — 본편 ChillModeManager/ChillSitData의 튜닝 UI.
/// 데모는 자체 착석 로직을 갖지 않는다: 착석/복귀/오프셋 적용은 전부 ChillModeManager가 수행하고,
/// 이 컨트롤러는 슬라이더 값을 SetCharacterOffset/SetChairOffset과 ChillSitData 책상 필드로 기록한다.
/// 캐릭터 charcode는 본편과 동일(diana / arona_tripo / arona)하므로 여기서 저장한 값이 그대로 본편 데이터가 된다.
///
/// 책상 좌표 모델(시트 앵커): 데모의 "위치 X/Y"는 책상 루트가 아니라 **착석 지점(chairSeatPoint)의 목표
/// 좌표**다. 매 적용 시 deskPositionOffset = 앵커 − R(각도)×(시트 데스크로컬 × 배율)을 해석적으로 계산해,
/// 회전(턴테이블/±15°)·전체 크기가 바뀌어도 X/Y 값은 불변이고 시트(캐릭터)가 그 좌표에 고정된 채
/// 제자리 회전/확대된다. (Desk_Set 루트 피벗이 소품에서 멀어 직접 회전하면 공전하는 문제의 해법)
/// ※ 데모 씬은 책상 원본 transform이 항등(0/identity/1)이라는 전제를 사용한다.
/// </summary>
public class ChillWithYouDemoController : MonoBehaviour
{
    [Header("씬 참조")]
    public ChillModeManager chillManager;
    public ChillSitData sitData;
    public RectTransform charParent;     // 캐릭터 스폰 부모 (Canvas_Char)
    public GameObject currentCharacter;

    [Header("캐릭터 프리팹 (버튼 순서와 동일)")]
    public GameObject[] characterPrefabs;

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
    public Button[] characterButtons;

    private const int CharLayer = 3;
    private const float TurntableSpeedDegPerSec = 30f;
    private const float EnterDelaySeconds = 0.5f; // MagicaCloth 등 초기화 후 착석

    private bool _turntable;
    private bool _paused;
    private bool _syncingUI; // 슬라이더 값 코드 세팅 중 리스너 무시용
    private bool _slidersInteractable = true; // 비착석 중 슬라이더 잠금 상태 캐시

    // 시트 앵커: 착석 지점의 목표 좌표(책상 부모 로컬). "위치 X/Y" 슬라이더가 이 값을 편집한다.
    private Vector3 _seatAnchor;

    // 리셋/정면 복귀용 시작 시점 기준값
    private Vector3 _baseSeatAnchor;
    private Vector3 _baseDeskRotationOffset;
    private float _baseDeskScaleMultiplier = 1f;
    private readonly Dictionary<string, ChillSitData.CharacterSitOffset> _snapshots =
        new Dictionary<string, ChillSitData.CharacterSitOffset>(); // charcode별 시작 시점 착석값

    private void Start()
    {
        if (chillManager != null)
        {
            // 책상 배치의 단일 출처는 ChillSitData — 시작 시 매니저 필드를 SO 값으로 정렬
            if (sitData != null)
            {
                chillManager.deskPositionOffset = sitData.deskPositionOffset;
                chillManager.deskRotationOffset = sitData.deskRotationOffset;
                chillManager.deskScaleMultiplier = sitData.deskScaleMultiplier;
            }
            _seatAnchor = ComputeSeatAnchor();
            _baseSeatAnchor = _seatAnchor;
            _baseDeskRotationOffset = chillManager.deskRotationOffset;
            _baseDeskScaleMultiplier = chillManager.deskScaleMultiplier;
            if (currentCharacter != null) chillManager.overrideCharacter = currentCharacter;
        }

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

        if (characterButtons != null)
        {
            for (int i = 0; i < characterButtons.Length; i++)
            {
                int index = i; // 클로저 캡처용 복사
                if (characterButtons[i] != null)
                {
                    characterButtons[i].onClick.AddListener(() => SwapCharacter(index));
                }
            }
        }

        LoadSlidersFromSitData(CurrentCharcode());
        SyncDeskSliders();
        UpdateAngleLabel();

        StartCoroutine(AutoEnter());
    }

    private IEnumerator AutoEnter()
    {
        yield return new WaitForSeconds(EnterDelaySeconds);
        if (chillManager != null && !chillManager.IsChillMode)
        {
            chillManager.EnterChillMode();
        }
        // 착석 성공 시 슬라이더를 실제 적용된 SO 값과 재동기화 (비착석 중 조작분 폐기 반영)
        if (chillManager != null && chillManager.IsChillMode)
        {
            LoadSlidersFromSitData(CurrentCharcode());
            _seatAnchor = ComputeSeatAnchor(); // Enter가 SO에서 책상 값을 로드했으므로 앵커 재계산
            SyncDeskSliders();
            UpdateAngleLabel();
        }
        UpdateEnterLabel();
    }

    private void Update()
    {
        // 비착석 중에는 ApplyDeskOffset이 무시되므로 회전하지 않는다
        if (_turntable && chillManager != null && chillManager.IsChillMode)
        {
            AddDeskYaw(TurntableSpeedDegPerSec * Time.deltaTime);
        }
        UpdateEnterLabel(); // 7키(ChillModeTestManager) 토글과도 동기
    }

    private void HookSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider != null) slider.onValueChanged.AddListener(action);
    }

    // ---------------------------------------------------------------- 모드/재생

    private void ToggleChill()
    {
        if (chillManager == null) return;
        chillManager.ToggleChillMode();
        if (chillManager.IsChillMode)
        {
            LoadSlidersFromSitData(CurrentCharcode()); // 재착석 시 SO 실제값과 재동기화
            _seatAnchor = ComputeSeatAnchor();
            SyncDeskSliders();
            UpdateAngleLabel();
        }
        UpdateEnterLabel();
    }

    private void UpdateEnterLabel()
    {
        if (chillManager == null) return;
        bool seated = chillManager.IsChillMode;
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
        if (currentCharacter == null) return;
        Animator animator = currentCharacter.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.speed = _paused ? 0f : 1f;
    }

    // ---------------------------------------------------------------- 오프셋 → ChillSitData

    private void OnCharacterOffsetChanged(float _)
    {
        RefreshCharacterLabels();
        if (_syncingUI || chillManager == null) return;
        // 슬라이더에 없는 회전 X/Z는 기존 데이터 보존
        ChillSitData.CharacterSitOffset cur = sitData != null ? sitData.GetOffset(CurrentCharcode()) : null;
        Vector3 rot = new Vector3(
            cur != null ? cur.rotationOffset.x : 0f,
            charRotYSlider != null ? charRotYSlider.value : 180f,
            cur != null ? cur.rotationOffset.z : 0f);
        chillManager.SetCharacterOffset(CurrentCharPosition(), rot,
            charScaleSlider != null ? charScaleSlider.value : 1f);
    }

    private void OnChairOffsetChanged(float _)
    {
        RefreshChairLabels();
        if (_syncingUI || chillManager == null) return;
        ChillSitData.CharacterSitOffset cur = sitData != null ? sitData.GetOffset(CurrentCharcode()) : null;
        Vector3 rot = cur != null ? cur.chairLocalRotation : Vector3.zero; // 회전은 데이터 보존
        chillManager.SetChairOffset(CurrentChairPosition(), rot);
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
        if (sitData == null) return;
        ChillSitData.CharacterSitOffset offset = sitData.GetOffset(charcode);
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
        if (chillManager == null || chillManager.chairSeatPoint == null || chillManager.deskSetRoot == null) return false;
        seatLocal = chillManager.deskSetRoot.InverseTransformPoint(chillManager.chairSeatPoint.position);
        return true;
    }

    /// <summary>현재 매니저 값이 만들어내는 시트 위치(책상 부모 로컬) = 앵커 역산.
    /// 데모 전제: 책상 원본 transform은 항등이므로 desk 로컬포즈 = 오프셋 그대로.</summary>
    private Vector3 ComputeSeatAnchor()
    {
        if (!TryGetSeatLocal(out Vector3 seatLocal)) return Vector3.zero;
        return chillManager.deskPositionOffset
            + Quaternion.Euler(chillManager.deskRotationOffset) * (seatLocal * chillManager.deskScaleMultiplier);
    }

    /// <summary>앵커/각도/배율로부터 deskPositionOffset을 해석적으로 계산해 적용하고 SO에 기록.
    /// 회전·배율이 바뀌어도 시트(캐릭터)는 앵커 좌표에 고정된다.</summary>
    private void ApplyDeskPose()
    {
        if (chillManager == null || !TryGetSeatLocal(out Vector3 seatLocal)) return;
        chillManager.deskPositionOffset = _seatAnchor
            - Quaternion.Euler(chillManager.deskRotationOffset) * (seatLocal * chillManager.deskScaleMultiplier);
        chillManager.ApplyDeskOffset();

        // 책상 배치는 SO가 단일 출처 — 즉시 SO에 반영(디스크 저장은 [데이터 저장])
        if (sitData != null)
        {
            sitData.deskPositionOffset = chillManager.deskPositionOffset;
            sitData.deskRotationOffset = chillManager.deskRotationOffset;
            sitData.deskScaleMultiplier = chillManager.deskScaleMultiplier;
        }
    }

    private void OnDeskAnchorChanged(float _)
    {
        RefreshDeskLabels();
        if (_syncingUI || chillManager == null) return;
        if (deskXSlider != null) _seatAnchor.x = deskXSlider.value;
        if (deskYSlider != null) _seatAnchor.y = deskYSlider.value;
        ApplyDeskPose();
    }

    private void OnDeskScaleChanged(float _)
    {
        RefreshDeskLabels();
        if (_syncingUI || chillManager == null) return;
        if (deskScaleSlider != null) chillManager.deskScaleMultiplier = deskScaleSlider.value;
        ApplyDeskPose(); // 앵커 고정 확대/축소 (책상+캐릭터 함께)
    }

    private void AddDeskYaw(float delta)
    {
        if (chillManager == null || !chillManager.IsChillMode) return;
        Vector3 rot = chillManager.deskRotationOffset;
        rot.y += delta;
        chillManager.deskRotationOffset = rot;
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
        if (chillManager == null) return;
        _seatAnchor = _baseSeatAnchor;
        chillManager.deskRotationOffset = _baseDeskRotationOffset;
        chillManager.deskScaleMultiplier = _baseDeskScaleMultiplier;
        ApplyDeskPose();
        SyncDeskSliders();
        UpdateAngleLabel();
    }

    private void SyncDeskSliders()
    {
        if (chillManager == null) return;
        _syncingUI = true;
        SetSliderValueSilently(deskXSlider, _seatAnchor.x);
        SetSliderValueSilently(deskYSlider, _seatAnchor.y);
        SetSliderValueSilently(deskScaleSlider, chillManager.deskScaleMultiplier);
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
        if (angleValueLabel == null || chillManager == null) return;
        angleValueLabel.text = Mathf.RoundToInt(NormalizeAngle(chillManager.deskRotationOffset.y)) + "°";
    }

    // ---------------------------------------------------------------- 리셋

    /// <summary>모든 값을 시작 시점으로 복원: 현재 캐릭터의 착석/의자 오프셋 + 책상 포즈 + 턴테이블 해제.</summary>
    private void ResetAll()
    {
        string charcode = CurrentCharcode();
        if (chillManager != null && sitData != null && _snapshots.TryGetValue(charcode, out ChillSitData.CharacterSitOffset snap))
        {
            if (chillManager.IsChillMode)
            {
                chillManager.SetCharacterOffset(snap.positionOffset, snap.rotationOffset, snap.scaleMultiplier);
                chillManager.SetChairOffset(snap.chairLocalPosition, snap.chairLocalRotation);
            }
            else
            {
                // 비착석 중에는 Set API가 무시되므로 SO에 직접 복원 (다음 착석 때 적용)
                ChillSitData.CharacterSitOffset entry = sitData.GetOrCreateOffset(charcode);
                entry.positionOffset = snap.positionOffset;
                entry.rotationOffset = snap.rotationOffset;
                entry.scaleMultiplier = snap.scaleMultiplier;
                entry.chairLocalPosition = snap.chairLocalPosition;
                entry.chairLocalRotation = snap.chairLocalRotation;
            }
        }

        ResetDeskPose(); // 책상 포즈 + 턴테이블 해제 포함
        LoadSlidersFromSitData(charcode);
        Debug.Log("[ChillWithYouDemo] 리셋 완료 (" + charcode + " 착석값 + 책상 포즈)");
    }

    // ---------------------------------------------------------------- 캐릭터 교체

    /// <summary>일어나기 → 캐릭터 교체 → 재착석. 책상/의자는 ChillModeManager가 관리하므로 그대로.</summary>
    public void SwapCharacter(int index)
    {
        if (characterPrefabs == null || index < 0 || index >= characterPrefabs.Length) return;
        GameObject prefab = characterPrefabs[index];
        if (prefab == null || chillManager == null) return;

        if (chillManager.IsChillMode)
        {
            chillManager.ExitChillMode(); // 원상 복구 후 교체 (착석 중 파괴 방지)
        }

        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
        }

        GameObject next = Instantiate(prefab, charParent);
        next.name = prefab.name;
        RectTransform rt = next.transform as RectTransform;
        if (rt != null) rt.anchoredPosition3D = new Vector3(0f, -450f, 0f);
        SetLayerRecursive(next, CharLayer); // Main Camera 컬링(3|6) 대응

        currentCharacter = next;
        chillManager.overrideCharacter = next;
        _paused = false;
        if (pauseButtonLabel != null) pauseButtonLabel.text = "멈추기";

        LoadSlidersFromSitData(CurrentCharcode());
        StartCoroutine(AutoEnter());
    }

    private string CurrentCharcode()
    {
        if (currentCharacter == null) return "";
        CharAttributes attrs = currentCharacter.GetComponent<CharAttributes>();
        if (attrs != null && !string.IsNullOrEmpty(attrs.charcode)) return attrs.charcode;
        return currentCharacter.name;
    }

    // ---------------------------------------------------------------- 저장/로그

    private void SaveSitData()
    {
        if (sitData == null)
        {
            Debug.LogWarning("[ChillWithYouDemo] ChillSitData가 연결되지 않았습니다.");
            return;
        }
        // 책상 배치 최신값 동기화 후 저장 (착석/의자 값은 Set API가 이미 SO에 기록)
        if (chillManager != null)
        {
            sitData.deskPositionOffset = chillManager.deskPositionOffset;
            sitData.deskRotationOffset = chillManager.deskRotationOffset;
            sitData.deskScaleMultiplier = chillManager.deskScaleMultiplier;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(sitData);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
        Debug.Log("[ChillWithYouDemo] ChillSitData 저장 완료 (" + CurrentCharcode() + " 착석값 + 책상 배치)");
    }

    private void LogValues()
    {
        string desk = chillManager != null
            ? string.Format("seatAnchor={0} deskPos={1} deskRot={2} deskScale={3}",
                _seatAnchor.ToString("F1"),
                chillManager.deskPositionOffset.ToString("F1"),
                chillManager.deskRotationOffset.ToString("F2"),
                chillManager.deskScaleMultiplier.ToString("0.##"))
            : "(매니저 없음)";
        Debug.Log(string.Format(
            "[ChillWithYouDemo] charcode={0} charPos={1} scale={2} rotY={3} chairPos={4} / {5}",
            CurrentCharcode(),
            CurrentCharPosition().ToString("F3"),
            charScaleSlider != null ? charScaleSlider.value.ToString("0.00") : "?",
            charRotYSlider != null ? charRotYSlider.value.ToString("0") : "?",
            CurrentChairPosition().ToString("F3"),
            desk));
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}
