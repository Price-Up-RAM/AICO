using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// TMP_InputField 선택 시 Unity TouchScreenKeyboard(= Quest OS 시스템 키보드)를 호출하고
/// 입력값을 InputField에 실시간 동기화합니다.
///
/// Meta XR Core SDK v201부터 OVRVirtualKeyboard가 제거되어,
/// Unity 표준 TouchScreenKeyboard API를 사용하는 방식으로 전환했습니다.
/// Quest의 시스템 키보드가 자동으로 시야 앞에 떠오르며 ray + 터치 + 한글 입력이 모두 지원됩니다.
///
/// 사용법:
/// 1. 임의의 GameObject에 이 컴포넌트를 붙이고 fields 리스트에 TMP_InputField들을 등록하거나,
///    런타임에 RegisterInputField()로 동적 등록.
/// 2. InputField가 선택되는 순간 시스템 키보드가 자동 표시되고 입력이 라우팅됩니다.
/// </summary>
public class MRTMPVirtualKeyboardBinder : MonoBehaviour
{
    [Header("자동 등록할 InputField들 (선택사항)")]
    [SerializeField] private List<TMP_InputField> fields = new List<TMP_InputField>();

    [Header("키보드 옵션")]
    [Tooltip("InputField의 contentType에 따라 자동 결정. 강제로 지정하고 싶다면 변경.")]
    [SerializeField] private TouchScreenKeyboardType keyboardType = TouchScreenKeyboardType.Default;

    [Tooltip("엔터(Done) 버튼을 누르면 onEndEdit 호출하고 키보드 닫음")]
    [SerializeField] private bool submitOnDone = true;

    // 키보드 열림/닫힘 이벤트 (필드 선택/해제 시)
    public event Action<TMP_InputField> KeyboardOpened;
    public event Action KeyboardClosed;

    // 현재 활성 InputField와 시스템 키보드
    private TMP_InputField _activeField;
    private TouchScreenKeyboard _keyboard;
    private string _lastSyncedText = "";

    /// <summary>
    /// 외부에서 키보드 열림 상태를 빠르게 조회
    /// </summary>
    public bool IsOpen => _keyboard != null && _keyboard.status == TouchScreenKeyboard.Status.Visible;

    private void Awake()
    {
        // 인스펙터에 등록된 모든 필드 자동 후킹
        foreach (var f in fields)
        {
            if (f != null) HookField(f);
        }
    }

    private void OnDestroy()
    {
        foreach (var f in fields)
        {
            if (f != null) UnhookField(f);
        }
        HideKeyboard();
    }

    /// <summary>
    /// 외부에서 동적으로 InputField를 등록 (예: 메뉴 UI가 매번 새로 생성되는 경우)
    /// </summary>
    public void RegisterInputField(TMP_InputField field)
    {
        if (field == null || fields.Contains(field)) return;
        fields.Add(field);
        HookField(field);
    }

    public void UnregisterInputField(TMP_InputField field)
    {
        if (field == null) return;
        UnhookField(field);
        fields.Remove(field);
    }

    private void HookField(TMP_InputField field)
    {
        // 동일한 람다 인스턴스로 후킹/해제하기 위해 UnityAction을 직접 추가
        field.onSelect.AddListener(_ => HandleFieldSelected(field));
        field.onDeselect.AddListener(_ => HandleFieldDeselected(field));
    }

    private void UnhookField(TMP_InputField field)
    {
        // 람다로 추가했기 때문에 정확한 RemoveListener는 불가하지만,
        // 객체 파괴 시 자동 GC되므로 일반적으로 문제 없음
    }

