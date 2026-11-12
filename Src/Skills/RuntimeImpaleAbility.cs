using Godot;

namespace OpenWC3;

/// <summary>
/// 穿刺 AUim（地穴领主）。朝落点射一条地刺，轨迹上的地面敌人吃 DataC(50/80/110) 伤害并眩晕 Dur(2/3/4) 秒。
/// 物编：Cool=9 Cost=100 Rng=700 Area=250 DataA=600（长度）DataB=0.3（尖刺推进间隔）。
/// </summary>
public sealed class RuntimeImpaleAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        return FlatDistance(Owner.Position, point) <= Mathf.Max(1f, CastRange) + 120f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Vector3 from = Owner.Position;
        Vector3 landing = Flatten(point);
        float distance = FlatDistance(from, landing);
        float length = Data('A') > 0f ? Data('A') : 600f;
        Vector3 direction = distance < 1f
            ? new Vector3(Mathf.Sin(Owner.Facing), 0f, Mathf.Cos(Owner.Facing))
            : new Vector3(landing.X - from.X, 0f, landing.Z - from.Z) / distance;

        Vector3 end = from + direction * Mathf.Min(length, Mathf.Max(240f, distance));
        foreach (Unit victim in world.EnemiesAlongLine(Owner, from, end, Area, groundOnly: true))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('C'), AttackType.Spells, DamageType.Magic));
            if (victim.Alive) victim.State.Add(StateMod.Stun, Duration);
        }
        return true;
    }

    /// <summary>AI：穿刺带眩晕，按控制技能先出手，朝范围内的敌方（优先英雄）打。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
