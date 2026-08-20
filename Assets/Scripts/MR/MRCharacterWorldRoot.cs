// Phase 3-1 — 캔버스 하위에 스폰되는 캐릭터를 월드 공간으로 옮긴다.
//
// 배경:
//   데스크톱에서 CharManager는 캐릭터를 Root260616/Canvases/Canvas_Char 하위에 스폰한다.
//   Canvas_Char는 Screen Space - Camera 캔버스라 좌표계가 화면 픽셀 단위이고,
//   캐릭터 루트의 localScale은 120이다. MR에서는 이 좌표계가 의미가 없다.
//
//   다행히 캐릭터는 UI가 아니다 — Aico.prefab에는 CanvasRenderer가 0개이고
//   SkinnedMeshRenderer 3개로 구성된 완전한 3D VRM이다. 루트만 RectTransform인데,
//   이는 캔버스 안에서 좌표를 잡기 위한 핸들일 뿐 렌더링과는 무관하다.
//   RectTransform은 Transform을 상속하므로, 부모가 일반 Transform이면
//   transform.position으로 그냥 움직이면 된다. 컴포넌트 교체가 필요 없다.
//
// 따라서 프리팹도 CharManager도 수정하지 않고, 런타임 리페어런트만으로 해결한다.
// 데스크톱 씬에는 이 컴포넌트가 없으므로 영향이 없다.
//
// 배치: SampleSceneKAI-MR 씬 루트 (KAIManager / MRSceneStripper 옆)

using UnityEngine;

public class MRCharacterWorldRoot : MonoBehaviour
{
    [Header("월드 루트")]
    [Tooltip("캐릭터를 옮겨 붙일 월드 루트. 비워두면 자동 생성한다.")]
    [SerializeField] private Transform worldRoot;

    public enum CharacterScaleMode
    {
        /// 캔버스 픽셀 ↔ 미터 비율을 고정한다. 캐릭터 간 상대 체격이 보존된다. (권장)
        FixedPixelRatio,
        /// 모든 캐릭터를 같은 키로 맞춘다. 체격 차이가 사라진다.
        UniformHeight,
        /// 크기를 건드리지 않는다.
        None,
    }

    [Header("크기 / 좌표계")]
    [Tooltip("FixedPixelRatio — '픽셀 N개 = 1미터' 비율을 고정. 캐릭터별 프리팹 스케일이\n" +
             "  상대 체격을 인코딩하고 있으므로(Aico=120, diana=200) 이 방식이 그 차이를 보존한다.\n" +
             "UniformHeight — 모든 캐릭터를 Target Height Meters로 통일. 체격 차이가 사라진다.")]
    [SerializeField] private CharacterScaleMode scaleMode = CharacterScaleMode.FixedPixelRatio;

    [Tooltip("[FixedPixelRatio] 캔버스 몇 픽셀을 1미터로 볼 것인가.\n" +
             "120이면 Aico(프리팹 스케일 120)가 원본 크기 약 1.62m로 선다.\n" +
             "값을 키우면 모든 캐릭터가 작아진다. 480 정도면 책상 위 마스코트 크기.")]
    [SerializeField] private float pixelsPerMeter = 120f;

    [Tooltip("[FixedPixelRatio] 전역 크기 배율. 상대 체격을 유지한 채 전체를 키우거나 줄인다.")]
    [SerializeField] private float sizeMultiplier = 1f;

    [Tooltip("[UniformHeight] 모든 캐릭터를 맞출 목표 높이(미터)")]
    [SerializeField] private float targetHeightMeters = 1.6f;

    [Tooltip("스케일을 캐릭터 자신이 아니라 '픽셀 공간 래퍼' 부모에 적용한다.\n\n" +
             "데스크톱 코드는 캔버스 픽셀 단위로 동작한다 — 캐릭터 localScale 120,\n" +
             "캔버스 깊이 z = -70, PhysicsManager moveSpeed = 120픽셀/초 등.\n" +
             "캐릭터를 직접 줄이면 이 값들이 미터 단위로 해석되어 터진다(120m/s로 날아감).\n" +
             "대신 부모 래퍼를 축소하면 좌표계 자체가 환산되어 픽셀 값이 전부 자연스러운\n" +
             "미터 값이 된다. 예: 래퍼 1/486 → 키 0.4m, z -0.14m, 이동 0.25m/s.")]
    [SerializeField] private bool usePixelSpaceWrapper = true;

