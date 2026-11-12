using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>漫游（Awan）：野怪没敌人、没指令时在营地附近随机走动（对标客户端 RuntimeWanderAbility）。</summary>
public sealed class Wander : Ability
{
    public override bool IsPassive => true;

    public float Radius => Area > 0f ? Area : 400f;

    private float _timer;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;
        if (Owner.IsBuilding || Owner.AutoAcquire) return;
        if (Owner.Target != null || Owner.MoveTarget.HasValue) return;
        if (Owner.State.Stunned || Owner.State.Snared || Owner.IsChanneling) return;

        _timer -= delta;
        if (_timer > 0f) return;
        _timer = 6f;

        int steps = world.RandomIndex(5) + 1;
        float angle = world.RandomIndex(8) * 0.7853982f;
        float distance = Radius * (0.3f + 0.14f * steps);
        Owner.MoveTarget = Owner.Home + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
    }
}
