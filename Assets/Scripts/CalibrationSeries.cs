using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Seria kalibracyjna kąta usypu: dla każdego przebiegu usuwa cząstki, sypie nową pryzmę,
// czeka na jej ustabilizowanie i mierzy kąt. Wszystkie wyniki serii trafiają do jednego CSV (zapisywanego po każdym przebiegu).
//   RollingFriction - zmienia tarcie toczne mu_r przy tarciu ślizgowym z materiału cząstek,
//   SlidingFriction - zmienia tarcie ślizgowe (static = dynamic) cząstek i podłogi przy stałym mu_r,
//   Shape           - zmienia kształt cząstek (prefab: kula, zlepki) przy stałym mu_r i tarciu.
// Tarcie ślizgowe zmieniane jest na kopii materiału z prefabu, więc plik materiału pozostaje bez zmian.
// Metoda usypywania wynika ze sceny: bez cylindra - lejek (funnel), z przypiętym LiftCylinder - test podnoszonego
// cylindra (cylinder): napełnienie, ustabilizowanie, podniesienie cylindra, ponowne ustabilizowanie, pomiar.
public class CalibrationSeries : MonoBehaviour
{
    public enum SeriesType { RollingFriction, SlidingFriction, Shape }

    [SerializeField] string runName = "calibration";
    [SerializeField] ParticleSpawner spawner;
    [SerializeField] ReposeAngleMeter meter;
    [SerializeField] SeriesType series = SeriesType.RollingFriction;
    [SerializeField] float maxSettleTime = 60f;
    [SerializeField] bool startOnPlay = true;
    [SerializeField] Collider[] frictionSurfaces;
    [SerializeField] LiftCylinder cylinder;

    [Header("Seria mu_r")]
    [SerializeField] float[] rollingFrictionValues = { 0f, 0.05f, 0.1f, 0.2f, 0.4f };

    [Header("Seria tarcia ślizgowego")]
    [SerializeField] float[] slidingFrictionValues = { 0.5f, 0.8f, 1.0f, 1.5f };
    [SerializeField] float fixedRollingFriction = 0.2f;

    [Header("Seria kształtu")]
    [SerializeField] GameObject[] shapePrefabs;
    [SerializeField] float shapeRollingFriction = 0.2f;
    [SerializeField] float shapeFriction = 1.0f;

    PhysicsMaterial baseMaterial;
    PhysicsMaterial[] surfaceMaterials;
    GameObject basePrefab;
    bool running;
    bool lastSettled;
    float lastSettleTime;

    void Start()
    {
        meter.AutoMeasure = false;
        basePrefab = spawner.ParticlePrefab;
        baseMaterial = basePrefab.GetComponentInChildren<Collider>().sharedMaterial;
        surfaceMaterials = new PhysicsMaterial[frictionSurfaces.Length];
        for (int i = 0; i < frictionSurfaces.Length; i++) surfaceMaterials[i] = frictionSurfaces[i].sharedMaterial;

        if (startOnPlay) StartCoroutine(Run());
    }

    void Update()
    {
        if (!running && Keyboard.current.cKey.wasPressedThisFrame) StartCoroutine(Run());
    }

    void OnDisable() => RestoreDefaults();

