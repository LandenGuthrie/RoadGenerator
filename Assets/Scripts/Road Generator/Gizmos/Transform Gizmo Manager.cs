using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TransformGizmoManager : MonoBehaviour
{
    [SerializeField] private MoveTransformGizmo MoveGizmo;
    [SerializeField] private ScaleTransformGizmo ScaleGizmo;
    [SerializeField] private RotateTransformGizmo RotateGizmo;

    [Header("Snapping Settings")]
    [SerializeField] private float GridSize = 1f;
    [SerializeField] private float RotationIncrement = 15f;
    [SerializeField] private float ScaleIncrement = 0.25f;

    // Events for external systems to hook into
    public event Action<Transform> OnTransformChanged;
    public event Action<Axis> OnAxisHoverChanged;
    public event Action OnDragStarted;
    public event Action OnDragEnded;

    // Static state
    private static Transform targetTransform;
    private static TransformMode currentMode = TransformMode.None;
    public static TransformMode CurrentMode => currentMode;
    public static Transform Target => targetTransform;

    private static Axis hoveredAxis = Axis.None;
    private static Axis draggedAxis = Axis.None;
    private static bool isDragging = false;

    private static float _gridSize;
    private static float _rotationIncrement;
    private static float _scaleIncrement;

    private TransformGizmosDragState _dragState;

    /// <summary>
    /// Initialize all gizmo components
    /// </summary>
    public void InitializeGizmos()
    {
        MoveGizmo.InitializeGizmo();
        ScaleGizmo.InitializeGizmo();
        RotateGizmo.InitializeGizmo();

        _gridSize = GridSize;
        _rotationIncrement = RotationIncrement;
        _scaleIncrement = ScaleIncrement;
    }

    /// <summary>
    /// Main update loop for gizmo rendering and interaction
    /// </summary>
    public void RenderGizmos()
    {
        if (targetTransform == null || currentMode == TransformMode.None)
        {
            SetActiveGizmo(TransformMode.None);
            return;
        }

        UpdateGizmoTransform();
        HandleInput();
        RenderSelectedGizmo();
    }

    /// <summary>
    /// Clean up gizmo objects
    /// </summary>
    public void ReleaseGizmos()
    {
        if (MoveGizmo != null) Destroy(MoveGizmo.gameObject);
        if (RotateGizmo != null) Destroy(RotateGizmo.gameObject);
        if (ScaleGizmo != null) Destroy(ScaleGizmo.gameObject);
    }

    private void RenderSelectedGizmo()
    {
        switch (currentMode)
        {
            case TransformMode.Move:
                if (MoveGizmo != null) MoveGizmo.RenderGizmo(hoveredAxis, draggedAxis);
                break;
            case TransformMode.Rotate:
                if (RotateGizmo != null) RotateGizmo.RenderGizmo(hoveredAxis, draggedAxis);
                break;
            case TransformMode.Scale:
                if (ScaleGizmo != null) ScaleGizmo.RenderGizmo(hoveredAxis, draggedAxis);
                break;
        }
    }

    private void UpdateGizmoTransform()
    {
        if (targetTransform == null) return;

        transform.position = targetTransform.position;
        transform.rotation = targetTransform.rotation;
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            CheckForAxisHover();
            if (hoveredAxis != Axis.None)
                StartDrag();
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            UpdateDrag();
        }
        else if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }
        else
        {
            CheckForAxisHover();
        }
    }

    private void CheckForAxisHover()
    {
        Ray ray = GlobalGizmoManager.Instance.GizmosCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Axis newHover = Axis.None;

        if (Physics.Raycast(ray, out hit, 1000f, GlobalGizmoManager.Instance.GizmosLayer))
        {
            newHover = GetAxisFromCollider(hit.collider);
        }

        if (newHover != hoveredAxis)
        {
            hoveredAxis = newHover;
            OnAxisHoverChanged?.Invoke(hoveredAxis);
        }
    }

    private Axis GetAxisFromCollider(Collider col)
    {
        Axis moveAxis = MoveGizmo.GetAxisFromCollider(col);
        if (moveAxis != Axis.None) return moveAxis;

        Axis rotateAxis = RotateGizmo.GetAxisFromCollider(col);
        if (rotateAxis != Axis.None) return rotateAxis;

        Axis scaleAxis = ScaleGizmo.GetAxisFromCollider(col);
        if (scaleAxis != Axis.None) return scaleAxis;

        return Axis.None;
    }

    #region Dragging Implementation

    private void StartDrag()
    {
        if (targetTransform == null) return;

        isDragging = true;
        draggedAxis = hoveredAxis;
        _dragState.dragStartMousePos = Input.mousePosition;

        _dragState.dragStartObjectLocalPos = targetTransform.localPosition;
        _dragState.dragStartObjectLocalRot = targetTransform.localRotation;
        _dragState.dragStartObjectLocalScale = targetTransform.localScale;

        SetupDragPlane();
        OnDragStarted?.Invoke();
    }

    private void UpdateDrag()
    {
        if (targetTransform == null) return;

        Ray ray = GlobalGizmoManager.Instance.GizmosCamera.ScreenPointToRay(Input.mousePosition);

        bool hitPlane = _dragState.dragPlane.Raycast(ray, out float enter);

        if (!hitPlane)
        {
            return;
        }

        Vector3 hitPoint = ray.GetPoint(enter);

        switch (currentMode)
        {
            case TransformMode.Move:
                UpdateMove(hitPoint);
                break;
            case TransformMode.Rotate:
                UpdateRotate(hitPoint);
                break;
            case TransformMode.Scale:
                UpdateScale(hitPoint);
                break;
        }

        OnTransformChanged?.Invoke(targetTransform);
    }

    private void EndDrag()
    {
        isDragging = false;
        draggedAxis = Axis.None;
        OnDragEnded?.Invoke();
    }

    private void SetupDragPlane()
    {
        Vector3 planeNormal = Vector3.up;

        if (currentMode == TransformMode.Move)
        {
            switch (draggedAxis)
            {
                case Axis.X:
                    planeNormal = Vector3.Cross(transform.right, Vector3.Cross(GlobalGizmoManager.Instance.GizmosCamera.transform.forward, transform.right)).normalized;
                    break;
                case Axis.Y:
                    planeNormal = Vector3.Cross(transform.up, Vector3.Cross(GlobalGizmoManager.Instance.GizmosCamera.transform.forward, transform.up)).normalized;
                    break;
                case Axis.Z:
                    planeNormal = Vector3.Cross(transform.forward, Vector3.Cross(GlobalGizmoManager.Instance.GizmosCamera.transform.forward, transform.forward)).normalized;
                    break;
                case Axis.XY:
                    planeNormal = transform.forward;
                    break;
                case Axis.XZ:
                    planeNormal = transform.up;
                    break;
                case Axis.YZ:
                    planeNormal = transform.right;
                    break;
                case Axis.Center:
                    planeNormal = GlobalGizmoManager.Instance.GizmosCamera.transform.forward;
                    break;
            }

            _dragState.dragPlane = new Plane(planeNormal, transform.position);

            Ray r = GlobalGizmoManager.Instance.GizmosCamera.ScreenPointToRay(_dragState.dragStartMousePos);
            if (_dragState.dragPlane.Raycast(r, out float enter))
                _dragState.dragStartPlaneHit = r.GetPoint(enter);
            else
                _dragState.dragStartPlaneHit = transform.position;
        }
        else if (currentMode == TransformMode.Rotate)
        {
            Vector3 axis = Vector3.zero;
            switch (draggedAxis)
            {
                case Axis.X: axis = transform.right; break;
                case Axis.Y: axis = transform.up; break;
                case Axis.Z: axis = transform.forward; break;
            }

            // Use the plane perpendicular to the rotation axis
            _dragState.dragPlane = new Plane(axis, transform.position);
            _dragState.rotationAxis = axis;

            Ray r = GlobalGizmoManager.Instance.GizmosCamera.ScreenPointToRay(_dragState.dragStartMousePos);
            if (_dragState.dragPlane.Raycast(r, out float enter))
            {
                _dragState.dragStartPlaneHit = r.GetPoint(enter);
            }
            else
            {
                _dragState.dragStartPlaneHit = transform.position;
            }
        }
        else if (currentMode == TransformMode.Scale)
        {
            planeNormal = GlobalGizmoManager.Instance.GizmosCamera.transform.forward;
            _dragState.dragPlane = new Plane(planeNormal, transform.position);

            Ray r = GlobalGizmoManager.Instance.GizmosCamera.ScreenPointToRay(_dragState.dragStartMousePos);
            if (_dragState.dragPlane.Raycast(r, out float enter))
                _dragState.dragStartPlaneHit = r.GetPoint(enter);
            else
                _dragState.dragStartPlaneHit = transform.position;
        }
    }

    private void UpdateMove(Vector3 hitPoint)
    {
        Vector3 delta = hitPoint - transform.position;
        Vector3 newLocalPosition = _dragState.dragStartObjectLocalPos;

        Vector3 worldMovement = Vector3.zero;

        switch (draggedAxis)
        {
            case Axis.X:
                worldMovement = transform.right * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.right);
                break;
            case Axis.Y:
                worldMovement = transform.up * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.up);
                break;
            case Axis.Z:
                worldMovement = transform.forward * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.forward);
                break;
            case Axis.XY:
                worldMovement =
                    transform.right * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.right) +
                    transform.up * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.up);
                break;
            case Axis.XZ:
                worldMovement =
                    transform.right * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.right) +
                    transform.forward * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.forward);
                break;
            case Axis.YZ:
                worldMovement =
                    transform.up * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.up) +
                    transform.forward * Vector3.Dot(hitPoint - _dragState.dragStartPlaneHit, transform.forward);
                break;
            case Axis.Center:
                worldMovement = hitPoint - _dragState.dragStartPlaneHit;
                break;
        }

        if (targetTransform.parent != null)
        {
            Vector3 localMovement = targetTransform.parent.InverseTransformVector(worldMovement);
            newLocalPosition = _dragState.dragStartObjectLocalPos + localMovement;
        }
        else
        {
            newLocalPosition = _dragState.dragStartObjectLocalPos + worldMovement;
        }

        if (_gridSize > 0)
        {
            newLocalPosition = SnapToGrid(newLocalPosition);
        }

        targetTransform.localPosition = newLocalPosition;
    }

    private void UpdateRotate(Vector3 hitPoint)
    {
        Vector3 axis = _dragState.rotationAxis;

        // Get vectors from center to drag start and current positions
        Vector3 startDir = (_dragState.dragStartPlaneHit - transform.position).normalized;
        Vector3 currentDir = (hitPoint - transform.position).normalized;

        // Calculate signed angle between the two directions around the rotation axis
        float angle = Vector3.SignedAngle(startDir, currentDir, axis);

        // Apply snapping if enabled
        if (_rotationIncrement > 0)
        {
            angle = Mathf.Round(angle / _rotationIncrement) * _rotationIncrement;
        }

        // Create rotation quaternion
        Quaternion rotationDelta = Quaternion.AngleAxis(angle, axis);

        // Apply rotation
        if (targetTransform.parent != null)
        {
            Quaternion worldRotation = targetTransform.parent.rotation * _dragState.dragStartObjectLocalRot;
            worldRotation = rotationDelta * worldRotation;
            targetTransform.localRotation = Quaternion.Inverse(targetTransform.parent.rotation) * worldRotation;
        }
        else
        {
            targetTransform.localRotation = rotationDelta * _dragState.dragStartObjectLocalRot;
        }
    }

    private void UpdateScale(Vector3 hitPoint)
    {
        Vector3 startDir = _dragState.dragStartPlaneHit - transform.position;
        Vector3 currentDir = hitPoint - transform.position;

        Vector3 newLocalScale = _dragState.dragStartObjectLocalScale;

        if (draggedAxis == Axis.Center)
        {
            float scaleFactor = currentDir.magnitude / Mathf.Max(startDir.magnitude, 0.0001f);
            newLocalScale = _dragState.dragStartObjectLocalScale * scaleFactor;
        }
        else
        {
            Vector3 axis = Vector3.zero;
            switch (draggedAxis)
            {
                case Axis.X: axis = transform.right; break;
                case Axis.Y: axis = transform.up; break;
                case Axis.Z: axis = transform.forward; break;
            }

            float startProj = Vector3.Dot(startDir, axis);
            float currentProj = Vector3.Dot(currentDir, axis);
            float scaleFactorAxis = currentProj / Mathf.Max(startProj, 0.0001f);

            if (draggedAxis == Axis.X) newLocalScale.x *= scaleFactorAxis;
            else if (draggedAxis == Axis.Y) newLocalScale.y *= scaleFactorAxis;
            else if (draggedAxis == Axis.Z) newLocalScale.z *= scaleFactorAxis;
        }

        if (_scaleIncrement > 0)
        {
            newLocalScale = SnapScaleToIncrement(newLocalScale);
        }

        targetTransform.localScale = newLocalScale;
    }

    #endregion

    #region Snapping Methods

    private Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / _gridSize) * _gridSize,
            Mathf.Round(position.y / _gridSize) * _gridSize,
            Mathf.Round(position.z / _gridSize) * _gridSize
        );
    }

    private Vector3 SnapScaleToIncrement(Vector3 scale)
    {
        return new Vector3(
            Mathf.Round(scale.x / _scaleIncrement) * _scaleIncrement,
            Mathf.Round(scale.y / _scaleIncrement) * _scaleIncrement,
            Mathf.Round(scale.z / _scaleIncrement) * _scaleIncrement
        );
    }

    #endregion

    #region Gizmo Activation API

    private void SetActiveGizmo(TransformMode mode)
    {
        MoveGizmo.gameObject.SetActive(mode == TransformMode.Move);
        RotateGizmo.gameObject.SetActive(mode == TransformMode.Rotate);
        ScaleGizmo.gameObject.SetActive(mode == TransformMode.Scale);
    }

    public static void SetTarget(Transform target)
    {
        targetTransform = target;
    }

    public static void SetMode(TransformMode mode)
    {
        currentMode = mode;
        GlobalGizmoManager.Instance.TransformGizmoManager.SetActiveGizmo(mode);
        hoveredAxis = Axis.None;
        draggedAxis = Axis.None;
        isDragging = false;
    }

    public static void SetGridSize(float size)
    {
        _gridSize = Mathf.Max(0, size);
    }

    public static void SetRotationIncrement(float increment)
    {
        _rotationIncrement = Mathf.Max(0, increment);
    }

    public static void SetScaleIncrement(float increment)
    {
        _scaleIncrement = Mathf.Max(0, increment);
    }

    public static float GetGridSize() => _gridSize;
    public static float GetRotationIncrement() => _rotationIncrement;
    public static float GetScaleIncrement() => _scaleIncrement;

    public static bool IsHoveringOverTransformGizmo()
    {
        if (currentMode == TransformMode.None || targetTransform == null)
            return false;

        if (isDragging)
            return true;

        return hoveredAxis != Axis.None;
    }

    public static Axis GetHoveredAxis()
    {
        return hoveredAxis;
    }

    public static TransformMode GetCurrentGizmoMode()
    {
        return currentMode;
    }

    public static bool IsDraggingTransformGizmo()
    {
        return isDragging;
    }

    #endregion
}

public enum TransformMode
{
    None,
    Move,
    Rotate,
    Scale
}

public enum Axis
{
    None,
    X,
    Y,
    Z,
    XY,
    XZ,
    YZ,
    Center
}

public struct TransformGizmosDragState
{
    public Vector2 dragStartMousePos;
    public Vector3 dragStartObjectLocalPos;
    public Quaternion dragStartObjectLocalRot;
    public Vector3 dragStartObjectLocalScale;
    public Plane dragPlane;
    public Vector3 dragStartPlaneHit;
    public Vector3 rotationAxis; // Store the rotation axis for rotation mode
}