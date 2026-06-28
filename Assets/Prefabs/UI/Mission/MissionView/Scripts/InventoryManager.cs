using System;
using System.IO;
using UnityEngine;

// gold/item1~3 재화 보유. inventory.json으로 영속 저장. 미션 보상 적립 대상. 런타임 접근 시 자동 생성.
[Serializable]
public class InventoryData
{
    public int gold;
    public int item1;
    public int item2;
    public int item3;
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
    public int ItemTotal => data.item1 + data.item2 + data.item3;

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

    public int GetItem(int slot)
    {
        switch (slot)
        {
            case 1: return data.item1;
            case 2: return data.item2;
            case 3: return data.item3;
            default: return 0;
        }
    }

    public InventoryData GetSnapshot()
    {
        return new InventoryData
        {
            gold = data.gold,
            item1 = data.item1,
            item2 = data.item2,
            item3 = data.item3,
            goldEarnedTotal = data.goldEarnedTotal,
            goldSpentTotal = data.goldSpentTotal,
        };
    }

    public void AddGold(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        if (amount > 0)
        {
            data.gold += amount;
            data.goldEarnedTotal += amount;
        }
        else
        {
            data.gold = Mathf.Max(0, data.gold + amount);
        }

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

    public void AddItem(int slot, int amount)
    {
        if (amount == 0)
        {
            return;
        }

        SetItem(slot, Mathf.Max(0, GetItem(slot) + amount));
        Persist();
    }

    public bool SpendItem(int slot, int amount)
    {
        if (amount <= 0 || GetItem(slot) < amount)
        {
            return false;
        }

        SetItem(slot, GetItem(slot) - amount);
        Persist();
        return true;
    }

    public void AddReward(MissionReward reward)
    {
        if (reward == null || reward.IsEmpty)
        {
            return;
        }

        if (reward.gold != 0)
        {
            data.gold += reward.gold;
            if (reward.gold > 0)
            {
                data.goldEarnedTotal += reward.gold;
            }
        }

        if (reward.item1 != 0) data.item1 = Mathf.Max(0, data.item1 + reward.item1);
        if (reward.item2 != 0) data.item2 = Mathf.Max(0, data.item2 + reward.item2);
        if (reward.item3 != 0) data.item3 = Mathf.Max(0, data.item3 + reward.item3);

        Persist();
    }

    public void ResetAll()
    {
        data.gold = 0;
        data.item1 = 0;
        data.item2 = 0;
        data.item3 = 0;
        data.goldEarnedTotal = 0;
        data.goldSpentTotal = 0;
        Persist();
    }

    private void SetItem(int slot, int value)
    {
        switch (slot)
        {
            case 1: data.item1 = value; break;
            case 2: data.item2 = value; break;
            case 3: data.item3 = value; break;
        }
    }
}
