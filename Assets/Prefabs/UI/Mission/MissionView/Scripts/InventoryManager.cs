using System;
using System.IO;
using UnityEngine;

// gold 재화 보유. inventory.json으로 영속 저장. 미션 보상 적립 대상. 런타임 접근 시 자동 생성.
[Serializable]
public class InventoryData
{
    public int gold;
    public int goldEarnedTotal;  // 누적 획득(도전 미션용)
    public int goldSpentTotal;   // 누적 소비(도전 미션용)
}

public class InventoryManager : MonoBehaviour
{
    private static InventoryManager _instance;

    public static InventoryManager Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                _instance = FindFirstObjectByType<InventoryManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("InventoryManager");
                    _instance = go.AddComponent<InventoryManager>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
    }

    public event Action InventoryChanged;

    private InventoryData data = new InventoryData();

    public int Gold => data.gold;
    public int GoldEarnedTotal => data.goldEarnedTotal;
    public int GoldSpentTotal => data.goldSpentTotal;

    private string SavePath => Path.Combine(Application.persistentDataPath, "inventory.json");

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Load();
    }

    private void Load()
    {
        string path = SavePath;
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            InventoryData loaded = JsonUtility.FromJson<InventoryData>(json);
            if (loaded != null)
            {
                data = loaded;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Inventory] 로드 실패: " + e.Message);
        }
    }

    // 저장 + 변경 통지
    private void Persist()
    {
        if (Application.isPlaying)
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Inventory] 저장 실패: " + e.Message);
            }
        }

        InventoryChanged?.Invoke();
    }

    // 소득 — 잔액과 누적 획득(goldEarnedTotal)을 함께 올린다(CH0001 골드 모으기 반응).
    public void EarnGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        data.gold += amount;
        data.goldEarnedTotal += amount;
        Persist();
    }

    // 순수 잔액 변경 — 미션류(누적 획득/소비)에 잡히지 않는 db성 변경. 음수 가능(0 하한).
    public void AddGold(int amount)
    {
        data.gold = Mathf.Max(0, data.gold + amount);
        Persist();
    }

    // 실패한 결제 되돌림 — 잔액 복구 + 누적 소비(goldSpentTotal) 차감.
    // CH0007(골드 소비하기) 진행도가 뒤로 물러나는 것은 의도된 동작(실패 결제는 소비가 아님).
    public void RefundGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        data.gold += amount;
        data.goldSpentTotal = Mathf.Max(0, data.goldSpentTotal - amount);
        Persist();
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0 || data.gold < amount)
        {
            return false;
        }

        data.gold -= amount;
        data.goldSpentTotal += amount;
        Persist();
        return true;
    }

    // 미션 보상 적립 — gold는 소득(Earn) 의미(양수면 누적 획득에도 가산).
    public void AddReward(MissionReward reward)
    {
        if (reward == null || reward.IsEmpty)
        {
            return;
        }

        if (reward.gold != 0)
        {
            // 음수 보상이 정의돼도 잔액 불변식(0 하한)은 지킨다 — 다른 진입점들과 동일
            data.gold = Mathf.Max(0, data.gold + reward.gold);
            if (reward.gold > 0)
            {
                data.goldEarnedTotal += reward.gold;
            }
        }

        Persist();
    }

    public void ResetAll()
    {
        data.gold = 0;
        data.goldEarnedTotal = 0;
        data.goldSpentTotal = 0;
        Persist();
    }
}
