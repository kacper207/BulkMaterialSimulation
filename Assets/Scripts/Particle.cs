using UnityEngine;

public class Particle : MonoBehaviour
{
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

        GetComponent<Renderer>().material.color = particleColor;
    }
}