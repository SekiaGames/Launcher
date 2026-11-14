using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class War3SplatSystem
{
    private sealed class Instance
    {
        public War3ModelEventData Data;
        public Vector2 MapPosition;
        public float Age;
    }

    private readonly List<Instance> _instances = new();
    private readonly Dictionary<string, List<ShadowRenderer.ColoredQuad>> _batches = new(StringComparer.Ordinal);
    private readonly List<string> _batchOrder = new();
    private readonly HashSet<string> _frameKeys = new(StringComparer.Ordinal);

    public int Count => _instances.Count;

    public static float WorldSize(War3ModelEventData data) => Mathf.Max(16f, data.Scale * 2f);

    public static Color ColorAt(War3ModelEventData data, float age)
    {
        age = Mathf.Max(0f, age);
        if (data.BirthTime + data.PauseTime + data.DecayTime > 0f)
        {
            if (age < data.BirthTime)
            {
                return data.StartColor.Lerp(data.MiddleColor, age / data.BirthTime);
            }
            age -= data.BirthTime;
            if (age < data.PauseTime)
            {
                return data.MiddleColor;
            }
            age -= data.PauseTime;
            if (data.DecayTime > 0f)
            {
                return data.MiddleColor.Lerp(data.EndColor, Mathf.Clamp(age / data.DecayTime, 0f, 1f));
            }
            return data.EndColor;
        }
        Color color = data.MiddleColor;
        color.A *= data.LifeSpan > 0f ? Mathf.Clamp(1f - age / data.LifeSpan, 0f, 1f) : 1f;
        return color;
    }

    public void Add(War3ModelEventData data, Vector3 worldPosition)
    {
        if (!data.IsGroundSplat || string.IsNullOrWhiteSpace(data.Resource))
        {
            return;
        }
        _instances.Add(new Instance
        {
            Data = data,
            MapPosition = new Vector2(0f - worldPosition.X, worldPosition.Z),
        });
    }

    public void Update(float delta)
    {
        if (_instances.Count == 0)
        {
            return;
        }
        float step = Mathf.Max(0f, delta);
        for (int index = _instances.Count - 1; index >= 0; index--)
        {
            Instance instance = _instances[index];
            instance.Age += step;
            if (instance.Data.LifeSpan > 0f && instance.Age >= instance.Data.LifeSpan)
            {
                _instances.RemoveAt(index);
            }
        }
    }

    public void Draw(ShadowRenderer renderer)
    {
        if (_instances.Count == 0)
        {
            return;
        }

        foreach (List<ShadowRenderer.ColoredQuad> existing in _batches.Values)
        {
            existing.Clear();
        }
        _batchOrder.Clear();
        _frameKeys.Clear();

        foreach (Instance instance in _instances)
        {
            string key = instance.Data.Resource + "|" + instance.Data.BlendMode;
            if (!_batches.TryGetValue(key, out List<ShadowRenderer.ColoredQuad> batch))
            {
                batch = new List<ShadowRenderer.ColoredQuad>();
                _batches[key] = batch;
            }
            if (_frameKeys.Add(key))
            {
                _batchOrder.Add(key);
            }
            float size = WorldSize(instance.Data);
            float half = size * 0.5f;
            batch.Add(new ShadowRenderer.ColoredQuad(
                new ShadowRenderer.Quad(
                    instance.MapPosition.X - half,
                    instance.MapPosition.Y - half,
                    instance.MapPosition.X + half,
                    instance.MapPosition.Y + half),
                ColorAt(instance.Data, instance.Age)));
        }

        foreach (string key in _batchOrder)
        {
            List<ShadowRenderer.ColoredQuad> batch = _batches[key];
            if (batch.Count == 0)
            {
                continue;
            }
            int separator = key.LastIndexOf('|');
            string texture = key[..separator];
            int blend = int.TryParse(key[(separator + 1)..], out int parsed) ? parsed : 0;
            renderer.DrawColored(batch, texture, ShadowRenderer.GroundSplatOffset, blend);
        }
    }
}
