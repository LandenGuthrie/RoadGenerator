using UnityEngine;

/// <summary>
/// Represents a single point in a road path with connections to adjacent points.
/// This component tracks the previous and next points in the road chain.
/// </summary>
public class RoadPoint : MonoBehaviour
{
    [SerializeField] private RoadPoint _previousPoint;
    [SerializeField] private RoadPoint _nextPoint;

    /// <summary>
    /// Gets the previous point in the road chain.
    /// </summary>
    public RoadPoint PreviousPoint => _previousPoint;

    /// <summary>
    /// Gets the next point in the road chain.
    /// </summary>
    public RoadPoint NextPoint => _nextPoint;

    /// <summary>
    /// Sets both previous and next connections.
    /// </summary>
    /// <param name="previous">The previous point in the chain</param>
    /// <param name="next">The next point in the chain</param>
    public void SetConnections(RoadPoint previous, RoadPoint next)
    {
        _previousPoint = previous;
        _nextPoint = next;
    }

    /// <summary>
    /// Sets the previous point connection.
    /// </summary>
    /// <param name="previous">The previous point in the chain</param>
    public void SetPreviousPoint(RoadPoint previous)
    {
        _previousPoint = previous;
    }

    /// <summary>
    /// Sets the next point connection.
    /// </summary>
    /// <param name="next">The next point in the chain</param>
    public void SetNextPoint(RoadPoint next)
    {
        _nextPoint = next;
    }

    /// <summary>
    /// Checks if this point is part of a closed loop.
    /// Traverses forward through the chain to see if we return to this point.
    /// </summary>
    /// <returns>True if this point is in a closed loop, false otherwise</returns>
    public bool IsInClosedLoop()
    {
        if (_previousPoint == null || _nextPoint == null)
            return false;

        // Traverse forward to see if we come back to this point
        RoadPoint current = _nextPoint;
        int safety = 0;

        while (current != null && current != this && safety < 10000)
        {
            current = current.NextPoint;
            safety++;
        }

        return current == this;
    }

    /// <summary>
    /// Gets the world position of this road point.
    /// </summary>
    /// <returns>The world space position of this point's transform</returns>
    public Vector3 GetPosition()
    {
        return transform.position;
    }
}