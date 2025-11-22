using System;
using System.Collections.Generic;
using UnityEngine;

public class RotateTransformGizmo : MonoBehaviour
{
    [SerializeField] private RotateGizmoSettings Settings;

    private List<SphereCollider> xAxisColliders = new List<SphereCollider>();
    private List<SphereCollider> yAxisColliders = new List<SphereCollider>();
    private List<SphereCollider> zAxisColliders = new List<SphereCollider>();

    /// <summary>
    /// Initialize rotate gizmo colliders and visual elements
    /// </summary>
    public void InitializeGizmo()
    {
        // Create multiple colliders around each rotation circle for better interaction
        int segments = Settings.ColliderSegments;

        // X-axis (red) - rotation around right axis
        xAxisColliders = CreateTorusColliders("XAxis_Rotate", Vector3.right, Vector3.up, Settings.GizmoRadius, segments);

        // Y-axis (green) - rotation around up axis
        yAxisColliders = CreateTorusColliders("YAxis_Rotate", Vector3.up, Vector3.right, Settings.GizmoRadius, segments);

        // Z-axis (blue) - rotation around forward axis
        zAxisColliders = CreateTorusColliders("ZAxis_Rotate", Vector3.forward, Vector3.right, Settings.GizmoRadius, segments);
    }

    /// <summary>
    /// Render the rotate gizmo visuals
    /// </summary>
    public void RenderGizmo(Axis hoveredAxis, Axis draggedAxis)
    {
        // Determine colors based on hover/drag state
        Color xColor = GetAxisColor(Axis.X, hoveredAxis, draggedAxis, Settings.XAxisColor);
        Color yColor = GetAxisColor(Axis.Y, hoveredAxis, draggedAxis, Settings.YAxisColor);
        Color zColor = GetAxisColor(Axis.Z, hoveredAxis, draggedAxis, Settings.ZAxisColor);

        DrawRotationCircle(transform.right, xColor);
        DrawRotationCircle(transform.up, yColor);
        DrawRotationCircle(transform.forward, zColor);

        void DrawRotationCircle(Vector3 axis, Color color)
        {
            GizmoDrawer.DrawCircle(transform.position, axis, Settings.GizmoRadius, Settings.GizmoThickness, color, 32, true);
        }
    }

    /// <summary>
    /// Get the axis corresponding to a collider
    /// </summary>
    public Axis GetAxisFromCollider(Collider col)
    {
        SphereCollider sphereCol = col as SphereCollider;
        if (sphereCol == null) return Axis.None;

        if (xAxisColliders.Contains(sphereCol)) return Axis.X;
        if (yAxisColliders.Contains(sphereCol)) return Axis.Y;
        if (zAxisColliders.Contains(sphereCol)) return Axis.Z;

        return Axis.None;
    }

    private Color GetAxisColor(Axis axis, Axis hovered, Axis dragged, Color baseColor)
    {
        if (dragged == axis)
            return Settings.DraggedColor;
        if (hovered == axis)
            return Settings.HoverColor;
        return baseColor;
    }

    private List<SphereCollider> CreateTorusColliders(string baseName, Vector3 axisNormal, Vector3 tangent, float radius, int segments)
    {
        List<SphereCollider> colliders = new List<SphereCollider>();

        // Create colliders distributed around the circle
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;

            // Calculate position on the circle with better stability
            Vector3 perpendicular = Vector3.Cross(axisNormal, tangent).normalized;

            // Ensure we have valid perpendicular vector
            if (perpendicular.sqrMagnitude < 0.001f)
            {
                // Fallback: use a different tangent
                tangent = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
                perpendicular = Vector3.Cross(axisNormal, tangent).normalized;
            }

            Vector3 position = (tangent * Mathf.Cos(angle) + perpendicular * Mathf.Sin(angle)) * radius;

            GameObject handle = CreateHandle($"{baseName}_{i}");
            handle.transform.localPosition = position;

            SphereCollider collider = handle.AddComponent<SphereCollider>();
            collider.radius = Settings.ColliderThickness;

            colliders.Add(collider);
        }

        return colliders;
    }
    private GameObject CreateHandle(string name)
    {
        GameObject handle = new GameObject(name);
        handle.transform.SetParent(transform, false);
        handle.transform.localPosition = Vector3.zero;
        handle.transform.localRotation = Quaternion.identity;
        handle.layer = GlobalGizmoManager.Instance.GizmosLayerIndex;
        return handle;
    }
}

[Serializable]
public struct RotateGizmoSettings
{
    [Header("Manual Settings")]
    public float GizmoThickness;
    public float GizmoRadius;
    public float ColliderThickness;

    [Header("Collider Settings")]
    [Tooltip("Number of sphere colliders per rotation ring (more = easier to select but more expensive)")]
    [Range(8, 64)]
    public int ColliderSegments;

    [Header("Axis Colors")]
    public Color XAxisColor;
    public Color YAxisColor;
    public Color ZAxisColor;

    [Header("Interaction Colors")]
    public Color HoverColor;
    public Color DraggedColor;
}