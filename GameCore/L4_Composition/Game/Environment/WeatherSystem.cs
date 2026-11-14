using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class WeatherSystem
{
    private readonly Node _parent;
    private readonly Func<float, float, float> _terrainHeight;
    private readonly List<WeatherRenderer> _renderers = new();

    public WeatherSystem(Node parent, W3iEnvironment environment,
        Func<float, float, float> terrainHeight)
    {
        _parent = parent;
        _terrainHeight = terrainHeight;
        string global = environment?.GlobalWeather?.Trim('\0', ' ');
        if (!string.IsNullOrEmpty(global))
        {
            AddGlobal(global);
        }
    }

    public IReadOnlyList<WeatherRenderer> Renderers => _renderers;

    public WeatherRenderer AddGlobal(string effectId)
    {
        WeatherRenderer renderer = WeatherRenderer.TryCreate(_parent, effectId, _terrainHeight);
        if (renderer == null) return null;
        _renderers.Add(renderer);
        GD.Print($"[WEATHER] {effectId} 全局天气已加载");
        return renderer;
    }

    public WeatherRenderer AddArea(string effectId, Vector3 boundsMin, Vector3 boundsMax,
        bool enabled = false)
    {
        WeatherRenderer renderer = WeatherRenderer.TryCreate(_parent, effectId, _terrainHeight,
            boundsMin, boundsMax, areaSpecified: true);
        if (renderer == null) return null;
        renderer.SetEnabled(enabled);
        _renderers.Add(renderer);
        GD.Print($"[WEATHER] {effectId} 区域天气已创建 "
            + $"({boundsMin.X:0},{boundsMin.Z:0})..({boundsMax.X:0},{boundsMax.Z:0})"
            + $"，{(enabled ? "已启用" : "未启用")}");
        return renderer;
    }

    public void Enable(WeatherRenderer renderer, bool enabled)
    {
        renderer?.SetEnabled(enabled);
    }

    public void Remove(WeatherRenderer renderer)
    {
        if (renderer == null) return;
        renderer.Remove();
        _renderers.Remove(renderer);
    }

    public void FrameSeconds(Vector3 focus)
    {
        foreach (WeatherRenderer renderer in _renderers)
        {
            renderer.FrameSeconds(focus);
        }
    }

    public void SetEnabled(bool enabled)
    {
        foreach (WeatherRenderer renderer in _renderers)
        {
            renderer.SetEnabled(enabled);
        }
    }
}
