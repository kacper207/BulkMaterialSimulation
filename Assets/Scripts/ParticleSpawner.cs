using UnityEngine;
using System.Collections;

public class ParticleSpawner : MonoBehaviour
{
    [Header("Ustawienia spawnu")]
    public GameObject particlePrefab;
    public Transform spawnPoint;
    public int particleCount = 50;
    public float spawnRadius = 0.1f;   
    public float spawnDelay = 0.05f;  

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartCoroutine(SpawnParticles());
        }
    }

    IEnumerator SpawnParticles()
    {
        for (int i = 0; i < particleCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = new Vector3(
                spawnPoint.position.x + randomCircle.x,
                spawnPoint.position.y,
                spawnPoint.position.z + randomCircle.y
            );

            Instantiate(particlePrefab, spawnPos, Quaternion.identity);

            yield return new WaitForSeconds(spawnDelay);
        }
    }
}