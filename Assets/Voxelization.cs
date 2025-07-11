using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Performs voxelization of a mesh to determine interior points (voxels) for soft body simulation.
/// Generates a 3D grid of voxels, identifies inside voxels, and prepares density data for marching cubes.
/// </summary>
public class Voxelization : MonoBehaviour
{
    [Tooltip("Voxel side length for volumetric discretization.")]
    public float voxelSize = 0.15f;

    /// <summary>Local positions of interior voxel centers (points inside the mesh volume).</summary>
    public List<Vector3> insideVoxels = new List<Vector3>();

    /// <summary>3D grid of voxels containing density values (negative inside, positive outside).</summary>
    public Voxel[,,] Voxels;

    /// <summary>Structure representing a voxel grid point.</summary>
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

    /// <summary>
    /// Voxelizes the attached mesh and populates insideVoxels and Voxels (density grid).
    /// Must be called before soft body setup or marching cubes mesh generation.
    /// </summary>
    public void Voxelize()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            UnityEngine.Debug.LogWarning($"{name}: Voxelization aborted (no mesh found).");
            return;
        }
        // Ensure a MeshCollider exists for overlap checks (if none, add temporarily)
        MeshCollider tempCollider = gameObject.GetComponent<MeshCollider>();
        bool addedTempCollider = false;
        if (tempCollider == null)
        {
            tempCollider = gameObject.AddComponent<MeshCollider>();
            tempCollider.convex = false; // keep as non-convex for accurate interior detection
            addedTempCollider = true;
        }
        tempCollider.sharedMesh = mf.sharedMesh;

        // Determine mesh bounding box in local space and pad it by one voxel on each side
        Bounds meshBounds = mf.sharedMesh.bounds;
        Vector3 min = meshBounds.min - Vector3.one * voxelSize;
        Vector3 max = meshBounds.max + Vector3.one * voxelSize;
        // Compute grid dimensions (number of sample points along each axis)
        int nx = Mathf.CeilToInt((max.x - min.x) / voxelSize) + 1;
        int ny = Mathf.CeilToInt((max.y - min.y) / voxelSize) + 1;
        int nz = Mathf.CeilToInt((max.z - min.z) / voxelSize) + 1;
        // Reset interior voxel list
        insideVoxels.Clear();

        // Iterate through each potential voxel cell in the grid
        Quaternion orientation = transform.rotation;
        Vector3 lossyScale = transform.lossyScale;
        Vector3 halfExtents = 0.5f * new Vector3(voxelSize * lossyScale.x, voxelSize * lossyScale.y, voxelSize * lossyScale.z);
        for (int ix = 0; ix < nx - 1; ix++)
        {
            for (int iy = 0; iy < ny - 1; iy++)
            {
                for (int iz = 0; iz < nz - 1; iz++)
                {
                    // Center of this voxel cell in local space
                    Vector3 localCenter = min + new Vector3((ix + 0.5f) * voxelSize, (iy + 0.5f) * voxelSize, (iz + 0.5f) * voxelSize);
                    // Convert to world position for physics check
                    Vector3 worldCenter = transform.TransformPoint(localCenter);
                    // Check if a cube at this position overlaps the mesh (indicating inside or touching surface)
                    Collider[] hits = Physics.OverlapBox(worldCenter, halfExtents, orientation);
                    bool intersectsMesh = false;
                    foreach (Collider col in hits)
                    {
                        if (col.gameObject == gameObject)
                        {
                            intersectsMesh = true;
                            break;
                        }
                    }
                    if (intersectsMesh)
                    {
                        insideVoxels.Add(localCenter);
                    }
                }
            }
        }
        // Remove temporary collider if it was added
        if (addedTempCollider)
        {
            DestroyImmediate(tempCollider);
        }

        // Initialize the voxel grid of density values
        Voxels = new Voxel[nx, ny, nz];
        Vector3 origin = min;
        for (int ix = 0; ix < nx; ix++)
        {
            for (int iy = 0; iy < ny; iy++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 localPos = origin + new Vector3(ix * voxelSize, iy * voxelSize, iz * voxelSize);
                    Vector3 worldPos = transform.TransformPoint(localPos);
                    // Default density: positive (+1) for outside
                    Voxels[ix, iy, iz] = new Voxel(new Vector3Int(ix, iy, iz), localPos, worldPos, +1f);
                }
            }
        }
        // Mark grid points (corners) that are inside by checking adjacent inside voxels
        foreach (Vector3 localCenter in insideVoxels)
        {
            // Determine indices of the voxel cell containing this center
            int cx = Mathf.FloorToInt((localCenter.x - origin.x) / voxelSize);
            int cy = Mathf.FloorToInt((localCenter.y - origin.y) / voxelSize);
            int cz = Mathf.FloorToInt((localCenter.z - origin.z) / voxelSize);
            // The cell corners in the grid are at (cx,cy,cz) through (cx+1,cy+1,cz+1)
            for (int dx = 0; dx <= 1; dx++)
            {
                for (int dy = 0; dy <= 1; dy++)
                {
                    for (int dz = 0; dz <= 1; dz++)
                    {
                        int px = cx + dx;
                        int py = cy + dy;
                        int pz = cz + dz;
                        if (px < nx && py < ny && pz < nz)
                        {
                            // Set density to negative for points in or on the surface (inside region)
                            Voxels[px, py, pz].Density = -1f;
                        }
                    }
                }
            }
        }
    }
}
