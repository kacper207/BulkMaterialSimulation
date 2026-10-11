using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    [SerializeField] int targetFps = 165;
    [SerializeField] bool benchmarkMode;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = benchmarkMode ? -1 : targetFps;
    }
}