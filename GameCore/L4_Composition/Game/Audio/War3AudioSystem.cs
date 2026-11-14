using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public enum SoundChannel { Unit, Combat, Ui, Effect, Ambient, Music, Movement, General }

/// <summary>
/// 声音声部池。对标客户端 War3AudioSystem：先按同文件、channel 配额、系统 24 声部
/// 三道限制选声部，满了按优先级抢占；每条声音用自己的 Min/MaxDistance 衰减，
/// 循环音（脚步/建造）通过播放句柄持续跟随位置。
/// </summary>
public sealed class War3AudioSystem
{
    private const int MaxSystemVoices = 24;
    private const int MaxSameFile = 3;
    private static readonly int[] ChannelMax = { 8, 8, 4, 4, 3, 2, 4, 4 };
    private const int MaxVoicesPerKind = MaxSystemVoices;

    private sealed class Voice
    {
        public uint Id;
        public SoundChannel Channel;
        public int Priority;
        public string Key;
        public bool Looping;
        public bool Positional;
        public AudioStreamPlayer3D Three;
        public AudioStreamPlayer Flat;
        public bool Active;

        public bool Playing => Three != null ? Three.Playing : Flat != null && Flat.Playing;

        public void StopPlaying()
        {
            Three?.Stop();
            Flat?.Stop();
        }
    }

    private readonly Node _parent;
    private readonly List<Voice> _voices = new();
    private readonly List<Voice> _active = new();
    private readonly Dictionary<AudioStream, AudioStream> _loopStreams = new();
    private uint _nextPlaybackId;
    private int _logBudget = 24;
    private float _volume = 1f;

    public void SetVolume(float value)
        => _volume = Mathf.Clamp(float.IsFinite(value) ? value : 1f, 0f, 1f);

    public War3AudioSystem(Node parent)
    {
        _parent = parent;
    }

    public static double Now => Time.GetTicksMsec() / 1000.0;

    public static bool Exists(string resource)
        => War3SoundTables.ResExists(resource);

    public static float Duration(AudioStream stream)
        => stream == null ? 0f : (float)stream.GetLength();

    public float Play2D(AudioStream stream, string key, float volume, float pitch,
        SoundChannel channel = SoundChannel.Ui, int priority = 10)
    {
if (Start(stream, key, Vector3.Zero, looping: false, positional: false,
                volume, pitch, 1f, 100f, channel, priority) == 0) return 0f;
        return Duration(stream) / Mathf.Max(0.01f, pitch);
    }

    public float Play3D(AudioStream stream, string key, Vector3 position, float volume, float pitch,
        float minDistance, float maxDistance,
        SoundChannel channel = SoundChannel.Unit, int priority = 50)
    {
if (Start(stream, key, position, looping: false, positional: true,
                volume, pitch, minDistance, maxDistance, channel, priority) == 0) return 0f;
        return Duration(stream) / Mathf.Max(0.01f, pitch);
    }

    public uint Start(AudioStream stream, string key, Vector3 position, bool looping, bool positional,
        float volume, float pitch, float minDistance, float maxDistance,
        SoundChannel channel = SoundChannel.Effect, int priority = 50)
    {
        if (stream == null) return 0;
        Reclaim();
        if (!looping && CountSameFile(key) >= MaxSameFile) return 0;
        if (CountChannel(channel) >= ChannelLimit(channel) && !Preempt(channel, priority, true)) return 0;
        if (_active.Count >= MaxSystemVoices && !Preempt(null, priority, false)) return 0;

        Voice voice = TakeVoice(positional);
        if (voice == null) return 0;

        voice.Id = ++_nextPlaybackId;
        voice.Channel = channel;
        voice.Priority = priority;
        voice.Key = key;
        voice.Looping = looping;
        voice.Positional = positional;
        voice.Active = true;

        AudioStream playable = looping ? Loopable(stream) : stream;
        if (positional)
        {
            voice.Three.Stream = playable;
            voice.Three.GlobalPosition = position;
            voice.Three.UnitSize = Mathf.Max(1f, minDistance);
            voice.Three.MaxDistance = Mathf.Max(minDistance + 1f, maxDistance);
            voice.Three.VolumeDb = War3Volume.ToDecibels(volume * _volume);
            voice.Three.PitchScale = Mathf.Max(0.01f, pitch);
            voice.Three.Play();
        }
        else
        {
            voice.Flat.Stream = playable;
            voice.Flat.VolumeDb = War3Volume.ToDecibels(volume * _volume);
            voice.Flat.PitchScale = Mathf.Max(0.01f, pitch);
            voice.Flat.Play();
        }

        _active.Add(voice);
        Log(key, positional ? position : null, stream, pitch);
        return voice.Id;
    }

