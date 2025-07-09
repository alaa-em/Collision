// Mass.cs
using UnityEngine;

public class Mass
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 force;
    public float mass;
    public float damping; // Air resistance coefficient

    public Mass(Vector3 pos, float m = 0.5f, float damp = 1.5f)
    {
        position = pos;
        velocity = Vector3.zero;
        force = Vector3.zero;
        mass = m;
        damping = damp;
    }

    public void ApplyForce(Vector3 f)
    {
        force += f;
    }
}