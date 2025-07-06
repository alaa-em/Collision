using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Voxelization))]
public class MassSpring : MonoBehaviour
{
    [Header("References")]
    public Voxelization voxelization;

    [Header("Spring Material")]
    public SpringMaterial springMaterial;

    [System.Serializable]
    public enum MaterialType { Steel, Rubber, Wood, Jelly, Cloth }

    [System.Serializable]
    public struct SpringMaterial
    {
        public MaterialType type;
        public float stiffness;
        public float damping;

        public static SpringMaterial GetPreset(MaterialType mat)
        {
            switch (mat)
            {
                case MaterialType.Steel: return new SpringMaterial { type = mat, stiffness = 10000f, damping = 20f };
                case MaterialType.Rubber: return new SpringMaterial { type = mat, stiffness = 500f, damping = 10f };
                case MaterialType.Wood: return new SpringMaterial { type = mat, stiffness = 2000f, damping = 15f };
                case MaterialType.Jelly: return new SpringMaterial { type = mat, stiffness = 200f, damping = 30f };
                case MaterialType.Cloth: return new SpringMaterial { type = mat, stiffness = 100f, damping = 50f };
                default: return new SpringMaterial { type = mat, stiffness = 500f, damping = 5f };
            }
        }
    }

    private List<Particle> particles;
    private List<Spring> springs;
    private Matrix4x4 prevLocalToWorld;

    private void Start()
    {
        if (voxelization == null)
            voxelization = GetComponent<Voxelization>();

        // Initialize particles at voxel centers (local space)
        particles = new List<Particle>(voxelization.insideVoxels.Count);
        foreach (var localCenter in voxelization.insideVoxels)
            particles.Add(new Particle(localCenter, Vector3.zero, 2));

        // Build different spring types based on voxel connectivity
        springs = new List<Spring>();
        float vs = voxelization.voxelSize;
        float diag = Mathf.Sqrt(2) * vs;
        float dbl = 2 * vs;

        // Structural springs: direct neighbors (grid axis)
        AddSprings((a, b) => Vector3.Distance(a.localPosition, b.localPosition) <= vs * 1.01f);
        // Shear springs: face diagonals
        AddSprings((a, b) => Mathf.Abs(Vector3.Distance(a.localPosition, b.localPosition) - diag) < 0.01f);
        // Bending springs: two-step neighbors
        AddSprings((a, b) => Mathf.Abs(Vector3.Distance(a.localPosition, b.localPosition) - dbl) < 0.01f);
        // (Optional) Volumetric springs: across cube diagonals (3D)
        float cubeDiag = Mathf.Sqrt(3) * vs;
        AddSprings((a, b) => Mathf.Abs(Vector3.Distance(a.localPosition, b.localPosition) - cubeDiag) < 0.01f);

        prevLocalToWorld = transform.localToWorldMatrix;
    }

    private void OnDrawGizmos()
    {
        if (particles == null || springs == null) return;

        Gizmos.color = Color.cyan;
        foreach (var s in springs)
        {
            Vector3 A = transform.TransformPoint(particles[s.i].localPosition);
            Vector3 B = transform.TransformPoint(particles[s.j].localPosition);
            Gizmos.DrawLine(A, B);
        }
    }


    private void AddSprings(System.Func<Particle, Particle, bool> predicate)
    {
        int c = particles.Count;
        for (int i = 0; i < c; i++)
            for (int j = i + 1; j < c; j++)
                if (predicate(particles[i], particles[j]))
                    springs.Add(new Spring(i, j, Vector3.Distance(particles[i].localPosition, particles[j].localPosition)));
    }

    private struct Particle
    {
        public Vector3 localPosition;
        public Vector3 velocity;
        public float mass;
        public Particle(Vector3 pos, Vector3 vel, float m) { localPosition = pos; velocity = vel; mass = m; }
    }

    private struct Spring
    {
        public int i, j;
        public float restLength;
        public Spring(int a, int b, float length) { i = a; j = b; restLength = length; }
    }
}
