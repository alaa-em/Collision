// MassSpring.cs
using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
[RequireComponent(typeof(Voxelization))]
public class MassSpring : MonoBehaviour
{
    // Physics parameters
    public Vector3 gravity = new Vector3(0, -1.81f, 0);
    public float timeStep = 0.005f;
    public int solverIterations = 10;

    // Mass parameters
    public float massValue = 0.5f;
    public float massDamping = 1.5f;

    // Spring parameters
    public float springStiffness = 5000f;
    public float springDamping = 50f;
    public float connectionThreshold = 1.1f;

    // Collision
    public bool enableGroundCollision = true;
    public float groundHeight = 0f;
    public float collisionRestitution = 0.7f;

    private List<Mass> masses = new List<Mass>();
    private List<Spring> springs = new List<Spring>();
    private Voxelization voxelizer;
    private float accumulatedTime = 0f;

    void Start()
    {
        voxelizer = GetComponent<Voxelization>();
        BuildSystem();
    }

    [ContextMenu("Rebuild System")]
    public void BuildSystem()
    {
        BuildMasses();
        BuildSprings();
    }

    void BuildMasses()
    {
        masses.Clear();
        if (voxelizer.InsideVoxels1 == null) return;

        foreach (var voxel in voxelizer.InsideVoxels1)
        {
            if (voxel == null) continue;
            Vector3 localPos = transform.InverseTransformPoint(voxel.WorldPosition);
            masses.Add(new Mass(localPos, massValue, massDamping));
        }
    }

    private void BuildSprings()
    {
        if (masses.Count == 0) return;

        float voxelSize = voxelizer.voxelSize;
        float threshold = voxelSize * connectionThreshold;
        float sqrThreshold = threshold * threshold;

        Dictionary<Vector3Int, List<int>> grid = new Dictionary<Vector3Int, List<int>>();
        float gridSize = threshold;

        for (int i = 0; i < masses.Count; i++)
        {
            Vector3Int cell = new Vector3Int(
                Mathf.FloorToInt(masses[i].position.x / gridSize),
                Mathf.FloorToInt(masses[i].position.y / gridSize),
                Mathf.FloorToInt(masses[i].position.z / gridSize)
            );

            if (!grid.ContainsKey(cell)) grid[cell] = new List<int>();
            grid[cell].Add(i);
        }

        for (int i = 0; i < masses.Count; i++)
        {
            Vector3Int cell = new Vector3Int(
                Mathf.FloorToInt(masses[i].position.x / gridSize),
                Mathf.FloorToInt(masses[i].position.y / gridSize),
                Mathf.FloorToInt(masses[i].position.z / gridSize)
            );

            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int neighborCell = cell + new Vector3Int(x, y, z);
                        if (!grid.ContainsKey(neighborCell)) continue;

                        foreach (int j in grid[neighborCell])
                        {
                            if (j <= i) continue;

                            float sqrDist = Vector3.SqrMagnitude(
                                masses[i].position - masses[j].position);

                            if (sqrDist <= sqrThreshold)
                            {
                                // Structural spring
                                springs.Add(new Spring(
                                    masses[i],
                                    masses[j],
                                    springStiffness,
                                    springDamping
                                ));
                            }
                            else if (sqrDist <= (sqrThreshold * 2f))
                            {
                                // Shear spring (example second type)
                                springs.Add(new Spring(
                                    masses[i],
                                    masses[j],
                                    springStiffness,
                                    springDamping
                                ));
                            }
                        }
                    }
        }

        Debug.Log(springs.Count);
    }

    void FixedUpdate()
    {
        // Use fixed timestep
        accumulatedTime += Time.fixedDeltaTime;

        while (accumulatedTime >= timeStep)
        {
            SimulateStep(timeStep);
            accumulatedTime -= timeStep;
        }
    }

    void SimulateStep(float dt)
    {
        // Apply external forces
        foreach (Mass mass in masses)
        {
            // Reset forces
            mass.force = Vector3.zero;

            // Apply gravity
            mass.ApplyForce(mass.mass * gravity);

            // Apply damping (air resistance)
            mass.ApplyForce(-mass.damping * mass.velocity);
        }

        // Apply spring forces
        foreach (Spring spring in springs)
        {
            spring.ApplyForces();
        }

        // Integrate - update velocities and positions
        foreach (Mass mass in masses)
        {
            Vector3 acceleration = mass.force / mass.mass;
            mass.velocity += acceleration * dt;
            mass.position += mass.velocity * dt;
        }

        // Handle constraints and collisions
        for (int i = 0; i < solverIterations; i++)
        {
            foreach (Spring spring in springs)
            {
                ApplySpringConstraint(spring);
            }

            if (enableGroundCollision)
            {
                foreach (Mass mass in masses)
                {
                    HandleGroundCollision(mass);
                }
            }
        }
    }

    void ApplySpringConstraint(Spring spring)
    {
        Vector3 delta = spring.massB.position - spring.massA.position;
        float currentLength = delta.magnitude;
        if (currentLength == 0) return;

        float displacement = currentLength - spring.restLength;
        Vector3 correction = delta * (displacement / currentLength) * 0.5f;

        spring.massA.position += correction;
        spring.massB.position -= correction;
    }

    void HandleGroundCollision(Mass mass)
    {
        Vector3 worldPos = transform.TransformPoint(mass.position);

        if (worldPos.y < groundHeight)
        {
            // Position correction
            worldPos.y = groundHeight;
            mass.position = transform.InverseTransformPoint(worldPos);

            // Velocity reflection with damping
            Vector3 worldVel = transform.TransformVector(mass.velocity);
            worldVel.y = -worldVel.y * collisionRestitution;
            mass.velocity = transform.InverseTransformVector(worldVel);
        }
    }

    void OnDrawGizmos()
    {
        //if (!voxelizer || !voxelizer.showGizmos) return;

        // Draw masses
        Gizmos.color = Color.red;
        foreach (Mass m in masses)
        {
            Vector3 worldPos = transform.TransformPoint(m.position);
            Gizmos.DrawSphere(worldPos, voxelizer.voxelSize * 0.1f);
        }

        // Draw springs
        Gizmos.color = Color.blue;
        foreach (Spring s in springs)
        {
            Vector3 pWorld = transform.TransformPoint(s.massA.position);
            Vector3 qWorld = transform.TransformPoint(s.massB.position);
            Gizmos.DrawLine(pWorld, qWorld);
        }
    }
}