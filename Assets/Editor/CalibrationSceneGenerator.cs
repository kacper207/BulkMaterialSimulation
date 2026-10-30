using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tworzy sceny kalibracyjne kąta usypu z płaską podłogą bez ścian:
//   Calibration    - metoda stałego lejka: lejek ze ściętego stożka nad środkiem, cząstki pojawiają się wewnątrz lejka,
//                    pryzma rośnie pod wylotem.
//   CylinderLift   - test podnoszonego cylindra: cylinder stoi na podłodze, jest napełniany, a po ustabilizowaniu
//                    cząstek powoli podnoszony - materiał osiada w pryzmę bez uderzającego strumienia.
// Ściany to płyty z BoxColliderami, a nie MeshCollider: cienka powłoka z siatki nie ma grubości
// i szybkie cząstki mogłyby przez nią przelatywać.
public static class CalibrationSceneGenerator
{
    const string PrefabPath = "Assets/SandParticle.prefab";
    static readonly string[] ShapePaths =
    {
        "Assets/SandParticle.prefab", "Assets/SandDimer.prefab", "Assets/SandTrimer.prefab", "Assets/SandTetrahedron.prefab",
    };

    const int Staves = 24;
    const float Thickness = 0.05f;
    const int ParticleCount = 3000;
    const float ParticlesPerSecond = 100f;
    const float RegionHalfWidth = 5f;

    // Lejek
    const float OutletDiameter = 0.6f;   // ok. 9 średnic cząstki - lejek się nie zatyka
    const float TopDiameter = 1.6f;
    const float FunnelHeight = 0.8f;
    const float TipHeight = 0.8f;        // wylot nad podłogą - powyżej wierzchołka pryzmy ~30° z 3000 cząstek (ok. 0,6 m)
    const float SpawnAboveTip = 0.3f;

    // Cylinder: 3000 cząstek wypełnia go do ok. 0,95 m (H/D ~ 1)
    const float CylinderDiameter = 1.0f;
    const float CylinderHeight = 1.4f;
    const float CylinderSpawnHeight = 1.2f;
    const float LiftHeight = 1.5f;       // dolna krawędź po podniesieniu - powyżej początkowej wysokości słupa
    const float LiftSpeed = 0.05f;

    [MenuItem("Tools/Create Calibration Scene")]
    static void CreateFunnelScene()
    {
        if (!Begin(out var prefab, out var material)) return;

        BuildFrustum("Funnel", OutletDiameter / 2f, TopDiameter / 2f, FunnelHeight, TipHeight, material);

        // Obszar pomiaru od podłogi do wylotu lejka - cząstki w lejku nie wchodzą do pomiaru.
        BuildCommon(prefab, material, "calibration", TipHeight + SpawnAboveTip,
            FunnelRadiusAt(SpawnAboveTip) - 0.1f, TipHeight, 0.2f, 0.8f);
        SetCamera(new Vector3(0f, 1.6f, -5f), new Vector3(0f, 0.5f, 0f));
        Save("Assets/Scenes/Calibration.unity");
    }

    [MenuItem("Tools/Create Cylinder Lift Scene")]
    static void CreateCylinderScene()
    {
        if (!Begin(out var prefab, out var material)) return;

        float radius = CylinderDiameter / 2f;
        var cylinder = BuildFrustum("Cylinder", radius, radius, CylinderHeight, 0f, material);
        cylinder.AddComponent<Rigidbody>().isKinematic = true;
        var lift = cylinder.AddComponent<LiftCylinder>();
        var so = new SerializedObject(lift);
        so.FindProperty("liftSpeed").floatValue = LiftSpeed;
        so.FindProperty("liftHeight").floatValue = LiftHeight;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Dopasowanie zbocza od 0,5 R: po podniesieniu cylindra może zostać ścięty wierzchołek o promieniu do r cylindra.
        var series = BuildCommon(prefab, material, "cylinder", CylinderSpawnHeight, radius - 0.1f, LiftHeight, 0.5f, 0.85f);
        so = new SerializedObject(series);
        so.FindProperty("cylinder").objectReferenceValue = lift;
        so.ApplyModifiedPropertiesWithoutUndo();

        SetCamera(new Vector3(0f, 1.8f, -5f), new Vector3(0f, 0.6f, 0f));
        Save("Assets/Scenes/CylinderLift.unity");
    }

