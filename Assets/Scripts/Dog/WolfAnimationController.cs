using UnityEngine;

/// <summary>
/// Low Poly Animated Animals 세트의 Wolf.controller 기준.
/// Parameters(all bool): isHowling, isAttacking, isDead, isRunning, isWalking
/// </summary>
[RequireComponent(typeof(Animator))]
public class WolfAnimationController : MonoBehaviour
{
    private static readonly int IsHowling = Animator.StringToHash("isHowling");
    private static readonly int IsAttacking = Animator.StringToHash("isAttacking");
    private static readonly int IsDead = Animator.StringToHash("isDead");
    private static readonly int IsRunning = Animator.StringToHash("isRunning");
    private static readonly int IsWalking = Animator.StringToHash("isWalking");

    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;

    [Header("Sounds")]
    [SerializeField] private AudioClip howlClip; // wolf-howl.ogg

    [Header("Random Idle")]
    [SerializeField] private bool enableRandomIdle = true;
    [SerializeField] private float randomIdleMinInterval = 8f;
    [SerializeField] private float randomIdleMaxInterval = 18f;
    [SerializeField] private float howlDuration = 3f;

    private float _randomIdleTimer;
    private bool _isMoving;
    private bool _isDead;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        ResetRandomIdleTimer();
    }

    private void Update()
    {
        if (!enableRandomIdle || _isMoving || _isDead) return;

        _randomIdleTimer -= Time.deltaTime;
        if (_randomIdleTimer <= 0f)
        {
            Howl();
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
        if (_isDead) return;
        _isMoving = walking || animator.GetBool(IsRunning);
        animator.SetBool(IsWalking, walking);
        if (walking) animator.SetBool(IsRunning, false);
    }

    public void SetRunning(bool running)
    {
        if (_isDead) return;
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

    // ---- 액션 ----

    public void SetAttacking(bool attacking)
    {
        if (_isDead) return;
        animator.SetBool(IsAttacking, attacking);
    }

    public void Howl()
    {
        if (_isDead) return;
        StartCoroutine(PulseBool(IsHowling, howlDuration));
        PlaySound(howlClip);
    }

    public void Die()
    {
        _isDead = true;
        _isMoving = false;
        animator.SetBool(IsWalking, false);
        animator.SetBool(IsRunning, false);
        animator.SetBool(IsAttacking, false);
        animator.SetBool(IsHowling, false);
        animator.SetBool(IsDead, true);
    }

    public void Revive()
    {
        _isDead = false;
        animator.SetBool(IsDead, false);
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
