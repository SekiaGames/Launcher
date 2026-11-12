using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

public sealed partial class World
{
    public float DamageBonus(Unit unit)
    {
        float bonus = unit.IsHero ? PrimaryAttributeOf(unit) : 0f;
        bonus += unit.BonusDamageFlat + unit.TechDamageFlat;
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
        armor += unit.BonusArmor + unit.AuraArmor + unit.TechArmor + unit.NonStackArmor;
        // 被动技能的常驻护甲（尖刺甲壳这类）
        foreach (Ability ability in unit.Abilities)
        {
            if (ability.Learned) armor += ability.PassiveArmor;
        }
        return armor;
    }

    public float HpMultiplier(Unit unit, int player) => 1f + (unit?.TechHpPercent ?? 0f);

    public void RefreshUpgradeStats(Unit unit)
    {
        if (unit?.Type == null) return;

        float flatHp = 0f, hpPercent = 0f, maxMana = 0f, manaRegen = 0f, hpRegen = 0f;
        float attackRange = 0f, attackSpeed = 0f, moveSpeed = 0f, damage = 0f, armor = 0f;
        float lumber = 0f, spiked = 0f, summonLife = 0f, summonLevel = 0f, targetCount = 0f;

        foreach (string upgradeId in unit.Type.UpgradesApplied)
        {
            UpgradeType upgrade = TechTree.Upgrade(upgradeId);
            if (upgrade == null) continue;
            int level = ResearchLevel(unit.Player, upgradeId);
            if (level <= 0) continue;

            flatHp += upgrade.FlatHpAt(level);
            hpPercent += upgrade.HpPercentAt(level);
            maxMana += upgrade.MaxManaAt(level);
            manaRegen += upgrade.ManaRegenAt(level);
            hpRegen += upgrade.HpRegenAt(level);
            attackRange += upgrade.RangeAt(level);
            attackSpeed += upgrade.AttackSpeedAt(level);
            moveSpeed += upgrade.MoveSpeedAt(level);
            damage += upgrade.DamageAt(level) + upgrade.ExtraDamageAt(level);
            armor += upgrade.ArmorAt(level);
            lumber += upgrade.LumberYieldAt(level);
            spiked += upgrade.SpikedAt(level);
            summonLife += upgrade.SummonLifeAt(level);
            summonLevel += upgrade.SummonLevelAt(level);
            targetCount += upgrade.TargetCountAt(level);
        }

        unit.TechFlatHp = flatHp;
        unit.TechHpPercent = hpPercent;
        unit.TechMaxManaFlat = maxMana;
        unit.TechManaRegen = manaRegen;
        unit.TechHpRegen = hpRegen;
        unit.TechAttackRange = attackRange;
        unit.TechAttackSpeedPct = attackSpeed;
        unit.TechMoveSpeedFlat = moveSpeed;
        unit.TechDamageFlat = damage;
        unit.TechArmor = armor;
        unit.TechLumberBonus = lumber;
        unit.TechSpikeDamage = spiked;
        unit.TechSummonLifeBonus = summonLife;
        unit.TechSummonLevelBonus = Mathf.RoundToInt(summonLevel);
        unit.TechTargetCountBonus = Mathf.RoundToInt(targetCount);

        RecomposeMaxStats(unit);
    }

    public void RefreshUpgradeStats(int player)
    {
        foreach (Unit unit in Units)
        {
            if (unit == null || !unit.Alive || unit.Player != player) continue;
            RefreshUpgradeStats(unit);
        }
    }

