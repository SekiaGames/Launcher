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
            if (string.Equals(unit.Type.RegenType, "none", StringComparison.OrdinalIgnoreCase)) continue;

            if (unit.Hp < unit.MaxHp)
            {
                float hpPerSecond = unit.Type.RegenHp;
                if (unit.IsHero) hpPerSecond += unit.Strength * HeroRules.StrRegenBonus;
                if (hpPerSecond > 0f) unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + hpPerSecond * Tick);
            }

            if (unit.IsHero && unit.Mana < unit.MaxMana)
            {
                float manaPerSecond = unit.Intelligence * HeroRules.IntRegenBonus + unit.Type.ManaRegen;
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

        if (target.Hp <= 0f) Kill(target, info.Source);
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

    private void Kill(Unit unit, Unit killer = null)
    {
        if (!unit.Alive) return;

        unit.CancelAttack();
        unit.EnterDeath();
        DropLootOnDeath(unit);
        GrantKillExperience(killer, unit);

        if (killer != null && killer.Player != unit.Player && !IsAllied(killer.Player, unit.Player) && GivesBounty(unit))
        {
            int bounty = BountyGoldOf(unit);
            if (bounty > 0) BountyAwarded?.Invoke(killer.Player, bounty, unit);
        }

        Died?.Invoke(unit);
    }
}
