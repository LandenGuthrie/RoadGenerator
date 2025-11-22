using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoadEditor : MonoBehaviour
{
    [SerializeField] private RoadEditorSettings Settings;

    /// <summary>
    /// Gets the settings of the road editor.
    /// </summary>
    /// <returns></returns>
    public RoadEditorSettings GetRoadEditorSettings() =>
        Settings;

    private void Start()
    {
        // Generating road & barriers
        GenerateRoad();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            GenerateRoad();
        }
        if (Input.GetKeyDown(KeyCode.Delete) && _currentSelectedRoadPoint != null)
        {
            DeleteRoadPoint(_currentSelectedRoadPoint);
        }
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C))
        {
            ToggleRoadClosed();
        }

        UpdatePlacmentState();

        if (Input.GetMouseButtonDown(0) )
        {
            // Raycast to find a valid position in the scene
            if (TransformGizmoManager.IsHoveringOverTransformGizmo())
            {
                return;
            }

            if (HoveringOverRoadPoint())
            {
                SelectRoadPoint(GetRoadPointFromMouse().GetComponent<RoadPoint>());
                return;
            }

            if (_canPlacePoint)
                AddRoadPoint(_previewPosition);
        }
    }

    public void SelectRoadPoint(RoadPoint point)
    {
        if (point != null)
        {
            TransformGizmoManager.SetTarget(point.transform);
            TransformGizmoManager.SetMode(TransformMode.Move);
            _currentSelectedRoadPoint = point.gameObject;
        }
        else
        {
            TransformGizmoManager.SetTarget(null);
            TransformGizmoManager.SetMode(TransformMode.None);
        }
    }

    /// <summary>
    /// Clears the original road data.
    /// Generates the road points.
    /// Generates the road mesh.
    /// </summary>
    public void GenerateRoad()
    {
        // Get all road segments (separate paths)
        List<List<RoadPoint>> roadSegments = GetRoadSegments();

        Settings.RoadGenerator.GenerateRoad(roadSegments);
    }

    /// <summary>
    /// Gets all separate road segments by following connections.
    /// </summary>
    private List<List<RoadPoint>> GetRoadSegments()
    {
        List<List<RoadPoint>> segments = new List<List<RoadPoint>>();
        HashSet<RoadPoint> processed = new HashSet<RoadPoint>();

        List<GameObject> allPoints = GetRoadPointObjects();

        foreach (GameObject pointObj in allPoints)
        {
            RoadPoint startPoint = pointObj.GetComponent<RoadPoint>();
            if (startPoint == null || processed.Contains(startPoint))
                continue;

            List<RoadPoint> segment = new List<RoadPoint>();
            RoadPoint current = startPoint;

            // Follow the chain
            do
            {
                segment.Add(current);
                processed.Add(current);
                current = current.NextPoint;

                // Safety check for infinite loops
                if (segment.Count > 10000)
                {
                    Debug.LogError("Road segment too large - possible infinite loop");
                    break;
                }

            } while (current != null && current != startPoint && !processed.Contains(current));

            // Check if this segment is closed
            bool isClosed = (current == startPoint);

            // Store segment info
            if (segment.Count > 0)
            {
                segments.Add(segment);
            }
        }

        return segments;
    }

    /// <summary>
    /// Deletes a road point and updates connections.
    /// </summary>
    private void DeleteRoadPoint(GameObject pointObject)
    {
        if (pointObject == null)
            return;

        RoadPoint roadPoint = pointObject.GetComponent<RoadPoint>();
        if (roadPoint == null)
            return;

        // Update connections to bypass this point
        RoadPoint previous = roadPoint.PreviousPoint;
        RoadPoint next = roadPoint.NextPoint;

        if (previous != null)
        {
            previous.SetNextPoint(next);
        }

        if (next != null)
        {
            next.SetPreviousPoint(previous);
        }

        // Destroy the GameObject
        Destroy(pointObject);

        // Clear selection
        TransformGizmoManager.SetMode(TransformMode.None);
        TransformGizmoManager.SetTarget(null);
        _currentSelectedRoadPoint = null;

        // Regenerate the road
        GenerateRoad();
    }

    /// <summary>
    /// Gets the segment that contains the specified road point.
    /// </summary>
    private List<RoadPoint> GetSegmentContainingPoint(RoadPoint point)
    {
        List<RoadPoint> segment = new List<RoadPoint>();
        HashSet<RoadPoint> visited = new HashSet<RoadPoint>();

        // Find the start of the segment by going backwards
        RoadPoint start = point;
        while (start.PreviousPoint != null && start.PreviousPoint != point && !visited.Contains(start.PreviousPoint))
        {
            visited.Add(start);
            start = start.PreviousPoint;
        }

        // Now traverse forward from start
        visited.Clear();
        RoadPoint current = start;
        do
        {
            segment.Add(current);
            visited.Add(current);
            current = current.NextPoint;

            if (segment.Count > 10000) break;

        } while (current != null && current != start && !visited.Contains(current));

        return segment;
    }

    /// <summary>
    /// Toggles whether the road is closed or open.
    /// </summary>
    private void ToggleRoadClosed()
    {
        // Get first segment
        List<List<RoadPoint>> segments = GetRoadSegments();
        if (segments.Count == 0 || segments[0].Count < 2)
        {
            Debug.LogWarning("No valid road segment to toggle.");
            return;
        }

        List<RoadPoint> segment = segments[0];
        RoadPoint first = segment[0];
        RoadPoint last = segment[segment.Count - 1];

        // Check if currently closed
        bool isClosed = (first.PreviousPoint == last && last.NextPoint == first);

        if (isClosed)
        {
            // Open the loop
            first.SetPreviousPoint(null);
            last.SetNextPoint(null);
            Debug.Log("Road is now open");
        }
        else
        {
            // Close the loop
            first.SetPreviousPoint(last);
            last.SetNextPoint(first);
            Debug.Log("Road is now closed");
        }

        GenerateRoad();
    }

    private bool HoveringOverRoadPoint()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, Settings.RoadPointLayers))
        {
            return true;
        }

        return false;
    }
    private GameObject GetRoadPointFromMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, Settings.RoadPointLayers))
        {
            return hitInfo.transform.gameObject;
        }

        return null;
    }

    /// <summary>
    /// Updates the placement preview gizmo showing where a new road point would be placed.
    /// </summary>
    private void UpdatePlacmentState()
    {

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, Settings.DrivableLayers)) 
            return;

        if (!HoveringOverRoadPoint())
        {
            _canPlacePoint = true;
            float y = _currentSelectedRoadPoint != null ? _currentSelectedRoadPoint.transform.position.y : hit.point.y;
            _previewPosition = new Vector3(hit.point.x, y, hit.point.z);
        }
        else
        {
            _canPlacePoint = false;
            _previewPosition = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Adds a new road point at the specified position.
    /// </summary>
    private void AddRoadPoint(Vector3 position)
    {
        // Instantiate the road point prefab
        GameObject newPointObj = Instantiate(Settings.RoadPointPrefab, position, Quaternion.identity, Settings.RoadPointsContainer);
        newPointObj.SetActive(true);
        newPointObj.name = $"RoadPoint_{GetRoadPointObjects().Count}";
        newPointObj.layer = Settings.RoadPointLayerIndex;

        // Add RoadPoint component if not present
        RoadPoint newPoint = newPointObj.GetComponent<RoadPoint>();
        if (newPoint == null)
        {
            newPoint = newPointObj.AddComponent<RoadPoint>();
        }

        // Determine connection logic based on current selection
        List<GameObject> existingPoints = GetRoadPointObjects();
        if (existingPoints.Count > 1) // Don't count the newly added point
        {
            RoadPoint connectionPoint = null;

            // If we have a currently selected road point, connect to it
            if (_currentSelectedRoadPoint != null)
            {
                RoadPoint selectedPoint = _currentSelectedRoadPoint.GetComponent<RoadPoint>();
                if (selectedPoint != null)
                {
                    connectionPoint = selectedPoint;
                    Debug.Log($"Connecting new point to selected point: {_currentSelectedRoadPoint.name}");
                }
            }
            else
            {
                // No selection - find the closest existing point
                GameObject closestPointObj = null;
                float closestDistance = float.MaxValue;

                foreach (GameObject pointObj in existingPoints)
                {
                    if (pointObj == newPointObj) continue;

                    float distance = Vector3.Distance(position, pointObj.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestPointObj = pointObj;
                    }
                }

                if (closestPointObj != null)
                {
                    connectionPoint = closestPointObj.GetComponent<RoadPoint>();
                    Debug.Log($"Connecting new point to nearest point: {closestPointObj.name}");
                }
            }

            // Make the connection
            if (connectionPoint != null)
            {
                // Insert between connection point and its next point
                RoadPoint nextPoint = connectionPoint.NextPoint;

                connectionPoint.SetNextPoint(newPoint);
                newPoint.SetPreviousPoint(connectionPoint);
                newPoint.SetNextPoint(nextPoint);

                if (nextPoint != null)
                {
                    nextPoint.SetPreviousPoint(newPoint);
                }
            }
        }

        Debug.Log($"Added new road point at {position}");

        // Regenerate the road
        GenerateRoad();

        // Automatically select the newly placed point
        TransformGizmoManager.SetTarget(newPointObj.transform);
        TransformGizmoManager.SetMode(TransformMode.Move);
        _currentSelectedRoadPoint = newPointObj;
    }

    /// <summary>
    /// Gets all the road point positions from road segments.
    /// </summary>
    public List<Vector3> GetRoadPoints()
    {
        List<Vector3> result = new List<Vector3>();
        List<GameObject> roadPoints = GetRoadPointObjects();
        foreach (var pt in roadPoints)
        {
            result.Add(pt.transform.position);
        }
        return result;
    }

    /// <summary>
    /// Gets all the road points by their GameObjects.
    /// </summary>
    public List<GameObject> GetRoadPointObjects()
    {
        List<GameObject> result = new List<GameObject>();
        foreach (Transform pt in Settings.RoadPointsContainer)
        {
            if (pt.gameObject.activeSelf) 
                result.Add(pt.gameObject);
        }
        return result;
    }


    private GameObject _currentSelectedRoadPoint;

    // Placement preview state
    private bool _canPlacePoint = false;
    private Vector3 _previewPosition = Vector3.zero;
}

[Serializable]
public struct RoadEditorSettings
{
    [Header("References")]
    public RoadGenerator RoadGenerator;

    [Header("Prefabs")]
    public GameObject RoadPointPrefab;

    [Header("Objects")]
    public Transform RoadPointsContainer;
    public MeshCollider RoadCollider;

    [Header("Layers")]
    public LayerMask RoadPointLayers;
    public LayerMask DrivableLayers;
    public int RoadPointLayerIndex;

    [Header("Gizmos")]
    public Color LineColor;
    public float LineThickness;

    public Color PointColor;
    public float PointSize;

    public Color PlacementPreviewColor;
}