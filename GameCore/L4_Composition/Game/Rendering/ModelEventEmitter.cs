using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class ModelEventEmitter
{
    private sealed class Track
    {
        public string Node;
        public string Name;
        public float[] Times;
        public Node3D Target;
    }

    private readonly Node3D _root;
    private readonly AnimationPlayer _player;
    private readonly Dictionary<string, List<Track>> _byClip = new(StringComparer.Ordinal);
    private string _lastClip;
    private float _lastTime = -1f;

    private ModelEventEmitter(Node3D root, AnimationPlayer player)
    {
        _root = root;
        _player = player;
        _lastClip = player.CurrentAnimation;
    }

    public static ModelEventEmitter Create(Node3D root)
    {
        if (root == null || !root.HasMeta("war3_model_events"))
        {
            return null;
        }

        var player = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (player == null)
        {
            return null;
        }

        var emitter = new ModelEventEmitter(root, player);
        Variant meta = root.GetMeta("war3_model_events");
        foreach (string entry in meta.AsStringArray())
        {
            if (!emitter.Parse(entry))
            {
                continue;
            }
        }
        return emitter._byClip.Count == 0 ? null : emitter;
    }

    private bool Parse(string entry)
    {
        string[] parts = entry.Split('|');
        if (parts.Length != 4)
        {
            return false;
        }

        string[] times = parts[3].Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (times.Length == 0)
        {
            return false;
        }

        var values = new float[times.Length];
        for (int index = 0; index < times.Length; index++)
        {
            if (!float.TryParse(times[index], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out values[index]))
            {
                return false;
            }
        }

        string clip = parts[0];
        if (!_byClip.TryGetValue(clip, out List<Track> tracks))
        {
            tracks = new List<Track>();
            _byClip[clip] = tracks;
        }
        tracks.Add(new Track
        {
            Node = parts[1],
            Name = parts[2],
            Times = values,
            Target = _root.FindChild(parts[1], true, false) as Node3D,
        });
        return true;
    }

    public void Tick(Action<War3ModelEventData, Vector3> fire, bool emit = true)
    {
        if (fire == null || _player == null || _byClip.Count == 0)
        {
            return;
        }

        string clip = _player.CurrentAnimation;
        if (string.IsNullOrEmpty(clip))
        {
            return;
        }

        float time = (float)_player.CurrentAnimationPosition;
        bool changed = _lastClip != clip;
        bool wrapped = !changed && time < _lastTime;
        if (_byClip.TryGetValue(clip, out List<Track> tracks))
        {
            foreach (Track track in tracks)
            {
                foreach (float at in track.Times)
                {
                    bool crossed = changed
                        ? at <= time
                        : wrapped
                            ? at > _lastTime || at <= time
                            : at > _lastTime && at <= time;
                    if (!crossed || !emit)
                    {
                        continue;
                    }
                    War3ModelEventData data = War3EventObjectData.Resolve(track.Name);
                    if (!data.IsGroundSplat)
                    {
                        continue;
                    }
                    fire(data, PositionOf(track));
                }
            }
        }

        _lastClip = clip;
        _lastTime = time;
    }

    private Vector3 PositionOf(Track track)
    {
        Node3D target = track.Target;
        if (target != null && GodotObject.IsInstanceValid(target))
        {
            return target.GlobalPosition;
        }
        return GodotObject.IsInstanceValid(_root) ? _root.GlobalPosition : Vector3.Zero;
    }
}
