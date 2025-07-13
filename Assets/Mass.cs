// Mass.cs
using System.Collections.Generic;
using UnityEngine;

public class Mass
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 force;
    public float mass;
    public float damping;
    public List<Mass> neighbors = new List<Mass>();

    public Mass(Vector3 pos, float m, float damp)
    {
        position = pos;
        mass = m;
        damping = damp;
        velocity = Vector3.zero;
        force = Vector3.zero;
    }


}
