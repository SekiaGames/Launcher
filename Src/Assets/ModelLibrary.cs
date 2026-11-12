using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public static class ModelLibrary
{
    public const string Root = War3Assets.ConvertedRoot;

    public static string Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return PathResolve.ModelScene(Normalize(name));
    }

    public static bool Exists(string name)
    {
        return Find(name) != null;
    }

    public static string FindImage(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string key = Normalize(name);
        return PathResolve.ResourceFile(key, ".png");
    }

    public static int ApplyMissingMaterials(Node root, string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return 0;

        string materialsDirectory = scenePath.GetBaseDir() + "/materials";
        if (!DirAccess.DirExistsAbsolute(materialsDirectory)) return 0;

        var files = new List<string>();
        DirAccess access = DirAccess.Open(materialsDirectory);
        if (access == null) return 0;
        foreach (string entry in access.GetFiles())
        {
            if (entry.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
            {
                files.Add(materialsDirectory + "/" + entry);
            }
        }
        if (files.Count == 0) return 0;
        files.Sort(StringComparer.Ordinal);

        int index = 0;
        int applied = 0;
        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            if (mesh.GetSurfaceOverrideMaterial(0) == null && index < files.Count)
            {
                string candidate = files[index];
                try
                {
                    if (ResourceLoader.Exists(candidate) && ResourceLoader.Load(candidate) is Material material)
                    {
                        mesh.SetSurfaceOverrideMaterial(0, material);
                        applied++;
                    }
                }
                catch (Exception error)
                {
                    GD.PushWarning($"[UNIT] 材质兜底失败 {candidate}: {error.Message}");
                }
            }
            index++;
        }
        return applied;
    }

    private static string Normalize(string name)
    {
        string key = name.Replace('\\', '/').TrimStart('/');
        if (key.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".blp", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^4];
        }
        return key;
    }
}