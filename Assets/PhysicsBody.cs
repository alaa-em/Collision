using UnityEngine;

/// <summary>
/// Simple physics body: handles mass, forces and gravity manually.
/// Attach this to any GameObject to give it physics-like movement.
/// </summary>
public class PhysicsBody : MonoBehaviour
{
    [Header("Physical Properties")]
    [Tooltip("Object's mass (kg). Must be > 0.")]
    public float mass = 1f;


    [Tooltip("Drag coefficient to simulate air resistance.")]
    [Range(0f, 10f)]
    public float drag = 0.1f;

    [Header("Runtime State (read-only)")]
    [SerializeField, Tooltip("Current velocity (m/s)")]
    private Vector3 velocity = Vector3.zero;

    [SerializeField, Tooltip("Accumulated acceleration (m/s²) this frame")]
    private Vector3 acceleration = Vector3.zero;

   
    public void AddForce(Vector3 force)
    {
       
        acceleration += force / mass;
    }

    
    public void AddImpulse(Vector3 impulse)
    {
        velocity += impulse / mass;
    }

    private void FixedUpdate()
    {

        


        velocity += acceleration * Time.fixedDeltaTime;

        velocity *= (1f / (1f + drag * Time.fixedDeltaTime));


        transform.position += velocity * Time.fixedDeltaTime;

        
        acceleration = Vector3.zero;
    }


    public Vector3 Velocity => velocity;
}
