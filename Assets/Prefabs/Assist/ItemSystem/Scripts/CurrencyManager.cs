using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 재화(돈) 상시 관리 싱글톤 — 모든 재화 잔액의 공식 소유자.
//
// 역할 분담:
//   - ItemCurrencyCatalog(Resources): 재화의 "정의"(이름/아이콘/설명/프리미엄) — 등재된 키만 증감 허용.
//   - CurrencyManager(여기): 잔액·누적(earned/spent)·저장(persistentDataPath/ItemSystem/currency.json).
//     특정 재화만 다른 시스템에 위탁하는 특례는 두지 않는다 — 전 재화가 같은 코드 경로를 탄다.
//
// 레거시 골드 브리지 (공존용 — 제거 대상):
//   기존 골드는 Mission의 InventoryManager(inventory.json)가 저장해 왔고, 상점 골드 라벨과
//   미션 집계(CH0001/CH0007)가 그 잔액·누적·InventoryChanged 이벤트에 결합돼 있다.
//   그래서 레거시 지갑이 살아 있는 동안에는 골드 증감을 레거시 지갑에 통과시키고(트랜잭션 실행자),
//   그 결과를 이 지갑이 즉시 채택(양방향 동기화)한다 — 어느 쪽 경로로 골드가 바뀌어도 두 지갑은 수렴한다.
//   골드 콜사이트가 전부 이 매니저로 이관되고 미션 집계가 여기를 구독하게 되면 브리지를 제거한다.
public class CurrencyManager : MonoBehaviour
{
    // ── 재화 키 (ItemCurrencyCatalog 등재 키와 동일 문자열) ──
    public const string GoldKey = "currency_gold";

    // ── 차후 재화 추가 방법 (예: Gem) ──
    // 지갑/증감/저장 로직은 전부 키 기반이라 코드 추가가 거의 없다. 두 곳만 만지면 된다:
    //   ① ItemSystemTools.CreateCurrencyCatalog의 배열에 한 줄 추가 (그 파일의 Gem 주석 예시 참조)
    //      — 또는 인스펙터에서 Resources/ItemCurrencyCatalog.asset 에 엔트리를 직접 추가.
    //   ② 아래 키 상수를 활성화하고 호출부에서 사용.
    // public const string GemKey = "currency_gem";
    // 사용 예:
    //   CurrencyManager.Instance.Add(CurrencyManager.GemKey, 10);
    //   CurrencyManager.Instance.Spend(CurrencyManager.GemKey, 3);
    //   int gems = CurrencyManager.Instance.GetBalance(CurrencyManager.GemKey);
    // (신규 재화는 레거시 브리지와 무관 — 처음부터 이 지갑에 네이티브 저장된다)

    private static CurrencyManager _instance;

    public static CurrencyManager Instance
    {
        get
        {
            // 에디트 모드(비플레이)에서는 항상 null — 베이크/에디터 경로가 매니저 없이 돌아야 한다
            if (_instance == null && Application.isPlaying)
            {
                _instance = FindFirstObjectByType<CurrencyManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CurrencyManager");
                    _instance = go.AddComponent<CurrencyManager>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
    }

    public event Action<string> CurrencyChanged;  // (currencyKey) — 잔액 변경 브로드캐스트 (UI 갱신 구독점)

    // 재화 1종의 지갑 상태
    [Serializable]
    public class CurrencyState
    {
        public string key;
        public int balance;
        public int earnedTotal;  // 누적 획득 (미션류 파생 집계 대비)
        public int spentTotal;   // 누적 소비
    }

    [Serializable]
    private class CurrencySaveData
    {
        public List<CurrencyState> currencies = new List<CurrencyState>();
    }

    // 저장 파일 래퍼 — payload(잔액 JSON) + checksum(변조/손상 감지).
    // 체크섬은 캐주얼 변조(메모장 수정)와 파일 손상 감지용 — salt가 클라이언트에 내장돼 있어 완전 방어는 아니다.
    [Serializable]
    private class CurrencySaveFile
    {
        public string payload;
        public string checksum;
    }

    private const string ChecksumSalt = "AICO.CurrencyManager.v1";  // 체크섬 salt (변경 시 기존 저장 전부 불일치 — 버전 올릴 때만)

    private CurrencySaveData data = new CurrencySaveData();
    private ItemCurrencyCatalog catalog;          // 재화 정의 카탈로그 (Resources)
    private InventoryManager subscribedLegacyWallet;  // 브리지 구독 당시 참조 (OnDestroy 해제용)

    private bool warnedNoCatalog;  // 카탈로그 부재 경고 1회 래치

    private string SavePath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "ItemSystem", "currency.json");
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[ItemSystem][CurrencyManager] 인스턴스가 이미 존재합니다. 중복 매니저를 파괴합니다.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        catalog = Resources.Load<ItemCurrencyCatalog>("ItemCurrencyCatalog");
        Load();

        // 레거시 골드 브리지: 지갑 이벤트 구독 + 부팅 시점 채택 (레거시 저장이 골드의 기존 진실)
        if (Application.isPlaying)
        {
            subscribedLegacyWallet = InventoryManager.Instance;
            if (subscribedLegacyWallet != null)
            {
                subscribedLegacyWallet.InventoryChanged += OnLegacyWalletChanged;
                SyncFromLegacyWallet();
            }
        }
    }

