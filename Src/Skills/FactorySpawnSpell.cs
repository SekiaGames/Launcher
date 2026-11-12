using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>工厂的出兵节奏：工厂活着就每隔 Interval 放一只地精，工厂没了/时间到就停。</summary>
public sealed class FactorySpawnSpell : IGroundSpell
{
    public Unit Factory;
    public string GoblinId = "";
    public float Remaining;
    public float Interval = 4f;
    public float Offset = 64f;
    public float GoblinLifetime;

    private float _timer = 1f;
    private float _angle;

    public bool Step(World world, float delta)
    {
        if (Factory == null || !Factory.Alive || world == null) return false;

        Remaining -= delta;
        if (Remaining <= 0f) return false;

        _timer -= delta;
        if (_timer > 0f) return true;
        _timer = Interval;
        if (GoblinId.Length < 4) return true;

        _angle += 1.1f;
        var spot = new Vector3(
            Factory.Position.X + Mathf.Cos(_angle) * Offset,
            Factory.Position.Y,
            Factory.Position.Z + Mathf.Sin(_angle) * Offset);
        world.Summon(Factory, GoblinId, spot, GoblinLifetime);
        return true;
    }
}
