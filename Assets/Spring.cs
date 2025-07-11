using UnityEngine;

/// <summary>
/// Represents a spring connecting two mass points in the mass-spring system. 
/// Stores the connected masses, rest length, stiffness, and damping, and can apply forces to the masses.
/// </summary>
public class Spring
{
    public Mass massA;
    public Mass massB;
    public float restLength;
    public float stiffness;
    public float damping;

    public Spring(Mass a, Mass b, float stiffness, float damping)
    {
        this.massA = a;
        this.massB = b;
        this.stiffness = stiffness;
        this.damping = damping;
        // Rest length is the initial distance between the two mass points
        this.restLength = (b.position - a.position).magnitude;
    }

    /// <summary>Applies spring force and damping force to the connected masses.</summary>
    public void ApplyForces()
    {
        // Current vector from A to B
        Vector3 delta = massB.position - massA.position;
        float dist = delta.magnitude;
        if (dist <= 1e-6f) return;
        Vector3 dir = delta / dist;
        // Hooke's law: F_spring = -k * (currentLength - restLength)
        float springForceMag = stiffness * (dist - restLength);
        // Damping force along the spring (relative velocity projected on spring direction)
        Vector3 relVel = massB.velocity - massA.velocity;
        float dampForceMag = damping * Vector3.Dot(relVel, dir);
        float forceMag = springForceMag + dampForceMag;
        Vector3 force = forceMag * dir;
        // Apply equal and opposite forces to masses
        massA.ApplyForce(force);
        massB.ApplyForce(-force);
    }
}
