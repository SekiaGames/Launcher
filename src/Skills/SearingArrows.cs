using Godot;

namespace Launcher;

/// <summary>
/// 灼热之箭 AHfa（月之女祭司）。对标客户端 Src/Skills 里的攻击附加型：每次普攻
/// 额外打出 DataA(10/20/30) 点火焰伤害，并扣 Cost(8) 点法力；蓝不够就只是普通一箭。
/// </summary>
public sealed class SearingArrows : Ability
{
    public override bool IsPassive => true;

    public float BonusDamage => Data('A');

    public override string PrepareAttackMissile(World world, Unit target)
    {
        if (Owner == null || target == null || BonusDamage <= 0f) return null;
        if (Owner.Mana < ManaCost) return null;

        Owner.Mana -= ManaCost;
        Owner.PaidArrowShot = true;
        return @"Abilities\Weapons\SearingArrow\SearingArrowMissile";
    }

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (Owner == null || !Owner.PaidArrowShot || BonusDamage <= 0f) return raw;
        return raw + BonusDamage;
    }
}