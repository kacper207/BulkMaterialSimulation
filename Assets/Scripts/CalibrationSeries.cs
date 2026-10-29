using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Seria kalibracyjna kąta usypu: dla każdej wartości tarcia tocznego mu_r usuwa cząstki, sypie nową pryzmę,
// czeka na jej ustabilizowanie i mierzy kąt. Wszystkie wyniki trafiają do jednego CSV (zapisywanego po każdym przebiegu).
public class CalibrationSeries : MonoBehaviour
{
    [SerializeField] string runName = "calibration";
    [SerializeField] ParticleSpawner spawner;
    [SerializeField] ReposeAngleMeter meter;
    [SerializeField] float[] rollingFrictionValues = { 0f, 0.05f, 0.1f, 0.2f, 0.4f };
    [SerializeField] float maxSettleTime = 60f;
    [SerializeField] bool startOnPlay = true;

    bool running;

    void Start()
    {
        meter.AutoMeasure = false;
        if (startOnPlay) StartCoroutine(Run());
    }

    void Update()
    {
        if (!running && Keyboard.current.cKey.wasPressedThisFrame) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;
        var step = new WaitForFixedUpdate();
        var inv = CultureInfo.InvariantCulture;
        string file = $"{runName}_{ResultsFile.Stamp()}.csv";
        var csv = new StringBuilder();
        csv.AppendLine("mu_r,particles,mean_diameter_m,pile_height_m,pile_radius_m,sectors_fitted,angle_mean_deg,angle_std_deg,angle_pooled_deg,angle_geometric_deg,settle_time_s,settled");

        foreach (float mu in rollingFrictionValues)
        {
            foreach (var p in Particle.All.ToArray()) Destroy(p.gameObject);
            yield return null;

            Debug.Log(string.Format(inv, "Calibration: mu_r = {0:0.###}", mu));
            Particle.RollingFrictionOverride = mu;
            spawner.StartSpawning();
            while (spawner.IsSpawning) yield return step;

            float t0 = Time.fixedTime, settledFor = 0f;
            bool settled = false;
            while (Time.fixedTime - t0 < maxSettleTime)
            {
                yield return step;
                float speed = meter.MeanSpeed(out int count);
                settledFor = count > 0 && speed < meter.SettleSpeed ? settledFor + Time.fixedDeltaTime : 0f;
                if (settledFor >= meter.SettleTime) { settled = true; break; }
            }
            if (!settled) Debug.LogWarning(string.Format(inv, "mu_r = {0:0.###}: pryzma nie ustabilizowała się w {1} s", mu, maxSettleTime));

            var r = meter.Measure(string.Format(inv, "{0}_mu{1:0.000}", runName, mu));
            csv.AppendLine(string.Format(inv, "{0:0.###},{1},{2:F4},{3:F4},{4:F4},{5},{6:F2},{7:F2},{8:F2},{9:F2},{10:F2},{11}",
                mu, r.Particles, r.MeanDiameter, r.Height, r.Radius, r.SectorsFitted,
                r.AngleMean, r.AngleStd, r.AnglePooled, r.AngleGeometric, Time.fixedTime - t0, settled ? 1 : 0));
            ResultsFile.Save(file, csv.ToString());
        }

        Particle.RollingFrictionOverride = float.NaN;
        running = false;
        Debug.Log("Calibration series finished");
    }
}
