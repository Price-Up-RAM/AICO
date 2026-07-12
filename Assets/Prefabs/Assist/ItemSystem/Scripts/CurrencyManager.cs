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
// 골드 단일화 완료 (구 레거시 브리지 제거됨):
//   구 Mission InventoryManager(inventory.json) 지갑은 삭제됐고, 골드도 다른 재화와 같은
//   네이티브 경로를 탄다. 구 저장분은 currency.json이 아직 없는 첫 부팅에만 1회 이관된다
//   (ImportLegacyGoldIfNeeded — 브리지 시절을 거친 기기는 매 세션 동기화로 이미 값이 넘어와 있다).
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
    //   CurrencyManager.Instance.Earn(CurrencyManager.GemKey, 10);    // 소득 — earnedTotal 집계
    //   CurrencyManager.Instance.Spend(CurrencyManager.GemKey, 3);    // 소비 — spentTotal 집계
    //   CurrencyManager.Instance.Refund(CurrencyManager.GemKey, 3);   // 실패 결제 되돌림 — spentTotal 역가산
    //   CurrencyManager.Instance.Add(CurrencyManager.GemKey, -2);     // 순수 잔액 변경(보정) — 집계 무반응
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
        ImportLegacyGoldIfNeeded();
    }

    private void OnDestroy()
    {
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

    // 재화 소득(적립) — earnedTotal 집계 경로. "번 돈"으로 취급되는 수입만 이걸 쓴다.
    // 양수만 허용, 카탈로그 미등재 키는 거부(오타 키로 잔액이 생기는 사고 차단).
    public bool Earn(string currencyKey, int amount)
    {
        if (amount <= 0 || IsKnownCurrency(currencyKey) == false)
        {
            return false;
        }

        CurrencyState state = FindOrCreate(currencyKey);
        state.balance = state.balance + amount;
        state.earnedTotal = state.earnedTotal + amount;
        Persist(currencyKey);
        return true;
    }

    // 실패 결제 되돌림 — 잔액 복원 + spentTotal 역가산(하한 0). "쓴 돈 취소"에만 쓴다.
    // (Spend 성공 후 후속 처리가 실패했을 때의 짝 — 미션류 소비 집계가 함께 되돌아간다)
    public bool Refund(string currencyKey, int amount)
    {
        if (amount <= 0 || IsKnownCurrency(currencyKey) == false)
        {
            return false;
        }

        CurrencyState state = FindOrCreate(currencyKey);
        state.balance = state.balance + amount;
        state.spentTotal = Mathf.Max(0, state.spentTotal - amount);
        Persist(currencyKey);
        return true;
    }

    // 순수 잔액 변경 — 소득/소비 누적(earned/spent)에 반영되지 않는다(보정/치트성 조정용 — 환불은 Refund).
    // 음수 허용, 잔액은 0 밑으로 내려가지 않는다. 카탈로그 미등재 키는 거부.
    public bool Add(string currencyKey, int amount)
    {
        if (amount == 0 || IsKnownCurrency(currencyKey) == false)
        {
            return false;
        }

        CurrencyState state = FindOrCreate(currencyKey);
        state.balance = Mathf.Max(0, state.balance + amount);
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

    // ── 레거시 골드 1회 이관 ──

    // inventory.json(구 Mission InventoryManager) 판독용 미러 — 이관 후에도 파일은 지우지 않는다(무해한 잔존물)
    [Serializable]
    private class LegacyInventoryData
    {
        public int gold;
        public int goldEarnedTotal;
        public int goldSpentTotal;
    }

    // currency.json이 아직 없는 첫 부팅에만 구 지갑(inventory.json)의 골드를 채택한다.
    // 브리지 시절을 거친 기기는 매 세션 동기화로 currency.json에 이미 값이 있어 이 경로를 타지 않는다.
    // (currency.json 존재 = 이관 완료로 간주 — 체크섬 불일치로 지갑이 초기화된 경우에도 재이관하지 않는다)
    private void ImportLegacyGoldIfNeeded()
    {
        if (Application.isPlaying == false || File.Exists(SavePath))
        {
            return;
        }

        string legacyPath = Path.Combine(Application.persistentDataPath, "inventory.json");
        if (File.Exists(legacyPath) == false)
        {
            return;
        }

        try
        {
            LegacyInventoryData legacy = JsonUtility.FromJson<LegacyInventoryData>(File.ReadAllText(legacyPath));
            if (legacy == null)
            {
                return;
            }

            CurrencyState state = FindOrCreate(GoldKey);
            state.balance = Mathf.Max(0, legacy.gold);
            state.earnedTotal = Mathf.Max(0, legacy.goldEarnedTotal);
            state.spentTotal = Mathf.Max(0, legacy.goldSpentTotal);
            Persist(GoldKey);
            Debug.Log($"[ItemSystem][CurrencyManager] 레거시 골드 지갑(inventory.json) 이관: {state.balance} G (earned {state.earnedTotal} / spent {state.spentTotal})");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ItemSystem][CurrencyManager] 레거시 골드 이관 실패: " + e.Message);
        }
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

            // 체크섬 불일치 = 변조 또는 손상 — 로그 후 지갑을 명시적으로 초기화한다(전 재화 0).
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
