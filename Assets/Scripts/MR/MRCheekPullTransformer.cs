using Oculus.Interaction;
using UnityEngine;

// 볼을 잡고 있는 동안 "얼마나 당겼는지"만 MRCheekPull에 넘기는 트랜스포머.
//
// 왜 커스텀인가 — 이게 이 기능의 핵심이다.
// ------------------------------------------------
// 기본 트랜스포머(또는 null)는 잡은 오브젝트의 Transform을 손에 맞춰 직접 움직인다.
// 볼은 스킨드 메시의 **본**이고, Animator가 매 프레임 본 포즈를 자기 값으로 되돌린다.
// 그래서 Transform을 직접 쓰면 Animator와 매 프레임 다퉈 덜덜 떨거나 아예 안 움직인다.
//
// 그래서 이 트랜스포머는 **Transform을 한 번도 건드리지 않는다.**
// 잡은 지점(GrabPoints[0])의 월드 좌표만 읽어 MRCheekPull에 전달하고,
// 실제 본 이동은 MRCheekPull.LateUpdate가 Animator 뒤에 적용한다.
//
// 이 오브젝트는 본이 아니라 본 아래에 런타임 생성된 프록시다(MRCheekPullBinder 참고).
// 프록시를 쓰는 이유는 HandGrabInteractable이 Rigidbody를 요구하는데,
// 애니메이션이 구동하는 본에 Rigidbody를 붙이면 물리와 Animator가 같은 Transform을 두고 다투기 때문이다.
public class MRCheekPullTransformer : MonoBehaviour, ITransformer
{
    // 이 프록시가 담당하는 볼. 바인더가 채운다.
    [SerializeField] private MRCheekPull owner;
    [SerializeField] private int cheekIndex = -1;

    private IGrabbable _grabbable;

    public void Setup(MRCheekPull pullOwner, int index)
    {
        owner = pullOwner;
        cheekIndex = index;
    }

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
    }

    public void BeginTransform()
    {
        if (!IsUsable())
        {
            return;
        }
        owner.BeginPull(cheekIndex, _grabbable.GrabPoints[0].position);
    }

    public void UpdateTransform()
    {
        if (!IsUsable())
        {
            return;
        }
        owner.UpdatePull(cheekIndex, _grabbable.GrabPoints[0].position);
    }

    public void EndTransform()
    {
        if (owner == null)
        {
            return;
        }
        owner.EndPull(cheekIndex);
    }

    // 잡은 지점이 없으면 아무 것도 하지 않는다. GrabPoints는 비어 있을 수 있다.
    private bool IsUsable()
    {
        if (owner == null)
        {
            return false;
        }
        if (_grabbable == null)
        {
            return false;
        }
        return _grabbable.GrabPoints.Count > 0;
    }
}
