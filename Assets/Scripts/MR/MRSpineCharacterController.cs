using System.Collections.Generic;
using UnityEngine;
using Spine;
using Spine.Unity;
using Random = UnityEngine.Random;

public class MRSpineCharacterController : MonoBehaviour
{
    // ================================
    // Skin Variants (Spine + Voice overrides)
    // ================================
    [System.Serializable]
    public class SkinVariant
    {
        public string id = "Base";

        [Header("Spine (required for this skin)")]
        public SkeletonDataAsset skeletonDataAsset;

        [Header("Voice Overrides (End states only)")]
        public List<AudioClip> ballEndVoicesOverride = new();
        public List<AudioClip> patEndVoicesOverride = new();
        public List<AudioClip> smashEndVoicesOverride = new();
    }

    [Header("Skins")]
    [SerializeField] private List<SkinVariant> skins = new();
    [SerializeField] private string initialSkinId = "Base";

    // 내부 상태
    private int _currentSkinIndex = -1;
    private SkinVariant _currentSkin;

    // ===== 등록 리스트(최적화) =====
    public static readonly List<MRSpineCharacterController> Instances = new();

    private void OnEnable()
    {
        if (!Instances.Contains(this)) Instances.Add(this);

        if (skeletonAnimationStanding != null)
            skeletonAnimationStanding.UpdateLocal += ApplyBoneOverrides;

        ApplyCharacterSize(PlayerPrefs.GetFloat("CharacterSize", 1f));
    }

    private void OnDisable()
    {
        Instances.Remove(this);

        if (skeletonAnimationStanding != null)
            skeletonAnimationStanding.UpdateLocal -= ApplyBoneOverrides;
    }

    // ===== 상태 =====
    public enum State
    {
        Idle,
        Ball, BallEnd,
        PrePatSmash, Pat, PatEnd,
        Smash, SmashEnd,
        PreTickle, Tickle1, Tickle2, TickleEnd,
        Eat
    }

    [Header("State")]
    [SerializeField] private State currentState = State.Idle;
    public State CurrentState => currentState;
    private float _stateElapsed;

    // ===== Hierarchy =====
    [Header("Hierarchy (Character root 기준)")]
    [SerializeField] private Transform characterRoot;
    [SerializeField] private Transform patRoot;

    // ===== Spine =====
    [Header("Spine")]
    [SerializeField] private SkeletonAnimation skeletonAnimationStanding;
    [SerializeField] private string ballBoneName = "Character_Ball_Move";
    [SerializeField] private string patBoneName = "Character_Pat";

    private Vector2 _initialBallLocal;
    private Vector2 _initialPatLocal;

    private Vector2 _dynamicBallTouchOffset;
    private float _autoBallOffsetAngle = 0f;

    // ===== Tags =====
    [Header("Collider Tags")]
    [SerializeField] private string ballTag = "Ball";
    [SerializeField] private string patTag = "Pat";
    [SerializeField] private string tickleTag = "Tickle";

    // ===== Audio =====
    [Header("Audio Sources")]
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Idle Voice")]
    [SerializeField] private List<AudioClip> idleVoices = new();

    [Header("Voices")]
    [SerializeField] private List<AudioClip> ballEndVoices = new();
    [SerializeField] private List<AudioClip> patEndVoices = new();
    [SerializeField] private List<AudioClip> smashVoices = new();
    [SerializeField] private List<AudioClip> smashEndVoices = new();
    [SerializeField] private List<AudioClip> tickle1Voices = new();
    [SerializeField] private List<AudioClip> tickle2Voices = new();
    [SerializeField] private List<AudioClip> eatVoices = new();

    private AudioClip _lastPlayedClip;

    [Header("SFX")]
    [SerializeField] private AudioClip ballSfx;
    [SerializeField] private AudioClip ballEndSfx;
    [SerializeField] private AudioClip patSfx;
    [SerializeField] private AudioClip patEndSfx;
    [SerializeField] private AudioClip tickleSfx;
    [SerializeField] private AudioClip smash1Sfx;
    [SerializeField] private AudioClip smash2Sfx;

    // ===== Animations =====
    [Header("Animations (Spine)")]
    [SerializeField] private List<string> idleAnimations = new();
    [SerializeField] private List<string> eatAnims = new();
    [SerializeField] private string ballAnim = "Touch_Idle";
    [SerializeField] private string ballEndAnim = "Touch_End";
    [SerializeField] private string patAnim = "Pat_Idle";
    [SerializeField] private string patEndAnim = "Pat_End";
    [SerializeField] private string smashAnim = "Smash_End_1";
    [SerializeField] private string smashEndAnim = "Smash_End_2";
    [SerializeField] private string tickle1Anim = "Tickle_Idle_1";
    [SerializeField] private string tickle2Anim = "Tickle_Idle_2";
    [SerializeField] private string tickleEndAnim = "Tickle_End";
    private string _lastPlayedIdleAnim;
    private string _lastPlayedEatAnim;

