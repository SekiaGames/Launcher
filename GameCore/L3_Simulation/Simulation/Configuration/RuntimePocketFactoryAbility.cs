using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>口袋工厂（ANsy）：落点召唤工厂，工厂按 DataA 间隔放出 DataB 地精（地精寿命 DataC、出位 DataD）。</summary>
public sealed class SummonFactory : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;

    public string FactoryId => Type?.SummonAt(Level) is { Length: >= 4 } id ? id : "nfac";
    public string GoblinId => (Type?.DataTextAt(Level, 'B') ?? "").Trim();
    public float Lifetime => Duration > 0f ? Duration : 40f;
    public float GoblinLifetime => Data('C');
    public float SpawnInterval => Data('A') > 0f ? Data('A') : 4f;
    public float SpawnOffset => Data('D') > 0f ? Data('D') : 64f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || world == null) return false;
        float range = CastRange > 0f ? CastRange : 500f;
        return FlatDistance(Owner.Position, point) <= range + 64f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Unit factory = world.Summon(Owner, FactoryId, Flatten(point), Lifetime);
        if (factory == null) return false;

        world.AddGroundSpell(new FactorySpawnSpell
        {
            Factory = factory,
            GoblinId = GoblinId,
            Remaining = Lifetime,
            Interval = SpawnInterval,
            Offset = SpawnOffset,
            GoblinLifetime = GoblinLifetime,
        });
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
