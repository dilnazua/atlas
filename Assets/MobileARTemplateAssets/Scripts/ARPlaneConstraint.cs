using UnityEngine;
using Unity.Collections;
#if AR_FOUNDATION_PRESENT
using UnityEngine.XR.ARFoundation;
#endif
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Constrains an object's movement to stay within the boundaries of tracked AR planes.
/// Attach to any AR-spawned model to prevent it from being dragged off detected surfaces.
/// </summary>
public class ARPlaneConstraint : MonoBehaviour
{
#if AR_FOUNDATION_PRESENT
    ARPlaneManager m_PlaneManager;
    XRGrabInteractable m_Grab;
    Vector3 m_LastValidPosition;
    bool m_HasValidPosition;
    bool m_IsGrabbed;

    void Start()
    {
        m_PlaneManager = FindObjectOfType<ARPlaneManager>();
        m_LastValidPosition = transform.position;

        if (m_PlaneManager == null)
        {
            Debug.LogWarning("ARPlaneConstraint: No ARPlaneManager found. Constraint disabled.");
            enabled = false;
            return;
        }

        m_Grab = GetComponent<XRGrabInteractable>();
        if (m_Grab != null)
        {
            // Instantaneous mode lets us override position in LateUpdate;
            // physics-based modes (VelocityTracking / Kinematic) would fight us.
            m_Grab.movementType = XRGrabInteractable.MovementType.Instantaneous;
            m_Grab.throwOnDetach = false;

            m_Grab.selectEntered.AddListener(_ => m_IsGrabbed = true);
            m_Grab.selectExited.AddListener(OnReleased);
        }

        Debug.Log($"ARPlaneConstraint: initialized on '{gameObject.name}'");
    }

    void OnDestroy()
    {
        if (m_Grab != null)
        {
            m_Grab.selectEntered.RemoveAllListeners();
            m_Grab.selectExited.RemoveListener(OnReleased);
        }
    }

    void OnReleased(SelectExitEventArgs _)
    {
        m_IsGrabbed = false;
        ConstrainNow();
    }

    void LateUpdate()
    {
        if (m_PlaneManager == null || m_PlaneManager.trackables.count == 0) return;
        ConstrainNow();
    }

    void ConstrainNow()
    {
        if (m_PlaneManager == null || m_PlaneManager.trackables.count == 0) return;

        Vector3 currentPos = transform.position;

        // If we're already inside a plane boundary, just snap Y to the surface
        foreach (var plane in m_PlaneManager.trackables)
        {
            if (TryProjectOntoPlane(plane, currentPos, out Vector3 projected))
            {
                m_LastValidPosition = projected;
                transform.position = projected;
                m_HasValidPosition = true;
                return;
            }
        }

        // Off all planes — find the nearest boundary edge and clamp
        if (FindNearestEdgeOnAnyPlane(currentPos, out Vector3 nearest))
        {
            m_LastValidPosition = nearest;
            transform.position = nearest;
            m_HasValidPosition = true;
        }
        else if (m_HasValidPosition)
        {
            transform.position = m_LastValidPosition;
        }
    }

    // ────────────────── Plane / polygon helpers ──────────────────

    bool TryProjectOntoPlane(ARPlane plane, Vector3 worldPos, out Vector3 projected)
    {
        projected = worldPos;
        var boundary = plane.boundary;
        if (boundary.Length < 3) return false;

        Vector3 localPos = plane.transform.InverseTransformPoint(worldPos);
        Vector2 local2D = new Vector2(localPos.x, localPos.z);

        if (!IsInsidePolygon(local2D, boundary)) return false;

        projected = plane.transform.TransformPoint(new Vector3(localPos.x, 0f, localPos.z));
        return true;
    }

    bool FindNearestEdgeOnAnyPlane(Vector3 worldPos, out Vector3 nearest)
    {
        nearest = worldPos;
        float bestSqr = float.MaxValue;
        bool found = false;

        foreach (var plane in m_PlaneManager.trackables)
        {
            var boundary = plane.boundary;
            if (boundary.Length < 3) continue;

            Vector3 localPos = plane.transform.InverseTransformPoint(worldPos);
            Vector2 local2D = new Vector2(localPos.x, localPos.z);
            Vector2 edge2D = NearestPointOnPolygonEdge(local2D, boundary);
            Vector3 edgeWorld = plane.transform.TransformPoint(new Vector3(edge2D.x, 0f, edge2D.y));

            float sqr = Vector3.SqrMagnitude(worldPos - edgeWorld);
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = edgeWorld;
                found = true;
            }
        }

        return found;
    }

    static bool IsInsidePolygon(Vector2 point, NativeArray<Vector2> polygon)
    {
        int count = polygon.Length;
        bool inside = false;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];

            if ((pi.y > point.y) != (pj.y > point.y) &&
                point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    static Vector2 NearestPointOnPolygonEdge(Vector2 point, NativeArray<Vector2> polygon)
    {
        int count = polygon.Length;
        float bestDist = float.MaxValue;
        Vector2 bestPoint = polygon[0];

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            Vector2 closest = NearestPointOnSegment(point, polygon[i], polygon[next]);
            float dist = Vector2.SqrMagnitude(point - closest);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestPoint = closest;
            }
        }

        return bestPoint;
    }

    static Vector2 NearestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float sqrLen = Vector2.Dot(ab, ab);
        if (sqrLen < 1e-8f) return a;

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqrLen);
        return a + ab * t;
    }
#endif
}
