using Godot;
using System;

namespace OpenWC3;

public static class War3Assets
{
    public const string ConvertedRoot = "res://extract/";

    public static bool Exists(string relative)
    {
        return ConvertedPath(relative) != null;
    }

    public static string ConvertedPath(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;

        string rel = relative.Replace('\\', '/');
        int slash = rel.LastIndexOf('/');
        string directory = slash >= 0 ? rel[..(slash + 1)] : "";
        string file = slash >= 0 ? rel[(slash + 1)..] : rel;
        int dot = file.LastIndexOf('.');
        if (dot > 0) file = file[..dot];

        return PathResolve.ResourceFile(directory + file, ".png");
    }

    public static Image LoadImage(string relative)
    {
        string path = ConvertedPath(relative);
        if (path == null) return null;

        Texture2D texture = ResourceLoader.Load<Texture2D>(path);
        Image image = texture?.GetImage();
        if (image == null) return null;

        if (image.IsCompressed()) image.Decompress();
        image.Convert(Image.Format.Rgba8);
        return image;
    }
}