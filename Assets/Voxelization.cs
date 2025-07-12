using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Performs voxelization of a mesh to determine interior points (voxels) for soft-body simulation.
/// Generates a 3D grid of voxels, identifies inside voxels, and prepares density data for marching-cubes.
/// </summary>
public class Voxelization : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────────
    // Inspector Parameters
    // ──────────────────────────────────────────────────────────────────────────────

    [Tooltip("Voxel side length for volumetric discretization.")]
    public float voxelSize = 0.2f;

    /// <summary>Soft-body material type (controls spring connectivity).</summary>
    public enum MaterialType { Jelly, Metal }

    [Tooltip("Soft-body material type (determines spring connections).")]
    public MaterialType softMaterialType = MaterialType.Jelly;

    // ──────────────────────────────────────────────────────────────────────────────
    // Public Data
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Local positions of interior voxel centres (points inside the mesh volume).</summary>
    public List<Vector3> insideVoxels = new List<Vector3>();

    /// <summary>3-D grid of voxels containing density values (negative = inside, positive = outside).</summary>
    public Voxel[,,] Voxels;

    /// <summary>Structure representing a voxel-grid point.</summary>
    public struct Voxel
    {
        public Vector3Int Index;
        public Vector3 LocalPosition;
        public Vector3 WorldPosition;
        public float Density;
        public Voxel(Vector3Int index, Vector3 localPos, Vector3 worldPos, float density)
        {
            Index = index;
            LocalPosition = localPos;
            WorldPosition = worldPos;
            Density = density;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Voxelization
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Voxelizes the attached mesh and populates <see cref="insideVoxels"/> and <see cref="Voxels"/>.
    /// Must be called before soft-body setup or marching-cubes mesh generation.
    /// </summary>
    public void Voxelize()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
             UnityEngine.Debug.LogWarning($"{name}: Voxelization aborted (no mesh found).");
            return;
        }

        // Ensure a MeshCollider exists for overlap checks (add temporarily if needed)
        MeshCollider tempCol = GetComponent<MeshCollider>();
        bool addedTempCol = false;
        if (tempCol == null)
        {
            tempCol = gameObject.AddComponent<MeshCollider>();
            tempCol.convex = false;
            addedTempCol = true;
        }
        tempCol.sharedMesh = mf.sharedMesh;

        // Local AABB padded by one voxel
        Bounds meshBounds = mf.sharedMesh.bounds;
        Vector3 min = meshBounds.min - Vector3.one * voxelSize;
        Vector3 max = meshBounds.max + Vector3.one * voxelSize;

        // Grid dimensions
        int nx = Mathf.CeilToInt((max.x - min.x) / voxelSize) + 1;
        int ny = Mathf.CeilToInt((max.y - min.y) / voxelSize) + 1;
        int nz = Mathf.CeilToInt((max.z - min.z) / voxelSize) + 1;

        insideVoxels.Clear();

        // Overlap-box test per voxel cell
        Quaternion orientation = transform.rotation;
        Vector3 lossyScale = transform.lossyScale;
        Vector3 halfExtents = 0.5f * new Vector3(voxelSize * lossyScale.x,
                                                    voxelSize * lossyScale.y,
                                                    voxelSize * lossyScale.z);

        for (int ix = 0; ix < nx - 1; ix++)
        {
            for (int iy = 0; iy < ny - 1; iy++)
            {
                for (int iz = 0; iz < nz - 1; iz++)
                {
                    Vector3 localCenter = min + new Vector3((ix + 0.5f) * voxelSize,
                                                            (iy + 0.5f) * voxelSize,
                                                            (iz + 0.5f) * voxelSize);
                    Vector3 worldCenter = transform.TransformPoint(localCenter);

                    Collider[] hits = Physics.OverlapBox(worldCenter, halfExtents, orientation);
                    bool inside = false;
                    foreach (Collider col in hits)
                    {
                        if (col.gameObject == gameObject) { inside = true; break; }
                    }
                    if (inside) insideVoxels.Add(localCenter);
                }
            }
        }

        if (addedTempCol) DestroyImmediate(tempCol);

        // Build density grid (default outside = +1)
        Voxels = new Voxel[nx, ny, nz];
        Vector3 origin = min;
        for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 localPos = origin + new Vector3(ix * voxelSize,
                                                            iy * voxelSize,
                                                            iz * voxelSize);
                    Vector3 worldPos = transform.TransformPoint(localPos);
                    Voxels[ix, iy, iz] =
                        new Voxel(new Vector3Int(ix, iy, iz), localPos, worldPos, +1f);
                }

        // Mark corners touching overlap boxes as inside (density = –1)
        foreach (Vector3 localCenter in insideVoxels)
        {
            int cx = Mathf.FloorToInt((localCenter.x - origin.x) / voxelSize);
            int cy = Mathf.FloorToInt((localCenter.y - origin.y) / voxelSize);
            int cz = Mathf.FloorToInt((localCenter.z - origin.z) / voxelSize);

            for (int dx = 0; dx <= 1; dx++)
                for (int dy = 0; dy <= 1; dy++)
                    for (int dz = 0; dz <= 1; dz++)
                    {
                        int px = cx + dx, py = cy + dy, pz = cz + dz;
                        if (px < nx && py < ny && pz < nz)
                            Voxels[px, py, pz].Density = -1f;
                    }
        }

        // ── Flood-fill to tag all true exterior voxels ────────────────────────────
        bool[,,] visited = new bool[nx, ny, nz];
        Queue<Vector3Int> q = new Queue<Vector3Int>();

        // Seed queue with boundary voxels known to be outside
        for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (ix == 0 || ix == nx - 1 ||
                        iy == 0 || iy == ny - 1 ||
                        iz == 0 || iz == nz - 1)
                    {
                        if (Voxels[ix, iy, iz].Density > 0f)
                        {
                            visited[ix, iy, iz] = true;
                            q.Enqueue(new Vector3Int(ix, iy, iz));
                        }
                    }
                }

        int[] dx6 = { 1, -1, 0, 0, 0, 0 };
        int[] dy6 = { 0, 0, 1, -1, 0, 0 };
        int[] dz6 = { 0, 0, 0, 0, 1, -1 };

        while (q.Count > 0)
        {
            Vector3Int v = q.Dequeue();
            for (int n = 0; n < 6; n++)
            {
                int nx_i = v.x + dx6[n];
                int ny_i = v.y + dy6[n];
                int nz_i = v.z + dz6[n];
                if (nx_i >= 0 && nx_i < nx &&
                    ny_i >= 0 && ny_i < ny &&
                    nz_i >= 0 && nz_i < nz &&
                    !visited[nx_i, ny_i, nz_i] &&
                    Voxels[nx_i, ny_i, nz_i].Density > 0f)
                {
                    visited[nx_i, ny_i, nz_i] = true;
                    q.Enqueue(new Vector3Int(nx_i, ny_i, nz_i));
                }
            }
        }

        // Any unvisited positive-density voxel is actually inside ⇒ set to –1
        for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
                for (int iz = 0; iz < nz; iz++)
                {
                    if (!visited[ix, iy, iz] && Voxels[ix, iy, iz].Density > 0f)
                        Voxels[ix, iy, iz].Density = -1f;
                }

        // Build final list of interior cell centres
        insideVoxels.Clear();
        for (int cx = 0; cx < nx - 1; cx++)
            for (int cy = 0; cy < ny - 1; cy++)
                for (int cz = 0; cz < nz - 1; cz++)
                {
                    bool cellInside = false;
                    for (int dx = 0; dx <= 1 && !cellInside; dx++)
                        for (int dy = 0; dy <= 1 && !cellInside; dy++)
                            for (int dz = 0; dz <= 1 && !cellInside; dz++)
                            {
                                if (Voxels[cx + dx, cy + dy, cz + dz].Density < 0f)
                                    cellInside = true;
                            }
                    if (cellInside)
                    {
                        Vector3 cellCenter = origin + new Vector3((cx + 0.5f) * voxelSize,
                                                                  (cy + 0.5f) * voxelSize,
                                                                  (cz + 0.5f) * voxelSize);
                        insideVoxels.Add(cellCenter);
                    }
                }
    }
}