    [Header("초기 배치")]
    [Tooltip("카메라 정면 몇 미터 앞에 놓을지")]
    [SerializeField] private float spawnDistance = 0.8f;

    [Tooltip("바닥(트래킹 원점 y=0) 기준 높이(미터).\n" +
             "OVRManager의 Tracking Origin이 Floor Level이므로 0이면 바닥에 선다.\n" +
             "책상 위에 올리려면 0.7 정도. Phase 2에서 MRUK 바닥/책상 안착으로 대체된다.")]
    [SerializeField] private float spawnFloorHeight = 0f;

    [Tooltip("헤드 트래킹이 붙을 때까지 배치를 미룬다. 끄면 즉시 배치(원점에 놓일 수 있음).")]
    [SerializeField] private bool waitForTracking = true;

    [Tooltip("트래킹을 이 시간까지 못 받으면 그냥 배치한다(초)")]
    [SerializeField] private float trackingWaitTimeout = 10f;

    [Tooltip("캐릭터가 사용자를 바라보게 한다 (Y축만)")]
    [SerializeField] private bool faceUser = true;

    [Header("레이어")]
    [Tooltip("리페어런트 후 적용할 레이어. CenterEyeAnchor 카메라의 Culling Mask에 포함돼야 보인다.\n" +
             "-1이면 레이어를 변경하지 않는다.")]
    [SerializeField] private int targetLayer = -1;

    [Header("폴백 스폰 (임시)")]
    [Tooltip("CharManager가 캐릭터를 스폰하지 못할 때 직접 생성할 프리팹. Assets/Char/Aico/Aico.prefab\n" +
             "Android에서 StreamingAssets를 읽지 못해 캐릭터 DB 로드가 실패하는 문제의 임시 우회다.\n" +
             "근본 해결은 MR_StreamingAssets_Migration_Plan.md 참조.")]
    [SerializeField] private GameObject fallbackCharacterPrefab;

    [Tooltip("이 시간까지 CharManager 스폰을 기다린 뒤 폴백을 생성한다(초). 0이면 폴백 비활성.")]
    [SerializeField] private float fallbackDelaySeconds = 8f;

    [Header("이동 범위 제한")]
    [Tooltip("PhysicsManager가 픽셀 공간 안에서 좌우로 걷는 범위를 미터로 제한한다.\n" +
             "원래 경계는 Canvas_Char 폭(±960픽셀)이라 1/120 스케일에서 ±8m가 되어 방을 벗어난다.\n" +
             "0이면 제한하지 않는다. Phase 2의 방 안 자율 이동이 들어오면 대체된다.")]
    [SerializeField] private float wanderRangeMeters = 0.6f;

    [Header("디버그")]
    [SerializeField] private bool verboseLog = true;

    [Tooltip("캐릭터·카메라 위치를 주기적으로 로그에 찍는다(초). 0이면 끔.")]
    [SerializeField] private float trackingLogInterval = 1f;

    private GameObject _lastCharacter;
    private Camera _cam;

    // Camera.main은 Awake 시점에 OVR 리그가 준비되지 않았거나 MainCamera 태그가 없으면 null이다.
    // 매번 지연 해석하고, 실패하면 CenterEyeAnchor를 이름으로 찾는다.
    private Camera ResolveCamera()
    {
        if (_cam != null && _cam.isActiveAndEnabled) return _cam;

        _cam = Camera.main;
        if (_cam != null && _cam.isActiveAndEnabled) return _cam;

        // 폴백 1: OVR CenterEyeAnchor
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Camera c in cams)
        {
            if (c.name == "CenterEyeAnchor") { _cam = c; return _cam; }
        }

