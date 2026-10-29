using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Mierzy masowy wydatek wypływu przez otwór silosa i porównuje go z równaniem Beverloo:
//   W = C * rho_b * sqrt(g) * (D - k*d)^2.5
// Obiekt należy umieścić na wysokości otworu. Cząstka jest liczona raz, gdy zejdzie poniżej tej płaszczyzny.
// Czas liczony jest w czasie symulacji (Time.fixedTime), a nie rzeczywistym.
public class FlowRateMeter : MonoBehaviour
{
    [SerializeField] string runName = "flow";

    [Header("Otwór")]
    [SerializeField] float orificeDiameter = 0.5f;

    [Header("Beverloo")]
    [SerializeField] float beverlooC = 0.58f;
    [SerializeField] float beverlooK = 1.5f;
    [SerializeField] float packingFraction = 0.6f;

    [Header("Pomiar")]
    [SerializeField] float sampleInterval = 0.1f;
    [SerializeField] float idleTimeout = 2f;
    [SerializeField, Range(0f, 0.5f)] float steadyTrim = 0.2f;

    readonly HashSet<Particle> passed = new HashSet<Particle>();
    readonly StringBuilder series = new StringBuilder();
    readonly List<Vector2> samples = new List<Vector2>();

    float startTime = -1f;
    float lastCrossing;
    float nextSample;
    float totalMass;
    double diameterSum;
    double densitySum;
    bool finished;

    void FixedUpdate()
    {
        if (finished) return;

        float planeY = transform.position.y;
        float t = Time.fixedTime;

        foreach (var p in Particle.All)
        {
            if (p.Diameter <= 0f || p.transform.position.y >= planeY || !passed.Add(p)) continue;

            if (startTime < 0f) BeginSeries(t);
            totalMass += p.Body.mass;
            diameterSum += p.Diameter;
            densitySum += p.Density;
            lastCrossing = t;
        }

        if (startTime < 0f) return;

        if (t >= nextSample)
        {
            float ts = t - startTime;
            samples.Add(new Vector2(ts, totalMass));
            series.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F3},{1},{2:F4}", ts, passed.Count, totalMass));
            nextSample += sampleInterval;
        }

        if (t - lastCrossing > idleTimeout) Finish();
    }

    void Update()
    {
        if (!finished && startTime >= 0f && Keyboard.current.fKey.wasPressedThisFrame) Finish();
    }

    void BeginSeries(float t)
    {
        startTime = t;
        nextSample = t;
        series.AppendLine("time_s,particles,mass_kg");
        Debug.Log("Flow measurement start");
    }

    void Finish()
    {
        finished = true;
        int n = passed.Count;
        if (n == 0) return;

        float d = (float)(diameterSum / n);
        float rhoP = (float)(densitySum / n);
        float rhoB = rhoP * packingFraction;
        float g = -Physics.gravity.y;
        float effective = Mathf.Max(orificeDiameter - beverlooK * d, 0f);
        float beverloo = beverlooC * rhoB * Mathf.Sqrt(g) * Mathf.Pow(effective, 2.5f);

        float measured = SteadyFlowRate(out float tFrom, out float tTo);
        float error = beverloo > 0f ? (measured - beverloo) / beverloo * 100f : float.NaN;
        float duration = lastCrossing - startTime;

        var summary = new StringBuilder();
        summary.AppendLine("particles,total_mass_kg,duration_s,mean_diameter_m,orifice_D_m,D_over_d,particle_density,bulk_density,steady_from_s,steady_to_s,measured_kg_s,beverloo_kg_s,error_pct");
        summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "{0},{1:F4},{2:F3},{3:F4},{4:F4},{5:F2},{6:F1},{7:F1},{8:F3},{9:F3},{10:F4},{11:F4},{12:F2}",
            n, totalMass, duration, d, orificeDiameter, orificeDiameter / d, rhoP, rhoB, tFrom, tTo, measured, beverloo, error));

        string stamp = ResultsFile.Stamp();
        ResultsFile.Save($"{runName}_{stamp}_series.csv", series.ToString());
        ResultsFile.Save($"{runName}_{stamp}_summary.csv", summary.ToString());

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "Flow: measured {0:F3} kg/s, Beverloo {1:F3} kg/s ({2:+0.0;-0.0}%), D/d = {3:F1}",
            measured, beverloo, error, orificeDiameter / d));
        if (orificeDiameter / d < 6f)
            Debug.LogWarning("D/d < 6: wypływ może się zakleszczać, Beverloo traci ważność.");
    }

    // Regresja liniowa masy skumulowanej po czasie w fazie ustalonej (bez początku i końca wypływu).
    float SteadyFlowRate(out float tFrom, out float tTo)
    {
        float lo = totalMass * steadyTrim, hi = totalMass * (1f - steadyTrim);
        double st = 0, sm = 0, stt = 0, stm = 0;
        int k = 0;
        tFrom = float.NaN; tTo = float.NaN;

        foreach (var s in samples)
        {
            if (s.y < lo || s.y > hi) continue;
            if (k == 0) tFrom = s.x;
            tTo = s.x;
            st += s.x; sm += s.y; stt += s.x * s.x; stm += s.x * s.y;
            k++;
        }

        if (k < 2) return totalMass / Mathf.Max(lastCrossing - startTime, 1e-6f);
        double denom = k * stt - st * st;
        return denom > 0 ? (float)((k * stm - st * sm) / denom) : float.NaN;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 c = transform.position;
        float r = orificeDiameter / 2f;
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r,
                            c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r);
        }
    }
}
