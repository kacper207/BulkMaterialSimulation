using UnityEditor;
using UnityEngine;

public static class HallLightGenerator
{
    const int CountX = 3;
    const int CountZ = 2;
    const float Intensity = 600f;

    [MenuItem("Tools/Generate Hall Lights")]
    static void Generate()
    {
        var target = Selection.activeGameObject;
        if (target == null) { Debug.LogWarning("Zaznacz house"); return; }

        var renderers = target.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        var old = GameObject.Find("HallLights");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("HallLights");
        Undo.RegisterCreatedObjectUndo(root, "Hall Lights");

        float y = b.min.y + b.size.y * 0.8f;
        float range = Mathf.Max(b.size.x / CountX, b.size.z / CountZ) * 1.5f + b.size.y;

        for (int i = 0; i < CountX; i++)
            for (int j = 0; j < CountZ; j++)
            {
                var go = new GameObject($"Lamp_{i}_{j}");
                go.transform.SetParent(root.transform);
                go.transform.position = new Vector3(
                    Mathf.Lerp(b.min.x, b.max.x, (i + 0.5f) / CountX), y,
                    Mathf.Lerp(b.min.z, b.max.z, (j + 0.5f) / CountZ));
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                l.range = range;
                l.intensity = Intensity;
                l.shadows = LightShadows.None;
            }
    }
}