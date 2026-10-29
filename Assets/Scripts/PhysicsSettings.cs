using UnityEngine;

public class PhysicsSettings : MonoBehaviour
{
    void Awake()
    {
        Physics.defaultSolverIterations = 20;
        Physics.defaultSolverVelocityIterations = 8;
        Physics.defaultContactOffset = 0.0001f;
        Physics.defaultMaxDepenetrationVelocity = 20f;
        // Domyślne 7 rad/s ograniczało toczenie kul (r ~ 3 cm) do ok. 0,2 m/s; opór toczenia daje teraz model w Particle.
        Physics.defaultMaxAngularSpeed = 100f;
        Time.fixedDeltaTime = 0.01f;


    }


}