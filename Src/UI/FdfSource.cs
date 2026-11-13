using Godot;
using System.IO;
using GodotFileAccess = Godot.FileAccess;

namespace OpenWC3.Game.UI;

public static class FdfSource
{
    public static string UnpackDir { get; } = Assets.WorkspaceDir + "/test/war3-unpack";

    public static string Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string relative = Normalize(path);

        string packed = Assets.Res(relative);
        if (GodotFileAccess.FileExists(packed))
        {
            using var handle = GodotFileAccess.Open(packed, GodotFileAccess.ModeFlags.Read);
            if (handle != null) return handle.GetAsText();
        }

        string loose = Path.Combine(UnpackDir, relative);
        return File.Exists(loose) ? File.ReadAllText(loose) : null;
    }

    static string Normalize(string path)
    {
        string relative = path.Replace('\\', '/').TrimStart('/');
        int markers = relative.IndexOf("UI/", System.StringComparison.OrdinalIgnoreCase);
        return markers > 0 ? relative[markers..] : relative;
    }
}