    IEnumerator Run()
    {
        running = true;
        string label = series switch
        {
            SeriesType.SlidingFriction => "friction",
            SeriesType.Shape => "shape",
            _ => "mu",
        };
        int runs = series switch
        {
            SeriesType.SlidingFriction => slidingFrictionValues.Length,
            SeriesType.Shape => shapePrefabs.Length,
            _ => rollingFrictionValues.Length,
        };

        var step = new WaitForFixedUpdate();
        var inv = CultureInfo.InvariantCulture;
        string file = $"{runName}_{label}_{ResultsFile.Stamp()}.csv";
        var csv = new StringBuilder();
        string method = cylinder != null ? "cylinder" : "funnel";
        csv.AppendLine("method,shape,mu_r,friction,contact_offset,max_depenetration,particles,mean_diameter_m,pile_height_m,pile_radius_m,sectors_fitted,angle_mean_deg,angle_std_deg,angle_pooled_deg,angle_geometric_deg,settle_time_s,settled");

        for (int i = 0; i < runs; i++)
        {
            foreach (var p in Particle.All.ToArray()) Destroy(p.gameObject);
            yield return null;
            if (cylinder != null) cylinder.ResetDown();

            RestoreDefaults();
            float mu, friction = baseMaterial.staticFriction;
            string runLabel;
            switch (series)
            {
                case SeriesType.SlidingFriction:
                    mu = fixedRollingFriction;
                    friction = slidingFrictionValues[i];
                    ApplySlidingFriction(friction);
                    runLabel = string.Format(inv, "friction{0:0.000}", friction);
                    break;
                case SeriesType.Shape:
                    mu = shapeRollingFriction;
                    friction = shapeFriction;
                    ApplySlidingFriction(friction);
                    spawner.ParticlePrefab = shapePrefabs[i];
                    runLabel = $"shape_{shapePrefabs[i].name}";
                    break;
                default:
                    mu = rollingFrictionValues[i];
                    runLabel = string.Format(inv, "mu{0:0.000}", mu);
                    break;
            }
            Particle.RollingFrictionOverride = mu;
            string shape = spawner.ParticlePrefab.name;

            Debug.Log(string.Format(inv, "Calibration: shape = {0}, mu_r = {1:0.###}, friction = {2:0.###}", shape, mu, friction));
            spawner.StartSpawning();
            while (spawner.IsSpawning) yield return step;

            yield return WaitForSettle();
            bool settled = lastSettled;
            if (cylinder != null)
            {
                if (!settled) Debug.LogWarning($"{runLabel}: materiał w cylindrze nie ustabilizował się przed podniesieniem");
                yield return cylinder.Lift();
                yield return WaitForSettle();
                settled &= lastSettled;
            }
            if (!settled) Debug.LogWarning($"{runLabel}: pryzma nie ustabilizowała się w {maxSettleTime} s");

            var r = meter.Measure($"{runName}_{runLabel}");
            csv.AppendLine(string.Format(inv, "{0},{1},{2:0.###},{3:0.###},{4:0.#####},{5:0.###},{6},{7:F4},{8:F4},{9:F4},{10},{11:F2},{12:F2},{13:F2},{14:F2},{15:F2},{16}",
                method, shape, mu, friction, Physics.defaultContactOffset, Physics.defaultMaxDepenetrationVelocity, r.Particles, r.MeanDiameter, r.Height, r.Radius, r.SectorsFitted,
                r.AngleMean, r.AngleStd, r.AnglePooled, r.AngleGeometric, lastSettleTime, settled ? 1 : 0));
            ResultsFile.Save(file, csv.ToString());
        }

        RestoreDefaults();
        running = false;
        Debug.Log("Calibration series finished");
    }

    // Czeka, aż średnia prędkość cząstek w obszarze pomiaru spadnie poniżej progu (najdłużej maxSettleTime).
    IEnumerator WaitForSettle()
    {
        var step = new WaitForFixedUpdate();
        float t0 = Time.fixedTime, settledFor = 0f;
        lastSettled = false;
        while (Time.fixedTime - t0 < maxSettleTime)
        {
            yield return step;
            float speed = meter.MeanSpeed(out int count);
            settledFor = count > 0 && speed < meter.SettleSpeed ? settledFor + Time.fixedDeltaTime : 0f;
            if (settledFor >= meter.SettleTime) { lastSettled = true; break; }
        }
        lastSettleTime = Time.fixedTime - t0;
    }

    // Kopia materiału cząstek z nowym tarciem, przypisana nowym cząstkom i powierzchniom (podłodze).
    void ApplySlidingFriction(float friction)
    {
        var material = Instantiate(baseMaterial);
        material.name = $"{baseMaterial.name}_f{friction:0.00}";
        material.staticFriction = friction;
        material.dynamicFriction = friction;

        if (!Mathf.Approximately(material.staticFriction, friction) || !Mathf.Approximately(material.dynamicFriction, friction))
            Debug.LogWarning($"Tarcie {friction} zostało zmienione przez silnik na static = {material.staticFriction}, dynamic = {material.dynamicFriction}");

        Particle.MaterialOverride = material;
        foreach (var surface in frictionSurfaces) surface.sharedMaterial = material;
    }

    void RestoreDefaults()
    {
        if (Particle.MaterialOverride != null) Destroy(Particle.MaterialOverride);
        Particle.MaterialOverride = null;
        Particle.RollingFrictionOverride = float.NaN;
        if (basePrefab != null) spawner.ParticlePrefab = basePrefab;

        if (surfaceMaterials == null) return;
        for (int i = 0; i < frictionSurfaces.Length; i++)
            if (frictionSurfaces[i] != null) frictionSurfaces[i].sharedMaterial = surfaceMaterials[i];
    }
}
