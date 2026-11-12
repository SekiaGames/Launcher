using Godot;

namespace OpenWC3;

/// <summary>普攻出手时的 Buff 钩子（疾风步的破隐一击这类）。</summary>
public interface IAttackLaunchBuff
{
    void OnAttackLaunched(World world, Unit target, ref float raw);
}

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

    /// <summary>是否为负面效果：驱散时对敌取负面、对友取正面。</summary>
    public virtual bool IsHarmful => true;

    public virtual void OnApply(World world) { }

    public virtual void OnRemove(World world) { }

    /// <summary>持有者被普攻命中后（霜冻护甲减速攻击者、荆棘反伤这类挂这里）。</summary>
    public virtual void OnAttacked(World world, Unit attacker, DamageData info) { }

    /// <summary>受到伤害修正（护甲与类型矩阵之前）：反魔法盾吸收、灵魂链分摊、相位转移都挂这里。</summary>
    public virtual void ModifyIncomingDamage(World world, DamageData info) { }

    /// <summary>持有者死亡时（黑蚀箭的标记到点召骷髅）。</summary>
    public virtual void OnDeath(World world) { }

    /// <summary>返回 true 表示可以继续存在。</summary>
    public virtual bool OnTick(World world, float delta)
    {
        Remaining -= delta;
        return Remaining > 0f;
    }

    // ---- 客户端 Buff 兼容层（对标 Src/Abilities/Buff.cs）----

    public int AuraLevel;

    public virtual bool Positive => !IsHarmful;

    public virtual bool Magic => false;

    public virtual bool Physical => false;

    public virtual bool AutoDispel => false;

    public virtual bool SpellStealable => false;

    public virtual bool Aura => false;

    public virtual bool TickRunsWhilePaused => false;

    public virtual float VisualAgeSeconds => 0f;

    public virtual void OnAdd() { }

    public virtual void OnRemove() { }

    public virtual void OnBuffAdd() { }

    public virtual void OnBuffRemove() { }

    public virtual void OnTick(float delta) { }
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

    public override void OnAttacked(World world, Unit attacker, DamageData info)
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

/// <summary>放逐：虚无（不能普攻、只吃魔法伤害）+ 减速，到期撤销。</summary>
public sealed class BanishBuff : Buff
{
    public float MoveSlowPct;
    public float AttackSlowPct;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        Owner.State.Add(StateMod.Ethereal, Remaining);
        if (MoveSlowPct > 0f || AttackSlowPct > 0f) Owner.ApplySlow(MoveSlowPct, AttackSlowPct, Remaining);
    }

    public override void OnRemove(World world)
    {
        Owner?.State.RemoveAll(StateMod.Ethereal);
    }
}

/// <summary>
/// 疾风步：淡入隐身 + 移速加成，第一次出手把隐身破掉并多打一刀。
/// 对标 Wiki RuntimeWindWalkBuff（DataA 淡入、DataB 移速系数、DataC 破隐伤害）。
/// </summary>
public sealed class WindWalkBuff : Buff, IAttackLaunchBuff
{
    public float FadeSeconds;
    public float MoveSpeedFactor;
    public float BackstabDamage;

    private float _fadeRemaining;
    private float _appliedMove;
    private bool _revealed;

    public bool Revealed => _revealed;

    public override void OnApply(World world)
    {
        if (Owner == null) return;
        _appliedMove = Owner.Type != null ? Owner.Type.Speed * MoveSpeedFactor : 0f;
        Owner.BonusMoveSpeedFlat += _appliedMove;
        _fadeRemaining = Mathf.Max(0f, FadeSeconds);
        if (_fadeRemaining <= 0f) BecomeInvisible();
    }

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (!alive || Owner == null) return alive;
        if (_revealed || Owner.State.Invisible) return alive;

        _fadeRemaining -= delta;
        if (_fadeRemaining <= 0f) BecomeInvisible();
        return alive;
    }

    public void OnAttackLaunched(World world, Unit target, ref float raw)
    {
        if (Owner == null || !Owner.State.Invisible) return;
        Owner.State.RemoveAll(StateMod.Invisible);
        _revealed = true;
        if (BackstabDamage > 0f) raw += BackstabDamage;
    }

    public override void OnRemove(World world)
    {
        if (Owner == null) return;
        Owner.BonusMoveSpeedFlat -= _appliedMove;
        _appliedMove = 0f;
        Owner.State.RemoveAll(StateMod.Invisible);
    }

    private void BecomeInvisible()
    {
        if (Owner == null || !Owner.Alive) return;
        Owner.State.Add(StateMod.Invisible, Mathf.Max(0.1f, Remaining));
    }
}

/// <summary>黑蚀箭标记：Dur 内被射死就召出 DataB 只 UnitID（DataC 秒）。</summary>
public sealed class BlackArrowMarkBuff : Buff
{
    public Unit Summoner;
    public string UnitId = "";
    public int Count = 1;
    public float SummonLifetime;

    public override void OnDeath(World world)
    {
        if (world == null || Summoner == null || Owner == null) return;
        if (string.IsNullOrEmpty(UnitId) || Count <= 0) return;

        for (int index = 0; index < Count; index++)
        {
            world.Summon(Summoner, UnitId, Owner.Position, SummonLifetime);
        }
    }
}

/// <summary>
/// 周期伤害（纠缠根须 / 暗影突袭的持续伤害）。每 Interval 秒结算一次，
/// 伤害来源记在 DamageSource 上，所以击杀经验与赏金仍然算给施法者。
/// </summary>
public sealed class PeriodicDamageBuff : Buff
{
    public Unit DamageSource;
    public float AmountPerTick;
    public float Interval = 1f;
    public AttackType AttackType = AttackType.Spells;
    public DamageType DamageType = DamageType.Magic;

    private float _accumulator;

    public override bool OnTick(World world, float delta)
    {
        bool alive = base.OnTick(world, delta);
        if (world == null || Owner == null || !Owner.Alive) return false;
        if (Interval <= 0f) return alive;

        _accumulator += delta;
        while (_accumulator >= Interval && Owner.Alive)
        {
            _accumulator -= Interval;
            world.DealDamage(World.MakeAbilityDamage(DamageSource, Owner, AmountPerTick, AttackType, DamageType));
        }
        return alive && Owner.Alive;
    }
}
