using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Manages a mass-spring system for a soft body. Voxelizes the mesh to create interior mass points and connects them with springs.
/// Updates the simulation (forces, integration, constraints) each physics step, and handles simple ground collisions.
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Voxelization))]
public class MassSpring : MonoBehaviour
{
    // Global simulation parameters
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float timeStep = 0.005f;
    public int solverIterations = 10;  // iterations for position constraint solving (spring length enforcement)

    // Mass parameters
    public float massValue = 0.5f;
    public float massDamping = 1.5f;  // per-mass damping factor (simple drag)

    // Spring parameters
    public float springStiffness = 5000f;
    public float springDamping = 50f;
    public float connectionThreshold = 1.1f;  // how far (in voxel lengths) to connect points with a spring

    // Ground collision parameters
    public bool enableGroundCollision = true;
    public float groundHeight = 0f;
    public float collisionRestitution = 0.7f;  // bounce factor for ground collisions

    // Internal lists of masses and springs
    private List<Mass> masses = new List<Mass>();
    private List<Spring> springs = new List<Spring>();
    private Voxelization voxelizer;
    private float accumulatedTime = 0f;

    void Start()
    {
        voxelizer = GetComponent<Voxelization>();
        // Ensure voxel data is generated
        if (voxelizer != null && (voxelizer.insideVoxels == null || voxelizer.insideVoxels.Count == 0))
        {
            voxelizer.Voxelize();
        }
        BuildSystem();
    }

    /// <summary>Rebuilds the mass-spring system (masses and springs) from the current voxelization data.</summary>
    [ContextMenu("Rebuild System")]
    public void BuildSystem()
    {
        voxelizer = GetComponent<Voxelization>();
        if (voxelizer != null && (voxelizer.insideVoxels == null || voxelizer.insideVoxels.Count == 0))
        {
            voxelizer.Voxelize();
        }
        BuildMasses();
        BuildSprings();
    }

    // Create mass points at each interior voxel position
    private void BuildMasses()
    {
        masses.Clear();
        if (voxelizer == null || voxelizer.insideVoxels == null) return;
        foreach (Vector3 localPos in voxelizer.insideVoxels)
        {
            Mass m = new Mass(localPos, massValue, massDamping);
            masses.Add(m);
        }
    }

