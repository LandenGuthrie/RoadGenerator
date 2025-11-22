using System;
using UnityEngine;

/// <summary>
/// Global singleton manager for the entire gizmo system
/// Coordinates between gizmo rendering and transform manipulation
/// </summary>
public class GlobalGizmoManager : MonoBehaviour
{
    public static GlobalGizmoManager Instance
    {
        get;
        private set;
    }

    [SerializeField] private GlobalGizmoManagerSettings References;

    public GizmoDrawer Gizmos => References.WSGizmos;
    public TransformGizmoManager TransformGizmoManager => References.TransformGizmoManager;

    public Camera GizmosCamera => References.GizmosCamera;
    public int GizmosLayerIndex => References.GizmosLayerIndex;
    public LayerMask GizmosLayer => References.GizmosLayer;

    public void Awake()
    {
        Instance = this;
    }
    public void Start()
    {
        Initialize();
    }
    public void Update()
    {
        UpdateGizmos();
    }

    /// <summary>
    /// Initialize all gizmo systems
    /// </summary>
    public void Initialize()
    {
        References.WSGizmos.InitializeGizmos();
        References.TransformGizmoManager.InitializeGizmos();
    }

    /// <summary>
    /// Update all gizmo rendering and interaction
    /// </summary>
    public void UpdateGizmos()
    {
        References.TransformGizmoManager.RenderGizmos();
        References.WSGizmos.RenderGizmos();
    }

    /// <summary>
    /// Clean up all gizmo resources
    /// </summary>
    public void ReleaseGizmos()
    {
        References.TransformGizmoManager.ReleaseGizmos();
    }
}

[Serializable]
public struct GlobalGizmoManagerSettings
{
    public GizmoDrawer WSGizmos;
    public TransformGizmoManager TransformGizmoManager;

    public Camera GizmosCamera;
    public int GizmosLayerIndex;
    public LayerMask GizmosLayer;
}