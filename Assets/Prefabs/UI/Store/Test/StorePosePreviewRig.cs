// 원본 복제·개작 (파생 코드 — Store/Test 폴더 규칙):
//  - Assets/Scripts/animationplayermanager.cs
//    (EnsureGraph / ApplyRandomPoseAndFreeze / ReleasePlayer — PlayableGraph 정지 포즈 패턴)
//  - Assets/Prefabs/Assist/InventorySystem/Editor/InventorySystemTools.cs
//    (FrameCamera / StripAppComponents — 렌더러 바운드 카메라 프레이밍 · MonoBehaviour 다중 패스 스트립)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// 미리보기 캡처 서비스: 스트립한 캐릭터 클론의 정지 포즈(PlayableGraph)와 파티클 정지컷
// (ParticleSystem.Simulate)을 전용 레이어(PortraitModel)에서 렌더링해 아이콘 스프라이트로 캡처한다.
// 캐시·키 해석은 StoreManager 소유 — 이 리그는 요청받은 엔트리를 순차 캡처해 콜백으로 넘길 뿐이다.
// 모든 실패 경로에서 onDone(entry, null) 호출을 보장한다 (매니저의 pending 해제 조건).
public class StorePosePreviewRig : MonoBehaviour
{
    [SerializeField, Min(0f)] private float previewLightIntensity = 0.8f;

    private static readonly Vector3 RigIsolationOffset = new Vector3(0f, -10000f, 0f);

    private GameObject rigRoot;
    private GameObject holder;
    private Light rigLight;

    public static StorePosePreviewRig Instance { get; private set; }  // Awake에서 설정, OnDestroy에서 해제 (자동 생성 없음)

    [SerializeField] private GameObject characterPrefab;    // 휴머노이드 캐릭터 프리팹 (에디터 빌더가 주입, 예: arona POC)
    [SerializeField] private int iconSize = 256;            // 캡처 해상도(px)

    private GameObject charInst;        // 스트립된 캐릭터 클론
    private GameObject activeFxHolder;  // 진행 중 이펙트 캡처의 홀더 (비활성 정리 대상 — 잔존 시 다음 캡처를 오염)
    private Animator animator;          // 클론의 Animator (MonoBehaviour가 아니라 스트립에서 살아남음)
    private Renderer[] charRenderers;   // 클론의 전체 렌더러 (이펙트 캡처 시 일괄 on/off)
    private Camera rigCam;              // 리그 전용 카메라 (항상 켜져 RT로 렌더)
    private RenderTexture rt;           // 캡처 대상 RT
    private int portraitLayer = -1;     // PortraitModel 레이어 (없으면 -1 — 레이어 분리 없이 진행)

    private PlayableGraph graph;                    // 정지 포즈용 그래프 (1개 유지, 클립만 교체)
    private AnimationPlayableOutput output;
    private AnimationClipPlayable clipPlayable;
    private bool isGraphCreated;

    private readonly Queue<CaptureRequest> requests = new Queue<CaptureRequest>();  // 순차 캡처 큐
    private Coroutine pump;         // 캡처 펌프 코루틴
    private CaptureRequest currentRequest;  // 펌프가 Dequeue해 진행 중인 요청 (파괴/비활성 시 실패 통지 대상)
    private bool hasCurrentRequest;
    private bool isReady;           // 리그 구성 완료 여부
    private bool disabled;          // 영구 캡처 불가 확정 (characterPrefab 없음 / Animator 없음)

    // 포즈/이펙트 공용 요청 — poseEntry/effectEntry 중 한쪽만 채워진다
    private struct CaptureRequest
    {
        public ItemRuntimeSpritePoseEntry poseEntry;
        public ItemRuntimeSpriteEffectEntry effectEntry;
        public System.Action<ItemRuntimeSpritePoseEntry, Sprite> onPoseDone;
        public System.Action<ItemRuntimeSpriteEffectEntry, Sprite> onEffectDone;
    }

