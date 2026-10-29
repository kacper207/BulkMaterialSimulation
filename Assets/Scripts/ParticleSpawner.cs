using UnityEngine;
using UnityEngine.InputSystem;

public class ParticleSpawner : MonoBehaviour
{
    [SerializeField] GameObject particlePrefab;
    [SerializeField] Transform spawnPoint;
    [SerializeField] GameObject silosBottom;
    [SerializeField] int particleCount = 1000;
    [SerializeField] float spawnRadius = 0.1f;
    [SerializeField] float particlesPerSecond = 200f;
    [SerializeField] int seed = 12345;
    [SerializeField] bool openBottomOnSpawn = true;

    int spawned;
    float accumulator;
    bool spawning;

    void Update()
    {
        if (!spawning && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Random.InitState(seed);
            spawned = 0;
            accumulator = 0f;
            spawning = true;
            if (openBottomOnSpawn) OpenBottom();
        }

        if (Keyboard.current.oKey.wasPressedThisFrame) OpenBottom();
    }

    void OpenBottom()
    {
        if (silosBottom != null) silosBottom.SetActive(false);
    }

    void FixedUpdate()
    {
        if (!spawning) return;

        accumulator += particlesPerSecond * Time.fixedDeltaTime;
        while (accumulator >= 1f && spawned < particleCount)
        {
            Vector2 c = Random.insideUnitCircle * spawnRadius;
            Instantiate(particlePrefab, spawnPoint.position + new Vector3(c.x, 0f, c.y), Quaternion.identity);
            spawned++;
            accumulator -= 1f;
        }

        if (spawned >= particleCount) spawning = false;
    }
}