    // Create springs connecting nearby mass points (within threshold distance)
    private void BuildSprings()
    {
        springs.Clear();
        if (masses.Count == 0) return;
        float voxelSize = voxelizer.voxelSize;
        float maxDist = voxelSize * connectionThreshold;
        float maxDistSqr = maxDist * maxDist;
        // Spatial hashing: bucket masses into grid cells to reduce pair checks
        Dictionary<Vector3Int, List<int>> grid = new Dictionary<Vector3Int, List<int>>();
        float cellSize = maxDist;
        for (int i = 0; i < masses.Count; i++)
        {
            Vector3 p = masses[i].position;
            Vector3Int cell = new Vector3Int(
                Mathf.FloorToInt(p.x / cellSize),
                Mathf.FloorToInt(p.y / cellSize),
                Mathf.FloorToInt(p.z / cellSize)
            );
            if (!grid.ContainsKey(cell))
            {
                grid[cell] = new List<int>();
            }
            grid[cell].Add(i);
        }
        // For each mass, check neighbors in adjacent cells for proximity
        for (int i = 0; i < masses.Count; i++)
        {
            Vector3 p = masses[i].position;
            Vector3Int cell = new Vector3Int(
                Mathf.FloorToInt(p.x / cellSize),
                Mathf.FloorToInt(p.y / cellSize),
                Mathf.FloorToInt(p.z / cellSize)
            );
            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iy = -1; iy <= 1; iy++)
                {
                    for (int iz = -1; iz <= 1; iz++)
                    {
                        Vector3Int neighborCell = cell + new Vector3Int(ix, iy, iz);
                        if (!grid.ContainsKey(neighborCell)) continue;
                        foreach (int j in grid[neighborCell])
                        {
                            if (j <= i) continue; // avoid duplicates and self
                            float sqrDist = (masses[i].position - masses[j].position).sqrMagnitude;
                            if (sqrDist <= maxDistSqr)
                            {
                                // Connect a spring between mass i and j
                                Spring spring = new Spring(masses[i], masses[j], springStiffness, springDamping);
                                springs.Add(spring);
                            }
                        }
                    }
                }
            }
        }
        // Debug: print total number of springs
        UnityEngine.Debug.Log($"MassSpring: Created {springs.Count} springs connecting {masses.Count} mass points.");
    }

    void FixedUpdate()
    {
        // Use fixed time step accumulation for stability
        accumulatedTime += Time.fixedDeltaTime;
        while (accumulatedTime >= timeStep)
        {
            SimulateStep(timeStep);
            accumulatedTime -= timeStep;
        }
    }

    // Perform one simulation step: apply forces, integrate motion, enforce constraints
    private void SimulateStep(float dt)
    {
        // Reset forces and apply external forces (gravity and damping)
        foreach (Mass mass in masses)
        {
            mass.force = Vector3.zero;
            // Gravity
            mass.ApplyForce(mass.mass * gravity);
            // Damping (simple linear drag in proportion to velocity)
            mass.ApplyForce(-mass.damping * mass.velocity);
        }
        // Apply spring forces (elastic and damping along springs)
        foreach (Spring spring in springs)
        {
            spring.ApplyForces();
        }
        // Integrate velocities and positions (explicit Euler integration)
        foreach (Mass mass in masses)
        {
            Vector3 acceleration = mass.force / mass.mass;
            mass.velocity += acceleration * dt;
            mass.position += mass.velocity * dt;
        }
        // Solve constraints iteratively (spring rest lengths and ground collisions)
        for (int iter = 0; iter < solverIterations; iter++)
        {
            // Position-based constraint: enforce spring rest lengths
            foreach (Spring spring in springs)
            {
                ApplySpringConstraint(spring);
            }
            // Constraint: object resting on ground plane (y = groundHeight)
            if (enableGroundCollision)
            {
                foreach (Mass mass in masses)
                {
                    HandleGroundCollision(mass);
                }
            }
        }
    }

    // Position correction to enforce spring rest length (half adjustment to each mass)
    private void ApplySpringConstraint(Spring spring)
    {
        Vector3 delta = spring.massB.position - spring.massA.position;
        float currentLength = delta.magnitude;
        if (currentLength == 0f) return;
        float diff = currentLength - spring.restLength;
        // Move each mass by half the displacement along the spring line
        Vector3 correction = delta * (diff / currentLength * 0.5f);
        spring.massA.position += correction;
        spring.massB.position -= correction;
    }

    // Handle collision of a mass point with the ground plane at groundHeight (simple floor collision)
    private void HandleGroundCollision(Mass mass)
    {
        // Compute world position of mass
        Vector3 worldPos = transform.TransformPoint(mass.position);
        if (worldPos.y < groundHeight)
        {
            // Position correction: put the point on the ground
            worldPos.y = groundHeight;
            mass.position = transform.InverseTransformPoint(worldPos);
            // Reflect velocity vertical component with restitution (bounce damping)
            Vector3 worldVel = transform.TransformVector(mass.velocity);
            if (worldVel.y < 0f)
            {
                worldVel.y = -worldVel.y * collisionRestitution;
            }
            mass.velocity = transform.InverseTransformVector(worldVel);
        }
    }

    // Provide access to internal masses list (useful for collision manager)
    public IEnumerable<Mass> GetMasses()
    {
        return masses;
    }

    void OnDrawGizmos()
    {
        // Draw red spheres for mass points and blue lines for springs (for visualization in Editor)
        Gizmos.color = Color.red;
        foreach (Mass m in masses)
        {
            Vector3 worldPos = transform.TransformPoint(m.position);
            float r = (voxelizer != null ? voxelizer.voxelSize * 0.1f : 0.02f);
            Gizmos.DrawSphere(worldPos, r);
        }
        Gizmos.color = Color.blue;
        foreach (Spring s in springs)
        {
            Vector3 p = transform.TransformPoint(s.massA.position);
            Vector3 q = transform.TransformPoint(s.massB.position);
            Gizmos.DrawLine(p, q);
        }
    }
}
