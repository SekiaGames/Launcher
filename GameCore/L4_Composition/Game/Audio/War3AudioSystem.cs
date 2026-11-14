using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 声音播放池。单位语音/战斗音/界面音走 2D（跟随监听者、不衰减），
/// 弹道/环境/技能命中走 3D（按声音自己的 Min/MaxDistance 衰减）。
/// </summary>
public sealed class War3AudioSystem
{
    private const int PositionalVoices = 16;
    private const int FlatVoices = 8;
    private const float SameSoundCooldown = 0.08f;

    private readonly List<AudioStreamPlayer3D> _positional = new();
    private readonly List<AudioStreamPlayer> _flat = new();
    private readonly Dictionary<string, double> _lastPlayed = new(System.StringComparer.OrdinalIgnoreCase);
    private int _logBudget = 24;

    public War3AudioSystem(Node parent)
    {
        for (int i = 0; i < PositionalVoices; i++)
        {
            var player = new AudioStreamPlayer3D
            {
                Name = $"Voice3D{i}",
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                UnitSize = 900f,
                MaxDistance = 4500f,
                PanningStrength = 0.6f,
                MaxPolyphony = 2,
            };
            parent.AddChild(player);
            _positional.Add(player);
        }
        for (int i = 0; i < FlatVoices; i++)
        {
            var player = new AudioStreamPlayer { Name = $"Voice2D{i}" };
            parent.AddChild(player);
            _flat.Add(player);
        }
    }

    public static double Now => Time.GetTicksMsec() / 1000.0;

    public static bool Exists(string resource)
        => War3SoundTables.ResExists(resource);

    public static float Duration(AudioStream stream)
        => stream == null ? 0f : (float)stream.GetLength();

    public float Play2D(AudioStream stream, string key, float volume, float pitch)
    {
        if (stream == null) return 0f;
        if (BlockedByCooldown(key)) return 0f;
        AudioStreamPlayer player = NextFreeFlat();
        if (player == null) return 0f;
        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.02f, volume));
        player.PitchScale = Mathf.Max(0.01f, pitch);
        player.Play();
        Log(key, null, stream, pitch);
        return Duration(stream) / Mathf.Max(0.01f, pitch);
    }

    public float Play3D(AudioStream stream, string key, Vector3 position, float volume, float pitch,
        float minDistance, float maxDistance)
    {
        if (stream == null) return 0f;
        if (BlockedByCooldown(key)) return 0f;
        AudioStreamPlayer3D player = NextFreePositional();
        if (player == null) return 0f;
        player.Stream = stream;
        player.GlobalPosition = position;
        player.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.02f, volume));
        player.PitchScale = Mathf.Max(0.01f, pitch);
        player.UnitSize = Mathf.Max(1f, minDistance);
        player.MaxDistance = Mathf.Max(minDistance + 1f, maxDistance);
        player.Play();
        Log(key, position, stream, pitch);
        return Duration(stream) / Mathf.Max(0.01f, pitch);
    }

    private bool BlockedByCooldown(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        double now = Now;
        if (_lastPlayed.TryGetValue(key, out double last) && now - last < SameSoundCooldown) return true;
        _lastPlayed[key] = now;
        return false;
    }

    private void Log(string key, Vector3? position, AudioStream stream, float pitch)
    {
        if (_logBudget <= 0) return;
        _logBudget--;
        string where = position.HasValue
            ? $"位置({position.Value.X:0},{position.Value.Z:0})"
            : "2D";
        GD.Print($"[AUDIO] 播放 {key} {where} 时长{Duration(stream) / Mathf.Max(0.01f, pitch):0.00}s");
    }

    private AudioStreamPlayer NextFreeFlat()
    {
        foreach (AudioStreamPlayer player in _flat)
        {
            if (!player.Playing) return player;
        }
        AudioStreamPlayer oldest = _flat[0];
        foreach (AudioStreamPlayer player in _flat)
        {
            if (player.GetPlaybackPosition() > oldest.GetPlaybackPosition()) oldest = player;
        }
        oldest.Stop();
        return oldest;
    }

    private AudioStreamPlayer3D NextFreePositional()
    {
        foreach (AudioStreamPlayer3D player in _positional)
        {
            if (!player.Playing) return player;
        }
        AudioStreamPlayer3D oldest = _positional[0];
        foreach (AudioStreamPlayer3D player in _positional)
        {
            if (player.GetPlaybackPosition() > oldest.GetPlaybackPosition()) oldest = player;
        }
        oldest.Stop();
        return oldest;
    }
}