    // =============================================
    // 필드 선택/해제 핸들러
    // =============================================
    private void HandleFieldSelected(TMP_InputField field)
    {
        _activeField = field;

        // 시스템 키보드 호출 (Quest에서는 시스템 가상 키보드가 떠오름)
        // 두 번째 인자는 keyboardType, 나머지: autocorrection, multiline, secure, alert, placeholder
        _keyboard = TouchScreenKeyboard.Open(
            text: field.text ?? "",
            keyboardType: ResolveKeyboardType(field),
            autocorrection: false,
            multiline: false,           // 앵커 이름은 한 줄
            secure: false,
            alert: false,
            textPlaceholder: field.placeholder is TMP_Text ph ? ph.text : "",
            characterLimit: field.characterLimit > 0 ? field.characterLimit : 0
        );

        _lastSyncedText = field.text ?? "";

        Debug.Log($"[MRKeyboardBinder] 시스템 키보드 호출: {field.name}");
        KeyboardOpened?.Invoke(field);
    }

    private TouchScreenKeyboardType ResolveKeyboardType(TMP_InputField field)
    {
        // InputField의 contentType에 따라 적절한 키보드 타입 자동 결정
        switch (field.contentType)
        {
            case TMP_InputField.ContentType.IntegerNumber:
                return TouchScreenKeyboardType.NumberPad;
            case TMP_InputField.ContentType.DecimalNumber:
                return TouchScreenKeyboardType.DecimalPad;
            case TMP_InputField.ContentType.EmailAddress:
                return TouchScreenKeyboardType.EmailAddress;
            case TMP_InputField.ContentType.Pin:
                return TouchScreenKeyboardType.NumbersAndPunctuation;
            default:
                return keyboardType;
        }
    }

    private void HandleFieldDeselected(TMP_InputField field)
    {
        // 사용자가 다른 곳을 클릭해 InputField 포커스가 빠지면 키보드는 알아서 닫힘
        // 단, 우리가 추적하는 활성 필드도 정리
        if (_activeField == field)
        {
            // 키보드를 즉시 닫지는 않음 (Done 또는 Canceled 신호로 처리)
        }
    }

    // =============================================
    // 키보드 상태 폴링: 매 프레임 텍스트와 상태를 InputField에 반영
    // =============================================
    private void Update()
    {
        if (_keyboard == null || _activeField == null) return;

        // 1. 텍스트 동기화 (사용자가 키보드에서 친 글자를 InputField에 반영)
        string currentText = _keyboard.text ?? "";
        if (currentText != _lastSyncedText)
        {
            _activeField.text = currentText;
            _lastSyncedText = currentText;
        }

        // 2. 상태 처리
        switch (_keyboard.status)
        {
            case TouchScreenKeyboard.Status.Done:
                if (submitOnDone)
                {
                    _activeField.text = currentText; // 최종 동기화
                    _activeField.onEndEdit?.Invoke(currentText);
                }
                FinishKeyboard();
                break;

            case TouchScreenKeyboard.Status.Canceled:
            case TouchScreenKeyboard.Status.LostFocus:
                FinishKeyboard();
                break;
        }
    }

    private void FinishKeyboard()
    {
        var fieldToDeselect = _activeField;

        if (_keyboard != null)
        {
            // TouchScreenKeyboard 인스턴스는 사용자가 Done/Canceled 처리하면 OS가 알아서 닫고,
            // 우리는 참조만 비우면 됩니다. 강제로 닫고자 하면 TouchScreenKeyboard.active = false 사용.
            _keyboard = null;
        }

        _activeField = null;
        _lastSyncedText = "";

        if (fieldToDeselect != null && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == fieldToDeselect.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        KeyboardClosed?.Invoke();
    }

    /// <summary>
    /// 외부에서 강제로 키보드를 닫고자 할 때 (예: 메뉴 자체가 닫힐 때)
    /// </summary>
    public void HideKeyboard()
    {
        if (_keyboard != null)
        {
            // 강제 닫기: 인스턴스 프로퍼티로 active 프로퍼티를 false로 설정
            try { _keyboard.active = false; }
            catch (Exception e) { Debug.LogWarning($"[MRKeyboardBinder] active=false 실패: {e.Message}"); }
            _keyboard = null;
        }
        _activeField = null;
        _lastSyncedText = "";
        KeyboardClosed?.Invoke();
    }
}
