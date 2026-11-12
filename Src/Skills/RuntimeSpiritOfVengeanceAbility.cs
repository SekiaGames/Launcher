using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed class RuntimeSpiritOfVengeanceAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        string list = Type?.DataTextAt(Level, 'A') ?? "";
        int created = 0;
        foreach (string raw in list.Split(',', StringSplitOptions.TrimEntries))
        {
            if (raw.Length < 4) continue;
            if (world.Summon(Owner, raw[..4], Owner.Position, Duration) != null) created++;
        }
        return created > 0;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
