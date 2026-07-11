using UnityEngine;

// 아이템 시스템 상시 파사드 싱글톤 — "아이템의 출입은 한 문으로".
//
// 역할 분담:
//   - ItemSystem(여기): 아이템의 정체(무엇인가)·능력(무엇을 하는가)의 단일 출처.
//     ItemCatalog(카테고리 레지스트리) → 카테고리별 하위 카탈로그(ItemGiftCatalog 등)를 조회한다.
//   - InventorySystem(InventorySystemManager): 보관·스택. 부여(GrantItem)는 여기로 위임한다.
//   - CurrencyManager: 재화(골드 등) 잔액의 공식 소유자 — 돈 관리는 이 파사드가 아니라 그쪽이다.
//     (재화 "정의"는 ItemCurrencyCatalog가 소유 — 같은 레지스트리의 "재화" 카테고리)
//   전부 문자열 key 규약을 공유한다 (Inventory/Equip/Store 카탈로그와 같은 키 공간).
//
// 후속 이관 계획(이번 라운드는 파사드만 — 기존 콜사이트는 건드리지 않는다):
//   - 콜사이트 스위칭: 아이템 지급 지점 → 이 파사드, 골드 변경 지점 → CurrencyManager.
//   - 판매 차감 API 신설: StoreView 판매의 캡슐화 우회를 파사드 차감 API로 해소.
//   - InventoryCatalog를 ItemCatalog 파생물로 격하하는 옵션 검토.
//
// 위임 대상 매니저는 씬 전환으로 사라질 수 있어 매 호출 Instance 로 조회한다(참조 캐시 금지).
public class ItemSystemManager : MonoBehaviour
{
    private static ItemSystemManager _instance;

    public static ItemSystemManager Instance
    {
        get
        {
            // 에디트 모드(비플레이)에서는 항상 null — 프리팹 베이크 경로가 매니저 없이 돌아야 한다
            if (_instance == null && Application.isPlaying)
            {
                _instance = FindFirstObjectByType<ItemSystemManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ItemSystemManager");
                    _instance = go.AddComponent<ItemSystemManager>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
    }

    private ItemCatalog itemCatalog;  // 카테고리 레지스트리 (Resources) — 정체·능력의 단일 출처

    private bool warnedNoCatalog;          // 카탈로그 부재 경고 1회 래치 (스팸 방지)
    private bool warnedNoInventorySystem;  // 보관 매니저 부재 경고 1회 래치

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[ItemSystem][ItemSystemManager] 인스턴스가 이미 존재합니다. 중복 매니저를 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        itemCatalog = Resources.Load<ItemCatalog>("ItemCatalog");
    }

    // ── 마스터 데이터 조회 (Catalog가 null이면 전부 null/false/0 — 경고 1회) ──

    // 카테고리 레지스트리 — 없으면 null (Tools/ItemSystem/1. Create Catalog 로 베이크)
    public ItemCatalog Catalog
    {
        get
        {
            return ResolveCatalog();
        }
    }

    // 키의 공통 정체 엔트리 — 미등재면 null
    public ItemEntry GetItem(string key)
    {
        ItemCatalog catalog = ResolveCatalog();
        if (catalog == null)
        {
            return null;
        }

        return catalog.Get(key);
    }

    // 키 등재 여부
    public bool Contains(string key)
    {
        ItemCatalog catalog = ResolveCatalog();
        if (catalog == null)
        {
            return false;
        }

        return catalog.Contains(key);
    }

    // 키가 속한 카테고리 이름 — 미등재면 null
    public string CategoryForKey(string key)
    {
        ItemCatalog catalog = ResolveCatalog();
        if (catalog == null)
        {
            return null;
        }

        return catalog.CategoryForKey(key);
    }

    // 선물 증정 시 인연도(affinity) 상승량 — 미등재/비선물이면 0
    public int GetGiftAffinityPoints(string key)
    {
        ItemCatalog catalog = ResolveCatalog();
        if (catalog == null)
        {
            return 0;
        }

        int affinityPoints;
        if (catalog.TryGetGiftPoints(key, out affinityPoints) == false)
        {
            return 0;
        }

        return affinityPoints;
    }

    // ── 부여 파사드 — 저장·스택은 InventorySystemManager 소유 ──
    // 목적: "아이템의 출입은 한 문으로" — 호출자는 이 파사드만 알면 된다 (콜사이트 이관은 후속).
    // 재화(골드 등)의 증감·잔액은 CurrencyManager 담당 — 여기서는 다루지 않는다.

    // 공용(MAIN) 스토어에 아이템 부여 — 보관 매니저 부재 시 false
    public bool GrantItem(string key, int amount)
    {
        InventorySystemManager inventory = ResolveInventorySystem();
        if (inventory == null)
        {
            return false;
        }

        return inventory.AddToMain(key, amount);
    }

    // 캐릭터 스토어에 아이템 부여 — 보관 매니저 부재 시 false
    public bool GrantItemToChar(string charcode, string key, int amount)
    {
        InventorySystemManager inventory = ResolveInventorySystem();
        if (inventory == null)
        {
            return false;
        }

        return inventory.AddToChar(charcode, key, amount);
    }

    // ── 위임 대상 해석 (매 호출 Instance 조회 + 부재 경고 1회 래치) ──

    // 카탈로그 해석 — 로드는 Awake 1회, 부재 시 경고 1회 후 null 유지 (StoreManager 관용구)
    private ItemCatalog ResolveCatalog()
    {
        if (itemCatalog == null && warnedNoCatalog == false)
        {
            warnedNoCatalog = true;
            Debug.LogWarning("[ItemSystem][ItemSystemManager] Resources/ItemCatalog 이 없습니다 — 'Tools/ItemSystem/1. Create Catalog'로 베이크하세요. 조회는 전부 null/false/0 을 반환합니다.");
        }

        return itemCatalog;
    }

    private InventorySystemManager ResolveInventorySystem()
    {
        InventorySystemManager inventory = InventorySystemManager.Instance;
        if (inventory == null && warnedNoInventorySystem == false)
        {
            warnedNoInventorySystem = true;
            Debug.LogWarning("[ItemSystem][ItemSystemManager] InventorySystemManager 가 없습니다 — 아이템 부여가 무시됩니다.");
        }

        return inventory;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
