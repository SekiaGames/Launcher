using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 霜冻新星 AUfn（巫妖）。物编 ubertip：主目标吃 DataB(100) 常规伤害，范围内其他敌人吃
/// DataA(50/100/150) 新星伤害，Dur(4/6/8) 秒内减速。
/// 减速比例物编没导出，按原版 50% 移速 / 50% 攻速。
/// </summary>
public sealed class RuntimeFrostNovaAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    private const float SlowMove = 0.5f;
    private const float SlowAttack = 0.25f;

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

        // 客户端 573b10：先对整个范围（含主目标）结算 DataA 并挂霜冻；
        // 56da80 再给原始目标补一次 DataB。原实现把主目标排除在范围伤害外，少了一段。
        foreach (Unit other in world.EnemiesInRange(Owner, center, Area))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, other, splash, AttackType.Spells, DamageType.Magic));
            if (other.Alive) other.ApplySlow(SlowMove, SlowAttack, Duration);
        }
        if (target.Alive)
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, target, primary, AttackType.Spells, DamageType.Magic));
        }
        return true;
    }

    /// <summary>AI：爆发。找主目标（优先英雄），命中后范围减速交给技能本体。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
