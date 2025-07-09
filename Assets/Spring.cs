// Spring.cs
using UnityEngine;

public class Spring
{
    public Mass massA;
    public Mass massB;
    public float stiffness;  // k_s
    public float damping;    // k_d
    public float restLength;

    public Spring(Mass a, Mass b, float stiff = 5000f, float damp = 50f)
    {
        massA = a;
        massB = b;
        stiffness = stiff;
        damping = damp;
        restLength = Vector3.Distance(a.position, b.position);
    }

    public void ApplyForces()
    {
        Vector3 deltaPos = massB.position - massA.position;
        float currentLength = deltaPos.magnitude;
        if (currentLength == 0) return;

        Vector3 direction = deltaPos / currentLength;

        // Hooke's Law (F = -k * Δx)
        float stretch = currentLength - restLength;
        Vector3 elasticForce = stiffness * stretch * direction;

        // Damping force (F = -k_d * relative velocity)
        Vector3 relVelocity = massB.velocity - massA.velocity;
        float velocityProjection = Vector3.Dot(relVelocity, direction);
        Vector3 dampingForce = damping * velocityProjection * direction;

        Vector3 totalForce = elasticForce + dampingForce;

        massA.ApplyForce(totalForce);
        massB.ApplyForce(-totalForce);
    }
}