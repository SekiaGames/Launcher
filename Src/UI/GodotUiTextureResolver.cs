using Godot;
using System.Collections.Generic;
using OpenWC3.RHI;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed class GodotUiTextureResolver : IUiTextureResolver
{
    readonly Dictionary<string, ITexture> _cache = new(System.StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _missing = new(System.StringComparer.OrdinalIgnoreCase);

    public War3UiSkin Skin { get; set; }

    public Texture2D Load(string path)
    {
        ITexture resolved = Resolve(path);
        return resolved is GodotUiTexture godot ? godot.Source : null;
    }

    public ITexture Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string key = path.Trim();
        if (_cache.TryGetValue(key, out ITexture cached)) return cached;
        if (_missing.Contains(key)) return null;

        string resolved = Skin?.Get(key, key) ?? key;
        Texture2D texture = LoadFromPack(resolved);
        if (texture == null) _missing.Add(key);
        ITexture result = texture == null ? null : new GodotUiTexture(texture);
        _cache[key] = result;
        return result;
    }

    static Texture2D LoadFromPack(string path)
    {
        string relative = path.Replace('\\', '/').TrimStart('/');
        int colon = relative.IndexOf(':');
        if (colon >= 0) relative = relative[(colon + 1)..].TrimStart('/');

        foreach (string candidate in Candidates(relative))
        {
            if (!ResourceLoader.Exists(candidate)) continue;
            var texture = ResourceLoader.Load<Texture2D>(candidate);
            if (texture != null) return texture;
        }
        return null;
    }

    static IEnumerable<string> Candidates(string relative)
    {
        string stem = relative;
        int dot = stem.LastIndexOf('.');
        if (dot > 0) stem = stem[..dot];

        yield return Assets.Res(stem + ".png");
        yield return Assets.Res(stem + ".tga");
        yield return Assets.Res(relative);
        yield return "res://" + stem + ".png";
    }
}
