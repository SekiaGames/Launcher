using Godot;
using System;

namespace Launcher;

/// <summary>
/// 技能系统。对标客户端 Src/Abilities/* + Src/Components/Stats.cs：
/// 一个入口 <see cref="TryCast"/> 管法力/冷却/目标校验，一个 <see cref="TickAbilities"/>
/// 推进冷却、减速与 Buff，一个 <see cref="RefreshAuras"/> 每 tick 重算光环。
/// </summary>
public sealed partial class World
{
    /// <summary>技能放出后通知场景层（音效、投射物表现）。</summary>
    public event Action<Unit, Ability, Unit> AbilityCast;

    /// <summary>英雄学会/升级技能。</summary>
    public event Action<Unit, Ability> AbilityLearned;

    /// <summary>技能投射物出手（场景层据此建弹道模型）。</summary>
    public event Action<Unit, Ability, Unit> AbilityMissileFired;

    private sealed class PendingAbilityHit
    {
        public Unit Caster;
        public Ability Ability;
        public Unit Target;
        public float Remaining;
    }

    private readonly System.Collections.Generic.List<PendingAbilityHit> _pendingAbilityHits = new();

    public int PendingAbilityHitCount => _pendingAbilityHits.Count;

    /// <summary>技能要求「飞一会儿再命中」时调这个：到点才 OnMissileHit。</summary>
    public bool ScheduleAbilityHit(Unit caster, Ability ability, Unit target, float delay)
    {
        if (caster == null || ability == null || target == null) return false;

        _pendingAbilityHits.Add(new PendingAbilityHit
        {
            Caster = caster,
            Ability = ability,
            Target = target,
            Remaining = Mathf.Max(Tick, delay),
        });
        AbilityMissileFired?.Invoke(caster, ability, target);
        return true;
    }

    /// <summary>推进技能投射物；目标在飞行途中死亡就落空。</summary>
    public void TickAbilityMissiles()
    {
        for (int index = _pendingAbilityHits.Count - 1; index >= 0; index--)
        {
            PendingAbilityHit hit = _pendingAbilityHits[index];
            hit.Remaining -= Tick;
            if (hit.Remaining > 0f) continue;

            _pendingAbilityHits.RemoveAt(index);
            Unit target = hit.Target;
            if (target == null || !target.Alive) continue;
            hit.Ability.OnMissileHit(this, target);
        }
    }

    /// <summary>
    /// 召唤通道：模拟层没有场景，建单位要靠对局层实现这个回调
    /// （Scene 用 Spawn 造模型节点，再把单位交给 World）。
    /// </summary>
    public Func<Unit, string, Vector3, Unit> SummonHandler;

    /// <summary>召唤一个单位：单位 id 来自技能的 UnitID{level} 列，lifetime 为持续时间。</summary>
    public Unit Summon(Unit caster, string unitId, Vector3 position, float lifetime)
    {
        if (SummonHandler == null || string.IsNullOrEmpty(unitId)) return null;

        Unit created = SummonHandler(caster, unitId, position);
        if (created == null) return null;

        created.Summoner = caster;
        if (lifetime > 0f) created.SummonRemaining = lifetime;
        if (!Units.Contains(created)) Units.Add(created);
        return created;
    }

    public bool AreAllied(int a, int b) => IsAllied(a, b);

    public bool RollPercent(float percent)
    {
        if (percent <= 0f) return false;
        return _random.NextDouble() * 100.0 < percent;
    }

    /// <summary>
    /// 施法唯一入口：校验 → 扣法力 → 进冷却 → 交给技能效果。
    /// 技能效果返回 false 视为「没放出去」，不扣任何资源。
    /// </summary>
    public bool TryCast(Unit caster, Ability ability, Unit target, Vector3 point)
    {
        if (caster == null || ability == null || !caster.Alive) return false;
        if (!ability.Learned || !ability.Ready) return false;
        if (!caster.State.CanCast) return false;
        if (caster.IsUnderConstruction) return false;

        float cost = ability.ManaCost;
        if (caster.Mana < cost) return false;
        if (!ability.CheckTarget(this, target, point)) return false;
        if (!ability.OnCast(this, target, point)) return false;

        caster.Mana = Mathf.Max(0f, caster.Mana - cost);
        ability.CooldownRemaining = ability.Cooldown;
        AbilityCast?.Invoke(caster, ability, target);
        return true;
    }

