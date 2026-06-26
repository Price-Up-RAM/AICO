using System;
using UnityEngine;

// gold/item1~3 CRUD 싱글톤. 미션 보상 적립 대상. (MISSION_Design.md §6.2)
// 런타임에 접근 시 자동 생성(DontDestroyOnLoad). 변경마다 즉시 저장 + InventoryChanged.
public class InventoryManager : MonoBehaviour
{
    private static InventoryManager _instance;

    public static InventoryManager Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                GameObject go = new GameObject("InventoryManager");
                _instance = go.AddComponent<InventoryManager>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public event Action InventoryChanged;

    private readonly InventoryRepository repository = new InventoryRepository();
    private InventoryData data;

    public int Gold => Data.gold;
    public int GoldEarnedTotal => Data.goldEarnedTotal;
    public int GoldSpentTotal => Data.goldSpentTotal;
    public int ItemTotal => Data.item1 + Data.item2 + Data.item3;

    private InventoryData Data
    {
        get
        {
            if (data == null)
            {
                data = repository.Load() ?? new InventoryData();
            }

            return data;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        data = repository.Load() ?? new InventoryData();
    }

    public int GetItem(int slot)
    {
        switch (slot)
        {
            case 1: return Data.item1;
            case 2: return Data.item2;
            case 3: return Data.item3;
            default: return 0;
        }
    }

    public InventoryData GetSnapshot()
    {
        InventoryData d = Data;
        return new InventoryData
        {
            gold = d.gold,
            item1 = d.item1,
            item2 = d.item2,
            item3 = d.item3,
            goldEarnedTotal = d.goldEarnedTotal,
            goldSpentTotal = d.goldSpentTotal,
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
            Data.gold += amount;
            Data.goldEarnedTotal += amount;
        }
        else
        {
            Data.gold = Mathf.Max(0, Data.gold + amount);
        }

        Persist();
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (Data.gold < amount)
        {
            return false;
        }

        Data.gold -= amount;
        Data.goldSpentTotal += amount;
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
        if (amount <= 0)
        {
            return false;
        }

        if (GetItem(slot) < amount)
        {
            return false;
        }

        SetItem(slot, GetItem(slot) - amount);
        Persist();
        return true;
    }

    // 미션 보상 일괄 적립.
    public void AddReward(MissionReward reward)
    {
        if (reward == null || reward.IsEmpty)
        {
            return;
        }

        if (reward.gold != 0)
        {
            Data.gold += reward.gold;
            if (reward.gold > 0)
            {
                Data.goldEarnedTotal += reward.gold;
            }
        }

        if (reward.item1 != 0)
        {
            Data.item1 = Mathf.Max(0, Data.item1 + reward.item1);
        }

        if (reward.item2 != 0)
        {
            Data.item2 = Mathf.Max(0, Data.item2 + reward.item2);
        }

        if (reward.item3 != 0)
        {
            Data.item3 = Mathf.Max(0, Data.item3 + reward.item3);
        }

        Persist();
    }

    public void ResetAll()
    {
        data = new InventoryData();
        Persist();
    }

    private void SetItem(int slot, int value)
    {
        switch (slot)
        {
            case 1: Data.item1 = value; break;
            case 2: Data.item2 = value; break;
            case 3: Data.item3 = value; break;
        }
    }

    private void Persist()
    {
        repository.Save(Data);
        InventoryChanged?.Invoke();
    }
}
