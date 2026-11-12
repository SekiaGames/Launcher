using Godot;

namespace Launcher;

/// <summary>
/// 重击 AHbh（被动）。对标客户端 Src/Skills/StunBuff + AHbh 的攻击后处理：
/// 每次普攻有 DataA% 概率追加 DataC 点伤害并眩晕 Dur 秒。
/// 物编：DataA=20(概率) DataC=25(伤害) Dur=2。
/// </summary>
public sealed class RuntimeBashAbility : Ability
{
    public override bool IsPassive => true;

    public float Chance => Data('A');
    public float BonusDamage => Data('C');

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (target == null || !target.Alive || world == null) return raw;
        if (target.State.MagicImmune) return raw;
        if (!world.RollPercent(Chance)) return raw;

        target.State.Add(StateMod.Stun, Duration);
        return raw + BonusDamage;
    }
}
