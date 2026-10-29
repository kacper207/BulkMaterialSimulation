using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Mierzy kąt naturalnego usypu pryzmy w obszarze (prostopadłościan wokół obiektu, obrócony razem z nim).
// Wszystkie obliczenia są w układzie lokalnym obiektu: oś pryzmy przechodzi przez jego środek, wysokość wzdłuż lokalnej osi Y.
// Obiekt powinien mieć skalę 1 - rozmiar obszaru i średnice cząstek są wtedy w tych samych jednostkach.
// Profil powierzchni: dla każdego sektora kątowego i pierścienia promieniowego bierzemy najwyższy punkt cząstki,
// a kąt wyznaczamy z regresji liniowej h(r) w środkowej części zbocza (bez wierzchołka i podnóża).
// Pomiar uruchamia się sam, gdy pryzma się ustabilizuje, albo ręcznie klawiszem R.
public class ReposeAngleMeter : MonoBehaviour
{
    [SerializeField] string runName = "repose";

    [Header("Obszar")]
    [SerializeField] Vector3 regionSize = new Vector3(2f, 2f, 2f);

    [Header("Profil")]
    [SerializeField] int sectors = 8;
    [SerializeField] int radialBins = 20;
    [SerializeField, Range(0f, 1f)] float fitFrom = 0.2f;
    [SerializeField, Range(0f, 1f)] float fitTo = 0.8f;

    [Header("Stabilizacja")]
    [SerializeField] bool autoMeasure = true;
    [SerializeField] int minParticles = 200;
    [SerializeField] float settleSpeed = 0.01f;
    [SerializeField] float settleTime = 1f;

    public struct Result
    {
        public int Particles;
        public float MeanDiameter, Height, Radius;
        public int SectorsFitted;
        public float AngleMean, AngleStd, AnglePooled, AngleGeometric;
    }

    readonly List<Particle> inside = new List<Particle>();
    readonly List<Vector3> localPositions = new List<Vector3>();
    float settledFor;
    bool measured;

    public bool AutoMeasure { get => autoMeasure; set => autoMeasure = value; }
    public float SettleSpeed => settleSpeed;
    public float SettleTime => settleTime;

    void FixedUpdate()
    {
        if (!autoMeasure || measured) return;

        float speed = MeanSpeed(out int count);
        if (count < minParticles) { settledFor = 0f; return; }

        settledFor = speed < settleSpeed ? settledFor + Time.fixedDeltaTime : 0f;
        if (settledFor >= settleTime)
        {
            measured = true;
            Measure(runName);
        }
    }

