using UnityEngine;

/// <summary>
/// Pet_Dog_Latte_01_AnimCtrl.controller 기준 강아지 애니메이션 제어.
/// Parameters: bWalk(bool), tDefault/tJump/tSit1/tSit2/tUnique1~5(trigger)
/// </summary>
[RequireComponent(typeof(Animator))]
public class DogAnimationController : MonoBehaviour
{
    private static readonly int BWalk = Animator.StringToHash("bWalk");
    private static readonly int TDefault = Animator.StringToHash("tDefault");
    private static readonly int TJump = Animator.StringToHash("tJump");
    private static readonly int TSit1 = Animator.StringToHash("tSit1");
    private static readonly int TSit2 = Animator.StringToHash("tSit2");
    private static readonly int TUnique1 = Animator.StringToHash("tUnique1");
    private static readonly int TUnique2 = Animator.StringToHash("tUnique2");
    private static readonly int TUnique3 = Animator.StringToHash("tUnique3");
    private static readonly int TUnique4 = Animator.StringToHash("tUnique4");
    private static readonly int TUnique5 = Animator.StringToHash("tUnique5");

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

    private float _randomIdleTimer;
    private bool _isWalking;

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        ResetRandomIdleTimer();
    }

    private void Update()
    {
        if (!enableRandomIdle || _isWalking) return;

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
        _isWalking = walking;
        animator.SetBool(BWalk, walking);
    }

    // ---- 액션 트리거 ----

    public void Jump() => animator.SetTrigger(TJump);

    public void Sit1() => animator.SetTrigger(TSit1);

    public void Sit2() => animator.SetTrigger(TSit2);

    public void Bark()
    {
        animator.SetTrigger(TUnique1);
        PlaySound(barkSingleClip);
    }

    public void BarkRepeat()
    {
        animator.SetTrigger(TUnique2);
        PlaySound(barkingClip);
    }

    public void Whine()
    {
        animator.SetTrigger(TUnique3);
        PlaySound(whiningClip);
    }

    public void Unique4() => animator.SetTrigger(TUnique4);

    public void Unique5() => animator.SetTrigger(TUnique5);

    public void ReturnToDefault() => animator.SetTrigger(TDefault);

    // ---- 랜덤 유휴 동작 ----

    private void PlayRandomIdle()
    {
        int pick = Random.Range(0, 4);
        switch (pick)
        {
            case 0: Sit1(); break;
            case 1: Sit2(); break;
            case 2: Unique4(); break;
            case 3: Unique5(); break;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
