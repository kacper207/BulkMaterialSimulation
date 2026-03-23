using UnityEngine;



public class Particle : MonoBehaviour
{
    [Header("Tekstury")]
    public Material[] materials;
    [Header("W³aœciwoœci materia³u")]
    public float mass = 1f;
    public float friction = 0.5f;
    public float cohesion = 0f;
    public Color particleColor = Color.yellow;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = friction;

        float randomX = Random.Range(0.05f, 0.08f);
        float randomY = Random.Range(0.05f, 0.08f);
        float currentZ = transform.localScale.z;
        transform.localScale = new Vector3(randomX, randomY, currentZ);

        if (materials != null && materials.Length > 0)
        {
            int randomIndex = Random.Range(0, materials.Length);
            GetComponent<Renderer>().material = materials[randomIndex];
        }
    }
}