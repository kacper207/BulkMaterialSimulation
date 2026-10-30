using UnityEditor;
using UnityEngine;

// Tworzy prefaby cząstek niekulistych jako zlepki nakładających się kul (multi-sphere clumps) na bazie SandParticle:
//   SandDimer (2 kule), SandTrimer (3 kule w trójkącie równobocznym), SandTetrahedron (4 kule w czworościanie foremnym).
// Kule składowe mają w skali 1 promień 0.5, odległość środków sąsiednich kul = Spacing.
// Objętość sumy kul liczona numerycznie na siatce i zapisywana w Particle.shapeVolume - Particle skaluje zlepek tak,
// żeby jego objętość była równa objętości kuli o wylosowanej średnicy (ta sama średnica ekwiwalentna i masa co dla kuli).
public static class ClumpPrefabGenerator
{
    const string SourcePath = "Assets/SandParticle.prefab";
    const float Radius = 0.5f;
    const float Spacing = 0.5f;          // = promień kuli składowej: wyraźne nakładanie, wydłużenie dimeru 1.5
    const int VolumeSamples = 100;       // punktów siatki na oś

    [MenuItem("Tools/Create Clump Prefabs")]
    static void Create()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (source == null) { Debug.LogWarning($"Brak prefabu {SourcePath}"); return; }

        float h = Spacing / 2f;
        float tri = Spacing / Mathf.Sqrt(3f);
        float tet = Spacing / (2f * Mathf.Sqrt(2f));

        Make(source, "SandDimer", new[] { new Vector3(-h, 0f, 0f), new Vector3(h, 0f, 0f) });
        Make(source, "SandTrimer", new[]
        {
            new Vector3(tri, 0f, 0f),
            new Vector3(tri * Mathf.Cos(2f * Mathf.PI / 3f), 0f, tri * Mathf.Sin(2f * Mathf.PI / 3f)),
            new Vector3(tri * Mathf.Cos(4f * Mathf.PI / 3f), 0f, tri * Mathf.Sin(4f * Mathf.PI / 3f)),
        });
        Make(source, "SandTetrahedron", new[]
        {
            new Vector3(tet, tet, tet), new Vector3(tet, -tet, -tet),
            new Vector3(-tet, tet, -tet), new Vector3(-tet, -tet, tet),
        });
        AssetDatabase.SaveAssets();
    }

    static void Make(GameObject source, string name, Vector3[] centers)
    {
        var go = Object.Instantiate(source);
        go.name = name;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one * 0.05f;

        var collider = go.GetComponent<SphereCollider>();
        var physicsMaterial = collider.sharedMaterial;
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        var renderMaterial = go.GetComponent<MeshRenderer>().sharedMaterial;
        Object.DestroyImmediate(collider);
        Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(go.GetComponent<MeshFilter>());

        for (int i = 0; i < centers.Length; i++)
        {
            var child = new GameObject($"Sphere_{i}");
            child.transform.SetParent(go.transform, false);
            child.transform.localPosition = centers[i];
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = renderMaterial;
            var sphere = child.AddComponent<SphereCollider>();
            sphere.radius = Radius;
            sphere.sharedMaterial = physicsMaterial;
        }

        float bounding = 0f;
        foreach (var c in centers) bounding = Mathf.Max(bounding, c.magnitude + Radius);
        float volume = UnionVolume(centers, bounding);

        var so = new SerializedObject(go.GetComponent<Particle>());
        so.FindProperty("shapeVolume").floatValue = volume;
        so.FindProperty("shapeBoundingRadius").floatValue = bounding;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = $"Assets/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        float sphereVolume = 4f / 3f * Mathf.PI * Radius * Radius * Radius;
        Debug.Log($"{path}: {centers.Length} kule, objętość {volume:F4} ({volume / sphereVolume:F2} kuli składowej), promień obejmujący {bounding:F3}");
    }

    // Objętość sumy kul o promieniu Radius - zliczanie środków komórek siatki w sześcianie [-bounding, bounding]^3.
    static float UnionVolume(Vector3[] centers, float bounding)
    {
        float cell = 2f * bounding / VolumeSamples;
        float r2 = Radius * Radius;
        int hits = 0;
        for (int x = 0; x < VolumeSamples; x++)
        for (int y = 0; y < VolumeSamples; y++)
        for (int z = 0; z < VolumeSamples; z++)
        {
            var p = new Vector3(-bounding + (x + 0.5f) * cell, -bounding + (y + 0.5f) * cell, -bounding + (z + 0.5f) * cell);
            foreach (var c in centers)
                if ((p - c).sqrMagnitude <= r2) { hits++; break; }
        }
        return hits * cell * cell * cell;
    }
}