    /// <summary>英雄学技能：花 1 点技能点，等级需求按 levelSkip / reqLevel 递推。</summary>
    public bool LearnAbility(Unit hero, string id)
    {
        if (hero == null || !hero.IsHero || !hero.Alive) return false;
        if (hero.SkillPoints <= 0) return false;
        if (!IsHeroSkill(hero, id)) return false;

        Ability ability = hero.GiveAbility(id, 0);
        if (ability?.Type == null) return false;
        if (ability.Level >= ability.Type.MaxLevel) return false;

        int next = ability.Level + 1;
        int skip = Mathf.Max(1, ability.Type.LevelSkip);
        int need = (next - 1) * skip + Mathf.Max(1, ability.Type.RequiredLevel);
        if (hero.Level < need) return false;

        ability.Level = next;
        hero.SkillPoints--;
        ability.OnLearned();
        AbilityLearned?.Invoke(hero, ability);
        return true;
    }

    public static bool IsHeroSkill(Unit hero, string id)
    {
        if (hero?.Type == null || string.IsNullOrEmpty(id)) return false;
        foreach (string known in hero.Type.HeroAbilList)
        {
            if (string.Equals(known, id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>每 tick：技能冷却、减速倒计时、Buff 到期、光环重算。</summary>
    public void TickAbilities()
    {
        foreach (Unit unit in Units)
        {
            if (!unit.Alive) continue;

            for (int index = 0; index < unit.Abilities.Count; index++)
            {
                Ability ability = unit.Abilities[index];
                if (ability.CooldownRemaining > 0f)
                {
                    ability.CooldownRemaining = Mathf.Max(0f, ability.CooldownRemaining - Tick);
                }
            }

            if (unit.SummonRemaining > 0f)
            {
                unit.SummonRemaining -= Tick;
                if (unit.SummonRemaining <= 0f) unit.ExpireSummon();
            }

            if (unit.SlowRemaining > 0f)
            {
                unit.SlowRemaining -= Tick;
                if (unit.SlowRemaining <= 0f)
                {
                    unit.SlowMovePct = 0f;
                    unit.SlowAttackPct = 0f;
                }
            }

            if (unit.AutoCast && unit.IsHero && unit.SkillPoints > 0) AutoLearnHeroSkills(unit);

            for (int index = unit.Buffs.Count - 1; index >= 0; index--)
            {
                Buff buff = unit.Buffs[index];
                if (buff.OnTick(this, Tick)) continue;
                unit.Buffs.RemoveAt(index);
                buff.OnRemove(this);
            }
        }

        RefreshAuras();
    }

    /// <summary>
    /// AI 自动施法：每 0.5 秒试一次，按技能自己挑目标（治疗找最虚弱的友军、伤害技能打当前目标）。
    /// 冷却与法力由 <see cref="TryCast"/> 统一管，所以不会刷。
    /// </summary>
    private void TickAutoCast(Unit unit)
    {
        if (unit.Abilities.Count == 0) return;

        if (unit.AutoCastTimer > 0f)
        {
            unit.AutoCastTimer -= Tick;
            return;
        }
        unit.AutoCastTimer = 0.5f;

        foreach (Ability ability in unit.Abilities)
        {
            if (!ability.Learned || ability.IsPassive || !ability.Ready) continue;
            if (unit.Mana < ability.ManaCost) continue;
            if (ability.TryAutoCast(this)) return;
        }
    }

    /// <summary>
    /// AI 英雄加点：优先补等级最低、且等级需求已满足的那个技能（谁低补谁，同高按表顺序），
    /// 这样 AI 英雄不会把所有点都堆在同一个技能上。玩家英雄走命令卡，不会进这里。
    /// </summary>
    private void AutoLearnHeroSkills(Unit hero)
    {
        Ability best = null;
        foreach (string id in hero.Type.HeroAbilList)
        {
            Ability ability = hero.FindAbility(id) ?? hero.GiveAbility(id, 0);
            if (ability?.Type == null || !ability.CanLevelUp) continue;

            int next = ability.Level + 1;
            int skip = Mathf.Max(1, ability.Type.LevelSkip);
            int need = (next - 1) * skip + Mathf.Max(1, ability.Type.RequiredLevel);
            if (hero.Level < need) continue;

            if (best == null || ability.Level < best.Level) best = ability;
        }

        if (best != null) LearnAbility(hero, best.Id);
    }

    /// <summary>光环：同源取最大，不叠加；施法者阵亡立即失效。</summary>
    public void RefreshAuras()
    {
        foreach (Unit unit in Units) unit.AuraArmor = 0f;

        foreach (Unit source in Units)
        {
            if (!source.Alive) continue;

            foreach (Ability ability in source.Abilities)
            {
                if (ability is not DevotionAura aura || !aura.Learned) continue;

                float value = aura.ArmorBonus;
                if (value <= 0f) continue;

                float radius = aura.AuraArea;
                foreach (Unit unit in Units)
                {
                    if (!unit.Alive) continue;
                    if (!AreAllied(source.Player, unit.Player)) continue;
                    if (source.DistanceTo(unit) > radius) continue;
                    unit.AuraArmor = Mathf.Max(unit.AuraArmor, value);
                }
            }
        }
    }

    /// <summary>普攻伤害钩子：致命一击、重击这类被动在这里改伤害或挂状态。</summary>
    public float ApplyAttackModifiers(Unit attacker, Unit target, float raw)
    {
        if (attacker == null || attacker.Abilities.Count == 0) return raw;

        float result = raw;
        foreach (Ability ability in attacker.Abilities)
        {
            if (!ability.Learned || !ability.IsPassive) continue;
            result = ability.ModifyOutgoingAttack(this, target, result);
        }
        return result;
    }

    /// <summary>按 基础 + 等级成长 + 装备/技能加成 重算英雄三围与生命法力。</summary>
    public void RefreshHeroAttributes(Unit hero)
    {
        if (hero?.Type == null || !hero.IsHero) return;

        UnitType type = hero.Type;
        int steps = Mathf.Max(0, hero.Level - 1);
        hero.Strength = type.Strength + Mathf.RoundToInt(type.StrengthGain * steps) + hero.BonusStrength;
        hero.Agility = type.Agility + Mathf.RoundToInt(type.AgilityGain * steps) + hero.BonusAgility;
        hero.Intelligence = type.Intelligence + Mathf.RoundToInt(type.IntelligenceGain * steps) + hero.BonusIntelligence;

        float newMaxHp = HeroRules.MaxHpOf(type, hero.Strength) + hero.BonusMaxHp;
        hero.Hp = Mathf.Max(1f, Mathf.Min(newMaxHp, hero.Hp + Mathf.Max(0f, newMaxHp - hero.MaxHp)));
        hero.MaxHp = newMaxHp;

        float newMaxMana = HeroRules.MaxManaOf(hero.Intelligence);
        hero.Mana = Mathf.Max(0f, Mathf.Min(newMaxMana, hero.Mana + Mathf.Max(0f, newMaxMana - hero.MaxMana)));
        hero.MaxMana = newMaxMana;

        hero.View?.SetHp(hero.Hp / Mathf.Max(1f, hero.MaxHp), hero.Alive);
    }

    /// <summary>移动速度：基础 + 装备，再乘减速。</summary>
    public static float SpeedOf(Unit unit)
    {
        if (unit?.Type == null) return 0f;
        float speed = unit.Type.Speed + unit.BonusMoveSpeedFlat;
        if (unit.Slowed) speed *= 1f - unit.SlowMovePct;
        return Mathf.Max(40f, speed);
    }

    /// <summary>装备加成落地：加字段并立刻重算英雄派生值。</summary>
    public void ApplyItemBonus(Unit unit, Item item)
    {
        if (unit == null || item?.Type == null) return;

        ItemBonus bonus = ItemBonus.ForItem(item.Type);
        if (bonus.IsZero) return;

        item.Bonus = bonus;
        unit.BonusStrength += bonus.Strength;
        unit.BonusAgility += bonus.Agility;
        unit.BonusIntelligence += bonus.Intelligence;
        unit.BonusDamageFlat += bonus.Damage;
        unit.BonusArmor += bonus.Armor;
        unit.BonusAttackSpeedPct += bonus.AttackSpeedPct;
        unit.BonusMoveSpeedFlat += bonus.MoveSpeed;

        RefreshHeroAttributes(unit);
    }

    /// <summary>扣除一件物品已经生效的加成（丢弃/被抢时用）。</summary>
    public void RemoveItemBonus(Unit unit, Item item)
    {
        if (unit == null || item == null) return;

        ItemBonus bonus = item.Bonus;
        if (bonus.IsZero) return;

        unit.BonusStrength -= bonus.Strength;
        unit.BonusAgility -= bonus.Agility;
        unit.BonusIntelligence -= bonus.Intelligence;
        unit.BonusDamageFlat -= bonus.Damage;
        unit.BonusArmor -= bonus.Armor;
        unit.BonusAttackSpeedPct -= bonus.AttackSpeedPct;
        unit.BonusMoveSpeedFlat -= bonus.MoveSpeed;
        item.Bonus = ItemBonus.Zero;

        RefreshHeroAttributes(unit);
    }
}
