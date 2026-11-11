using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 雷霆一击 AHtc。对标客户端 Src/Skills/ThunderClap.cs：
/// 无目标，以施法者为中心半径 Area 内的敌人 受伤 DataA + 减速 DataC(移速)/DataD(攻速) 持续 Dur。
/// 物编：Cool=6 Cost=90 Area=250 DataA=60/100/140 DataC=0.5 DataD=0.5 Dur=5。
/// </summary>
public sealed class ThunderClap : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;

    public float Damage => Data('A');
    public float MoveSlowPct => Data('C');
    public float AttackSlowPct => Data('D');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        float radius = Area > 0f ? Area : 250f;
        var victims = new List<Unit>();
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other == Owner) continue;
            if (world.AreAllied(Owner.Player, other.Player)) continue;
            if (other.State.MagicImmune) continue;
            if (Owner.DistanceTo(other) > radius + other.Radius) continue;
            victims.Add(other);
        }

        foreach (Unit victim in victims)
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Damage, AttackType.Spells, DamageType.Universal));
            if (victim.Alive) victim.ApplySlow(MoveSlowPct, AttackSlowPct, Duration);
        }
        return true;
    }
}
