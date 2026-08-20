// 서브 컨텍스트 메뉴를 부모 메뉴에 붙여 둔다.
//
// 왜 필요한가 (2026-08-19 실기)
// ---------------------------
// `Context Menu`와 `Context Menu Sub`는 MR에서 **각각 독립된 월드 캔버스**다
// (§4-18: 월드 캔버스를 다른 캔버스 안에 넣을 수 없다). 그래서 둘 다 각자 잡아서
// 옮길 수 있는데, 서브메뉴만 따로 떼어 옮기면 부모 항목과의 관계가 끊겨 이상해진다.
//
// 계층으로 묶을 수 없으니 **런타임에 상대 자세를 유지**하는 방식으로 붙인다.
// `ContextMenu.ShowNextTo()`가 잡아준 첫 위치를 부모 기준 로컬 좌표로 기억하고,
// 그 뒤로는 부모를 따라간다. 부모를 잡고 옮기면 서브메뉴도 같이 온다.
//
// 잡기 자체는 씬에서 막는다 — `Context Menu Sub/GrabFrame`을 비활성으로 저장하면
// `MRFloatingPanel.hideInteractionWhenTransparent`가 "이미 꺼둔 자식"으로 보고
// 되살리지 않는다(§8-7). 판정 면(`HandInteraction`)은 남겨야 항목을 누를 수 있다.

using UnityEngine;
using DevionGames.UIWidgets;
using ContextMenu = DevionGames.UIWidgets.ContextMenu;

[RequireComponent(typeof(RectTransform))]
public class MRSubMenuFollower : MonoBehaviour
{
    [Tooltip("따라갈 부모 메뉴. 비우면 UIWidget.Name이 'ContextMenu'인 위젯을 찾는다.")]
    [SerializeField] private RectTransform target;

    [Tooltip("보이는 동안 매 프레임 상대 자세를 유지한다. 끄면 뜰 때 한 번만 맞춘다.")]
    [SerializeField] private bool keepFollowing = true;

    private RectTransform _self;
    private CanvasGroup _group;
    private UIWidget _widget;

    private bool _hasOffset;
    private Vector3 _localPosition;
    private Quaternion _localRotation;

    private void Awake()
    {
        _self = transform as RectTransform;
        _group = GetComponent<CanvasGroup>();
        _widget = GetComponent<UIWidget>();
    }

    // 위치 확정은 ShowNextTo가 LateUpdate 이전에 끝내므로 여기서 읽는다.
    // MRFloatingPanel의 alpha 감시도 LateUpdate라 순서가 갈릴 수 있어,
    // "보이는 동안 매 프레임 맞춘다"로 두면 어느 쪽이 먼저든 결과가 같다.
    private void LateUpdate()
    {
        if (!ResolveTarget()) return;

        if (!IsVisible())
        {
            // 닫히면 다음에 뜰 때 자리를 다시 잡도록 오프셋을 버린다.
            _hasOffset = false;
            return;
        }

        if (!_hasOffset)
        {
            CaptureOffset();
            return;
        }

        if (!keepFollowing) return;

        _self.position = target.TransformPoint(_localPosition);
        _self.rotation = target.rotation * _localRotation;
    }

    private void CaptureOffset()
    {
        _localPosition = target.InverseTransformPoint(_self.position);
        _localRotation = Quaternion.Inverse(target.rotation) * _self.rotation;
        _hasOffset = true;
    }

    private bool IsVisible()
    {
        if (_group != null) return _group.alpha > 0.001f;
        if (_widget != null) return _widget.IsVisible;

        return gameObject.activeInHierarchy;
    }

    private bool ResolveTarget()
    {
        if (target != null) return true;

        ContextMenu main = WidgetUtility.Find<ContextMenu>("ContextMenu");
        if (main == null) return false;

        target = main.transform as RectTransform;
        return target != null;
    }
}
