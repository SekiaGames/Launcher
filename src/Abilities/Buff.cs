namespace Launcher;

/// <summary>
/// 时限增益/减益。对标客户端 Src/Abilities/Buff.cs：
/// 只负责「到期撤销」，具体加成由子类在 OnApply/OnRemove 里加减到单位字段上。
/// </summary>
public class Buff
{
    public Unit Owner;
    public Ability Source;
    public string Name = "buff";
    public float Remaining;

    public bool Expired => Remaining <= 0f;

    public virtual void OnApply(World world) { }

    public virtual void OnRemove(World world) { }

    /// <summary>持有者被普攻命中后（霜冻护甲减速攻击者、荆棘反伤这类挂这里）。</summary>
    public virtual void OnAttacked(World world, Unit attacker, DamageInfo info) { }

    /// <summary>返回 true 表示可以继续存在。</summary>
    public virtual bool OnTick(World world, float delta)
    {
        Remaining -= delta;
        return Remaining > 0f;
    }
}

/// <summary>霜冻护甲：+护甲，被普攻命中时让攻击者减速。</summary>
public sealed class FrostArmorBuff : Buff
{
    public float ArmorBonus;
    public float SlowMovePct = 0.3f;
    public float SlowSeconds = 5f;

    public override void OnApply(World world)
    {
        if (Owner != null) Owner.BonusArmor += ArmorBonus;
    }

    public override void OnRemove(World world)
    {
        if (Owner != null) Owner.BonusArmor -= ArmorBonus;
    }

    public override void OnAttacked(World world, Unit attacker, DamageInfo info)
    {
        if (attacker == null || !attacker.Alive || attacker.IsBuilding) return;
        attacker.ApplySlow(SlowMovePct, 0f, SlowSeconds);
    }
}

/// <summary>天神下凡：+护甲/+生命/+攻击，期间魔法免疫。</summary>
public sealed class AvatarBuff : Buff
{
    public float ArmorBonus;
    public float HitPointBonus;
    public float DamageBonus;

    public override void OnApply(World world)
    {
        if (Owner == null) return;

        Owner.BonusArmor += ArmorBonus;
        Owner.BonusDamageFlat += DamageBonus;
        Owner.BonusMaxHp += HitPointBonus;
        Owner.State.Add(StateMod.MagicImmune, Remaining);
        world?.RefreshHeroAttributes(Owner);
        if (Owner.MaxHp > 0f)
        {
            Owner.Hp = System.Math.Min(Owner.MaxHp, Owner.Hp + HitPointBonus);
            Owner.View?.SetHp(Owner.Hp / Owner.MaxHp, Owner.Alive);
        }
    }

    public override void OnRemove(World world)
    {
        if (Owner == null) return;

        Owner.BonusArmor -= ArmorBonus;
        Owner.BonusDamageFlat -= DamageBonus;
        Owner.BonusMaxHp -= HitPointBonus;
        Owner.State.RemoveAll(StateMod.MagicImmune);
        Owner.Hp = System.Math.Max(1f, System.Math.Min(Owner.Hp, Owner.MaxHp - HitPointBonus));
        world?.RefreshHeroAttributes(Owner);
        Owner.View?.SetHp(Owner.Hp / System.Math.Max(1f, Owner.MaxHp), Owner.Alive);
    }
}
