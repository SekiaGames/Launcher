using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using WorldEdit.Core.Formats.W3x;
using WorldEdit.Core.Map;

namespace Launcher;

public static class GmapImporter
{
    public static int Build(string mapPath, string outputPath)
    {
        if (!File.Exists(mapPath))
        {
            Console.Error.WriteLine($"[GMAP] 找不到地图: {mapPath}");
            return 1;
        }

        string stage = Path.GetFullPath(Path.Combine(Assets.WorkspaceDir, ".gmap_build"));
        if (Directory.Exists(stage)) Directory.Delete(stage, true);
        Directory.CreateDirectory(stage + "/map");

        MapDocument document;
        try
        {
            document = MapDocument.Load(mapPath);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"[GMAP] 解析地图失败: {error.Message}");
            return 1;
        }

        if (document.Terrain == null)
        {
            Console.Error.WriteLine("[GMAP] 缺少 war3map.w3e");
            return 1;
        }

        int written = WriteRawEntries(document, stage + "/map");
        WriteManifest(document, mapPath, stage + "/manifest.json");
        long size = Pack(stage, outputPath);
        Directory.Delete(stage, true);

        Console.WriteLine($"[GMAP] 条目 {written} 个 → {outputPath} ({size / 1024 / 1024.0:0.0} MB)");
        return 0;
    }

    private static int WriteRawEntries(MapDocument document, string directory)
    {
        int written = 0;
        foreach (MapDocument.Entry entry in document.Entries)
        {
            if (entry.Name == null || entry.Data == null || entry.Data.Length == 0) continue;

            string relative = entry.Name.Replace('\\', '/').TrimStart('/');
            if (relative.StartsWith("_locales", StringComparison.OrdinalIgnoreCase)) continue;

            string target = directory + "/" + relative;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, entry.Data);
            written++;
        }
        return written;
    }

    private static void WriteManifest(MapDocument document, string mapPath, string path)
    {
        W3eTerrain terrain = document.Terrain;
        var manifest = new Godot.Collections.Dictionary
        {
            ["format"] = 1,
            ["generator"] = "LauncherGmapImporter",
            ["name"] = document.MapName ?? Path.GetFileNameWithoutExtension(mapPath),
            ["source"] = Path.GetFileName(mapPath),
            ["tileset"] = terrain.Tileset.ToString(),
            ["width"] = terrain.Width,
            ["height"] = terrain.Height,
            ["offset_x"] = terrain.OffsetX,
            ["offset_y"] = terrain.OffsetY,
            ["built_at"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };

        var files = new Godot.Collections.Array<string>();
        foreach (MapDocument.Entry entry in document.Entries)
        {
            if (entry.Name != null) files.Add(entry.Name.Replace('\\', '/'));
        }
        manifest["files"] = files;

        var players = new Godot.Collections.Array();
        if (document.Info != null)
        {
            foreach (W3iPlayer player in document.Info.Players)
            {
                players.Add(new Godot.Collections.Dictionary
                {
                    ["id"] = player.Id,
                    ["controller"] = player.Controller,
                    ["race"] = player.Race,
                    ["start_x"] = player.StartX,
                    ["start_y"] = player.StartY,
                });
            }
        }
        manifest["players"] = players;

        File.WriteAllText(path, Json.Stringify(manifest, "  "));
    }

    private static long Pack(string stageDirectory, string outputPath)
    {
        string full = Path.GetFullPath(outputPath);
        string parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        if (File.Exists(full)) File.Delete(full);

        var packer = new PckPacker();
        packer.PckStart(full, 32);

        var files = new List<string>(Directory.GetFiles(stageDirectory, "*", SearchOption.AllDirectories));
        files.Sort(StringComparer.Ordinal);
        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(stageDirectory, file).Replace('\\', '/');
            packer.AddFile("res://gmap/" + relative, file);
        }

        Error error = packer.Flush(true);
        if (error != Error.Ok)
        {
            Console.Error.WriteLine($"[GMAP] 打包失败: {error}");
            return 0;
        }
        return new FileInfo(full).Length;
    }
}