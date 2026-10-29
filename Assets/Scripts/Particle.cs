using System.Collections.Generic;
using UnityEngine;

// Cząstka materiału sypkiego. Tarcie toczne (model typu A, Ai i in. 2011):
//   M_r = -mu_r * R_r * F_n * w_rel/|w_rel|,   R_r = R_i*R_j/(R_i+R_j) (dla ściany R_r = R_i)
// działa tylko w kontakcie. F_n liczone z impulsu kontaktu podanego przez PhysX w OnCollisionStay.
// Moment z kroku k jest przykładany w kroku k+1 i ograniczany tak, żeby nie odwrócił kierunku obrotu.
[RequireComponent(typeof(Rigidbody))]
public class Particle : MonoBehaviour
{
    public static readonly List<Particle> All = new List<Particle>();
    public static int Count => All.Count;

    // Ustawiane przez serię kalibracyjną; NaN = wartość z prefabu.
    public static float RollingFrictionOverride = float.NaN;

    [SerializeField] Material[] materials;
    [SerializeField] float minDiameter = 0.05f;
    [SerializeField] float maxDiameter = 0.08f;
    [SerializeField] float density = 2650f;
    [SerializeField] float rollingFriction = 0.1f;

    public Rigidbody Body { get; private set; }
    public float Diameter { get; private set; }
    public float Density => density;
    public float RollingFriction => rollingFriction;
    public float MaxDiameter => maxDiameter;

    Vector3 rollingImpulse;

    // Rozmiar i masa ustawiane w Awake (wywoływanym w Instantiate), a nie w Start: Start dla obiektów tworzonych
    // w FixedUpdate wykonuje się dopiero po kroku fizyki, więc cząstka przez ten krok miałaby rozmiar z prefabu.
    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        if (!float.IsNaN(RollingFrictionOverride)) rollingFriction = RollingFrictionOverride;

        Diameter = Random.Range(minDiameter, maxDiameter);
        transform.localScale = Vector3.one * Diameter;

        Body.mass = density * Mathf.PI / 6f * Diameter * Diameter * Diameter;
        Body.angularDamping = 0f;

        if (materials != null && materials.Length > 0)
            GetComponent<Renderer>().sharedMaterial = materials[Random.Range(0, materials.Length)];
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void FixedUpdate()
    {
        if (rollingImpulse == Vector3.zero) return;

        // Ograniczenie: impuls nie większy niż potrzebny do zatrzymania obrotu w jednym kroku.
        Vector3 w = Body.angularVelocity;
        float max = Body.inertiaTensor.x * w.magnitude;
        float magnitude = rollingImpulse.magnitude;
        if (magnitude > max) rollingImpulse *= max / magnitude;

        Body.AddTorque(rollingImpulse, ForceMode.Impulse);
        rollingImpulse = Vector3.zero;
    }

    void OnCollisionStay(Collision collision)
    {
        if (rollingFriction <= 0f || Diameter <= 0f) return;

        Vector3 wRel = Body.angularVelocity;
        float radius = Diameter / 2f;
        float effectiveRadius = radius;

        var other = collision.rigidbody;
        if (other != null)
        {
            wRel -= other.angularVelocity;
            if (other.TryGetComponent(out Particle p) && p.Diameter > 0f)
            {
                float r = p.Diameter / 2f;
                effectiveRadius = radius * r / (radius + r);
            }
        }

        float w = wRel.magnitude;
        if (w < 1e-4f) return;

        Vector3 normal = Vector3.zero;
        for (int i = 0; i < collision.contactCount; i++) normal += collision.GetContact(i).normal;
        if (normal == Vector3.zero) return;

        // Impuls normalny z ostatniego kroku = F_n * dt, więc moment * dt = mu_r * R_r * impuls.
        float normalImpulse = Mathf.Abs(Vector3.Dot(collision.impulse, normal.normalized));
        rollingImpulse -= wRel / w * (rollingFriction * effectiveRadius * normalImpulse);
    }
}
