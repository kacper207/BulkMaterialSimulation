using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tworzy scenę kalibracyjną kąta usypu (metoda stałego lejka): płaska podłoga bez ścian,
// lejek ze ściętego stożka nad środkiem (otwarty od dołu i od góry), cząstki pojawiają się wewnątrz lejka.
// Ściany lejka to płyty z BoxColliderami, a nie MeshCollider: cienka powłoka z siatki nie ma grubości
// i szybkie cząstki mogłyby przez nią przelatywać.
// Pryzma rośnie, aż wierzchołek dojdzie do wylotu - wtedy wypływ ustaje, a nadmiar zostaje w lejku.
public static class CalibrationSceneGenerator
{
    const string ScenePath = "Assets/Scenes/Calibration.unity";
    const string PrefabPath = "Assets/SandParticle.prefab";

    const float OutletDiameter = 0.6f;   // ok. 9 średnic cząstki - lejek się nie zatyka
    const float TopDiameter = 1.6f;
    const float FunnelHeight = 0.8f;
    const float TipHeight = 0.5f;        // wylot nad podłogą
    const float SpawnAboveTip = 0.3f;
    const int Staves = 24;
    const float Thickness = 0.05f;

    const int ParticleCount = 3000;
    const float ParticlesPerSecond = 100f;
    const float RegionHalfWidth = 5f;

    [MenuItem("Tools/Create Calibration Scene")]
    static void Create()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogWarning($"Brak prefabu {PrefabPath}"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var material = prefab.GetComponent<Collider>().sharedMaterial;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        new GameObject("PhysicsSettings").AddComponent<PhysicsSettings>();

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(20f, 0.2f, 20f);
        floor.GetComponent<Collider>().sharedMaterial = material;

        BuildFunnel(material);

        var spawnPoint = new GameObject("SpawnPoint").transform;
        spawnPoint.position = new Vector3(0f, TipHeight + SpawnAboveTip, 0f);

        var spawnerGo = new GameObject("Spawner");
        var spawner = spawnerGo.AddComponent<ParticleSpawner>();
        var so = new SerializedObject(spawner);
        so.FindProperty("particlePrefab").objectReferenceValue = prefab;
        so.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
        so.FindProperty("particleCount").intValue = ParticleCount;
        so.FindProperty("spawnRadius").floatValue = FunnelRadiusAt(SpawnAboveTip) - 0.1f;
        so.FindProperty("particlesPerSecond").floatValue = ParticlesPerSecond;
        so.FindProperty("openBottomOnSpawn").boolValue = false;
        so.FindProperty("spawnOnKey").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Obszar pomiaru od podłogi do wylotu lejka - cząstki w lejku nie wchodzą do pomiaru.
        var meterGo = new GameObject("ReposeAngleMeter");
        meterGo.transform.position = new Vector3(0f, TipHeight / 2f, 0f);
        var meter = meterGo.AddComponent<ReposeAngleMeter>();
        so = new SerializedObject(meter);
        so.FindProperty("regionSize").vector3Value = new Vector3(2f * RegionHalfWidth, TipHeight, 2f * RegionHalfWidth);
        so.FindProperty("radialBins").intValue = 15;
        so.FindProperty("autoMeasure").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();

        var series = spawnerGo.AddComponent<CalibrationSeries>();
        so = new SerializedObject(series);
        so.FindProperty("spawner").objectReferenceValue = spawner;
        so.FindProperty("meter").objectReferenceValue = meter;
        so.ApplyModifiedPropertiesWithoutUndo();

        var camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 1.2f, -4f);
            camera.transform.LookAt(new Vector3(0f, 0.3f, 0f));
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Scena kalibracyjna zapisana: {ScenePath}");
    }

    static float FunnelRadiusAt(float heightAboveTip) =>
        OutletDiameter / 2f + (TopDiameter - OutletDiameter) / 2f * heightAboveTip / FunnelHeight;

    // Ścięty stożek z płaskich płyt stycznych do powierzchni stożka (wewnętrzna powierzchnia jest wielokątem bez szczelin).
    static void BuildFunnel(PhysicsMaterial material)
    {
        var root = new GameObject("Funnel");
        float rb = OutletDiameter / 2f, rt = TopDiameter / 2f;
        float length = Mathf.Sqrt((rt - rb) * (rt - rb) + FunnelHeight * FunnelHeight);
        float width = 2f * rt * Mathf.Tan(Mathf.PI / Staves) * 1.1f;

        for (int i = 0; i < Staves; i++)
        {
            float a = i * 2f * Mathf.PI / Staves;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var slant = (radial * (rt - rb) + Vector3.up * FunnelHeight).normalized;
            var normal = (radial * FunnelHeight - Vector3.up * (rt - rb)).normalized;

            var stave = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stave.name = $"Stave_{i}";
            stave.transform.SetParent(root.transform);
            stave.transform.position = radial * ((rb + rt) / 2f) + Vector3.up * (TipHeight + FunnelHeight / 2f) + normal * (Thickness / 2f);
            stave.transform.rotation = Quaternion.LookRotation(normal, slant);
            stave.transform.localScale = new Vector3(width, length, Thickness);
            stave.GetComponent<Collider>().sharedMaterial = material;
        }
    }
}
