using UnityEditor;
using UnityEngine;

public static class HallColliderGenerator
{
    const float T = 1f;

    [MenuItem("Tools/Generate Hall Colliders")]
    static void Generate()
    {
        var target = Selection.activeGameObject;
        if (target == null) { Debug.LogWarning("Zaznacz obiekt hali"); return; }

        var renderers = target.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);

        Debug.Log($"Hala: X = {b.size.x:F2} m, Z = {b.size.z:F2} m, wysokosc = {b.size.y:F2} m, podloga Y = {b.min.y:F2}");

        var old = GameObject.Find("HallColliders");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("HallColliders");
        Undo.RegisterCreatedObjectUndo(root, "Hall Colliders");

        var c = b.center;
        var s = b.size;
        Make(root, "Wall_N", new Vector3(c.x, c.y, b.max.z - T / 2), new Vector3(s.x, s.y, T));
        Make(root, "Wall_S", new Vector3(c.x, c.y, b.min.z + T / 2), new Vector3(s.x, s.y, T));
        Make(root, "Wall_E", new Vector3(b.max.x - T / 2, c.y, c.z), new Vector3(T, s.y, s.z));
        Make(root, "Wall_W", new Vector3(b.min.x + T / 2, c.y, c.z), new Vector3(T, s.y, s.z));
        Make(root, "Ceiling", new Vector3(c.x, b.max.y - T / 2, c.z), new Vector3(s.x, T, s.z));
    }

    static void Make(GameObject parent, string name, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        go.AddComponent<BoxCollider>().size = size;
    }
}