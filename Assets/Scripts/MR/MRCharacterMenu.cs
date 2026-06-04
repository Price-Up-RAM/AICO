using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tickle 탭 시 표시되는 캐릭터 메뉴 UI.
/// 현재는 스킨 변경(드롭다운) 기능만 포함하며, 추후 다른 기능 추가 가능.
/// World Space Canvas로 사용하여 MR 환경에서 캐릭터 머리 위에 표시되도록 합니다.
/// </summary>
public class MRCharacterMenu : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("최상위 프리팹 오브젝트 (FlatUnityCanvas 전체를 움직이기 위함)")]
    [SerializeField] private Transform menuRoot;
    [SerializeField] private Canvas menuCanvas;
    [SerializeField] private TMP_Dropdown skinDropdown;
    [SerializeField] private Button closeButton;

    [Header("PlayerPrefs Sliders")]
    [SerializeField] private Slider sizeSlider;
    [SerializeField] private TMP_Text sizeValueText;
    
    [SerializeField] private Slider idleDelaySlider;
    [SerializeField] private TMP_Text idleDelayValueText;
    
    [SerializeField] private Slider idleChanceSlider;
    [SerializeField] private TMP_Text idleChanceValueText;
    
    [SerializeField] private Slider idleIntervalSlider;
    [SerializeField] private TMP_Text idleIntervalValueText;

    [Header("Menu Positioning")]
    [Tooltip("메뉴를 소환 지점(손)으로부터 얼마나 위에 표시할지 (미터)")]
    [SerializeField] private float menuOffsetY = 0.15f;

    public MRHandInteractionRouter handRouter;
    public MRHandInteractionRouter.HandState leftHandState;
    
    private MRSpineCharacterController _currentCharacter;
    private Camera _cam;
    private bool _isOpen;

    // --- 드래그 이동 관련 (복구됨!) ---
    private bool _isDragging = false;
    private OVRInput.Controller _dragController;
    private Vector3 _dragOffset;
    private float _dragDistance = 0.4f;

    private void Awake()
    {
        _cam = Camera.main;

        if (menuCanvas != null)
            menuCanvas.enabled = false;

        if (skinDropdown != null)
            skinDropdown.onValueChanged.AddListener(OnSkinSelected);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMenu);

        // Sliders Listeners
        if (sizeSlider != null) sizeSlider.onValueChanged.AddListener(v => OnSliderChanged("CharacterSize", v, sizeValueText, "0.0"));
        if (idleDelaySlider != null) idleDelaySlider.onValueChanged.AddListener(v => OnSliderChanged("IdleRerollDelayAfterVoice", v, idleDelayValueText, "0.0"));
        if (idleChanceSlider != null) idleChanceSlider.onValueChanged.AddListener(v => OnSliderChanged("IdleRerollChance", v, idleChanceValueText, "0.00"));
        if (idleIntervalSlider != null) idleIntervalSlider.onValueChanged.AddListener(v => OnSliderChanged("IdleRerollInterval", v, idleIntervalValueText, "0.0"));
    }

    void Start()
    {
        if (handRouter != null && handRouter.handStates != null && handRouter.handStates.Length > 0)
        {
            leftHandState = handRouter.handStates[0];
        }
    }

    private void Update()
    {
        // 1. 메뉴 켜기/끄기 토글 (왼손 Start 버튼 / 시스템 핀치)
        if (OVRInput.GetDown(OVRInput.RawButton.Start) || OVRInput.GetDown(OVRInput.Button.Start))
        {
            if (_isOpen)
            {
                CloseMenu();
            }
            else
            {
                // 소환 지점 결정: 왼손 검지 끝 기준
                Vector3 spawnPos = _cam.transform.position + _cam.transform.forward * 0.5f; // 기본값
                var handPos = MRHandInteractionRouter.GetHandIndexTip(true); // true = Left Hand
                
                if (handPos.HasValue)
                {
                    spawnPos = handPos.Value;
                }
                else
                {
                    // 손 트래킹이 안될 경우 컨트롤러 위치로 대체
                    Transform rigRoot = _cam.transform.parent ? _cam.transform.parent : _cam.transform;
                    spawnPos = rigRoot.TransformPoint(OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch));
                }

                MRSpineCharacterController targetChar = MRSpineCharacterController.Instances.Count > 0 ? MRSpineCharacterController.Instances[0] : null;
                OpenMenu(targetChar, spawnPos);
            }
        }

        if (!_isOpen || menuCanvas == null || _cam == null) return;

        // 2. 드래그 로직 (복구됨!)
        bool leftPinch = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > 0.5f;
        bool rightPinch = OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) > 0.5f;

        if (!_isDragging)
        {
            // 메뉴 근처(반경 30cm)에서 핀치를 잡으면 드래그 시작
            if (leftPinch || rightPinch)
            {
                OVRInput.Controller ctrl = leftPinch ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
                Vector3 handDragPos = OVRInput.GetLocalControllerPosition(ctrl);

                if (Vector3.Distance(handDragPos, menuRoot.position) < 0.3f)
                {
                    _isDragging = true;
                    _dragController = ctrl;
                    _dragDistance = Vector3.Distance(_cam.transform.position, menuRoot.position);
                    _dragOffset = menuRoot.position - handDragPos;
                }
            }
        }
        else
        {
            // 드래그 중
            bool stillPinching = _dragController == OVRInput.Controller.LTouch ? leftPinch : rightPinch;
            if (!stillPinching)
            {
                _isDragging = false;
            }
            else
            {
                Vector3 handDragPos = OVRInput.GetLocalControllerPosition(_dragController);
                Vector3 targetPos = handDragPos + _dragOffset;
                
                // 너무 가까워지거나 멀어지는 것 방지
                Vector3 dirFromCam = (targetPos - _cam.transform.position).normalized;
                menuRoot.position = _cam.transform.position + dirFromCam * _dragDistance;
                
                // 드래그 중에도 메뉴가 카메라를 바라보도록 유지
                Vector3 lookDir = menuRoot.position - _cam.transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    menuRoot.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }
        
        // (주의: 매 프레임 돌아가는 빌보드 로직은 여기에 넣지 않습니다. 그래야 허공에 고정됩니다.)
    }

    public void OpenMenu(MRSpineCharacterController character, Vector3? spawnPos = null)
    {
        _currentCharacter = character;
        _isOpen = true;

        if (menuCanvas != null)
        {
            menuCanvas.enabled = true;
            
            if (spawnPos.HasValue && menuRoot != null)
            {
                // 손 위치보다 설정값만큼 위에 배치
                menuRoot.position = spawnPos.Value + Vector3.up * menuOffsetY;
                
                // 메뉴가 켜질 때 카메라(사용자)를 바라보도록 1회전 설정
                Vector3 dirToCam = menuRoot.position - _cam.transform.position;
                dirToCam.y = 0; // 상하로 눕지 않도록 수평 고정
                if (dirToCam.sqrMagnitude > 0.001f)
                {
                    menuRoot.rotation = Quaternion.LookRotation(dirToCam);
                }
            }
        }

        PopulateSkinDropdown();
        SyncSlidersWithPrefs();

        // 캔버스 크기가 엄청 큰 상태(기본값)라면 작게 축소
        if (menuCanvas.transform.localScale.x > 0.1f)
        {
            menuCanvas.transform.localScale = Vector3.one * 0.001f;
        }
    }

    public void CloseMenu()
    {
        _isOpen = false;
        _currentCharacter = null;

        if (menuCanvas != null)
            menuCanvas.enabled = false;
    }

    private void PopulateSkinDropdown()
    {
        if (skinDropdown == null || _currentCharacter == null) return;

        var skins = _currentCharacter.GetSkins();
        int currentIndex = _currentCharacter.GetCurrentSkinIndex();

        skinDropdown.ClearOptions();

        List<string> options = new List<string>();
        for (int i = 0; i < skins.Count; i++)
        {
            string label = string.IsNullOrEmpty(skins[i].id) ? $"Skin {i}" : skins[i].id;
            options.Add(label);
        }

        skinDropdown.AddOptions(options);
        skinDropdown.SetValueWithoutNotify(Mathf.Max(0, currentIndex));
    }

    private void OnSkinSelected(int index)
    {
        if (_currentCharacter == null) return;
        _currentCharacter.SetSkinByIndex(index);
    }

    // UI 버튼 이벤트 연결용 (복구됨!)
    public void OnResetButtonClicked()
    {
        if (_currentCharacter != null && menuRoot != null)
        {
            _currentCharacter.ResetPositionToCamera();
            Debug.Log("[MRCharacterMenu] Character position reset.");
        }
    }

    private void SyncSlidersWithPrefs()
    {
        SetSliderSilently(sizeSlider, PlayerPrefs.GetFloat("CharacterSize", 1f), sizeValueText, "0.0");
        SetSliderSilently(idleDelaySlider, PlayerPrefs.GetFloat("IdleRerollDelayAfterVoice", 1f), idleDelayValueText, "0.0");
        SetSliderSilently(idleChanceSlider, PlayerPrefs.GetFloat("IdleRerollChance", 1f), idleChanceValueText, "0.00");
        SetSliderSilently(idleIntervalSlider, PlayerPrefs.GetFloat("IdleRerollInterval", 5f), idleIntervalValueText, "0.0");
    }

    private void SetSliderSilently(Slider slider, float value, TMP_Text text, string format)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
            if (text != null) text.text = value.ToString(format);
        }
    }

    private void OnSliderChanged(string prefKey, float value, TMP_Text text, string format)
    {
        PlayerPrefs.SetFloat(prefKey, value);
        PlayerPrefs.Save();

        if (text != null) text.text = value.ToString(format);

        if (prefKey == "CharacterSize")
        {
            foreach (var character in MRSpineCharacterController.Instances)
            {
                if (character != null) character.ApplyCharacterSize(value);
            }
        }
    }

    private void OnDestroy()
    {
        if (skinDropdown != null)
            skinDropdown.onValueChanged.RemoveListener(OnSkinSelected);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseMenu);
    }
}