    void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame) Measure(runName);
    }

    // Średnia prędkość cząstek w obszarze.
    public float MeanSpeed(out int count)
    {
        Collect();
        count = inside.Count;
        if (count == 0) return 0f;

        float speed = 0f;
        foreach (var p in inside) speed += p.Body.linearVelocity.magnitude;
        return speed / count;
    }

    void Collect()
    {
        inside.Clear();
        localPositions.Clear();
        var bounds = new Bounds(Vector3.zero, regionSize);
        foreach (var p in Particle.All)
        {
            if (p.Diameter <= 0f) continue;
            Vector3 local = transform.InverseTransformPoint(p.transform.position);
            if (!bounds.Contains(local)) continue;
            inside.Add(p);
            localPositions.Add(local);
        }
    }

    // Mierzy kąt usypu i zapisuje profil oraz podsumowanie do plików z prefiksem name.
    public Result Measure(string name)
    {
        Collect();
        if (inside.Count < 3)
        {
            Debug.LogWarning("Repose: za mało cząstek w obszarze.");
            return new Result { Particles = inside.Count, AngleMean = float.NaN, AngleStd = float.NaN, AnglePooled = float.NaN, AngleGeometric = float.NaN };
        }

        // Oś pryzmy = środek obiektu (lokalne XZ = 0), podstawa = najniższy punkt.
        float baseY = float.MaxValue, meanD = 0f;
        for (int i = 0; i < inside.Count; i++)
        {
            baseY = Mathf.Min(baseY, localPositions[i].y - inside[i].Diameter / 2f);
            meanD += inside[i].Diameter;
        }
        meanD /= inside.Count;

        float maxR = 0f, maxH = 0f;
        for (int i = 0; i < inside.Count; i++)
        {
            Vector3 pos = localPositions[i];
            maxR = Mathf.Max(maxR, new Vector2(pos.x, pos.z).magnitude);
            maxH = Mathf.Max(maxH, pos.y + inside[i].Diameter / 2f - baseY);
        }

        float binWidth = maxR / radialBins;
        var surface = new float[sectors, radialBins];
        for (int s = 0; s < sectors; s++)
            for (int b = 0; b < radialBins; b++)
                surface[s, b] = float.NaN;

        for (int i = 0; i < inside.Count; i++)
        {
            Vector3 pos = localPositions[i];
            Vector2 rel = new Vector2(pos.x, pos.z);
            int b = Mathf.Min((int)(rel.magnitude / binWidth), radialBins - 1);
            float angle = Mathf.Atan2(rel.y, rel.x) + Mathf.PI;
            int s = Mathf.Min((int)(angle / (2f * Mathf.PI) * sectors), sectors - 1);
            float h = pos.y + inside[i].Diameter / 2f - baseY;
            if (float.IsNaN(surface[s, b]) || h > surface[s, b]) surface[s, b] = h;
        }

        var profile = new StringBuilder("sector,r_m,h_m\n");
        var angles = new List<float>();
        double ar = 0, ah = 0, arr = 0, arh = 0;
        int an = 0;

        for (int s = 0; s < sectors; s++)
        {
            double sr = 0, sh = 0, srr = 0, srh = 0;
            int n = 0;
            for (int b = 0; b < radialBins; b++)
            {
                float h = surface[s, b];
                if (float.IsNaN(h)) continue;
                float r = (b + 0.5f) * binWidth;
                profile.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},{2:F4}", s, r, h));

                if (r < fitFrom * maxR || r > fitTo * maxR) continue;
                sr += r; sh += h; srr += r * r; srh += r * h; n++;
                ar += r; ah += h; arr += r * r; arh += r * h; an++;
            }

            float slope = Slope(sr, sh, srr, srh, n);
            if (!float.IsNaN(slope)) angles.Add(Mathf.Atan(-slope) * Mathf.Rad2Deg);
        }

        float mean = 0f, std = 0f;
        foreach (var a in angles) mean += a;
        mean /= Mathf.Max(angles.Count, 1);
        foreach (var a in angles) std += (a - mean) * (a - mean);
        std = angles.Count > 1 ? Mathf.Sqrt(std / (angles.Count - 1)) : 0f;

        float pooled = Mathf.Atan(-Slope(ar, ah, arr, arh, an)) * Mathf.Rad2Deg;
        float geometric = Mathf.Atan2(maxH, maxR) * Mathf.Rad2Deg;

        var summary = new StringBuilder();
        summary.AppendLine("particles,mean_diameter_m,pile_height_m,pile_radius_m,sectors_fitted,angle_mean_deg,angle_std_deg,angle_pooled_deg,angle_geometric_deg");
        summary.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "{0},{1:F4},{2:F4},{3:F4},{4},{5:F2},{6:F2},{7:F2},{8:F2}",
            inside.Count, meanD, maxH, maxR, angles.Count, mean, std, pooled, geometric));

        string stamp = ResultsFile.Stamp();
        ResultsFile.Save($"{name}_{stamp}_profile.csv", profile.ToString());
        ResultsFile.Save($"{name}_{stamp}_summary.csv", summary.ToString());

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "Repose angle: {0:F1} ± {1:F1}° (pooled {2:F1}°, atan(H/R) {3:F1}°), {4} particles",
            mean, std, pooled, geometric, inside.Count));

        return new Result
        {
            Particles = inside.Count, MeanDiameter = meanD, Height = maxH, Radius = maxR, SectorsFitted = angles.Count,
            AngleMean = mean, AngleStd = std, AnglePooled = pooled, AngleGeometric = geometric
        };
    }

    static float Slope(double sx, double sy, double sxx, double sxy, int n)
    {
        if (n < 2) return float.NaN;
        double denom = n * sxx - sx * sx;
        return denom > 0 ? (float)((n * sxy - sx * sy) / denom) : float.NaN;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, regionSize);
    }
}
