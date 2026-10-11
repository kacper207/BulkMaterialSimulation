using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Particle : MonoBehaviour
{
    public static int Count;

    [SerializeField] Material[] materials;
    [SerializeField] float minDiameter = 0.05f;
    [SerializeField] float maxDiameter = 0.08f;
    [SerializeField] float density = 2650f;
    [SerializeField] float rollingResistance = 0.5f;

    void OnEnable() => Count++;
    void OnDisable() => Count--;

    void Start()
    {
        float d = Random.Range(minDiameter, maxDiameter);
        transform.localScale = Vector3.one * d;

        var rb = GetComponent<Rigidbody>();
        rb.mass = density * Mathf.PI / 6f * d * d * d;
        rb.angularDamping = rollingResistance;

        if (materials != null && materials.Length > 0)
            GetComponent<Renderer>().sharedMaterial = materials[Random.Range(0, materials.Length)];
    }
}