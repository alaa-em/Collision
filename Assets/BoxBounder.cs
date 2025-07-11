using UnityEngine;

/// <summary>
/// Computes axis-aligned bounding box (AABB) for an object each frame, both in local space (mesh bounds) and world space (renderer bounds).
/// Used for broad-phase collision detection of soft/rigid bodies.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(Renderer))]
public class BoxBounder : MonoBehaviour
{
    private MeshFilter _mf;
    private Renderer _rend;

    [Header("Local-space AABB (mesh.bounds)")]
    public Vector3 min;  // Local-space min corner of AABB
    public Vector3 max;  // Local-space max corner of AABB

    [Header("World-space AABB (renderer.bounds)")]
    public Vector3 worldMin; // World-space min corner of AABB
    public Vector3 worldMax; // World-space max corner of AABB

    void Awake()
    {
        // Cache components
        _mf = GetComponent<MeshFilter>();
        _rend = GetComponent<Renderer>();
    }

    void Update()
    {
        UpdateBounds();
    }

    /// <summary>
    /// Updates the bounding box values based on the current mesh and transform.
    /// </summary>
    private void UpdateBounds()
    {
        if (_mf == null || _mf.sharedMesh == null || _rend == null) return;
        // Local space bounds from the mesh
        Bounds localBounds = _mf.sharedMesh.bounds;
        min = localBounds.min;
        max = localBounds.max;
        // World space bounds from the renderer (accounts for object transform)
        Bounds worldBounds = _rend.bounds;
        worldMin = worldBounds.min;
        worldMax = worldBounds.max;
    }

    void OnDrawGizmos()
    {
        if (_mf == null) _mf = GetComponent<MeshFilter>();
        if (_rend == null) _rend = GetComponent<Renderer>();
        if (_mf == null || _mf.sharedMesh == null || _rend == null) return;

        // Draw wireframe box for local-space bounds (yellow)
        Gizmos.color = Color.yellow;
        DrawBox(_mf.sharedMesh.bounds, transform.localToWorldMatrix);
        // Draw wireframe box for world-space bounds (yellow, using identity since world coords)
        Gizmos.color = Color.yellow;
        DrawBox(_rend.bounds, Matrix4x4.identity);
    }

    // Draws an axis-aligned wireframe box given Bounds b and a transform matrix
    private void DrawBox(Bounds b, Matrix4x4 transformMatrix)
    {
        Vector3 c = b.center;
        Vector3 e = b.extents;
        // 8 corners of the bounding box
        Vector3[] corners =
        {
            new Vector3(+e.x, +e.y, +e.z),
            new Vector3(-e.x, +e.y, +e.z),
            new Vector3(-e.x, -e.y, +e.z),
            new Vector3(+e.x, -e.y, +e.z),
            new Vector3(+e.x, +e.y, -e.z),
            new Vector3(-e.x, +e.y, -e.z),
            new Vector3(-e.x, -e.y, -e.z),
            new Vector3(+e.x, -e.y, -e.z)
        };
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = transformMatrix.MultiplyPoint3x4(c + corners[i]);
        }
        // 12 edges of the box (pairs of corner indices)
        int[,] edges =
        {
            {0,1},{1,2},{2,3},{3,0},
            {4,5},{5,6},{6,7},{7,4},
            {0,4},{1,5},{2,6},{3,7}
        };
        // Draw each edge line
        for (int i = 0; i < edges.GetLength(0); i++)
        {
            Gizmos.DrawLine(corners[edges[i, 0]], corners[edges[i, 1]]);
        }
    }
}
