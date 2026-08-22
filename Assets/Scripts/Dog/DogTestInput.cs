using UnityEngine;

/// <summary>
/// 에디터 테스트용 강아지 이동/액션 입력.
/// WASD/화살표: 이동 (Shift 누르면 달리기, LowPoly/Wolf 컨트롤러 한정)
/// Space: 점프(Latte) / 짖기(LowPoly, Wolf 공통)
/// C: 앉기(Latte, LowPoly)
/// V: 하울링(Wolf) / 두번째 짖기(Latte)
/// 같은 오브젝트에 세 컨트롤러 중 붙어있는 것 하나를 자동으로 찾아 사용.
/// </summary>
public class DogTestInput : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float runSpeed = 4f;
    [SerializeField] private float rotateSpeed = 180f;

    private DogAnimationController _latte;
    private LowPolyDogAnimationController _lowPoly;
    private WolfAnimationController _wolf;

    private void Awake()
    {
        _latte = GetComponent<DogAnimationController>();
        _lowPoly = GetComponent<LowPolyDogAnimationController>();
        _wolf = GetComponent<WolfAnimationController>();
    }

    private void Update()
    {
        float v = Input.GetAxisRaw("Vertical");
        float h = Input.GetAxisRaw("Horizontal");
        bool isRunKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool isMoving = Mathf.Abs(v) > 0.01f;

        // 회전 (좌우 입력)
        if (Mathf.Abs(h) > 0.01f)
        {
            transform.Rotate(Vector3.up, h * rotateSpeed * Time.deltaTime);
        }

        // 이동 (전후 입력, 바라보는 방향 기준)
        if (isMoving)
        {
            float speed = isRunKey ? runSpeed : moveSpeed;
            transform.position += transform.forward * (v * speed * Time.deltaTime);
        }

        ApplyMovementState(isMoving, isRunKey && isMoving);
        HandleActionKeys();
    }

    private void ApplyMovementState(bool moving, bool running)
    {
        if (_latte != null)
        {
            _latte.SetWalking(moving);
        }

        if (_lowPoly != null)
        {
            if (!moving) _lowPoly.SetWalking(false);
            else if (running) _lowPoly.SetRunning(true);
            else _lowPoly.SetWalking(true);
        }

        if (_wolf != null)
        {
            if (!moving) _wolf.SetWalking(false);
            else if (running) _wolf.SetRunning(true);
            else _wolf.SetWalking(true);
        }
    }

    private void HandleActionKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _latte?.Jump();
            _lowPoly?.Bark();
            _wolf?.Howl();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            _latte?.Sit1();
            _lowPoly?.SetSitting(true);
        }
        else if (Input.GetKeyUp(KeyCode.C))
        {
            _lowPoly?.SetSitting(false);
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            _latte?.Sit2();
            _lowPoly?.BarkRepeat();
            _wolf?.Howl();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            _latte?.Bark();
            _lowPoly?.Bark();
        }
    }
}
