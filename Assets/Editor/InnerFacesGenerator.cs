using UnityEditor;
using UnityEngine;

public static class InnerFacesGenerator
{
    const string Folder = "Assets/GeneratedMeshes";

    [MenuItem("Tools/Create Inner Faces")]
    static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");

        foreach (var go in Selection.gameObjects)
        {
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            if (mf == null || mr == null) continue;

            var mesh = Object.Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + "_Inner";

            var n = mesh.normals;
            for (int i = 0; i < n.Length; i++) n[i] = -n[i];
            mesh.normals = n;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3) (tris[i], tris[i + 1]) = (tris[i + 1], tris[i]);
                mesh.SetTriangles(tris, s);
            }
            mesh.RecalculateTangents();

            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{mesh.name}.asset"));

            var inner = new GameObject(go.name + "_Inner");
            Undo.RegisterCreatedObjectUndo(inner, "Create Inner Faces");
            inner.transform.SetParent(go.transform, false);
            inner.AddComponent<MeshFilter>().sharedMesh = mesh;
            inner.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
        }
        AssetDatabase.SaveAssets();
    }
}