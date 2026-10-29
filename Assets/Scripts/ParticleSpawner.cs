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
    [SerializeField] bool spawnOnKey = true;

    [Header("Wolne miejsce")]
    [SerializeField] int placementAttempts = 10;
    [SerializeField] float maxBlockedTime = 3f;

    int spawned;
    float accumulator;
    float blockedTime;
    float clearance;
    bool spawning;

    void Update()
    {
        if (spawnOnKey && !spawning && Keyboard.current.eKey.wasPressedThisFrame) StartSpawning();

        if (Keyboard.current.oKey.wasPressedThisFrame) OpenBottom();
    }

    public bool IsSpawning => spawning;

    public void StartSpawning()
    {
        Random.InitState(seed);
        spawned = 0;
        accumulator = 0f;
        blockedTime = 0f;
        clearance = particlePrefab.GetComponent<Particle>().MaxDiameter / 2f;
        spawning = true;
        if (openBottomOnSpawn) OpenBottom();
    }

    void OpenBottom()
    {
        if (silosBottom != null) silosBottom.SetActive(false);
    }

    void FixedUpdate()
    {
        if (!spawning) return;

        accumulator += particlesPerSecond * Time.fixedDeltaTime;
        bool blocked = false;
        while (accumulator >= 1f && spawned < particleCount)
        {
            if (!TryFindFreeSpot(out Vector3 position)) { blocked = true; break; }

            Instantiate(particlePrefab, position, Quaternion.identity);
            Physics.SyncTransforms();
            spawned++;
            accumulator -= 1f;
        }

        // Brak miejsca (np. pełny lejek): czekamy, a po maxBlockedTime kończymy sypanie.
        if (blocked)
        {
            accumulator = Mathf.Min(accumulator, 1f);
            blockedTime += Time.fixedDeltaTime;
            if (blockedTime >= maxBlockedTime)
            {
                Debug.LogWarning($"Spawner: brak wolnego miejsca przez {maxBlockedTime} s, koniec po {spawned}/{particleCount} cząstkach.");
                spawning = false;
            }
        }
        else blockedTime = 0f;

        if (spawned >= particleCount) spawning = false;
    }

    // Losuje punkt w okręgu spawnu, w którym nowa cząstka nie nachodzi na żaden collider.
    bool TryFindFreeSpot(out Vector3 position)
    {
        for (int i = 0; i < placementAttempts; i++)
        {
            Vector2 c = Random.insideUnitCircle * spawnRadius;
            position = spawnPoint.position + new Vector3(c.x, 0f, c.y);
            if (!Physics.CheckSphere(position, clearance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;
        }
        position = default;
        return false;
    }
}