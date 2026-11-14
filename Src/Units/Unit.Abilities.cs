using Godot;
using System.Collections.Generic;

using OpenWC3.Game;
namespace OpenWC3;

/// <summary>
/// 单位技能与加成。对标客户端 Units/Unit.Abilities + Components/Stats：
/// 技能实例挂在单位上，属性加成分成「技能/光环/物品」三类字段，全部由外部（World）加减，
/// 单位自己只负责把它们汇总进最终数值。
/// </summary>
public sealed partial class Unit
{
    AbilityContainer _abilities;

    /// <summary>技能表。构造时即绑定主人，供「Buff 也在技能表里」的遍历使用。</summary>
    public AbilityContainer Abilities => _abilities ??= new AbilityContainer { Owner = this };
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
    /// <summary>攻击未命中率（醉酒云雾）。</summary>
    public float AttackMissChance;
    /// <summary>物品给的闪避（AIev）。</summary>
    public float BonusEvasionPct;
    /// <summary>物品给的每秒回蓝（AIrn/AIrm）。</summary>
    public float BonusManaRegenPerSecond;

    public float BonusHpRegenPerSecond;

    public float TechManaPercent;
    /// <summary>物品给的视野加成（AIsi）。</summary>
    public float BonusSight;
    /// <summary>法球：命中附加伤害与触发效果。</summary>
    public float OrbDamage;
    /// <summary>本次射击是否已被箭类技能付费（决定附加伤害与弹道）。</summary>
    public bool PaidArrowShot;
    /// <summary>本次射击的弹道覆盖（技能给的原版箭模型）。</summary>
    public string NextMissileScene;
    public float OrbSecondary;
    public OrbEffect OrbEffectKind = OrbEffect.None;
    public float OrbSeconds;
    public float BonusMaxHp;
    /// <summary>攻击类型覆盖（战棍：攻城）。null 表示用武器自带的类型。</summary>
    public AttackType? AttackTypeOverride;
    /// <summary>射程加成（战棍 +DataE）。</summary>
    public float AttackRangeBonus;
    /// <summary>手里抓着树（战棍生效中，不能重复拔树）。</summary>
    public bool GrabbedTree;
    /// <summary>护甲类型覆盖（古树拔根后换成物编 DataD 指定的类型）。null 表示用单位类型自带的。</summary>
    public DefenseType? DefenseTypeOverride;

    /// <summary>护甲类型：有覆盖先看覆盖。</summary>
    public DefenseType DefenseTypeOrDefault => DefenseTypeOverride ?? (Type?.DefenseType ?? DefenseType.Unarmored);

    public float AttackReach => (Type?.AttackRange ?? 0f) + AttackRangeBonus + TechAttackRange;

    public float BaseMaxHp;
    public float BaseMaxMana;
    public float TechFlatHp;
    public float TechHpPercent;
    public float TechMaxManaFlat;
    public float TechHpRegen;
    public float TechManaRegen;
    public float TechAttackRange;
    public float TechAttackSpeedPct;
    public float TechMoveSpeedFlat;
    public float TechDamageFlat;
    public float TechArmor;
    public float TechLumberBonus;
    public float TechSpikeDamage;
    public int TechTargetCountBonus;
    public float TechSummonLifeBonus;
    public int TechSummonLevelBonus;
    /// <summary>装备给的法力上限（不随智力重算清零）。</summary>
    public float BonusMaxMana;
    /// <summary>魔法伤害减免（符文护腕 0.33）。</summary>
    public float MagicDamageReductionPct;
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
    public float AuraHpRegenPct;
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
        AuraHpRegenPct = 0f;
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
        AuraHpRegenPct = Mathf.Max(AuraHpRegenPct, bonus.HpRegenPct);
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


    public void Heal(float amount)
    {
        if (!Alive || amount <= 0f) return;
        Hp = Mathf.Min(MaxHp, Hp + amount);
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

    /// <summary>按码摘掉一个技能实例（工程升级会把旧码换成升级后的码）。</summary>
    public bool RemoveAbility(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        for (int index = Abilities.Count - 1; index >= 0; index--)
        {
            if (!string.Equals(Abilities[index].Id, id, System.StringComparison.OrdinalIgnoreCase)) continue;
            Abilities[index].OnRemove();
            Abilities.RemoveAt(index);
            return true;
        }
        return false;
    }

    public T FindBuff<T>() where T : Buff
    {
        foreach (Buff buff in Buffs)
        {
            if (buff is T match) return match;
        }
        // 客户端把 Buff 也放技能表（Buff : Ability），这里一并找
        foreach (Ability ability in Abilities)
        {
            if (ability is T match) return match;
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

        Ability ability = RuntimeAbilityFactory.Create(type);
        if (ability == null) return null;

        ability.Owner = this;
        ability.BindObjectData(type.Id, new AbilityTypeObjectData(type), Mathf.Clamp(level, 0, type.MaxLevel));
        ability.Level = Mathf.Clamp(level, 0, type.MaxLevel);
        Abilities.Add(ability);
        ability.OnAdd();
        ability.OnLevelUp();
        if (ability.Learned) ability.OnLearned();
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
