using Godot;

namespace OpenWC3;

/// <summary>
/// 剑刃风暴 AOww（剑圣终极）。引导 HeroDur(5) 秒，每秒对 Area(200) 内地面敌人造成 DataA(110) 伤害。
/// 物编：Cool=180 Cost=200。
/// </summary>
public sealed class Bladestorm : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public override bool IsChanneled => true;
    public override float ChannelWaveInterval => 1f;
    public override float ChannelTotal => EffectiveDuration;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;
        return world.BeginChannel(Owner, this, Owner.Position, ChannelTotal, ChannelWaveInterval);
    }

    public override void OnChannelWave(World world, Vector3 point)
    {
        if (world == null || Owner == null) return;

        foreach (Unit victim in world.EnemiesInRange(Owner, Owner.Position, Area))
        {
            if (victim.Type != null && victim.Type.Flying) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('A'), AttackType.Spells, DamageType.Magic));
        }
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null) return false;
        int nearby = 0;
        bool anyHero = false;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other.Type == null || other.Type.Flying) continue;
            if (other.Gold > 0) continue;
            if (world.AreAllied(Owner.Player, other.Player)) continue;
            if (Owner.DistanceTo(other) > Area + other.Radius) continue;
            nearby++;
            if (other.IsHero) anyHero = true;
        }
        if (nearby < 2 && !anyHero) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
