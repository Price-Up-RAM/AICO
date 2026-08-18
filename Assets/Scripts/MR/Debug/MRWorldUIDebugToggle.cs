// 디버그용 — 왼손(비주력 손) 메뉴 제스처로 WorldUI 아래 모든 UI를 한 번에 켜고 끈다.
//
// 왜 필요한가
// ----------
// 앱 시작 시 UI가 전부 비활성화되는데, MR에서 이들을 켜는 정상 경로(캐릭터 컨텍스트 메뉴 →
// 각 패널 열기)는 Phase 4에서 만든다. 그때까지 실기에서 패널을 띄워볼 방법이 없어서
// 테스트용 복제본(`Image_ChatBalloon (1)` 등)을 만들어 쓰고 있었는데, 이 컴포넌트가 있으면
// 그 복제본들이 필요 없어진다.
//
// 입력
// ----
// OVRInput.Button.Start = 손바닥을 위로 향한 채 핀치하는 메타 메뉴 제스처.
// Kickoff Guide §4-6대로 **비주력 손(오른손잡이 기준 왼손)에서만** 발생한다.
// 오른손의 같은 제스처는 OS 홈으로 나가는 동작이라 앱에서 가로챌 수 없다.
//
// 주의: Phase 4에서 정상적인 메뉴 소환 경로가 만들어지면 이 컴포넌트는 제거한다.
// 남겨두면 실제 기능과 입력이 충돌한다.

using System.Collections.Generic;
using UnityEngine;

public class MRWorldUIDebugToggle : MonoBehaviour
{
    [Tooltip("토글할 부모. 비워두면 씬에서 이름으로 'WorldUI'를 찾는다.")]
    [SerializeField] private Transform worldUIRoot;

    [Tooltip("이름에 이 문자열들이 포함된 자식은 토글 대상에서 제외한다. " +
             "(상시 켜져 있어야 하는 시스템 오브젝트용)")]
    [SerializeField] private string[] excludeNameContains = new string[0];

    [Tooltip("시작 시 전부 꺼둘지 여부. 원래 씬 상태를 유지하려면 끈다.")]
    [SerializeField] private bool startHidden = true;

    private bool _visible;
    private readonly List<GameObject> _targets = new List<GameObject>();

    private void Start()
    {
        if (worldUIRoot == null)
        {
            GameObject found = GameObject.Find("WorldUI");
            if (found != null) worldUIRoot = found.transform;
        }

        if (worldUIRoot == null)
        {
            Debug.LogWarning("[MRWorldUIDebugToggle] WorldUI를 찾지 못했습니다. " +
                             "worldUIRoot를 인스펙터에서 직접 지정하세요.");
            enabled = false;
            return;
        }

        CollectTargets();

        _visible = !startHidden;
        ApplyVisibility();

        int panelCount = 0;
        var names = new System.Text.StringBuilder();
        foreach (var go in _targets)
        {
            if (go == null) continue;
            int p = go.GetComponentsInChildren<MRFloatingPanel>(true).Length;
            panelCount += p;
            names.Append($"\n    · {go.name} (MRFloatingPanel {p}개)");
        }

        Debug.Log($"[MRWorldUIDebugToggle] 준비 완료. 토글 대상 {_targets.Count}개, " +
                  $"그 안의 MRFloatingPanel {panelCount}개.{names}");
    }

    private void CollectTargets()
    {
        _targets.Clear();
        foreach (Transform child in worldUIRoot)
        {
            Collect(child);
        }
    }

    /// <summary>실제 UI 오브젝트(캔버스를 가진 것)를 찾을 때까지 내려간다.
    ///
    /// WorldUI 아래를 빈 오브젝트로 그룹핑(Balloons / Panels / Apps …)하면 직속 자식은
    /// 그룹 오브젝트뿐이다. 그룹만 SetActive 하면, 패널 자신의 activeSelf가 false인 경우
    /// (이전 토글이 꺼둔 상태로 씬이 저장되면 그렇게 된다) 부모를 켜도 자식은 계속 꺼져 있다.
    /// 실기 확인 2026-08-15: 그룹핑 직후 그룹에 든 메뉴만 전부 안 뜨던 원인이다.</summary>
    private void Collect(Transform t)
    {
        if (t == null) return;
        if (IsExcluded(t.name)) return;

        // 캔버스가 있으면 그 자체가 UI 단위다 — 더 내려가지 않는다.
        if (t.GetComponent<Canvas>() != null)
        {
            _targets.Add(t.gameObject);
            return;
        }

        // 그룹(빈 오브젝트)이면 한 단계 더 내려간다.
        if (t.childCount == 0)
        {
            _targets.Add(t.gameObject);
            return;
        }

        foreach (Transform child in t)
        {
            Collect(child);
        }
    }

