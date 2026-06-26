using System;
using System.Collections.Generic;
using UnityEngine;

// 미션 런타임 상태(진행도) + 진행 API + 이벤트 + Repository/Inventory 연결.
// 외부 게임 이벤트는 Report/ReportFlag로 진행도를 올린다. (MISSION_Design.md §4)
public class MissionManager : MonoBehaviour
{
    private static MissionManager _instance;

    public static MissionManager Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                GameObject go = new GameObject("MissionManager");
                _instance = go.AddComponent<MissionManager>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public event Action MissionsChanged;

    private readonly MissionRepository repository = new MissionRepository();
    private readonly Dictionary<string, MissionProgress> progressMap = new Dictionary<string, MissionProgress>();
    private MissionSaveData saveData;
    private bool updatingDerived;
    private string language = "ko";

    public string Language
    {
        get => language;
        set
        {
            if (language == value)
            {
                return;
            }

            language = value;
            MissionsChanged?.Invoke();
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
        LoadFromDisk();

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
        }

        UpdateDerived();
    }

    private void OnDestroy()
    {
        if (_instance == this && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
        }
    }

    // ── 조회 ─────────────────────────────────────────────────────────────────
    public IReadOnlyList<MissionDef> GetDefs(MissionCategory category)
    {
        return MissionCatalog.GetByCategory(category);
    }

    public MissionProgress GetProgress(string id)
    {
        return GetOrCreate(id);
    }

    public bool IsCompleted(string id)
    {
        MissionDef def = MissionCatalog.GetById(id);
        if (def == null)
        {
            return false;
        }

        MissionProgress p = GetOrCreate(id);
        return def.IsAllDone(p.claimedTiers);
    }

    public bool IsClaimable(string id)
    {
        MissionDef def = MissionCatalog.GetById(id);
        if (def == null)
        {
            return false;
        }

        return IsClaimable(def, GetOrCreate(id));
    }

    public static bool IsClaimable(MissionDef def, MissionProgress p)
    {
        if (def == null || p == null)
        {
            return false;
        }

        if (def.IsAllDone(p.claimedTiers))
        {
            return false;
        }

        return p.currentCount >= def.TargetForLevel(p.claimedTiers);
    }

    // 카테고리 달성/전체 카운트 (탭 뱃지용). 메타 제외.
    public void GetCategoryCounts(MissionCategory category, out int done, out int total)
    {
        done = 0;
        total = 0;
        List<MissionDef> defs = MissionCatalog.GetByCategory(category);
        for (int i = 0; i < defs.Count; i++)
        {
            total++;
            if (def_IsConsideredDone(defs[i]))
            {
                done++;
            }
        }
    }

    private bool def_IsConsideredDone(MissionDef def)
    {
        MissionProgress p = GetOrCreate(def.id);
        if (def.type == MissionType.Increment)
        {
            return p.claimedTiers > 0; // 반복형은 1레벨 이상 수령 시 '진행 중' 취급
        }

        return def.IsAllDone(p.claimedTiers);
    }

    // ── 진행 보고 ─────────────────────────────────────────────────────────────
    public void Report(string missionId, int delta = 1)
    {
        MissionDef def = MissionCatalog.GetById(missionId);
        if (def == null || delta == 0)
        {
            return;
        }

        MissionProgress p = GetOrCreate(missionId);
        p.currentCount = Mathf.Max(0, p.currentCount + delta);
        Persist();
        MissionsChanged?.Invoke();
    }

    // Flag류(단발) 달성 보고: 다음 목표 이상으로 끌어올려 수령 가능하게.
    public void ReportFlag(string missionId)
    {
        MissionDef def = MissionCatalog.GetById(missionId);
        if (def == null)
        {
            return;
        }

        MissionProgress p = GetOrCreate(missionId);
        int needed = def.TargetForLevel(p.claimedTiers);
        if (p.currentCount < needed)
        {
            p.currentCount = needed;
            Persist();
            MissionsChanged?.Invoke();
        }
    }

    // ── 보상 수령 ─────────────────────────────────────────────────────────────
    public bool ClaimReward(string id)
    {
        MissionDef def = MissionCatalog.GetById(id);
        if (def == null)
        {
            return false;
        }

        MissionProgress p = GetOrCreate(id);
        if (!IsClaimable(def, p))
        {
            return false;
        }

        MissionReward reward = def.RewardForLevel(p.claimedTiers);
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddReward(reward);
        }

