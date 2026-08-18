using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// (임시·Phase 4-A 검증용) 레거시 UI 열기 경로를 손으로 두들겨보기 위한 트리거.
///
/// 왜 필요한가
/// ----------
/// Phase 4-A의 정식 트리거(MRIntentRouter → 캐릭터 컨텍스트 메뉴)는 아직 없다.
/// 그런데 §4-33 수정이 제대로 됐는지는 **레거시 경로를 실제로 타봐야** 알 수 있다.
///
/// 이전의 MRWorldUIDebugToggle은 MRFloatingPanel.Open()을 **직접** 호출해
/// UIManager를 통째로 건너뛰었다. 그래서 아래 두 문제를 한 번도 드러내지 못했다:
///   ① UIManager.ShowSimpleUI()가 SetActive 직전에 데스크톱 캔버스 좌표를 대입한다
///      (메인 Canvas lossyScale 0.75 → 700px가 525m). §4-36
///   ② 이미 active=1인 오브젝트에는 SetActive(true)가 no-op이라 OnEnable이 안 뜬다.
///
/// 이 컴포넌트는 **반드시 UIManager를 통해서만** 연다. 그래야 검증이 의미가 있다.
///
/// 사용법: 비주력 손 palm-up 핀치(OVRInput.Button.Start, §4-6)를 누를 때마다
/// 다음 패널로 넘어간다. 이전 패널은 닫는다. 한 바퀴 다 돌면 두 번째 바퀴에서
/// **"닫았다 다시 연" 경로**가 검증된다 — 패널을 손으로 옮겨두고 한 바퀴 돌려서
/// 그 자리에 그대로 있는지 보면 §4-27까지 확인된다.
///
/// Phase 4-A 완료 시 삭제할 것.
/// </summary>
public class MRLegacyOpenDebugTrigger : MonoBehaviour
{
    [Tooltip("열린 패널의 월드 위치를 로그로 남긴다. 데스크톱 좌표 텔레포트(수백 m) 진단용.")]
    [SerializeField] private bool logWorldPosition = true;

    [Tooltip("눈(CenterEyeAnchor)에서 이 거리(m)를 넘으면 경고 로그를 낸다.")]
    [SerializeField] private float suspiciousDistance = 5f;

    private class Step
    {
        public string label;
        public Action open;
        public Action close;
        public string panelObjectName;
    }

    private readonly List<Step> _steps = new List<Step>();
    private int _index = -1;

    private void Start()
    {
        // UIManager의 레거시 열기/닫기 경로만 쓴다. MRFloatingPanel을 직접 만지지 않는다.
        _steps.Add(new Step
        {
            label = "Calendar",
            panelObjectName = "CalendarPicker",
            open = delegate { UIManager.Instance.ShowCalendar(); },
            close = delegate { UIManager.Instance.CloseCalendar(); }
        });

        _steps.Add(new Step
        {
            label = "TODOList",
            panelObjectName = "TODOList",
            open = delegate { UIManager.Instance.ShowTODOList(); },
            close = delegate { UIManager.Instance.CloseTODOList(); }
        });

        _steps.Add(new Step
        {
            label = "ChatHistory",
            panelObjectName = "ChatHistory",
            open = delegate { UIManager.Instance.ShowChatHistory(); },
            close = delegate { UIManager.Instance.CloseChatHistory(); }
        });

        _steps.Add(new Step
        {
            label = "Alarm",
            panelObjectName = "Alarm",
            open = delegate { UIManager.Instance.ShowAlarm(); },
            close = delegate { UIManager.Instance.CloseAlarm(); }
        });

        _steps.Add(new Step
        {
            label = "CharChange",
            panelObjectName = "CharChange",
            open = delegate { UIManager.Instance.ShowCharChange(); },
            close = delegate { UIManager.Instance.CloseCharChange(); }
        });

        Debug.Log($"[MRLegacyOpen] 준비 완료. 비주력 손 palm-up 핀치로 {_steps.Count}개 패널을 순환합니다.");
    }

    private void Update()
    {
        if (!OVRInput.GetDown(OVRInput.Button.Start)) return;

        Advance();
    }

    /// <summary>에디터/외부에서도 부를 수 있게 공개해둔다.</summary>
    public void Advance()
    {
        if (_steps.Count == 0) return;

        if (UIManager.Instance == null)
        {
            Debug.LogError("[MRLegacyOpen] UIManager.Instance가 null입니다.");
            return;
        }

        // 이전 패널 닫기 — 이 경로가 SetActive(false)를 태우므로
        // MRFloatingPanel.OnDisable이 월드 포즈를 기억하게 된다.
        if (_index >= 0 && _index < _steps.Count)
        {
            Step prev = _steps[_index];
            Debug.Log($"[MRLegacyOpen] 닫기: {prev.label}");
            SafeInvoke(prev.close, prev.label + ".close");
        }

        _index++;
        if (_index >= _steps.Count) _index = 0;

        Step cur = _steps[_index];
        Debug.Log($"[MRLegacyOpen] 열기: {cur.label}  ({_index + 1}/{_steps.Count})");
        SafeInvoke(cur.open, cur.label + ".open");

        if (logWorldPosition) ReportPosition(cur);
    }

    private void SafeInvoke(Action action, string what)
    {
        if (action == null) return;

        try
        {
            action();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MRLegacyOpen] {what} 실패: {e.GetType().Name} {e.Message}");
        }
    }

    /// <summary>열린 패널이 실제로 어디에 놓였는지 로그로 남긴다.
    /// 데스크톱 캔버스 좌표가 그대로 먹으면 눈에서 수백 m 떨어진 값이 찍힌다.</summary>
    private void ReportPosition(Step step)
    {
        GameObject panel = GameObject.Find(step.panelObjectName);
        if (panel == null)
        {
            Debug.LogWarning($"[MRLegacyOpen] '{step.panelObjectName}' 오브젝트를 못 찾았습니다 " +
                             "(비활성이면 GameObject.Find로는 안 잡힙니다 — 열리지 않았다는 뜻).");
            return;
        }

        Vector3 p = panel.transform.position;

        Transform eye = ResolveEye();
        if (eye == null)
        {
            Debug.Log($"[MRLegacyOpen] {step.label} 위치 = {p} (눈 트랜스폼을 못 찾아 거리 생략)");
            return;
        }

        float dist = Vector3.Distance(eye.position, p);

        string msg = $"[MRLegacyOpen] {step.label} 위치 = {p}, 눈에서 {dist:F2} m";
        if (dist > suspiciousDistance)
        {
            Debug.LogError(msg + "  ← 너무 멉니다. 데스크톱 캔버스 좌표가 그대로 먹었을 가능성 " +
                                 "(UIPositionManager의 MR 분기 / MRFloatingPanel의 포즈 복원 확인).");
            return;
        }

        Debug.Log(msg);
    }

    private Transform ResolveEye()
    {
        GameObject byName = GameObject.Find("CenterEyeAnchor");
        if (byName != null) return byName.transform;

        Camera cam = Camera.main;
        if (cam != null) return cam.transform;

        return null;
    }
}
