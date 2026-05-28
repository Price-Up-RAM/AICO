using UnityEngine;

// Animator 창에서 블렌드 처리가 필요한 각 상태(State)를 클릭한 뒤 
// Add Behaviour 버튼을 통해 이 컴포넌트를 부착합니다.
public class AnimationBlendStateChanger : StateMachineBehaviour
{
    [Tooltip("Animator 상태 이름 (예: idle, Talk, Listen, Pat, Walk, Pick, Fall)")]
    public string stateName;

    // 애니메이션 상태에 진입할 때 유니티가 자동으로 1회 호출해 줍니다.
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 대상 Animator 객체에 붙은 Controller를 찾아 이벤트를 넘겨줍니다.
        var controller = animator.GetComponent<AnimationBlendController>();
        if (controller != null)
        {
            controller.OnStateEntered(stateName);
        }
        else
        {
            // 혹시 대상이 상위 객체에 부착되어 있을 경우를 대비
            controller = animator.GetComponentInParent<AnimationBlendController>();
            if (controller != null)
                controller.OnStateEntered(stateName);
        }
    }
}