        p.claimedTiers++;
        Persist();
        UpdateDerived();
        MissionsChanged?.Invoke();
        return true;
    }

    // 테스트용: 대상 미션의 진행도만 +1. 해당 미션의 최대치를 넘지 않는다.
    // (롱프레스 테스트 훅 MissionTestPoke가 호출. 결합도 낮게 이 한 메서드만 의존)
    public void TestIncrement(string id)
    {
        MissionDef def = MissionCatalog.GetById(id);
        if (def == null)
        {
            return;
        }

        MissionProgress p = GetOrCreate(id);
        int cap = def.IsIncrement
            ? def.TargetForLevel(p.claimedTiers)          // 증가형: 현재 레벨 목표까지(수령하면 다음 레벨)
            : def.TargetForLevel(def.TierCount - 1);       // 일회/열거형: 마지막 단계 목표까지
        if (p.currentCount >= cap)
        {
            return;
        }

        p.currentCount++;
        Persist();
        MissionsChanged?.Invoke();
    }

    // 테스트용: 미션 진행 상태만 초기화한다. 인벤토리(이미 지급된 보상)는 건드리지 않는다.
    public void ResetAllProgress()
    {
        progressMap.Clear();
        saveData = new MissionSaveData();
        Persist();
        UpdateDerived(); // 인벤토리 기반 도전 미션은 현재 보유/누적치에서 다시 산출됨
        MissionsChanged?.Invoke();
    }

    // ── 내부 ─────────────────────────────────────────────────────────────────
    private void LoadFromDisk()
    {
        saveData = repository.Load() ?? new MissionSaveData();
        progressMap.Clear();
        for (int i = 0; i < saveData.progresses.Count; i++)
        {
            MissionProgress p = saveData.progresses[i];
            if (p != null && !string.IsNullOrEmpty(p.id) && !progressMap.ContainsKey(p.id))
            {
                progressMap[p.id] = p;
            }
        }
    }

    private MissionProgress GetOrCreate(string id)
    {
        if (progressMap.TryGetValue(id, out MissionProgress existing) && existing != null)
        {
            return existing;
        }

        MissionProgress created = new MissionProgress(id);
        progressMap[id] = created;
        if (saveData == null)
        {
            saveData = new MissionSaveData();
        }

        saveData.progresses.Add(created);
        return created;
    }

    private void Persist()
    {
        repository.Save(saveData);
    }

    private void OnInventoryChanged()
    {
        UpdateDerived();
        MissionsChanged?.Invoke();
    }

    // 메타 + 인벤토리 파생 미션 진행도 갱신. 재진입 가드.
    private void UpdateDerived()
    {
        if (updatingDerived)
        {
            return;
        }

        updatingDerived = true;
        try
        {
            // 인벤토리 기반 도전 미션
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null)
            {
                SetProgress("CH0001", inv.GoldEarnedTotal);   // 골드 모으기(누적 획득)
                SetProgress("CH0007", inv.GoldSpentTotal);    // 골드 소비(누적 소비)
                SetProgress("CH0008", inv.ItemTotal);         // 아이템 모으기(보유 합계)
            }

            // 카테고리 전체 달성 메타
            SetProgress("CH0003", AllCategoryDone(MissionCategory.Onboarding) ? 1 : 0);
            SetProgress("CH0004", AllCategoryDone(MissionCategory.Conversation) ? 1 : 0);
            SetProgress("CH0005", AllCategoryDone(MissionCategory.Affection) ? 1 : 0);
            SetProgress("CH0006", AllCategoryDone(MissionCategory.Productivity) ? 1 : 0);

            // 누적 미션 달성 수(메타 제외, 1단계라도 수령한 미션 수)
            SetProgress("CH0002", CountAchievedMissions());

            Persist();
        }
        finally
        {
            updatingDerived = false;
        }
    }

    private void SetProgress(string id, int value)
    {
        MissionProgress p = GetOrCreate(id);
        if (p.currentCount != value)
        {
            p.currentCount = value;
        }
    }

    private bool AllCategoryDone(MissionCategory category)
    {
        List<MissionDef> defs = MissionCatalog.GetByCategory(category);
        if (defs.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < defs.Count; i++)
        {
            MissionDef def = defs[i];
            if (MissionCatalog.IsMeta(def.id))
            {
                continue;
            }

            MissionProgress p = GetOrCreate(def.id);
            // 반복형(Increment)은 '끝'이 없으므로 1레벨 이상 수령으로 충족 처리.
            bool done = def.type == MissionType.Increment ? p.claimedTiers > 0 : def.IsAllDone(p.claimedTiers);
            if (!done)
            {
                return false;
            }
        }

        return true;
    }

    private int CountAchievedMissions()
    {
        int count = 0;
        IReadOnlyList<MissionDef> all = MissionCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            MissionDef def = all[i];
            if (MissionCatalog.IsMeta(def.id))
            {
                continue;
            }

            MissionProgress p = GetOrCreate(def.id);
            if (p.claimedTiers > 0)
            {
                count++;
            }
        }

        return count;
    }
}
