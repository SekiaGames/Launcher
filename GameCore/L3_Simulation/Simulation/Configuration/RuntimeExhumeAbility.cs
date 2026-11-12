using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>掘尸（Aexh）：每 Dur 秒往货舱里补一具 UnitID 尸体，货舱满或到 DataA 上限就停。</summary>
public sealed class Exhume : Ability
{
    public override bool IsPassive => true;

    public float Interval => Duration > 0f ? Duration : 15f;
    public int MaxCorpses => (int)Data('A');
    public string CorpseUnitId => Type?.SummonAt(Level) ?? "ugho";

    private float _elapsed;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        MeatCargo cargo = Owner.FindPassive<MeatCargo>() as MeatCargo;
        if (cargo == null) return;
        if (cargo.CorpseCount >= Mathf.Min(cargo.Capacity, Mathf.Max(1, MaxCorpses))) return;

        _elapsed += delta;
        if (_elapsed < Interval) return;
        _elapsed = 0f;

        Unit corpse = world.CreateFor(Owner, CorpseUnitId, Owner.Position);
        if (corpse == null) return;
        corpse.Hp = 0f;
        corpse.EnterDeath();
        if (!cargo.Store(corpse)) world.RemoveUnit(corpse);
    }

    public override void OnOwnerDeath(World world) => _elapsed = 0f;
}
