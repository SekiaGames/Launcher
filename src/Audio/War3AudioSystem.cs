using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>3D 音效播放池：单位语音、战斗音、建造音都走这里，超出池子就丢掉最旧的。</summary>
public sealed class War3AudioSystem
{
    private const int Voices = 16;
    private const float SameSoundCooldown = 0.08f;

    private readonly List<AudioStreamPlayer3D> _players = new();
    private readonly Dictionary<string, float> _lastPlayed = new();
    private float _clock;
    private int _logBudget = 24;

    public War3AudioSystem(Node parent)
    {
        for (int i = 0; i < Voices; i++)
        {
            var player = new AudioStreamPlayer3D
            {
                Name = $"Voice{i}",
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                UnitSize = 900f,
                MaxDistance = 4500f,
                PanningStrength = 0.6f,
                MaxPolyphony = 2,
            };
            parent.AddChild(player);
            _players.Add(player);
        }
    }

    public void Play(string soundName, Vector3 position, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(soundName)) return;

        _clock += 0.016f;
        if (_lastPlayed.TryGetValue(soundName, out float last) && _clock - last < SameSoundCooldown) return;

        AudioStream stream = War3SoundTables.Pick(soundName, out float volume);
        if (stream == null) return;

        AudioStreamPlayer3D player = NextFree();
        if (player == null) return;

        _lastPlayed[soundName] = _clock;
        player.Stream = stream;
        player.GlobalPosition = position;
        player.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.02f, volume * volumeScale));
        player.Play();
        if (_logBudget > 0)
        {
            _logBudget--;
            GD.Print($"[AUDIO] 播放 {soundName} 位置({position.X:0},{position.Z:0}) 时长{stream.GetLength():0.00}s");
        }
    }

    private AudioStreamPlayer3D NextFree()
    {
        foreach (AudioStreamPlayer3D player in _players)
        {
            if (!player.Playing) return player;
        }

        AudioStreamPlayer3D oldest = _players[0];
        foreach (AudioStreamPlayer3D player in _players)
        {
            if (player.GetPlaybackPosition() > oldest.GetPlaybackPosition()) oldest = player;
        }
        oldest.Stop();
        return oldest;
    }
}