    public void Stop(uint playbackId)
    {
        Voice voice = Find(playbackId);
        if (voice == null) return;
        voice.StopPlaying();
        voice.Active = false;
        _active.Remove(voice);
    }

    public bool IsPlaying(uint playbackId)
    {
        Voice voice = Find(playbackId);
        return voice != null && voice.Playing;
    }

    public void SetPosition(uint playbackId, Vector3 position)
    {
        Voice voice = Find(playbackId);
        if (voice?.Three != null && voice.Positional) voice.Three.GlobalPosition = position;
    }

    public void Update(float delta)
    {
        Reclaim();
    }

    private Voice Find(uint playbackId)
    {
        if (playbackId == 0) return null;
        foreach (Voice voice in _active)
        {
            if (voice.Id == playbackId) return voice;
        }
        return null;
    }

    private void Reclaim()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Voice voice = _active[i];
            if (voice.Playing) continue;
            voice.Active = false;
            _active.RemoveAt(i);
        }
    }

    private static int ChannelLimit(SoundChannel channel) =>
        (int)channel >= 0 && (int)channel < ChannelMax.Length ? ChannelMax[(int)channel] : 4;

    private int CountSameFile(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        int count = 0;
        foreach (Voice voice in _active)
        {
            if (!voice.Looping && string.Equals(voice.Key, key, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private int CountChannel(SoundChannel channel)
    {
        int count = 0;
        foreach (Voice voice in _active)
        {
            if (voice.Channel == channel) count++;
        }
        return count;
    }

    private bool Preempt(SoundChannel? channel, int newPriority, bool sameChannel)
    {
        Voice victim = null;
        foreach (Voice voice in _active)
        {
            if (voice.Priority >= newPriority) continue;
            if (sameChannel && voice.Channel != channel.Value) continue;
            if (victim == null || voice.Priority < victim.Priority) victim = voice;
        }
        if (victim == null) return false;
        victim.StopPlaying();
        victim.Active = false;
        _active.Remove(victim);
        return true;
    }

    private Voice TakeVoice(bool positional)
    {
        foreach (Voice voice in _voices)
        {
            if (voice.Active) continue;
            if (positional ? voice.Three != null : voice.Flat != null) return voice;
        }

        int count = 0;
        foreach (Voice voice in _voices)
        {
            if (positional ? voice.Three != null : voice.Flat != null) count++;
        }
        if (count >= MaxVoicesPerKind) return null;

        if (positional)
        {
            var player = new AudioStreamPlayer3D
            {
                Name = $"Voice3D{count}",
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                UnitSize = 900f,
                MaxDistance = 4500f,
                PanningStrength = 0.6f,
                MaxPolyphony = 1,
            };
            _parent.AddChild(player);
            var voice = new Voice { Three = player };
            _voices.Add(voice);
            return voice;
        }

        var flat = new AudioStreamPlayer { Name = $"Voice2D{count}" };
        _parent.AddChild(flat);
        var created = new Voice { Flat = flat };
        _voices.Add(created);
        return created;
    }

    private AudioStream Loopable(AudioStream stream)
    {
        if (_loopStreams.TryGetValue(stream, out AudioStream cached)) return cached;
        AudioStream looped = stream;
        if (stream is AudioStreamWav wav)
        {
            var copy = (AudioStreamWav)wav.Duplicate();
            copy.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            copy.LoopBegin = 0;
            copy.LoopEnd = Mathf.Max(1, (int)(copy.GetLength() * copy.MixRate));
            looped = copy;
        }
        else if (stream is AudioStreamMP3 mp3)
        {
            var copy = (AudioStreamMP3)mp3.Duplicate();
            copy.Loop = true;
            looped = copy;
        }
        _loopStreams[stream] = looped;
        return looped;
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
}