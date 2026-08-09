using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates or toggles a physical prefab at a named anchor below a target object.
/// Dynamic Rigidbodies stay dynamic and are never parented to the anchor.
/// </summary>
public static class AnchorPlacement
{
    private const float SurfaceClearance = 0.01f;
    private static readonly Dictionary<PlacementKey, GameObject> Instances =
        new Dictionary<PlacementKey, GameObject>();

    /// <summary>
    /// The complete placement API: item prefab, placement target, and anchor name.
    /// A hidden instance is shown where it was left; a missing instance is created at the anchor.
    /// </summary>
    public static GameObject Toggle(
        GameObject itemPrefab,
        GameObject placementTarget,
        string anchorName)
    {
        if (itemPrefab == null || placementTarget == null || string.IsNullOrWhiteSpace(anchorName))
        {
            Debug.LogWarning(
                "[AnchorPlacement] Item prefab, placement target, and anchor name are all required.");
            return null;
        }

        PlacementKey key = new PlacementKey(
            itemPrefab.GetInstanceID(),
            placementTarget.GetInstanceID(),
            anchorName.Trim());

        if (Instances.TryGetValue(key, out GameObject instance) && instance != null)
        {
            instance.SetActive(instance.activeSelf == false);
            return instance;
        }

        Transform anchor = FindAnchor(placementTarget, anchorName);
        if (anchor == null)
        {
            Debug.LogWarning(
                $"[AnchorPlacement] Anchor '{anchorName}' was not found below '{placementTarget.name}'.",
                placementTarget);
            return null;
        }

        instance = Create(itemPrefab, anchor);
        Instances[key] = instance;
        return instance;
    }

    public static GameObject Create(GameObject itemPrefab, Transform anchor)
    {
        if (itemPrefab == null || anchor == null)
        {
            return null;
        }

        GameObject instance = UnityEngine.Object.Instantiate(
            itemPrefab,
            anchor.position,
            anchor.rotation);
        instance.name = itemPrefab.name;
        PlaceAboveSurface(instance, anchor);
        return instance;
    }

    private static Transform FindAnchor(GameObject placementTarget, string anchorName)
    {
        string requestedName = anchorName.Trim();
        Transform[] transforms = placementTarget.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null &&
                string.Equals(candidate.name, requestedName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void PlaceAboveSurface(GameObject instance, Transform anchor)
    {
        instance.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

        Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (body == null || body.isKinematic)
            {
                continue;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();

        Vector3 normal = anchor.up.sqrMagnitude > 1e-6f
            ? anchor.up.normalized
            : Vector3.up;
        float lowestProjection;
        if (TryGetLowestProjection(instance, normal, out lowestProjection))
        {
            float surfaceProjection = Vector3.Dot(anchor.position, normal);
            float lift = surfaceProjection + SurfaceClearance - lowestProjection;
            instance.transform.position += normal * Mathf.Max(0f, lift);
            Physics.SyncTransforms();
        }
    }

    private static bool TryGetLowestProjection(
        GameObject instance,
        Vector3 axis,
        out float lowestProjection)
    {
        lowestProjection = float.PositiveInfinity;
        bool found = false;

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.enabled == false || collider.isTrigger)
            {
                continue;
            }

            found |= AccumulateMinimum(collider.bounds, axis, ref lowestProjection);
        }

        if (found)
        {
            return true;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.enabled == false)
            {
                continue;
            }

            found |= AccumulateMinimum(renderer.bounds, axis, ref lowestProjection);
        }

        return found;
    }

    private static bool AccumulateMinimum(
        Bounds bounds,
        Vector3 axis,
        ref float lowestProjection)
    {
        Vector3 extents = bounds.extents;
        if (extents.sqrMagnitude <= 1e-12f)
        {
            return false;
        }

        float projectedRadius =
            Mathf.Abs(axis.x) * extents.x +
            Mathf.Abs(axis.y) * extents.y +
            Mathf.Abs(axis.z) * extents.z;
        float minimum = Vector3.Dot(bounds.center, axis) - projectedRadius;
        lowestProjection = Mathf.Min(lowestProjection, minimum);
        return true;
    }

    private readonly struct PlacementKey : IEquatable<PlacementKey>
    {
        private readonly int prefabId;
        private readonly int targetId;
        private readonly string anchorName;

        public PlacementKey(int prefabId, int targetId, string anchorName)
        {
            this.prefabId = prefabId;
            this.targetId = targetId;
            this.anchorName = anchorName.ToUpperInvariant();
        }

        public bool Equals(PlacementKey other)
        {
            return prefabId == other.prefabId &&
                   targetId == other.targetId &&
                   anchorName == other.anchorName;
        }

        public override bool Equals(object obj)
        {
            return obj is PlacementKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = prefabId;
                hash = (hash * 397) ^ targetId;
                hash = (hash * 397) ^ anchorName.GetHashCode();
                return hash;
            }
        }
    }
}
