using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 单位技能与加成。对标客户端 Units/Unit.Abilities + Components/Stats：
/// 技能实例挂在单位上，属性加成分成「技能/光环/物品」三类字段，全部由外部（World）加减，
/// 单位自己只负责把它们汇总进最终数值。
/// </summary>
public sealed partial class Unit
{
    public readonly List<Ability> Abilities = new();
    public readonly List<Buff> Buffs = new();

    // —— 加成（不含单位类型自身的数值） ——
    public float BonusArmor;
    public float BonusDamageFlat;
    /// <summary>攻击力百分比（命令光环、战鼓、咆哮）。</summary>
    public float BonusDamagePct;
    /// <summary>受到伤害百分比（狂暴：+50%）。</summary>
    public float BonusDamageTakenPct;
    public float BonusAttackSpeedPct;
    public float BonusMoveSpeedFlat;
    /// <summary>物品给的攻击吸血比例（AIva）。</summary>
    public float BonusLifestealPct;
    public float BonusMaxHp;
    /// <summary>AI 控制（非玩家视角）时为 true，会自己找机会放技能。</summary>
    public bool AutoCast;
    public float AutoCastTimer;
    public int BonusStrength;
    public int BonusAgility;
    public int BonusIntelligence;

    /// <summary>光环护甲：每 tick 由 World.RefreshAuras 重算，不是累加值。</summary>
    public float AuraArmor;
    public float AuraThornsPct;
    public float AuraLifestealPct;
    public float AuraRangedDamagePct;
    public float AuraAttackSpeedPct;
    public float AuraMoveSpeedPct;
    public float AuraDamagePct;
    public float AuraHpRegenPerSecond;
    public float AuraManaRegenPerSecond;

    /// <summary>清空这一 tick 的光环结果（RefreshAuras 每次重算前调用）。</summary>
    public void ClearAura()
    {
        AuraArmor = 0f;
        AuraThornsPct = 0f;
        AuraLifestealPct = 0f;
        AuraRangedDamagePct = 0f;
        AuraAttackSpeedPct = 0f;
        AuraMoveSpeedPct = 0f;
        AuraDamagePct = 0f;
        AuraHpRegenPerSecond = 0f;
        AuraManaRegenPerSecond = 0f;
    }

    public void MergeAura(AuraBonus bonus)
    {
        AuraArmor = Mathf.Max(AuraArmor, bonus.Armor);
        AuraThornsPct = Mathf.Max(AuraThornsPct, bonus.ThornsPct);
        AuraLifestealPct = Mathf.Max(AuraLifestealPct, bonus.LifestealPct);
        AuraRangedDamagePct = Mathf.Max(AuraRangedDamagePct, bonus.RangedDamagePct);
        AuraAttackSpeedPct = Mathf.Max(AuraAttackSpeedPct, bonus.AttackSpeedPct);
        AuraMoveSpeedPct = Mathf.Max(AuraMoveSpeedPct, bonus.MoveSpeedPct);
        AuraDamagePct = Mathf.Max(AuraDamagePct, bonus.DamagePct);
        AuraHpRegenPerSecond = Mathf.Max(AuraHpRegenPerSecond, bonus.HpRegenPerSecond);
        AuraManaRegenPerSecond = Mathf.Max(AuraManaRegenPerSecond, bonus.ManaRegenPerSecond);
    }

    /// <summary>开着的开关技能（献祭）：关闭时不占命令卡状态。</summary>
    public bool HasToggleOn(string id)
    {
        Ability ability = FindAbility(id);
        return ability != null && ability.ToggledOn;
    }

    // —— 减速（雷霆一击、诱捕这类） ——
    public float SlowMovePct;
    public float SlowAttackPct;
    public float SlowRemaining;
    public bool Slowed => SlowRemaining > 0f;

    /// <summary>取更强的减速：同类不叠加，刷新时长。</summary>
    public void ApplySlow(float movePct, float attackPct, float seconds)
    {
        SlowMovePct = Mathf.Max(SlowMovePct, Mathf.Clamp(movePct, 0f, 0.95f));
        SlowAttackPct = Mathf.Max(SlowAttackPct, Mathf.Clamp(attackPct, 0f, 0.95f));
        SlowRemaining = Mathf.Max(SlowRemaining, seconds);
    }

    public void Heal(float amount)
    {
        if (!Alive || amount <= 0f) return;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        View?.SetHp(Hp / Mathf.Max(1f, MaxHp), true);
    }

    public Ability FindAbility(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Ability ability in Abilities)
        {
            if (string.Equals(ability.Id, id, System.StringComparison.OrdinalIgnoreCase)) return ability;
        }
        return null;
    }

    public T FindBuff<T>() where T : Buff
    {
        foreach (Buff buff in Buffs)
        {
            if (buff is T match) return match;
        }
        return null;
    }

    /// <summary>身上是否有可驱散的某类 Buff（harmful=true 查负面）。</summary>
    public bool HasBuff(bool harmful)
    {
        foreach (Buff buff in Buffs)
        {
            if (buff.IsHarmful == harmful) return true;
        }
        return false;
    }

    /// <summary>挂一个技能实例；已经有的直接返回旧的。</summary>
    public Ability GiveAbility(string id, int level = 1)
    {
        Ability existing = FindAbility(id);
        if (existing != null)
        {
            if (level > existing.Level) existing.Level = level;
            return existing;
        }

        AbilityType type = AbilityCatalog.Get(id);
        if (type == null) return null;

        Ability ability = SkillRegistry.Create(type);
        if (ability == null) return null;

        ability.Owner = this;
        ability.Level = Mathf.Clamp(level, 0, type.MaxLevel);
        Abilities.Add(ability);
        return ability;
    }

    /// <summary>英雄可学技能（UnitAbilities.heroAbilList，等级从 0 开始）。</summary>
    public IEnumerable<string> HeroAbilityIds => Type?.HeroAbilList ?? System.Array.Empty<string>();

    public Ability FindPassive<T>() where T : Ability
    {
        foreach (Ability ability in Abilities)
        {
            if (ability is T && ability.Learned) return ability;
        }
        return null;
    }
}
