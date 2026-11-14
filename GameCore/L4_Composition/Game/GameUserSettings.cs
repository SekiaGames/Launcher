using System;
using System.Globalization;
using System.IO;

namespace OpenWC3.Game;

public sealed class GameUserSettings
{
    private const string FileName = "openwc3-launcher-settings.ini";

    public const float MinScale = 0.5f;
    public const float MaxScale = 4f;
    public const float DefaultMusicVolume = 0.6f;

    public float CameraDistanceScale { get; private set; } = 1f;
    public float PanSensitivity { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = DefaultMusicVolume;
    public float SoundVolume { get; private set; } = 1f;
    public int FrameRateLimit { get; private set; }
    public float RenderScale { get; private set; } = 1f;

    public static GameUserSettings Load()
    {
        var settings = new GameUserSettings();
        string path = Path.Combine(SettingsDirectory(), FileName);
        if (!File.Exists(path)) return settings;
        foreach (string line in File.ReadLines(path))
        {
            string[] pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !float.TryParse(pair[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value)) continue;
            if (pair[0].Equals("cameraDistance", StringComparison.OrdinalIgnoreCase))
                settings.CameraDistanceScale = ClampScale(value);
            else if (pair[0].Equals("panSensitivity", StringComparison.OrdinalIgnoreCase))
                settings.PanSensitivity = ClampScale(value);
            else if (pair[0].Equals("musicVolume", StringComparison.OrdinalIgnoreCase))
                settings.MusicVolume = Math.Clamp(value, 0f, 1f);
            else if (pair[0].Equals("soundVolume", StringComparison.OrdinalIgnoreCase))
                settings.SoundVolume = Math.Clamp(value, 0f, 1f);
            else if (pair[0].Equals("frameRateLimit", StringComparison.OrdinalIgnoreCase))
                settings.FrameRateLimit = NormalizeFrameRate((int)value);
            else if (pair[0].Equals("renderScale", StringComparison.OrdinalIgnoreCase))
                settings.RenderScale = NormalizeRenderScale(value);
        }
        return settings;
    }

    public void SetCameraDistance(float value) => CameraDistanceScale = ClampScale(value);
    public void SetPanSensitivity(float value) => PanSensitivity = ClampScale(value);
    public void SetMusicVolume(float value) => MusicVolume = Math.Clamp(value, 0f, 1f);
    public void SetSoundVolume(float value) => SoundVolume = Math.Clamp(value, 0f, 1f);
    public void SetFrameRateLimit(int value) => FrameRateLimit = NormalizeFrameRate(value);
    public void SetRenderScale(float value) => RenderScale = NormalizeRenderScale(value);

    public void Save()
    {
        string directory = SettingsDirectory();
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        string text = "cameraDistance=" + CameraDistanceScale.ToString("0.###", CultureInfo.InvariantCulture)
            + Environment.NewLine
            + "panSensitivity=" + PanSensitivity.ToString("0.###", CultureInfo.InvariantCulture)
            + Environment.NewLine
            + "musicVolume=" + MusicVolume.ToString("0.###", CultureInfo.InvariantCulture)
            + Environment.NewLine
            + "soundVolume=" + SoundVolume.ToString("0.###", CultureInfo.InvariantCulture)
            + Environment.NewLine
            + "frameRateLimit=" + FrameRateLimit.ToString(CultureInfo.InvariantCulture)
            + Environment.NewLine
            + "renderScale=" + RenderScale.ToString("0.###", CultureInfo.InvariantCulture)
            + Environment.NewLine;
        File.WriteAllText(path, text);
    }

    public static int NormalizeFrameRate(int fps)
        => fps is 30 or 60 or 90 or 120 or 144 ? fps : 0;

    public static float NormalizeRenderScale(float scale)
    {
        if (!float.IsFinite(scale)) return 1f;
        float[] values = { 0.5f, 0.67f, 0.75f, 0.85f, 1f };
        float nearest = 1f;
        float delta = float.MaxValue;
        foreach (float value in values)
        {
            float candidate = MathF.Abs(value - scale);
            if (candidate >= delta) continue;
            delta = candidate;
            nearest = value;
        }
        return nearest;
    }

    static float ClampScale(float value)
        => Math.Clamp(float.IsFinite(value) ? value : 1f, MinScale, MaxScale);

    static string SettingsDirectory()
    {
        string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        return string.IsNullOrWhiteSpace(personal) ? AppContext.BaseDirectory : personal;
    }
}