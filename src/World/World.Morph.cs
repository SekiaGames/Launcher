using Godot;
using System;

namespace Launcher;

/// <summary>
/// 变形系统。对标客户端 Units/Unit.Morph.cs + Game/Scenes/War3GameScene 的模型重建：
/// 模拟层只换 <see cref="Unit.Type"/> 与生命上限，模型重建由对局层监听 <see cref="UnitMorphed"/> 完成。
/// 两种形态：disable=true 是妖术这种「变羊」（禁攻击禁施法但仍能移动），
/// disable=false 是变身（恶魔形态、德鲁伊形态，照常战斗）。
/// 生命上限始终以原单位为准（bonusHp 是变身额外加的那部分），所以变羊不会把血条变成小动物的血量。
/// </summary>
public sealed partial class World
{
    public event Action<Unit> UnitMorphed;

    public bool BeginMorph(Unit unit, UnitType morphType, float seconds, bool disable, float bonusHp = 0f)
    {
        if (unit == null || unit.Type == null || morphType == null || !unit.Alive) return false;
        if (unit.IsBuilding || unit.IsUnderConstruction) return false;
        if (seconds <= 0f) return false;

        if (unit.Morphed) EndMorph(unit, notify: false);

        unit.MorphOrigin = unit.Type;
        unit.MorphOriginMaxHp = unit.MaxHp;
        unit.MorphOriginHero = unit.IsHero;
        unit.MorphHpRatio = Mathf.Clamp(unit.Hp / Mathf.Max(1f, unit.MaxHp), 0.01f, 1f);
        unit.MorphRemaining = seconds;

        unit.Type = morphType;
        unit.MaxHp = Mathf.Max(1f, unit.MorphOriginMaxHp + bonusHp);
        unit.Hp = Mathf.Clamp(unit.MaxHp * unit.MorphHpRatio, 1f, unit.MaxHp);

        unit.CancelAttack();
        unit.Target = null;
        unit.MoveTarget = null;
        unit.Path = null;
        if (disable) unit.State.Add(StateMod.Morph, seconds);

        UnitMorphed?.Invoke(unit);
        return true;
    }

    public void TickMorph()
    {
        foreach (Unit unit in Units)
        {
            if (!unit.Morphed) continue;
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
        unit.MorphRemaining = 0f;
        unit.State.RemoveAll(StateMod.Morph);

        if (notify) UnitMorphed?.Invoke(unit);
    }

    /// <summary>变形中死亡：先把类型还原，尸体与经验才算在原单位上。</summary>
    public void EndMorphBeforeDeath(Unit unit)
    {
        if (unit?.Morphed == true) EndMorph(unit, notify: false);
    }
}
