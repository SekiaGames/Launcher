using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>掘尸（Aexh）：每 Dur 秒往货舱里补一具 UnitID 尸体，货舱满或到 DataA 上限就停。</summary>
public sealed class RuntimeExhumeAbility : Ability
{
    public override bool IsPassive => true;

    public float Interval => Duration > 0f ? Duration : 15f;
    public int MaxCorpses => (int)Data('A');
    public string CorpseUnitId => Type?.SummonAt(Level) ?? "ugho";

    private float _elapsed;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        OpenWC3.RuntimeMeatLoadAbility cargo = Owner.FindPassive<OpenWC3.RuntimeMeatLoadAbility>() as OpenWC3.RuntimeMeatLoadAbility;
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
