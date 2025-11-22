using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class RoadGenerator : MonoBehaviour
{
    [SerializeField] private RoadGeneratorSettings Settings;

    public RoadGeneratorSettings GetRoadGeneratorSettings() => Settings;

    private void Update()
    {
        if (Settings.DisplayGizmos) DrawRoadPoints();
    }


    public void GenerateRoad(List<List<RoadPoint>> roads)
    {
        SetRoads(roads);
        GenerateRoadsPoints();

        if (Settings.GenerateRoadMeshes)
        {
            if (_roads.Count <= 0) return;
            RoadData data = _roads[0];

            List<Vector3> snapPoints = _allLeftPoints.Concat(_allRightPoints).ToList();

            List<MeshQuad> meshQuads = QuadMeshBuilder.GenerateQuads(data, data.IsClosed);
            List<MeshQuad> subdividedQuads = QuadMeshBuilder.AdaptiveSubdivideQuads(meshQuads, snapPoints, Settings.PointSpacing + 0.5f);
            Mesh mesh = QuadMeshBuilder.BuildExtrudedMeshFromQuads(subdividedQuads, Settings.RoadThickness, true);
            _debugMesh = mesh;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
    private void SetRoads(List<List<RoadPoint>> roads)
    {
        foreach (var spline in Settings.SplineContainer.Splines.ToArray())
            Settings.SplineContainer.RemoveSpline(spline);

        foreach (var segment in roads)
        {
            if (segment.Count < 2) continue;

            Spline spline = new Spline();

            foreach (var roadPoint in segment)
            {
                BezierKnot knot = new BezierKnot
                {
                    Position = new float3(roadPoint.GetPosition())
                };
                spline.Add(knot, TangentMode.AutoSmooth);
            }

            bool isClosed = segment[0].PreviousPoint == segment[segment.Count - 1] &&
                            segment[segment.Count - 1].NextPoint == segment[0];
            spline.Closed = isClosed;
            Settings.SplineContainer.AddSpline(spline);
        }
    }

    private void SampleSpline(Spline spline, out List<Vector3> left, out List<Vector3> right, out List<Vector3> center, out List<bool> isKnotPoint)
    {
        left = new();
        right = new();
        center = new();
        isKnotPoint = new();

        if (spline == null) return;

        bool closed = spline.Closed;
        float totalLength = spline.GetLength();
        float halfWidth = Settings.RoadWidth * 0.5f;
        float targetSpacing = Mathf.Max(Settings.PointSpacing, 0.05f);

        List<Vector3> knotPositions = new();
        for (int i = 0; i < spline.Count; i++)
        {
            float t = (float)i / (spline.Count - 1);
            if (closed && i == spline.Count - 1) t = 0f;

            spline.Evaluate(t, out float3 pos, out _, out _);
            knotPositions.Add((Vector3)pos);
        }

        spline.Evaluate(0f, out float3 p0, out float3 tan0, out _);
        Vector3 lastPos = p0;
        Vector3 lastTangent = ((Vector3)tan0).normalized;
        Vector3 lastRight = Vector3.Cross(Vector3.up, lastTangent).normalized;
        Vector3 lastUp = Vector3.Cross(lastTangent, lastRight).normalized;

        if (lastRight.sqrMagnitude < 0.01f)
        {
            lastRight = Vector3.Cross(Vector3.forward, lastTangent).normalized;
            lastUp = Vector3.Cross(lastTangent, lastRight).normalized;
        }

        // Store the original point positions (without thickness)
        Vector3 originalPos = lastPos;
        Vector3 centerPoint = new Vector3(originalPos.x, originalPos.y + Settings.RoadThickness, originalPos.z);
        center.Add(centerPoint);
        left.Add(new Vector3(centerPoint.x - lastRight.x * halfWidth, centerPoint.y, centerPoint.z - lastRight.z * halfWidth));
        right.Add(new Vector3(centerPoint.x + lastRight.x * halfWidth, centerPoint.y, centerPoint.z + lastRight.z * halfWidth));

        bool isFirstKnot = knotPositions.Any(k => Vector3.Distance(lastPos, k) < 0.01f);
        isKnotPoint.Add(isFirstKnot);

        float splineDist = 0f;

        while (splineDist < totalLength)
        {
            splineDist += targetSpacing;
            if (splineDist >= totalLength) { if (closed) break; splineDist = totalLength; }

            float t = SplineUtility.GetNormalizedInterpolation(spline, splineDist, PathIndexUnit.Distance);
            spline.Evaluate(t, out float3 posF3, out float3 tanF3, out _);

            Vector3 pos = posF3;
            Vector3 tangent = ((Vector3)tanF3).normalized;
            Vector3 rightVec = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 upVec = Vector3.Cross(tangent, rightVec).normalized;

            if (rightVec.sqrMagnitude < 0.01f)
            {
                rightVec = lastRight;
                upVec = lastUp;
            }

            if (Vector3.Dot(rightVec, lastRight) < 0f)
            {
                rightVec = -rightVec;
                upVec = -upVec;
            }

            rightVec = Vector3.Cross(upVec, tangent).normalized;
            upVec = Vector3.Cross(tangent, rightVec).normalized;

            // Store the original point position (without thickness)
            originalPos = pos;
            centerPoint = new Vector3(originalPos.x, originalPos.y + Settings.RoadThickness, originalPos.z);
            center.Add(centerPoint);
            left.Add(new Vector3(centerPoint.x - rightVec.x * halfWidth, centerPoint.y, centerPoint.z - rightVec.z * halfWidth));
            right.Add(new Vector3(centerPoint.x + rightVec.x * halfWidth, centerPoint.y, centerPoint.z + rightVec.z * halfWidth));

            bool isKnot = knotPositions.Any(k => Vector3.Distance(pos, k) < 0.01f);
            isKnotPoint.Add(isKnot);

            lastTangent = tangent;
            lastRight = rightVec;
            lastUp = upVec;
            lastPos = pos;

            if (left.Count > 50000) break;
        }

        if (closed && isKnotPoint.Count > 0) isKnotPoint[^1] = true;
    }
    private List<Vector3> SampleCenter(Spline spline)
    {
        List<Vector3> center = new();

        if (spline == null) return null;

        bool closed = spline.Closed;
        float totalLength = spline.GetLength();
        float targetSpacing = Mathf.Max(Settings.PointSpacing, 0.05f);

        // Pre-calculate knot positions
        List<Vector3> knotPositions = new();
        for (int i = 0; i < spline.Count; i++)
        {
            float t = (float)i / (spline.Count - 1);
            if (closed && i == spline.Count - 1) t = 0f;

            spline.Evaluate(t, out float3 pos, out _, out _);
            knotPositions.Add((Vector3)pos);
        }

        // Add the first point
        spline.Evaluate(0f, out float3 firstPos, out _, out _);
        Vector3 lastPos = firstPos;
        center.Add(lastPos);

        float cumulativeDist = 0f;
        float splineDist = 0f;

        // Sample points along the spline
        while (splineDist < totalLength)
        {
            splineDist += targetSpacing;
            if (splineDist >= totalLength) { if (closed) break; splineDist = totalLength; }

            float t = SplineUtility.GetNormalizedInterpolation(spline, splineDist, PathIndexUnit.Distance);
            spline.Evaluate(t, out float3 pos, out _, out _);

            Vector3 currentPos = pos;
            cumulativeDist += Vector3.Distance(lastPos, currentPos);

            center.Add(currentPos);

            lastPos = currentPos;
        }
        return center;
    }

    private void GenerateRoadsPoints()
    {
        _roads.Clear();
        _allLeftPoints.Clear();
        _allRightPoints.Clear();

        foreach (var spline in Settings.SplineContainer.Splines)
        {
            RoadData roadData = new RoadData { IsClosed = spline.Closed };
            SampleSpline(spline, out roadData.LeftPoints, out roadData.RightPoints,
                         out roadData.CenterPoints, out roadData.IsKnotPoint);
            roadData.RoadSpline = spline;
            if (Settings.EnableSmoothing)
            {
                ApplySmoothing(roadData, Settings.SmoothingStrength, Settings.SmoothingIterations);
            }

            if (Settings.EnableSelfIntersectionsDetection)
            {
                roadData.LeftPoints = RemoveSelfIntersections(roadData.LeftPoints, roadData.IsClosed);
                roadData.RightPoints = RemoveSelfIntersections(roadData.RightPoints, roadData.IsClosed);
            }

            if (Settings.EnableAdaptiveSpacing)
            {
                roadData.LeftPoints = RoadGenerationUtils.ResampleByDistance(roadData.LeftPoints, Settings.PointSpacing);
                roadData.RightPoints = RoadGenerationUtils.ResampleByDistance(roadData.RightPoints, Settings.PointSpacing);
            }

            RoadGenerationUtils.RebuildCenterFromSides(roadData, SampleCenter(roadData.RoadSpline), Settings.PointSpacing, 5, roadData.IsClosed);

            _roads.Add(roadData);
            _allLeftPoints.AddRange(roadData.LeftPoints);
            _allRightPoints.AddRange(roadData.RightPoints);
        }
    }

    private void DrawRoadPoints()
    {
        foreach (var road in _roads)
        {
            foreach (var pt in road.CenterPoints)
            {
                GizmoDrawer.DrawSphere(pt, 0.1f, Color.red, true);
            }
        }
        for (int i = 0; i < _allLeftPoints.Count; i++)
        {
            Vector3 pt = _allLeftPoints[i];
            if (i < _allLeftPoints.Count - 1) GizmoDrawer.DrawLine(pt, _allLeftPoints[i + 1], Color.white, 0.02f, true);
            GizmoDrawer.DrawSphere(pt, 0.1f, Color.blue, true);
        }
        for (int i = 0; i < _allRightPoints.Count; i++)
        {
            Vector3 pt = _allRightPoints[i];
            if (i < _allRightPoints.Count - 1) GizmoDrawer.DrawLine(pt, _allRightPoints[i + 1], Color.white, 0.02f, true);
            GizmoDrawer.DrawSphere(pt, 0.1f, Color.blue, true);
        }

        // Draw normals if enabled
        if (Settings.DisplayNormals && _debugMesh != null)
        {
            DrawNormals();
        }
    }

    private void DrawNormals()
    {
        if (_debugMesh == null) return;

        Vector3[] vertices = _debugMesh.vertices;
        Vector3[] normals = _debugMesh.normals;
        Transform transform = GetComponent<Transform>();

        // Draw a subset of normals to avoid clutter
        int step = Mathf.Max(1, vertices.Length / 100); // Draw at most 100 normals

        for (int i = 0; i < vertices.Length; i += step)
        {
            Vector3 worldPos = transform.TransformPoint(vertices[i]);
            Vector3 worldNormal = transform.TransformDirection(normals[i]);

            // Draw arrow representing the normal
            GizmoDrawer.DrawArrow(
                worldPos,
                worldPos + worldNormal * Settings.NormalArrowLength,
                Settings.NormalArrowColor,
                Settings.NormalArrowWidth,
                true
            );
        }
    }

    private List<Vector3> RemoveSelfIntersections(List<Vector3> points, bool isClosed)
    {
        if (points == null || points.Count < 4) return points;
        return RoadGenerationUtils.ClipSelfIntersections3D(points, isClosed, 0.1f, 1000);
    }

    private void ApplySmoothing(RoadData data, float strength, int iterations)
    {
        if (data == null || data.CenterPoints == null || data.CenterPoints.Count < 3) return;

        strength = Mathf.Clamp01(strength);
        iterations = Mathf.Max(0, iterations);

        var centers = new List<Vector3>(data.CenterPoints);
        var originalCenters = new List<Vector3>(centers);

        for (int it = 0; it < iterations; it++)
        {
            var next = new List<Vector3>(centers);

            for (int i = 0; i < centers.Count; i++)
            {
                bool isKnot = (i < data.IsKnotPoint.Count) && data.IsKnotPoint[i];
                if (isKnot) continue;

                // Get the previous, current, and next points
                Vector3 prev = centers[(i - 1 + centers.Count) % centers.Count];
                Vector3 curr = centers[i];
                Vector3 nextP = centers[(i + 1) % centers.Count];

                // Calculate the direction vectors
                Vector3 dir1 = (curr - prev).normalized;
                Vector3 dir2 = (nextP - curr).normalized;

                // Calculate the angle between the direction vectors
                float angle = Vector3.Angle(dir1, dir2);

                // Adjust the strength based on the angle
                // Higher angle means less smoothing to preserve the shape
                float adjustedStrength = strength * (1f - angle / 180f);

                // Calculate the target position using a weighted average
                Vector3 target = (prev + 2f * curr + nextP) / 4f;

                // Move the current point towards the target
                next[i] = Vector3.Lerp(curr, target, adjustedStrength);
            }

            centers = next;
        }

        // Blend the smoothed points with the original points to maintain the overall shape
        for (int i = 0; i < centers.Count; i++)
        {
            bool isKnot = (i < data.IsKnotPoint.Count) && data.IsKnotPoint[i];
            if (isKnot) continue;

            centers[i] = Vector3.Lerp(originalCenters[i], centers[i], strength);
        }

        data.CenterPoints = centers;

        // Rebuild sides with consistent winding
        float halfWidth = Settings.RoadWidth * 0.5f;
        RoadGenerationUtils.RebuildSidesFromCenter(data, halfWidth, Settings.RoadThickness);

        // ENSURE left/right is consistent: cross product should point roughly up
        for (int i = 0; i < data.CenterPoints.Count; i++)
        {
            Vector3 forward = (i < data.CenterPoints.Count - 1)
                ? (data.CenterPoints[i + 1] - data.CenterPoints[i]).normalized
                : (data.CenterPoints[i] - data.CenterPoints[i - 1]).normalized;

            Vector3 cross = Vector3.Cross(forward, data.RightPoints[i] - data.LeftPoints[i]);
            if (cross.y < 0f)
            {
                // Swap left/right
                Vector3 tmp = data.LeftPoints[i];
                data.LeftPoints[i] = data.RightPoints[i];
                data.RightPoints[i] = tmp;
            }
        }
    }
    
    private List<RoadData> _roads = new();
    private List<Vector3> _allLeftPoints = new();
    private List<Vector3> _allRightPoints = new();
    private Mesh _debugMesh;
}

[Serializable]
public struct RoadGeneratorSettings
{
    public float RoadWidth;
    public float RoadThickness;
    public float PointSpacing;
    public bool EnableAdaptiveSpacing;
    public bool EnableSelfIntersectionsDetection;
    public bool GenerateRoadMeshes;
    public bool DisplayGizmos;

    // Normal visualization settings
    public bool DisplayNormals;
    public float NormalArrowLength;
    public float NormalArrowWidth;
    public Color NormalArrowColor;

    // smoothing options
    public bool EnableSmoothing;
    public float SmoothingStrength;
    public int SmoothingIterations;

    public SplineContainer SplineContainer;
}

public class RoadData
{
    public List<Vector3> LeftPoints = new();
    public List<Vector3> RightPoints = new();
    public List<bool> IsKnotPoint = new();
    public bool IsClosed = false;

    public List<Vector3> CenterPoints = new();
    public Dictionary<int, List<Vector3>> AllowedLeftPointsFromCenter = new();
    public Dictionary<int, List<Vector3>> AllowedRightPointsFromCenter = new();

    public Spline RoadSpline = null;
}

public static class RoadGenerationUtils
{
    public static List<Vector3> ClipSelfIntersections3D(List<Vector3> poly, bool isClosed, float minDistance = 0.05f, int maxIters = 200)
    {
        if (poly == null) throw new ArgumentNullException(nameof(poly));
        if (poly.Count < 2) return new List<Vector3>(poly);

        var pts = new List<Vector3>(poly);
        NormalizeClosure(pts, isClosed);

        int iter = 0;
        const float endpointFrac = 1e-3f;

        while (iter < maxIters)
        {
            bool didRemove = false;
            int segCount = pts.Count - 1;
            if (segCount < 2) break;

            var segLengths = new float[segCount];
            float totalLength = 0f;
            for (int i = 0; i < segCount; i++)
            {
                float l = Vector3.Distance(pts[i], pts[i + 1]);
                segLengths[i] = l;
                totalLength += l;
            }

            for (int i = 0; i < segCount && !didRemove; i++)
            {
                Vector3 a0 = pts[i];
                Vector3 a1 = pts[i + 1];

                for (int j = i + 1; j < segCount && !didRemove; j++)
                {
                    if (AreSegmentsAdjacent(i, j, segCount, isClosed)) continue;

                    Vector3 b0 = pts[j];
                    Vector3 b1 = pts[j + 1];

                    if (SegmentsIntersect3D(a0, a1, b0, b1, minDistance, endpointFrac, out Vector3 ip, out float s, out float t))
                    {
                        float forward = ArcDistance(i, s, j, t, segLengths, totalLength);
                        float backward = totalLength - forward;
                        bool removeForward = forward <= backward;

                        pts = RemoveArc(pts, i, s, j, t, ip, removeForward, isClosed);
                        CleanDegenerates(pts, minDistance * 0.5f, isClosed);
                        NormalizeClosure(pts, isClosed);

                        didRemove = true;
                    }
                }
            }

            if (!didRemove) break;
            iter++;
        }

        NormalizeClosure(pts, isClosed);
        return pts;
    }

    static void NormalizeClosure(List<Vector3> pts, bool isClosed)
    {
        if (pts.Count < 2) return;

        if (isClosed)
        {
            if (pts[0] != pts[pts.Count - 1])
                pts.Add(pts[0]);
        }
        else
        {
            if (pts[0] == pts[pts.Count - 1])
                pts.RemoveAt(pts.Count - 1);
        }
    }

    static bool AreSegmentsAdjacent(int i, int j, int segCount, bool isClosed)
    {
        if (Mathf.Abs(i - j) <= 1) return true;
        if (!isClosed) return false;
        return (i == 0 && j == segCount - 1) || (j == 0 && i == segCount - 1);
    }

    static bool SegmentsIntersect3D(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, float minDist, float endpointFrac, out Vector3 ip, out float sOut, out float tOut)
    {
        ip = Vector3.zero;
        sOut = tOut = 0f;

        float dist = DistanceSegmentSegment(a0, a1, b0, b1, out float s, out float t, out Vector3 c1, out Vector3 c2);

        bool sInterior = s > endpointFrac && s < 1f - endpointFrac;
        bool tInterior = t > endpointFrac && t < 1f - endpointFrac;
        if (!(sInterior || tInterior)) return false;

        if (dist <= Mathf.Max(minDist, 1e-6f))
        {
            ip = (c1 + c2) * 0.5f;
            sOut = s;
            tOut = t;
            return true;
        }

        return false;
    }

    static float DistanceSegmentSegment(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t, out Vector3 c1, out Vector3 c2)
    {
        Vector3 d1 = q1 - p1;
        Vector3 d2 = q2 - p2;
        Vector3 r = p1 - p2;

        float a = Vector3.Dot(d1, d1);
        float e = Vector3.Dot(d2, d2);
        float f = Vector3.Dot(d2, r);
        const float EPS = 1e-9f;

        if (a <= EPS && e <= EPS)
        {
            s = t = 0f;
            c1 = p1;
            c2 = p2;
            return Vector3.Distance(c1, c2);
        }

        if (a <= EPS)
        {
            s = 0f;
            t = Mathf.Clamp01(f / e);
        }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= EPS)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float denom = a * e - b * b;

                if (Mathf.Abs(denom) > 1e-12f)
                    s = Mathf.Clamp01((b * f - c * e) / denom);
                else
                    s = Mathf.Clamp01(-c / a);

                t = (b * s + f) / e;

                if (t < 0f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else if (t > 1f)
                {
                    t = 1f;
                    s = Mathf.Clamp01((b - c) / a);
                }
            }
        }

        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
        return Vector3.Distance(c1, c2);
    }

    static float ArcDistance(int aSeg, float aFrac, int bSeg, float bFrac, float[] segLengths, float totalLength)
    {
        int segCount = segLengths.Length;
        float d = segLengths[aSeg] * (1f - aFrac);

        int idx = (aSeg + 1) % segCount;
        while (idx != bSeg)
        {
            d += segLengths[idx];
            idx = (idx + 1) % segCount;
        }

        d += segLengths[bSeg] * bFrac;
        return Mathf.Clamp(d, 0f, totalLength);
    }

    static List<Vector3> RemoveArc(List<Vector3> pts, int segI, float s, int segJ, float t, Vector3 ip, bool forward, bool isClosed)
    {
        var list = new List<Vector3>(pts);
        bool hadClosure = isClosed && list[0] == list[list.Count - 1];
        if (hadClosure) list.RemoveAt(list.Count - 1);

        int n = list.Count;
        List<Vector3> result;

        if (!isClosed)
        {
            int start = forward ? segI : segJ;
            int end = forward ? segJ : segI;

            result = new List<Vector3>();
            for (int i = 0; i <= start; i++) result.Add(list[i]);
            result.Add(ip);
            for (int i = end + 1; i < n; i++) result.Add(list[i]);
        }
        else
        {
            result = new List<Vector3>();

            if (forward)
            {
                int cur = (segJ + 1) % n;
                while (true)
                {
                    result.Add(list[cur]);
                    if (cur == segI) break;
                    cur = (cur + 1) % n;
                }
                result.Add(ip);
            }
            else
            {
                int cur = (segI + 1) % n;
                while (true)
                {
                    result.Add(list[cur]);
                    if (cur == segJ) break;
                    cur = (cur + 1) % n;
                }
                result.Add(ip);
            }
        }

        if (result.Count < 2)
            return new List<Vector3>(list);

        if (isClosed)
        {
            if (result[0] != result[result.Count - 1])
                result.Add(result[0]);
        }

        return result;
    }

    static void CleanDegenerates(List<Vector3> pts, float minLen, bool isClosed)
    {
        for (int i = pts.Count - 1; i > 0; i--)
        {
            if (Vector3.Distance(pts[i], pts[i - 1]) <= minLen)
                pts.RemoveAt(i);
        }

        if (isClosed)
        {
            if (pts.Count > 1 && pts[0] != pts[pts.Count - 1])
                pts.Add(pts[0]);

            if (pts.Count > 2 && Vector3.Distance(pts[pts.Count - 1], pts[pts.Count - 2]) <= minLen)
                pts.RemoveAt(pts.Count - 1);
        }
        else
        {
            if (pts.Count > 1 && pts[0] == pts[pts.Count - 1])
                pts.RemoveAt(pts.Count - 1);
        }
    }

    // -- Point Resampling -- \\
    public static List<Vector3> ResampleByDistance(List<Vector3> points, float targetDistance, bool isClosed = false)
    {
        if (points == null || points.Count < 2 || targetDistance <= 0f)
            return points;

        float totalLength = 0f;
        for (int i = 1; i < points.Count; i++)
            totalLength += Vector3.Distance(points[i - 1], points[i]);

        float closingSegmentLength = 0f;
        if (isClosed)
        {
            closingSegmentLength = Vector3.Distance(points[^1], points[0]);
            totalLength += closingSegmentLength;
        }

        int newCount = Mathf.Max(2, Mathf.RoundToInt(totalLength / targetDistance) + 1);
        var result = new List<Vector3>(newCount) { points[0] };

        // For an open path, the distance is between N-1 segments. For a closed path, it's N segments.
        float distanceBetween = totalLength / (isClosed ? newCount : newCount - 1);
        float currentTarget = distanceBetween;
        float accumulated = 0f;
        int currentIndex = 1;

        // Pre-calculate the bounding box for clamping on open paths.
        // This is the box defined by the original start and end points.
        Vector3 minBounds, maxBounds;
        if (!isClosed)
        {
            Vector3 startPoint = points[0];
            Vector3 endPoint = points[^1];
            minBounds = new Vector3(Mathf.Min(startPoint.x, endPoint.x), Mathf.Min(startPoint.y, endPoint.y), Mathf.Min(startPoint.z, endPoint.z));
            maxBounds = new Vector3(Mathf.Max(startPoint.x, endPoint.x), Mathf.Max(startPoint.y, endPoint.y), Mathf.Max(startPoint.z, endPoint.z));
        }
        else
        {
            // Not used for closed paths, but initialized to avoid compiler errors.
            minBounds = maxBounds = Vector3.zero;
        }

        while (result.Count < newCount)
        {
            Vector3 prev, next;
            float segmentLength;

            if (currentIndex < points.Count)
            {
                prev = points[currentIndex - 1];
                next = points[currentIndex];
                segmentLength = Vector3.Distance(prev, next);
            }
            else if (isClosed && currentIndex == points.Count)
            {
                prev = points[^1];
                next = points[0];
                segmentLength = closingSegmentLength;
            }
            else
            {
                // Should not be reached for open paths as the last point is handled after the loop.
                break;
            }

            if (accumulated + segmentLength >= currentTarget)
            {
                float t = (currentTarget - accumulated) / segmentLength;
                Vector3 newPoint = Vector3.Lerp(prev, next, t);

                // --- CLAMPING LOGIC ---
                // This is where we prevent the end points from going past the start points.
                // We only do this for an OPEN path.
                if (!isClosed)
                {
                    // Check if this is the last point we will interpolate before the final endpoint is forced.
                    // The loop condition is `result.Count < newCount`. When count is `newCount - 1`,
                    // we are generating the last interpolated point.
                    if (result.Count == newCount - 1)
                    {
                        // Clamp this penultimate point to the bounds of the original start/end points.
                        newPoint.x = Mathf.Clamp(newPoint.x, minBounds.x, maxBounds.x);
                        newPoint.y = Mathf.Clamp(newPoint.y, minBounds.y, maxBounds.y);
                        newPoint.z = Mathf.Clamp(newPoint.z, minBounds.z, maxBounds.z);
                    }
                }

                result.Add(newPoint);
                currentTarget += distanceBetween;
            }
            else
            {
                accumulated += segmentLength;
                currentIndex++;
            }
        }

        // Ensure the last point of an open path exactly matches the original last point.
        if (!isClosed && result[result.Count - 1] != points[^1])
            result.Add(points[^1]);

        if (isClosed && result.Count > 1)
        {
            // For closed paths, the first and last points might be very close.
            // Average them and remove the duplicate to create a clean loop.
            float endDistance = Vector3.Distance(result[0], result[^1]);
            if (endDistance < 0.65f * targetDistance)
            {
                Vector3 average = (result[0] + result[^1]) * 0.5f;
                result[0] = average;
                result.RemoveAt(result.Count - 1);
            }
        }

        return result;
    }
    public static void RebuildSidesFromCenter(RoadData data, float halfWidth, float roadThickness = 0f)
    {
        var centers = data.CenterPoints;
        if (centers == null || centers.Count < 2) return;

        var left = new List<Vector3>(centers.Count);
        var right = new List<Vector3>(centers.Count);
        var distances = new List<float>(centers.Count);

        Vector3 lastRight = Vector3.right;
        Vector3 lastUp = Vector3.up;

        float cumulative = 0f;
        distances.Add(0f);

        for (int i = 0; i < centers.Count; i++)
        {
            Vector3 p = centers[i];

            Vector3 tangent;
            if (i == 0)
                tangent = (centers.Count > 1) ? (centers[1] - centers[0]).normalized : Vector3.forward;
            else if (i == centers.Count - 1)
                tangent = (centers.Count > 1) ? (centers[i] - centers[i - 1]).normalized : Vector3.forward;
            else
                tangent = (centers[i + 1] - centers[i - 1]).normalized;

            if (tangent.sqrMagnitude < 1e-6f) tangent = lastRight;

            Vector3 rightVec = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 upVec = Vector3.Cross(tangent, rightVec).normalized;

            if (rightVec.sqrMagnitude < 0.01f)
            {
                rightVec = lastRight;
                upVec = lastUp;
            }

            if (Vector3.Dot(rightVec, lastRight) < 0f)
            {
                rightVec = -rightVec;
                upVec = -upVec;
            }

            // Store the original point position (without thickness)
            Vector3 originalPos = new Vector3(p.x, p.y - roadThickness, p.z);
            Vector3 topPos = new Vector3(originalPos.x, originalPos.y + roadThickness, originalPos.z);

            left.Add(topPos - rightVec * halfWidth);
            right.Add(topPos + rightVec * halfWidth);

            if (i > 0)
                cumulative += Vector3.Distance(centers[i - 1], centers[i]);

            distances.Add(cumulative);

            lastRight = rightVec;
            lastUp = upVec;
        }

        if (distances.Count > centers.Count)
            distances.RemoveAt(0);

        data.LeftPoints = left;
        data.RightPoints = right;
    }
    public static void RebuildCenterFromSides(RoadData road, List<Vector3> center, float targetDistance, int maxIndexDistance, bool isClosed)
    {
        List<Vector3> newCenter = new List<Vector3>();
        int previousRightIndex = 0;
        int previousLeftIndex = 0;

        List<Vector3> left = new List<Vector3>(road.LeftPoints);
        List<Vector3> right = new List<Vector3>(road.RightPoints);
        List<Vector3> centerPoints = ResampleByDistance(new List<Vector3>(road.CenterPoints), targetDistance, false);

        foreach (var centerPoint in centerPoints)
        {
            int centerPTIndex = centerPoints.IndexOf(centerPoint);

            Vector3 closestRP = Vector3.zero;
            float closestRPist = float.MaxValue;
            Vector3 closestLP = Vector3.zero;
            float closestLPist = float.MaxValue;

            foreach (var rightPoint in right)
            {
                int currentRightIndex = right.IndexOf(rightPoint);

                if (currentRightIndex != right.Count - 1 && isClosed)
                {
                    previousRightIndex = 0;
                    currentRightIndex = 0;
                }
                if (Mathf.Abs(currentRightIndex - previousRightIndex) > maxIndexDistance) continue;

                if (road.AllowedRightPointsFromCenter.ContainsKey(centerPTIndex))
                    road.AllowedRightPointsFromCenter[centerPTIndex].Add(rightPoint);
                else road.AllowedRightPointsFromCenter.Add(centerPTIndex, new List<Vector3>() { rightPoint });

                float dist = Vector3.Distance(centerPoint, rightPoint);
                if (dist < closestRPist)
                {
                    closestRPist = dist;
                    closestRP = rightPoint;
                }
            }
            previousRightIndex = right.IndexOf(closestRP);

            foreach (var leftPoint in left)
            {
                int currentLeftIndex = left.IndexOf(leftPoint);

                if (currentLeftIndex != left.Count - 1 && isClosed)
                {
                    previousLeftIndex = 0;
                    currentLeftIndex = 0;
                }
                if (Mathf.Abs(currentLeftIndex - previousLeftIndex) > maxIndexDistance) continue;

                if (road.AllowedLeftPointsFromCenter.ContainsKey(centerPTIndex))
                    road.AllowedLeftPointsFromCenter[centerPTIndex].Add(leftPoint);
                else road.AllowedLeftPointsFromCenter.Add(centerPTIndex, new List<Vector3>() { leftPoint });
                float dist = Vector3.Distance(centerPoint, leftPoint);
                if (dist < closestLPist)
                {
                    closestLPist = dist;
                    closestLP = leftPoint;
                }
            }
            previousLeftIndex = left.IndexOf(closestLP);

            newCenter.Add((closestRP + closestLP) / 2);
        }
        road.CenterPoints = newCenter;
    }
}

