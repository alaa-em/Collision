using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Voxelization))]
public class MassSpring : MonoBehaviour
{
    // Physics parameters
    public Vector3 gravity = new Vector3(0, -2.81f, 0);
    public float timeStep = 0.005f;

    // Mass parameters
    public float massValue = 0.5f;
    public float massDamping = 1.5f;

    // Spring parameters
    [Header("Structural Springs (Direct Neighbors)")]
    public float structuralStiffness = 5000f;
    public float structuralDamping = 50f;

    [Header("Shear Springs (Diagonal Neighbors)")]
    public float shearStiffness = 4000f;
    public float shearDamping = 40f;

    [Header("Bending Springs (Same Axis)")]
    public float bendingStiffness = 3000f;
    public float bendingDamping = 30f;


    private List<Mass> masses = new List<Mass>();
    private List<Spring> springs = new List<Spring>();
    private Voxelization voxelizer;

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

    }

    private void BuildSprings()
    {

    }

  
    void OnDrawGizmos()
    {
        if (!voxelizer || !voxelizer.showGizmos) return;

        // Draw masses
        Gizmos.color = Color.red;
        foreach (Mass m in masses)
        {
            Vector3 worldPos = transform.TransformPoint(m.position);
            Gizmos.DrawSphere(worldPos, voxelizer.voxelSize * 0.1f);
        }

        // Draw springs
        foreach (Spring s in springs)
        {       
            
        }
    }
}