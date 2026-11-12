using Godot;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>一块临时视野（远视这类技能）：按玩家记录圆心/半径/剩余时间。</summary>
public sealed class FogReveal
{
    public int Player;
    public Vector3 Center;
    public float Radius;
    public float Remaining;
}

public sealed partial class World
{
    public readonly List<FogReveal> Reveals = new();

    public void AddFogReveal(int player, Vector3 center, float radius, float seconds)
    {
        if (radius <= 0f) return;
        Reveals.Add(new FogReveal
        {
            Player = player,
            Center = center,
            Radius = radius,
            Remaining = Mathf.Max(Tick, seconds),
        });
    }

    public void TickFogReveals()
    {
        for (int index = Reveals.Count - 1; index >= 0; index--)
        {
            FogReveal reveal = Reveals[index];
            reveal.Remaining -= Tick;
            if (reveal.Remaining <= 0f) Reveals.RemoveAt(index);
        }
    }
}