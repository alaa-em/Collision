using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;


public struct AABB
{
    public Vector3 Min, Max;
    public AABB(Vector3 min, Vector3 max) { Min = min; Max = max; }

    public bool IntersectRay(Vector3 origin, Vector3 dir, float maxDistance)
    {
        // Slab‐method ray/AABB
        float tMin = 0f, tMax = maxDistance;
        for (int i = 0; i < 3; i++)
        {
            float invD = 1f / dir[i];
            float t0 = (Min[i] - origin[i]) * invD;
            float t1 = (Max[i] - origin[i]) * invD;
            if (invD < 0f) { var tmp = t0; t0 = t1; t1 = tmp; }
            tMin = t0 > tMin ? t0 : tMin;
            tMax = t1 < tMax ? t1 : tMax;
            if (tMax <= tMin) return false;
        }
        return true;
    }

    public static AABB FromTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        Vector3 min = Vector3.Min(v0, Vector3.Min(v1, v2));
        Vector3 max = Vector3.Max(v0, Vector3.Max(v1, v2));
        return new AABB(min, max);
    }

    public static AABB Merge(AABB a, AABB b)
    {
        return new AABB(Vector3.Min(a.Min, b.Min), Vector3.Max(a.Max, b.Max));
    }
}




[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
public class Voxelization : MonoBehaviour
{
    public int resolution = 32;
    public float voxelSize = 0.1f;
    public bool showGizmos = true;

    private Mesh mesh;
    private Vector3[] vertices;
    private int[] triangles;
    public BVHNode bvhRoot;
    public List<Vector3> insideVoxels = new List<Vector3>();
    public class BVHNode
    {
        public AABB Bounds;
        public BVHNode Left, Right;
        public List<int> TriIndices; // indices into the triangle array (i.e. leaf)

        // Leaf ctor
        public BVHNode(List<int> tris, AABB bounds)
        {
            TriIndices = tris;
            Bounds = bounds;
        }

        // Internal ctor
        public BVHNode(BVHNode left, BVHNode right)
        {
            Left = left;
            Right = right;
            Bounds = AABB.Merge(left.Bounds, right.Bounds);
        }

        public bool IsLeaf => TriIndices != null;
    }
    public class Voxel
    {
        public bool IsInside;
        public Vector3 WorldPosition;
        public Vector3 LocalPosition;

        public Voxel(bool inside, Vector3 worldPos, Vector3 localPos)
        {
            IsInside = inside;
            WorldPosition = worldPos;
            LocalPosition = localPos;
        }
    }

    void Start() => GenerateVoxels();
    
    void OnValidate() => GenerateVoxels();


    public Voxel[,,] Voxels;


    public void GenerateVoxels()
    {
        insideVoxels.Clear();

        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        mesh = mf.sharedMesh;
        vertices = mesh.vertices;
        triangles = mesh.triangles;
        bvhRoot = BuildBVH();

        var bounds = GetComponent<Renderer>().bounds;
        Vector3 origin = bounds.min;
        Vector3 size = bounds.size;
        int voxX = Mathf.CeilToInt(size.x / voxelSize);
        int voxY = Mathf.CeilToInt(size.y / voxelSize);
        int voxZ = Mathf.CeilToInt(size.z / voxelSize);

        // allocate the Voxel grid
        Voxels = new Voxel[voxX, voxY, voxZ];

        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

        int slice = Mathf.Max(1, voxX / 4);
        var ranges = new (int x0, int x1)[]
        {
        (0,       slice),
        (slice,   slice*2),
        (slice*2, slice*3),
        (slice*3, voxX)
        };

        var tasks = new Task[ranges.Length];
        for (int i = 0; i < ranges.Length; i++)
        {
            int xStart = ranges[i].x0;
            int xEnd = ranges[i].x1;

            tasks[i] = Task.Run(() =>
            {
                for (int x = xStart; x < xEnd; x++)
                    for (int y = 0; y < voxY; y++)
                        for (int z = 0; z < voxZ; z++)
                        {
                            // compute voxel center positions
                            Vector3 worldCenter = origin + new Vector3(
                                (x + 0.5f) * voxelSize,
                                (y + 0.5f) * voxelSize,
                                (z + 0.5f) * voxelSize);
                            Vector3 localCenter = worldToLocal.MultiplyPoint3x4(worldCenter);

                            // count odd‐ray hits
                            Vector3[] dirs = {
                    Vector3.right,  Vector3.left,
                    Vector3.up,     Vector3.down,
                    Vector3.forward,Vector3.back
                };
                            int oddCount = 0;
                            foreach (var dir in dirs)
                            {
                                int hits = TraverseBVH(bvhRoot, localCenter, dir, float.MaxValue);
                                if ((hits & 1) == 1) oddCount++;
                                if (oddCount >= 3) break;
                            }

                            bool isInside = oddCount >= 3;

                            // store into your Voxel array
                            Voxels[x, y, z] =
                                new Voxel(isInside, worldCenter, localCenter);

                            // keep your existing list of inside‐centers
                            if (isInside)
                                insideVoxels.Add(localCenter);
                        }
            });
        }

        Task.WaitAll(tasks);
        Debug.Log($"Voxelization complete. Inside voxels: {insideVoxels.Count}");
    }



    BVHNode BuildBVH()
    {
        int triCount = triangles.Length / 3;
        var allIndices = new List<int>(triCount);
        for (int i = 0; i < triCount; i++)
            allIndices.Add(i);
        return BuildRecursive(allIndices);
    }

    BVHNode BuildRecursive(List<int> triIndices)
    {

        AABB nodeBB = new AABB(
            new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
            new Vector3(float.MinValue, float.MinValue, float.MinValue));
        foreach (int ti in triIndices)
        {
            Vector3 v0 = vertices[triangles[3 * ti + 0]];
            Vector3 v1 = vertices[triangles[3 * ti + 1]];
            Vector3 v2 = vertices[triangles[3 * ti + 2]];
            nodeBB = AABB.Merge(nodeBB, AABB.FromTriangle(v0, v1, v2));
        }

      
        if (triIndices.Count <= 8)
            return new BVHNode(triIndices, nodeBB);

        
        Vector3 extents = nodeBB.Max - nodeBB.Min;
        int axis = extents.x > extents.y
                ? (extents.x > extents.z ? 0 : 2)
                : (extents.y > extents.z ? 1 : 2);

      
        triIndices.Sort((a, b) =>
        {
            float ca = (
                vertices[triangles[3 * a + 0]][axis] +
                vertices[triangles[3 * a + 1]][axis] +
                vertices[triangles[3 * a + 2]][axis]) / 3f;
            float cb = (
                vertices[triangles[3 * b + 0]][axis] +
                vertices[triangles[3 * b + 1]][axis] +
                vertices[triangles[3 * b + 2]][axis]) / 3f;
            return ca.CompareTo(cb);
        });

    
        int mid = triIndices.Count / 2;
        var leftList = triIndices.GetRange(0, mid);
        var rightList = triIndices.GetRange(mid, triIndices.Count - mid);

        var leftNode = BuildRecursive(leftList);
        var rightNode = BuildRecursive(rightList);
        return new BVHNode(leftNode, rightNode);
    }

    
    int TraverseBVH(BVHNode node, Vector3 origin, Vector3 dir, float maxDist)
    {
        if (!node.Bounds.IntersectRay(origin, dir, maxDist))
            return 0;

        if (node.IsLeaf)
        {
            int count = 0;
            foreach (int ti in node.TriIndices)
            {
                Vector3 v0 = vertices[triangles[3 * ti + 0]];
                Vector3 v1 = vertices[triangles[3 * ti + 1]];
                Vector3 v2 = vertices[triangles[3 * ti + 2]];
                if (RayIntersectsTriangle(origin, dir, v0, v1, v2, out float dist)
                 && dist > 0 && dist < maxDist)
                {
                    count++;
                }
            }
            return count;
        }
        else
        {
           
            return TraverseBVH(node.Left, origin, dir, maxDist)
                 + TraverseBVH(node.Right, origin, dir, maxDist);
        }
    }

    
    bool RayIntersectsTriangle(Vector3 origin, Vector3 dir, Vector3 v0, Vector3 v1, Vector3 v2, out float distance)
    {
        const float EPSILON = 1e-6f;
        distance = 0f;
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 pvec = Vector3.Cross(dir, edge2);
        float det = Vector3.Dot(edge1, pvec);
        if (Mathf.Abs(det) < EPSILON) return false;
        float invDet = 1f / det;
        Vector3 tvec = origin - v0;
        float u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0f || u > 1f) return false;
        Vector3 qvec = Vector3.Cross(tvec, edge1);
        float v = Vector3.Dot(dir, qvec) * invDet;
        if (v < 0f || u + v > 1f) return false;
        distance = Vector3.Dot(edge2, qvec) * invDet;
        return distance > EPSILON;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || insideVoxels == null) return;
        Gizmos.color = Color.red;
        foreach (var pos in insideVoxels)

            Gizmos.DrawCube(transform.TransformPoint(pos), Vector3.one * voxelSize * 0.2f);


    }
    //[Header("Gizmo Colors")]

    //public Color insideColor = new Color(0f, 1f, 0f, 1f);  // opaque green
    //public Color outsideColor = new Color(1f, 0f, 0f, 1f);  // opaque red


    //void OnDrawGizmos()
    //{
    //    if (!showGizmos || Voxels == null)
    //        return;

    //    float s = voxelSize * 0.2f;
    //    Vector3 cubeSize = Vector3.one * s;

    //    int sizeX = Voxels.GetLength(0),
    //        sizeY = Voxels.GetLength(1),
    //        sizeZ = Voxels.GetLength(2);

    //    for (int x = 0; x < sizeX; x++)
    //        for (int y = 0; y < sizeY; y++)
    //            for (int z = 0; z < sizeZ; z++)
    //            {
    //                var voxel = Voxels[x, y, z];
    //                Gizmos.color = voxel.IsInside ? insideColor : outsideColor;

    //                // Transform the local voxel coordinate by the current transform:
    //                Vector3 worldPos = transform.TransformPoint(voxel.LocalPosition);
    //                Gizmos.DrawCube(worldPos, cubeSize);
    //            }
    //}



}