public class QuadMeshBuilder
{
    public static List<MeshQuad> GenerateQuads(
        RoadData road,
        bool connectEndToStart = false)
    {
        if (road.CenterPoints?.Count < 2) return new List<MeshQuad>();

        var quads = new List<MeshQuad>(road.CenterPoints.Count - 1 + (connectEndToStart ? 1 : 0));

        Vector3 prevC = road.CenterPoints[0];
        Vector3 prevL = GetClosestPoint(road.AllowedLeftPointsFromCenter[0], prevC);
        Vector3 prevR = GetClosestPoint(road.AllowedRightPointsFromCenter[0], prevC);

        for (int i = 1; i < road.CenterPoints.Count; i++)
        {
            Vector3 currC = road.CenterPoints[i];
            Vector3 currL = GetClosestPoint(road.AllowedLeftPointsFromCenter[i], currC);
            Vector3 currR = GetClosestPoint(road.AllowedRightPointsFromCenter[i], currC);

            quads.Add(new MeshQuad(prevL, prevR, currR, currL, prevC, currC));
            (prevL, prevR, prevC) = (currL, currR, currC);
        }

        if (connectEndToStart)
        {
            Vector3 firstC = road.CenterPoints[0];
            Vector3 firstL = GetClosestPoint(road.AllowedLeftPointsFromCenter[0], firstC);
            Vector3 firstR = GetClosestPoint(road.AllowedRightPointsFromCenter[0], firstC);

            if (prevC != firstC || prevL != firstL || prevR != firstR)
            {
                quads.Add(new MeshQuad(prevL, prevR, firstR, firstL, prevC, firstC));
            }
        }

        return quads;
    }


