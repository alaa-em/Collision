using UnityEngine;

/// <summary>
/// Data structure for a point mass in the soft body. Stores local position, velocity, mass, damping and accumulated force.
/// </summary>
public class Mass
{
    public Vector3 position;   // Local position of the mass point (relative to object origin)
    public Vector3 velocity;   // Local velocity of the mass point
    public float mass;
    public float damping;
    public Vector3 force;      // Accumulated force on this mass (reset each step)

    public Mass(Vector3 position, float mass, float damping)
    {
        this.position = position;
        this.mass = mass;
        this.damping = damping;
        this.velocity = Vector3.zero;
        this.force = Vector3.zero;
    }

    /// <summary>Adds a force vector to this mass (for force accumulation).</summary>
    public void ApplyForce(Vector3 f)
    {
        force += f;
    }
}
