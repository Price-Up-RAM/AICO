using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

/// <summary>
/// Declares one physical Anchor. Inventory ownership is persisted under ownerId,
/// while placementAnchor supplies only the runtime position and rotation.
/// </summary>
public sealed class AnchorScript : MonoBehaviour
{
    [SerializeField] private string ownerId = "desk1";
    [SerializeField, FormerlySerializedAs("slotId")]
    private string anchorName = "doll1";
    [SerializeField] private Transform placementAnchor;
    [SerializeField] private bool presentOnEnable = true;

    internal GameObject RuntimeInstance { get; private set; }
    internal bool IsPresented { get; private set; }

    private Matrix4x4 previousPlacementMatrix;
    private Quaternion previousPlacementRotation;
    private Vector3 previousPlacementPosition;
    private Vector3 previousPlacementScale;
    private Transform trackedPlacementAnchor;
    private bool hasPlacementSnapshot;

    public string OwnerId
    {
        get
        {
            return ownerId;
        }
    }

    public string AnchorName
    {
        get
        {
            return anchorName;
        }
    }

    public Transform PlacementAnchor
    {
        get
        {
            return placementAnchor != null ? placementAnchor : transform;
        }
    }

    internal void SetRuntimeInstance(GameObject instance)
    {
        RuntimeInstance = instance;
        CapturePlacementSnapshot();
    }

    internal void SetPresented(bool presented)
    {
        IsPresented = presented;
    }

    private void Awake()
    {
        IsPresented = presentOnEnable;
        CapturePlacementSnapshot();
    }

    private void OnEnable()
    {
        CapturePlacementSnapshot();
        if (Application.isPlaying)
        {
            AnchorManager.Instance.Register(this);
        }
    }

    private void LateUpdate()
    {
        Transform currentAnchor = PlacementAnchor;
        if (currentAnchor == null)
        {
            hasPlacementSnapshot = false;
            trackedPlacementAnchor = null;
            return;
        }

        if (hasPlacementSnapshot == false || trackedPlacementAnchor != currentAnchor)
        {
            CapturePlacementSnapshot();
            return;
        }

        Vector3 currentPosition = currentAnchor.position;
        Quaternion currentRotation = currentAnchor.rotation;
        Vector3 currentScale = currentAnchor.lossyScale;
        bool placementMoved =
            (currentPosition - previousPlacementPosition).sqrMagnitude > 0.00000001f ||
            Quaternion.Angle(currentRotation, previousPlacementRotation) > 0.001f ||
            (currentScale - previousPlacementScale).sqrMagnitude > 0.00000001f;

        if (placementMoved && RuntimeInstance != null)
        {
            Matrix4x4 currentMatrix = currentAnchor.localToWorldMatrix;
            Matrix4x4 placementDelta = currentMatrix * previousPlacementMatrix.inverse;
            Quaternion rotationDelta =
                currentRotation * Quaternion.Inverse(previousPlacementRotation);

            AnchoredItemHandle handle =
                RuntimeInstance.GetComponent<AnchoredItemHandle>();
            if (handle != null)
            {
                handle.ApplyPlacementDelta(placementDelta, rotationDelta);
            }
        }

        previousPlacementMatrix = currentAnchor.localToWorldMatrix;
        previousPlacementRotation = currentRotation;
        previousPlacementPosition = currentPosition;
        previousPlacementScale = currentScale;
    }

    private void OnDisable()
    {
        if (Application.isPlaying && AnchorManager.HasInstance)
        {
            AnchorManager.Instance.Unregister(this);
        }
    }

    private void OnValidate()
    {
        ownerId = ownerId != null ? ownerId.Trim() : string.Empty;
        anchorName = anchorName != null ? anchorName.Trim() : string.Empty;
    }

    private void CapturePlacementSnapshot()
    {
        Transform currentAnchor = PlacementAnchor;
        trackedPlacementAnchor = currentAnchor;
        if (currentAnchor == null)
        {
            hasPlacementSnapshot = false;
            return;
        }

        previousPlacementMatrix = currentAnchor.localToWorldMatrix;
        previousPlacementRotation = currentAnchor.rotation;
        previousPlacementPosition = currentAnchor.position;
        previousPlacementScale = currentAnchor.lossyScale;
        hasPlacementSnapshot = true;
    }
}

/// <summary>
/// Runtime marker added to an instantiated Anchor item. It keeps item interaction
/// independent from Equip markers and remains valid even if a Rigidbody moves the item.
/// </summary>
public sealed class AnchoredItemHandle :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const float LongPressSeconds = 0.7f;
    private const float ClickMoveTolerance = 24f;

    public AnchorScript Target { get; private set; }
    public string ItemKey { get; private set; }

    private Rigidbody rootBody;
    private bool pointerPressed;
    private bool pointerInside;
    private float pressedAt;
    private Vector2 pressedPosition;

    public void Initialize(AnchorScript target, string itemKey)
    {
        Target = target;
        ItemKey = itemKey;
        rootBody = GetComponent<Rigidbody>();
        ResetPointer();
    }

    private void Update()
    {
        if (pointerPressed == false)
        {
            return;
        }

        if (pointerInside == false ||
            Vector2.Distance(pressedPosition, Input.mousePosition) > ClickMoveTolerance)
        {
            ResetPointer();
            return;
        }

        if (Time.unscaledTime - pressedAt < LongPressSeconds)
        {
            return;
        }

        ResetPointer();
        if (AnchorManager.Instance.ReturnToMain(this) == false)
        {
            Debug.LogWarning(
                $"[AnchorSystem] '{ItemKey}'을 MAIN 인벤토리로 반환하지 못했습니다.");
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        pointerPressed = true;
        pointerInside = true;
        pressedAt = Time.unscaledTime;
        pressedPosition = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            pointerPressed == false)
        {
            return;
        }

        bool isClick =
            pointerInside &&
            Time.unscaledTime - pressedAt < LongPressSeconds &&
            Vector2.Distance(pressedPosition, eventData.position) <= ClickMoveTolerance;

        ResetPointer();
        if (isClick)
        {
            AnchorManager.Instance.NotifyItemSelected(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerPressed = false;
    }

    internal void ApplyPlacementDelta(
        Matrix4x4 placementDelta,
        Quaternion rotationDelta)
    {
        if (rootBody != null)
        {
            rootBody.position =
                placementDelta.MultiplyPoint3x4(rootBody.position);
            rootBody.rotation = rotationDelta * rootBody.rotation;
            rootBody.linearVelocity = rotationDelta * rootBody.linearVelocity;
            rootBody.angularVelocity = rotationDelta * rootBody.angularVelocity;
            return;
        }

        transform.SetPositionAndRotation(
            placementDelta.MultiplyPoint3x4(transform.position),
            rotationDelta * transform.rotation);
    }

    private void OnDisable()
    {
        ResetPointer();
    }

    private void ResetPointer()
    {
        pointerPressed = false;
        pointerInside = false;
    }
}
