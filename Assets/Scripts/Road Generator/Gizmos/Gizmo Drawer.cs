using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Handles efficient rendering of gizmo visuals using GPU instancing
/// Provides static methods for drawing various shapes and lines
/// </summary>
public class GizmoDrawer : MonoBehaviour
{
    /// <summary>
    /// Initialize the gizmo rendering system
    /// </summary>
    public void InitializeGizmos()
    {
        // Cache meshes
        meshCache = new Dictionary<VisualType, Mesh>();
        foreach (VisualType type in System.Enum.GetValues(typeof(VisualType)))
        {
            GameObject temp = GameObject.CreatePrimitive(GetPrimitiveType(type));
            meshCache[type] = temp.GetComponent<MeshFilter>().sharedMesh;

            // Remove collider
            Collider col = temp.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Destroy(temp);
        }

        // Initialize batching structures
        transformBatches = new Dictionary<VisualType, List<Matrix4x4>>();
        colorBatches = new Dictionary<VisualType, List<Vector4>>();
        onTopTransformBatches = new Dictionary<VisualType, List<Matrix4x4>>();
        onTopColorBatches = new Dictionary<VisualType, List<Vector4>>();

        foreach (VisualType type in System.Enum.GetValues(typeof(VisualType)))
        {
            transformBatches[type] = new List<Matrix4x4>(MAX_INSTANCES_PER_BATCH);
            colorBatches[type] = new List<Vector4>(MAX_INSTANCES_PER_BATCH);
            onTopTransformBatches[type] = new List<Matrix4x4>(MAX_INSTANCES_PER_BATCH);
            onTopColorBatches[type] = new List<Vector4>(MAX_INSTANCES_PER_BATCH);
        }

        propertyBlock = new MaterialPropertyBlock();

        // Ensure materials support instancing
        Settings.VisualMaterial.enableInstancing = true;
        Settings.OnTopMaterial.enableInstancing = true;
    }

    /// <summary>
    /// Render all batched gizmo visuals
    /// </summary>
    public void RenderGizmos()
    {
        RenderBatches(transformBatches, colorBatches, Settings.VisualMaterial);
        RenderBatches(onTopTransformBatches, onTopColorBatches, Settings.OnTopMaterial);
    }

    #region Public Drawing API

    public static void DrawCube(Vector3 position, Color color, bool onTop = false) =>
        AddVisualToBatch(VisualType.Cube, position, Quaternion.identity, Vector3.one, color, onTop);

    public static void DrawCube(Vector3 position, Quaternion rotation, Vector3 scale, Color color, bool onTop = false) =>
        AddVisualToBatch(VisualType.Cube, position, rotation, scale, color, onTop);

    public static void DrawSphere(Vector3 position, Color color, bool onTop = false) =>
        AddVisualToBatch(VisualType.Sphere, position, Quaternion.identity, Vector3.one, color, onTop);

    public static void DrawSphere(Vector3 position, float radius, Color color, bool onTop = false) =>
        AddVisualToBatch(VisualType.Sphere, position, Quaternion.identity, Vector3.one * radius * 2f, color, onTop);

    public static void DrawCylinder(Vector3 position, Quaternion rotation, Vector3 scale, Color color, bool onTop = false) =>
        AddVisualToBatch(VisualType.Cylinder, position, rotation, scale, color, onTop);

    public static void DrawCapsule(Vector3 position, Quaternion rotation, Vector3 scale, Color color, bool onTop = false) =>
        AddVisualToBatch(VisualType.Capsule, position, rotation, scale, color, onTop);

    public static void DrawPlane(Vector3 position, Quaternion rotation, Vector3 scale, Color color, bool onTop = false) =>
        AddVisualToBatch(VisualType.Plane, position, rotation, scale, color, onTop);

    public static void DrawQuad(Vector3 position, Quaternion rotation, Vector3 scale, Color color, bool onTop = false) =>
        AddVisualToBatch(VisualType.Quad, position, rotation, scale, color, onTop);

