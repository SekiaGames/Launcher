using Godot;

namespace Launcher;

/// <summary>
/// 死亡之指 ANfd（阿克蒙德）。对标客户端 Src/Skills/CampaignAndAuraSkills.cs 的 FingerOfDeath：
/// 单体 DataC（500）点魔法伤害，物编 Cool=15 Rng=800，只打非英雄单位与建筑。
/// </summary>
public sealed class FingerOfDeath : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Damage => Data('C');

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.State.MagicImmune) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        world.DealDamage(World.MakeAbilityDamage(Owner, target, Damage, AttackType.Spells, DamageType.Magic));
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other => !other.State.MagicImmune);
        return target != null && world.TryCast(Owner, this, target, target.Position);
    }
}