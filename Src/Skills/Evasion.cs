using Godot;

namespace OpenWC3;

/// <summary>
/// 闪避 AEev（恶魔猎手 / 守望者）。被动：受到**普攻**时按 DataA(10/20/30%) 概率完全闪掉。
/// 挂点是 <see cref="World.PassesDefensiveHooks"/>（护甲结算之前），所以闪掉的攻击既不掉血也不触发命中特效。
/// 法术伤害不受影响——原版闪避只对物理攻击生效。
/// </summary>
public sealed class Evasion : Ability
{
    public override bool IsPassive => true;

    public float Chance => Mathf.Clamp(Data('A'), 0f, 1f);

    public override bool CanBeHitBy(World world, DamageData info)
    {
        if (info == null) return true;
        if (!info.IsAttack) return true;
        if (Chance <= 0f) return true;

        bool dodged = world.RollPercent(Chance * 100f);
        if (dodged) Dodged++;
        return !dodged;
    }

    /// <summary>累计闪掉次数，探针用。</summary>
    public int Dodged;
}