    private void RecomposeMaxStats(Unit unit)
    {
        if (unit.IsHero)
        {
            RefreshHeroAttributes(unit);
            return;
        }

        float newMaxHp = (unit.BaseMaxHp + unit.TechFlatHp) * (1f + unit.TechHpPercent);
        newMaxHp = Mathf.Max(1f, newMaxHp);
        float hpGain = newMaxHp - unit.MaxHp;
        unit.MaxHp = newMaxHp;
        if (hpGain > 0f) unit.Hp += hpGain;
        unit.Hp = Mathf.Min(unit.Hp, unit.MaxHp);

        if (unit.BaseMaxMana > 0f || unit.TechMaxManaFlat > 0f)
        {
            float newMaxMana = unit.BaseMaxMana + unit.BonusMaxMana + unit.TechMaxManaFlat;
            float manaGain = newMaxMana - unit.MaxMana;
            unit.MaxMana = newMaxMana;
            if (manaGain > 0f) unit.Mana += manaGain;
            unit.Mana = Mathf.Min(unit.Mana, unit.MaxMana);
        }

        unit.View?.SetHp(unit.Hp / Mathf.Max(1f, unit.MaxHp), unit.Alive);
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
                float hpPerSecond = unit.Type.RegenHp + unit.TechHpRegen + unit.AuraHpRegenPerSecond + unit.MaxHp * unit.AuraHpRegenPct;
                if (unit.IsHero) hpPerSecond += unit.Strength * HeroRules.StrRegenBonus;
                if (IsUndeadUnit(unit) && IsBlighted(unit.Position)) hpPerSecond += BlightHealPerSecond;
                if (hpPerSecond > 0f) unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + hpPerSecond * Tick);
            }

            if (unit.MaxMana > 0f && unit.Mana < unit.MaxMana)
            {
                float manaPerSecond = unit.Type.ManaRegen + unit.TechManaRegen + unit.AuraManaRegenPerSecond + unit.BonusManaRegenPerSecond;
                if (unit.IsHero) manaPerSecond += unit.Intelligence * HeroRules.IntRegenBonus;
                if (manaPerSecond > 0f) unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + manaPerSecond * Tick);
            }
        }
    }

    public DamageData MakeAttackDamage(Unit source, Unit target, float raw, Weapon weapon = null)
    {
        bool underConstruction = target.IsUnderConstruction;
        DefenseType defense = underConstruction ? DefenseType.Unarmored : target.DefenseTypeOrDefault;
        float armor = underConstruction ? 0f : ArmorOf(target);
        weapon ??= source.Type.Weapon;

        return new DamageData
        {
            Source = source,
            Target = target,
            Weapon = weapon,
            Raw = raw,
            Amount = raw,
            AttackType = source.AttackTypeOverride ?? weapon.AttackType,
            DefenseType = defense,
            DamageType = DamageType.Normal,
            Armor = armor,
            UnderConstruction = underConstruction,
            Ethereal = target.State.Ethereal,
            IsAttack = true,
            Ranged = weapon != null && !string.IsNullOrEmpty(weapon.MissileScene),
        };
    }

    /// <summary>
    /// 唯一伤害入口。顺序对齐客户端 Health.DamageScaling：
    /// DamagePreview 钩子 → 免疫判定 → 护甲 → 攻击/护甲类型矩阵 → 夹到 min(raw,1) → 扣血。
    /// </summary>
    public void DealDamage(DamageData info)
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
            if (info.IsAttack) AttackMissed?.Invoke(info.Source);
            DamageApplied?.Invoke(info);
            return;
        }

        if (target.State.Invulnerable)
        {
            info.Amount = 0f;
            DamageApplied?.Invoke(info);
            return;
        }

        if (info.IsAttack && target.BonusEvasionPct > 0f
            && RollPercent(Mathf.Clamp(target.BonusEvasionPct, 0f, 1f) * 100f))
        {
            info.Amount = 0f;
            info.Cancelled = true;
            AttackMissed?.Invoke(info.Source);
            DamageApplied?.Invoke(info);
            return;
        }

        bool physical = info.DamageType == DamageType.Normal;
        // 客户端 Health.cs:196：虚无单位免疫"非魔法"的物理伤害（放逐后普攻打不动，魔法照常）。
        if (physical && target.State.Ethereal)
        {
            info.Amount = 0f;
            DamageApplied?.Invoke(info);
            return;
        }
        if (target.State.MagicImmune && !physical || target.State.PhysicalImmune && physical)
        {
            info.Amount = 0f;
            DamageApplied?.Invoke(info);
            return;
        }

        info.Ethereal = target.State.Ethereal;
        if (target.IsIllusion && target.IllusionDamageReceived > 0f) info.Amount *= target.IllusionDamageReceived;
        if (target.BonusDamageTakenPct != 0f) info.Amount *= 1f + target.BonusDamageTakenPct;
        ApplyIncomingModifiers(info);
        if (info.DamageType == DamageType.Magic && target.MagicDamageReductionPct > 0f)
        {
            info.Amount *= 1f - Mathf.Clamp(target.MagicDamageReductionPct, 0f, 0.9f);
        }
        info.ThornsBase = Mathf.Max(0f, info.Amount);
        info.ArmorFactor = info.UnderConstruction || !physical ? 1f : War3GameplayConfig.Default.ArmorFactor(info.Armor);
        info.ClassRatio = War3GameplayConfig.Default.ClassRatio(info.AttackType, info.DefenseType);
        info.Amount = War3GameplayConfig.Default.Resolve(info.Amount, info.AttackType, info.DefenseType, info.DamageType,
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
    private void SustainAfterHit(DamageData info)
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

        bool ranged = info.Ranged || (info.Weapon != null && !string.IsNullOrEmpty(info.Weapon.MissileScene));
        float ratio = ReflectRatioOf(target, ranged);
        if (ratio > 0f)
        {
            float basis = info.ThornsBase > 0f ? info.ThornsBase : info.Amount;
            DamageData counter = MakeAbilityDamage(target, attacker, Mathf.Max(1f, basis * ratio),
                AttackType.Spells, DamageType.Universal);
            counter.Reflected = true;
            DealDamage(counter);
        }

        float flat = ReflectFlatOf(target, ranged);
        if (flat > 0f)
        {
            DamageData spikes = MakeAbilityDamage(target, attacker, flat,
                AttackType.Spells, DamageType.Universal);
            spikes.Reflected = true;
            DealDamage(spikes);
        }
    }

    /// <summary>固定反伤（尖刺路障）：只看近战普通攻击，按点数值返回。</summary>
    public float ReflectFlatOf(Unit target, bool ranged)
    {
        if (target == null || ranged) return 0f;

        float flat = Mathf.Max(0f, target.TechSpikeDamage);
        foreach (Ability ability in target.Abilities)
        {
            if (!ability.Learned) continue;
            flat = Mathf.Max(flat, ability.ReflectFlat);
        }
        return flat;
    }

    /// <summary>受到伤害修正钩子（艾露恩恩泽、硬化皮肤）：在护甲与类型矩阵之前改数值。</summary>
    public void ApplyIncomingModifiers(DamageData info)
    {
        Unit target = info?.Target;
        if (target == null) return;

        for (int index = 0; index < target.Buffs.Count; index++)
        {
            target.Buffs[index].ModifyIncomingDamage(this, info);
        }

        if (target.Abilities == null) return;
        foreach (Ability ability in target.Abilities)
        {
            if (!ability.Learned) continue;
            ability.ModifyIncomingDamage(this, info);
        }
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
    public static DamageData MakeAbilityDamage(Unit source, Unit target, float amount,
        AttackType attack, DamageType type)
    {
        bool underConstruction = target.IsUnderConstruction;
        return new DamageData
        {
            Source = source,
            Target = target,
            Raw = amount,
            Amount = amount,
            AttackType = attack,
            DefenseType = underConstruction ? DefenseType.Unarmored : target.DefenseTypeOrDefault,
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
        if (TryPhoenixEgg(unit)) return;

        TriggerBuffDeathEffects(unit);

        EndMorphBeforeDeath(unit);
        unit.CancelAttack();
        InterruptChannel(unit);
        for (int index = 0; index < unit.Abilities.Count; index++)
        {
            unit.Abilities[index].OnOwnerDeath(this);
        }
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
        if (unit.IsIllusion)
        {
            unit.IsCorpse = false;
            if (unit.View != null) unit.View.Root.Visible = false;
        }
    }

    /// <summary>死亡时遍历 Buff 钩子（黑蚀箭标记召骷髅）。正在跑的回调可能挂新 Buff，用下标遍历。</summary>
    private void TriggerBuffDeathEffects(Unit unit)
    {
        for (int index = 0; index < unit.Buffs.Count; index++)
        {
            unit.Buffs[index].OnDeath(this);
        }
    }
}
