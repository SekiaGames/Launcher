using Godot;
using System.Collections.Generic;

namespace OpenWC3;

public sealed class RuntimeFreezingBreathAbility : Ability
{
    public override bool IsPassive => true;

    public float MoveSlowPct => 0.5f;
    public float AttackSlowPct => 0.5f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;
        if (!info.Target.IsBuilding) return;

        float seconds = Duration > 0f ? Duration : 5f;
        info.Target.ApplySlow(MoveSlowPct, AttackSlowPct, seconds);
    }
}
