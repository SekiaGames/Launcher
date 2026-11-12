using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3;

/// <summary>
/// 坟场产尸（Agyd）：每逢 Dur(15)s 在建筑旁留下一个 UnitID(ugho) 尸体，同时在场的最多 DataA(5) 具。
/// </summary>
public sealed class Graveyard : Ability
{
    public override bool IsPassive => true;

    public float Interval => Duration > 0f ? Duration : 15f;
    public int MaxCorpses => Mathf.Max(1, (int)Data('A'));
    public float Radius => Area > 0f ? Area : 200f;
    public string CorpseUnitId => Type?.SummonAt(Level) ?? "ugho";

    private float _elapsed;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive) return;

        _elapsed += delta;
        if (_elapsed < Interval) return;
        _elapsed = 0f;

        int alive = 0;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse) continue;
            if (!string.Equals(other.Type?.Id, CorpseUnitId, StringComparison.OrdinalIgnoreCase)) continue;
            if (Owner.DistanceTo(other) > Radius * 2f) continue;
            alive++;
        }
        if (alive >= MaxCorpses) return;

        Vector3 spot = Owner.Position + new Vector3(Radius * 0.6f, 0f, Radius * 0.3f);
        Unit corpse = world.CreateFor(Owner, CorpseUnitId, spot);
        if (corpse == null) return;

        corpse.Hp = 0f;
        corpse.EnterDeath();
        if (corpse.View != null) corpse.View.Root.Position = spot;
    }
}
