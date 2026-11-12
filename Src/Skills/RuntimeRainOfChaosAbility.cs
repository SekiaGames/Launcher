using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 混乱之雨（Anrc / ANr3）。对标客户端 RuntimeRainOfChaosAbility：
/// DataA 是地狱火技能码（ANin），在 Area(900) 内随机砸下 DataB 只地狱火单位；
/// 每颗落地按 ANin 的 DataA 伤害 / Area 半径 / Dur 眩晕，召唤物寿命取 ANin 的 DataB。
/// </summary>
public sealed class RainOfChaos : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public int Count => Mathf.Max(1, (int)Data('B'));
    public float Radius => Area > 0f ? Area : 900f;

    public string InfernalAbilityId
    {
        get
        {
            string raw = (Type?.DataTextAt(Level, 'A') ?? "").Split(',')[0].Trim();
            if (raw.Length >= 4) return raw[..4];
            string own = Type?.SummonAt(Level) ?? "";
            return own.Length >= 4 ? own[..4] : "ANin";
        }
    }

    private AbilityType InfernalData => AbilityCatalog.Get(InfernalAbilityId);

    public string InfernalId
    {
        get
        {
            string unit = InfernalData?.SummonAt(1) ?? "";
            unit = unit.Trim();
            if (unit.Length >= 4 && unit != "-") return unit[..4];
            string own = Type?.SummonAt(Level) ?? "";
            own = own.Trim();
            return own.Length >= 4 && own != "-" ? own[..4] : "ninf";
        }
    }

    public float ImpactDamage => InfernalData?.DataAt(1, 'A') ?? 0f;
    public float ImpactRadius => InfernalData?.AreaAt(1) ?? 0f;
    public float ImpactStun => InfernalData?.DurationAt(1) ?? 0f;
    public float ImpactStunOnHero => InfernalData?.HeroDurationAt(1) ?? ImpactStun;
    public float SummonSeconds => InfernalData?.DataAt(1, 'B') ?? 0f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        float impactRadius = ImpactRadius > 0f ? ImpactRadius : 200f;
        int summoned = 0;
        for (int index = 0; index < Count; index++)
        {
            float angle = (world.RandomIndex(16) / 16f) * Mathf.Tau;
            float distance = Radius * (0.25f + 0.05f * world.RandomIndex(10));
            Vector3 spot = Owner.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            if (ImpactDamage > 0f)
            {
                foreach (Unit victim in world.EnemiesInRange(Owner, spot, impactRadius))
                {
                    world.DealDamage(World.MakeAbilityDamage(Owner, victim, ImpactDamage, AttackType.Spells, DamageType.Magic));
                }
            }
            if (ImpactStun > 0f)
            {
                foreach (Unit victim in world.EnemiesInRange(Owner, spot, impactRadius))
                {
                    victim.State.Add(StateMod.Stun, victim.IsHero ? ImpactStunOnHero : ImpactStun);
                }
            }

            if (world.Summon(Owner, InfernalId, spot, SummonSeconds) != null) summoned++;
        }
        return summoned > 0;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
