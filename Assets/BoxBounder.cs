using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(Renderer))]
public class BoxBounder : MonoBehaviour
{
    private MeshFilter _mf;
    private Renderer _rend;

    [Header("Local‐space AABB (mesh.bounds)")]
    public Vector3 min;
    public Vector3 max;

    [Header("World‐space AABB (renderer.bounds)")]
    public Vector3 worldMin;
    public Vector3 worldMax;


    void Update()
    {
        UpdateBounds();
    }



    void UpdateBounds()
    {
        if (_mf == null || _mf.sharedMesh == null || _rend == null)
            return;

        Bounds lb = _mf.sharedMesh.bounds;
        min = lb.min;
        max = lb.max;

        Bounds wb = _rend.bounds;
        worldMin = wb.min;
        worldMax = wb.max;
    }

    
    void OnDrawGizmos()
    {
        if (_mf == null) _mf = GetComponent<MeshFilter>();
        if (_rend == null) _rend = GetComponent<Renderer>();
        if (_mf == null || _mf.sharedMesh == null || _rend == null) return;

        Gizmos.color = Color.yellow;
        DrawBox(_mf.sharedMesh.bounds, transform.localToWorldMatrix);

        Gizmos.color = Color.yellow;
        DrawBox(_rend.bounds, Matrix4x4.identity);
    }

    void DrawBox(Bounds b, Matrix4x4 xform)
    {
        Vector3 c = b.center;
        Vector3 e = b.extents;

        Vector3[] corners = {
            new Vector3(+e.x,+e.y,+e.z), new Vector3(-e.x,+e.y,+e.z),
            new Vector3(-e.x,-e.y,+e.z), new Vector3(+e.x,-e.y,+e.z),
            new Vector3(+e.x,+e.y,-e.z), new Vector3(-e.x,+e.y,-e.z),
            new Vector3(-e.x,-e.y,-e.z), new Vector3(+e.x,-e.y,-e.z),
        };

        for (int i = 0; i < corners.Length; i++)
            corners[i] = xform.MultiplyPoint3x4(c + corners[i]);

        int[,] edges = {
            {0,1},{1,2},{2,3},{3,0},
            {4,5},{5,6},{6,7},{7,4},
            {0,4},{1,5},{2,6},{3,7}
        };

        for (int i = 0; i < edges.GetLength(0); i++)
            Gizmos.DrawLine(corners[edges[i, 0]], corners[edges[i, 1]]);
    }
}
