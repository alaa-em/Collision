using UnityEngine;

[RequireComponent(typeof(Voxelization), typeof(MeshFilter))]
public class MarchCubeRunner : MonoBehaviour
{
    [Range(-1f, 1f)]
    public float isoLevel = 0f;          // 0 = surface of signed-distance field

    void Start()
    {
        var vox = GetComponent<Voxelization>();
        var voxGrid = vox.Voxels;        // Voxel[,,]

        // ----------- STEP 1 : copy just the Density into a float[,,] -------------
        int sx = voxGrid.GetLength(0);
        int sy = voxGrid.GetLength(1);
        int sz = voxGrid.GetLength(2);

        float[,,] density = new float[sx, sy, sz];

        for (int x = 0; x < sx; ++x)
            for (int y = 0; y < sy; ++y)
                for (int z = 0; z < sz; ++z)
                    density[x, y, z] = voxGrid[x, y, z].Density;
        // -------------------------------------------------------------------------

        // STEP 2 : generate the surface mesh
        var mc = new MarchCube();
        Mesh surface = mc.GenerateMesh(density, isoLevel);

        // STEP 3 : show it
        GetComponent<MeshFilter>().mesh = surface;
    }
}
