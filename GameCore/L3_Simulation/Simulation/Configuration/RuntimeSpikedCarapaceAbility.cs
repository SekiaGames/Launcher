using Godot;

namespace Launcher;

/// <summary>
/// 尖刺甲壳 AUts（地穴领主，被动）。物编 ubertip：+DataC(3/5/7) 护甲，
/// 敌人近战攻击反弹 DataA(15%/30%/45%) 伤害。
/// 护甲走 <see cref="Ability.PassiveArmor"/>（ArmorOf 汇总），反弹走 Ability.ReflectRatio。
/// </summary>
public sealed class SpikedCarapace : Ability
{
    public override bool IsPassive => true;

    public override float PassiveArmor => Data('C');
    public override float ReflectRatio => Data('A');
    public override bool ReflectsRangedAttack => false;
}
