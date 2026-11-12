using Godot;

namespace OpenWC3.Game;

public sealed class RuntimePurgeAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public float SlowFactor => Data('A');
    public float SummonDamage => Data('C');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding && !world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        bool hostile = !world.AreAllied(Owner.Player, target.Player);
        // 对敌清掉正面增益，对友清掉负面减益（同 MassDispel 的口径）
        world.DispelBuffs(target, Owner, harmful: !hostile, helpful: hostile, summonDamage: 0f);
        if (!hostile) return true;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        int moveStages = Mathf.Max(1, Mathf.RoundToInt(Data('A')));
        int attackStages = Mathf.Max(0, Mathf.RoundToInt(Data('B')));
        target.AddSlow(1f, attackStages > 0 ? 1f : 0f, seconds, moveStages, attackStages);

        if (SummonDamage > 0f && target.IsSummon)
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, target, SummonDamage,
                AttackType.Spells, DamageType.Magic));
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => other.IsSummon || other.Slowed == false);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