    private void OnDestroy()
    {
        // 종료 중 Instance getter를 부르면 지갑이 재생성될 수 있어, 구독 당시 참조로만 해제한다
        if (subscribedLegacyWallet != null)
        {
            subscribedLegacyWallet.InventoryChanged -= OnLegacyWalletChanged;
            subscribedLegacyWallet = null;
        }

        if (_instance == this)
        {
            _instance = null;
        }
    }

    // ── 공개 API ──

    // 잔액 조회 (미등록 키/미보유 재화는 0)
    public int GetBalance(string currencyKey)
    {
        CurrencyState state = Find(currencyKey);
        return state != null ? state.balance : 0;
    }

    // 편의 접근자 — 골드 잔액
    public int Gold
    {
        get
        {
            return GetBalance(GoldKey);
        }
    }

    // 재화 적립. 카탈로그 미등재 키는 거부(오타 키로 잔액이 생기는 사고 차단).
    public bool Add(string currencyKey, int amount)
    {
        if (amount <= 0 || IsKnownCurrency(currencyKey) == false)
        {
            return false;
        }

        // 레거시 브리지: 골드는 레거시 지갑이 살아 있는 동안 그쪽을 트랜잭션 실행자로 쓴다
        // (미션 집계·기존 UI가 그 이벤트에 결합) — 결과는 이벤트 → SyncFromLegacyWallet 으로 채택된다
        InventoryManager legacy = ResolveLegacyWalletForGold(currencyKey);
        if (legacy != null)
        {
            legacy.AddGold(amount);
            return true;
        }

        CurrencyState state = FindOrCreate(currencyKey);
        state.balance = state.balance + amount;
        state.earnedTotal = state.earnedTotal + amount;
        Persist(currencyKey);
        return true;
    }

    // 재화 차감 — 잔액 부족/미등재 키면 false
    public bool Spend(string currencyKey, int amount)
    {
        if (amount <= 0 || IsKnownCurrency(currencyKey) == false)
        {
            return false;
        }

        InventoryManager legacy = ResolveLegacyWalletForGold(currencyKey);
        if (legacy != null)
        {
            return legacy.SpendGold(amount);
        }

        CurrencyState state = Find(currencyKey);
        if (state == null || state.balance < amount)
        {
            return false;
        }

        state.balance = state.balance - amount;
        state.spentTotal = state.spentTotal + amount;
        Persist(currencyKey);
        return true;
    }

    // 누적 획득/소비 조회 (미션류 파생 집계 대비)
    public int GetEarnedTotal(string currencyKey)
    {
        CurrencyState state = Find(currencyKey);
        return state != null ? state.earnedTotal : 0;
    }

    public int GetSpentTotal(string currencyKey)
    {
        CurrencyState state = Find(currencyKey);
        return state != null ? state.spentTotal : 0;
    }

    // ── 레거시 골드 브리지 ──

    // 골드이고 레거시 지갑이 살아 있으면 그 지갑을 반환 (그 외 null = 네이티브 경로)
    private InventoryManager ResolveLegacyWalletForGold(string currencyKey)
    {
        if (currencyKey != GoldKey)
        {
            return null;
        }

        // 매 호출 조회 — 씬 전환으로 사라질 수 있다 (사라지면 네이티브 경로로 자연 전환)
        return Application.isPlaying ? InventoryManager.Instance : null;
    }

    private void OnLegacyWalletChanged()
    {
        SyncFromLegacyWallet();
    }

    // 레거시 지갑의 골드 잔액·누적을 이 지갑에 채택 (값이 같으면 무동작 — 이벤트 루프 없음)
    private void SyncFromLegacyWallet()
    {
        InventoryManager legacy = subscribedLegacyWallet != null ? subscribedLegacyWallet : InventoryManager.Instance;
        if (legacy == null)
        {
            return;
        }

        CurrencyState state = FindOrCreate(GoldKey);
        if (state.balance == legacy.Gold
            && state.earnedTotal == legacy.GoldEarnedTotal
            && state.spentTotal == legacy.GoldSpentTotal)
        {
            return;
        }

        state.balance = legacy.Gold;
        state.earnedTotal = legacy.GoldEarnedTotal;
        state.spentTotal = legacy.GoldSpentTotal;
        Persist(GoldKey);
    }

