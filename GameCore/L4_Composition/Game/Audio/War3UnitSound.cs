using Godot;
using System;

namespace OpenWC3.Game;

public sealed class War3UnitSound
{
    public static readonly War3UnitSound Silent = new();

    private readonly War3SoundTables.Entry _entry;
    private int _lastIndex = -1;

    private War3UnitSound() { }

    private War3UnitSound(War3SoundTables.Entry entry)
    {
        _entry = entry;
        if (entry == null) return;
        if (entry.Flags.Contains("NODUPEUSERNAMES", StringComparison.OrdinalIgnoreCase))
            ResponseUsername = entry.Priority;
    }

    internal int? ResponseUsername { get; }

    public int Count => _entry?.Files.Length ?? 0;

    public bool IsSilent => _entry == null || _entry.Files.Length == 0;

    public static War3UnitSound Create(War3SoundTables.Entry entry)
        => entry == null ? Silent : new War3UnitSound(entry);

    private int SelectFileIndex(int index)
    {
        if (_entry == null || _entry.Files.Length == 0 || index >= _entry.Files.Length) return -1;
        if (index >= 0)
        {
            _lastIndex = index;
            return index;
        }
        int selected = 0;
        if (_entry.Files.Length > 1)
        {
            for (int attempt = 0; attempt < 11; attempt++)
            {
                selected = Random.Shared.Next(_entry.Files.Length);
                if (selected != _lastIndex) break;
            }
        }
        _lastIndex = selected;
        return selected;
    }

    private float PickPitch()
    {
        if (_entry == null) return 1f;
        if (!_entry.RandomPitch || _entry.PitchVariance <= 0f) return _entry.Pitch;
        return _entry.Pitch
            + (float)Random.Shared.NextDouble() * _entry.PitchVariance * 2f
            - _entry.PitchVariance;
    }

    public float Play2D(War3AudioSystem audio, float gain = 1f, int index = -1,
        SoundChannel channel = SoundChannel.Ui, int priority = 10)
    {
        if (audio == null || IsSilent) return 0f;
        int file = SelectFileIndex(index);
        if (file < 0) return 0f;
        AudioStream stream = War3SoundTables.PickStream(_entry, file);
        if (stream == null) return 0f;
        float pitch = Mathf.Max(0.01f, PickPitch());
        return audio.Play2D(stream, _entry.Name,
            _entry.Volume * Mathf.Clamp(gain, 0f, 1f), pitch, channel, priority);
    }

    public float Play3D(War3AudioSystem audio, Vector3 position, float gain = 1f, int index = -1,
        SoundChannel channel = SoundChannel.Unit, int priority = 50)
    {
        if (audio == null || IsSilent) return 0f;
        int file = SelectFileIndex(index);
        if (file < 0) return 0f;
        AudioStream stream = War3SoundTables.PickStream(_entry, file);
        if (stream == null) return 0f;
        float pitch = Mathf.Max(0.01f, PickPitch());
        return audio.Play3D(stream, _entry.Name, position,
            _entry.Volume * Mathf.Clamp(gain, 0f, 1f), pitch,
            Mathf.Max(1f, _entry.MinDistance),
            Mathf.Max(_entry.MinDistance + 1f, _entry.MaxDistance), channel, priority);
    }

    public uint PlayLooping(War3AudioSystem audio, Vector3 position,
        SoundChannel channel = SoundChannel.Ambient, int priority = 30, int index = -1, float gain = 1f)
    {
        if (audio == null || IsSilent) return 0;
        int file = SelectFileIndex(index);
        if (file < 0) return 0;
        AudioStream stream = War3SoundTables.PickStream(_entry, file);
        if (stream == null) return 0;
        float pitch = Mathf.Max(0.01f, PickPitch());
        return audio.Start(stream, _entry.Name, position, looping: true, positional: true,
            _entry.Volume * Mathf.Clamp(gain, 0f, 1f), pitch,
            Mathf.Max(1f, _entry.MinDistance),
            Mathf.Max(_entry.MinDistance + 1f, _entry.MaxDistance), channel, priority);
    }

    public void PlayOneShot(War3AudioSystem audio, Vector3 position,
        SoundChannel channel = SoundChannel.Ambient, int priority = 30, float gain = 1f)
    {
        if (audio == null || IsSilent) return;
        int file = SelectFileIndex(-1);
        if (file < 0) return;
        AudioStream stream = War3SoundTables.PickStream(_entry, file);
        if (stream == null) return;
        float pitch = Mathf.Max(0.01f, PickPitch());
        audio.Start(stream, _entry.Name, position, looping: false, positional: true,
            _entry.Volume * Mathf.Clamp(gain, 0f, 1f), pitch,
            Mathf.Max(1f, _entry.MinDistance),
            Mathf.Max(_entry.MinDistance + 1f, _entry.MaxDistance), channel, priority);
    }
}
