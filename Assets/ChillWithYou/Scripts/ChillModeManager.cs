using System.Collections;
using UnityEngine;
using MagicaCloth2;

// 칠윗유(Chill With You) 모드 진입/종료 관리
public class ChillModeManager : MonoBehaviour
{
    private static ChillModeManager instance;  // 싱글톤 인스턴스
    public static ChillModeManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ChillModeManager>();
            }
            return instance;
        }
    }

    [Header("References")]
    public ChillSitData chillSitData;  // 캐릭터별 착석 오프셋 데이터
    public Transform deskSetRoot;  // Desk_Set 프리팹 루트 (Chill 모드 중 위치/회전/스케일 실시간 조정용)
    public Transform chairRoot;  // 의자 오브젝트 자체 (예: SM_Prop_Chair_05) - 의자를 실제로 옮길 때 대상
    public RectTransform chairSeatPoint;  // 의자 하위, diana가 SetParent될 착석 기준점 (diana 로컬 오프셋 계산용)
    public RuntimeAnimatorController chillAnimatorController;  // HY Motion Animator (공용 리타겟팅)
    public string chillStateName = "SitTyping";  // 착석 시 재생할 상태 이름

    [Header("Desk_Set 오프셋 (에디터 튜닝용)")]
    public Vector3 deskPositionOffset;  // deskSetRoot 원래 위치에 더할 오프셋
    public Vector3 deskRotationOffset;  // deskSetRoot 원래 회전에 더할 오프셋 (Euler)
    public float deskScaleMultiplier = 1f;  // deskSetRoot 원래 스케일에 곱할 배율

    [Header("LookAround")]
    public string lookAroundTriggerName = "LookAround";  // SitLookAround 재생용 트리거 파라미터 이름
    public float lookAroundMinInterval = 8f;  // 최소 대기 시간(초)
    public float lookAroundMaxInterval = 20f;  // 최대 대기 시간(초)

    [Header("시점 전환")]
    public float viewTransitionSeconds = 0.5f;  // 시점 프리셋 전환 시간 (0이면 즉시)

    public bool IsViewTransitioning { get { return viewTransitionCoroutine != null; } }  // UI가 전환 중 입력을 잠그는 데 사용

    private Coroutine viewTransitionCoroutine;  // 진행 중인 시점 전환 (중복 실행 방지)

    [Header("에디터 튜닝")]
    public bool applyOffsetEveryFrame = true;  // 인스펙터에서 값 바꿀 때 즉시 확인용, UI 연동 시에는 꺼두고 Set 함수로 1회씩 적용

    [Header("데모/테스트")]
    public GameObject overrideCharacter;  // CharManager가 없는 데모씬 등에서 착석 대상을 직접 지정 (null이면 CharManager의 현재 캐릭터)

    public bool IsChillMode { get { return isChillMode; } }  // 외부(UI)에서 현재 모드 확인용
    public GameObject CurrentChillCharacter { get { return chillCharacter; } }  // 현재 착석 중인 캐릭터 (SitSupport 등 UI용)

    private bool isChillMode = false;  // 현재 칠윗유 모드 여부
    private GameObject chillCharacter;  // 칠윗유 모드에 들어간 캐릭터
    private float lookAroundTimer;  // 다음 LookAround 트리거까지 남은 시간

    private Transform originalParent;  // 원래 부모
    private Vector2 originalAnchoredPosition;  // 원래 anchoredPosition
    private float originalAnchoredPositionZ;  // 원래 anchoredPosition3D.z
    private Quaternion originalLocalRotation;  // 원래 로컬 회전
    private Vector3 originalLocalScale;  // 원래 로컬 스케일
    private RuntimeAnimatorController originalController;  // 원래 애니메이터 컨트롤러
    private FallingObject fallingObject;  // 데스크톱 하단 낙하 처리 컴포넌트 (Chill 모드 중 비활성화 대상)

    private Vector3 deskOriginalPosition;  // deskSetRoot 원래 로컬 위치 (오프셋 기준점 + Exit 시 복원용)
    private Quaternion deskOriginalRotation;  // deskSetRoot 원래 로컬 회전 (오프셋 기준점 + Exit 시 복원용)
    private Vector3 deskOriginalScale;  // deskSetRoot 원래 로컬 스케일 (오프셋 기준점 + Exit 시 복원용)

    private Vector3 chairOriginalLocalPosition;  // chairRoot 원래 로컬 위치 (오프셋 기준점 + Exit 시 복원용)
    private Quaternion chairOriginalLocalRotation;  // chairRoot 원래 로컬 회전 (오프셋 기준점 + Exit 시 복원용)

    // 칠윗유 모드 진입 (현재 캐릭터를 의자에 착석)
    public void EnterChillMode()
    {
        Debug.Log("[ChillMode] EnterChillMode 호출됨");

        if (isChillMode)
        {
            // 이미 칠윗유 모드인 경우 무시
            Debug.Log("[ChillMode] 이미 칠윗유 모드라서 무시");
            return;
        }

        if (chillSitData == null || chairSeatPoint == null || chillAnimatorController == null)
        {
            Debug.Log($"[ChillMode] 참조 누락: chillSitData={chillSitData}, chairSeatPoint={chairSeatPoint}, chillAnimatorController={chillAnimatorController}");
            return;
        }

        GameObject character = overrideCharacter;
        if (character == null && CharManager.Instance != null)
        {
            character = CharManager.Instance.GetCurrentCharacter();
        }
        if (character == null)
        {
            Debug.Log("No ChillModeManager target: current character is null");
            return;
        }

        CharAttributes attrs = character.GetComponent<CharAttributes>();
        Animator animator = character.GetComponent<Animator>();
        RectTransform charRect = character.transform as RectTransform;
        if (attrs == null || animator == null || charRect == null)
        {
            Debug.Log("No ChillModeManager target: missing CharAttributes, Animator or RectTransform");
            return;
        }

        Debug.Log($"[ChillMode] 대상 캐릭터: {character.name}, charcode={attrs.charcode}");

        // 책상 공통 배치는 ChillSitData가 단일 출처 (데모 [데이터 저장]으로 영속되어 본편과 공유)
        deskPositionOffset = chillSitData.deskPositionOffset;
        deskRotationOffset = chillSitData.deskRotationOffset;
        deskScaleMultiplier = chillSitData.deskScaleMultiplier;

        // 원본 상태 저장
        chillCharacter = character;
        originalParent = charRect.parent;
        originalAnchoredPosition = charRect.anchoredPosition;
        originalAnchoredPositionZ = charRect.anchoredPosition3D.z;
        originalLocalRotation = charRect.localRotation;
        originalLocalScale = charRect.localScale;
        originalController = animator.runtimeAnimatorController;

        // 데스크톱 바닥으로 끌어내리는 낙하 로직을 Chill 모드 동안 정지
        fallingObject = character.GetComponent<FallingObject>();
        if (fallingObject != null)
        {
            fallingObject.enabled = false;
        }

        // 좌우 이동 로직도 Chill 모드 동안 정지 (씬 단일 인스턴스, 현재 캐릭터를 참조로 주입받는 구조)
        // 데모씬 등 매니저 부재 환경 대비 null 가드
        if (PhysicsManager.Instance != null)
        {
            PhysicsManager.Instance.StopAllAnimations();
            PhysicsManager.Instance.enabled = false;
        }

        // Desk_Set 원본 상태 저장 (오프셋은 이 값 기준으로 더해짐)
        if (deskSetRoot != null)
        {
            deskOriginalPosition = deskSetRoot.localPosition;
            deskOriginalRotation = deskSetRoot.localRotation;
            deskOriginalScale = deskSetRoot.localScale;
        }

        // chairRoot 원본 상태 저장 (오프셋은 이 값 기준으로 더해짐)
        if (chairRoot != null)
        {
            chairOriginalLocalPosition = chairRoot.localPosition;
            chairOriginalLocalRotation = chairRoot.localRotation;
        }

        // 캐릭터별 오프셋 조회 (의자 오프셋도 캐릭터별로 함께 관리)
        ChillSitData.CharacterSitOffset offset = chillSitData.GetOffset(attrs.charcode);
        if (offset == null)
        {
            Debug.Log($"[ChillMode] charcode={attrs.charcode}에 대한 오프셋 없음 (defaultOffset도 비어있음)");
            return;
        }

        // chairRoot 위치/회전 오프셋 적용 (캐릭터의 조상이므로 SetParent 전에 먼저 반영)
        if (chairRoot != null)
        {
            chairRoot.localPosition = chairOriginalLocalPosition + offset.chairLocalPosition;
            chairRoot.localRotation = chairOriginalLocalRotation * Quaternion.Euler(offset.chairLocalRotation);
        }

        Debug.Log($"[ChillMode] chairSeatPoint anchoredPos3D={chairSeatPoint.anchoredPosition3D}, localScale={chairSeatPoint.localScale}, lossyScale={chairSeatPoint.lossyScale}");

        // SetParent 시 중간 좌표가 잠깐 렌더링되는 것을 막기 위해 캐릭터를 잠시 비활성화
        character.SetActive(false);

        // 의자 기준점 아래로 이동 및 오프셋 적용 (RectTransform 기준 anchoredPosition3D 사용)
        charRect.SetParent(chairSeatPoint, false);
        Debug.Log($"[ChillMode] SetParent 직후 anchoredPos3D={charRect.anchoredPosition3D}");

        charRect.anchoredPosition3D = offset.positionOffset;

        // diana 원본 회전(카메라를 마주보는 180도 등)을 유지한 채 오프셋을 추가로 적용
        charRect.localRotation = originalLocalRotation * Quaternion.Euler(offset.rotationOffset);

        // Desk_Set(부모) 배율에 맞춰 diana를 키우는 것이 목적이므로, scaleMultiplier를 localScale로 그대로 사용
        charRect.localScale = Vector3.one * offset.scaleMultiplier;

        Debug.Log($"[ChillMode] 배치 완료 pos={charRect.anchoredPosition3D}, scale={charRect.localScale}, lossyScale={charRect.lossyScale}, parent={charRect.parent.name}");

        // 부모 트랜스폼 급변경으로 클로스(헤어 등) 시뮬레이션이 튀는 것을 방지 (포즈 유지한 채 텔레포트 처리)
        ResetClothTeleport(character);

        // 공용 리타겟팅 애니메이터로 교체 후 착석 상태 재생
        animator.runtimeAnimatorController = chillAnimatorController;
        bool hasState = animator.HasState(0, Animator.StringToHash(chillStateName));
        Debug.Log($"[ChillMode] Animator 교체됨={chillAnimatorController.name}, HasState({chillStateName})={hasState}");
        animator.Play(chillStateName, 0, 0);

        // 배치가 모두 끝난 뒤 다시 활성화
        character.SetActive(true);

        // LookAround 랜덤 트리거 타이머 초기화
        lookAroundTimer = Random.Range(lookAroundMinInterval, lookAroundMaxInterval);

        isChillMode = true;
    }

    // 칠윗유 모드 종료 (원래 상태로 복귀)
    public void ExitChillMode()
    {
        if (!isChillMode)
        {
            // 칠윗유 모드가 아닌 경우 무시
            return;
        }

        // 착석 중 캐릭터가 외부에서 파괴된 경우(의상 스왑 등) — 캐릭터 복원은 건너뛰고 환경만 복원
        // (가드 없이는 MissingReferenceException으로 모드 상태가 영구히 꼬인다)
        if (chillCharacter == null)
        {
            Debug.LogWarning("[ChillMode] 착석 캐릭터가 이미 파괴됨 — 환경만 복원하고 모드를 종료합니다.");
            if (fallingObject != null)
            {
                fallingObject.enabled = true;
            }
            if (PhysicsManager.Instance != null)
            {
                PhysicsManager.Instance.enabled = true;
            }
            if (deskSetRoot != null)
            {
                deskSetRoot.localPosition = deskOriginalPosition;
                deskSetRoot.localRotation = deskOriginalRotation;
                deskSetRoot.localScale = deskOriginalScale;
            }
            if (chairRoot != null)
            {
                chairRoot.localPosition = chairOriginalLocalPosition;
                chairRoot.localRotation = chairOriginalLocalRotation;
            }
            isChillMode = false;
            chillCharacter = null;
            return;
        }

        Animator animator = chillCharacter.GetComponent<Animator>();
        RectTransform charRect = chillCharacter.transform as RectTransform;

        // SetParent 시 중간 좌표가 잠깐 렌더링되는 것을 막기 위해 캐릭터를 잠시 비활성화
        chillCharacter.SetActive(false);

        charRect.SetParent(originalParent, false);
        charRect.anchoredPosition3D = new Vector3(originalAnchoredPosition.x, originalAnchoredPosition.y, originalAnchoredPositionZ);
        charRect.localRotation = originalLocalRotation;
        charRect.localScale = originalLocalScale;
        animator.runtimeAnimatorController = originalController;
        animator.Play("idle", 0, 0);

        // 부모 트랜스폼 복귀로 인한 클로스(헤어 등) 시뮬레이션 튐 방지
        ResetClothTeleport(chillCharacter);

        // 배치가 모두 끝난 뒤 다시 활성화
        chillCharacter.SetActive(true);

        // 낙하 로직 복원
        if (fallingObject != null)
        {
            fallingObject.enabled = true;
        }

        // 좌우 이동 로직 복원 (데모씬 등 매니저 부재 환경 대비 null 가드)
        if (PhysicsManager.Instance != null)
        {
            PhysicsManager.Instance.enabled = true;
        }

        // Desk_Set 원본 상태 복원
        if (deskSetRoot != null)
        {
            deskSetRoot.localPosition = deskOriginalPosition;
            deskSetRoot.localRotation = deskOriginalRotation;
            deskSetRoot.localScale = deskOriginalScale;
        }

        // chairRoot 원본 상태 복원
        if (chairRoot != null)
        {
            chairRoot.localPosition = chairOriginalLocalPosition;
            chairRoot.localRotation = chairOriginalLocalRotation;
        }

        isChillMode = false;
        chillCharacter = null;
    }

    // 부모 변경 직후 MagicaCloth의 시뮬레이션을 현재 포즈 유지한 채 텔레포트 처리
    private void ResetClothTeleport(GameObject character)
    {
        MagicaCloth[] cloths = character.GetComponentsInChildren<MagicaCloth>(true);
        foreach (MagicaCloth cloth in cloths)
        {
            cloth.ResetCloth(true);
        }
    }

    // Chill 모드 중 ChillSitData/Desk_Set 값을 인스펙터에서 조정하는 즉시 확인할 수 있도록 매 프레임 재적용 (에디터 튜닝용, UI 연동 시 applyOffsetEveryFrame을 꺼두고 ApplyCharacterOffset/ApplyDeskOffset을 직접 호출)
    private void Update()
    {
        if (!isChillMode || chillCharacter == null)
        {
            return;
        }

        if (applyOffsetEveryFrame)
        {
            ApplyChairOffset();
            ApplyCharacterOffset();
            ApplyDeskOffset();
        }

        UpdateLookAroundTimer();
    }

    // chairRoot 위치/회전 오프셋을 현재 캐릭터의 chillSitData 값 그대로 즉시 1회 적용 (UI에서 값 변경 시 호출)
    public void ApplyChairOffset()
    {
        if (!isChillMode || chairRoot == null || chillCharacter == null || chillSitData == null)
        {
            return;
        }

        CharAttributes attrs = chillCharacter.GetComponent<CharAttributes>();
        if (attrs == null)
        {
            return;
        }

        ChillSitData.CharacterSitOffset offset = chillSitData.GetOffset(attrs.charcode);
        if (offset == null)
        {
            return;
        }

        chairRoot.localPosition = chairOriginalLocalPosition + offset.chairLocalPosition;
        chairRoot.localRotation = chairOriginalLocalRotation * Quaternion.Euler(offset.chairLocalRotation);
    }

    // 캐릭터 착석 오프셋을 현재 chillSitData 값 그대로 즉시 1회 적용 (UI에서 값 변경 시 호출)
    public void ApplyCharacterOffset()
    {
        if (!isChillMode || chillCharacter == null || chillSitData == null)
        {
            return;
        }

        CharAttributes attrs = chillCharacter.GetComponent<CharAttributes>();
        RectTransform charRect = chillCharacter.transform as RectTransform;
        if (attrs == null || charRect == null)
        {
            return;
        }

        ChillSitData.CharacterSitOffset offset = chillSitData.GetOffset(attrs.charcode);
        if (offset == null)
        {
            return;
        }

        charRect.anchoredPosition3D = offset.positionOffset;
        charRect.localRotation = originalLocalRotation * Quaternion.Euler(offset.rotationOffset);
        charRect.localScale = Vector3.one * offset.scaleMultiplier;
    }

    // Desk_Set 오프셋을 현재 deskPositionOffset/deskRotationOffset/deskScaleMultiplier 값 그대로 즉시 1회 적용 (UI에서 값 변경 시 호출)
    public void ApplyDeskOffset()
    {
        if (!isChillMode || deskSetRoot == null)
        {
            return;
        }

        deskSetRoot.localPosition = deskOriginalPosition + deskPositionOffset;
        deskSetRoot.localRotation = deskOriginalRotation * Quaternion.Euler(deskRotationOffset);
        deskSetRoot.localScale = deskOriginalScale * deskScaleMultiplier;
    }

    // UI에서 의자 오프셋 값을 넘겨받아 현재 캐릭터 전용 항목에 반영하고 즉시 적용 (charcode는 GetCurrentCharacter 기준)
    public void SetChairOffset(Vector3 positionOffset, Vector3 rotationOffset)
    {
        if (chillCharacter == null || chillSitData == null)
        {
            return;
        }

        CharAttributes attrs = chillCharacter.GetComponent<CharAttributes>();
        if (attrs == null)
        {
            return;
        }

        // defaultOffset을 건드리지 않도록 charcode 전용 항목이 없으면 새로 만들어서 그 캐릭터만 튜닝
        ChillSitData.CharacterSitOffset offset = chillSitData.GetOrCreateOffset(attrs.charcode);
        offset.chairLocalPosition = positionOffset;
        offset.chairLocalRotation = rotationOffset;

        ApplyChairOffset();
    }

    // UI에서 캐릭터 오프셋 값을 넘겨받아 chillSitData에 반영하고 즉시 적용 (charcode는 GetCurrentCharacter 기준)
    public void SetCharacterOffset(Vector3 positionOffset, Vector3 rotationOffset, float scaleMultiplier)
    {
        if (chillCharacter == null || chillSitData == null)
        {
            return;
        }

        CharAttributes attrs = chillCharacter.GetComponent<CharAttributes>();
        if (attrs == null)
        {
            return;
        }

        // defaultOffset을 건드리지 않도록 charcode 전용 항목이 없으면 새로 만들어서 그 캐릭터만 튜닝
        ChillSitData.CharacterSitOffset offset = chillSitData.GetOrCreateOffset(attrs.charcode);
        offset.positionOffset = positionOffset;
        offset.rotationOffset = rotationOffset;
        offset.scaleMultiplier = scaleMultiplier;

        ApplyCharacterOffset();
    }

    // UI에서 Desk_Set 오프셋 값을 넘겨받아 즉시 적용
    public void SetDeskOffset(Vector3 positionOffset, Vector3 rotationOffset, float scaleMultiplier)
    {
        deskPositionOffset = positionOffset;
        deskRotationOffset = rotationOffset;
        deskScaleMultiplier = scaleMultiplier;

        ApplyDeskOffset();
    }

    // ---------------------------------------------------------------- 시점 프리셋 (책상 배치 저장 슬롯)

    // 현재 책상 배치(위치/각도/전체 크기)를 시점 슬롯에 저장 (SO 인메모리 — 디스크 저장은 SitSupport [데이터 저장])
    public void SaveViewPreset(int index)
    {
        if (chillSitData == null) return;
        ChillSitData.ViewPreset preset = chillSitData.GetOrCreateViewPreset(index);
        if (preset == null) return;
        preset.isSet = true;
        preset.deskPositionOffset = deskPositionOffset;
        preset.deskRotationOffset = deskRotationOffset;
        preset.deskScaleMultiplier = deskScaleMultiplier;
        Debug.Log($"[ChillMode] 시점 {index + 1} 저장: pos={deskPositionOffset:F1} rot={deskRotationOffset:F1} scale={deskScaleMultiplier:0.#}");
    }

    // 시점 슬롯의 책상 배치로 전환. 착석 중이면 시트(착석 지점)를 고정점으로 부드럽게 전환하고,
    // 비착석/전환시간 0이면 값만 즉시 반영(다음 착석 때 적용). 빈 슬롯이면 false.
    public bool ApplyViewPreset(int index)
    {
        if (chillSitData == null) return false;
        ChillSitData.ViewPreset preset = chillSitData.GetViewPreset(index);
        if (preset == null || !preset.isSet)
        {
            Debug.Log($"[ChillMode] 시점 {index + 1}이 비어 있습니다. 먼저 저장하세요.");
            return false;
        }

        if (viewTransitionCoroutine != null)
        {
            StopCoroutine(viewTransitionCoroutine);
            viewTransitionCoroutine = null;
        }

        if (!isChillMode || viewTransitionSeconds <= 0f)
        {
            SetDeskPose(preset.deskPositionOffset, preset.deskRotationOffset, preset.deskScaleMultiplier);
            return true;
        }

        viewTransitionCoroutine = StartCoroutine(TransitionToViewPreset(preset));
        return true;
    }

    // 시트(착석 캐릭터)를 고정점으로 유지하며 현재 배치 → 프리셋 배치로 보간.
    // 캐릭터는 제자리(앵커 사이를 직선 이동)에 머물고 책상이 주위로 회전/확대되는 "시점 전환" 연출.
    private IEnumerator TransitionToViewPreset(ChillSitData.ViewPreset preset)
    {
        // 시트의 데스크 로컬 좌표 — 전환 동안 의자 오프셋은 불변이므로 상수
        bool hasSeat = chairSeatPoint != null && deskSetRoot != null;
        Vector3 seatLocal = hasSeat ? deskSetRoot.InverseTransformPoint(chairSeatPoint.position) : Vector3.zero;

        Vector3 pos0 = deskPositionOffset;
        Quaternion rot0 = Quaternion.Euler(deskRotationOffset);
        float scale0 = deskScaleMultiplier;
        Quaternion rot1 = Quaternion.Euler(preset.deskRotationOffset);
        float scale1 = preset.deskScaleMultiplier;

        // 시트 앵커(시트가 놓이는 좌표) 기준으로 보간해야 중간 프레임에서 캐릭터가 흘러다니지 않는다
        Vector3 anchor0 = pos0 + rot0 * (seatLocal * scale0);
        Vector3 anchor1 = preset.deskPositionOffset + rot1 * (seatLocal * scale1);

        float elapsed = 0f;
        while (elapsed < viewTransitionSeconds)
        {
            if (!isChillMode) break; // 전환 중 기립하면 중단하고 최종값만 남긴다

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / viewTransitionSeconds));
            Quaternion rot = Quaternion.Slerp(rot0, rot1, t);
            float scale = Mathf.Lerp(scale0, scale1, t);
            Vector3 anchor = Vector3.Lerp(anchor0, anchor1, t);
            Vector3 pos = hasSeat
                ? anchor - rot * (seatLocal * scale)
                : Vector3.Lerp(pos0, preset.deskPositionOffset, t);
            SetDeskPose(pos, rot.eulerAngles, scale);
            yield return null;
        }

        // 종료 시 프리셋 값으로 정확히 스냅 (보간 오차/오일러 표현 차이 제거)
        SetDeskPose(preset.deskPositionOffset, preset.deskRotationOffset, preset.deskScaleMultiplier);
        viewTransitionCoroutine = null;
    }

    // 책상 배치 일괄 갱신 — 단일 출처인 ChillSitData도 함께 동기
    private void SetDeskPose(Vector3 positionOffset, Vector3 rotationOffset, float scaleMultiplier)
    {
        deskPositionOffset = positionOffset;
        deskRotationOffset = rotationOffset;
        deskScaleMultiplier = scaleMultiplier;
        if (chillSitData != null)
        {
            chillSitData.deskPositionOffset = positionOffset;
            chillSitData.deskRotationOffset = rotationOffset;
            chillSitData.deskScaleMultiplier = scaleMultiplier;
        }
        ApplyDeskOffset();
    }

    // 랜덤 주기로 SitLookAround 트리거 발동
    private void UpdateLookAroundTimer()
    {
        lookAroundTimer -= Time.deltaTime;
        if (lookAroundTimer <= 0f)
        {
            lookAroundTimer = Random.Range(lookAroundMinInterval, lookAroundMaxInterval);

            Animator animator = chillCharacter.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger(lookAroundTriggerName);
            }
        }
    }

    // 배경 음악 재생 (재생 로직은 추후 구현)
    public void PlayMusic()
    {
    }

    // 진입/종료 토글
    public void ToggleChillMode()
    {
        if (isChillMode)
        {
            ExitChillMode();
        }
        else
        {
            EnterChillMode();
        }
    }
}