    private sealed class CaptureOperation
    {
        public Sprite sprite;
        public System.Exception error;
    }

    // 영구 캡처 불가 확정 시 true. 리그 준비 전(Start 이전)은 false — 요청은 내부 큐에 보존된다.
    public bool IsDisabled
    {
        get
        {
            return disabled;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Store][StorePosePreviewRig] 인스턴스가 이미 존재합니다. 중복 리그는 무동작합니다.");
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (Instance != this)
        {
            return;
        }

        if (characterPrefab == null)
        {
            // 영구 비활성 — 데모씬은 그대로 동작하고 카드는 NoImage 폴백으로 표시된다
            Debug.LogWarning("[Store][StorePosePreviewRig] characterPrefab이 없어 미리보기 캡처를 비활성화합니다.");
            SetPermanentlyDisabled();
            return;
        }

        BuildRig();

        if (isReady == false)
        {
            // Animator 부재 등 리그 구성 실패 — 영구 비활성 확정
            SetPermanentlyDisabled();
            return;
        }

        // Start 순서상 리그 구성 전에 도착해 큐에 보존된 요청 처리
        if (requests.Count > 0 && pump == null)
        {
            pump = StartCoroutine(CapturePump());
        }
    }

    // 영구 비활성 확정 + 대기 요청 드레인 — 각 요청에 실패를 통지해 매니저의 pending을 풀어준다
    private void SetPermanentlyDisabled()
    {
        disabled = true;
        SetCaptureSessionActive(false);
        DrainRequestsWithFailure();
    }

    // 진행 중/대기 요청 전부에 실패 통지. 리그(씬 소속)가 캡처 도중 죽어도 상시 매니저(DontDestroyOnLoad)의
    // pendingKeys가 풀려야 하므로, 파괴/비활성/영구비활성의 모든 경로가 이 드레인을 거친다.
    private void DrainRequestsWithFailure()
    {
        if (hasCurrentRequest)
        {
            CaptureRequest inflight = currentRequest;
            hasCurrentRequest = false;
            NotifyFailure(inflight);
        }

        while (requests.Count > 0)
        {
            NotifyFailure(requests.Dequeue());
        }
    }

    private void NotifyFailure(CaptureRequest req)
    {
        InvokeCompletion(req, null);
    }

    private void InvokeCompletion(CaptureRequest req, Sprite sprite)
    {
        bool hasReceiver = req.onPoseDone != null || req.onEffectDone != null;
        try
        {
            if (req.onPoseDone != null)
            {
                req.onPoseDone(req.poseEntry, sprite);
            }
            else if (req.onEffectDone != null)
            {
                req.onEffectDone(req.effectEntry, sprite);
            }
        }
        catch (System.Exception exception)
        {
            // 콜백 호출이 시작되는 순간 결과 소유권은 수신자에게 넘어간다. 수신자가 캐시에
            // 저장한 뒤 이벤트 처리에서 예외가 난 경우까지 여기서 파괴하면 캐시가 깨진다.
            Debug.LogException(exception, this);
            return;
        }

        if (hasReceiver == false)
        {
            ReleaseCapturedSprite(sprite);
        }
    }

    // ── 공개 API ──

    // 포즈 캡처 요청: 큐에 넣고 순차 캡처 후 콜백. 실패 시에도 반드시 onDone(entry, null) 호출.
    public void RequestPoseCapture(ItemRuntimeSpritePoseEntry entry, System.Action<ItemRuntimeSpritePoseEntry, Sprite> onDone)
    {
        if (entry == null || disabled)
        {
            InvokeCompletion(new CaptureRequest { poseEntry = entry, onPoseDone = onDone }, null);
            return;
        }

        // 리그 구성 전(Start 실행 순서 미보장)에 도착한 요청도 큐에 보존 — BuildRig 완료 후 펌프가 처리
        requests.Enqueue(new CaptureRequest { poseEntry = entry, onPoseDone = onDone });

        if (isReady && pump == null)
        {
            pump = StartCoroutine(CapturePump());
        }
    }

