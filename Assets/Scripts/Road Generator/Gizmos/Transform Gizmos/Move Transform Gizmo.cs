using System;
using UnityEngine;

public class MoveTransformGizmo : MonoBehaviour
{
    [SerializeField] private MoveGizmoSettings Settings;

    /// <summary>
    /// Initialize move gizmo colliders and visual elements
    /// </summary>
    public void InitializeGizmo()
    {
        Settings.XAxis = CreateBoxCollider("XAxis_Move",
            new Vector3(Settings.GizmoSize, Settings.ColliderThickness, Settings.ColliderThickness),
            Vector3.right * Settings.GizmoSize * 0.5f);

        Settings.YAxis = CreateBoxCollider("YAxis_Move",
            new Vector3(Settings.ColliderThickness, Settings.GizmoSize, Settings.ColliderThickness),
            Vector3.up * Settings.GizmoSize * 0.5f);

        Settings.ZAxis = CreateBoxCollider("ZAxis_Move",
            new Vector3(Settings.ColliderThickness, Settings.ColliderThickness, Settings.GizmoSize),
            Vector3.forward * Settings.GizmoSize * 0.5f);

        Settings.XYPlane = CreateBoxCollider("XYPlane_Move",
            new Vector3(Settings.GizmoSize * 0.3f, Settings.GizmoSize * 0.3f, Settings.ColliderThickness * 0.5f),
            (Vector3.right + Vector3.up) * Settings.GizmoSize * 0.25f);

        Settings.XZPlane = CreateBoxCollider("XZPlane_Move",
            new Vector3(Settings.GizmoSize * 0.3f, Settings.ColliderThickness * 0.5f, Settings.GizmoSize * 0.3f),
            (Vector3.right + Vector3.forward) * Settings.GizmoSize * 0.25f);

        Settings.YZPlane = CreateBoxCollider("YZPlane_Move",
            new Vector3(Settings.ColliderThickness * 0.5f, Settings.GizmoSize * 0.3f, Settings.GizmoSize * 0.3f),
            (Vector3.up + Vector3.forward) * Settings.GizmoSize * 0.25f);

        Settings.Center = CreateSphereCollider("Center_Move",
            Settings.ColliderThickness * 1.5f);
    }

    /// <summary>
    /// Render the move gizmo visuals
    /// </summary>
    public void RenderGizmo(Axis hoveredAxis, Axis draggedAxis)
    {
        // Determine colors based on hover/drag state
        Color xColor = GetAxisColor(Axis.X, hoveredAxis, draggedAxis, Settings.XAxisColor);
        Color yColor = GetAxisColor(Axis.Y, hoveredAxis, draggedAxis, Settings.YAxisColor);
        Color zColor = GetAxisColor(Axis.Z, hoveredAxis, draggedAxis, Settings.ZAxisColor);
        Color xyColor = GetAxisColor(Axis.XY, hoveredAxis, draggedAxis, Settings.PlaneColor);
        Color xzColor = GetAxisColor(Axis.XZ, hoveredAxis, draggedAxis, Settings.PlaneColor);
        Color yzColor = GetAxisColor(Axis.YZ, hoveredAxis, draggedAxis, Settings.PlaneColor);
        Color centerColor = GetAxisColor(Axis.Center, hoveredAxis, draggedAxis, Settings.CenterColor);

        DrawAxisArrow(Vector3.zero, transform.right * Settings.GizmoSize, xColor);
        DrawAxisArrow(Vector3.zero, transform.up * Settings.GizmoSize, yColor);
        DrawAxisArrow(Vector3.zero, transform.forward * Settings.GizmoSize, zColor);

        DrawPlaneSquare((Vector3.right + Vector3.up) * Settings.GizmoSize * 0.25f, transform.right, transform.up, Settings.GizmoSize * 0.15f, xyColor);
        DrawPlaneSquare((Vector3.right + Vector3.forward) * Settings.GizmoSize * 0.25f, transform.right, transform.forward, Settings.GizmoSize * 0.15f, xzColor);
        DrawPlaneSquare((Vector3.up + Vector3.forward) * Settings.GizmoSize * 0.25f, transform.up, transform.forward, Settings.GizmoSize * 0.15f, yzColor);

        GizmoDrawer.DrawSphere(transform.position, Settings.GizmoThickness * 2, centerColor, true);

        void DrawAxisArrow(Vector3 start, Vector3 end, Color color)
        {
            GizmoDrawer.DrawArrow(transform.position + start, transform.position + end, color, Settings.GizmoThickness, true);
        }

        void DrawPlaneSquare(Vector3 center, Vector3 axis1, Vector3 axis2, float size, Color color)
        {
            Vector3 corner1 = center - axis1 * size - axis2 * size;
            Vector3 corner2 = center + axis1 * size - axis2 * size;
            Vector3 corner3 = center + axis1 * size + axis2 * size;
            Vector3 corner4 = center - axis1 * size + axis2 * size;

            GizmoDrawer.DrawLine(transform.position + corner1, transform.position + corner2, color, Settings.GizmoThickness * 0.5f, true);
            GizmoDrawer.DrawLine(transform.position + corner2, transform.position + corner3, color, Settings.GizmoThickness * 0.5f, true);
            GizmoDrawer.DrawLine(transform.position + corner3, transform.position + corner4, color, Settings.GizmoThickness * 0.5f, true);
            GizmoDrawer.DrawLine(transform.position + corner4, transform.position + corner1, color, Settings.GizmoThickness * 0.5f, true);
        }
    }

    /// <summary>
    /// Get the axis corresponding to a collider
    /// </summary>
    public Axis GetAxisFromCollider(Collider col)
    {
        if (col == Settings.XAxis) return Axis.X;
        if (col == Settings.YAxis) return Axis.Y;
        if (col == Settings.ZAxis) return Axis.Z;
        if (col == Settings.XYPlane) return Axis.XY;
        if (col == Settings.XZPlane) return Axis.XZ;
        if (col == Settings.YZPlane) return Axis.YZ;
        if (col == Settings.Center) return Axis.Center;

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

    private BoxCollider CreateBoxCollider(string name, Vector3 size, Vector3 center)
    {
        GameObject handle = CreateHandle(name);
        BoxCollider collider = handle.AddComponent<BoxCollider>();
        collider.size = size;
        collider.center = center;
        return collider;
    }

    private SphereCollider CreateSphereCollider(string name, float radius)
    {
        GameObject handle = CreateHandle(name);
        SphereCollider collider = handle.AddComponent<SphereCollider>();
        collider.radius = radius;
        return collider;
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
public struct MoveGizmoSettings
{
    [Header("Manual Settings")]
    public float GizmoThickness;
    public float GizmoSize;
    public float ColliderThickness;

    [Header("Axis Colors")]
    public Color XAxisColor;
    public Color YAxisColor;
    public Color ZAxisColor;
    public Color PlaneColor;
    public Color CenterColor;

    [Header("Interaction Colors")]
    public Color HoverColor;
    public Color DraggedColor;

    [Header("Auto Populated Settings")]
    public SphereCollider Center;
    public BoxCollider XAxis, YAxis, ZAxis;
    public BoxCollider XYPlane, XZPlane, YZPlane;
}