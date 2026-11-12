using Godot;

namespace Launcher;

/// <summary>
/// 黑蚀箭 ANba（黑暗游侠）。对标 Wiki RuntimeBlackArrowAbility：
/// 每次普攻附加 DataA(2/10/20) 伤害并扣 Cost(6) 蓝，命中后给目标挂 DataDur(2s) 标记；
/// 目标在标记期内死亡就召出 UnitID（ndr1/2/3）的骷髅，存在 DataC(80) 秒。
/// 英雄与建筑不挂标记。
/// </summary>
public sealed class BlackArrow : Ability
{
    public override bool IsPassive => true;

    public float BonusDamage => Data('A');
    public int SkeletonCount => Mathf.Max(1, Mathf.RoundToInt(Data('B')));
    public float SkeletonSeconds => Data('C') > 0f ? Data('C') : 80f;
    public float MarkSeconds => Duration > 0f ? Duration : 2f;
    public string SkeletonUnitId => Type?.SummonAt(Level) ?? "ndr1";

    public override float ModifyOutgoingAttack(World world, Unit target, float raw)
    {
        if (Owner == null || target == null || BonusDamage <= 0f) return raw;
        if (Owner.Mana < ManaCost) return raw;
        if (target.IsHero || target.IsBuilding) return raw;

        Owner.Mana -= ManaCost;
        ApplyMark(world, target);
        return raw + BonusDamage;
    }

    private void ApplyMark(World world, Unit target)
    {
        if (world == null || target == null) return;

        for (int index = target.Buffs.Count - 1; index >= 0; index--)
        {
            if (target.Buffs[index] is not BlackArrowMarkBuff) continue;
            target.Buffs[index].OnRemove(world);
            target.Buffs.RemoveAt(index);
        }

        target.Buffs.Add(new BlackArrowMarkBuff
        {
            Owner = target,
            Source = this,
            Name = Name,
            Remaining = MarkSeconds,
            Summoner = Owner,
            UnitId = SkeletonUnitId,
            Count = SkeletonCount,
            SummonLifetime = SkeletonSeconds,
        });
    }
}