    // 이펙트 캡처 요청: 파티클을 Simulate로 정지시켜 캡처. 실패 시에도 반드시 onDone(entry, null) 호출.
    public void RequestEffectCapture(ItemRuntimeSpriteEffectEntry entry, System.Action<ItemRuntimeSpriteEffectEntry, Sprite> onDone)
    {
        if (entry == null || disabled)
        {
            InvokeCompletion(new CaptureRequest { effectEntry = entry, onEffectDone = onDone }, null);
            return;
        }

        requests.Enqueue(new CaptureRequest { effectEntry = entry, onEffectDone = onDone });

        if (isReady && pump == null)
        {
            pump = StartCoroutine(CapturePump());
        }
    }

#if UNITY_EDITOR
    // 에디터 빌더 주입용 (StoreTools 데모씬 빌드에서 호출)
    public void EditorSet(GameObject characterPrefab)
    {
        this.characterPrefab = characterPrefab;
    }
#endif

    // ── 리그 구성 ──

    private void BuildRig()
    {
        portraitLayer = LayerMask.NameToLayer("PortraitModel");
        if (portraitLayer < 0)
        {
            // 레이어 분리 실패 — 레이어는 그대로 두고 진행 (메인 카메라에 리그가 비칠 수 있음)
            Debug.LogWarning("[Store][StorePosePreviewRig] 'PortraitModel' 레이어를 찾지 못했습니다. 레이어 분리 없이 진행합니다.");
        }

        // 비활성 홀더 아래 인스턴스화 → 앱 스크립트의 Awake/OnEnable이 한 프레임도 실행되지 않게 차단
        // (animationplayermanager.cs 오프스크린 프로브 주석이 문서화한 사고 패턴 대비)
        rigRoot = new GameObject("PreviewRigRoot");
        rigRoot.transform.SetParent(transform, false);
        rigRoot.transform.localPosition = RigIsolationOffset;

        holder = new GameObject("Holder");
        holder.transform.SetParent(rigRoot.transform, false);
        holder.SetActive(false);

        charInst = Instantiate(characterPrefab, holder.transform);
        charInst.transform.localPosition = Vector3.zero;
        charInst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // 앱 동작/물리만 비활성화하고 렌더러·본·Animator·머티리얼 구조는 원형 보존
        PrepareVisualClone(charInst);

        if (portraitLayer >= 0)
        {
            SetLayerRecursively(holder.transform, portraitLayer);
        }

        holder.SetActive(true);

        charRenderers = charInst.GetComponentsInChildren<Renderer>(true);

        animator = charInst.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError("[Store][StorePosePreviewRig] 캐릭터 인스턴스에 Animator가 없어 미리보기 캡처를 비활성화합니다.");
            return;
        }
        animator.applyRootMotion = false;                       // 클립 루트 이동으로 프레임 이탈 방지
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;  // 오프스크린에서도 평가 보장

        // 리그 전용 카메라 (캡처 세션 중에만 RT로 렌더 — SRP에서 Camera.Render() 호출 금지)
        GameObject camGo = new GameObject("RigCamera");
        camGo.transform.SetParent(rigRoot.transform, false);
        rigCam = camGo.AddComponent<Camera>();
        rigCam.clearFlags = CameraClearFlags.SolidColor;
        rigCam.backgroundColor = new Color(0.11f, 0.12f, 0.15f, 1f);  // 불투명 어두운 배경 (툰셰이더 알파 비의존)
        rigCam.fieldOfView = 40f;
        rigCam.allowHDR = false;
        rigCam.allowMSAA = false;
        rigCam.enabled = false;
        if (portraitLayer >= 0)
        {
            rigCam.cullingMask = 1 << portraitLayer;
        }
        rt = new RenderTexture(iconSize, iconSize, 24, RenderTextureFormat.ARGB32);
        rigCam.targetTexture = rt;