    // ===== Timings =====
    [Header("Timings")]
    private float idleRerollDelayAfterVoice = 1f;
    private float idleRerollInterval = 5f;
    private float idleRerollChance = 1f;

    [SerializeField] private float prePatHoldSeconds = 0.2f;
    [SerializeField] private float smashToSmashEndSeconds = 1f;
    [SerializeField] private float gestureExitGraceSeconds = 0.5f;
    [SerializeField] private float tickleEndStateSeconds = 4f;
    [SerializeField] private float voiceExtraAfterFinish = 1f;

    // ===== Ball / Pat movement =====
    [Header("Ball Movement (Local XY)")]
    [SerializeField] private Vector2 ballOffset = new Vector2(-50f, 0f);
    [SerializeField] private float ballStretchLimit = 0.5f;
    [SerializeField] private float ballRotationAngle = 0f;

    [Header("Pat Movement (Local XY)")]
    [SerializeField] private float patMoveLimit = 0.6f;

    // ===== BallEnd Elastic Return =====
    [Header("BallEnd Elastic Return")]
    [SerializeField] private bool enableBallEndElastic = true;
    [SerializeField] private float ballEndElasticDuration = 1f;
    [SerializeField] private float ballEndOmega = (21f / 2f) * Mathf.PI;
    [SerializeField] private float ballEndDamping = 17f;

    private bool _ballElasticActive;
    private float _ballElasticStartTime;
    private Vector2 _ballElasticOffset;

    // ===== Tickle =====
    [Header("Tickle")]
    [SerializeField] private float tickleTouchThreshold = 0.15f;
    [SerializeField] private float tickleGestureRatio = 0.6f;
    private float _tickleDistanceAccum;
    private bool _tickleHasLast;
    private Vector2 _tickleLastLocal;

    // ===== Smash =====
    [Header("Smash (Gesture)")]
    [SerializeField] private float smashWindowSeconds = 0.1f;
    [SerializeField] private float smashMinDownLocalY = 0.02f;
    [SerializeField] private float smashRearmUpDelta = 0.01f;

    private readonly Queue<(float t, float y)> _fistYHistory = new();
    private float _lastSmashTriggerY = float.PositiveInfinity;
    [SerializeField] private int _smashHitCount;

    // ===== Interaction session lock =====
    private bool _interactionLocked;

    public bool IsInteractionLocked => _interactionLocked;

    public bool IsAvailableForInteraction()
    {
        if (_interactionLocked) return false;

        switch (currentState)
        {
            case State.Idle:
            case State.BallEnd:
            case State.PatEnd:
            case State.TickleEnd:
            case State.SmashEnd:
                return true;
            default:
                return false;
        }
    }

    // ===== Gesture exit timers =====
    private float _patMissTime;
    private float _tickleMissTime;

    // ===== "최소 유지(음성+1초)"를 위한 지연 종료 =====
    private bool _pendingExit;
    private State _pendingExitState;
    private float _pendingExitAtTime;

    // ===== Idle 재롤 타이밍 =====
    private float _idleNextRerollTime;

    private Camera _cam;

    [Header("Effects")]
    [SerializeField] private CharacterEffectSpawnerPlane effectSpawner;

    // ===== Bone override =====
    private bool _overrideBall;
    private Vector2 _ballOverrideLocal;

    private bool _overridePat;
    private Vector2 _patOverrideLocal;

    private void Awake()
    {
        if (characterRoot == null) characterRoot = transform;
        _cam = Camera.main;

        if (voiceSource == null) voiceSource = GetComponent<AudioSource>();
        if (sfxSource == null) sfxSource = voiceSource;

        ApplyCharacterSize(PlayerPrefs.GetFloat("CharacterSize", 1f));
        ApplyInitialSkinIfPossible();
        CacheInitialBonePositions();

        if (skeletonAnimationStanding != null)
        {
            skeletonAnimationStanding.UpdateLocal -= ApplyBoneOverrides;
            skeletonAnimationStanding.UpdateLocal += ApplyBoneOverrides;
        }

        EnterState(State.Idle, force: true);
    }

    private void Start()
    {
        // 앱 시작 시 내 앞 바닥(Y=0)으로 자동 이동
        ResetPositionToCamera();
    }

    private void Update()
    {
        _stateElapsed += Time.deltaTime;

        if (_pendingExit && Time.time >= _pendingExitAtTime)
        {
            _pendingExit = false;
            EnterState(_pendingExitState);
        }

        if (currentState == State.BallEnd)
            UpdateBallEndElastic();

        UpdateAutoTransitions();
    }

