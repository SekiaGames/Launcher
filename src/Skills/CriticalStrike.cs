using Godot;

namespace Launcher;

/// <summary>
/// 致命一击 AOcr（被动）。对标客户端 Src/Skills/CriticalStrike.cs：
/// DataA% 概率造成 DataB 倍伤害（物编 DataA=15 恒定，DataB=2/3/4）。
/// </summary>
public sealed class CriticalStrike : Ability
{
    public override bool IsPassive => true;

    public float Chance => Data('A');
    public float Multiplier => Mathf.Max(1f, Data('B'));

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (world == null || !world.RollPercent(Chance)) return raw;
        return raw * Multiplier;
    }
}
