using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns Anchor-only placement. Equip targets are never resolved here, so an Anchor item
/// cannot fall through into character inventory/equipment handling.
/// </summary>
public sealed class AnchorManager : MonoBehaviour
{
    private static AnchorManager instance;

    [SerializeField] private AnchorCatalog catalog;
    [SerializeField] private AnchorTargetCatalog targetCatalog;

    private readonly List<AnchorScript> targets = new List<AnchorScript>();
    private readonly Dictionary<string, GameObject> runtimeTargets =
        new Dictionary<string, GameObject>(System.StringComparer.Ordinal);
    private bool inventoryMutation;

    public static bool HasInstance
    {
        get
        {
            return instance != null;
        }
    }

    public static AnchorManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<AnchorManager>();
            }

            if (instance == null && Application.isPlaying)
            {
                GameObject go = new GameObject("AnchorManager");
                instance = go.AddComponent<AnchorManager>();
                DontDestroyOnLoad(go);
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (catalog == null)
        {
            ItemCatalog itemCatalog = ItemCatalog.Default;
            catalog = itemCatalog != null && itemCatalog.AnchorCatalog != null
                ? itemCatalog.AnchorCatalog
                : Resources.Load<AnchorCatalog>("AnchorCatalog");
        }
        if (targetCatalog == null)
        {
            targetCatalog =
                Resources.Load<AnchorTargetCatalog>("AnchorTargetCatalog");
        }

        InventoryEvents.OnStoreChanged += HandleStoreChanged;
    }

    private void OnDestroy()
    {
        InventoryEvents.OnStoreChanged -= HandleStoreChanged;
        if (instance == this)
        {
            instance = null;
        }
    }

    public void Register(AnchorScript target)
    {
        if (target == null || targets.Contains(target))
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            AnchorScript registered = targets[i];
            if (registered != null &&
                registered.OwnerId == target.OwnerId &&
                string.Equals(
                    registered.AnchorName,
                    target.AnchorName,
                    System.StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[AnchorSystem] 중복 Anchor '{target.OwnerId}/{target.AnchorName}'는 등록하지 않습니다.",
                    target);
                return;
            }
        }

        targets.Add(target);
        if (target.IsPresented)
        {
            RefreshTarget(target);
        }
    }

    public void Unregister(AnchorScript target)
    {
        if (target == null)
        {
            return;
        }

        targets.Remove(target);
        DestroyRuntime(target, false);
    }

    public void PresentOwner(string ownerId, bool playChangeEffect = true)
    {
        SetOwnerPresentation(ownerId, true, playChangeEffect);
    }

    public void CollectOwner(string ownerId)
    {
        SetOwnerPresentation(ownerId, false, true);
    }

    public GameObject SpawnTarget(string key, Transform parent)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (runtimeTargets.TryGetValue(key, out GameObject current))
        {
            if (current != null)
            {
                return current;
            }

            runtimeTargets.Remove(key);
        }

        if (targetCatalog == null)
        {
            targetCatalog =
                Resources.Load<AnchorTargetCatalog>("AnchorTargetCatalog");
        }

        AnchorTargetEntry entry =
            targetCatalog != null ? targetCatalog.Get(key) : null;
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning(
                $"[AnchorSystem] AnchorTargetCatalog에서 '{key}' 프리팹을 찾을 수 없습니다.");
            return null;
        }

        GameObject target = Instantiate(entry.prefab, parent, false);
        target.name = entry.prefab.name;
        runtimeTargets.Add(key, target);
        return target;
    }

    public bool DespawnTarget(string key)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            runtimeTargets.TryGetValue(key, out GameObject target) == false)
        {
            return false;
        }

        runtimeTargets.Remove(key);
        if (target != null)
        {
            target.SetActive(false);
            Destroy(target);
        }

        return true;
    }

    public bool IsAnchorItem(string key)
    {
        ItemEntry item = ResolveItem(key);
        return item != null &&
               item.useType == ItemUseType.Anchor &&
               catalog != null &&
               catalog.Contains(key);
    }

    public bool TryPlaceAtDefault(string sourceOwnerId, int sourceSlot, string key)
    {
        AnchorEntry entry = ResolveAnchorEntry(key);
        if (entry == null)
        {
            return false;
        }

        AnchorScript target = FindDefaultTarget(entry.anchorName);
        if (target == null)
        {
            Debug.LogWarning(
                $"[AnchorSystem] 활성 Anchor '{entry.anchorName}'을 찾을 수 없습니다.");
            return false;
        }

        return TryPlace(sourceOwnerId, sourceSlot, key, target);
    }

    public bool TryPlaceAtScreenPosition(
        string sourceOwnerId,
        int sourceSlot,
        string key,
        Vector2 screenPosition)
    {
        AnchorEntry entry = ResolveAnchorEntry(key);
        if (entry == null ||
            TryFindTargetAtScreenPosition(
                screenPosition,
                entry.anchorName,
                out AnchorScript target) == false)
        {
            return false;
        }

        return TryPlace(sourceOwnerId, sourceSlot, key, target);
    }

    public bool IsPointerOverAnchor(Vector2 screenPosition)
    {
        return TryFindTargetAtScreenPosition(screenPosition, null, out _);
    }

    public bool TryPlace(
        string sourceOwnerId,
        int sourceSlot,
        string key,
        AnchorScript target)
    {
        InventorySystemManager inventory = InventorySystemManager.Instance;
        AnchorEntry entry = ResolveAnchorEntry(key);
        if (inventory == null ||
            entry == null ||
            target == null ||
            target.IsPresented == false ||
            string.IsNullOrWhiteSpace(sourceOwnerId) ||
            string.Equals(
                entry.anchorName,
                target.AnchorName,
                System.StringComparison.Ordinal) == false)
        {
            return false;
        }

        if (sourceOwnerId == target.OwnerId)
        {
            return false;
        }

        InvStore sourceStore = ResolveStore(inventory, sourceOwnerId);
        InvItemStack sourceStack = sourceStore != null ? sourceStore.FindBySlot(sourceSlot) : null;
        if (sourceStack == null || sourceStack.key != key || sourceStack.count <= 0)
        {
            return false;
        }

        InvStore targetStore = ResolveStore(inventory, target.OwnerId);
        if (targetStore == null)
        {
            return false;
        }

        InvItemStack previous = FindOccupant(targetStore, target.AnchorName);
        string previousKey = previous != null ? previous.key : null;

        inventoryMutation = true;
        bool previousReturned = false;
        bool placed = false;
        try
        {
            if (previous != null)
            {
                InvStore mainStore = inventory.GetMainStore();
                InvItemStack mainStack = mainStore != null ? mainStore.Find(previous.key) : null;
                previousReturned = inventory.MoveStackAmount(
                    target.OwnerId,
                    previous.slot,
                    InventorySystemManager.MainOwnerId,
                    mainStack != null ? mainStack.slot : -1,
                    1);
                if (previousReturned == false)
                {
                    return false;
                }
            }

            placed = inventory.MoveStackAmount(
                sourceOwnerId,
                sourceSlot,
                target.OwnerId,
                -1,
                1);

            if (placed == false && previousReturned)
            {
                InvItemStack rollback = inventory.GetMainStore().Find(previousKey);
                if (rollback != null)
                {
                    inventory.MoveStackAmount(
                        InventorySystemManager.MainOwnerId,
                        rollback.slot,
                        target.OwnerId,
                        -1,
                        1);
                }
            }
        }
        finally
        {
            inventoryMutation = false;
            RefreshTarget(target);
        }

        return placed;
    }

    public bool ReturnToMain(AnchoredItemHandle handle)
    {
        if (handle == null || handle.Target == null)
        {
            return false;
        }

        AnchorScript target = handle.Target;
        InventorySystemManager inventory = InventorySystemManager.Instance;
        InvStore targetStore = inventory != null ? ResolveStore(inventory, target.OwnerId) : null;
        InvItemStack stack = targetStore != null ? targetStore.Find(handle.ItemKey) : null;
        if (stack == null)
        {
            return false;
        }

        inventoryMutation = true;
        bool moved;
        try
        {
            InvStore mainStore = inventory.GetMainStore();
            InvItemStack mainStack = mainStore != null ? mainStore.Find(handle.ItemKey) : null;
            moved = inventory.MoveStackAmount(
                target.OwnerId,
                stack.slot,
                InventorySystemManager.MainOwnerId,
                mainStack != null ? mainStack.slot : -1,
                1);
        }
        finally
        {
            inventoryMutation = false;
            RefreshTarget(target);
        }

        return moved;
    }

    public void NotifyItemSelected(AnchoredItemHandle handle)
    {
        if (handle == null || string.IsNullOrWhiteSpace(handle.ItemKey))
        {
            return;
        }

        ItemEntry item = ResolveItem(handle.ItemKey);
        string displayName =
            item != null && string.IsNullOrWhiteSpace(item.displayName) == false
                ? item.displayName
                : handle.ItemKey;
        Debug.Log($"[Item] {displayName} 선택");
    }

    private AnchorEntry ResolveAnchorEntry(string key)
    {
        ItemEntry item = ResolveItem(key);
        if (item == null || item.useType != ItemUseType.Anchor || catalog == null)
        {
            return null;
        }

        return catalog.Get(key);
    }

    private static ItemEntry ResolveItem(string key)
    {
        InventorySystemManager inventory = InventorySystemManager.Instance;
        return inventory != null && inventory.Catalog != null
            ? inventory.Catalog.Get(key)
            : null;
    }

    private AnchorScript FindDefaultTarget(string anchorName)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            AnchorScript target = targets[i];
            if (target != null &&
                target.isActiveAndEnabled &&
                target.IsPresented &&
                string.Equals(
                    target.AnchorName,
                    anchorName,
                    System.StringComparison.Ordinal))
            {
                return target;
            }
        }

        return null;
    }

    private void HandleStoreChanged(string ownerId)
    {
        if (inventoryMutation || string.IsNullOrEmpty(ownerId))
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            AnchorScript target = targets[i];
            if (target != null &&
                target.IsPresented &&
                target.OwnerId == ownerId)
            {
                RefreshTarget(target);
            }
        }
    }

    private void RefreshTarget(AnchorScript target, bool playChangeEffect = true)
    {
        if (target == null ||
            target.isActiveAndEnabled == false ||
            target.IsPresented == false)
        {
            return;
        }

        InventorySystemManager inventory = InventorySystemManager.Instance;
        InvStore store = inventory != null ? ResolveStore(inventory, target.OwnerId) : null;
        InvItemStack occupant = store != null ? FindOccupant(store, target.AnchorName) : null;
        if (occupant == null)
        {
            DestroyRuntime(target, playChangeEffect);
            return;
        }

        AnchoredItemHandle currentHandle = target.RuntimeInstance != null
            ? target.RuntimeInstance.GetComponent<AnchoredItemHandle>()
            : null;
        if (currentHandle != null && currentHandle.ItemKey == occupant.key)
        {
            return;
        }

        DestroyRuntime(target, playChangeEffect);

        AnchorEntry entry = ResolveAnchorEntry(occupant.key);
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning(
                $"[AnchorSystem] '{occupant.key}'의 AnchorCatalog prefab을 찾을 수 없습니다.");
            return;
        }

        GameObject instanceObject = AnchorPlacement.Create(entry.prefab, target.PlacementAnchor);
        if (instanceObject == null)
        {
            return;
        }

        ItemEntry item = ResolveItem(occupant.key);
        EnsureInteractionPhysics(
            instanceObject,
            item != null ? item.itemClass : ItemClass.None);

        AnchoredItemHandle handle = instanceObject.GetComponent<AnchoredItemHandle>();
        if (handle == null)
        {
            handle = instanceObject.AddComponent<AnchoredItemHandle>();
        }
        handle.Initialize(target, occupant.key);
        target.SetRuntimeInstance(instanceObject);
        if (playChangeEffect)
        {
            PlayAnchorChangeEffect(instanceObject.transform.position);
        }
    }

    private void DestroyRuntime(AnchorScript target, bool playEffect)
    {
        if (target == null || target.RuntimeInstance == null)
        {
            return;
        }

        GameObject runtimeInstance = target.RuntimeInstance;
        target.SetRuntimeInstance(null);
        if (playEffect)
        {
            PlayAnchorChangeEffect(runtimeInstance.transform.position);
        }

        Destroy(runtimeInstance);
    }

    private InvItemStack FindOccupant(InvStore store, string anchorName)
    {
        if (store == null || store.stacks == null)
        {
            return null;
        }

        InvItemStack first = null;
        for (int i = 0; i < store.stacks.Count; i++)
        {
            InvItemStack stack = store.stacks[i];
            AnchorEntry entry = stack != null ? ResolveAnchorEntry(stack.key) : null;
            if (entry == null ||
                string.Equals(
                    entry.anchorName,
                    anchorName,
                    System.StringComparison.Ordinal) == false)
            {
                continue;
            }

            if (first == null)
            {
                first = stack;
            }
            else
            {
                Debug.LogWarning(
                    $"[AnchorSystem] '{store.ownerId}/{anchorName}'에 둘 이상의 아이템이 저장되어 첫 항목만 표시합니다.");
                break;
            }
        }

        return first;
    }

    private static InvStore ResolveStore(InventorySystemManager inventory, string ownerId)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(ownerId))
        {
            return null;
        }

        return ownerId == InventorySystemManager.MainOwnerId
            ? inventory.GetMainStore()
            : inventory.GetCharStore(ownerId);
    }

    private bool TryFindTargetAtScreenPosition(
        Vector2 screenPosition,
        string requiredAnchorName,
        out AnchorScript target)
    {
        target = null;
        Camera camera = ResolveInteractionCamera();
        if (camera == null)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            camera.ScreenPointToRay(screenPosition),
            float.MaxValue,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearest = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            AnchorScript candidate = null;
            AnchoredItemHandle handle =
                hits[i].collider.GetComponentInParent<AnchoredItemHandle>();
            if (handle != null && handle.Target != null)
            {
                candidate = FindTarget(
                    handle.Target.OwnerId,
                    requiredAnchorName);
            }

            if (candidate == null)
            {
                AnchorScript[] parentAnchors =
                    hits[i].collider.GetComponentsInParent<AnchorScript>(true);
                for (int anchorIndex = 0;
                     anchorIndex < parentAnchors.Length;
                     anchorIndex++)
                {
                    AnchorScript parentAnchor = parentAnchors[anchorIndex];
                    if (IsMatchingTarget(parentAnchor, null, requiredAnchorName))
                    {
                        candidate = parentAnchor;
                        break;
                    }
                }
            }

            if (candidate == null || hits[i].distance >= nearest)
            {
                continue;
            }

            nearest = hits[i].distance;
            target = candidate;
        }

        return target != null;
    }

    private AnchorScript FindTarget(string ownerId, string anchorName)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            AnchorScript candidate = targets[i];
            if (IsMatchingTarget(candidate, ownerId, anchorName))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsMatchingTarget(
        AnchorScript candidate,
        string ownerId,
        string anchorName)
    {
        return candidate != null &&
               candidate.isActiveAndEnabled &&
               candidate.IsPresented &&
               (string.IsNullOrEmpty(ownerId) ||
                candidate.OwnerId == ownerId) &&
               (string.IsNullOrEmpty(anchorName) ||
                string.Equals(
                    candidate.AnchorName,
                    anchorName,
                    System.StringComparison.Ordinal));
    }

    private static Camera ResolveInteractionCamera()
    {
        CanvasManager canvasManager = CanvasManager.Instance;
        if (canvasManager != null &&
            canvasManager.canvasChar != null &&
            canvasManager.canvasChar.worldCamera != null)
        {
            return canvasManager.canvasChar.worldCamera;
        }

        return Camera.main;
    }

    private void SetOwnerPresentation(string ownerId, bool presented, bool playChangeEffect)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            AnchorScript target = targets[i];
            if (target == null || target.OwnerId != ownerId)
            {
                continue;
            }

            target.SetPresented(presented);
            if (presented)
            {
                RefreshTarget(target, playChangeEffect);
            }
            else
            {
                DestroyRuntime(target, playChangeEffect);
            }
        }
    }

    private static void PlayAnchorChangeEffect(Vector3 worldPosition)
    {
        EffectManager effectManager = EffectManager.Instance;
        if (effectManager != null)
        {
            effectManager.PlayChangeEffectAt(worldPosition);
        }
    }

    private static void EnsureInteractionPhysics(GameObject instanceObject, ItemClass itemClass)
    {
        Collider[] colliders = instanceObject.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0 && TryGetLocalRendererBounds(instanceObject, out Bounds bounds))
        {
            BoxCollider box = instanceObject.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        if (itemClass == ItemClass.Doll &&
            instanceObject.GetComponentInChildren<Rigidbody>(true) == null)
        {
            Rigidbody body = instanceObject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }

    private static bool TryGetLocalRendererBounds(GameObject root, out Bounds localBounds)
    {
        localBounds = new Bounds();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.enabled == false)
            {
                continue;
            }

            Bounds world = renderer.bounds;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? world.min.x : world.max.x,
                            y == 0 ? world.min.y : world.max.y,
                            z == 0 ? world.min.z : world.max.z);
                        Vector3 local = root.transform.InverseTransformPoint(corner);
                        if (found)
                        {
                            localBounds.Encapsulate(local);
                        }
                        else
                        {
                            localBounds = new Bounds(local, Vector3.zero);
                            found = true;
                        }
                    }
                }
            }
        }

        return found;
    }

}
