using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages broad-phase AABB collision detection and triggers narrow-phase voxel collision checks.
/// Resolves collisions by adjusting mass positions and velocities (for soft bodies) and ensures proper force and deformation.
/// </summary>
[ExecuteAlways]
public class AABBCollisionManager : MonoBehaviour
{
    class AABBEntry
    {
        public Vector3 min;
        public Vector3 max;
        public GameObject obj;
    }

    private List<AABBEntry> aabbs = new List<AABBEntry>();
    private List<Vector3> collisionVoxels = new List<Vector3>(); // Stores world positions of colliding voxel points (for debug drawing)

    void Update()
    {
        // Collect all axis-aligned bounding boxes from objects with BoxBounder
        aabbs.Clear();
        collisionVoxels.Clear();
        foreach (BoxBounder bounder in FindObjectsOfType<BoxBounder>())
        {
            aabbs.Add(new AABBEntry
            {
                min = bounder.worldMin,
                max = bounder.worldMax,
                obj = bounder.gameObject
            });
        }
        // Sort by x-axis to optimize broad-phase pair checking
        aabbs.Sort((a, b) => a.min.x.CompareTo(b.min.x));

        // Broad-phase: check each pair whose AABBs overlap in x, then full overlap
        for (int i = 0; i < aabbs.Count; i++)
        {
            var a = aabbs[i];
            for (int j = i + 1; j < aabbs.Count; j++)
            {
                var b = aabbs[j];
                if (b.min.x > a.max.x) break; // no further overlaps along x axis
                if (IsColliding(a, b))
                {
                    // Both objects must have voxelization data for narrow-phase
                    Voxelization voxA = a.obj.GetComponent<Voxelization>();
                    Voxelization voxB = b.obj.GetComponent<Voxelization>();
                    if (voxA != null && voxB != null)
                    {
                        NarrowPhaseVoxelTest(voxA, voxB);
                    }
                }
            }
        }
    }

    // Checks AABB overlap between two entries in all axes
    private bool IsColliding(AABBEntry a, AABBEntry b)
    {
        return (a.min.x <= b.max.x && a.max.x >= b.min.x) &&
               (a.min.y <= b.max.y && a.max.y >= b.min.y) &&
               (a.min.z <= b.max.z && a.max.z >= b.min.z);
    }

