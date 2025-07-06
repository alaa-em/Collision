using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class AABBCollisionManager : MonoBehaviour
{
    class AABBEntry
    {
        public Vector3 min, max;
        public GameObject obj;
    }

    List<AABBEntry> aabbs = new List<AABBEntry>();
    private List<Vector3> collisionVoxels = new List<Vector3>();

    void Update()
    {
        aabbs.Clear();
        collisionVoxels.Clear();

        foreach (BoxBounder b in FindObjectsOfType<BoxBounder>())
        {
            aabbs.Add(new AABBEntry
            {
                min = b.worldMin,
                max = b.worldMax,
                obj = b.gameObject
            });
        }

        aabbs.Sort((a, b) => a.min.x.CompareTo(b.min.x));

        for (int i = 0; i < aabbs.Count; i++)
        {
            var a = aabbs[i];
            for (int j = i + 1; j < aabbs.Count; j++)
            {
                var b = aabbs[j];
                if (b.min.x > a.max.x) break;

                if (IsColliding(a, b))
                {
                    var voxA = a.obj.GetComponent<Voxelization>();
                    var voxB = b.obj.GetComponent<Voxelization>();
                    if (voxA != null && voxB != null)
                        NarrowPhaseVoxelTest(voxA, voxB);
                }
            }
        }
    }

    bool IsColliding(AABBEntry a, AABBEntry b)
    {
        return (a.min.x <= b.max.x && a.max.x >= b.min.x) &&
               (a.min.y <= b.max.y && a.max.y >= b.min.y) &&
               (a.min.z <= b.max.z && a.max.z >= b.min.z);
    }

    void NarrowPhaseVoxelTest(Voxelization voxA, Voxelization voxB)
    {
        float sizeA = voxA.voxelSize;
        float sizeB = voxB.voxelSize;
        Vector3 halfA = Vector3.one * (sizeA * 0.5f);
        Vector3 halfB = Vector3.one * (sizeB * 0.5f);

        var voxelBoundsA = new List<(Vector3 min, Vector3 max, Vector3 center)>(voxA.insideVoxels.Count);
        //foreach (var localCenterA in voxA.insideVoxels)
        //{
        //    Vector3 worldCenterA = voxA.transform.TransformPoint(localCenterA);
        //    voxelBoundsA.Add((worldCenterA - halfA, worldCenterA + halfA, worldCenterA));
        //}

        //foreach (var localCenterB in voxB.insideVoxels)
        //{
        //    Vector3 worldCenterB = voxB.transform.TransformPoint(localCenterB);
        //    Vector3 minB = worldCenterB - halfB;
        //    Vector3 maxB = worldCenterB + halfB;

        //    for (int i = 0; i < voxelBoundsA.Count; i++)
        //    {
        //        var (minA, maxA, centerA) = voxelBoundsA[i];

        //        if (minA.x <= maxB.x && maxA.x >= minB.x &&
        //            minA.y <= maxB.y && maxA.y >= minB.y &&
        //            minA.z <= maxB.z && maxA.z >= minB.z)
        //        {
        //            collisionVoxels.Add(centerA);
        //            collisionVoxels.Add(worldCenterB);
        //        }
        //    }
        //}
        var v = FindObjectOfType<Voxelization>();
        Dictionary<Vector3Int, List<Vector3>> voxelGridA = new Dictionary<Vector3Int, List<Vector3>>();


        foreach (var localCenterA in voxA.insideVoxels)
        {
            Vector3 worldCenterA = voxA.transform.TransformPoint(localCenterA);
            Vector3Int cell = Vector3Int.FloorToInt(worldCenterA / v.voxelSize); // Assumes uniform voxel size

            if (!voxelGridA.TryGetValue(cell, out var list))
            {
                list = new List<Vector3>();
                voxelGridA[cell] = list;
            }
            list.Add(worldCenterA);
        }

        foreach (var localCenterB in voxB.insideVoxels)
        {
            Vector3 worldCenterB = voxB.transform.TransformPoint(localCenterB);
            Vector3Int cellB = Vector3Int.FloorToInt(worldCenterB / v.voxelSize);

            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int neighborCell = cellB + new Vector3Int(x, y, z);
                        if (voxelGridA.TryGetValue(neighborCell, out var list))
                        {
                            foreach (var centerA in list)
                            {
                                if (AABBOverlap(centerA, worldCenterB, v.voxelSize))
                                {
                                    collisionVoxels.Add(centerA);
                                    collisionVoxels.Add(worldCenterB);
                                }
                            }
                        }
                    }
        }

    }

    bool AABBOverlap(Vector3 a, Vector3 b, float size)
    {
        float half = size * 0.5f;
        return Mathf.Abs(a.x - b.x) < size &&
               Mathf.Abs(a.y - b.y) < size &&
               Mathf.Abs(a.z - b.z) < size;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;

        float drawSize = 0.1f;
        if (collisionVoxels.Count > 0)
        {
            // Use voxel size from any relevant object
            var v = FindObjectOfType<Voxelization>();
            if (v != null) drawSize = v.voxelSize * 0.2f;
        }

        foreach (var c in collisionVoxels)
        {
            Gizmos.DrawCube(c, Vector3.one * drawSize);
        }
    }

}
