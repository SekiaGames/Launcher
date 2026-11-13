using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using OpenWC3.UI;

namespace OpenWC3.Game.Diagnostics;

public static class FdfProbe
{
    public static int Run(string fdfPath)
    {
        if (string.IsNullOrWhiteSpace(fdfPath) || !File.Exists(fdfPath))
        {
            GD.PushError($"[FDF] 找不到文件 {fdfPath}");
            return 1;
        }

        string assetsRoot = FindAssetsRoot(Path.GetFullPath(fdfPath));
        var env = new FrameTemplateEnvironment();
        List<FrameDefinition> frames = FdfParser.Parse(File.ReadAllText(fdfPath), env,
            include => LoadInclude(assetsRoot, include));

        RuntimeDiagnostics.WriteLine($"[FDF] {Path.GetFileName(fdfPath)} 顶层帧 {frames.Count}");
        foreach (FrameDefinition frame in frames)
        {
            RuntimeDiagnostics.WriteLine($"[FDF]   {frame.FrameClass} \"{frame.Name}\" 子帧 {frame.Children.Count}");
            foreach (FrameDefinition child in frame.Children)
                RuntimeDiagnostics.WriteLine($"[FDF]     {child.FrameClass} \"{child.Name}\" 子帧 {child.Children.Count}");
        }
        return 0;
    }

    static string LoadInclude(string assetsRoot, string include)
    {
        if (string.IsNullOrWhiteSpace(include) || assetsRoot == null) return null;
        string relative = include.Replace('\\', '/').TrimStart('/');
        string full = Path.Combine(assetsRoot, relative);
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }

    static string FindAssetsRoot(string startPath)
    {
        for (DirectoryInfo directory = new(Path.GetDirectoryName(startPath));
             directory != null; directory = directory.Parent)
        {
            if (directory.Name.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                return directory.FullName;
        }
        return null;
    }
}
