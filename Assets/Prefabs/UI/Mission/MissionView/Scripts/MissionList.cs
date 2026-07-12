using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 미션 목록 + 런타임 로직을 한 곳에서 관리한다(ChangeCharManager 방식).
/// 정의는 MissionDatabase(코드, 1줄씩)에서 가져오고, 진행 상태는 MissionInfo에 메모리로만 보관한다(JSON 없음).
/// 외부 게임 이벤트는 Report/ReportFlag로 진행도를 올린다.
/// </summary>
public class MissionList : MonoBehaviour
{
    private static MissionList _instance;

    public static MissionList Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                _instance = FindFirstObjectByType<MissionList>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MissionList");
                    _instance = go.AddComponent<MissionList>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
    }

    public event Action MissionsChanged;

    private readonly List<MissionInfo> missions = new List<MissionInfo>();
    private readonly Dictionary<string, MissionInfo> byId = new Dictionary<string, MissionInfo>();
    private bool updatingDerived;
    private bool suppressNotify;   // claim 중 중간 알림 억제(클릭 1회=알림 1회)
    private string language = "ko";

    private void RaiseChanged()
    {
        if (!suppressNotify)
        {
            SaveProgress();
            MissionsChanged?.Invoke();
        }
    }

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
            RaiseChanged();
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
        BuildMissions();
        LoadProgress(); // 저장된 진행도 적용(정의는 코드, 상태만 JSON)

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged += OnInventoryChanged;
        }

        UpdateDerived(); // 인벤토리 기반 도전은 현재 보유/누적치에서 재산출
    }

    private string SavePath => Path.Combine(Application.persistentDataPath, "missions.json");

    private void LoadProgress()
    {
        string path = SavePath;
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            MissionSaveData data = JsonUtility.FromJson<MissionSaveData>(json);
            if (data == null || data.items == null)
            {
                return;
            }

            for (int i = 0; i < data.items.Count; i++)
            {
                MissionProgressDto dto = data.items[i];
                MissionInfo info = GetById(dto.id);
                if (info != null)
                {
                    info.current = dto.current;
                    info.claimedTiers = dto.claimedTiers;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Mission] 진행도 로드 실패: " + e.Message);
        }
    }

    private void SaveProgress()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        try
        {
            MissionSaveData data = new MissionSaveData();
            for (int i = 0; i < missions.Count; i++)
            {
                MissionInfo m = missions[i];
                data.items.Add(new MissionProgressDto { id = m.id, current = m.current, claimedTiers = m.claimedTiers });
            }

            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Mission] 진행도 저장 실패: " + e.Message);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.InventoryChanged -= OnInventoryChanged;
        }
    }

    // ── 조회 ─────────────────────────────────────────────────────────────────
    public IReadOnlyList<MissionInfo> All => missions;

    public MissionInfo GetById(string id)
    {
        return byId.TryGetValue(id, out MissionInfo info) ? info : null;
    }

    public List<MissionInfo> GetByTab(MissionTab tab)
    {
        List<MissionInfo> result = new List<MissionInfo>();
        for (int i = 0; i < missions.Count; i++)
        {
            if (missions[i].tab == tab)
            {
                result.Add(missions[i]);
            }
        }

        return result;
    }

    // 탭 뱃지용: 달성/전체 수(메타 제외). Increment는 1레벨 이상이면 진행 취급.
    public void GetTabCounts(MissionTab tab, out int done, out int total)
    {
        done = 0;
        total = 0;
        for (int i = 0; i < missions.Count; i++)
        {
            MissionInfo info = missions[i];
            if (info.tab != tab)
            {
                continue;
            }

            total++;
            bool d = info.type == MissionType.Increment ? info.claimedTiers > 0 : info.AllDone;
            if (d)
            {
                done++;
            }
        }
    }

    // ── 진행 보고 ─────────────────────────────────────────────────────────────
    public void Report(string id, int delta = 1)
    {
        MissionInfo info = GetById(id);
        if (info == null || delta == 0)
        {
            return;
        }

        info.current = Mathf.Max(0, info.current + delta);
        RaiseChanged();
    }

    // 최대값(best) 집계: 한 세션에서 달성한 값 중 '최고치'를 진행도로 둔다(누적 아님).
    // 예) "한 번의 대화에 '바나나' 5회 이상" → 대화 종료 시 ReportBest(id, 그 대화의 바나나 수).
    public void ReportBest(string id, int sessionValue)
    {
        MissionInfo info = GetById(id);
        if (info == null)
        {
            return;
        }

        if (sessionValue > info.current)
        {
            info.current = sessionValue;
            RaiseChanged();
        }
    }

    public void ReportFlag(string id)
    {
        MissionInfo info = GetById(id);
        if (info == null)
        {
            return;
        }

        int needed = info.NextTarget;
        if (info.current < needed)
        {
            info.current = needed;
            RaiseChanged();
        }
    }

    // 테스트용: 대상 미션만 +1, 최대치(마지막 단계/현재 레벨) 초과 금지.
    public void TestIncrement(string id)
    {
        MissionInfo info = GetById(id);
        if (info == null)
        {
            return;
        }

        int cap = info.IsIncrement ? info.TargetForLevel(info.claimedTiers) : info.TargetForLevel(info.TierCount - 1);
        if (info.current >= cap)
        {
            return;
        }

        info.current++;
        RaiseChanged();
    }

    // ── 보상 수령 ─────────────────────────────────────────────────────────────
    public bool ClaimReward(string id)
    {
        MissionInfo info = GetById(id);
        if (info == null || !info.Claimable)
        {
            return false;
        }

        MissionReward reward = info.RewardForLevel(info.claimedTiers);

        // 보상 적립(→InventoryChanged) + 단계 증가 + 파생 갱신을 묶고, 알림은 마지막에 1회만.
        suppressNotify = true;
        try
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddReward(reward);
            }

            info.claimedTiers++;
            UpdateDerived();
        }
        finally
        {
            suppressNotify = false;
        }

        RaiseChanged();
        return true;
    }

    public bool IsCompleted(string id)
    {
        MissionInfo info = GetById(id);
        return info != null && info.AllDone;
    }

    // 테스트용: 진행 상태만 초기화(재화/인벤토리는 유지).
    public void ResetAllProgress()
    {
        for (int i = 0; i < missions.Count; i++)
        {
            missions[i].current = 0;
            missions[i].claimedTiers = 0;
        }

        UpdateDerived(); // 인벤토리 기반 도전은 현재 보유/누적치에서 다시 산출
        RaiseChanged();
    }

    private void OnInventoryChanged()
    {
        UpdateDerived();
        RaiseChanged();
    }

    // 메타 + 인벤토리 파생 미션 진행도 갱신(재진입 가드).
    private void UpdateDerived()
    {
        if (updatingDerived)
        {
            return;
        }

        updatingDerived = true;
        try
        {
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null)
            {
                SetCurrent("CH0001", inv.GoldEarnedTotal);
                SetCurrent("CH0007", inv.GoldSpentTotal);
            }

            SetCurrent("CH0003", AllTabDone(MissionTab.Onboarding) ? 1 : 0);
            SetCurrent("CH0004", AllTabDone(MissionTab.Conversation) ? 1 : 0);
            SetCurrent("CH0005", AllTabDone(MissionTab.Affection) ? 1 : 0);
            SetCurrent("CH0006", AllTabDone(MissionTab.Productivity) ? 1 : 0);
            SetCurrent("CH0002", CountAchieved());
        }
        finally
        {
            updatingDerived = false;
        }
    }

    private void SetCurrent(string id, int value)
    {
        MissionInfo info = GetById(id);
        if (info != null)
        {
            info.current = value;
        }
    }

    private bool AllTabDone(MissionTab tab)
    {
        bool any = false;
        for (int i = 0; i < missions.Count; i++)
        {
            MissionInfo info = missions[i];
            if (info.tab != tab || info.isMeta)
            {
                continue;
            }

            any = true;
            bool done = info.type == MissionType.Increment ? info.claimedTiers > 0 : info.AllDone;
            if (!done)
            {
                return false;
            }
        }

        return any;
    }

    private int CountAchieved()
    {
        int count = 0;
        for (int i = 0; i < missions.Count; i++)
        {
            MissionInfo info = missions[i];
            if (info.isMeta)
            {
                continue;
            }

            if (info.claimedTiers > 0)
            {
                count++;
            }
        }

        return count;
    }

    // 정의는 MissionDatabase(코드, 1줄씩)에서 로드. 진행 상태는 메모리.
    private void BuildMissions()
    {
        missions.Clear();
        byId.Clear();
        missions.AddRange(MissionDatabase.Build());
        for (int i = 0; i < missions.Count; i++)
        {
            byId[missions[i].id] = missions[i];
        }
    }
}