    /// <summary>
    /// Narrow-phase collision detection between two objects using their voxel representations.
    /// Identifies overlapping voxels and applies collision resolution (position correction and impulse) to their masses.
    /// </summary>
    private void NarrowPhaseVoxelTest(Voxelization voxA, Voxelization voxB)
    {
        // Get simulation components and physics properties
        MassSpring simA = voxA.GetComponent<MassSpring>();
        MassSpring simB = voxB.GetComponent<MassSpring>();
        PhysicsBody bodyA = voxA.GetComponent<PhysicsBody>();
        PhysicsBody bodyB = voxB.GetComponent<PhysicsBody>();

        // Determine common cell size for spatial partition (use smaller voxel size for finer resolution)
        float cellSize = Mathf.Min(voxA.voxelSize, voxB.voxelSize);
        Vector3 halfCell = Vector3.one * (cellSize * 0.5f);

        // Build spatial hash for object A's points (either masses if soft, or static voxel centers)
        Dictionary<Vector3Int, List<(Vector3 worldPos, Mass mass)>> gridA = new Dictionary<Vector3Int, List<(Vector3, Mass)>>();
        if (simA != null)
        {
            // Use current mass positions for soft body A
            foreach (Mass m in simA.GetMasses())
            {
                Vector3 worldPos = voxA.transform.TransformPoint(m.position);
                Vector3Int cell = Vector3Int.FloorToInt(worldPos / cellSize);
                if (!gridA.ContainsKey(cell))
                    gridA[cell] = new List<(Vector3, Mass)>();
                gridA[cell].Add((worldPos, m));
            }
        }
        else
        {
            // Use static interior voxel centers for object A (likely rigid or environment)
            foreach (Vector3 localCenter in voxA.insideVoxels)
            {
                Vector3 worldPos = voxA.transform.TransformPoint(localCenter);
                Vector3Int cell = Vector3Int.FloorToInt(worldPos / cellSize);
                if (!gridA.ContainsKey(cell))
                    gridA[cell] = new List<(Vector3, Mass)>();
                // No Mass object (rigid body), store null for mass reference
                gridA[cell].Add((worldPos, null));
            }
        }

        // Prepare restitution and hardness factors
        float restitution = 0.5f;
        if (bodyA != null && bodyB != null) restitution = (bodyA.restitution + bodyB.restitution) * 0.5f;
        else if (bodyA != null) restitution = bodyA.restitution;
        else if (bodyB != null) restitution = bodyB.restitution;
        float hardA = bodyA ? bodyA.hardness : 1.0f;
        float hardB = bodyB ? bodyB.hardness : 1.0f;
        // Treat objects with no mass-spring (rigid/static) as very hard (immovable)
        if (simA == null) hardA = 1000f;
        if (simB == null) hardB = 1000f;
        float totalHard = hardA + hardB;
        float fracA = (totalHard > 0) ? hardB / totalHard : 0.5f;
        float fracB = (totalHard > 0) ? hardA / totalHard : 0.5f;

        // Iterate through object B's points and check neighbors in gridA
        if (simB != null)
        {
            // Soft object B: use its current mass positions
            foreach (Mass mB in simB.GetMasses())
            {
                Vector3 worldB = voxB.transform.TransformPoint(mB.position);
                Vector3Int cellB = Vector3Int.FloorToInt(worldB / cellSize);
                // Check neighboring cells in A's grid (within 1 cell radius) for potential overlaps
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            Vector3Int neighborCell = cellB + new Vector3Int(dx, dy, dz);
                            if (!gridA.ContainsKey(neighborCell)) continue;
                            foreach ((Vector3 worldA, Mass mA) in gridA[neighborCell])
                            {
                                if (AABBOverlap(worldA, worldB, cellSize))
                                {
                                    // Record overlapping points for debug
                                    collisionVoxels.Add(worldA);
                                    collisionVoxels.Add(worldB);
                                    // Compute collision normal and penetration
                                    Vector3 normal = worldA - worldB;
                                    float dist = normal.magnitude;
                                    if (dist < 1e-6f)
                                    {
                                        normal = Vector3.up; // arbitrary normal if overlapping almost exactly
                                        dist = 0f;
                                    }
                                    else
                                    {
                                        normal /= dist;
                                    }
                                    float penetration = cellSize - dist;
                                    if (penetration < 0f) penetration = 0f;
                                    // Position correction: push points apart based on hardness fractions
                                    if (mA != null)
                                    {
                                        // Object A is soft
                                        Vector3 localMoveA = voxA.transform.InverseTransformVector(normal * (penetration * 0.5f * fracA));
                                        mA.position += localMoveA;
                                    }
                                    if (mB != null)
                                    {
                                        // Object B is soft
                                        Vector3 localMoveB = voxB.transform.InverseTransformVector(-normal * (penetration * 0.5f * fracB));
                                        mB.position += localMoveB;
                                    }

                                    // Velocity adjustment (impulse) for bounce 
                                    // Convert velocities to world space for calculation
                                    Vector3 vA_world = (mA != null && simA != null) ? voxA.transform.TransformVector(mA.velocity) : Vector3.zero;
                                    Vector3 vB_world = (mB != null && simB != null) ? voxB.transform.TransformVector(mB.velocity) : Vector3.zero;
                                    // Relative velocity along collision normal
                                    float relVel = Vector3.Dot(vB_world - vA_world, normal);
                                    if (relVel < 0f)
                                    {
                                        // Compute impulse scalar (elastic collision with restitution)
                                        float invMassA = (mA != null && mA.mass > 0) ? 1.0f / mA.mass : 0f;
                                        float invMassB = (mB != null && mB.mass > 0) ? 1.0f / mB.mass : 0f;
                                        float j = -(1 + restitution) * relVel;
                                        if (invMassA + invMassB > 1e-6f)
                                            j /= (invMassA + invMassB);
                                        Vector3 impulse = j * normal;
                                        // Apply impulse to velocities (divide by mass)
                                        if (mA != null)
                                        {
                                            vA_world -= impulse * invMassA;
                                            mA.velocity = voxA.transform.InverseTransformVector(vA_world);
                                        }
                                        if (mB != null)
                                        {
                                            vB_world += impulse * invMassB;
                                            mB.velocity = voxB.transform.InverseTransformVector(vB_world);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        else
        {
            // Object B has no mass-spring (likely rigid), treat its points as static
            foreach (Vector3 localB in voxB.insideVoxels)
            {
                Vector3 worldB = voxB.transform.TransformPoint(localB);
                Vector3Int cellB = Vector3Int.FloorToInt(worldB / cellSize);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            Vector3Int neighborCell = cellB + new Vector3Int(dx, dy, dz);
                            if (!gridA.ContainsKey(neighborCell)) continue;
                            foreach ((Vector3 worldA, Mass mA) in gridA[neighborCell])
                            {
                                if (AABBOverlap(worldA, worldB, cellSize))
                                {
                                    collisionVoxels.Add(worldA);
                                    collisionVoxels.Add(worldB);
                                    // Collision normal and penetration
                                    Vector3 normal = worldA - worldB;
                                    float dist = normal.magnitude;
                                    if (dist < 1e-6f)
                                    {
                                        normal = Vector3.up;
                                        dist = 0f;
                                    }
                                    else
                                    {
                                        normal /= dist;
                                    }
                                    float penetration = cellSize - dist;
                                    if (penetration < 0f) penetration = 0f;
                                    // Push soft object A away (B is static/hard)
                                    if (mA != null)
                                    {
                                        Vector3 localMoveA = voxA.transform.InverseTransformVector(normal * penetration);
                                        mA.position += localMoveA;
                                    }
                                    // Velocity bounce: reflect A's velocity
                                    if (mA != null && simA != null)
                                    {
                                        Vector3 vA_world = voxA.transform.TransformVector(mA.velocity);
                                        float approachSpeed = Vector3.Dot(vA_world, normal);
                                        if (approachSpeed < 0f)
                                        {
                                            // Invert velocity component along normal
                                            vA_world -= (1 + restitution) * approachSpeed * normal;
                                            mA.velocity = voxA.transform.InverseTransformVector(vA_world);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Checks if two cubes (centered at points a and b with side length = size) overlap
    private bool AABBOverlap(Vector3 aCenter, Vector3 bCenter, float size)
    {
        // Two voxel cubes overlap if distance between centers in each axis is less than full size
        return Mathf.Abs(aCenter.x - bCenter.x) < size &&
               Mathf.Abs(aCenter.y - bCenter.y) < size &&
               Mathf.Abs(aCenter.z - bCenter.z) < size;
    }

    void OnDrawGizmos()
    {
        // Draw debug markers for collision points
        Gizmos.color = Color.blue;
        float drawSize = 0.1f;
        if (collisionVoxels.Count > 0)
        {
            // If available, scale debug cube size relative to voxel size
            Voxelization anyVox = FindObjectOfType<Voxelization>();
            if (anyVox != null) drawSize = anyVox.voxelSize * 0.2f;
        }
        for (int i = 0; i < collisionVoxels.Count; i++)
        {
            Gizmos.DrawCube(collisionVoxels[i], Vector3.one * drawSize);
        }
    }
}
