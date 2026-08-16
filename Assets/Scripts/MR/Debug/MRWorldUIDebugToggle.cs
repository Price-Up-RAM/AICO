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

        Debug.Log($"[MRWorldUIDebugToggle] 준비 완료. 대상 {_targets.Count}개. " +
                  $"왼손 메뉴 제스처(손바닥 위로 + 핀치)로 토글하세요.");
    }

    private void CollectTargets()
    {
        _targets.Clear();
        foreach (Transform child in worldUIRoot)
        {
            if (IsExcluded(child.name)) continue;
            _targets.Add(child.gameObject);
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

    private void ApplyVisibility()
    {
        foreach (var go in _targets)
        {
            if (go == null) continue;
            go.SetActive(_visible);
        }
    }

    /// <summary>외부(다른 디버그 UI 등)에서 강제로 상태를 바꿀 때.</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        ApplyVisibility();
    }
}
