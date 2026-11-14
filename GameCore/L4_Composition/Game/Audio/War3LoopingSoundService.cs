using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 单位循环行为音：对标客户端 War3LoopingSoundService。移动时播 MovementSoundLabel 循环音
/// 并跟随位置，停下即停；建造开始播一次 BuildingSoundLabel。
/// </summary>
public sealed class War3LoopingSoundService
{
    // 原版脚步属于独立的 UNITMOVEMENT volume group；循环素材按单声源标定，
    // 整队同时播放时仍要留组内余量，否则多个脚步会直接叠加过响。
    private const float MovementGain = 0.45f;

    private readonly War3AudioSystem _audio;
    private readonly Func<float, float, float> _terrainHeight;
    private readonly Dictionary<string, War3UnitSound> _moveCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, War3UnitSound> _buildCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, MoveVoice> _moveVoices = new();
    private readonly HashSet<int> _buildSoundPlayed = new();
    private readonly HashSet<int> _activeUnits = new();
    private readonly List<int> _deadMoveVoices = new();

    private sealed class MoveVoice
    {
        public uint Source;
        public float LastX, LastZ;
        public bool Active;
    }

    public War3LoopingSoundService(War3AudioSystem audio, Func<float, float, float> terrainHeight)
    {
        _audio = audio;
        _terrainHeight = terrainHeight;
    }

    public void Update(IEnumerable<Unit> units, float delta, Func<Unit, bool> audible = null)
    {
        if (_audio == null || units == null) return;
        _activeUnits.Clear();
        if (units is IReadOnlyList<Unit> indexed)
        {
            for (int index = 0; index < indexed.Count; index++)
                UpdateAudibleUnit(indexed[index], audible);
        }
        else
        {
            foreach (Unit unit in units) UpdateAudibleUnit(unit, audible);
        }

        _deadMoveVoices.Clear();
        foreach (int id in _moveVoices.Keys)
            if (!_activeUnits.Contains(id)) _deadMoveVoices.Add(id);
        for (int i = 0; i < _deadMoveVoices.Count; i++)
        {
            int id = _deadMoveVoices[i];
            if (_moveVoices[id].Active) _audio.Stop(_moveVoices[id].Source);
            _moveVoices.Remove(id);
        }
        _buildSoundPlayed.RemoveWhere(id => !_activeUnits.Contains(id));
    }

    private void UpdateAudibleUnit(Unit unit, Func<Unit, bool> audible)
    {
        if (unit == null || unit.Removed || !unit.Alive) return;
        if (audible != null && !audible(unit)) return;
        int simId = unit.SimulationId;
        _activeUnits.Add(simId);
        UpdateMoveVoice(unit, simId);
        UpdateBuildVoice(unit, simId);
    }

    private War3UnitSound GetSound(string label, Dictionary<string, War3UnitSound> cache)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        if (cache.TryGetValue(label, out War3UnitSound cached)) return cached;
        War3SoundTables.TryGet(label, out War3SoundTables.Entry entry);
        War3UnitSound sound = War3UnitSound.Create(entry);
        cache[label] = sound;
        return sound;
    }

    private Vector3 PositionOf(Unit unit)
        => new(unit.Position.X, _terrainHeight(unit.Position.X, unit.Position.Z), unit.Position.Z);

    private void UpdateMoveVoice(Unit unit, int simId)
    {
        War3UnitSound sound = GetSound(unit.Type?.MovementSoundLabel, _moveCache);
        float x = unit.Position.X;
        float z = unit.Position.Z;
        if (sound == null || sound.IsSilent)
        {
            if (_moveVoices.TryGetValue(simId, out MoveVoice silent) && silent.Active)
            {
                _audio.Stop(silent.Source);
                _moveVoices.Remove(simId);
            }
            return;
        }

        if (!_moveVoices.TryGetValue(simId, out MoveVoice voice))
        {
            _moveVoices[simId] = new MoveVoice { LastX = x, LastZ = z };
            return;
        }

        // 语音/战斗可能因声部上限抢占脚步；同步真实播放状态，否则之后不会恢复。
        if (voice.Active && !_audio.IsPlaying(voice.Source))
        {
            voice.Source = 0;
            voice.Active = false;
        }

        float dx = x - voice.LastX;
        float dz = z - voice.LastZ;
        bool moving = dx * dx + dz * dz > 0.1f;
        if (moving && !voice.Active)
        {
            voice.Source = sound.PlayLooping(_audio, PositionOf(unit), SoundChannel.Movement, 30,
                gain: MovementGain);
            voice.Active = voice.Source != 0;
        }
        else if (!moving && voice.Active)
        {
            _audio.Stop(voice.Source);
            voice.Source = 0;
            voice.Active = false;
        }
        else if (moving && voice.Active)
        {
            _audio.SetPosition(voice.Source, PositionOf(unit));
        }

        voice.LastX = x;
        voice.LastZ = z;
    }

    private void UpdateBuildVoice(Unit unit, int simId)
    {
        bool constructing = unit.IsBuilding && unit.IsUnderConstruction;
        if (!constructing)
        {
            _buildSoundPlayed.Remove(simId);
            return;
        }
        if (_buildSoundPlayed.Contains(simId)) return;

        War3UnitSound sound = GetSound(unit.Type?.BuildingSoundLabel, _buildCache);
        if (sound == null || sound.IsSilent) return;
        // 建造开始只播一遍并整体衰减（实测建造期循环过吵）。
        sound.PlayOneShot(_audio, PositionOf(unit), SoundChannel.Ambient, 30, gain: 0.5f);
        _buildSoundPlayed.Add(simId);
    }
}