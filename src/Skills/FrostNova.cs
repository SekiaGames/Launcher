using Godot;

namespace Launcher;

/// <summary>
/// 霜冻新星 AUfn（巫妖）。物编 ubertip：主目标吃 DataB(100) 常规伤害，范围内其他敌人吃
/// DataA(50/100/150) 新星伤害，Dur(4/6/8) 秒内减速。
/// 减速比例物编没导出，按原版 50% 移速 / 50% 攻速。
/// </summary>
public sealed class FrostNova : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

    private const float SlowMove = 0.5f;
    private const float SlowAttack = 0.5f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.State.MagicImmune || target.State.Invulnerable) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        Vector3 center = target.Position;
        float primary = Data('B');
        float splash = Data('A');

        world.DealDamage(World.MakeAbilityDamage(Owner, target, primary, AttackType.Spells, DamageType.Magic));
        if (target.Alive) target.ApplySlow(SlowMove, SlowAttack, Duration);

        foreach (Unit other in world.EnemiesInRange(Owner, center, Area))
        {
            if (ReferenceEquals(other, target)) continue;
            world.DealDamage(World.MakeAbilityDamage(Owner, other, splash, AttackType.Spells, DamageType.Magic));
            if (other.Alive) other.ApplySlow(SlowMove, SlowAttack, Duration);
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = Owner?.Target;
        if (target == null || !target.Alive) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
