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
    public float BonusAttackSpeedPct;
    public float BonusMoveSpeedFlat;
    public float BonusMaxHp;
    /// <summary>AI 控制（非玩家视角）时为 true，会自己找机会放技能。</summary>
    public bool AutoCast;
    public float AutoCastTimer;
    public int BonusStrength;
    public int BonusAgility;
    public int BonusIntelligence;

    /// <summary>光环护甲：每 tick 由 World.RefreshAuras 重算，不是累加值。</summary>
    public float AuraArmor;

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
