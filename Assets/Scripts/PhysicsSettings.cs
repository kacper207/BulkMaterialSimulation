using UnityEngine;

public class PhysicsSettings : MonoBehaviour
{
    void Awake()
    {
        Physics.defaultSolverIterations = 20;
        Physics.defaultSolverVelocityIterations = 8;
        Physics.defaultContactOffset = 0.0001f;
        Physics.defaultMaxDepenetrationVelocity = 20f;
        Time.fixedDeltaTime = 0.01f;


    }


}