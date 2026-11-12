using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class RuntimeStampedeAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;
    public override bool IsChanneled => true;

    public float Damage => Data('B');
    public float Radius => Data('D') > 0f ? Data('D') : 275f;
    public float WaveInterval => Data('E') > 0f ? Data('E') : 0.2f;

    private int _wave;

    public override float ChannelWaveInterval => WaveInterval;
    public override float ChannelTotal => Duration > 0f ? Duration : 30f;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        _wave = 0;
        return world.BeginChannel(Owner, this, Flatten(point), ChannelTotal, WaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        _wave++;
        if (_wave % 5 != 0) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, point, Radius, false))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage, AttackType.Siege, DamageType.Magic));
        }
    }

    public override void OnChannelEnded(World world, bool interrupted)
    {
        _wave = 0;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, target.Position);
    }
}
