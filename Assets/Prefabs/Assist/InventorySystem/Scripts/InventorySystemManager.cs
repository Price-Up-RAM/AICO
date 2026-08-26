using System.Collections.Generic;
using System.IO;
using UnityEngine;

// InventorySystem 매니저 (완전 독립 standalone).
// 아이템을 {key,count} 스토어(MAIN 공용 풀 + 캐릭터별 스토어)로 관리하고 JSON으로 영속화.
// 장착/해제는 전부 EquipSystem(EquipManager)에 위임 — 단방향 의존.
// 스토어는 "소유"만 추적하며, 장착 상태의 진실은 EquipSystem에 있고
// 여기서는 표시용 런타임 미러(저장 안 함)만 유지한다.
public class InventorySystemManager : MonoBehaviour
{
    public const string MainOwnerId = "MAIN";  // 공용(MAIN) 스토어의 ownerId

    private static InventorySystemManager instance;  // 싱글톤 인스턴스
    public static InventorySystemManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 인스턴스가 없으면 찾아서 할당
                instance = FindObjectOfType<InventorySystemManager>();
            }

            return instance;
        }
    }

    [SerializeField] private ItemCatalog catalog;           // 아이템 메타 카탈로그. 인스펙터 지정 우선.
    [SerializeField] private EquipCatalog equipCatalog;     // 장착 가능 여부/슬롯 판정용 EquipSystem 카탈로그.

    // 뷰의 메타 조회용 getter
    public ItemCatalog Catalog
    {
        get
        {
            return catalog;
        }
    }

    public string ActiveCharcode { get; private set; }      // 현재 활성 캐릭터 charcode
    public GameObject ActiveTarget { get; private set; }    // 현재 활성 캐릭터 GameObject (장착 대상)

    // 장착 허용 판정 — 외부(앱 배선)에서 주입 (예: 캐릭터 기능 태그 판정). null이면 전부 허용.
    // 독립성 원칙: 이 패키지는 프로젝트 매니저를 직접 참조하지 않는다.
    public System.Func<GameObject, bool> equipPermissionResolver;

    // 활성 캐릭터의 장착 허용 여부 (뷰의 사전 차단용 — 판정은 주입된 resolver 경유)
    public bool CanEquipOnActive()
    {
        return ActiveTarget != null && (equipPermissionResolver == null || equipPermissionResolver(ActiveTarget));
    }

    private InvStore mainStore;                                             // MAIN 스토어 (지연 로드)
    private Dictionary<string, InvStore> charStores = new Dictionary<string, InvStore>();  // charcode→스토어 캐시

    // 장착 미러: charcode → (slotId → key). 런타임 표시 전용, 저장 안 함.
    private Dictionary<string, Dictionary<string, string>> equipMirror = new Dictionary<string, Dictionary<string, string>>();

    // 카탈로그 미지정 시 Resources에서 자동 로드 (인스펙터 지정이 우선)
    private void Awake()
    {
        if (catalog == null)
        {
            catalog = Resources.Load<ItemCatalog>("ItemCatalog");
        }

        if (equipCatalog == null)
        {
            equipCatalog = catalog != null && catalog.EquipCatalog != null
                ? catalog.EquipCatalog
                : Resources.Load<EquipCatalog>("EquipCatalog");
        }
    }

    // 저장 디렉토리 경로 (persistentDataPath/InventorySystem)
    private string GetSaveDir()
    {
        return Path.Combine(Application.persistentDataPath, "InventorySystem");
    }

    // 스토어 저장 파일 경로 (MAIN=main.json, 캐릭터=char_{charcode}.json)
    private string GetStorePath(string ownerId)
    {
        if (ownerId == MainOwnerId)
        {
            return Path.Combine(GetSaveDir(), "main.json");
        }

        return Path.Combine(GetSaveDir(), $"char_{ownerId}.json");
    }

    // 파일에서 스토어 로드 (파일 없거나 파싱 실패 시 신규 생성)
    private InvStore LoadStore(string ownerId)
    {
        string path = GetStorePath(ownerId);

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                InvStore loaded = JsonUtility.FromJson<InvStore>(json);
                if (loaded != null)
                {
                    // 파일 이름 기준 ownerId를 신뢰 (파일 내용 불일치 보정)
                    loaded.ownerId = ownerId;
                    if (loaded.stacks == null)
                    {
                        loaded.stacks = new List<InvItemStack>();
                    }
                    if (loaded.equippedKeys == null)
                    {
                        loaded.equippedKeys = new List<string>();
                    }

                    // 칸 미배정(-1)/중복 보정 (slot 필드가 없던 구버전 세이브 대응)
                    loaded.NormalizeSlots();

                    return loaded;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[InventorySystemManager] 스토어 로드 실패({ownerId}): {e.Message}");
            }
        }

        InvStore store = new InvStore();
        store.ownerId = ownerId;
        return store;
    }

    // MAIN 스토어 조회 (지연 로드)
    public InvStore GetMainStore()
    {
        if (mainStore == null)
        {
            mainStore = LoadStore(MainOwnerId);
        }

        return mainStore;
    }

    // 캐릭터 스토어 조회 (지연 로드: 파일 있으면 로드, 없으면 신규)
    public InvStore GetCharStore(string charcode)
    {
        if (string.IsNullOrEmpty(charcode))
        {
            return null;
        }

        if (charStores.TryGetValue(charcode, out InvStore cached))
        {
            return cached;
        }

        InvStore store = LoadStore(charcode);
        charStores[charcode] = store;
        return store;
    }

    // 활성 캐릭터의 스토어 조회 (활성 캐릭터 없으면 null)
    public InvStore GetActiveCharStore()
    {
        if (string.IsNullOrEmpty(ActiveCharcode))
        {
            return null;
        }

        return GetCharStore(ActiveCharcode);
    }

    // 활성 소유자(캐릭터) 전환 — 미래 앱 배선 시 외부(CharManager 등)가 부를 단일 진입점
    public void SetActiveOwner(string charcode, GameObject target)
    {
        if (string.IsNullOrEmpty(charcode))
        {
            Debug.LogWarning("[InventorySystemManager] SetActiveOwner: charcode가 비어 있습니다.");
            return;
        }

        if (target == null)
        {
            Debug.LogWarning("[InventorySystemManager] SetActiveOwner: target이 null입니다.");
            return;
        }

        // 임시 안전망: 소켓이 하나도 없는 캐릭터는 origin 소켓을 주입해 장착이 전면 차단되지 않게 한다.
        // 씬 인스턴스 메모리 전용(Application.isPlaying 게이트) — 에셋/프리팹에 기록되지 않아 저장 오염 없음.
        if (Application.isPlaying && target.GetComponentsInChildren<EquipSocket>(true).Length == 0)
        {
            EquipSocketController ctrl = target.GetComponent<EquipSocketController>();
            if (ctrl == null)
            {
                ctrl = target.AddComponent<EquipSocketController>();
            }

            ctrl.CreateOriginSocket();
            Debug.LogWarning($"[InventorySystemManager] '{target.name}'에 소켓이 없어 origin 임시 소켓 주입 — 정식 소켓 재저작 필요");
        }

        // 전환 전 현재 상태 저장
        SaveAll();

        ActiveCharcode = charcode;
        ActiveTarget = target;

        // 스토어 로드 보장
        GetCharStore(charcode);

        // 저장된 착용 목록 재장착 (플레이 중에만 — 에디트 모드 Instantiate 방지, origin 주입과 동일 게이트)
        //
        // 2026-08-26: 즉시 부르지 않고 **캐릭터 트랜스폼이 자리를 잡을 때까지 기다린다.**
        // 실측 로그가 원인을 짚었다 —
        //   [MRInv/장착:부팅복원] 소켓월드=(-4.156,-5.029,0) 물건월드=(같음) 캐릭터 pos=(같음) lossyScale=0.00000
        // 소켓 해석은 우선순위 2로 정상이었는데, 그 시점의 캐릭터는 **월드 스케일이 0이고
        // 스켈레톤이 아직 포즈 전이라 모든 본이 루트 원점에 겹쳐 있었다.**
        // 그 상태로 배치 수학(refDist × lossyScale)을 돌리면 값이 무너져 아이템이 발밑에 남고,
        // 나중에 스케일·포즈가 확정돼도 이미 구워진 로컬 트랜스폼은 되돌아오지 않는다.
        // 수동으로 다시 장착하면 제자리로 가던 이유가 이것이다.
        if (Application.isPlaying)
        {
            StartCoroutine(RestoreEquipsWhenReady(charcode, target));
        }

        InventoryEvents.OnActiveOwnerChanged?.Invoke(charcode);
    }

    // 저장된 착용 목록(equippedKeys)을 재장착 — 슬롯은 저장하지 않았으므로 EquipManager의
    // 해석 사다리가 현재 소켓 구성 기준으로 재해석한다 (소켓 리네임/재저작 내성).
    // EquipKey를 거치지 않고 Equip 직접 호출 — 토글 판정·저장 재기록과 섞이지 않는 전용 경로.
    // 캐릭터 트랜스폼이 안정될 때까지 기다렸다가 복원한다.
    //
    // 조건 두 가지 — 둘 다 "측정 가능한 상태"이지 프레임 수 같은 주술이 아니다.
    //   ① 월드 스케일이 퇴화하지 않았다 (lossyScale > 임계)
    //   ② 그 스케일이 연속 두 프레임 같다 (= 더 이상 바뀌지 않는다)
    // MRCharacterWorldRoot의 픽셀 공간 래퍼가 스케일을 나중에 잡아주므로 ①이 핵심이고,
    // ②는 잡는 도중의 중간값을 물지 않기 위한 것이다.
    // 타임아웃을 두어 조건이 영영 안 맞아도 복원을 포기하지 않는다 — 잘못 붙는 것보다
    // 안 붙는 것이 더 나쁘고, 그때는 경고가 원인을 말해준다.
    private System.Collections.IEnumerator RestoreEquipsWhenReady(string charcode, GameObject target)
    {
        const float MinScale = 1e-4f;      // 이 아래는 퇴화로 본다
        const float TimeoutSec = 10f;

        float started = Time.realtimeSinceStartup;
        float lastScale = -1f;
        int stableFrames = 0;

        while (true)
        {
            if (target == null)
            {
                Debug.LogWarning($"[InventorySystemManager] 착용 복원 취소 — 대상이 사라졌다 ({charcode})");
                yield break;
            }

            // 그 사이 다른 캐릭터로 전환됐으면 이 복원은 낡았다
            if (ActiveTarget != target)
            {
                Debug.Log($"[InventorySystemManager] 착용 복원 취소 — 활성 대상이 바뀌었다 ({charcode})");
                yield break;
            }

            float scale = target.transform.lossyScale.x;
            float waited = Time.realtimeSinceStartup - started;

            if (scale > MinScale && Mathf.Approximately(scale, lastScale))
            {
                stableFrames = stableFrames + 1;
                if (stableFrames >= 2)
                {
                    Debug.Log($"[MRInv/장착:대기] 캐릭터 안정 — lossyScale={scale:F5} 대기={waited:F2}초 (임계 {MinScale}) → 복원 시작");
                    break;
                }
            }
            else
            {
                stableFrames = 0;
            }

            lastScale = scale;

            if (waited > TimeoutSec)
            {
                Debug.LogWarning($"[MRInv/장착:대기] {TimeoutSec}초 동안 캐릭터 스케일이 안정되지 않았다 " +
                                 $"(현재 lossyScale={scale:F5}, 임계 {MinScale}) → 그대로 복원한다. " +
                                 "아이템이 발밑에 붙으면 이 줄이 원인이다");
                break;
            }

            yield return null;
        }

        RestoreEquips(charcode, target);
    }

    private void RestoreEquips(string charcode, GameObject target)
    {
        // 미러 초기화가 먼저 — 새로 스폰된 인스턴스의 진실은 "무장착"이다. 이전 세션/이전 인스턴스의
        // 스테일 미러가 남으면 EquipKey가 거짓 멱등 성공을 반환(재장착 불가)하고 ToggleEquip이
        // 실제로 입지 않은 기록을 삭제한다. 기록이 비어 있어도(early-return 케이스) 초기화는 필요.
        if (equipMirror.TryGetValue(charcode, out Dictionary<string, string> stale))
        {
            stale.Clear();
        }

        if (EquipManager.Instance == null || equipCatalog == null)
        {
            return;
        }

        // 장착 불가 캐릭터는 복원 전체 스킵 — 기록은 유지 (전환마다 반복되는 경로라 안내 없이 무음 처리)
        if (equipPermissionResolver != null && equipPermissionResolver(target) == false)
        {
            Debug.Log($"[InventorySystemManager] 착용 복원 스킵(장착 불가 캐릭터): {charcode} — 기록 유지");
            return;
        }

        InvStore store = GetCharStore(charcode);
        if (store == null || store.equippedKeys == null || store.equippedKeys.Count == 0)
        {
            return;
        }

        Dictionary<string, string> slots = GetActiveMirror();
        List<string> keys = new List<string>(store.equippedKeys);  // 순회 중 기록 정리 대비 복사
        bool dirty = false;

        foreach (string key in keys)
        {
            // 소유 검증: 보유하지 않은 키는 착용 불가 (세이브 수동 편집/불일치 방어) — 기록 정리
            if (store.CountOf(key) <= 0)
            {
                store.equippedKeys.Remove(key);
                dirty = true;
                Debug.LogWarning($"[InventorySystemManager] 착용 복원 스킵(미보유): '{key}' — 기록 정리");
                continue;
            }

            string reason;
            bool ok = EquipManager.Instance.Equip(target, key, out reason);
            LogEquipPlacement("부팅복원", target, key, ok);
            if (ok == false)
            {
                // 기록은 유지 — 소켓을 저작하고 나면 다음 전환/재시작 때 자연 복원된다
                Debug.LogWarning($"[InventorySystemManager] 착용 복원 실패: '{key}' — {reason} (기록 유지)");
                continue;
            }

            // 미러 기록: Equip과 동일 사다리로 해석된 실제 슬롯
            EquipEntry entry = equipCatalog.Get(key);
            string slotId;
            int priority;
            if (entry != null && EquipSlotResolver.Resolve(target, entry, out slotId, out priority) != null)
            {
                // 카테고리 배타로 다른 슬롯에서 밀려난 키의 기록도 제거 (방금 Equip이 물리 해제함)
                EvictSameCategoryMirror(charcode, slots, entry, key);

                // 두 기록 키가 같은 슬롯으로 재해석되면(소켓 재저작으로 폴백 충돌) 앞서 입힌 키는
                // 방금 Equip이 교체·파괴했다 — 밀려난 키의 기록도 제거 (EquipKey의 교체 처리와 동일 불변식)
                if (slots.TryGetValue(slotId, out string prevKey) && prevKey != key)
                {
                    PersistEquipped(charcode, prevKey, false);
                }
                slots[slotId] = key;
            }
        }

        if (dirty)
        {
            SaveStore(store);
        }
    }

    // 인벤토리 카탈로그에 등록된 키인지
    public bool IsKnownKey(string key)
    {
        if (catalog == null)
        {
            return false;
        }

        return catalog.Contains(key);
    }

    // 장착 가능한 키인지 (EquipCatalog에 존재하는지)
    public bool IsEquippable(string key)
    {
        ItemEntry meta = catalog != null ? catalog.Get(key) : null;
        return meta != null &&
               meta.useType == ItemUseType.Equip &&
               equipCatalog != null &&
               equipCatalog.Contains(key);
    }

    // 스토어에 아이템 추가 공통 처리 (카탈로그 검증 + maxStack 클램프 + 저장 + 이벤트)
    private bool AddToStore(InvStore store, string key, int amount)
    {
        if (store == null)
        {
            return false;
        }

        if (amount <= 0)
        {
            return false;
        }

        if (IsKnownKey(key) == false)
        {
            Debug.LogWarning($"[InventorySystemManager] 카탈로그에 없는 키: {key}");
            return false;
        }

        ItemEntry meta = catalog.Get(key);
        if (meta != null && meta.isMainOnly && store.ownerId != MainOwnerId)
        {
            Debug.LogWarning($"[InventorySystemManager] MAIN 전용 아이템은 CHAR 스토어에 추가할 수 없습니다: {key}");
            return false;
        }

        // isCountable=false는 데이터의 maxStack 값과 무관하게 항상 1개 한정이다.
        int maxStack = meta != null ? meta.EffectiveMaxStack : 99;
        int current = store.CountOf(key);
        int addable = Mathf.Min(amount, maxStack - current);
        if (addable <= 0)
        {
            Debug.LogWarning($"[InventorySystemManager] 최대 스택 도달: {key} ({current}/{maxStack})");
            return false;
        }

        store.Add(key, addable);
        SaveStore(store);
        InventoryEvents.OnStoreChanged?.Invoke(store.ownerId);
        return true;
    }

    // MAIN 스토어에 아이템 추가
    public bool AddToMain(string key, int amount)
    {
        return AddToStore(GetMainStore(), key, amount);
    }

    // 캐릭터 스토어에 아이템 추가
    public bool AddToChar(string charcode, string key, int amount)
    {
        InvStore store = GetCharStore(charcode);
        if (store == null)
        {
            Debug.LogWarning("[InventorySystemManager] AddToChar: charcode가 비어 있습니다.");
            return false;
        }

        return AddToStore(store, key, amount);
    }

    // MAIN → 캐릭터 이동 (MAIN.Remove 성공 시에만 char.Add — 원자적)
    public bool MoveMainToChar(string charcode, string key, int amount)
    {
        InvStore charStore = GetCharStore(charcode);
        if (charStore == null)
        {
            Debug.LogWarning("[InventorySystemManager] MoveMainToChar: charcode가 비어 있습니다.");
            return false;
        }

        ItemEntry meta = catalog != null ? catalog.Get(key) : null;
        if (meta != null && meta.isMainOnly)
        {
            Debug.LogWarning($"[InventorySystemManager] MAIN 전용 아이템은 CHAR로 이동할 수 없습니다: {key}");
            return false;
        }

        // 목적지 maxStack 불변식 유지: 여유량 부족이면 Remove 전에 거부 (원자성 유지)
        if (HasStackRoom(charStore, key, amount) == false)
        {
            return false;
        }

        InvStore main = GetMainStore();
        if (main.Remove(key, amount) == false)
        {
            return false;
        }

        charStore.Add(key, amount);

        SaveStore(main);
        SaveStore(charStore);
        InventoryEvents.OnStoreChanged?.Invoke(MainOwnerId);
        InventoryEvents.OnStoreChanged?.Invoke(charcode);
        return true;
    }

    // 캐릭터 → MAIN 반환. 이동 후 보유량이 0이 되었고 그 키가 장착 미러에 있으면 장착 해제부터 수행.
    public bool MoveCharToMain(string charcode, string key, int amount)
    {
        InvStore charStore = GetCharStore(charcode);
        if (charStore == null)
        {
            Debug.LogWarning("[InventorySystemManager] MoveCharToMain: charcode가 비어 있습니다.");
            return false;
        }

        // 목적지(MAIN) maxStack 불변식 유지: 여유량 부족이면 Remove 전에 거부 (원자성 유지)
        InvStore main = GetMainStore();
        if (HasStackRoom(main, key, amount) == false)
        {
            return false;
        }

        if (charStore.Remove(key, amount) == false)
        {
            return false;
        }

        // 보유량이 0이 되었는데 아직 장착 중이면 해제 (소유하지 않은 아이템을 입고 있을 수 없음)
        if (charStore.CountOf(key) == 0)
        {
            UnequipIfMirrored(charcode, key);
        }

        main.Add(key, amount);

        SaveStore(charStore);
        SaveStore(main);
        InventoryEvents.OnStoreChanged?.Invoke(charcode);
        InventoryEvents.OnStoreChanged?.Invoke(MainOwnerId);
        return true;
    }

    // ownerId로 스토어 조회 (MAIN 또는 charcode)
    private InvStore GetStore(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId))
        {
            return null;
        }

        if (ownerId == MainOwnerId)
        {
            return GetMainStore();
        }

        return GetCharStore(ownerId);
    }

    // 키의 최대 스택 수 (카탈로그 메타 없으면 99)
    private int GetMaxStack(string key)
    {
        ItemEntry meta = catalog != null ? catalog.Get(key) : null;
        return meta != null ? meta.EffectiveMaxStack : 99;
    }

    // 스택 칸 이동 (드래그 앤 드롭용). 같은 스토어 = 자리 이동/스왑/병합, 다른 스토어 = 통째 이동.
    // toSlot < 0 이면 목적지의 빈 칸에 자동 배치.
    // 수량 지정 이동 (수량 선택 모달의 단일 진입점): amount를 현재 스택 수량으로 클램프한 뒤
    // 전량이면 MoveStack에 위임(스왑/병합/장착해제 등 기존 규칙 그대로), 일부면 스택을 분할해 옮긴다.
    // 부분 이동은 원본에 잔량이 남으므로 장착 해제가 필요 없다. 같은 스토어면 배치 의미라 MoveStack에 위임.
    public bool MoveStackAmount(string fromOwnerId, int fromSlot, string toOwnerId, int toSlot, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        InvStore fromStore = GetStore(fromOwnerId);
        InvStore toStore = GetStore(toOwnerId);
        if (fromStore == null || toStore == null)
        {
            return false;
        }

        InvItemStack moving = fromStore.FindBySlot(fromSlot);
        if (moving == null)
        {
            return false;
        }

        if (toOwnerId != MainOwnerId && fromOwnerId != toOwnerId && IsMainOnly(moving.key))
        {
            Debug.LogWarning($"[InventorySystemManager] MAIN 전용 아이템 이동 거부: {moving.key} → {toOwnerId}");
            return false;
        }

        // 모달이 뜬 사이 스택이 변했을 수 있다 — 현재 수량으로 클램프
        if (amount >= moving.count || fromOwnerId == toOwnerId)
        {
            return MoveStack(fromOwnerId, fromSlot, toOwnerId, toSlot);
        }

        // ── 부분 이동 (다른 스토어, amount < count) ──
        if (toSlot < 0)
        {
            toSlot = toStore.FirstFreeSlot();
        }

        InvItemStack target = toStore.FindBySlot(toSlot);
        int moved;
        if (target == null)
        {
            InvItemStack piece = new InvItemStack();
            piece.key = moving.key;
            piece.count = amount;
            piece.slot = toSlot;
            toStore.stacks.Add(piece);
            moved = amount;
        }
        else
        {
            if (target.key != moving.key)
            {
                Debug.LogWarning($"[InventorySystemManager] 부분 이동 거부(칸이 차 있음): {toStore.ownerId} 칸 {toSlot} = {target.key}");
                return false;
            }

            int room = GetMaxStack(moving.key) - target.count;
            if (room <= 0)
            {
                Debug.LogWarning($"[InventorySystemManager] 부분 이동 거부(최대 스택 도달): {moving.key} → {toStore.ownerId} 칸 {toSlot}");
                return false;
            }

            moved = Mathf.Min(room, amount);
            target.count += moved;
        }

        moving.count -= moved;  // 부분 이동이라 항상 잔량 > 0 — 장착 해제/스택 제거 불필요

        SaveStore(fromStore);
        SaveStore(toStore);
        InventoryEvents.OnStoreChanged?.Invoke(fromStore.ownerId);
        InventoryEvents.OnStoreChanged?.Invoke(toStore.ownerId);
        return true;
    }

    public bool MoveStack(string fromOwnerId, int fromSlot, string toOwnerId, int toSlot)
    {
        InvStore fromStore = GetStore(fromOwnerId);
        InvStore toStore = GetStore(toOwnerId);
        if (fromStore == null || toStore == null)
        {
            return false;
        }

        InvItemStack moving = fromStore.FindBySlot(fromSlot);
        if (moving == null)
        {
            return false;
        }

        if (toOwnerId != MainOwnerId && fromOwnerId != toOwnerId && IsMainOnly(moving.key))
        {
            Debug.LogWarning($"[InventorySystemManager] MAIN 전용 아이템 이동 거부: {moving.key} → {toOwnerId}");
            return false;
        }

        // ── 같은 스토어: 자리 이동 / 스왑 / 병합 ──
        if (fromOwnerId == toOwnerId)
        {
            if (toSlot < 0 || toSlot == fromSlot)
            {
                return false;
            }

            InvItemStack occupant = fromStore.FindBySlot(toSlot);
            if (occupant == null)
            {
                moving.slot = toSlot;
            }
            else if (occupant.key == moving.key)
            {
                // 같은 키 → 병합 (maxStack 초과분은 원래 칸에 잔류, 여유 없으면 스왑)
                int room = GetMaxStack(moving.key) - occupant.count;
                if (room <= 0)
                {
                    occupant.slot = fromSlot;
                    moving.slot = toSlot;
                }
                else
                {
                    int merged = Mathf.Min(room, moving.count);
                    occupant.count += merged;
                    moving.count -= merged;
                    if (moving.count == 0)
                    {
                        fromStore.stacks.Remove(moving);
                    }
                }
            }
            else
            {
                // 다른 키 → 자리 스왑
                occupant.slot = fromSlot;
                moving.slot = toSlot;
            }

            SaveStore(fromStore);
            InventoryEvents.OnStoreChanged?.Invoke(fromStore.ownerId);
            return true;
        }

        // ── 다른 스토어: 통째 이동 (같은 키 칸이면 병합, 다른 키가 차지한 칸이면 거부) ──
        if (toSlot < 0)
        {
            toSlot = toStore.FirstFreeSlot();
        }

        InvItemStack target = toStore.FindBySlot(toSlot);
        if (target == null)
        {
            fromStore.stacks.Remove(moving);
            moving.slot = toSlot;
            toStore.stacks.Add(moving);
        }
        else if (target.key == moving.key)
        {
            int room = GetMaxStack(moving.key) - target.count;
            if (room <= 0)
            {
                Debug.LogWarning($"[InventorySystemManager] 이동 거부(최대 스택 도달): {moving.key} → {toStore.ownerId} 칸 {toSlot}");
                return false;
            }

            int merged = Mathf.Min(room, moving.count);
            target.count += merged;
            moving.count -= merged;
            if (moving.count == 0)
            {
                fromStore.stacks.Remove(moving);
            }
        }
        else
        {
            Debug.LogWarning($"[InventorySystemManager] 이동 거부(칸이 차 있음): {toStore.ownerId} 칸 {toSlot} = {target.key}");
            return false;
        }

        // 캐릭터 스토어에서 빠져나가 보유량이 0이 된 장착 아이템은 해제
        if (fromOwnerId != MainOwnerId && fromStore.CountOf(moving.key) == 0)
        {
            UnequipIfMirrored(fromOwnerId, moving.key);
        }

        SaveStore(fromStore);
        SaveStore(toStore);
        InventoryEvents.OnStoreChanged?.Invoke(fromStore.ownerId);
        InventoryEvents.OnStoreChanged?.Invoke(toStore.ownerId);
        return true;
    }

    // 목적지 스토어에 amount만큼 넣을 여유가 있는지 (이동 경로에서도 maxStack 불변식 유지)
    private bool HasStackRoom(InvStore store, string key, int amount)
    {
        if (store == null || amount <= 0)
        {
            return false;
        }

        ItemEntry meta = catalog != null ? catalog.Get(key) : null;
        int maxStack = meta != null ? meta.EffectiveMaxStack : 99;
        int current = store.CountOf(key);
        if (current + amount > maxStack)
        {
            Debug.LogWarning($"[InventorySystemManager] 이동 거부(최대 스택 초과): {key} → {store.ownerId} ({current}+{amount}/{maxStack})");
            return false;
        }

        return true;
    }

    private bool IsMainOnly(string key)
    {
        ItemEntry meta = catalog != null ? catalog.Get(key) : null;
        return meta != null && meta.isMainOnly;
    }

    // 착용 기록(스토어 equippedKeys) 갱신 + 즉시 저장 — 착용이 바뀌는 3지점(EquipKey/ToggleEquip 해제/UnequipIfMirrored) 공용.
    // 저장 데이터는 key 목록뿐 — 슬롯은 복원 시 해석 사다리가 재해석한다.
    private void PersistEquipped(string charcode, string key, bool equipped)
    {
        InvStore store = GetCharStore(charcode);
        if (store == null || string.IsNullOrEmpty(key))
        {
            return;
        }
        if (store.equippedKeys == null)
        {
            store.equippedKeys = new List<string>();
        }

        bool changed = false;
        if (equipped)
        {
            if (store.equippedKeys.Contains(key) == false)
            {
                store.equippedKeys.Add(key);
                changed = true;
            }
        }
        else
        {
            changed = store.equippedKeys.Remove(key);
        }

        if (changed)
        {
            SaveStore(store);
        }
    }

    // 미러에 해당 키가 장착 중으로 기록되어 있으면 Unequip + 미러 제거. 착용 영속 기록은 미러와 무관하게 정리.
    private void UnequipIfMirrored(string charcode, string key)
    {
        // 영속 기록 정리는 무조건 — 이번 세션에 활성화된 적 없는 캐릭터(미러 없음)도 아이템이 스토어를
        // 떠나면 착용 기록을 유지할 수 없다. PersistEquipped는 멱등이라 기록이 없으면 no-op.
        PersistEquipped(charcode, key, false);

        if (equipMirror.TryGetValue(charcode, out Dictionary<string, string> slots) == false)
        {
            return;
        }

        // 이 키가 장착된 슬롯 수집 (열거 중 수정 방지)
        List<string> slotIds = new List<string>();
        foreach (KeyValuePair<string, string> pair in slots)
        {
            if (pair.Value == key)
            {
                slotIds.Add(pair.Key);
            }
        }

        foreach (string slotId in slotIds)
        {
            // 활성 캐릭터일 때만 실제 Unequip 가능 (target 필요). 미러는 항상 정리.
            if (charcode == ActiveCharcode && ActiveTarget != null && EquipManager.Instance != null)
            {
                EquipManager.Instance.Unequip(ActiveTarget, slotId);
            }

            slots.Remove(slotId);
        }
    }

    // 활성 캐릭터에 키 장착 (멱등 — 이미 장착 중이면 그대로 유지). 같은 슬롯의 다른 장착물은 교체.
    public bool EquipKey(string key)
    {
        if (ActiveTarget == null)
        {
            Debug.LogWarning("[InventorySystemManager] EquipKey: 활성 캐릭터가 없습니다.");
            return false;
        }

        // 장착 게이트 — 장착 불가 캐릭터 차단 (안내 대사는 호출부 UI 몫, 해제는 ToggleEquip에서 계속 허용)
        if (CanEquipOnActive() == false)
        {
            Debug.Log($"[InventorySystemManager] EquipKey: 장착 불가 캐릭터 — '{key}' 차단 ({ActiveCharcode})");
            return false;
        }

        if (equipCatalog == null)
        {
            Debug.LogWarning("[InventorySystemManager] EquipKey: EquipCatalog가 지정되지 않았습니다.");
            return false;
        }

        if (EquipManager.Instance == null)
        {
            Debug.LogWarning("[InventorySystemManager] EquipKey: EquipManager가 없습니다.");
            return false;
        }

        EquipEntry entry = equipCatalog.Get(key);
        if (entry == null)
        {
            Debug.LogWarning($"[InventorySystemManager] EquipKey: 장착 불가 키: {key}");
            return false;
        }

        // 사전 검증: EquipManager.Equip은 실패해도 조용히 반환(void)하므로,
        // 동일 기준(프리팹/소켓)을 미리 확인해 실패 시 미러를 오염시키지 않는다.
        if (entry.prefab == null)
        {
            Debug.LogWarning($"[InventorySystemManager] EquipKey: 프리팹이 비어 있는 키: {key}");
            return false;
        }

        // 실장착(EquipManager)과 동일한 해석 사다리로 사전 검증: ①key 동명 소켓 ②targetSlotId ③fallback
        // (직조회하면 key 이름 소켓/폴백으로 장착 가능한 아이템을 오탐 거부하고, 미러 키가 실제 슬롯과 어긋난다)
        string slotId;
        int priority;
        if (EquipSlotResolver.Resolve(ActiveTarget, entry, out slotId, out priority) == null)
        {
            string candidates = string.Join("/", EquipSlotResolver.Candidates(entry));
            Debug.LogWarning($"[InventorySystemManager] EquipKey: 장착 가능한 소켓 없음: '{key}' (후보: {candidates}) on {ActiveTarget.name}");
            return false;
        }

        Dictionary<string, string> slots = GetActiveMirror();

        if (slots.TryGetValue(slotId, out string equippedKey) && equippedKey == key)
        {
            return true; // 이미 장착 중 (멱등)
        }

        // 장착 (같은 슬롯 기존 장착물은 EquipManager가 교체). 실패 시 미러 오염 방지
        string reason;
        bool equipped = EquipManager.Instance.Equip(ActiveTarget, key, out reason);
        LogEquipPlacement("수동장착", ActiveTarget, key, equipped);
        if (equipped == false)
        {
            Debug.LogWarning($"[InventorySystemManager] EquipKey: 장착 실패 — {reason}");
            return false;
        }

        // 카테고리 배타로 다른 슬롯에서 밀려난 장착 기록 정리 (물리 해제는 EquipManager.Equip이 이미 수행)
        EvictSameCategoryMirror(ActiveCharcode, slots, entry, key);

        // 착용 기록: 같은 슬롯에서 교체된 이전 아이템 기록 제거 + 새 아이템 기록 (즉시 저장)
        if (string.IsNullOrEmpty(equippedKey) == false && equippedKey != key)
        {
            PersistEquipped(ActiveCharcode, equippedKey, false);
        }
        slots[slotId] = key;
        PersistEquipped(ActiveCharcode, key, true);

        InventoryEvents.OnStoreChanged?.Invoke(ActiveCharcode);
        return true;
    }

    // 활성 캐릭터에 대해 키 장착/해제 토글 (장착 위임은 전부 EquipManager)
    public bool ToggleEquip(string key)
    {
        if (ActiveTarget == null)
        {
            Debug.LogWarning("[InventorySystemManager] ToggleEquip: 활성 캐릭터가 없습니다.");
            return false;
        }

        if (equipCatalog == null)
        {
            Debug.LogWarning("[InventorySystemManager] ToggleEquip: EquipCatalog가 지정되지 않았습니다.");
            return false;
        }

        EquipEntry entry = equipCatalog.Get(key);
        if (entry == null)
        {
            Debug.LogWarning($"[InventorySystemManager] ToggleEquip: 장착 불가 키: {key}");
            return false;
        }

        Dictionary<string, string> slots = GetActiveMirror();

        // 미러 키도 해석 사다리 결과(실제 장착 슬롯)로 조회 — targetSlotId 직조회는 폴백/키 소켓과 어긋남
        string resolvedSlotId;
        int priority;
        EquipSocket resolved = EquipSlotResolver.Resolve(ActiveTarget, entry, out resolvedSlotId, out priority);

        if (resolved != null && slots.TryGetValue(resolvedSlotId, out string equippedKey) && equippedKey == key)
        {
            // 이미 이 키가 장착 중 → 해제
            if (EquipManager.Instance == null)
            {
                Debug.LogWarning("[InventorySystemManager] ToggleEquip: EquipManager가 없습니다.");
                return false;
            }

            EquipManager.Instance.Unequip(ActiveTarget, resolvedSlotId);
            slots.Remove(resolvedSlotId);
            PersistEquipped(ActiveCharcode, key, false);  // 착용 기록 제거 (즉시 저장)
            InventoryEvents.OnStoreChanged?.Invoke(ActiveCharcode);
            return true;
        }

        return EquipKey(key);
    }

    // 카테고리 배타로 밀려난 기록 정리 — 새 키와 같은 카테고리(대소문자 무시)의 다른 키를
    // 미러에서 제거하고 착용 영속 기록도 삭제 (물리 해제는 EquipManager.Equip의 카테고리 배타가 수행)
    private void EvictSameCategoryMirror(string charcode, Dictionary<string, string> slots, EquipEntry entry, string newKey)
    {
        if (equipCatalog == null || entry == null || EquipCategory.HasValue(entry.category) == false)
        {
            return;
        }

        // 열거 중 수정 방지 — 제거 대상 슬롯 먼저 수집
        List<string> evictSlotIds = new List<string>();
        foreach (KeyValuePair<string, string> pair in slots)
        {
            if (pair.Value == newKey)
            {
                continue;
            }

            EquipEntry other = equipCatalog.Get(pair.Value);
            if (other != null && EquipCategory.IsSame(other.category, entry.category))
            {
                evictSlotIds.Add(pair.Key);
            }
        }

        foreach (string slotId in evictSlotIds)
        {
            PersistEquipped(charcode, slots[slotId], false);
            slots.Remove(slotId);
        }
    }

    // 활성 캐릭터의 장착 미러 확보
    private Dictionary<string, string> GetActiveMirror()
    {
        if (equipMirror.TryGetValue(ActiveCharcode, out Dictionary<string, string> slots) == false)
        {
            slots = new Dictionary<string, string>();
            equipMirror[ActiveCharcode] = slots;
        }

        return slots;
    }

    // 스토어 정렬: 아이템 종류(category) → 아이템명(displayName) 순. 정렬 결과는 저장된다.
    public bool SortStore(string ownerId)
    {
        if (string.IsNullOrEmpty(ownerId))
        {
            return false;
        }

        InvStore store = ownerId == MainOwnerId ? GetMainStore() : GetCharStore(ownerId);
        if (store == null)
        {
            return false;
        }

        store.stacks.Sort(CompareStacks);

        // 정렬 순서대로 칸을 앞에서부터 재배치 (1페이지부터 채움)
        for (int i = 0; i < store.stacks.Count; i++)
        {
            if (store.stacks[i] != null)
            {
                store.stacks[i].slot = i;
            }
        }

        SaveStore(store);
        InventoryEvents.OnStoreChanged?.Invoke(store.ownerId);
        return true;
    }

    // 정렬 비교: 종류(category) → 이름(displayName, 없으면 key) → key. null 스택은 뒤로.
    private int CompareStacks(InvItemStack a, InvItemStack b)
    {
        if (a == null && b == null)
        {
            return 0;
        }

        if (a == null)
        {
            return 1;
        }

        if (b == null)
        {
            return -1;
        }

        ItemEntry metaA = catalog != null ? catalog.Get(a.key) : null;
        ItemEntry metaB = catalog != null ? catalog.Get(b.key) : null;

        string categoryA = catalog != null ? catalog.CategoryForKey(a.key) ?? "" : "";
        string categoryB = catalog != null ? catalog.CategoryForKey(b.key) ?? "" : "";
        int compare = string.Compare(categoryA, categoryB, System.StringComparison.Ordinal);
        if (compare != 0)
        {
            return compare;
        }

        string nameA = metaA != null && string.IsNullOrEmpty(metaA.displayName) == false ? metaA.displayName : a.key;
        string nameB = metaB != null && string.IsNullOrEmpty(metaB.displayName) == false ? metaB.displayName : b.key;
        compare = string.Compare(nameA, nameB, System.StringComparison.Ordinal);
        if (compare != 0)
        {
            return compare;
        }

        return string.Compare(a.key, b.key, System.StringComparison.Ordinal);
    }

    // 활성 캐릭터에 해당 키가 장착 중인지 (표시용 미러 조회)
    public bool IsEquippedOnActive(string key)
    {
        if (string.IsNullOrEmpty(ActiveCharcode))
        {
            return false;
        }

        if (equipMirror.TryGetValue(ActiveCharcode, out Dictionary<string, string> slots) == false)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> pair in slots)
        {
            if (pair.Value == key)
            {
                return true;
            }
        }

        return false;
    }

    // 장착 직후 배치 결과를 한 줄로 찍는다 (경로 태그 포함 — §7-1 D).
    //
    // "처음 켤 때는 발밑에 붙고 다시 장착하면 제자리로 간다"의 원인을 가르기 위한 계측이다.
    // 부팅 복원과 수동 장착을 **같은 형식**으로 찍어 두 줄을 나란히 비교할 수 있게 한다.
    // 후보: ① 소켓 해석이 부팅 시엔 실패해 origin으로 감 ② 캐릭터 스케일이 아직 확정 전이라
    // refDist 환산이 틀어짐 ③ 부착 자체는 맞는데 본 포즈가 아직 T-pose/원점.
    // 소켓 이름·우선순위·부모 본·월드 위치·캐릭터 lossyScale을 같이 찍으면 셋이 갈린다.
    private void LogEquipPlacement(string channel, GameObject target, string key, bool ok)
    {
        if (target == null)
        {
            return;
        }

        string slotId = "-";
        int priority = 0;
        EquipSocket socket = null;

        EquipEntry entry = equipCatalog != null ? equipCatalog.Get(key) : null;
        if (entry != null)
        {
            socket = EquipSlotResolver.Resolve(target, entry, out slotId, out priority);
        }

        string socketName = socket != null ? socket.gameObject.name : "없음";
        string boneName = "-";
        string socketWorld = "-";
        if (socket != null)
        {
            boneName = socket.transform.parent != null ? socket.transform.parent.name : "(부모없음)";
            socketWorld = socket.transform.position.ToString("F3");
        }

        // 실제로 붙은 물건의 위치 — 소켓 위치와 다르면 배치 수학이 틀어진 것이다
        string markerWorld = "-";
        EquipMarker[] markers = target.GetComponentsInChildren<EquipMarker>(true);
        foreach (EquipMarker marker in markers)
        {
            if (marker != null && marker.key == key)
            {
                markerWorld = marker.transform.position.ToString("F3");
                break;
            }
        }

        Debug.Log($"[MRInv/장착:{channel}] key={key} 성공={ok} | 소켓={socketName}(slot={slotId}, 우선순위={priority}, 본={boneName}) " +
                  $"| 소켓월드={socketWorld} 물건월드={markerWorld} " +
                  $"| 캐릭터 pos={target.transform.position.ToString("F3")} lossyScale={target.transform.lossyScale.x:F5} " +
                  $"| 우선순위 4 = origin(발밑)");
    }

    // 스토어 1개를 JSON 파일로 저장
    public void SaveStore(InvStore store)
    {
        if (store == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(store.ownerId))
        {
            Debug.LogWarning("[InventorySystemManager] SaveStore: ownerId가 비어 있습니다.");
            return;
        }

        try
        {
            string dir = GetSaveDir();
            if (Directory.Exists(dir) == false)
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(store, true);
            File.WriteAllText(GetStorePath(store.ownerId), json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[InventorySystemManager] 스토어 저장 실패({store.ownerId}): {e.Message}");
        }
    }

    // 로드된 모든 스토어 저장
    public void SaveAll()
    {
        if (mainStore != null)
        {
            SaveStore(mainStore);
        }

        foreach (KeyValuePair<string, InvStore> pair in charStores)
        {
            SaveStore(pair.Value);
        }
    }

    // 종료 시 전체 저장
    private void OnApplicationQuit()
    {
        SaveAll();
    }
}
