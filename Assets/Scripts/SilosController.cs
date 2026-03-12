using UnityEngine;
using System.Collections;

public class SilosController : MonoBehaviour
{
    [Header("Cz¹stki")]
    public GameObject particlePrefab;
    public int particleCount = 200;
    public float spawnRadius = 0.1f;
    public float spawnRate = 0.02f;   

    [Header("Punkty")]
    public Transform spawnPoint;      
    public GameObject silosBottom;     

    private bool isPouring = false;

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!isPouring)
            {
                StartCoroutine(PourParticles());
            }
        }
    }

    IEnumerator PourParticles()
    {
        isPouring = true;

        if (silosBottom != null)
            silosBottom.SetActive(false);


        for (int i = 0; i < particleCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = new Vector3(
                spawnPoint.position.x + randomCircle.x,
                spawnPoint.position.y,
                spawnPoint.position.z + randomCircle.y
            );

            Instantiate(particlePrefab, spawnPos, Quaternion.identity);

            yield return new WaitForSeconds(spawnRate);
        }

        isPouring = false;
    }
}