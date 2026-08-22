using UnityEngine;

/// <summary>
/// Low Poly Animated Animals 세트 중 Dog-Chihuahua / Dog-GreatDane 컨트롤러 기준.
/// Parameters(all bool): isWalking, isRunning, isBarking, isSitting
/// Fox.controller도 동일 4종 파라미터 + isAttacking을 추가로 가지므로 이 스크립트로 겸용 가능
/// (isAttacking은 별도 메서드로 처리, 대상 Animator에 파라미터가 없으면 SetBool 호출 시 경고만 뜨고 무시됨에 주의)
/// </summary>
[RequireComponent(typeof(Animator))]
public class LowPolyDogAnimationController : MonoBehaviour
{
    private static readonly int IsWalking = Animator.StringToHash("isWalking");
    private static readonly int IsRunning = Animator.StringToHash("isRunning");
    private static readonly int IsBarking = Animator.StringToHash("isBarking");
    private static readonly int IsSitting = Animator.StringToHash("isSitting");
    private static readonly int IsAttacking = Animator.StringToHash("isAttacking");

    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    [Header("Sounds")]
    [SerializeField] private AudioClip barkSingleClip;
    [SerializeField] private AudioClip barkingClip;
    [SerializeField] private AudioClip whiningClip;

    [Header("Random Idle")]
    [SerializeField] private bool enableRandomIdle = true;
    [SerializeField] private float randomIdleMinInterval = 5f;
    [SerializeField] private float randomIdleMaxInterval = 12f;
    [SerializeField] private float randomActionDuration = 2f;

    private float _randomIdleTimer;
    private bool _isMoving;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        ResetRandomIdleTimer();
    }

    private void Update()
    {
        if (!enableRandomIdle || _isMoving) return;

        _randomIdleTimer -= Time.deltaTime;
        if (_randomIdleTimer <= 0f)
        {
            PlayRandomIdle();
            ResetRandomIdleTimer();
        }
    }

    private void ResetRandomIdleTimer()
    {
        _randomIdleTimer = Random.Range(randomIdleMinInterval, randomIdleMaxInterval);
    }

    // ---- 기본 이동 ----

    public void SetWalking(bool walking)
    {
        _isMoving = walking || animator.GetBool(IsRunning);
        animator.SetBool(IsWalking, walking);
        if (walking) animator.SetBool(IsRunning, false);
    }

    public void SetRunning(bool running)
    {
        _isMoving = running || animator.GetBool(IsWalking);
        animator.SetBool(IsRunning, running);
        if (running) animator.SetBool(IsWalking, false);
    }

    public void StopMoving()
    {
        _isMoving = false;
        animator.SetBool(IsWalking, false);
        animator.SetBool(IsRunning, false);
    }

    // ---- 액션 트리거 ----

    public void SetSitting(bool sitting) => animator.SetBool(IsSitting, sitting);

    public void SetAttacking(bool attacking) => animator.SetBool(IsAttacking, attacking);

    public void Bark()
    {
        StartCoroutine(PulseBool(IsBarking, randomActionDuration));
        PlaySound(barkSingleClip);
    }

    public void BarkRepeat()
    {
        StartCoroutine(PulseBool(IsBarking, randomActionDuration));
        PlaySound(barkingClip);
    }

    public void Whine()
    {
        PlaySound(whiningClip);
    }

    // ---- 랜덤 유휴 동작 ----

    private void PlayRandomIdle()
    {
        int pick = Random.Range(0, 2);
        switch (pick)
        {
            case 0:
                StartCoroutine(PulseBool(IsSitting, randomActionDuration));
                break;
            case 1:
                Bark();
                break;
        }
    }

    private System.Collections.IEnumerator PulseBool(int paramHash, float duration)
    {
        animator.SetBool(paramHash, true);
        yield return new WaitForSeconds(duration);
        animator.SetBool(paramHash, false);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
