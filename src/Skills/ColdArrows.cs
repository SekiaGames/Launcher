using Godot;

namespace Launcher;

/// <summary>
/// 冰霜箭 ANfa / 寒冰箭 AHca（海巫 / 凯尔萨斯）。对标 Wiki RuntimeColdArrowsAbility：
/// 每次普攻附加 DataA(5/10/15) 伤害，并给目标挂 DataB 移速 / DataC 攻速的减速，
/// 普通单位 Dur(5/6) 秒、英雄 HeroDur(1.5/1) 秒；每箭扣 Cost(8~10) 蓝。
/// </summary>
public sealed class ColdArrows : Ability
{
    public override bool IsPassive => true;

    public float BonusDamage => Data('A');
    public float MoveSlowPct => Data('B');
    public float AttackSlowPct => Data('C');

    public override string PrepareAttackMissile(World world, Unit target)
    {
        if (Owner == null || target == null || BonusDamage <= 0f) return null;
        if (Owner.Mana < ManaCost) return null;

        Owner.Mana -= ManaCost;
        Owner.PaidArrowShot = true;
        return @"Abilities\Weapons\ColdArrow\ColdArrowMissile";
    }

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (Owner == null || target == null || !Owner.PaidArrowShot || BonusDamage <= 0f) return raw;

        float seconds = target.IsHero && HeroDuration > 0f ? HeroDuration : Duration;
        if (seconds > 0f && (MoveSlowPct > 0f || AttackSlowPct > 0f))
        {
            target.ApplySlow(MoveSlowPct, AttackSlowPct, seconds);
        }
        return raw + BonusDamage;
    }
}