    // =========================================================
    // MR Utilities (Reset Position)
    // =========================================================
    public void ResetPositionToCamera()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // 카메라 정면 수평 방향벡터 계산
        Vector3 forward = _cam.transform.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < 0.001f) forward = _cam.transform.up;
        forward.Normalize();

        // 70cm 앞, 바닥(Y=0)으로 위치 설정
        Vector3 defaultPos = _cam.transform.position + forward * 0.7f;
        defaultPos.y = 0f;

        Transform targetTransform = (characterRoot != null && characterRoot.parent != null) ? characterRoot.parent : transform;
        targetTransform.position = defaultPos;

        // 캐릭터가 카메라를 바라보도록 회전
        Vector3 lookPos = _cam.transform.position;
        lookPos.y = targetTransform.position.y;
        targetTransform.LookAt(lookPos);
    }

    // =========================================================
    // Router API: 3D 상호작용 (MR용 - 월드 좌표 직접 수신)
    // =========================================================
    public void ForceIdleReroll()
    {
        if (_interactionLocked) return;
        EnterState(State.Idle, force: true);
    }

    /// <summary>
    /// 손이 캐릭터 콜라이더에 처음 닿았을 때 호출
    /// </summary>
    public void BeginInteraction(Vector3 worldPoint, string colliderTag)
    {
        _interactionLocked = true;

        if (colliderTag == ballTag)
        {
            EnterState(State.Ball);
            SetupBallOffset(worldPoint);
        }
        else if (colliderTag == patTag)
        {
            EnterState(State.PrePatSmash);
        }
        else if (colliderTag == tickleTag)
        {
            EnterState(State.PreTickle);
        }
    }

    /// <summary>
    /// 손이 계속 콜라이더 근처에서 움직이고 있을 때 매 프레임 호출
    /// </summary>
    public void UpdateInteraction(Vector3 worldPoint)
    {
        if (!_interactionLocked) return;

        switch (currentState)
        {
            case State.Ball:
                UpdateBallByWorld(worldPoint);
                break;

            case State.PrePatSmash:
                if (_stateElapsed >= prePatHoldSeconds)
                    EnterState(State.Pat);
                break;

            case State.Pat:
                UpdatePatByWorld(worldPoint);
                break;

            case State.PreTickle:
                // PreTickle에서는 움직임이 감지되면 Tickle1로 전환
                UpdateTickleByWorld(worldPoint);
                if (_tickleDistanceAccum > 0.001f)
                {
                    EnterState(State.Tickle1);
                    _tickleDistanceAccum = 0f;
                    _tickleHasLast = false;
                    UpdateTickleByWorld(worldPoint);
                }
                break;

            case State.Tickle1:
            case State.Tickle2:
                UpdateTickleByWorld(worldPoint);
                break;
        }
    }

    /// <summary>
    /// 손이 콜라이더에서 떨어졌을 때 호출
    /// </summary>
    public void EndInteraction()
    {
        if (!_interactionLocked) return;
        _interactionLocked = false;

        switch (currentState)
        {
            case State.Ball:
                EnterState(State.BallEnd);
                break;

            case State.Pat:
                EnterState(State.PatEnd);
                break;

            case State.PrePatSmash:
                EnterState(State.Smash);
                break;

            case State.PreTickle:
                ForceIdleReroll();
                break;

            case State.Tickle1:
            case State.Tickle2:
                RequestTickleEndRespectingVoiceMinHold();
                break;
        }
    }

    // =========================================================
    // Router API: Smash (주먹으로 내리치기 — 속도 기반)
    // =========================================================
    /// <summary>
    /// 주먹이 Pat 콜라이더 근처에서 빠르게 하강할 때 매 프레임 호출.
    /// 속도 이력을 기반으로 Smash 발동 여부를 판정합니다.
    /// </summary>
    public void ProcessFistFrame(Vector3 fistWorldPoint)
    {
        float localY = WorldToLocalXY(fistWorldPoint).y;
        PushFistHistory(localY);

        if (CanTriggerSmashAgain(localY) && IsFistMovingDown())
        {
            EnterState(State.Smash, force: true);
            _fistYHistory.Clear();
            _lastSmashTriggerY = localY;
        }
    }

    public void ClearFistHistory()
    {
        _fistYHistory.Clear();
        _lastSmashTriggerY = float.PositiveInfinity;
    }

    // =========================================================
    // Router API: Pat/Tickle 미스 타이머 (손이 잠시 벗어남)
    // =========================================================
    public void AccumulatePatMiss(float dt)
    {
        if (currentState == State.Pat)
        {
            _patMissTime += dt;
            if (_patMissTime >= gestureExitGraceSeconds)
                EnterState(State.PatEnd);
        }
    }

    public void ResetPatMiss() => _patMissTime = 0f;

    public void AccumulateTickleMiss(float dt)
    {
        if (currentState == State.Tickle1 || currentState == State.Tickle2)
        {
            _tickleMissTime += dt;
            if (_tickleMissTime >= gestureExitGraceSeconds)
                RequestTickleEndRespectingVoiceMinHold();
        }
    }

    public void ResetTickleMiss() => _tickleMissTime = 0f;

    // =========================================================
    // Skin API (외부 메뉴용)
    // =========================================================
    public List<SkinVariant> GetSkins() => skins;
    public int GetCurrentSkinIndex() => _currentSkinIndex;

    public void SetSkinByIndex(int index)
    {
        ApplySkinByIndex(index, isInitial: false);
    }

    public void NextSkin()
    {
        if (skins == null || skins.Count == 0) return;
        int next = (_currentSkinIndex < 0) ? 0 : (_currentSkinIndex + 1) % skins.Count;
        ApplySkinByIndex(next, isInitial: false);
    }

    // =========================================================
    // Skin Apply
    // =========================================================
    private void ApplyInitialSkinIfPossible()
    {
        if (skins == null || skins.Count == 0) return;
        int idx = FindSkinIndexById(initialSkinId);
        if (idx < 0) idx = 0;
        ApplySkinByIndex(idx, isInitial: true);
    }

    private int FindSkinIndexById(string id)
    {
        if (skins == null) return -1;
        for (int i = 0; i < skins.Count; i++)
        {
            if (skins[i] != null && skins[i].id == id) return i;
        }
        return -1;
    }

    private void ApplySkinByIndex(int index, bool isInitial)
    {
        if (skins == null || index < 0 || index >= skins.Count) return;
        var skin = skins[index];
        if (skin == null || skin.skeletonDataAsset == null) return;

        _currentSkinIndex = index;
        _currentSkin = skin;

        if (skeletonAnimationStanding != null)
        {
            skeletonAnimationStanding.skeletonDataAsset = skin.skeletonDataAsset;
            skeletonAnimationStanding.Initialize(true);
            skeletonAnimationStanding.UpdateLocal -= ApplyBoneOverrides;
            skeletonAnimationStanding.UpdateLocal += ApplyBoneOverrides;
        }

        CacheInitialBonePositions();

        _ballElasticActive = false;
        _pendingExit = false;

        if (isInitial)
        {
            RefreshAnimationForCurrentState();
        }
        else
        {
            State savedState = currentState;
            // 진행 중인 상호작용 상태는 End 상태로 전환
            if (savedState == State.PreTickle || savedState == State.Tickle1 || savedState == State.Tickle2)
                savedState = State.TickleEnd;
            else if (savedState == State.Smash)
                savedState = State.SmashEnd;
            else if (savedState == State.Pat || savedState == State.PrePatSmash)
                savedState = State.PatEnd;
            else if (savedState == State.Ball)
                savedState = State.BallEnd;

            EnterState(savedState, force: true);
        }
    }

    private List<AudioClip> GetBallEndVoiceList()
    {
        if (_currentSkin != null && _currentSkin.ballEndVoicesOverride != null && _currentSkin.ballEndVoicesOverride.Count > 0)
            return _currentSkin.ballEndVoicesOverride;
        return ballEndVoices;
    }

    private List<AudioClip> GetPatEndVoiceList()
    {
        if (_currentSkin != null && _currentSkin.patEndVoicesOverride != null && _currentSkin.patEndVoicesOverride.Count > 0)
            return _currentSkin.patEndVoicesOverride;
        return patEndVoices;
    }

    private List<AudioClip> GetSmashEndVoiceList()
    {
        if (_currentSkin != null && _currentSkin.smashEndVoicesOverride != null && _currentSkin.smashEndVoicesOverride.Count > 0)
            return _currentSkin.smashEndVoicesOverride;
        return smashEndVoices;
    }

    private void RefreshAnimationForCurrentState()
    {
        switch (currentState)
        {
            case State.Idle: SetRandomIdleAnimation(); break;
            case State.Ball: SetSpineAnimation(ballAnim, true); break;
            case State.BallEnd: SetSpineAnimation(ballEndAnim, true); break;
            case State.Pat: SetSpineAnimation(patAnim, true); break;
            case State.PatEnd: SetSpineAnimation(patEndAnim, true); break;
            case State.Smash: SetSpineAnimation(smashAnim, false); break;
            case State.SmashEnd: SetSpineAnimation(smashEndAnim, true); break;
            case State.Tickle1: SetSpineAnimation(tickle1Anim, true); break;
            case State.Tickle2: SetSpineAnimation(tickle2Anim, true); break;
            case State.TickleEnd: SetSpineAnimation(tickleEndAnim, true); break;
            case State.Eat: SetRandomEatAnimation(); break;
        }
    }

    // =========================================================
    // State Machine
    // =========================================================
    private void EnterState(State next, bool force = false)
    {
        if (currentState == State.Pat && next != State.Pat)
        {
            if (effectSpawner != null) effectSpawner.SetPatActive(false);
        }

        if (!force && currentState == next) return;

        // [중요 수정]: 캐릭터가 대기나 종료(End) 상태로 진입하면 손 인터랙션 잠금을 해제합니다.
        // 이것이 없으면 이전 손이 떠나도 영원히 잠겨서 다음 동작이 진행되지 않았습니다.
        if (next == State.Idle || next == State.BallEnd || next == State.PatEnd || next == State.TickleEnd || next == State.SmashEnd)
        {
            _interactionLocked = false;
        }

        if (next == State.BallEnd)
        {
            StartBallEndElasticFromCurrent();
            ResetBones(resetBall: false, resetPat: true);
        }
        else
        {
            _ballElasticActive = false;
            ResetBones(resetBall: true, resetPat: true);
        }

        currentState = next;
        _stateElapsed = 0f;

        _pendingExit = false;
        _patMissTime = 0f;
        _tickleMissTime = 0f;

        if (currentState != State.Smash && currentState != State.PrePatSmash)
            _smashHitCount = 0;

        switch (currentState)
        {
            case State.Idle:
                PlayRandomIdle();
                ScheduleNextIdleReroll();
                break;

            case State.Ball:
                SetSpineAnimation(ballAnim, loop: true);
                PlaySfx(ballSfx);
                break;

            case State.BallEnd:
                SetSpineAnimation(ballEndAnim, loop: true);
                PlaySfx(ballEndSfx);
                PlayRandomVoice(GetBallEndVoiceList());
                break;

            case State.PrePatSmash:
            case State.PreTickle:
                break;

            case State.Pat:
                SetSpineAnimation(patAnim, loop: true);
                PlaySfx(patSfx);
                if (effectSpawner != null) effectSpawner.SetPatActive(true);
                break;

            case State.PatEnd:
                SetSpineAnimation(patEndAnim, loop: true);
                PlaySfx(patEndSfx);
                PlayRandomVoice(GetPatEndVoiceList());
                break;

            case State.Smash:
                SetSpineAnimation(smashAnim, loop: false);
                _smashHitCount++;
                if (effectSpawner != null) effectSpawner.SpawnSmashStars(_smashHitCount);
                var sfx = (_smashHitCount < 10) ? smash1Sfx : smash2Sfx;
                PlaySfx(sfx);
                PlayRandomVoice(smashVoices);
                break;

            case State.Eat:
                SetRandomEatAnimation();
                PlayRandomVoice(eatVoices);
                break;

            case State.SmashEnd:
                _smashHitCount = 0;
                SetSpineAnimation(smashEndAnim, loop: true);
                PlayRandomVoice(GetSmashEndVoiceList());
                break;

            case State.Tickle1:
                _tickleDistanceAccum = 0f;
                _tickleHasLast = false;
                SetSpineAnimation(tickle1Anim, loop: true);
                PlaySfx(tickleSfx);
                PlayRandomVoice(tickle1Voices);
                break;

            case State.Tickle2:
                SetSpineAnimation(tickle2Anim, loop: true);
                PlayRandomVoice(tickle2Voices);
                break;

            case State.TickleEnd:
                SetSpineAnimation(tickleEndAnim, loop: true);
                break;
        }
    }

    private void UpdateAutoTransitions()
    {
        switch (currentState)
        {
            case State.Idle:
                UpdateIdleReroll();
                break;

            case State.BallEnd:
                if (HasVoiceFinishedPlusExtra()) EnterState(State.Idle);
                break;

            case State.PatEnd:
                if (HasVoiceFinishedPlusExtra()) EnterState(State.Idle);
                break;

            case State.Smash:
                if (_stateElapsed >= smashToSmashEndSeconds) EnterState(State.SmashEnd);
                break;

            case State.SmashEnd:
                if (HasVoiceFinishedPlusExtra()) EnterState(State.Idle);
                break;

            case State.Eat:
                if (HasVoiceFinishedPlusExtra()) EnterState(State.Idle);
                break;

            case State.TickleEnd:
                if (_stateElapsed >= tickleEndStateSeconds) EnterState(State.Idle);
                break;
        }
    }

    // =========================================================
    // BallEnd Elastic
    // =========================================================
    private void StartBallEndElasticFromCurrent()
    {
        if (!enableBallEndElastic || skeletonAnimationStanding == null)
        {
            _ballElasticActive = false;
            return;
        }

        Vector2 releaseLocal = GetBallBoneLocal();
        _ballElasticOffset = releaseLocal - _initialBallLocal;
        _ballElasticStartTime = Time.time;
        _ballElasticActive = true;

        _overrideBall = true;
        _ballOverrideLocal = releaseLocal;
    }

    private void UpdateBallEndElastic()
    {
        if (!_ballElasticActive || !enableBallEndElastic) return;

        float t = Time.time - _ballElasticStartTime;

        if (t >= ballEndElasticDuration)
        {
            _ballElasticActive = false;
            SetBallBoneLocal(_initialBallLocal);
            return;
        }

        float osc = Mathf.Cos(ballEndOmega * t);
        float damp = Mathf.Pow(2f, -ballEndDamping * t);

        Vector2 target = _initialBallLocal + _ballElasticOffset * osc * damp;

        Vector2 anchor = GetBallAnchorLocal();
        if (Vector2.Distance(target, anchor) > ballStretchLimit)
            target = (target - anchor).normalized * ballStretchLimit + anchor;

        SetBallBoneLocal(target);
    }

    // =========================================================
    // Idle logic
    // =========================================================
    private void PlayRandomIdle()
    {
        SetRandomIdleAnimation();
        PlayRandomVoice(idleVoices);
    }

    private void ScheduleNextIdleReroll()
    {
        _idleNextRerollTime = Time.time + CurrentVoiceLengthOrZero() + PlayerPrefs.GetFloat("IdleRerollDelayAfterVoice", idleRerollDelayAfterVoice);
    }

    private void UpdateIdleReroll()
    {
        if (Time.time < _idleNextRerollTime) return;

        if (Random.value < PlayerPrefs.GetFloat("IdleRerollChance", idleRerollChance))
        {
            EnterState(State.Idle, force: true);
        }
        else
        {
            _idleNextRerollTime = Time.time + PlayerPrefs.GetFloat("IdleRerollInterval", idleRerollInterval) + PlayerPrefs.GetFloat("IdleRerollDelayAfterVoice", idleRerollDelayAfterVoice);
        }
    }

    // =========================================================
    // Ball / Pat update (3D 월드 좌표 기반)
    // =========================================================
    private void SetupBallOffset(Vector3 worldPoint)
    {
        Vector2 mouseParentLocal = GetSpineBoneLocalPosition(worldPoint, ballBoneName);
        _dynamicBallTouchOffset = GetBallBoneLocal() - mouseParentLocal;
    }

    private void UpdateBallByWorld(Vector3 worldPoint)
    {
        Vector2 mouseParentLocal = GetSpineBoneLocalPosition(worldPoint, ballBoneName);
        Vector2 targetLocal = mouseParentLocal + _dynamicBallTouchOffset;

        Vector2 anchor = GetBallAnchorLocal();

        if (Vector2.Distance(targetLocal, anchor) > ballStretchLimit)
        {
            targetLocal = anchor + (targetLocal - anchor).normalized * ballStretchLimit;
        }

        if (ballRotationAngle != 0f)
            targetLocal = RotateVector2(targetLocal, ballRotationAngle);

        _ballElasticActive = false;
        SetBallBoneLocal(targetLocal);
    }

    private void UpdatePatByWorld(Vector3 worldPoint)
    {
        Vector2 local = patRoot.InverseTransformPoint(worldPoint);

        if (Vector2.Distance(local, _initialPatLocal) > patMoveLimit)
            local = (local - _initialPatLocal).normalized * patMoveLimit + _initialPatLocal;

        SetPatBoneLocal(local);
    }

    // =========================================================
    // Tickle (3D 월드 좌표 기반)
    // =========================================================
    private void UpdateTickleByWorld(Vector3 worldPoint)
    {
        Vector2 local = WorldToLocalXY(worldPoint);

        if (_tickleHasLast)
        {
            float dist = Vector2.Distance(local, _tickleLastLocal);
            if (dist < 0.1f) // 갑작스럽게 손가락 부위가 전환될 때의 튀는 값(점프) 방지
                _tickleDistanceAccum += dist;
        }

        _tickleLastLocal = local;
        _tickleHasLast = true;

        if (currentState == State.Tickle1 && _tickleDistanceAccum >= tickleTouchThreshold)
        {
            EnterState(State.Tickle2);
        }
    }

    private void RequestTickleEndRespectingVoiceMinHold()
    {
        float minHold = CurrentVoiceLengthOrZero() + voiceExtraAfterFinish;

        if (_stateElapsed < minHold)
        {
            _pendingExit = true;
            _pendingExitState = State.TickleEnd;
            _pendingExitAtTime = Time.time + (minHold - _stateElapsed);
        }
        else
        {
            EnterState(State.TickleEnd);
        }
    }

    // =========================================================
    // Smash
    // =========================================================
    private void PushFistHistory(float localY)
    {
        float now = Time.time;
        _fistYHistory.Enqueue((now, localY));

        while (_fistYHistory.Count > 0 && now - _fistYHistory.Peek().t > smashWindowSeconds)
            _fistYHistory.Dequeue();
    }

    private bool IsFistMovingDown()
    {
        if (_fistYHistory.Count < 2) return false;

        var oldest = _fistYHistory.Peek();
        (float t, float y) newest = oldest;
        foreach (var v in _fistYHistory) newest = v;

        float delta = newest.y - oldest.y;
        return delta <= -Mathf.Abs(smashMinDownLocalY);
    }

    private bool CanTriggerSmashAgain(float currentLocalY)
    {
        if (float.IsPositiveInfinity(_lastSmashTriggerY)) return true;
        return currentLocalY >= _lastSmashTriggerY + smashRearmUpDelta;
    }

    // =========================================================
    // Helpers: Bone override / Spine / Audio / Coords
    // =========================================================
    private void ApplyBoneOverrides(ISkeletonAnimation anim)
    {
        if (skeletonAnimationStanding == null) return;
        var skel = skeletonAnimationStanding.Skeleton;

        if (_overrideBall)
        {
            var b = skel.FindBone(ballBoneName);
            if (b != null)
            {
                b.X = _ballOverrideLocal.x;
                b.Y = _ballOverrideLocal.y;
            }
        }

        if (_overridePat)
        {
            var p = skel.FindBone(patBoneName);
            if (p != null)
            {
                p.X = _patOverrideLocal.x;
                p.Y = _patOverrideLocal.y;
            }
        }
    }

    private void CacheInitialBonePositions()
    {
        if (skeletonAnimationStanding == null) return;

        var skel = skeletonAnimationStanding.Skeleton;

        var ballBone = skel.FindBone(ballBoneName);
        var patBone = skel.FindBone(patBoneName);

        if (ballBone != null)
        {
            _initialBallLocal = new Vector2(ballBone.Data.X, ballBone.Data.Y);

            float absX = Mathf.Abs(_initialBallLocal.x);
            float absY = Mathf.Abs(_initialBallLocal.y);

            if (absX > absY)
            {
                if (_initialBallLocal.x < 0) _autoBallOffsetAngle = 0f;
                else _autoBallOffsetAngle = 180f;
            }
            else
            {
                if (_initialBallLocal.y > 0) _autoBallOffsetAngle = 270f;
                else _autoBallOffsetAngle = 90f;
            }
        }
        else _initialBallLocal = Vector2.zero;

        if (patBone != null)
        {
            _initialPatLocal = new Vector2(patBone.Data.X, patBone.Data.Y);
        }
        else _initialPatLocal = Vector2.zero;

        _overrideBall = true;
        _ballOverrideLocal = _initialBallLocal;

        _overridePat = true;
        _patOverrideLocal = _initialPatLocal;
    }

    private void ResetBones(bool resetBall, bool resetPat)
    {
        if (resetBall) SetBallBoneLocal(_initialBallLocal);
        if (resetPat) SetPatBoneLocal(_initialPatLocal);
    }

    private Vector2 GetBallBoneLocal()
    {
        if (skeletonAnimationStanding == null) return Vector2.zero;
        var bone = skeletonAnimationStanding.Skeleton.FindBone(ballBoneName);
        if (bone == null) return Vector2.zero;
        return new Vector2(bone.X, bone.Y);
    }

    private void SetBallBoneLocal(Vector2 localXY)
    {
        _overrideBall = true;
        _ballOverrideLocal = localXY;

        if (skeletonAnimationStanding == null) return;
        var bone = skeletonAnimationStanding.Skeleton.FindBone(ballBoneName);
        if (bone == null) return;
        bone.X = localXY.x;
        bone.Y = localXY.y;
    }

    private void SetPatBoneLocal(Vector2 localXY)
    {
        _overridePat = true;
        _patOverrideLocal = localXY;

        if (skeletonAnimationStanding == null) return;
        var bone = skeletonAnimationStanding.Skeleton.FindBone(patBoneName);
        if (bone == null) return;
        bone.X = localXY.x;
        bone.Y = localXY.y;
    }

    private void SetSpineAnimation(string animName, bool loop)
    {
        if (skeletonAnimationStanding == null) return;
        if (string.IsNullOrEmpty(animName)) return;

        var state = skeletonAnimationStanding.AnimationState;
        if (state == null) return;

        state.SetAnimation(0, animName, loop);
    }

    private void SetRandomIdleAnimation()
    {
        if (skeletonAnimationStanding == null) return;
        if (idleAnimations == null || idleAnimations.Count == 0) return;

        string animName;

        if (idleAnimations.Count == 1)
        {
            animName = idleAnimations[0];
        }
        else
        {
            int idx = Random.Range(0, idleAnimations.Count);
            animName = idleAnimations[idx];

            if (animName == _lastPlayedIdleAnim)
            {
                idx = (idx + 1) % idleAnimations.Count;
                animName = idleAnimations[idx];
            }
        }

        if (string.IsNullOrEmpty(animName)) return;

        _lastPlayedIdleAnim = animName;

        if (skeletonAnimationStanding.Skeleton == null) skeletonAnimationStanding.Initialize(false);
        if (skeletonAnimationStanding.Skeleton?.Data?.FindAnimation(animName) == null) return;

        skeletonAnimationStanding.AnimationState.SetAnimation(0, animName, true);
    }

    private void SetRandomEatAnimation()
    {
        if (skeletonAnimationStanding == null) return;
        if (eatAnims == null || eatAnims.Count == 0) return;

        string animName;

        if (eatAnims.Count == 1)
        {
            animName = eatAnims[0];
        }
        else
        {
            int idx = Random.Range(0, eatAnims.Count);
            animName = eatAnims[idx];

            if (animName == _lastPlayedEatAnim)
            {
                idx = (idx + 1) % eatAnims.Count;
                animName = eatAnims[idx];
            }
        }

        if (string.IsNullOrEmpty(animName)) return;

        _lastPlayedEatAnim = animName;

        if (skeletonAnimationStanding.Skeleton == null) skeletonAnimationStanding.Initialize(false);
        if (skeletonAnimationStanding.Skeleton?.Data?.FindAnimation(animName) == null) return;

        skeletonAnimationStanding.AnimationState.SetAnimation(0, animName, true);
    }

    private void PlayRandomVoice(List<AudioClip> list)
    {
        if (voiceSource == null) return;
        if (list == null || list.Count == 0) return;

        AudioClip clipToPlay;

        if (list.Count == 1)
        {
            clipToPlay = list[0];
        }
        else
        {
            int randomIndex = Random.Range(0, list.Count);
            clipToPlay = list[randomIndex];

            if (clipToPlay == _lastPlayedClip)
            {
                randomIndex = (randomIndex + 1) % list.Count;
                clipToPlay = list[randomIndex];
            }
        }

        if (clipToPlay == null) return;

        _lastPlayedClip = clipToPlay;

        voiceSource.loop = false;
        voiceSource.clip = clipToPlay;
        voiceSource.Play();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxSource == null && voiceSource == null) return;

        var src = (sfxSource != null) ? sfxSource : voiceSource;
        src.PlayOneShot(clip);
    }

    private float CurrentVoiceLengthOrZero()
    {
        if (voiceSource == null) return 0f;
        if (voiceSource.clip == null) return 0f;
        return voiceSource.clip.length;
    }

    private bool HasVoiceFinishedPlusExtra()
    {
        float len = CurrentVoiceLengthOrZero();
        return _stateElapsed >= (len + voiceExtraAfterFinish);
    }

    private Vector2 WorldToLocalXY(Vector3 worldPoint)
    {
        Vector3 local = characterRoot.InverseTransformPoint(worldPoint);
        return new Vector2(local.x, local.y);
    }

    public void EnterEat()
    {
        EnterState(State.Eat);
    }

    private Vector2 GetSpineBoneLocalPosition(Vector3 worldPoint, string boneName)
    {
        if (skeletonAnimationStanding == null) return Vector2.zero;
        var skel = skeletonAnimationStanding.Skeleton;
        var bone = skel.FindBone(boneName);
        if (bone == null) return Vector2.zero;

        Vector3 spineRootLocal = skeletonAnimationStanding.transform.InverseTransformPoint(worldPoint);

        if (bone.Parent != null)
        {
            bone.Parent.WorldToLocal(spineRootLocal.x, spineRootLocal.y, out float localX, out float localY);
            return new Vector2(localX, localY);
        }

        return new Vector2(spineRootLocal.x, spineRootLocal.y);
    }

    private Vector2 RotateVector2(Vector2 v, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            v.x * cos - v.y * sin,
            v.x * sin + v.y * cos
        );
    }

    private Vector2 GetBallAnchorLocal()
    {
        Vector2 rotatedOffset = RotateVector2(ballOffset, _autoBallOffsetAngle);
        return _initialBallLocal + rotatedOffset;
    }

    public void ApplyCharacterSize(float size)
    {
        transform.localScale = new Vector3(size, size, size) * 0.02f;
    }
}