        // 리그 전용 디렉셔널 라이트 (데모씬에는 라이트가 없어 리그가 자체 광원을 가져야 한다)
        GameObject lightGo = new GameObject("RigLight");
        lightGo.transform.SetParent(rigRoot.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        rigLight = lightGo.AddComponent<Light>();
        rigLight.type = LightType.Directional;
        rigLight.intensity = previewLightIntensity;
        rigLight.shadows = LightShadows.None;
        if (portraitLayer >= 0)
        {
            rigLight.cullingMask = 1 << portraitLayer;  // 본편 씬을 이중 조명하지 않도록 리그 레이어만 비춘다
        }

        // 초기 프레이밍 (바인드 포즈 기준 — 포즈 정지 후 매번 재프레이밍)
        FrameCamera(rigCam, charInst);

        isReady = true;
        SetCaptureSessionActive(false);
        Debug.Log("[Store][StorePosePreviewRig] 리그 구성 완료.");
    }

    // 시각 계층은 그대로 두고 앱 동작과 물리 상호작용만 안전하게 비활성화한다.
    private void PrepareVisualClone(GameObject target)
    {
        // 비활성 부모 아래에서 앱 동작만 멈춘다. 컴포넌트를 파괴하지 않아 RequireComponent 체인과
        // SkinnedMeshRenderer의 본/머티리얼 구성을 원본 그대로 보존한다.
        foreach (MonoBehaviour comp in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp != null)
            {
                comp.enabled = false;
            }
        }

        foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        foreach (Collider2D collider in target.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        foreach (Rigidbody body in target.GetComponentsInChildren<Rigidbody>(true))
        {
            if (body != null)
            {
                body.detectCollisions = false;
                body.isKinematic = true;
            }
        }

        foreach (Rigidbody2D body in target.GetComponentsInChildren<Rigidbody2D>(true))
        {
            if (body != null)
            {
                body.simulated = false;
            }
        }

        foreach (AudioSource source in target.GetComponentsInChildren<AudioSource>(true))
        {
            if (source != null)
            {
                source.enabled = false;
            }
        }

        foreach (Camera cloneCamera in target.GetComponentsInChildren<Camera>(true))
        {
            if (cloneCamera != null)
            {
                cloneCamera.enabled = false;
            }
        }

        if (portraitLayer >= 0)
        {
            foreach (Light cloneLight in target.GetComponentsInChildren<Light>(true))
            {
                if (cloneLight != null)
                {
                    cloneLight.cullingMask = 1 << portraitLayer;
                }
            }
        }
    }

