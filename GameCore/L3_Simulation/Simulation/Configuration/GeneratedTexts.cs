using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game.Generated;

namespace OpenWC3.Game;

public static class GeneratedTexts
{
    static readonly Dictionary<string, Godot.Collections.Dictionary> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    static readonly HashSet<string> Missing = new(StringComparer.OrdinalIgnoreCase);

    public static Godot.Collections.Dictionary Get(string pathOrName)
    {
        string name = Normalize(pathOrName);
        if (Cache.TryGetValue(name, out Godot.Collections.Dictionary cached)) return cached;

        Godot.Collections.Dictionary root = Build(name);
        Cache[name] = root;
        return root;
    }

    static Godot.Collections.Dictionary Build(string name)
    {
        var keys = new Godot.Collections.Dictionary();
        var root = new Godot.Collections.Dictionary { ["keys"] = keys };

        if (!TextTables.All.TryGetValue(name, out var table))
        {
            string match = null;
            foreach (string key in TextTables.All.Keys)
            {
                if (key.EndsWith(name, StringComparison.OrdinalIgnoreCase)) { match = key; break; }
            }
            if (match == null)
            {
                if (Missing.Add(name)) GD.PushError($"[TEXT] 生成的文本表里找不到 {name}");
                return root;
            }
            table = TextTables.All[match];
        }

        for (int i = 0; i < table.Keys.Length; i++)
            keys[table.Keys[i]] = table.Values[i];
        return root;
    }

    public static bool TryGet(string table, string key, out string value)
    {
        value = null;
        Godot.Collections.Dictionary keys = Get(table)["keys"].AsGodotDictionary();
        if (keys.TryGetValue(key, out Variant exact)) { value = exact.AsString(); return true; }
        foreach (KeyValuePair<Variant, Variant> pair in keys)
        {
            string name = pair.Key.AsString();
            int dot = name.LastIndexOf('.');
            string tail = dot >= 0 ? name[(dot + 1)..] : name;
            if (!tail.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            value = pair.Value.AsString();
            return true;
        }
        return false;
    }

    static string Normalize(string pathOrName)
    {
        string name = pathOrName ?? "";
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name[(slash + 1)..];
        return name;
    }
}
