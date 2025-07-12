using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Manages a mass-spring system for a soft body. Voxelizes the mesh to create interior mass points and connects them with springs.
/// Updates the simulation (forces, integration, constraints) each physics step, and handles simple ground collisions and mesh deformation.
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(Voxelization))]
[RequireComponent(typeof(MeshFilter))]
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
    public float connectionThreshold = 1.1f;  // base connection distance in voxel units (for Jelly material)

    // Ground collision parameters
    public bool enableGroundCollision = true;
    public float groundHeight = 0f;
    public float collisionRestitution = 0.7f;  // bounce factor for ground collisions

    // Internal lists of masses and springs
    private List<Mass> masses = new List<Mass>();
    private List<Spring> springs = new List<Spring>();
    private Voxelization voxelizer;
    private float accumulatedTime = 0f;

    // Data for mesh deformation
    private Mesh originalMesh;
    private Mesh deformedMesh;
    private Vector3[] originalVertices;
    private int[,] vertexToMassIndices;
    private float[,] vertexWeights;
    private Vector3[] vertexOffsets;

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
        // Prepare mesh for deformation after masses and springs are built
        SetupMeshDeformation();
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

    // Create springs connecting nearby mass points based on material type
    private void BuildSprings()
    {
        springs.Clear();
        if (masses.Count == 0) return;
        float voxelSize = voxelizer.voxelSize;
        // Determine connection range: Jelly = orthogonal only, Metal = include diagonals (sqrt(3) distance)
        float thresholdFactor = (voxelizer.softMaterialType == Voxelization.MaterialType.Metal ? 1.8f : 1.1f);
        float maxDist = voxelSize * thresholdFactor;
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
        // For each mass, check neighboring cells for nearby masses
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
                            if (j <= i) continue; // avoid duplicate pairs and self
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
        // Debug: print total number of springs created
        UnityEngine.Debug.Log($"MassSpring: Created {springs.Count} springs connecting {masses.Count} mass points (Material={voxelizer.softMaterialType}).");
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
        // Update visual mesh vertices based on mass movements after simulation
        if (deformedMesh != null)
        {
            UpdateMeshFromMasses();
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
            // Damping (simple linear drag proportional to velocity)
            mass.ApplyForce(-mass.damping * mass.velocity);
        }
        // Apply spring forces (elastic + damping along springs)
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
        // Solve constraints iteratively (enforce spring rest lengths and handle ground collisions)
        for (int iter = 0; iter < solverIterations; iter++)
        {
            // Constraint: enforce spring rest lengths (position-based correction)
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
            // Reflect velocity's vertical component with restitution (bounce)
            Vector3 worldVel = transform.TransformVector(mass.velocity);
            if (worldVel.y < 0f)
            {
                worldVel.y = -worldVel.y * collisionRestitution;
            }
            mass.velocity = transform.InverseTransformVector(worldVel);
        }
    }

    // Expose internal masses list (for collision manager or other uses)
    public IEnumerable<Mass> GetMasses()
    {
        return masses;
    }

    /// <summary>
    /// Prepares the mesh for real-time deformation by creating a copy of the original mesh 
    /// and calculating mappings from mesh vertices to mass points.
    /// </summary>
    private void SetupMeshDeformation()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        // Make a copy of the original mesh for deformation
        originalMesh = mf.sharedMesh;
        deformedMesh = Instantiate(originalMesh);
        mf.mesh = deformedMesh;
        // Get original vertex positions (local space)
        originalVertices = originalMesh.vertices;
        int vertexCount = originalVertices.Length;
        vertexToMassIndices = new int[vertexCount, 4];
        vertexWeights = new float[vertexCount, 4];
        vertexOffsets = new Vector3[vertexCount];
        // Precompute mass initial positions for distance calculations
        Vector3[] massPositions = new Vector3[masses.Count];
        for (int m = 0; m < masses.Count; m++)
        {
            massPositions[m] = masses[m].position;
        }
        // For each vertex, find up to 4 nearest mass points
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 vPos = originalVertices[i];
            // Initialize nearest neighbor tracking
            float[] bestDist = { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
            int[] bestIndex = { -1, -1, -1, -1 };
            // Find 4 closest masses (using squared distance for efficiency)
            for (int m = 0; m < massPositions.Length; m++)
            {
                float distSqr = (massPositions[m] - vPos).sqrMagnitude;
                if (distSqr < bestDist[3])
                {
                    // Insert this mass in sorted order (smallest distances first)
                    int insertPos = 3;
                    if (distSqr < bestDist[2]) insertPos = 2;
                    if (distSqr < bestDist[1]) insertPos = 1;
                    if (distSqr < bestDist[0]) insertPos = 0;
                    // Shift larger distances downwards
                    for (int k = 3; k > insertPos; k--)
                    {
                        bestDist[k] = bestDist[k - 1];
                        bestIndex[k] = bestIndex[k - 1];
                    }
                    bestDist[insertPos] = distSqr;
                    bestIndex[insertPos] = m;
                }
            }
            // Compute weights for the 4 nearest masses
            float[] w = { 0f, 0f, 0f, 0f };
            float wSum = 0f;
            // If the closest mass is extremely near the vertex, assign full weight to it
            if (bestIndex[0] != -1 && bestDist[0] < 1e-6f)
            {
                w[0] = 1f;
                bestIndex[1] = bestIndex[2] = bestIndex[3] = -1;
            }
            else
            {
                for (int k = 0; k < 4; k++)
                {
                    if (bestIndex[k] < 0) break;
                    float dist = Mathf.Sqrt(bestDist[k]);
                    // Inverse-distance weighting
                    w[k] = (dist > 1e-6f ? 1f / dist : 0f);
                    wSum += w[k];
                }
                // Normalize weights to sum to 1
                if (wSum > 1e-6f)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        w[k] /= wSum;
                    }
                }
            }
            // Compute the vertex's initial position approximation from masses and store offset
            Vector3 approxPos = Vector3.zero;
            for (int k = 0; k < 4; k++)
            {
                vertexToMassIndices[i, k] = bestIndex[k];
                vertexWeights[i, k] = w[k];
                if (bestIndex[k] >= 0)
                {
                    approxPos += massPositions[bestIndex[k]] * w[k];
                }
            }
            // Offset is the difference between actual vertex position and mass-interpolated position
            vertexOffsets[i] = vPos - approxPos;
        }
    }

    /// <summary>
    /// Updates the deformed mesh vertices each frame based on current mass positions.
    /// </summary>
    private void UpdateMeshFromMasses()
    {
        if (deformedMesh == null) return;
        Vector3[] vertices = new Vector3[originalVertices.Length];
        // Compute new vertex positions using the weighted mass positions and stored offsets
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 blendedPos = Vector3.zero;
            // Combine contributions from up to 4 nearest masses
            for (int k = 0; k < 4; k++)
            {
                int mIndex = vertexToMassIndices[i, k];
                float w = vertexWeights[i, k];
                if (mIndex >= 0 && w > 0f)
                {
                    blendedPos += masses[mIndex].position * w;
                }
            }
            // Add the original offset to preserve the mesh's initial shape details
            vertices[i] = blendedPos + vertexOffsets[i];
        }
        // Update mesh vertices and recalc normals for correct lighting
        deformedMesh.vertices = vertices;
        deformedMesh.RecalculateNormals();
        deformedMesh.RecalculateBounds();
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
