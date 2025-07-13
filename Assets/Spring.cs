// Spring.cs
using UnityEngine;

public class Spring
{
    public Mass massA;
    public Mass massB;
    public float stiffness;  // k_s
    public float damping;    // k_d
    public float restLength;

    public Spring(Mass a, Mass b, float stiff, float damp)
    {
        massA = a;
        massB = b;
        stiffness = stiff;
        damping = damp;
        restLength = Vector3.Distance(a.position, b.position);
    }

}