    public static void DrawLine(Vector3 start, Vector3 end, Color color, float thickness = 0.05f, bool onTop = false)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance < 0.0001f) return;

        Quaternion rotation = Quaternion.LookRotation(direction);
        Vector3 scale = new Vector3(thickness, thickness, distance);
        Vector3 center = (start + end) * 0.5f;

        AddVisualToBatch(VisualType.Cube, center, rotation, scale, color, onTop);
    }

    public static void DrawPoint(Vector3 position, Color color, float size = 0.1f, bool onTop = false)
    {
        AddVisualToBatch(VisualType.Sphere, position, Quaternion.identity, Vector3.one * size, color, onTop);
    }

    public static void DrawArrow(Vector3 start, Vector3 end, Color color, float thickness = 0.05f, bool onTop = false)
    {
        // Draw line
        DrawLine(start, end, color, thickness, onTop);

        // Draw arrowhead
        Vector3 direction = (end - start).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction);
        Vector3 headScale = new Vector3(thickness * 3f, thickness * 3f, thickness * 5f);

        AddVisualToBatch(VisualType.Cylinder, end - direction * thickness * 2.5f, rotation, headScale, color, onTop);
    }

    public static void DrawBox(Bounds bounds, Color color, bool onTop = false)
    {
        AddVisualToBatch(VisualType.Cube, bounds.center, Quaternion.identity, bounds.size, color, onTop);
    }

    public static void DrawCircle(Vector3 center, Vector3 normal, float radius, float thickness, Color color, int segments = 32, bool onTop = false)
    {
        // Use a reference up vector, fallback if normal is parallel
        Vector3 referenceUp = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;

        Vector3 right = Vector3.Cross(normal, referenceUp).normalized;
        Vector3 up = Vector3.Cross(right, normal).normalized;

        Vector3 prevPoint = center + right * radius;
        float angleStep = 360f / segments;

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 nextPoint = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            DrawLine(prevPoint, nextPoint, color, thickness, onTop);
            prevPoint = nextPoint;
        }
    }
    #endregion

    #region Internal Implementation

    private const int MAX_INSTANCES_PER_BATCH = 1023; // Unity limit for DrawMeshInstanced

    [SerializeField]
    private VisualObjectSettings Settings;

    private static void AddVisualToBatch(VisualType type, Vector3 pos, Quaternion rot, Vector3 scale, Color color, bool onTop = false)
    {
        // Add to appropriate batch based on onTop flag
        if (onTop)
        {
            onTopTransformBatches[type].Add(Matrix4x4.TRS(pos, rot, scale));
            onTopColorBatches[type].Add(color);
        }
        else
        {
            transformBatches[type].Add(Matrix4x4.TRS(pos, rot, scale));
            colorBatches[type].Add(color);
        }
    }

    private PrimitiveType GetPrimitiveType(VisualType type)
    {
        switch (type)
        {
            case VisualType.Cube: return PrimitiveType.Cube;
            case VisualType.Sphere: return PrimitiveType.Sphere;
            case VisualType.Cylinder: return PrimitiveType.Cylinder;
            case VisualType.Capsule: return PrimitiveType.Capsule;
            case VisualType.Plane: return PrimitiveType.Plane;
            case VisualType.Quad: return PrimitiveType.Quad;
            default: return PrimitiveType.Cube;
        }
    }

    private void RenderBatches(Dictionary<VisualType, List<Matrix4x4>> transforms,
                       Dictionary<VisualType, List<Vector4>> colors,
                       Material material)
    {
        // Render all batches using GPU instancing
        foreach (VisualType type in System.Enum.GetValues(typeof(VisualType)))
        {
            int count = transforms[type].Count;
            if (count == 0) continue;

            List<Matrix4x4> transformList = transforms[type];
            List<Vector4> colorList = colors[type];

            // Process in batches of MAX_INSTANCES_PER_BATCH
            int batchCount = (count + MAX_INSTANCES_PER_BATCH - 1) / MAX_INSTANCES_PER_BATCH;

            for (int batch = 0; batch < batchCount; batch++)
            {
                int startIdx = batch * MAX_INSTANCES_PER_BATCH;
                int instanceCount = Mathf.Min(MAX_INSTANCES_PER_BATCH, count - startIdx);

                // Copy to arrays (required by DrawMeshInstanced)
                transformList.CopyTo(startIdx, matrixArray, 0, instanceCount);
                colorList.CopyTo(startIdx, colorArray, 0, instanceCount);

                // Set colors in property block
                propertyBlock.SetVectorArray("_Color", colorArray);

                // Draw instanced
                Graphics.DrawMeshInstanced(
                    meshCache[type],
                    0,
                    material,
                    matrixArray,
                    instanceCount,
                    propertyBlock
                );
            }

            // Clear for next frame
            transformList.Clear();
            colorList.Clear();
        }
    }

    #endregion

    // Static batching system
    private static Dictionary<VisualType, Mesh> meshCache;
    private static Dictionary<VisualType, List<Matrix4x4>> transformBatches;
    private static Dictionary<VisualType, List<Vector4>> colorBatches;
    private static Dictionary<VisualType, List<Matrix4x4>> onTopTransformBatches;
    private static Dictionary<VisualType, List<Vector4>> onTopColorBatches;

    private MaterialPropertyBlock propertyBlock;

    private static Matrix4x4[] matrixArray = new Matrix4x4[MAX_INSTANCES_PER_BATCH];
    private static Vector4[] colorArray = new Vector4[MAX_INSTANCES_PER_BATCH];
}

/// <summary>
/// Available visual types for gizmo rendering
/// </summary>
public enum VisualType
{
    Cube,
    Sphere,
    Cylinder,
    Capsule,
    Plane,
    Quad
}

[Serializable]
public struct VisualObjectSettings
{
    public Material VisualMaterial;
    public Material OnTopMaterial;
}