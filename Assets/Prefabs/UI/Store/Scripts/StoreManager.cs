using System.Collections.Generic;
using UnityEngine;

// 상점 상시 서비스 싱글톤 — 프리뷰 아이콘 캐시/리롤/NoImage 폴백을 창(StoreView) 수명과 분리해 관리한다.
// 아이콘 해석은 상점 카탈로그(StoreEntry.iconType) 소유: File이면 등록 스프라이트, Runtime이면 프리뷰 캡처 캐시.
// 상점 아이콘은 인벤토리 UI 아이콘과 완전 별개 — InventoryCatalog를 조회하지 않는다.
// 리그(StorePosePreviewRig)는 씬 전환으로 사라질 수 있어 매 호출 Instance 로 조회한다(참조 캐시 금지).
public class StoreManager : MonoBehaviour
{
    private static StoreManager _instance;

    public static StoreManager Instance
    {
        get
        {
            // 에디트 모드(비플레이)에서는 항상 null — 프리팹 베이크 경로가 매니저 없이 돌아야 한다
            if (_instance == null && Application.isPlaying)
            {
                _instance = FindFirstObjectByType<StoreManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("StoreManager");
                    _instance = go.AddComponent<StoreManager>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
    }

    public event System.Action<string, Sprite> IconReady;  // (key, sprite) — 프리뷰 캡처 완료/리롤 갱신 브로드캐스트

    private StoreCatalog storeCatalog;                     // 태그 레지스트리 (Resources) — 아이콘 소유 카탈로그
    private StoreDetailPoseCatalog detailPoseCatalog;      // 포즈 키→클립 상세 카탈로그 (Resources)
    private StoreDetailEffectCatalog detailEffectCatalog;  // 이펙트 키→프리팹 상세 카탈로그 (Resources)

    private readonly Dictionary<string, Sprite> previewCache = new Dictionary<string, Sprite>();  // key→캡처 스프라이트
    private readonly HashSet<string> pendingKeys = new HashSet<string>();  // 캡처 요청 중 (중복 요청 방지)
    private readonly HashSet<string> failedKeys = new HashSet<string>();   // 캡처 실패 확정 (재요청 방지, 리롤로만 해제)

    private Sprite noImageSprite;    // NoImage 폴백 캐시
    private bool noImageLoadTried;   // Resources 로드는 1회만 시도 (실패 시 경고 1회 후 null 유지 — 런타임 생성 폴백 없음)

    // NoImage 폴백 스프라이트: 베이크된 Resources/StoreNoImage.png 단일 소스.
    // 없으면 null 반환 — 호출측(카드/모달)이 아이콘을 숨기고 이름 텍스트로 폴백한다.
    public Sprite NoImageSprite
    {
        get
        {
            if (noImageLoadTried == false)
            {
                noImageLoadTried = true;
                noImageSprite = Resources.Load<Sprite>("StoreNoImage");
                if (noImageSprite == null)
                {
                    Debug.LogWarning("[Store][StoreManager] Resources/StoreNoImage 스프라이트가 없습니다 — 'Tools/Store/1. Create Catalog'로 베이크하세요. 아이콘 없는 카드는 이름 텍스트로 폴백합니다.");
                }
            }

            return noImageSprite;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[Store][StoreManager] 인스턴스가 이미 존재합니다. 중복 매니저를 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        storeCatalog = Resources.Load<StoreCatalog>("StoreCatalog");
        detailPoseCatalog = Resources.Load<StoreDetailPoseCatalog>("StoreDetailPoseCatalog");
        detailEffectCatalog = Resources.Load<StoreDetailEffectCatalog>("StoreDetailEffectCatalog");
    }

    // ── 공개 API ──

    // 실아이콘 해석: 상점 엔트리의 iconType 기준 — File이면 등록 스프라이트, Runtime이면 프리뷰 캐시 → null.
    // 상점 아이콘은 상점 카탈로그 소유 — 인벤토리 UI 아이콘과 별개라 InventoryCatalog는 조회하지 않는다.
    // null = "실아이콘 없음" 의미 — NoImage 는 여기서 반환하지 않는다(호출측이 씌움).
    public Sprite ResolveIcon(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (storeCatalog == null)
        {
            return null;
        }

        StoreEntry entry = storeCatalog.Get(key);
        if (entry == null)
        {
            return null;
        }

        // File 모드는 등록 스프라이트가 곧 실아이콘 — 비어 있으면 null(호출측이 NoImage를 씌움)
        if (entry.iconType == StoreIconType.File)
        {
            return entry.icon;
        }

        // Runtime 모드는 프리뷰 캡처 캐시만 본다
        if (previewCache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        return null;
    }

    // 프리뷰 캡처 대상 키인지 — 상점 엔트리가 Runtime 모드이고 Detail 카탈로그(포즈/이펙트)에 등재된 키만.
    // Detail 미등재 Runtime 키는 캡처 불가 → false → NoImage로 정착한다.
    public bool IsPreviewKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (storeCatalog == null)
        {
            return false;
        }

        StoreEntry entry = storeCatalog.Get(key);
        if (entry == null || entry.iconType != StoreIconType.Runtime)
        {
            return false;
        }

        if (detailPoseCatalog != null && detailPoseCatalog.Contains(key))
        {
            return true;
        }

        if (detailEffectCatalog != null && detailEffectCatalog.Contains(key))
        {
            return true;
        }

        return false;
    }

    // 프리뷰 캡처 요청 (비 Runtime 모드/캐시 히트/요청 중/실패 확정/리그 부재 시 무동작)
    public void RequestPreview(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        // Runtime 모드가 아니거나 Detail 미등재인 키는 캡처 대상이 아니다
        if (IsPreviewKey(key) == false)
        {
            return;
        }

        if (previewCache.ContainsKey(key))
        {
            return;
        }

        if (pendingKeys.Contains(key))
        {
            return;
        }

        if (failedKeys.Contains(key))
        {
            return;
        }

        StorePosePreviewRig rig = StorePosePreviewRig.Instance;
        if (rig == null || rig.IsDisabled)
        {
            return;
        }

        if (detailPoseCatalog != null)
        {
            StoreDetailPoseEntry poseEntry = detailPoseCatalog.Get(key);
            if (poseEntry != null)
            {
                // 리그가 실패를 동기 콜백할 수 있어 pending 등록이 요청보다 먼저여야 한다
                pendingKeys.Add(key);
                rig.RequestPoseCapture(poseEntry, OnPoseCaptured);
                return;
            }
        }

        if (detailEffectCatalog != null)
        {
            StoreDetailEffectEntry effectEntry = detailEffectCatalog.Get(key);
            if (effectEntry != null)
            {
                pendingKeys.Add(key);
                rig.RequestEffectCapture(effectEntry, OnEffectCaptured);
            }
        }
    }

    // 포즈 전 엔트리 강제 재캡처 (정지 시점이 랜덤이라 리롤마다 다른 프리뷰가 나온다)
    public void RerollPoses()
    {
        if (detailPoseCatalog == null)
        {
            return;
        }

        // 레지스트리 부재면 Runtime 모드 여부를 판정할 수 없다 — 재캡처하지 않는다
        if (storeCatalog == null)
        {
            return;
        }

        StorePosePreviewRig rig = StorePosePreviewRig.Instance;
        if (rig == null || rig.IsDisabled)
        {
            return;
        }

        foreach (StoreDetailPoseEntry entry in detailPoseCatalog.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            // 상점 엔트리가 Runtime 모드인 키만 재캡처 — 미등재/File 모드는 리롤 대상이 아니다
            StoreEntry storeEntry = storeCatalog.Get(entry.key);
            if (storeEntry == null || storeEntry.iconType != StoreIconType.Runtime)
            {
                continue;
            }

            // 캐시 유무와 무관하게 강제 재캡처 — 실패/대기 상태를 풀고 다시 pending 등록
            failedKeys.Remove(entry.key);
            pendingKeys.Remove(entry.key);
            pendingKeys.Add(entry.key);
            rig.RequestPoseCapture(entry, OnPoseCaptured);
        }
    }

    // ── 캡처 완료 콜백 ──

    private void OnPoseCaptured(StoreDetailPoseEntry entry, Sprite sprite)
    {
        if (entry == null)
        {
            return;
        }

        HandleCaptureResult(entry.key, sprite);
    }

    private void OnEffectCaptured(StoreDetailEffectEntry entry, Sprite sprite)
    {
        if (entry == null)
        {
            return;
        }

        HandleCaptureResult(entry.key, sprite);
    }

    // 캡처 결과 공통 처리: 실패면 failed 등록, 성공이면 캐시 갱신 → IconReady 발화 → 옛 스프라이트 파괴
    private void HandleCaptureResult(string key, Sprite sprite)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (sprite == null)
        {
            pendingKeys.Remove(key);
            failedKeys.Add(key);
            return;
        }

        previewCache.TryGetValue(key, out Sprite old);
        previewCache[key] = sprite;
        pendingKeys.Remove(key);

        // 옛 스프라이트 파괴는 반드시 브로드캐스트 뒤에 — 구독자(카드/모달)가 새 스프라이트로
        // 교체를 마치기 전에 파괴하면 파괴된 참조가 흰 사각형으로 그려진다.
        System.Action<string, Sprite> handler = IconReady;
        if (handler != null)
        {
            handler(key, sprite);
        }

        if (old != null && old != sprite)
        {
            if (old.texture != null)
            {
                Destroy(old.texture);
            }
            Destroy(old);
        }
    }

    // ── 정리 ──

    private void OnDestroy()
    {
        // 캡처 텍스처/스프라이트 정리 (씬 언로드로는 자동 해제되지 않음)
        foreach (Sprite sprite in previewCache.Values)
        {
            if (sprite == null)
            {
                continue;
            }
            if (sprite.texture != null)
            {
                Destroy(sprite.texture);
            }
            Destroy(sprite);
        }
        previewCache.Clear();
        pendingKeys.Clear();
        failedKeys.Clear();

        if (_instance == this)
        {
            _instance = null;
        }
    }
}
