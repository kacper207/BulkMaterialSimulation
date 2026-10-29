using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Particle : MonoBehaviour
{
    public static readonly List<Particle> All = new List<Particle>();
    public static int Count => All.Count;

    [SerializeField] Material[] materials;
    [SerializeField] float minDiameter = 0.05f;
    [SerializeField] float maxDiameter = 0.08f;
    [SerializeField] float density = 2650f;
    [SerializeField] float rollingResistance = 0.5f;

    public Rigidbody Body { get; private set; }
    public float Diameter { get; private set; }
    public float Density => density;

    void Awake() => Body = GetComponent<Rigidbody>();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        Diameter = Random.Range(minDiameter, maxDiameter);
        transform.localScale = Vector3.one * Diameter;

        Body.mass = density * Mathf.PI / 6f * Diameter * Diameter * Diameter;
        Body.angularDamping = rollingResistance;

        if (materials != null && materials.Length > 0)
            GetComponent<Renderer>().sharedMaterial = materials[Random.Range(0, materials.Length)];
    }
}
