using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 变形系统。对标客户端 Units/Unit.Morph.cs + Unit.TypeChange.cs 的 ApplyUnitTypeTemplate：
/// 模拟层只换 <see cref="Unit.Type"/> 与生命上限，模型重建由对局层监听 <see cref="UnitMorphed"/> 完成。
/// 三种形态：disable=true 是妖术这种「变羊」（禁攻击禁施法但仍能移动）；
/// disable=false 且 seconds>0 是变身（恶魔形态，照常战斗、到期还原）；
/// seconds<=0 是永久形态（德鲁伊熊/乌鸦），由技能自己再施放一次变回来。
/// 非英雄换类型时技能栏同步换成新类型自己的 abilList（客户端 AbilityContainer 保留实例、重读类型字段）。
/// 生命上限始终以原单位为准（bonusHp 是变身额外加的那部分），所以变羊不会把血条变成小动物的血量。
/// </summary>
public sealed partial class World
{
    public event Action<Unit> UnitMorphed;

    public bool BeginMorph(Unit unit, UnitType morphType, float seconds, bool disable, float bonusHp = 0f)
    {
        if (unit == null || unit.Type == null || morphType == null || !unit.Alive) return false;
        if (unit.IsBuilding || unit.IsUnderConstruction) return false;
        if (seconds < 0f) return false;

        if (unit.Morphed) EndMorph(unit, notify: false);

        unit.MorphOrigin = unit.Type;
        unit.MorphOriginMaxHp = unit.MaxHp;
        unit.MorphOriginHero = unit.IsHero;
        unit.MorphHpRatio = Mathf.Clamp(unit.Hp / Mathf.Max(1f, unit.MaxHp), 0.01f, 1f);
        unit.MorphPermanent = seconds <= 0f;
        unit.MorphRemaining = seconds > 0f ? seconds : 0f;

        unit.Type = morphType;
        unit.MaxHp = Mathf.Max(1f, unit.MorphOriginMaxHp + bonusHp);
        unit.Hp = Mathf.Clamp(unit.MaxHp * unit.MorphHpRatio, 1f, unit.MaxHp);

        unit.CancelAttack();
        unit.Target = null;
        unit.MoveTarget = null;
        unit.Path = null;
        if (disable) unit.State.Add(StateMod.Morph, seconds > 0f ? seconds : float.PositiveInfinity);

        SyncAbilitiesToType(unit);
        UnitMorphed?.Invoke(unit);
        return true;
    }

    /// <summary>非英雄换类型时把技能栏换成新类型的 abilList（英雄保留已学技能，与客户端一致）。</summary>
    private static void SyncAbilitiesToType(Unit unit)
    {
        if (unit.Type == null || unit.IsHero) return;

        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in unit.Type.Abilities)
        {
            if (!string.IsNullOrWhiteSpace(id) && id.Length >= 4) wanted.Add(id[..4]);
        }

        unit.Abilities.RemoveAll(ability => !wanted.Contains(ability.Id));
        foreach (string raw in unit.Type.Abilities)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length < 4) continue;
            unit.GiveAbility(raw[..4], 1);
        }
    }

    /// <summary>
    /// 研究与升级驱动的「永久换单位类型」（猎头者→狂暴战士、蒸汽机车→升级机车）。
    /// 与 <see cref="BeginMorph"/> 的区别：生命上限取新类型自己的，不看旧上限。
    /// </summary>
    public bool ReplaceType(Unit unit, UnitType newType)
    {
        if (unit == null || unit.Type == null || newType == null || !unit.Alive) return false;
        if (string.Equals(unit.Type.Id, newType.Id, StringComparison.OrdinalIgnoreCase)) return false;

        if (unit.Morphed) EndMorph(unit, notify: false);

        float ratio = Mathf.Clamp(unit.Hp / Mathf.Max(1f, unit.MaxHp), 0.01f, 1f);
        unit.Type = newType;
        unit.IsHero = newType.IsHero;
        unit.IsBuilding = newType.IsBuilding;
        unit.BaseMaxHp = Mathf.Max(1f, newType.Hp);
        unit.BaseMaxMana = newType.ManaMax > 0f ? newType.ManaMax : 0f;
        unit.MaxHp = newType.IsHero
            ? Mathf.Max(1f, HeroRules.MaxHpOf(newType, unit.Strength) + unit.BonusMaxHp)
            : Mathf.Max(1f, newType.Hp);
        unit.Hp = Mathf.Clamp(unit.MaxHp * ratio, 1f, unit.MaxHp);
        unit.MaxMana = newType.ManaMax > 0f ? newType.ManaMax : 0f;
        unit.Mana = Mathf.Min(unit.Mana, unit.MaxMana);
        unit.CancelAttack();

        SyncAbilitiesToType(unit);
        RefreshUpgradeStats(unit);
        UnitMorphed?.Invoke(unit);
        return true;
    }

    public void TickMorph()
    {
        foreach (Unit unit in Units)
        {
            if (!unit.Morphed || unit.MorphPermanent) continue;
            unit.MorphRemaining -= Tick;
            if (unit.MorphRemaining <= 0f) EndMorph(unit, notify: true);
        }
    }

    /// <summary>还原成变形前的类型；生命按变形时保存的比例换算回原来的上限。</summary>
    public void EndMorph(Unit unit, bool notify = true)
    {
        if (unit?.MorphOrigin == null) return;

        float ratio = unit.MorphHpRatio;
        unit.Type = unit.MorphOrigin;
        unit.IsHero = unit.MorphOriginHero;
        unit.MaxHp = unit.IsHero
            ? Mathf.Max(1f, HeroRules.MaxHpOf(unit.Type, unit.Strength) + unit.BonusMaxHp)
            : Mathf.Max(1f, unit.MorphOriginMaxHp);
        unit.Hp = Mathf.Clamp(unit.MaxHp * ratio, 1f, unit.MaxHp);

        unit.MorphOrigin = null;
        unit.MorphPermanent = false;
        unit.MorphRemaining = 0f;
        unit.State.RemoveAll(StateMod.Morph);

        SyncAbilitiesToType(unit);
        if (notify) UnitMorphed?.Invoke(unit);
    }

    /// <summary>变形中死亡：先把类型还原，尸体与经验才算在原单位上。</summary>
    public void EndMorphBeforeDeath(Unit unit)
    {
        if (unit?.Morphed == true) EndMorph(unit, notify: false);
    }
}
