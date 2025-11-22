using System;
using UnityEngine;

public class ScaleTransformGizmo : MonoBehaviour
{
    [SerializeField] private ScaleGizmoSettings Settings;

    /// <summary>
    /// Initialize scale gizmo colliders and visual elements
    /// </summary>
    public void InitializeGizmo()
    {
        Settings.XAxis = CreateBoxCollider("XScale",
            new Vector3(Settings.GizmoSize, Settings.ColliderThickness, Settings.ColliderThickness),
            Vector3.right * Settings.GizmoSize * 0.5f);

        Settings.YAxis = CreateBoxCollider("YScale",
            new Vector3(Settings.ColliderThickness, Settings.GizmoSize, Settings.ColliderThickness),
            Vector3.up * Settings.GizmoSize * 0.5f);

        Settings.ZAxis = CreateBoxCollider("ZScale",
            new Vector3(Settings.ColliderThickness, Settings.ColliderThickness, Settings.GizmoSize),
            Vector3.forward * Settings.GizmoSize * 0.5f);

        Settings.Center = CreateSphereCollider("CenterScale",
            Settings.ColliderThickness * 1.5f);
    }

    /// <summary>
    /// Render the scale gizmo visuals
    /// </summary>
    public void RenderGizmo(Axis hoveredAxis, Axis draggedAxis)
    {
        // Determine colors based on hover/drag state
        Color xColor = GetAxisColor(Axis.X, hoveredAxis, draggedAxis, Settings.XAxisColor);
        Color yColor = GetAxisColor(Axis.Y, hoveredAxis, draggedAxis, Settings.YAxisColor);
        Color zColor = GetAxisColor(Axis.Z, hoveredAxis, draggedAxis, Settings.ZAxisColor);
        Color centerColor = GetAxisColor(Axis.Center, hoveredAxis, draggedAxis, Settings.CenterColor);

        DrawAxisLine(Vector3.zero, transform.right * Settings.GizmoSize, xColor);
        DrawAxisLine(Vector3.zero, transform.up * Settings.GizmoSize, yColor);
        DrawAxisLine(Vector3.zero, transform.forward * Settings.GizmoSize, zColor);

        GizmoDrawer.DrawCube(transform.position + transform.right * Settings.GizmoSize, Quaternion.identity, Vector3.one * Settings.GizmoThickness * 3,
            xColor, true);
        GizmoDrawer.DrawCube(transform.position + transform.up * Settings.GizmoSize, Quaternion.identity, Vector3.one * Settings.GizmoThickness * 3,
            yColor, true);
        GizmoDrawer.DrawCube(transform.position + transform.forward * Settings.GizmoSize, Quaternion.identity, Vector3.one * Settings.GizmoThickness * 3,
            zColor, true);

        GizmoDrawer.DrawCube(transform.position, Quaternion.identity, Vector3.one * Settings.GizmoThickness * 4,
            centerColor, true);

        void DrawAxisLine(Vector3 start, Vector3 end, Color color)
        {
            GizmoDrawer.DrawLine(transform.position + start, transform.position + end, color, Settings.GizmoThickness, true);
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
public struct ScaleGizmoSettings
{
    [Header("Manual Settings")]
    public float GizmoThickness;
    public float GizmoSize;
    public float ColliderThickness;

    [Header("Axis Colors")]
    public Color XAxisColor;
    public Color YAxisColor;
    public Color ZAxisColor;
    public Color CenterColor;

    [Header("Interaction Colors")]
    public Color HoverColor;
    public Color DraggedColor;

    [Header("Auto Populated Settings")]
    public SphereCollider Center;
    public BoxCollider XAxis, YAxis, ZAxis;
}