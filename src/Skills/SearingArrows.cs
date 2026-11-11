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

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (Owner == null || BonusDamage <= 0f) return raw;
        if (Owner.Mana < ManaCost) return raw;

        Owner.Mana -= ManaCost;
        return raw + BonusDamage;
    }
}