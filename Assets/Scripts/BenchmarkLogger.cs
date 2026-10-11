using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

public class BenchmarkLogger : MonoBehaviour
{
    [SerializeField] string runName = "run";
    [SerializeField] float duration = 30f;

    ProfilerRecorder mainThread;
    ProfilerRecorder physics;
    readonly StringBuilder csv = new StringBuilder();
    float startTime;
    bool recording;
    int fixedSteps;

    void OnEnable()
    {
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        physics = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate", 1);
    }

    void OnDisable()
    {
        mainThread.Dispose();
        physics.Dispose();
    }

    void FixedUpdate() => fixedSteps++;

    void Update()
    {
        if (!recording && Keyboard.current.bKey.wasPressedThisFrame) Begin();

        if (recording)
        {
            float t = Time.realtimeSinceStartup - startTime;
            csv.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3},{2:F3},{3:F3},{4},{5}",
                t,
                Time.unscaledDeltaTime * 1000f,
                mainThread.LastValue / 1e6,
                physics.LastValue / 1e6,
                Particle.Count,
                fixedSteps));

            if (t >= duration) Save();
        }

        fixedSteps = 0;
    }

    void Begin()
    {
        csv.Clear();
        csv.AppendLine("time_s,frame_ms,main_thread_ms,physics_ms,particles,fixed_steps");
        startTime = Time.realtimeSinceStartup;
        recording = true;
        Debug.Log("Benchmark start");
    }

    void Save()
    {
        recording = false;
        string dir = Path.Combine(Application.dataPath, "..", "Benchmarks");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{runName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllText(path, csv.ToString());
        Debug.Log($"Benchmark saved: {path}");
    }
}