    // 레이어 재귀 적용
    private void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
        {
            SetLayerRecursively(t.GetChild(i), layer);
        }
    }

    private void SetCaptureSessionActive(bool active)
    {
        if (holder != null)
        {
            holder.SetActive(active);
        }
        if (rigCam != null)
        {
            rigCam.enabled = active;
        }
        if (rigLight != null)
        {
            rigLight.enabled = active;
        }

        if (isGraphCreated && graph.IsValid())
        {
            if (active)
            {
                graph.Play();
            }
            else
            {
                graph.Stop();
            }
        }
    }

    // ── 정지 포즈 (원본: animationplayermanager.cs EnsureGraph / ApplyRandomPoseAndFreeze) ──

    private void EnsureGraph()
    {
        // 이미 생성된 경우 Play 상태 보장 후 재사용
        if (isGraphCreated && graph.IsValid())
        {
            if (!graph.IsPlaying())
            {
                graph.Play();
            }
            return;
        }

        // 새로 생성 — 그래프가 살아있는 동안 AnimatorController는 자동으로 마스킹된다
        graph = PlayableGraph.Create("StorePosePreviewGraph");
        output = AnimationPlayableOutput.Create(graph, "Output", animator);
        clipPlayable = AnimationClipPlayable.Create(graph, null);
        output.SetSourcePlayable(clipPlayable);
        output.SetWeight(1f);

        graph.Play();
        isGraphCreated = true;
    }

    // 클립을 freezeMin~freezeMax 사이 랜덤 정규화 시점에서 정지시키고 카메라를 재프레이밍
    private bool FreezePose(ItemRuntimeSpritePoseEntry entry)
    {
        if (entry.clip == null)
        {
            Debug.LogWarning($"[Store][StorePosePreviewRig] 포즈 '{entry.key}'의 클립이 null — 캡처를 건너뜁니다.");
            return false;
        }

        EnsureGraph();

        // 기존 clipPlayable 파괴 후 새 클립으로 재생성 (그래프는 유지)
        if (clipPlayable.IsValid())
        {
            clipPlayable.Destroy();
        }
        clipPlayable = AnimationClipPlayable.Create(graph, entry.clip);
        output.SetSourcePlayable(clipPlayable);

        // 20~80% 재생 위치 랜덤 정지 — 권장 순서: speed=0 → time → Evaluate
        float t = Random.Range(Mathf.Clamp01(entry.freezeMin), Mathf.Clamp01(entry.freezeMax)) * entry.clip.length;
        clipPlayable.SetSpeed(0);
        clipPlayable.SetTime(t);
        graph.Evaluate(0f);

        // Evaluate 이후의 포즈 바운드로 프레이밍 (루트 오프셋이 큰 클립 대비)
        FrameCamera(rigCam, charInst);
        return true;
    }

    // ── 카메라 프레이밍 (원본: InventorySystemTools.FrameCamera — 런타임 API만 사용, 그대로 재사용) ──

    private void FrameCamera(Camera cam, GameObject target)
    {
        Renderer[] rs = target.GetComponentsInChildren<Renderer>();
        if (rs == null || rs.Length == 0)
        {
            cam.transform.position = target.transform.position + new Vector3(0f, 1f, -3f);
            cam.transform.rotation = Quaternion.identity;
            return;
        }

        // 전체 바운드 합치기
        bool has = false;
        Bounds b = new Bounds();
        foreach (Renderer r in rs)
        {
            if (r == null)
            {
                continue;
            }

            if (has == false)
            {
                b = r.bounds;
                has = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        // 바운딩 스피어가 화면에 들어오는 거리 계산 (+여백)
        float radius = b.extents.magnitude;
        // 살아있는 파티클이 0개면 바운드가 퇴화해 dist가 0/NaN이 된다 — 하한 클램프로 방지
        radius = Mathf.Max(radius, 0.25f);
        float halfFovRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float dist = radius / Mathf.Sin(halfFovRad);
        dist = dist * 1.15f;

        // 정면(-Z)에서 바라봄
        cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - dist);
        cam.transform.rotation = Quaternion.LookRotation(b.center - cam.transform.position, Vector3.up);
        cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
        cam.farClipPlane = dist + radius * 4f;
    }

    // ── 캡처 펌프 (RT 읽기: FaceTextureChanger.cs / 프레임 끝 대기: ApiAgentFunctionScreenshotActionManager.cs 패턴) ──

    private IEnumerator CapturePump()
    {
        // StartCoroutine은 첫 yield까지 동기 실행된다. FreezePose 실패로 큐가 yield 없이
        // 전부 드레인되면 아래의 `pump = null`이 호출측의 `pump = StartCoroutine(...)` 대입보다
        // 먼저 실행되어, 종료된 코루틴 핸들이 pump에 남는다(펌프 영구 고착). 첫 프레임을
        // 양보해 대입이 항상 본문보다 먼저 완료되게 한다.
        yield return null;

        SetCaptureSessionActive(true);
        bool completedNormally = false;
        try
        {
            while (requests.Count > 0)
            {
                CaptureRequest req = requests.Dequeue();
                // Dequeue 이후 파괴/비활성되면 큐 드레인만으로는 이 요청이 유실된다 — 진행 중 표시로 보호
                currentRequest = req;
                hasCurrentRequest = true;

                CaptureOperation result = new CaptureOperation();
                IEnumerator operation = req.poseEntry != null
                    ? CapturePose(req.poseEntry, result)
                    : CaptureEffect(req.effectEntry, result);

                yield return ExecuteCaptureSafely(operation, result);

                if (result.error != null)
                {
                    Debug.LogError("[Store][StorePosePreviewRig] 캡처 요청 처리 중 예외가 발생했습니다. 다음 요청을 계속 처리합니다.", this);
                    Debug.LogException(result.error, this);
                    ReleaseCapturedSprite(result.sprite);
                    result.sprite = null;

                    if (activeFxHolder != null)
                    {
                        CleanupEffectCapture(activeFxHolder);
                    }
                }

                hasCurrentRequest = false;
                InvokeCompletion(req, result.sprite);
            }

            completedNormally = true;
        }
        finally
        {
            if (completedNormally)
            {
                hasCurrentRequest = false;
            }
            SetCaptureSessionActive(false);
            pump = null;
        }
    }

    private IEnumerator ExecuteCaptureSafely(IEnumerator operation, CaptureOperation result)
    {
        bool finished = false;
        while (finished == false)
        {
            bool hasNext = false;
            object yielded = null;
            try
            {
                hasNext = operation != null && operation.MoveNext();
                if (hasNext)
                {
                    yielded = operation.Current;
                }
                else
                {
                    finished = true;
                }
            }
            catch (System.Exception exception)
            {
                result.error = exception;
                finished = true;
            }

            if (hasNext)
            {
                yield return yielded;
            }
        }
    }

    private IEnumerator CapturePose(ItemRuntimeSpritePoseEntry entry, CaptureOperation result)
    {
        // 직전 이펙트 캡처가 캐릭터 렌더러를 꺼놨을 수 있음 — 캡처 전 복원 보장
        SetCharRenderersEnabled(true);

        if (FreezePose(entry) == false)
        {
            yield break;
        }

        // 포즈 변경 프레임의 스테일 렌더 방지: 한 프레임 넘긴 뒤 프레임 끝에서 RT를 읽는다
        yield return null;
        yield return new WaitForEndOfFrame();

        result.sprite = ReadBackSprite();
    }

    private IEnumerator CaptureEffect(ItemRuntimeSpriteEffectEntry entry, CaptureOperation result)
    {
        if (entry.effectPrefab == null)
        {
            Debug.LogWarning($"[Store][StorePosePreviewRig] 이펙트 '{entry.key}'의 프리팹이 null — 캡처를 건너뜁니다.");
            yield break;
        }

        // 캐릭터를 프레임에서 제거 — 이펙트 단독 아이콘
        SetCharRenderersEnabled(false);

        // 비활성 서브홀더 아래 인스턴스화 → 스트립 완료 전까지 이펙트 스크립트의 Awake/OnEnable 차단
        // (Fx_LoveAura는 MonoBehaviour가 없지만 CFXR 계열 대체 프리팹은 CFXR_Effect가 있을 수 있어 스트립 필수)
        GameObject fxHolder = new GameObject("FxHolder");
        fxHolder.transform.SetParent(rigRoot.transform, false);
        fxHolder.SetActive(false);
        activeFxHolder = fxHolder;  // 캡처 도중 리그가 비활성화되면 OnDisable이 대신 정리한다

        GameObject fxInst = Instantiate(entry.effectPrefab, fxHolder.transform);
        fxInst.transform.localPosition = Vector3.zero;
        fxInst.transform.localRotation = Quaternion.identity;

        PrepareVisualClone(fxInst);

        if (portraitLayer >= 0)
        {
            SetLayerRecursively(fxHolder.transform, portraitLayer);
        }

        fxHolder.SetActive(true);

        ParticleSystem[] systems = fxInst.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            Debug.LogWarning($"[Store][StorePosePreviewRig] 이펙트 '{entry.key}'에 ParticleSystem이 없어 캡처를 건너뜁니다.");
            CleanupEffectCapture(fxHolder);
            yield break;
        }

        // 프리팹에 형제 파티클 트리가 병렬로 있을 수 있어 최상위 시스템만 골라 각각 Simulate한다
        // (중첩 시스템은 부모의 withChildren이 함께 처리). Simulate(restart) 후 시스템은 일시정지
        // 상태로 남아 이후 프레임에도 같은 그림이 렌더된다.
        foreach (ParticleSystem ps in systems)
        {
            Transform psParent = ps.transform.parent;
            bool nested = psParent != null && psParent.GetComponentInParent<ParticleSystem>() != null;
            if (nested == false)
            {
                ps.Simulate(entry.simulateTime, true, true);
            }
        }

        // 이펙트 인스턴스 기준 프레이밍 (ParticleSystemRenderer는 Renderer 파생이라 기존 프레이밍이 그대로 동작)
        FrameCamera(rigCam, fxInst);

        // 스테일 렌더 방지: 한 프레임 넘긴 뒤 프레임 끝에서 RT를 읽는다
        yield return null;
        yield return new WaitForEndOfFrame();

        result.sprite = ReadBackSprite();

        CleanupEffectCapture(fxHolder);
    }

    // 이펙트 캡처의 모든 종료 경로에서 호출한다.
    private void CleanupEffectCapture(GameObject fxHolder)
    {
        if (fxHolder != null)
        {
            Destroy(fxHolder);
        }
        if (activeFxHolder == fxHolder)
        {
            activeFxHolder = null;
        }

        SetCharRenderersEnabled(true);
    }

    private void SetCharRenderersEnabled(bool enabled)
    {
        if (charRenderers == null)
        {
            return;
        }

        foreach (Renderer r in charRenderers)
        {
            if (r != null)
            {
                r.enabled = enabled;
            }
        }
    }

    // 현재 RT 내용을 읽어 스프라이트 생성 — 생성물의 수명 관리(파괴)는 StoreManager 캐시가 담당
    private Sprite ReadBackSprite()
    {
        RenderTexture prev = RenderTexture.active;
        Texture2D tex = null;
        try
        {
            RenderTexture.active = rt;
            tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            tex = null; // 성공 시 소유권은 Sprite/StoreManager로 이전
            return sprite;
        }
        finally
        {
            RenderTexture.active = prev;
            if (tex != null)
            {
                Destroy(tex);
            }
        }
    }

    // 캡처 결과는 Sprite와 런타임 생성 Texture2D를 한 쌍으로 해제해야 한다.
    public static void ReleaseCapturedSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        Texture texture = sprite.texture;
        if (texture != null)
        {
            Destroy(texture);
        }
        Destroy(sprite);
    }

    // ── 정리 (원본: animationplayermanager.cs ReleasePlayer) ──

    private void OnDisable()
    {
        // GO 비활성화는 코루틴을 죽이지만 pump 핸들은 남는다 — 핸들을 비워 재활성화 시 재시동을 허용하고,
        // 죽은 캡처(진행 중 + 대기)에 실패를 통지한다. 컴포넌트 enabled=false만으로는 코루틴이 살아 있으므로
        // 그 경우(activeInHierarchy == true)는 건드리지 않는다. 파괴 경로는 OnDestroy의 드레인이 커버한다.
        if (gameObject.activeInHierarchy)
        {
            return;
        }

        pump = null;
        DrainRequestsWithFailure();
        if (activeFxHolder != null)
        {
            Destroy(activeFxHolder);
            activeFxHolder = null;
        }
        SetCharRenderersEnabled(true);
        SetCaptureSessionActive(false);
    }

    private void OnDestroy()
    {
        if (pump != null)
        {
            StopCoroutine(pump);
            pump = null;
        }

        DrainRequestsWithFailure();

        if (activeFxHolder != null)
        {
            Destroy(activeFxHolder);
            activeFxHolder = null;
        }
        SetCaptureSessionActive(false);

        if (isGraphCreated && graph.IsValid())
        {
            graph.Destroy();
        }
        graph = default;
        output = default;
        clipPlayable = default;
        isGraphCreated = false;

        if (rigCam != null)
        {
            rigCam.targetTexture = null;
        }
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