    static bool Begin(out GameObject prefab, out PhysicsMaterial material)
    {
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        material = null;
        if (prefab == null) { Debug.LogWarning($"Brak prefabu {PrefabPath}"); return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        material = prefab.GetComponent<Collider>().sharedMaterial;
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        return true;
    }

    // Podłoga, ustawienia fizyki, spawner, miernik kąta (obszar od podłogi do regionHeight) i seria kalibracyjna.
    static CalibrationSeries BuildCommon(GameObject prefab, PhysicsMaterial material, string runName,
        float spawnHeight, float spawnRadius, float regionHeight, float fitFrom, float fitTo)
    {
        new GameObject("PhysicsSettings").AddComponent<PhysicsSettings>();

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
        floor.GetComponent<Collider>().sharedMaterial = material;

        var spawnPoint = new GameObject("SpawnPoint").transform;
        spawnPoint.position = new Vector3(0f, spawnHeight, 0f);

        var spawnerGo = new GameObject("Spawner");
        var spawner = spawnerGo.AddComponent<ParticleSpawner>();
        var so = new SerializedObject(spawner);
        so.FindProperty("particlePrefab").objectReferenceValue = prefab;
        so.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
        so.FindProperty("particleCount").intValue = ParticleCount;
        so.FindProperty("spawnRadius").floatValue = spawnRadius;
        so.FindProperty("particlesPerSecond").floatValue = ParticlesPerSecond;
        so.FindProperty("openBottomOnSpawn").boolValue = false;
        so.FindProperty("spawnOnKey").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        var meterGo = new GameObject("ReposeAngleMeter");
        meterGo.transform.position = new Vector3(0f, regionHeight / 2f, 0f);
        var meter = meterGo.AddComponent<ReposeAngleMeter>();
        so = new SerializedObject(meter);
        so.FindProperty("regionSize").vector3Value = new Vector3(2f * RegionHalfWidth, regionHeight, 2f * RegionHalfWidth);
        so.FindProperty("radialBins").intValue = 15;
        so.FindProperty("fitFrom").floatValue = fitFrom;
        so.FindProperty("fitTo").floatValue = fitTo;
        so.FindProperty("autoMeasure").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        var series = spawnerGo.AddComponent<CalibrationSeries>();
        so = new SerializedObject(series);
        so.FindProperty("runName").stringValue = runName;
        so.FindProperty("spawner").objectReferenceValue = spawner;
        so.FindProperty("meter").objectReferenceValue = meter;
        var surfaces = so.FindProperty("frictionSurfaces");
        surfaces.arraySize = 1;
        surfaces.GetArrayElementAtIndex(0).objectReferenceValue = floor.GetComponent<Collider>();
        var shapes = so.FindProperty("shapePrefabs");
        shapes.arraySize = 0;
        foreach (var path in ShapePaths)
        {
            var shape = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (shape == null) { Debug.LogWarning($"Brak {path} - uruchom najpierw Tools/Create Clump Prefabs"); continue; }
            shapes.arraySize++;
            shapes.GetArrayElementAtIndex(shapes.arraySize - 1).objectReferenceValue = shape;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return series;
    }

    static void SetCamera(Vector3 position, Vector3 target)
    {
        var camera = Camera.main;
        if (camera == null) return;
        camera.transform.position = position;
        camera.transform.LookAt(target);
    }

    static void Save(string path)
    {
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        Debug.Log($"Scena kalibracyjna zapisana: {path}");
    }

    static float FunnelRadiusAt(float heightAboveTip) =>
        OutletDiameter / 2f + (TopDiameter - OutletDiameter) / 2f * heightAboveTip / FunnelHeight;

    // Ścięty stożek (dla rb = rt - cylinder) z płaskich płyt stycznych do powierzchni bocznej,
    // od wysokości baseY do baseY + height. Wewnętrzna powierzchnia jest wielokątem bez szczelin.
    static GameObject BuildFrustum(string name, float rb, float rt, float height, float baseY, PhysicsMaterial material)
    {
        var root = new GameObject(name);
        float length = Mathf.Sqrt((rt - rb) * (rt - rb) + height * height);
        float width = 2f * rt * Mathf.Tan(Mathf.PI / Staves) * 1.1f;

        for (int i = 0; i < Staves; i++)
        {
            float a = i * 2f * Mathf.PI / Staves;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var slant = (radial * (rt - rb) + Vector3.up * height).normalized;
            var normal = (radial * height - Vector3.up * (rt - rb)).normalized;

            var stave = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stave.name = $"Stave_{i}";
            stave.transform.SetParent(root.transform);
            stave.transform.position = radial * ((rb + rt) / 2f) + Vector3.up * (baseY + height / 2f) + normal * (Thickness / 2f);
            stave.transform.rotation = Quaternion.LookRotation(normal, slant);
            stave.transform.localScale = new Vector3(width, length, Thickness);
            stave.GetComponent<Collider>().sharedMaterial = material;
        }
        return root;
    }
}