        // 폴백 2: 활성 카메라 중 아무거나
        if (cams.Length > 0)
        {
            _cam = cams[0];
            Debug.LogWarning($"[MRCharWorld] CenterEyeAnchor를 찾지 못해 '{_cam.name}'을 사용합니다.");
            return _cam;
        }

        return null;
    }

    private void Awake()
    {
        if (worldRoot == null)
        {
            GameObject go = new GameObject("MR Character World Root");
            worldRoot = go.transform;
            worldRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }

    private float _waitLogTimer;
    private bool _everSawCharacter;

    private void Start()
    {
        Debug.Log($"[MRCharWorld] 활성화됨 — 월드 루트 '{worldRoot.name}', " +
                  $"목표 높이 {targetHeightMeters}m, 캐릭터 스폰 대기 중");
    }

    private GameObject _fallbackInstance;
    private float _elapsed;

    private void Update()
    {
        _elapsed += Time.deltaTime;

        GameObject current = (CharManager.Instance != null)
            ? CharManager.Instance.GetCurrentCharacter()
            : null;

        if (current == null)
        {
            string reason = (CharManager.Instance == null)
                ? "CharManager.Instance가 null입니다"
                : "CharManager.GetCurrentCharacter()가 null입니다 — 캐릭터가 아직 스폰되지 않았습니다";
            WarnIfStuck(reason);
            TrySpawnFallback();
            return;
        }

        // CharManager가 뒤늦게 정상 스폰했다면 폴백을 정리한다.
        if (_fallbackInstance != null && current != _fallbackInstance)
        {
            Debug.Log("[MRCharWorld] CharManager가 캐릭터를 스폰해 폴백 인스턴스를 제거합니다.");
            Destroy(_fallbackInstance);
            _fallbackInstance = null;
        }

        if (current == _lastCharacter)
        {
            ClampWanderRange();
            RepositionWhenTrackingArrives();
            LogTracking();
            return;
        }

        _lastCharacter = current;
        _everSawCharacter = true;
        MoveToWorld(current);
    }

    // =========================================================
    // 폴백 스폰 — StreamingAssets 문제의 임시 우회
    // =========================================================
    private void TrySpawnFallback()
    {
        if (_fallbackInstance != null) return;
        if (fallbackCharacterPrefab == null) return;
        if (fallbackDelaySeconds <= 0f) return;
        if (_elapsed < fallbackDelaySeconds) return;

        Debug.LogWarning($"[MRCharWorld] {fallbackDelaySeconds}초 동안 캐릭터 스폰이 없어 " +
                         $"'{fallbackCharacterPrefab.name}'을 직접 생성합니다. " +
                         "(StreamingAssets 로드 실패에 대한 임시 우회)");

        // 정상 경로와 동일하게 픽셀 공간 래퍼 안에 생성한다.
        Transform attachTo = usePixelSpaceWrapper
            ? CreatePixelSpace(fallbackCharacterPrefab.name)
            : worldRoot;

        _fallbackInstance = Instantiate(fallbackCharacterPrefab, attachTo);
        _fallbackInstance.transform.localPosition = Vector3.zero;
        _fallbackInstance.transform.localRotation = Quaternion.identity;
        _fallbackInstance.name = fallbackCharacterPrefab.name + " (MR Fallback)";
        _fallbackInstance.SetActive(true);

        CharAttributes fallbackAttrs = _fallbackInstance.GetComponent<CharAttributes>();
        if (fallbackAttrs == null)
        {
            fallbackAttrs = _fallbackInstance.AddComponent<CharAttributes>();
            fallbackAttrs.charcode = "aico";
            fallbackAttrs.nickname = "AICO";
        }

        _lastCharacter = _fallbackInstance;
        _everSawCharacter = true;

        // 프리팹 루트가 RectTransform(localScale 120)이므로 크기·위치를 반드시 보정한다.
        ApplyScale(_fallbackInstance);
        PlaceInFrontOfUser(_fallbackInstance);
        if (targetLayer >= 0) ApplyLayerRecursive(_fallbackInstance.transform, targetLayer);

        Debug.Log($"[MRCharWorld] 폴백 생성 완료 — lossyScale {_fallbackInstance.transform.lossyScale.y:F4}, " +
                  $"pos {_fallbackInstance.transform.position}");

        DiagnoseVisibility(_fallbackInstance);
    }

    // 캐릭터를 못 찾는 상태가 이어지면 5초마다 이유를 알린다.
    // (조용히 아무것도 하지 않아 원인 파악이 어려웠던 경험 때문)
    private void WarnIfStuck(string reason)
    {
        if (_everSawCharacter) return;

        _waitLogTimer -= Time.deltaTime;
        if (_waitLogTimer > 0f) return;

        _waitLogTimer = 5f;
        Debug.LogWarning($"[MRCharWorld] 대기 중 — {reason}");
    }

    // =========================================================
    // 캔버스 → 월드 이관
    // =========================================================
    private void MoveToWorld(GameObject character)
    {
        if (character.transform.parent == worldRoot)
        {
            return;  // 이미 처리됨
        }

        string beforeParent = character.transform.parent != null ? character.transform.parent.name : "(없음)";

        // 픽셀 공간 래퍼를 만들어 그 안으로 옮긴다. 래퍼를 축소하면 캐릭터의 로컬 좌표계
        // (= anchoredPosition, moveSpeed, z=-70 등 데스크톱 픽셀 값이 사는 공간) 전체가
        // 미터 단위로 환산된다. 캐릭터 자신을 축소하는 것과 결정적으로 다르다.
        Transform attachTo = usePixelSpaceWrapper ? CreatePixelSpace(character.name) : worldRoot;

        // worldPositionStays: false — 래퍼 기준 로컬 좌표를 그대로 쓰기 위함.
        character.transform.SetParent(attachTo, worldPositionStays: !usePixelSpaceWrapper);

        if (usePixelSpaceWrapper)
        {
            // 캔버스 안에 있던 로컬 변환을 그대로 재현한다.
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
        }

        ClearCanvasDepth(character);

        ApplyScale(character);
        PlaceInFrontOfUser(character);
        if (targetLayer >= 0) ApplyLayerRecursive(character.transform, targetLayer);

        if (verboseLog)
        {
            Debug.Log($"[MRCharWorld] '{character.name}' 이관 완료 — " +
                      $"부모 {beforeParent} → {worldRoot.name}, " +
                      $"lossyScale {character.transform.lossyScale.y:F4}, " +
                      $"layer {LayerMask.LayerToName(character.layer)}");
        }

        DiagnoseVisibility(character);
    }

    // 데스크톱 캔버스 규약의 깊이 값(z = -70)을 제거한다.
    // CharManager가 스폰 시 anchoredPosition3D = (0,0,-70)으로 박아두는데,
    // 캔버스 안에서는 70픽셀이지만 월드에서는 70미터가 되어 카메라 뒤로 밀려난다.
    // RectTransform은 anchoredPosition을 쓸 때마다 이 z를 되살리므로 명시적으로 지운다.
    private void ClearCanvasDepth(GameObject character)
    {
        if (character.transform is RectTransform rt)
        {
            Vector3 p3 = rt.anchoredPosition3D;
            if (Mathf.Abs(p3.z) > 0.001f)
            {
                Debug.Log($"[MRCharWorld] 캔버스 깊이 상수 제거: anchoredPosition3D.z {p3.z} → 0");
                rt.anchoredPosition3D = new Vector3(p3.x, p3.y, 0f);
            }
        }
    }

    // 픽셀 공간 래퍼 생성. 캐릭터의 좌표계를 담는 그릇이다.
    private Transform _pixelSpace;

    private Transform CreatePixelSpace(string charName)
    {
        if (_pixelSpace != null) return _pixelSpace;

        GameObject go = new GameObject($"PixelSpace ({charName})");
        _pixelSpace = go.transform;
        _pixelSpace.SetParent(worldRoot, worldPositionStays: false);
        _pixelSpace.localPosition = Vector3.zero;
        _pixelSpace.localRotation = Quaternion.identity;
        _pixelSpace.localScale = Vector3.one;
        return _pixelSpace;
    }

    // 실제로 위치·회전을 조작할 대상. 래퍼가 있으면 래퍼를, 없으면 캐릭터를 움직인다.
    private Transform MoveTarget(GameObject character)
    {
        return (usePixelSpaceWrapper && _pixelSpace != null) ? _pixelSpace : character.transform;
    }

    private bool TryMeasureHeight(GameObject character, out float height)
    {
        height = 0f;
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[MRCharWorld] Renderer를 찾지 못해 크기를 측정할 수 없습니다.");
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        height = bounds.size.y;
        return height > 0.0001f;
    }

    // 크기 적용. 래퍼가 있으면 래퍼에 적용해야 캐릭터의 로컬 좌표계(픽셀 값이 사는 공간)가
    // 함께 환산되어 moveSpeed·z=-70 같은 상수가 자연스러운 미터 값이 된다.
    private void ApplyScale(GameObject character)
    {
        if (scaleMode == CharacterScaleMode.None) return;

        Transform target = (usePixelSpaceWrapper && _pixelSpace != null) ? _pixelSpace : character.transform;
        float before = target.localScale.x;
        float newScale;

        if (scaleMode == CharacterScaleMode.FixedPixelRatio)
        {
            // 캐릭터마다 프리팹 localScale이 다르다(Aico=120, diana=200). 그 값이 상대 체격을
            // 인코딩하고 있으므로, 비율만 고정하면 체격 차이가 그대로 보존된다.
            if (pixelsPerMeter <= 0f)
            {
                Debug.LogWarning("[MRCharWorld] pixelsPerMeter가 0 이하입니다. 크기 적용을 건너뜁니다.");
                return;
            }
            newScale = (1f / pixelsPerMeter) * Mathf.Max(0.0001f, sizeMultiplier);
        }
        else // UniformHeight
        {
            if (!TryMeasureHeight(character, out float h))
            {
                Debug.LogWarning("[MRCharWorld] 높이 측정 실패로 크기 적용을 건너뜁니다.");
                return;
            }
            newScale = before * (targetHeightMeters / h);
        }

        target.localScale = Vector3.one * newScale;

        // 적용 후 실제 높이를 재서 로그로 남긴다. 캐릭터별 체격 차이를 확인할 수 있다.
        TryMeasureHeight(character, out float finalH);

        Debug.Log(
            $"[MRCharWorld] 크기 적용 [{scaleMode}] — '{character.name}'\n" +
            $"  픽셀 공간 스케일 : {newScale:F6}  (1픽셀 ≈ {newScale * 1000f:F2}mm, 1/{(newScale > 0 ? 1f / newScale : 0f):F0})\n" +
            $"  실제 키          : {finalH:F3} m\n" +
            $"  환산 예시        : z=-70 → {-70f * newScale:F3}m, moveSpeed 120px/s → {120f * newScale:F3}m/s");

        if (finalH > 3f || (finalH > 0f && finalH < 0.05f))
        {
            Debug.LogWarning($"[MRCharWorld] ⚠ 캐릭터 키가 비정상적입니다({finalH:F3}m). " +
                             $"pixelsPerMeter({pixelsPerMeter}) 값을 확인하세요.");
        }
    }

    // 헤드 트래킹이 붙었는지 판정한다.
    // 앱 시작 직후에는 카메라가 원점(0,0,0)에 머물러 있어, 그 상태로 배치하면
    // 캐릭터가 바닥 아래나 엉뚱한 곳에 놓인다. (실제로 y = -0.35m에 묻힌 사고가 있었다)
    private bool IsTrackingReady(Camera cam)
    {
        if (cam == null) return false;
        // Floor Level 원점 기준으로 머리는 최소 0.5m 이상 위에 있다.
        return cam.transform.position.y > 0.5f || cam.transform.position.sqrMagnitude > 0.01f;
    }

    // 카메라 정면, 바닥 높이에 배치한다. Phase 2에서 MRUK 바닥·책상 안착으로 대체된다.
    private void PlaceInFrontOfUser(GameObject character)
    {
        Camera cam = ResolveCamera();
        if (cam == null)
        {
            Debug.LogWarning("[MRCharWorld] 사용할 카메라를 찾지 못해 초기 배치를 건너뜁니다.");
            return;
        }

        Transform camT = cam.transform;
        bool tracked = IsTrackingReady(cam);

        if (verboseLog)
        {
            Debug.Log($"[MRCharWorld] 기준 카메라: {cam.name} (pos {camT.position}, 트래킹 {(tracked ? "정상" : "미준비")})");
        }

        // 수평 방향만 사용해 카메라가 위아래를 봐도 캐릭터가 눈앞에 뜨지 않게 한다.
        Vector3 forward = camT.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 pos = camT.position + forward * spawnDistance;

        // 높이는 카메라가 아니라 트래킹 원점(바닥) 기준으로 정한다.
        // 카메라 높이에 의존하면 트래킹 미준비 상태에서 바닥 아래로 내려간다.
        pos.y = spawnFloorHeight;

        // 래퍼가 있으면 래퍼를 움직인다. 캐릭터를 직접 옮기면 픽셀 좌표계 안에서
        // 미터 단위 값을 쓰게 되어 어긋난다.
        Transform target = MoveTarget(character);
        target.position = pos;

        if (faceUser)
        {
            target.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        }

        _placedWithTracking = tracked;
    }

    // 트래킹이 늦게 붙은 경우 사용자 앞으로 한 번 다시 놓는다.
    private bool _placedWithTracking;
    private float _repositionTimer;

    private void RepositionWhenTrackingArrives()
    {
        if (_placedWithTracking) return;
        if (_lastCharacter == null) return;
        if (!waitForTracking) return;

        _repositionTimer -= Time.deltaTime;
        if (_repositionTimer > 0f) return;
        _repositionTimer = 0.5f;

        Camera cam = ResolveCamera();
        if (!IsTrackingReady(cam))
        {
            if (_elapsed > trackingWaitTimeout)
            {
                Debug.LogWarning($"[MRCharWorld] {trackingWaitTimeout}초 동안 헤드 트래킹을 받지 못했습니다. 현재 위치를 유지합니다.");
                _placedWithTracking = true;   // 더 시도하지 않음
            }
            return;
        }

        Debug.Log("[MRCharWorld] 헤드 트래킹이 붙어 캐릭터를 사용자 앞으로 다시 배치합니다.");
        PlaceInFrontOfUser(_lastCharacter);
        DiagnoseVisibility(_lastCharacter);
    }

    /// <summary>캐릭터를 지금 보고 있는 방향 앞으로 다시 소환한다. 메뉴/디버그에서 호출용.</summary>
    public void RepositionInFrontOfUser()
    {
        if (_lastCharacter == null) return;
        PlaceInFrontOfUser(_lastCharacter);
        DiagnoseVisibility(_lastCharacter);
    }

    // =========================================================
    // 이동 범위 제한
    // =========================================================
    // PhysicsManager는 픽셀 공간 안에서 anchoredPosition.x를 좌우로 움직인다.
    // 자체 경계가 Canvas_Char 폭(±960픽셀)이라 월드로 환산하면 ±8m가 되어 방을 벗어난다.
    // 래퍼 스케일을 역산해 미터 단위로 제한한다.
    private void ClampWanderRange()
    {
        if (wanderRangeMeters <= 0f) return;
        if (_lastCharacter == null) return;
        if (!(_lastCharacter.transform is RectTransform rt)) return;

        float pixelScale = (usePixelSpaceWrapper && _pixelSpace != null) ? _pixelSpace.localScale.x : 1f;
        if (pixelScale <= 0.000001f) return;

        float limitPx = wanderRangeMeters / pixelScale;   // 미터 → 픽셀 공간 단위
        Vector2 ap = rt.anchoredPosition;
        float clampedX = Mathf.Clamp(ap.x, -limitPx, limitPx);

        if (Mathf.Approximately(clampedX, ap.x)) return;

        rt.anchoredPosition = new Vector2(clampedX, ap.y);
    }

    // =========================================================
    // 주기적 추적 로그 — 캐릭터가 어디에 있고 실제로 그려지는지
    // =========================================================
    private float _trackLogTimer;
    private Renderer[] _trackedRenderers;

    private void LogTracking()
    {
        if (trackingLogInterval <= 0f) return;
        if (_lastCharacter == null) return;

        _trackLogTimer -= Time.deltaTime;
        if (_trackLogTimer > 0f) return;
        _trackLogTimer = trackingLogInterval;

        Camera cam = ResolveCamera();
        Transform ct = _lastCharacter.transform;

        if (_trackedRenderers == null || _trackedRenderers.Length == 0)
            _trackedRenderers = _lastCharacter.GetComponentsInChildren<Renderer>(true);

        // isVisible == 어떤 카메라든 지난 프레임에 실제로 렌더링했는가.
        // 위치·레이어가 맞는데 이게 0이면 컬링·셰이더·머티리얼 문제로 좁혀진다.
        int visible = 0, enabled = 0;
        foreach (Renderer r in _trackedRenderers)
        {
            if (r == null) continue;
            if (r.enabled && r.gameObject.activeInHierarchy) enabled++;
            if (r.isVisible) visible++;
        }

        if (cam == null)
        {
            Debug.Log($"[MRTrack] char {ct.position} | 카메라 없음 | 렌더러 활성 {enabled} 표시 {visible}");
            return;
        }

        Vector3 cp = cam.transform.position;
        Vector3 vp = cam.WorldToViewportPoint(ct.position);
        float dist = Vector3.Distance(cp, ct.position);
        bool inView = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;

        Debug.Log(
            $"[MRTrack] cam {cp.ToString("F2")} fwd {cam.transform.forward.ToString("F2")} | " +
            $"char {ct.position.ToString("F2")} scale {ct.lossyScale.y:F3} | " +
            $"거리 {dist:F2}m | 뷰포트 {vp.ToString("F2")} 시야안 {inView} | " +
            $"렌더러 활성 {enabled} / 실제표시 {visible}");
    }

    // =========================================================
    // 가시성 진단 — "왜 안 보이는가"를 한 번에 판정한다
    // =========================================================
    private void DiagnoseVisibility(GameObject character)
    {
        Camera cam = ResolveCamera();
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);

        int enabledCount = 0;
        Bounds b = new Bounds(character.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer r in renderers)
        {
            if (r.enabled && r.gameObject.activeInHierarchy) enabledCount++;
            if (!hasBounds) { b = r.bounds; hasBounds = true; }
            else b.Encapsulate(r.bounds);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[MRCharWorld] === 가시성 진단: {character.name} ===");
        sb.AppendLine($"  activeInHierarchy : {character.activeInHierarchy}");
        sb.AppendLine($"  Renderer          : 전체 {renderers.Length}개 / 활성 {enabledCount}개");
        sb.AppendLine($"  월드 위치         : {character.transform.position}");
        sb.AppendLine($"  lossyScale        : {character.transform.lossyScale}");
        sb.AppendLine($"  바운즈 크기       : {(hasBounds ? b.size.ToString() : "없음")}");
        sb.AppendLine($"  레이어            : {character.layer} ({LayerMask.LayerToName(character.layer)})");

        if (cam != null)
        {
            float dist = Vector3.Distance(cam.transform.position, character.transform.position);
            bool inMask = (cam.cullingMask & (1 << character.layer)) != 0;
            Vector3 vp = cam.WorldToViewportPoint(character.transform.position);
            bool inFrustum = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;

            sb.AppendLine($"  기준 카메라       : {cam.name} (near {cam.nearClipPlane}, far {cam.farClipPlane})");
            sb.AppendLine($"  카메라와 거리     : {dist:F3} m");
            sb.AppendLine($"  뷰포트 좌표       : {vp} (시야 안: {inFrustum})");
            sb.AppendLine($"  컬링 마스크 포함  : {inMask}");

            // 원인 판정
            if (!inMask)
                sb.AppendLine($"  ❌ 원인: 레이어 '{LayerMask.LayerToName(character.layer)}'가 카메라 컬링 마스크에 없습니다. " +
                              "MRCharacterWorldRoot의 Target Layer를 0(Default)으로 설정하세요.");
            else if (enabledCount == 0)
                sb.AppendLine("  ❌ 원인: 활성 Renderer가 0개입니다.");
            else if (hasBounds && b.size.y < 0.005f)
                sb.AppendLine($"  ❌ 원인: 너무 작습니다(높이 {b.size.y:F5}m). 크기 보정이 잘못됐습니다.");
            else if (dist < cam.nearClipPlane)
                sb.AppendLine($"  ❌ 원인: 카메라 근접 평면({cam.nearClipPlane}m) 안쪽에 있습니다.");
            else if (!inFrustum)
                sb.AppendLine("  ⚠ 시야 밖입니다. 고개를 돌려보세요. 위치 자체는 정상일 수 있습니다.");
            else
                sb.AppendLine("  ✅ 렌더링 조건은 모두 정상입니다. 머티리얼/셰이더 문제일 수 있습니다.");
        }
        else
        {
            sb.AppendLine("  ❌ 기준 카메라를 찾지 못했습니다.");
        }

        Debug.Log(sb.ToString());
    }

    private void ApplyLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
        {
            ApplyLayerRecursive(t.GetChild(i), layer);
        }
    }

    // =========================================================
    // 외부 호출용 — Phase 2에서 MRFloorPlacement가 사용
    // =========================================================
    public Transform WorldRoot => worldRoot;
    public GameObject CurrentCharacter => _lastCharacter;

    /// <summary>현재 캐릭터를 지정 위치로 옮긴다. 바닥 안착·드래그에서 사용.</summary>
    public void SetCharacterPosition(Vector3 worldPosition)
    {
        if (_lastCharacter == null) return;
        MoveTarget(_lastCharacter).position = worldPosition;
    }

    /// <summary>픽셀 공간 래퍼. Phase 2의 바닥 배치·드래그는 이것을 움직여야 한다.</summary>
    public Transform PixelSpace => _pixelSpace;

    // 위치와 **회전**을 조작할 대상 트랜스폼. 래퍼가 있으면 래퍼, 없으면 캐릭터다.
    //
    // SetCharacterPosition은 위치만 다루는데 MRRayDragAdapter가 손 회전으로 캐릭터를
    // 돌려야 해서 노출했다. "무엇을 움직이는가"의 판단이 두 곳으로 갈라지면
    // 위치와 회전이 서로 다른 트랜스폼에 걸리는 사고가 난다(§4-47).
    public Transform CharacterMoveTarget
    {
        get
        {
            if (_lastCharacter == null) return null;

            return MoveTarget(_lastCharacter);
        }
    }

    // =========================================================
    // 외부 호출용 — Phase 3-2 AICO 시스템 메뉴의 크기 슬라이더
    // =========================================================
    /// <summary>현재 전역 크기 배율. FixedPixelRatio 모드에서만 의미가 있다.</summary>
    public float SizeMultiplier => sizeMultiplier;

    /// <summary>
    /// 런타임에 크기 배율을 바꾸고 즉시 적용한다 (시스템 메뉴의 크기 슬라이더용).
    /// scaleMode가 FixedPixelRatio가 아니면 무시된다 — UniformHeight/None은 다른 기준으로 크기를 정한다.
    /// </summary>
    public void SetSizeMultiplier(float multiplier)
    {
        if (scaleMode != CharacterScaleMode.FixedPixelRatio)
        {
            Debug.LogWarning($"[MRCharWorld] SetSizeMultiplier는 FixedPixelRatio 모드 전용입니다 (현재: {scaleMode}). 무시합니다.");
            return;
        }

        sizeMultiplier = Mathf.Max(0.01f, multiplier);
        if (_lastCharacter != null) ApplyScale(_lastCharacter);
    }
}
