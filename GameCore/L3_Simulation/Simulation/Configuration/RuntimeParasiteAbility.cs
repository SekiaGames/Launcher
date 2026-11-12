using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 寄生虫（ANpa / ACpa）。对标客户端 RuntimeParasiteAbility：
/// 命中挂一个每秒 DataA 点伤害的 Buff，目标在持续时间内死亡时召出 DataE 只 UnitID 小虫。
/// </summary>
public sealed class RuntimeParasiteAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float DamagePerSecond => Data('A');
    public int SpawnCount => Mathf.Max(1, (int)Data('E'));
    public string SpawnUnitId => Type?.SummonAt(Level) is { Length: >= 4 } id ? id : "ncfs";

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (IsHostile(world, target) == false) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        world.ApplyBuff(target, new ParasiteBuff
        {
            SourceAbility = this,
            Summoner = Owner,
            DamagePerSecond = DamagePerSecond,
            SpawnUnitId = SpawnUnitId,
            SpawnCount = SpawnCount,
        }, EffectiveDuration);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null) return false;
        Unit enemy = FindEnemyInRange(world, accept: unit => unit.FindBuff<ParasiteBuff>() == null);
        if (enemy == null) return false;
        return world.TryCast(Owner, this, enemy, enemy.Position);
    }
}