    // ── 내부 ──

    private bool IsKnownCurrency(string currencyKey)
    {
        if (string.IsNullOrEmpty(currencyKey))
        {
            return false;
        }

        if (catalog == null)
        {
            if (warnedNoCatalog == false)
            {
                warnedNoCatalog = true;
                Debug.LogWarning("[ItemSystem][CurrencyManager] Resources/ItemCurrencyCatalog 이 없습니다 — 'Tools/ItemSystem/1. Create Catalog'로 베이크하세요. 재화 증감이 전부 거부됩니다.");
            }
            return false;
        }

        return catalog.Contains(currencyKey);
    }

    private CurrencyState Find(string currencyKey)
    {
        foreach (CurrencyState state in data.currencies)
        {
            if (state != null && state.key == currencyKey)
            {
                return state;
            }
        }

        return null;
    }

    private CurrencyState FindOrCreate(string currencyKey)
    {
        CurrencyState state = Find(currencyKey);
        if (state == null)
        {
            state = new CurrencyState { key = currencyKey };
            data.currencies.Add(state);
        }

        return state;
    }

    // 저장 + 변경 통지. 쓰기는 원자적(temp에 쓴 뒤 교체) — 도중 크래시로 파일이 반토막 나는 사고 방지.
    private void Persist(string changedKey)
    {
        if (Application.isPlaying)
        {
            try
            {
                string dir = Path.GetDirectoryName(SavePath);
                if (Directory.Exists(dir) == false)
                {
                    Directory.CreateDirectory(dir);
                }

                CurrencySaveFile file = new CurrencySaveFile();
                file.payload = JsonUtility.ToJson(data);
                file.checksum = ComputeChecksum(file.payload);

                string tmpPath = SavePath + ".tmp";
                File.WriteAllText(tmpPath, JsonUtility.ToJson(file, true));
                if (File.Exists(SavePath))
                {
                    File.Replace(tmpPath, SavePath, null);
                }
                else
                {
                    File.Move(tmpPath, SavePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ItemSystem][CurrencyManager] 저장 실패: " + e.Message);
            }
        }

        CurrencyChanged?.Invoke(changedKey);
    }

    private void Load()
    {
        string path = SavePath;
        if (File.Exists(path) == false)
        {
            return;
        }

        try
        {
            string text = File.ReadAllText(path);

            CurrencySaveFile file = JsonUtility.FromJson<CurrencySaveFile>(text);
            if (file == null || string.IsNullOrEmpty(file.payload))
            {
                // 래퍼 이전(체크섬 도입 전) 구 포맷 — 1회 수용하고 다음 저장부터 신 포맷으로 이행
                CurrencySaveData legacyData = JsonUtility.FromJson<CurrencySaveData>(text);
                if (legacyData != null && legacyData.currencies != null)
                {
                    data = legacyData;
                }
                return;
            }

            // 체크섬 불일치 = 변조 또는 손상 — 로그 후 지갑을 명시적으로 초기화한다.
            // (골드는 부팅 시 레거시 지갑 브리지가 채택하므로 실손실 없음. 네이티브 재화만 0이 된다)
            if (ComputeChecksum(file.payload) != file.checksum)
            {
                Debug.Log("[ItemSystem][CurrencyManager] 저장 파일 체크섬 불일치 — 변조/손상. 지갑을 초기화합니다: " + path);
                ResetWallet();
                return;
            }

            CurrencySaveData loaded = JsonUtility.FromJson<CurrencySaveData>(file.payload);
            if (loaded != null && loaded.currencies != null)
            {
                data = loaded;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemSystem][CurrencyManager] 로드 실패: " + e.Message);
        }
    }

    // 지갑 초기화: 전 재화 잔액·누적을 0으로 만들고(골드 포함) 새 체크섬으로 저장 파일을 재생성한다.
    // 체크섬 불일치 시 자동 호출되며, 필요하면 외부에서 직접 불러도 된다(디버그/초기화 UX 등).
    public void ResetWallet()
    {
        data = new CurrencySaveData();
        Persist(GoldKey);  // 빈 payload + 새 checksum 기록 (재화가 골드뿐이라 변경 통지도 GoldKey로)
    }

    // payload + salt의 SHA-256 (Base64) — 캐주얼 변조/파일 손상 감지용
    private static string ComputeChecksum(string payload)
    {
        using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload + ChecksumSalt));
            return Convert.ToBase64String(hash);
        }
    }
}
