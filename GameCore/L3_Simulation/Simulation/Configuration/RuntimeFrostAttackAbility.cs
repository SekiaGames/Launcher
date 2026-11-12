using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeFrostAttackAbility : Ability
{
    public override bool IsPassive => true;

    public float MoveSlowPct => 0.5f;
    public float AttackSlowPct => 0.25f;

    public override void OnAttackLanded(World world, DamageData info)
    {
        if (world == null || Owner == null || info?.Target == null || !info.Target.Alive) return;

        float seconds = info.Target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        info.Target.ApplySlow(MoveSlowPct, AttackSlowPct, seconds > 0f ? seconds : 10f);

        float radius = Area;
        if (radius <= 0f) return;

        foreach (Unit other in world.EnemiesInRange(Owner, info.Target.Position, radius, false))
        {
            if (other == info.Target) continue;
            other.ApplySlow(MoveSlowPct, AttackSlowPct, seconds > 0f ? seconds : 10f);
        }
    }
}
