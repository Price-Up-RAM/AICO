// AICO 시스템 메뉴 — 빈 공간을 보고 palm-up 했을 때 뜨는 메뉴 (SampleSceneKAI_MR_Port_Plan.md §7).
//
// 캐릭터와 무관한 전역 제어만 담는다: 주크박스 볼륨, 방 재스캔/이펙트 메시 재생성/앵커 편집
// (MRSpatialAnchorEditor), 캐릭터 크기·Idle 확률 슬라이더, Exit.
// Settings/Function/Control(음성 패널 등)은 MRCharacterContextMenu가 아니라 기존 UIManager
// 플로팅 패널 경로(Tools → MR → 6으로 변환한 Tab Window_Settings 등)로 연결한다 — 여기서 중복하지 않는다.
//
// MRCharacterMenu(MRSampleScene 참조 구현)가 검증한 슬라이더 배선 패턴을 그대로 따른다:
// SetValueWithoutNotify로 초기화하고, PlayerPrefs에 저장하며, 값 옆에 텍스트로 표시한다.
//
// 트리거(빈 공간 + palm-up 판정)는 Phase 4의 MRGazeProvider/MRIntentRouter가 담당한다.
// 이 컴포넌트는 MRFloatingPanel과 같은 오브젝트에 붙이고, 외부에서 GetComponent<MRFloatingPanel>().Open()을
// 호출하면 뜨는 구조를 전제로 한다 — 이 스크립트 자신은 열고 닫는 로직을 갖지 않는다.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(MRFloatingPanel))]
public class MRSystemMenuController : MonoBehaviour
{
    [Header("주크박스")]
    [SerializeField] private Slider volumeSlider;

    [Header("공간 앵커 (MRSpatialAnchorEditor)")]
    [SerializeField] private MRSpatialAnchorEditor spatialAnchorEditor;
    [SerializeField] private Button rescanButton;             // LaunchSceneCapture — 방 재스캔
    [SerializeField] private Button rebuildEffectMeshButton;  // RebuildEffectMesh
    [SerializeField] private Button toggleEditModeButton;     // ToggleEditMode — 앵커 편집 모드
    [SerializeField] private Button resetAnchorsButton;       // ResetAllAnchors

    [Header("캐릭터 크기 (MRCharacterWorldRoot)")]
    [SerializeField] private MRCharacterWorldRoot characterWorldRoot;
    [SerializeField] private Slider sizeSlider;
    [SerializeField] private TMP_Text sizeValueText;

    [Header("Idle 확률/주기 (PlayerPrefs)")]
    [SerializeField] private Slider idleDelaySlider;
    [SerializeField] private TMP_Text idleDelayValueText;
    [SerializeField] private Slider idleChanceSlider;
    [SerializeField] private TMP_Text idleChanceValueText;
    [SerializeField] private Slider idleIntervalSlider;
    [SerializeField] private TMP_Text idleIntervalValueText;

    [Header("종료")]
    [SerializeField] private Button exitButton;

    private void Awake()
    {
        if (spatialAnchorEditor == null) spatialAnchorEditor = FindFirstObjectByType<MRSpatialAnchorEditor>();
        if (characterWorldRoot == null) characterWorldRoot = FindFirstObjectByType<MRCharacterWorldRoot>();

        WireButton(rescanButton, () => spatialAnchorEditor?.LaunchSceneCapture());
        WireButton(rebuildEffectMeshButton, () => spatialAnchorEditor?.RebuildEffectMesh());
        WireButton(toggleEditModeButton, () => spatialAnchorEditor?.ToggleEditMode());
        WireButton(resetAnchorsButton, () => spatialAnchorEditor?.ResetAllAnchors());
        WireButton(exitButton, RequestExit);

        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (sizeSlider != null) sizeSlider.onValueChanged.AddListener(OnSizeSliderChanged);
        if (idleDelaySlider != null) idleDelaySlider.onValueChanged.AddListener(v =>
            SaveFloatPref("IdleRerollDelayAfterVoice", v, idleDelayValueText, "0.0"));
        if (idleChanceSlider != null) idleChanceSlider.onValueChanged.AddListener(v =>
            SaveFloatPref("IdleRerollChance", v, idleChanceValueText, "0.00"));
        if (idleIntervalSlider != null) idleIntervalSlider.onValueChanged.AddListener(v =>
            SaveFloatPref("IdleRerollInterval", v, idleIntervalValueText, "0.0"));
    }

    private void OnEnable()
    {
        SyncWithCurrentState();
    }

    private void WireButton(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.AddListener(action);
    }

    private void OnVolumeChanged(float v)
    {
        if (MRJukebox.Instance != null) MRJukebox.Instance.Volume = v;
    }

    private void OnSizeSliderChanged(float v)
    {
        PlayerPrefs.SetFloat("CharacterSize", v);
        PlayerPrefs.Save();
        if (sizeValueText != null) sizeValueText.text = v.ToString("0.0");
        if (characterWorldRoot != null) characterWorldRoot.SetSizeMultiplier(v);
    }

    private void SaveFloatPref(string key, float value, TMP_Text text, string format)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        if (text != null) text.text = value.ToString(format);
    }

    /// <summary>패널이 열릴 때(MRFloatingPanel.onOpened)와 Inspector 확인용으로 호출한다.</summary>
    public void SyncWithCurrentState()
    {
        SetSliderSilently(sizeSlider,
            characterWorldRoot != null ? characterWorldRoot.SizeMultiplier : PlayerPrefs.GetFloat("CharacterSize", 1f),
            sizeValueText, "0.0");
        SetSliderSilently(idleDelaySlider, PlayerPrefs.GetFloat("IdleRerollDelayAfterVoice", 1f), idleDelayValueText, "0.0");
        SetSliderSilently(idleChanceSlider, PlayerPrefs.GetFloat("IdleRerollChance", 1f), idleChanceValueText, "0.00");
        SetSliderSilently(idleIntervalSlider, PlayerPrefs.GetFloat("IdleRerollInterval", 5f), idleIntervalValueText, "0.0");

        if (volumeSlider != null && MRJukebox.Instance != null)
            volumeSlider.SetValueWithoutNotify(MRJukebox.Instance.Volume);
    }

    private void SetSliderSilently(Slider slider, float value, TMP_Text text, string format)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
        if (text != null) text.text = value.ToString(format);
    }

    private void RequestExit()
    {
        Debug.Log("[MRSystemMenuController] Exit 요청됨.");
        Application.Quit();
    }
}
