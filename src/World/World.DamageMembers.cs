using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    public float DamageBonus(Unit unit)
    {
        float bonus = unit.IsHero ? PrimaryAttributeOf(unit) : 0f;
        bonus += unit.BonusDamageFlat;
        foreach (string upgradeId in unit.Type.UpgradesApplied)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;
            bonus += upgrade.DamageAt(ResearchLevel(unit.Player, upgradeId));
        }
        return bonus;
    }

    /// <summary>英雄攻击 = 武器伤害 + 主属性。</summary>
    public static float PrimaryAttributeOf(Unit unit)
    {
        if (unit == null || !unit.IsHero) return 0f;
        return unit.Type.Primary switch
        {
            "STR" => unit.Strength,
            "AGI" => unit.Agility,
            "INT" => unit.Intelligence,
            _ => 0f,
        };
    }

    public float ArmorOf(Unit unit)
    {
        float armor = unit.Type.Armor;
        if (unit.IsHero) armor += HeroRules.AgilityArmor(unit.Agility);
        armor += unit.BonusArmor + unit.AuraArmor;
        // 被动技能的常驻护甲（尖刺甲壳这类）
        foreach (Ability ability in unit.Abilities)
        {
            if (ability.Learned) armor += ability.PassiveArmor;
        }
        foreach (string upgradeId in unit.Type.UpgradesApplied)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;
            armor += upgrade.ArmorAt(ResearchLevel(unit.Player, upgradeId));
        }
        return armor;
    }

    public float HpMultiplier(Unit unit, int player)
    {
        float multiplier = 1f;
        foreach (string upgradeId in unit.Type.UpgradesApplied)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;
            multiplier += upgrade.HpPercentAt(ResearchLevel(player, upgradeId));
        }
        return multiplier;
    }

    /// <summary>
    /// 自然恢复：对标客户端 Components/Health 的 regen。
    /// 生命 = UnitBalance.regenHP（英雄再叠加 力量×StrRegenBonus）；法力 = 智力×IntRegenBonus。
    /// regenType=none 的单位不回血；night / blight 两种条件暂时按 always 处理。
    /// </summary>
    public void TickRegeneration()
    {
        foreach (Unit unit in Units)
        {
            if (!unit.Alive || unit.IsUnderConstruction) continue;
            bool noHpRegen = string.Equals(unit.Type.RegenType, "none", StringComparison.OrdinalIgnoreCase);

            if (!noHpRegen && unit.Hp < unit.MaxHp)
            {
                float hpPerSecond = unit.Type.RegenHp + unit.AuraHpRegenPerSecond;
                if (unit.IsHero) hpPerSecond += unit.Strength * HeroRules.StrRegenBonus;
                if (hpPerSecond > 0f) unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + hpPerSecond * Tick);
            }

            if (unit.MaxMana > 0f && unit.Mana < unit.MaxMana)
            {
                float manaPerSecond = unit.Type.ManaRegen + unit.AuraManaRegenPerSecond;
                if (unit.IsHero) manaPerSecond += unit.Intelligence * HeroRules.IntRegenBonus;
                if (manaPerSecond > 0f) unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + manaPerSecond * Tick);
            }
        }
    }

    public DamageInfo MakeAttackDamage(Unit source, Unit target, float raw, WeaponProfile weapon = null)
    {
        bool underConstruction = target.IsUnderConstruction;
        DefenseType defense = underConstruction ? DefenseType.Unarmored : target.Type.DefenseType;
        float armor = underConstruction ? 0f : ArmorOf(target);
        weapon ??= source.Type.Weapon;

        return new DamageInfo
        {
            Source = source,
            Target = target,
            Weapon = weapon,
            Raw = raw,
            Amount = raw,
            AttackType = weapon.AttackType,
            DefenseType = defense,
            DamageType = DamageType.Normal,
            Armor = armor,
            UnderConstruction = underConstruction,
            Ethereal = target.State.Ethereal,
            IsAttack = true,
        };
    }

    /// <summary>
    /// 唯一伤害入口。顺序对齐客户端 Health.DamageScaling：
    /// DamagePreview 钩子 → 免疫判定 → 护甲 → 攻击/护甲类型矩阵 → 夹到 min(raw,1) → 扣血。
    /// </summary>
    public void DealDamage(DamageInfo info)
    {
        if (info?.Target == null || !info.Target.Alive) return;

        DamagePreview?.Invoke(info);
        if (info.Cancelled) return;

        Unit target = info.Target;

        // 受击钩子：闪避这类被动在这里把伤害取消掉（护甲/免疫都还没算，所以是最省的一层）
        if (!PassesDefensiveHooks(info))
        {
            info.Amount = 0f;
            info.Cancelled = true;
            DamageApplied?.Invoke(info);
            return;
        }

        if (target.State.Invulnerable)
        {
            info.Amount = 0f;
            DamageApplied?.Invoke(info);
            return;
        }

        bool physical = info.DamageType == DamageType.Normal;
        if (target.State.MagicImmune && !physical || target.State.PhysicalImmune && physical)
        {
            info.Amount = 0f;
            DamageApplied?.Invoke(info);
            return;
        }

        info.Ethereal = target.State.Ethereal;
        info.ArmorFactor = info.UnderConstruction || !physical ? 1f : War3GameplayConfig.ArmorFactor(info.Armor);
        info.ClassRatio = War3GameplayConfig.ClassRatio(info.AttackType, info.DefenseType);
        info.Amount = War3GameplayConfig.Resolve(info.Amount, info.AttackType, info.DefenseType, info.DamageType,
            info.Armor, info.UnderConstruction, info.Ethereal);

        target.Hp -= info.Amount;
        target.View?.SetHp(target.Hp / Mathf.Max(1f, target.MaxHp), target.Alive);
        DamageApplied?.Invoke(info);

        // 被攻击回调：霜冻护甲减速攻击者、荆棘反伤将来也挂这里
        if (info.IsAttack && info.Amount > 0f && info.Source != null && target.Buffs.Count > 0)
        {
            for (int index = 0; index < target.Buffs.Count; index++)
            {
                target.Buffs[index].OnAttacked(this, info.Source, info);
            }
        }

        if (info.Amount > 0f) SustainAfterHit(info);

        if (target.Hp <= 0f) Kill(target, info.Source);
    }

    /// <summary>
    /// 命中之后的连带效果（对标客户端 Health 的 OnDamaged 监听）：
    /// 睡眠被打醒 → 吸血光环按实际伤害回血 → 荆棘光环/尖刺甲壳把伤害反给攻击者。
    /// </summary>
    private void SustainAfterHit(DamageInfo info)
    {
        Unit target = info.Target;
        if (target == null) return;

        // 原版口径：睡眠单位被攻击就醒（只在掉血时）
        if (target.State.Has(StateMod.Sleep)) target.State.RemoveAll(StateMod.Sleep);

        Unit attacker = info.Source;
        if (attacker == null || !attacker.Alive || ReferenceEquals(attacker, target)) return;
        if (info.Reflected || !info.IsAttack) return;

        if (attacker.AuraLifestealPct > 0f || attacker.BonusLifestealPct > 0f)
        {
            attacker.Heal(info.Amount * (attacker.AuraLifestealPct + attacker.BonusLifestealPct));
        }

        bool ranged = info.Weapon != null && !string.IsNullOrEmpty(info.Weapon.MissileScene);
        float ratio = ReflectRatioOf(target, ranged);
        if (ratio <= 0f) return;

        DamageInfo counter = MakeAbilityDamage(target, attacker, info.Amount * ratio,
            AttackType.Spells, DamageType.Universal);
        counter.Reflected = true;
        DealDamage(counter);
    }

    /// <summary>反伤比例：荆棘光环只反近战，被动（尖刺甲壳）自己声明吃不吃远程。</summary>
    public float ReflectRatioOf(Unit target, bool ranged)
    {
        if (target == null) return 0f;

        float ratio = ranged ? 0f : target.AuraThornsPct;
        foreach (Ability ability in target.Abilities)
        {
            if (!ability.Learned) continue;
            if (ranged && !ability.ReflectsRangedAttack) continue;
            ratio = Mathf.Max(ratio, ability.ReflectRatio);
        }
        return ratio;
    }

    /// <summary>牺牲一个友军单位（死亡契约 / 黑暗仪式）：不当成击杀，所以不给经验、不掉落。</summary>
    public void Sacrifice(Unit victim)
    {
        if (victim == null || !victim.Alive) return;
        Kill(victim, null);
    }

    /// <summary>技能/陷阱等非武器伤害的统一构造入口。</summary>
    public static DamageInfo MakeAbilityDamage(Unit source, Unit target, float amount,
        AttackType attack, DamageType type)
    {
        bool underConstruction = target.IsUnderConstruction;
        return new DamageInfo
        {
            Source = source,
            Target = target,
            Raw = amount,
            Amount = amount,
            AttackType = attack,
            DefenseType = underConstruction ? DefenseType.Unarmored : target.Type.DefenseType,
            DamageType = type,
            Armor = 0f,
            UnderConstruction = underConstruction,
            Ethereal = target.State.Ethereal,
            IsAttack = false,
        };
    }

    /// <summary>吃掉/取材一具尸体：尸体消失，不再参与复活与后续取材。</summary>
    public void ConsumeCorpse(Unit corpse)
    {
        if (corpse == null || corpse.Alive) return;

        corpse.IsCorpse = false;
        corpse.CorpseRemaining = 0f;
        corpse.AwaitingRevive = false;
        if (corpse.View != null) corpse.View.Root.Visible = false;
    }

    /// <summary>直接死亡（灵魂保存 / 黑暗转换）：绕过伤害计算，击杀者照常记账。</summary>
    public void KillUnit(Unit unit, Unit killer = null) => Kill(unit, killer);

    private void Kill(Unit unit, Unit killer = null)
    {
        if (!unit.Alive) return;

        EndMorphBeforeDeath(unit);
        unit.CancelAttack();
        InterruptChannel(unit);
        unit.EnterDeath();
        bool reincarnating = TryDeathTriggers(unit);
        if (!reincarnating)
        {
            DropLootOnDeath(unit);
            GrantKillExperience(killer, unit);

            if (killer != null && killer.Player != unit.Player && !IsAllied(killer.Player, unit.Player) && GivesBounty(unit))
            {
                int bounty = BountyGoldOf(unit);
                if (bounty > 0) BountyAwarded?.Invoke(killer.Player, bounty, unit);
            }
        }

        Died?.Invoke(unit);
    }
}
