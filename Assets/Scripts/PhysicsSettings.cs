using UnityEngine;

public class PhysicsSettings : MonoBehaviour
{
    // Kontakt powstaje, gdy kształty są bliżej niż contactOffset. Zbyt mała wartość (wcześniej 0.0001) sprawia,
    // że cząstki wchodzą w siebie przed zderzeniem, a depenetracja (do maxDepenetrationVelocity) dodaje energii pryzmie.
    // Wartość musi być wyraźnie mniejsza od promienia najmniejszej kuli (kule zlepków ok. 2 cm).
    [SerializeField] float contactOffset = 0.005f;
    [SerializeField] float maxDepenetrationVelocity = 1f;

    void Awake()
    {
        Physics.defaultSolverIterations = 20;
        Physics.defaultSolverVelocityIterations = 8;
        Physics.defaultContactOffset = contactOffset;
        // Domyślna wartość działa tylko na nowe collidery - istniejące w scenie (podłoga, lejek, silos) ustawiamy ręcznie.
        foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None)) c.contactOffset = contactOffset;
        Physics.defaultMaxDepenetrationVelocity = maxDepenetrationVelocity;
        // Domyślne 7 rad/s ograniczało toczenie kul (r ~ 3 cm) do ok. 0,2 m/s; opór toczenia daje teraz model w Particle.
        Physics.defaultMaxAngularSpeed = 100f;
        Time.fixedDeltaTime = 0.01f;
    }
}
