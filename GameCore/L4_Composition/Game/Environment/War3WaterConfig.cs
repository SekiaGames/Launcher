using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public sealed class War3WaterConfig
{

    public float HeightOffset { get; private set; }
    public float WavesDepth { get; private set; } = 25f / 128f;
    public string ShoreDirectory { get; private set; } = "";
    public string StraightFile { get; private set; } = "";
    public int StraightVariations { get; private set; } = 1;
    public string OutsideCornerFile { get; private set; } = "";
    public int OutsideCornerVariations { get; private set; } = 1;
    public string InsideCornerFile { get; private set; } = "";
    public int InsideCornerVariations { get; private set; } = 1;
    public bool ShowCliffWaves { get; private set; } = true;
    public bool ShowRollingWaves { get; private set; } = true;
    public int FrameCount { get; private set; }
    public int FrameRate { get; private set; }
    public Color ShallowMin { get; private set; } = new Color(1f, 1f, 1f, 1f);
    public Color ShallowMax { get; private set; } = new Color(1f, 1f, 1f, 1f);
    public Color DeepMin { get; private set; } = new Color(1f, 1f, 1f, 1f);
    public Color DeepMax { get; private set; } = new Color(1f, 1f, 1f, 1f);
    public Texture2DArray Texture { get; private set; }

    public static float LoadHeightOffset(char tileset)
    {
        SlkTable table = War3SlkReader.Water();
        int row = table.FindRow("waterID", tileset + "Sha");
        return row < 0 ? 0f : table.GetFloat(row, "height");
    }

    public static War3WaterConfig Load(char tileset)
    {
        SlkTable table = War3SlkReader.Water();
        string rowId = tileset + "Sha";
        int row = table.FindRow("waterID", rowId);
        if (row < 0)
        {
            GD.PushWarning($"[WATER] Water.slk 里没有 {rowId}");
            return null;
        }

        var water = new War3WaterConfig
        {
            HeightOffset = table.GetFloat(row, "height"),
            WavesDepth = ReadMiscFloat("Water.WavesDepth", 25f) / 128f,
            ShoreDirectory = (table.Get(row, "shoreDir") ?? "").Replace('\\', '/').Trim('/'),
            StraightFile = table.Get(row, "shoreSFile") ?? "",
            StraightVariations = Mathf.Max(1, table.GetInt(row, "shoreSVar", 1)),
            OutsideCornerFile = table.Get(row, "shoreOCFile") ?? "",
            OutsideCornerVariations = Mathf.Max(1, table.GetInt(row, "shoreOCVar", 1)),
            InsideCornerFile = table.Get(row, "shoreICFile") ?? "",
            InsideCornerVariations = Mathf.Max(1, table.GetInt(row, "shoreICVar", 1)),
            FrameCount = Mathf.Max(0, table.GetInt(row, "numTex")),
            FrameRate = Mathf.Max(1, table.GetInt(row, "texRate")),
            ShallowMin = ReadColor(table, row, "Smin"),
            ShallowMax = ReadColor(table, row, "Smax"),
            DeepMin = ReadColor(table, row, "Dmin"),
            DeepMax = ReadColor(table, row, "Dmax"),
        };

        water.Texture = BuildTexture(table.Get(row, "texFile"), water.FrameCount);
        return water;
    }

    private static Color ReadColor(SlkTable table, int row, string prefix)
    {
        return new Color(
            table.GetInt(row, prefix + "_R") / 255f,
            table.GetInt(row, prefix + "_G") / 255f,
            table.GetInt(row, prefix + "_B") / 255f,
            table.GetInt(row, prefix + "_A") / 255f);
    }

    private static float ReadMiscFloat(string key, float fallback)
    {
        if (!GeneratedTexts.TryGet("UIMiscData", key, out string value)) return fallback;
        return float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    private static Texture2DArray BuildTexture(string texFile, int frameCount)
    {
        if (string.IsNullOrEmpty(texFile) || frameCount <= 0)
        {
            return null;
        }

        string prefix = texFile.Replace('\\', '/');
        var images = new Godot.Collections.Array<Image>();
        for (int i = 0; i < frameCount; i++)
        {
            string path = $"{prefix}{(i < 10 ? "0" : "")}{i}.blp";
            if (!War3Assets.Exists(path))
            {
                GD.PushWarning($"[WATER] 缺少波纹贴图 {path}");
                continue;
            }
            Image image = War3Assets.LoadImage(path);
            if (image == null)
            {
                GD.PushWarning($"[WATER] 贴图加载失败 {path}");
                continue;
            }
            if (image.GetFormat() != Image.Format.Rgba8)
            {
                image.Convert(Image.Format.Rgba8);
            }
            image.GenerateMipmaps();
            images.Add(image);
        }

        if (images.Count == 0)
        {
            return null;
        }

        var texture = new Texture2DArray();
        texture.CreateFromImages(images);
        return texture;
    }
}