    public static List<MeshQuad> AdaptiveSubdivideQuads(List<MeshQuad> quads, List<Vector3> snapPoints, float maxEdgeLength)
    {
        if (quads == null || quads.Count == 0 || maxEdgeLength <= 0f) return quads;

        var refinedQuads = new List<MeshQuad>();
        foreach (var quad in quads)
        {
            float adLength = Vector3.Distance(quad.A, quad.D);
            float bcLength = Vector3.Distance(quad.B, quad.C);
            if (Mathf.Max(adLength, bcLength) > maxEdgeLength)
            {
                Vector3 midD = GetClosestPoint(snapPoints, (quad.A + quad.D) * 0.5f);
                Vector3 midC = GetClosestPoint(snapPoints, (quad.B + quad.C) * 0.5f);
                Vector3 midCenter = (quad.CenterStart + quad.CenterEnd) * 0.5f;

                refinedQuads.Add(new MeshQuad(quad.A, quad.B, midC, midD, quad.CenterStart, midCenter));
                refinedQuads.Add(new MeshQuad(midD, midC, quad.C, quad.D, midCenter, quad.CenterEnd));
            }
            else
            {
                refinedQuads.Add(quad);
            }
        }
        return refinedQuads;
    }
    public static Mesh BuildExtrudedMeshFromQuads(List<MeshQuad> quads, float extrusionHeight = 0f, bool isClosedLoop = false, float uvScale = 1f)
    {
        if (quads == null || quads.Count == 0) return CreateEmptyMesh();

        // --- Pre-calculate cumulative distances for consistent UVs ---
        var cumulativeDistances = new Dictionary<Vector3, float>();
        cumulativeDistances[quads[0].CenterStart] = 0f;
        for (int i = 0; i < quads.Count; i++)
        {
            var quad = quads[i];
            if (!cumulativeDistances.ContainsKey(quad.CenterEnd))
            {
                float segmentLength = Vector3.Distance(quad.CenterStart, quad.CenterEnd);
                cumulativeDistances[quad.CenterEnd] = cumulativeDistances[quad.CenterStart] + segmentLength;
            }
        }

        // --- Track boundary edges for side faces ---
        var edgeUsage = new Dictionary<(Vector3, Vector3), int>();
        foreach (var quad in quads)
        {
            AddEdge(edgeUsage, quad.A, quad.D);
            AddEdge(edgeUsage, quad.B, quad.C);
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        for (int i = 0; i < quads.Count; i++)
        {
            var quad = quads[i];
            int vCount = vertices.Count;

            // Top vertices
            vertices.Add(quad.A);
            vertices.Add(quad.B);
            vertices.Add(quad.C);
            vertices.Add(quad.D);

            // Top triangles
            triangles.Add(vCount + 0); triangles.Add(vCount + 2); triangles.Add(vCount + 1);
            triangles.Add(vCount + 0); triangles.Add(vCount + 3); triangles.Add(vCount + 2);

            // Bottom face
            if (extrusionHeight > 0f)
            {
                vCount = vertices.Count;
                Vector3 offset = Vector3.down * extrusionHeight;
                vertices.Add(quad.A + offset);
                vertices.Add(quad.B + offset);
                vertices.Add(quad.C + offset);
                vertices.Add(quad.D + offset);

                // Bottom triangles (reversed winding)
                triangles.Add(vCount + 1); triangles.Add(vCount + 2); triangles.Add(vCount + 0);
                triangles.Add(vCount + 2); triangles.Add(vCount + 3); triangles.Add(vCount + 0);
            }

            // Side faces
            if (extrusionHeight > 0f)
            {
                // Left side (A-D)
                if (IsEdgeBoundary(edgeUsage, quad.A, quad.D))
                {
                    AddSide(vertices, triangles, quad.A, quad.D, quad.CenterStart, quad.CenterEnd, extrusionHeight, cumulativeDistances);
                }
                // Right side (B-C)
                if (IsEdgeBoundary(edgeUsage, quad.B, quad.C))
                {
                    AddSide(vertices, triangles, quad.C, quad.B, quad.CenterEnd, quad.CenterStart, extrusionHeight, cumulativeDistances);
                }
            }
        }

        var mesh = new Mesh { indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        return mesh;
    }

    #region UV and Mesh Helpers

    private static void AddSide(List<Vector3> vertices, List<int> triangles, Vector3 topStart, Vector3 topEnd, Vector3 centerStart, Vector3 centerEnd, float extrusionHeight, Dictionary<Vector3, float> cumulativeDistances)
    {
        int vCount = vertices.Count;
        Vector3 botStart = topStart - Vector3.up * extrusionHeight;
        Vector3 botEnd = topEnd - Vector3.up * extrusionHeight;

        vertices.Add(botStart); vertices.Add(botEnd); vertices.Add(topEnd); vertices.Add(topStart);

        triangles.Add(vCount + 0); triangles.Add(vCount + 1); triangles.Add(vCount + 2);
        triangles.Add(vCount + 0); triangles.Add(vCount + 2); triangles.Add(vCount + 3);
    }

    private static Vector3 GetClosestPoint(List<Vector3> points, Vector3 target)
    {
        if (points == null || points.Count == 0) return target;
        Vector3 best = points[0];
        float bestSqrDist = (target - best).sqrMagnitude;
        for (int i = 1; i < points.Count; i++)
        {
            float sqrDist = (target - points[i]).sqrMagnitude;
            if (sqrDist < bestSqrDist) { bestSqrDist = sqrDist; best = points[i]; }
        }
        return best;
    }

    private static void AddEdge(Dictionary<(Vector3, Vector3), int> edgeUsage, Vector3 a, Vector3 b)
    {
        var edge = (a, b);
        var reverseEdge = (b, a);
        if (edgeUsage.ContainsKey(edge)) edgeUsage[edge]++;
        else if (edgeUsage.ContainsKey(reverseEdge)) edgeUsage[reverseEdge]++;
        else edgeUsage[edge] = 1;
    }

    private static bool IsEdgeBoundary(Dictionary<(Vector3, Vector3), int> edgeUsage, Vector3 a, Vector3 b)
    {
        var edge = (a, b);
        var reverseEdge = (b, a);
        if (edgeUsage.ContainsKey(edge)) return edgeUsage[edge] == 1;
        if (edgeUsage.ContainsKey(reverseEdge)) return edgeUsage[reverseEdge] == 1;
        return false;
    }

    private static Mesh CreateEmptyMesh() => new Mesh { indexFormat = IndexFormat.UInt32 };

    #endregion
}

public struct MeshQuad
{
    public Vector3 A, B, C, D;
    public Vector3 CenterStart, CenterEnd;

    public MeshQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 centerStart, Vector3 centerEnd)
    {
        A = a; B = b; C = c; D = d;
        CenterStart = centerStart; CenterEnd = centerEnd;
    }
}
public struct MeshTriangle
{
    public Vector3 A, B, C;
    public int ConnectionA, ConnectionB, ConnectionC;

    public override bool Equals(object obj)
    {
        if (obj is MeshTriangle t)
        {
            return A.Equals(t.A) &&
                   B.Equals(t.B) &&
                   C.Equals(t.C);
        }
        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int h = A.GetHashCode();
            h = (h * 397) ^ B.GetHashCode();
            h = (h * 397) ^ C.GetHashCode();
            return h;
        }
    }

    public static bool operator ==(MeshTriangle a, MeshTriangle b) => a.Equals(b);
    public static bool operator !=(MeshTriangle a, MeshTriangle b) => !a.Equals(b);
}