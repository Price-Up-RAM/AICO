using UnityEngine;

/// <summary>
/// cookie 리그(Latte 애니메이션 이식) 임시 테스트용 숫자키 입력.
/// 1: 점프, 2: 앉기1, 3: 앉기2, 4: 짖기, 5: 짖기(연속), 6: Unique5, 7: 걷기(토글)
/// </summary>
[RequireComponent(typeof(DogAnimationController))]
public class DogNumberKeyTestInput : MonoBehaviour
{
    private DogAnimationController _controller;
    private bool _walking;

    private void Awake()
    {
        _controller = GetComponent<DogAnimationController>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) _controller.Jump();
        if (Input.GetKeyDown(KeyCode.Alpha2)) _controller.Sit1();
        if (Input.GetKeyDown(KeyCode.Alpha3)) _controller.Sit2();
        if (Input.GetKeyDown(KeyCode.Alpha4)) _controller.Bark();
        if (Input.GetKeyDown(KeyCode.Alpha5)) _controller.BarkRepeat();
        if (Input.GetKeyDown(KeyCode.Alpha6)) _controller.Unique5();
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            _walking = !_walking;
            _controller.SetWalking(_walking);
        }
    }
}
