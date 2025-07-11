using UnityEngine;

/// <summary>
/// Defines physical material properties for an object (soft or rigid).
/// Includes material type and parameters like mass, restitution (bounciness), stiffness and hardness for deformation.
/// </summary>
public class PhysicsBody : MonoBehaviour
{
    public enum MaterialType { Jelly, Rubber, Metal }

    [Header("Material Settings")]
    public MaterialType type = MaterialType.Jelly;
    public float mass = 1.0f;
    [Range(0f, 1f)] public float restitution = 0.5f;  // Bounciness [0,1]

    [Header("Soft Body Properties")]
    public float stiffness = 5000f;  // Spring stiffness for soft materials
    public float hardness = 0.5f;    // Hardness (resistance to deformation). 1 = very hard (rigid), 0 = very soft.

    // You can extend this class to initialize MassSpring parameters based on material type if needed.
}
