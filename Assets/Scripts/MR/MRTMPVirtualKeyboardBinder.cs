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

    [Header("자동 등록")]
    [Tooltip("켜면 fields가 비어 있을 때 씬 전체(비활성 포함)에서 TMP_InputField를 찾아 등록한다. " +
             "수동 배선 없이 동작시키기 위한 옵션이다.")]
    [SerializeField] private bool autoRegisterAllFields = true;

    [Tooltip("런타임에 생성되는 InputField를 주기적으로 다시 찾는 간격(초). 0이면 재탐색하지 않는다.")]
    [SerializeField] private float rescanInterval = 3f;

    private float _nextRescanTime;

    private void Awake()
    {
        // 인스펙터에 등록된 것이 없으면 씬에서 자동으로 찾는다.
        // 이 컴포넌트가 씬에 아예 없어서 키보드가 안 떴던 것이 2026-08-25에 실측됐고,
        // 붙이더라도 fields 수동 배선을 잊으면 같은 증상이라 자동 등록을 기본으로 뒀다.
        if (autoRegisterAllFields && (fields == null || fields.Count == 0))
        {
            RegisterAllFieldsInScene();
        }

        // 인스펙터/자동 등록된 모든 필드 후킹
        foreach (var f in fields)
        {
            if (f != null) HookField(f);
        }

        Debug.Log($"[MRKeyboardBinder] 초기화 | 등록 {fields.Count}개 | TouchScreenKeyboard.isSupported={TouchScreenKeyboard.isSupported} | 자동등록={autoRegisterAllFields}");
        if (!TouchScreenKeyboard.isSupported)
        {
            Debug.Log("[MRKeyboardBinder] 이 플랫폼은 시스템 키보드를 지원하지 않는다(Editor 등). 물리 키보드로 InputField에 직접 입력해야 한다.");
        }
    }

    // 씬 전체(비활성 포함)에서 TMP_InputField를 찾아 등록한다.
    private void RegisterAllFieldsInScene()
    {
        TMP_InputField[] found = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            TMP_InputField f = found[i];
            if (f == null || fields.Contains(f))
            {
                continue;
            }
            fields.Add(f);
            Debug.Log($"[MRKeyboardBinder] 자동 등록: '{GetFieldPath(f)}'");
        }
    }

    private static string GetFieldPath(TMP_InputField f)
    {
        string path = f.gameObject.name;
        Transform t = f.transform.parent;
        int depth = 0;
        while (t != null && depth < 3)
        {
            path = t.name + "/" + path;
            t = t.parent;
            depth++;
        }
        return path;
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

        // 이 플랫폼이 시스템 키보드를 지원하지 않으면 Open을 부르지 않는다.
        //
        // 2026-08-26 실측: Editor(+Quest Link)는 isSupported=False인데
        // TouchScreenKeyboard.Open()이 null이 아닌 '껍데기'를 돌려준다.
        // 그걸 Update에서 .text로 읽는 순간 네이티브 핸들이 없어 NRE가 나고,
        // Update는 매 프레임 도니까 로그가 초당 수십 줄씩 쌓인다.
        // '호출이 성공했다'와 '쓸 수 있는 객체다'는 다른 사실이다 (Kickoff Guide 4-58).
        if (!TouchScreenKeyboard.isSupported)
        {
            _keyboard = null;
            _lastSyncedText = field.text ?? "";
            Debug.Log($"[MRKeyboardBinder] '{field.name}' 선택 — 이 플랫폼은 시스템 키보드 미지원(isSupported=False), " +
                      "Open 생략. 물리 키보드나 Tools → MR → 질문 보내기 창을 쓸 것");
            KeyboardOpened?.Invoke(field);
            return;
        }

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

        // 호출했다는 것과 실제로 떴다는 것은 다른 사실이다 (Kickoff Guide 4-58).
        string status = "(null)";
        if (_keyboard != null)
        {
            status = _keyboard.status.ToString();
        }
        Debug.Log($"[MRKeyboardBinder] 시스템 키보드 호출: '{field.name}' | keyboard={( _keyboard == null ? "null" : "생성됨")} status={status} isSupported={TouchScreenKeyboard.isSupported} active={TouchScreenKeyboard.visible}");
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
        // 런타임에 생성된 InputField(패널이 나중에 열리는 경우 등)를 주기적으로 잡는다.
        if (autoRegisterAllFields && rescanInterval > 0f && Time.unscaledTime >= _nextRescanTime)
        {
            _nextRescanTime = Time.unscaledTime + rescanInterval;
            int before = fields.Count;
            TMP_InputField[] found = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                TMP_InputField f = found[i];
                if (f == null || fields.Contains(f))
                {
                    continue;
                }
                fields.Add(f);
                HookField(f);
                Debug.Log($"[MRKeyboardBinder] 런타임 등록: '{GetFieldPath(f)}'");
            }
            if (fields.Count != before)
            {
                Debug.Log($"[MRKeyboardBinder] 재탐색: {before}개 → {fields.Count}개");
            }
        }

        if (_keyboard == null || _activeField == null) return;

        // 네이티브 핸들이 죽은 껍데기일 수 있다. 한 번 실패하면 참조를 버리고 조용히 끝낸다.
        // 여기서 잡지 않으면 매 프레임 예외가 나 로그가 폭발하고 프레임이 떨어진다.
        string currentText;
        TouchScreenKeyboard.Status status;
        try
        {
            currentText = _keyboard.text ?? "";
            status = _keyboard.status;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[MRKeyboardBinder] 시스템 키보드 접근 실패 — 참조를 버린다: {e.GetType().Name} {e.Message}");
            _keyboard = null;
            return;
        }

        // 1. 텍스트 동기화 (사용자가 키보드에서 친 글자를 InputField에 반영)
        if (currentText != _lastSyncedText)
        {
            _activeField.text = currentText;
            _lastSyncedText = currentText;
        }

        // 2. 상태 처리
        switch (status)
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
