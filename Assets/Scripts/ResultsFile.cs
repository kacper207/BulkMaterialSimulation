using System.IO;
using UnityEngine;

public static class ResultsFile
{
    // Zapisuje plik do katalogu Benchmarks/ obok Assets/ i zwraca jego ścieżkę.
    public static string Save(string name, string content)
    {
        string dir = Path.Combine(Application.dataPath, "..", "Benchmarks");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        Debug.Log($"Saved: {path}");
        return path;
    }

    public static string Stamp() => System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
}