    private bool IsExcluded(string childName)
    {
        foreach (string s in excludeNameContains)
        {
            if (string.IsNullOrEmpty(s)) continue;
            if (childName.Contains(s)) return true;
        }
        return false;
    }

    private void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            _visible = !_visible;
            ApplyVisibility();
            Debug.Log($"[MRWorldUIDebugToggle] WorldUI 토글 → {(_visible ? "표시" : "숨김")} ({_targets.Count}개)");
        }
    }

    [Header("디버그 배치")]
    [Tooltip("켜면 패널들을 사용자 주위에 부채꼴로 펼쳐 놓는다. " +
             "끄면 MRFloatingPanel 기본 소환 위치(전부 눈앞 같은 자리)에 겹쳐 뜬다.")]
    [SerializeField] private bool spreadPanelsInArc = true;

    [Tooltip("부채꼴 배치 시 사용자로부터의 거리(m).")]
    [SerializeField] private float arcRadius = 1.1f;

    [Tooltip("부채꼴 전체가 덮는 각도(도).")]
    [SerializeField] private float arcSpan = 140f;

    private void ApplyVisibility()
    {
        int panelIndex = 0;
        int panelTotal = 0;
        if (spreadPanelsInArc)
        {
            foreach (var go in _targets)
            {
                if (go != null && go.GetComponentInChildren<MRFloatingPanel>(true) != null) panelTotal++;
            }
        }

        foreach (var go in _targets)
        {
            if (go == null) continue;

            // 부모 그룹이 꺼져 있으면 자식을 켜도 보이지 않는다.
            // 그룹핑(WorldUI/Balloons 등) 이전 빌드에서 그룹이 SetActive(false)로 꺼진 채
            // 씬이 저장돼 있으면 그대로 막힌다 — 실기 확인 2026-08-15.
            if (_visible) EnsureAncestorsActive(go.transform);

            go.SetActive(_visible);

            // MRFloatingPanel이 붙은 패널은 SetActive만으로는 안 보인다 —
            // Awake()에서 panelCanvas.enabled = false로 시작해 Open()을 불러야 켜지기 때문이다.
            // (실기 확인 2026-08-15: 이 처리가 없어서 설정·캐릭터목록·대화기록 패널이
            //  토글해도 나타나지 않았다.)
            // 자식까지 훑는다 — WorldUI 아래를 빈 오브젝트로 그룹핑(Panels/Balloons 등)하면
            // 패널이 직속 자식이 아니게 되기 때문이다.
            foreach (var panel in go.GetComponentsInChildren<MRFloatingPanel>(true))
            {
                if (panel == null) continue;

                if (!_visible)
                {
                    panel.Close();
                    continue;
                }

                if (spreadPanelsInArc && panelTotal > 0)
                {
                    panel.OpenAt(ArcPosition(panelIndex, panelTotal));
                    panelIndex++;
                }
                else
                {
                    panel.Open();
                }
            }
        }
    }

    /// <summary>부모 체인을 WorldUI까지 거슬러 올라가며 전부 활성화한다.</summary>
    private void EnsureAncestorsActive(Transform t)
    {
        Transform cur = t.parent;
        while (cur != null)
        {
            if (!cur.gameObject.activeSelf) cur.gameObject.SetActive(true);
            if (cur == worldUIRoot) break;
            cur = cur.parent;
        }
    }

    /// <summary>패널들이 한 자리에 겹치지 않도록 사용자 주위 부채꼴로 흩어 놓는다.</summary>
    private Vector3 ArcPosition(int index, int total)
    {
        Transform eye = ResolveEye();
        if (eye == null) return transform.position;

        float t = total <= 1 ? 0.5f : (float)index / (total - 1);
        float angle = Mathf.Lerp(-arcSpan * 0.5f, arcSpan * 0.5f, t);

        Vector3 forward = eye.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
        Vector3 pos = eye.position + dir * arcRadius;

        // 개수가 많으면 위아래 두 줄로 나눠 겹침을 더 줄인다.
        pos.y = eye.position.y + ((index % 2 == 0) ? -0.15f : 0.2f);
        return pos;
    }

    private Transform _eye;

    private Transform ResolveEye()
    {
        if (_eye != null) return _eye;

        var byName = GameObject.Find("CenterEyeAnchor");
        if (byName != null) { _eye = byName.transform; return _eye; }

        if (Camera.main != null) { _eye = Camera.main.transform; return _eye; }
        return null;
    }

    /// <summary>외부(다른 디버그 UI 등)에서 강제로 상태를 바꿀 때.</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        ApplyVisibility();
    }
}
