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

    /// <summary>随机取 [0, count) 的下标（妖术从一列小动物里随机挑一只）。</summary>
    public int RandomIndex(int count)
    {
        if (count <= 0) return 0;
        return _random.Next(count);
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

        // 开关型技能：关掉不花法力、不进冷却（原版关献祭是瞬时的）
        if (ability.IsToggle && ability.ToggledOn)
        {
            ability.SetToggled(this, false);
            InterruptChannel(caster);
            AbilityCast?.Invoke(caster, ability, target);
            return true;
        }

        float cost = ability.ManaCost;
        if (caster.Mana < cost) return false;
        if (!ability.CheckTarget(this, target, point)) return false;
        if (!ability.OnCast(this, target, point)) return false;

        // 施放别的技能会打断正在进行的引导（引导型技能自己刚开的不能打断）
        if (!ability.IsChanneled) InterruptChannel(caster);

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
        // Buff 到期回调里可能召唤单位（黑暗转换）、换阵营；用下标遍历避开集合变更异常。
        for (int unitIndex = 0; unitIndex < Units.Count; unitIndex++)
        {
            Unit unit = Units[unitIndex];
            if (!unit.Alive) continue;

            for (int abilityIndex = 0; abilityIndex < unit.Abilities.Count; abilityIndex++)
            {
                Ability ability = unit.Abilities[abilityIndex];
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

            float debuffScale = 1f;
            for (int upkeepIndex = 0; upkeepIndex < unit.Abilities.Count; upkeepIndex++)
            {
                Ability kept = unit.Abilities[upkeepIndex];
                if (kept.ToggledOn) kept.OnUpkeep(this);
                if (!kept.Learned) continue;
                kept.OnWorldTick(this, Tick);
                debuffScale = Mathf.Min(debuffScale, kept.DebuffDurationScale);
            }
            unit.State.DebuffScale = debuffScale;

            for (int buffIndex = unit.Buffs.Count - 1; buffIndex >= 0; buffIndex--)
            {
                Buff buff = unit.Buffs[buffIndex];
                if (buff.OnTick(this, Tick)) continue;
                unit.Buffs.RemoveAt(buffIndex);
                buff.OnRemove(this);
            }
        }

        RefreshAuras();
    }

    /// <summary>
    /// AI 自动施法：每 0.5 秒试一次，按 <see cref="Ability.AiCastPriority"/> 排序——
    /// 先把控制技能放出去（妖术/缠绕/睡眠/沉默），再放爆发（风暴之锤/霜冻新星），
    /// 最后才是治疗/召唤/增益。冷却与法力由 <see cref="TryCast"/> 统一管，所以不会刷。
    /// 高优先级那招条件不满足时继续试后面的，不会因为排第一就卡住整轮。
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

        var ordered = new System.Collections.Generic.List<Ability>(unit.Abilities.Count);
        foreach (Ability ability in unit.Abilities)
        {
            if (!ability.Learned || ability.IsPassive || !ability.Ready) continue;
            bool turningOff = ability.IsToggle && ability.ToggledOn;
            if (!turningOff && unit.Mana < ability.ManaCost) continue;

            int at = ordered.Count;
            while (at > 0 && ordered[at - 1].AiCastPriority > ability.AiCastPriority) at--;
            ordered.Insert(at, ability);
        }

        foreach (Ability ability in ordered)
        {
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
        foreach (Unit unit in Units) unit.ClearAura();

        var bonus = new AuraBonus();
        foreach (Unit source in Units)
        {
            if (!source.Alive) continue;

            foreach (Ability ability in source.Abilities)
            {
                if (!ability.IsAura || !ability.Learned) continue;

                bonus = default;
                ability.CollectAura(ref bonus);
                if (bonus.IsZero) continue;

                float radius = ability.AuraRadius;
                foreach (Unit unit in Units)
                {
                    if (!unit.Alive) continue;
                    if (!AreAllied(source.Player, unit.Player)) continue;
                    if (source.DistanceTo(unit) > radius) continue;
                    unit.MergeAura(bonus);
                }
            }
        }
    }

    /// <summary>受击钩子：只要有一个已学被动要取消这次伤害（闪避），就整体取消。</summary>
    public bool PassesDefensiveHooks(DamageInfo info)
    {
        Unit target = info?.Target;
        if (target?.Abilities == null || target.Abilities.Count == 0) return true;

        foreach (Ability ability in target.Abilities)
        {
            if (!ability.Learned) continue;
            if (!ability.CanBeHitBy(this, info)) return false;
        }
        return true;
    }

    /// <summary>普攻伤害钩子：致命一击、重击这类被动在这里改伤害或挂状态。</summary>
    public float ApplyAttackModifiers(Unit attacker, Unit target, float raw)
    {        if (attacker == null || attacker.Abilities.Count == 0) return raw;

        float result = raw;
        foreach (Ability ability in attacker.Abilities)
        {
            if (!ability.Learned || !ability.IsPassive) continue;
            result = ability.ModifyOutgoingAttack(this, target, result);
        }
        return result;
    }

    /// <summary>普攻出手钩子：疾风步的破隐一击挂 Buff 上（隐身要在这时破）。</summary>
    public float ApplyLaunchModifiers(Unit attacker, Unit target, float raw)
    {
        if (attacker == null || attacker.Buffs.Count == 0) return raw;

        float result = raw;
        for (int index = 0; index < attacker.Buffs.Count; index++)
        {
            if (attacker.Buffs[index] is IAttackLaunchBuff hook) hook.OnAttackLaunched(this, target, ref result);
        }
        return result;
    }

    /// <summary>按 基础 + 等级成长 + 装备/技能加成 重算英雄三围与生命法力。</summary>
    public void RefreshHeroAttributes(Unit hero)
    {
        if (hero?.Type == null || !hero.IsHero) return;

        // 变形期间按「原形态」算三围，生命上限交给 Morph 管（变身额外加成是它加上去的）
        UnitType type = hero.Morphed && hero.MorphOrigin != null ? hero.MorphOrigin : hero.Type;
        int steps = Mathf.Max(0, hero.Level - 1);
        hero.Strength = type.Strength + Mathf.RoundToInt(type.StrengthGain * steps) + hero.BonusStrength;
        hero.Agility = type.Agility + Mathf.RoundToInt(type.AgilityGain * steps) + hero.BonusAgility;
        hero.Intelligence = type.Intelligence + Mathf.RoundToInt(type.IntelligenceGain * steps) + hero.BonusIntelligence;

        if (hero.Morphed)
        {
            hero.View?.SetHp(hero.Hp / Mathf.Max(1f, hero.MaxHp), hero.Alive);
            return;
        }

        float newMaxHp = HeroRules.MaxHpOf(type, hero.Strength) + hero.BonusMaxHp;
        hero.Hp = Mathf.Max(1f, Mathf.Min(newMaxHp, hero.Hp + Mathf.Max(0f, newMaxHp - hero.MaxHp)));
        hero.MaxHp = newMaxHp;

        float newMaxMana = HeroRules.MaxManaOf(hero.Intelligence) + hero.BonusMaxMana;
        hero.Mana = Mathf.Max(0f, Mathf.Min(newMaxMana, hero.Mana + Mathf.Max(0f, newMaxMana - hero.MaxMana)));
        hero.MaxMana = newMaxMana;

        hero.View?.SetHp(hero.Hp / Mathf.Max(1f, hero.MaxHp), hero.Alive);
    }

    /// <summary>移动速度：基础 + 装备，乘光环加成，再乘减速。</summary>
    public static float SpeedOf(Unit unit)
    {
        if (unit?.Type == null) return 0f;
        float speed = unit.Type.Speed + unit.BonusMoveSpeedFlat;
        speed *= 1f + Mathf.Max(0f, unit.AuraMoveSpeedPct);
        if (unit.Slowed) speed *= 1f - unit.SlowMovePct;
        return Mathf.Max(40f, speed);
    }

    /// <summary>装备加成落地：加字段并立刻重算英雄派生值。</summary>
    public void ApplyItemBonus(Unit unit, Item item)
    {
        if (unit == null || item?.Type == null) return;

        GrantItemAbilities(unit, item);

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
        unit.BonusLifestealPct += bonus.LifestealPct;
        unit.BonusManaRegenPerSecond += bonus.ManaRegen;
        unit.BonusEvasionPct += bonus.EvasionPct;
        unit.BonusSight += bonus.Sight;
        unit.BonusMaxHp += bonus.MaxHp;
        unit.BonusMaxMana += bonus.MaxMana;
        unit.MagicDamageReductionPct += bonus.MagicReductionPct;
        RebuildOrb(unit, item, null);

        RefreshHeroAttributes(unit);
    }

    private static readonly string[] ItemAbilityCodes = { "AIba", "AIav", "AIar", "AIae", "AIau", "AIcd", "AIuv", "AIcf", "AIad", "AIgx", "AIrc" };

    private void GrantItemAbilities(Unit unit, Item item)
    {
        if (unit == null || string.IsNullOrWhiteSpace(item?.Type?.AbilList)) return;

        foreach (string raw in item.Type.AbilList.Split(',', ';', StringSplitOptions.TrimEntries))
        {
            if (raw.Length < 4) continue;
            string code = raw[..4];
            if (System.Array.IndexOf(ItemAbilityCodes, code) < 0) continue;
            if (unit.GiveAbility(code, 1) == null) continue;
            item.GrantedAbilities.Add(code);
        }
    }

    private void RevokeItemAbilities(Unit unit, Item item)
    {
        if (unit == null || item == null) return;

        foreach (string code in item.GrantedAbilities)
        {
            bool stillGranted = false;
            foreach (Item other in unit.Inventory)
            {
                if (other == null || other == item) continue;
                foreach (string otherCode in other.GrantedAbilities)
                {
                    if (!string.Equals(otherCode, code, StringComparison.OrdinalIgnoreCase)) continue;
                    stillGranted = true;
                    break;
                }
                if (stillGranted) break;
            }
            if (stillGranted) continue;

            for (int index = unit.Abilities.Count - 1; index >= 0; index--)
            {
                if (!string.Equals(unit.Abilities[index].Id, code, StringComparison.OrdinalIgnoreCase)) continue;
                unit.Abilities.RemoveAt(index);
            }
        }
        item.GrantedAbilities.Clear();
    }

    /// <summary>重新汇总身上的法球（装备/丢弃/转交后重算，不叠多件）。</summary>
    private void RebuildOrb(Unit unit, Item added, Item removed)
    {
        unit.OrbDamage = 0f;
        unit.OrbSecondary = 0f;
        unit.OrbSeconds = 0f;
        unit.OrbEffectKind = OrbEffect.None;

        foreach (Item item in unit.Inventory)
        {
            if (item == null || item == removed) continue;
            ApplyOrb(item, unit);
        }
        if (added != null && System.Array.IndexOf(unit.Inventory, added) < 0) ApplyOrb(added, unit);
    }

    private static void ApplyOrb(Item item, Unit unit)
    {
        ItemBonus bonus = item.Bonus;
        if (bonus.OrbEffect == OrbEffect.None) return;

        unit.OrbDamage += bonus.OrbDamage;
        unit.OrbSecondary = Mathf.Max(unit.OrbSecondary, bonus.OrbSecondary);
        unit.OrbSeconds = Mathf.Max(unit.OrbSeconds, bonus.OrbSeconds);
        if (unit.OrbEffectKind == OrbEffect.None) unit.OrbEffectKind = bonus.OrbEffect;
    }

    /// <summary>扣除一件物品已经生效的加成（丢弃/被抢时用）。</summary>
    public void RemoveItemBonus(Unit unit, Item item)
    {
        if (unit == null || item == null) return;

        RevokeItemAbilities(unit, item);

        ItemBonus bonus = item.Bonus;
        if (bonus.IsZero) return;

        unit.BonusStrength -= bonus.Strength;
        unit.BonusAgility -= bonus.Agility;
        unit.BonusIntelligence -= bonus.Intelligence;
        unit.BonusDamageFlat -= bonus.Damage;
        unit.BonusArmor -= bonus.Armor;
        unit.BonusAttackSpeedPct -= bonus.AttackSpeedPct;
        unit.BonusMoveSpeedFlat -= bonus.MoveSpeed;
        unit.BonusLifestealPct -= bonus.LifestealPct;
        unit.BonusManaRegenPerSecond -= bonus.ManaRegen;
        unit.BonusEvasionPct -= bonus.EvasionPct;
        unit.BonusSight -= bonus.Sight;
        unit.BonusMaxHp -= bonus.MaxHp;
        unit.BonusMaxMana -= bonus.MaxMana;
        unit.MagicDamageReductionPct -= bonus.MagicReductionPct;
        item.Bonus = ItemBonus.Zero;
        RebuildOrb(unit, null, item);

        RefreshHeroAttributes(unit);
    }
}
