using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using OpenWC3.RHI;

namespace OpenWC3.Game.UI;

public static class MapThumbnailCache
{
    private static readonly Dictionary<string, ITexture> Cache = new(StringComparer.Ordinal);

    private static readonly string[] Candidates =
    {
        "war3mapMap.png", "war3mapMap.tga", "war3mapMap.blp",
        "war3mapPreview.png", "war3mapPreview.tga", "war3mapPreview.blp", "minimap.tga",
    };

    public static ITexture Get(MapSource map)
    {
        if (map?.Package == null) return null;
        string key = map.Package.Path ?? map.Name;
        if (Cache.TryGetValue(key, out ITexture cached)) return cached;

        ITexture texture = Decode(map);
        Cache[key] = texture;
        return texture;
    }

    private static ITexture Decode(MapSource map)
    {
        foreach (string candidate in Candidates)
        {
            byte[] bytes = map.Package.ReadMapFile(candidate);
            if (bytes == null) continue;
            var image = new Image();
            Error error = Path.GetExtension(candidate).ToLowerInvariant() switch
            {
                ".tga" => image.LoadTgaFromBuffer(bytes),
                ".png" => image.LoadPngFromBuffer(bytes),
                _ => Error.Unavailable,
            };
            if (error != Error.Ok) continue;
            return new GodotUiTexture(ImageTexture.CreateFromImage(image));
        }

        var path = map.Info?.MinimapTgaPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            string png = "res://gmap/map/" + Path.ChangeExtension(path.Replace('\\', '/'), ".png");
            byte[] bytes = Assets.ReadBytes(png);
            if (bytes != null)
            {
                var image = new Image();
                if (image.LoadPngFromBuffer(bytes) == Error.Ok)
                {
                    return new GodotUiTexture(ImageTexture.CreateFromImage(image));
                }
            }
        }
        return null;
    }
}
