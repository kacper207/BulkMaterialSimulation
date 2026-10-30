using System.Collections.Generic;
using UnityEngine;

// Cząstka materiału sypkiego: pojedyncza kula albo zlepek kul (clump) z SphereColliderami w obiektach potomnych.
// Rozmiar losowany jako średnica ekwiwalentna d (kula o tej samej objętości), masa = gęstość * objętość.
// shapeVolume i shapeBoundingRadius opisują kształt w skali 1 (dla kuli z prefabu: pi/6 i 0.5),
// dla zlepków wylicza je ClumpPrefabGenerator.
//
// Tarcie toczne (model typu A, Ai i in. 2011):
//   M_r = -mu_r * R_r * F_n * w_rel/|w_rel|,   R_r = R_i*R_j/(R_i+R_j) (dla ściany R_r = R_i)
// działa tylko w kontakcie; R_i, R_j to promienie stykających się kul (dla zlepka - kuli składowej).
// F_n liczone z impulsu kontaktu podanego przez PhysX w OnCollisionStay.
// Moment z kroku k jest przykładany w kroku k+1 i ograniczany tak, żeby nie odwrócił kierunku obrotu.
[RequireComponent(typeof(Rigidbody))]
public class Particle : MonoBehaviour
{
    public static readonly List<Particle> All = new List<Particle>();
    public static int Count => All.Count;

    // Ustawiane przez serię kalibracyjną; NaN / null = wartość z prefabu.
    public static float RollingFrictionOverride = float.NaN;
    public static PhysicsMaterial MaterialOverride;

    [SerializeField] Material[] materials;
    [SerializeField] float minDiameter = 0.05f;
    [SerializeField] float maxDiameter = 0.08f;
    [SerializeField] float density = 2650f;
    [SerializeField] float rollingFriction = 0.1f;

    [Header("Kształt (w skali 1)")]
    [SerializeField] float shapeVolume = Mathf.PI / 6f;
    [SerializeField] float shapeBoundingRadius = 0.5f;

    public Rigidbody Body { get; private set; }
    public float Diameter { get; private set; }
    public float Density => density;
    public float RollingFriction => rollingFriction;
    public float MaxDiameter => maxDiameter;
    public float MaxBoundingRadius => shapeBoundingRadius * ScaleFor(maxDiameter);
    public int SphereCount => spheres.Length;

    SphereCollider[] spheres;
    Vector3 rollingImpulse;

    // Rozmiar i masa ustawiane w Awake (wywoływanym w Instantiate), a nie w Start: Start dla obiektów tworzonych
    // w FixedUpdate wykonuje się dopiero po kroku fizyki, więc cząstka przez ten krok miałaby rozmiar z prefabu.
    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        spheres = GetComponentsInChildren<SphereCollider>();
        if (!float.IsNaN(RollingFrictionOverride)) rollingFriction = RollingFrictionOverride;
        if (MaterialOverride != null)
            foreach (var s in spheres) s.sharedMaterial = MaterialOverride;

        Diameter = Random.Range(minDiameter, maxDiameter);
        transform.localScale = Vector3.one * ScaleFor(Diameter);

        Body.mass = density * Mathf.PI / 6f * Diameter * Diameter * Diameter;
        Body.angularDamping = 0f;

        if (materials != null && materials.Length > 0)
        {
            var material = materials[Random.Range(0, materials.Length)];
            foreach (var r in GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
        }
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Skala, przy której objętość kształtu równa się objętości kuli o średnicy d.
    float ScaleFor(float d) => Mathf.Pow(Mathf.PI / 6f * d * d * d / shapeVolume, 1f / 3f);

    // Środek i promień kuli składowej w układzie świata.
    public void GetSphere(int i, out Vector3 center, out float radius)
    {
        var s = spheres[i];
        center = s.transform.TransformPoint(s.center);
        radius = RadiusOf(s);
    }

    static float RadiusOf(Collider c) =>
        c is SphereCollider s ? s.radius * s.transform.lossyScale.x : 0f;

    void FixedUpdate()
    {
        if (rollingImpulse == Vector3.zero) return;

        // Ograniczenie: impuls nie większy niż moment pędu I*w (w układzie świata - zlepki mają niesferyczny tensor).
        Vector3 w = Body.angularVelocity;
        Quaternion q = Body.rotation * Body.inertiaTensorRotation;
        float max = (q * Vector3.Scale(Body.inertiaTensor, Quaternion.Inverse(q) * w)).magnitude;
        float magnitude = rollingImpulse.magnitude;
        if (magnitude > max) rollingImpulse *= max / magnitude;

        Body.AddTorque(rollingImpulse, ForceMode.Impulse);
        rollingImpulse = Vector3.zero;
    }

    void OnCollisionStay(Collision collision)
    {
        if (rollingFriction <= 0f || Diameter <= 0f || collision.contactCount == 0) return;

        var contact = collision.GetContact(0);
        float radius = RadiusOf(contact.thisCollider);
        if (radius <= 0f) return;

        Vector3 wRel = Body.angularVelocity;
        float effectiveRadius = radius;

        var other = collision.rigidbody;
        if (other != null)
        {
            wRel -= other.angularVelocity;
            float r = RadiusOf(contact.otherCollider);
            if (r > 0f && other.TryGetComponent(out Particle _))
                effectiveRadius = radius * r / (radius + r);
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
