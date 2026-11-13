using System;

namespace OpenWC3;

// Data-backed buff placeholder for effects whose behavior is driven by JASS.
// It participates in the normal timed buff lifecycle and exposes source/caster/rawcode immediately.
//
// Standard Buff object data controls presentation. Effect magnitudes belong to
// the applying ability's template; DataA has no universal damage meaning.
// Only explicit project extension fields (b*) are interpreted here.
public sealed class ObjectDataBuff : Buff
{
    // 数据驱动 Buff 的显示名: 优先取物编 Name, 回退到 rawcode(或通用标签)。
    public override string Name => ObjectData?.GetString("Name",
        Rawcode.Length > 0 ? Rawcode : "物编Buff")
        ?? (Rawcode.Length > 0 ? Rawcode : "物编Buff");

    // 记录本次施加的增量, 移除时精确反向。
    float _armor, _damage, _maxLife, _maxMana, _manaMul, _lifeRegen, _manaRegen;
    float _moveSpeed, _attackSpeed;

    protected override void OnBuffAdd()
    {
        var s = Owner.Stats;
        var h = Owner.Health;
        _damage = ReadAny("bdmg");
        _maxLife = ReadAny("bhpo", "bhp");
        _maxMana = ReadAny("bman");
        _manaMul = ReadAny("bmap");
        _armor = ReadAny("barm");
        _lifeRegen = ReadAny("blif");
        _manaRegen = ReadAny("bmnr");
        _moveSpeed = ReadAny("bspd", "bmvs");
        _attackSpeed = ReadAny("bats");

        if (_armor != 0) s.ArmorBonus += _armor;
        if (_damage != 0) s.DamageBonus += _damage;
        ApplyLife(_maxLife);
        ApplyMana(_maxMana, _manaMul);
        if (_lifeRegen != 0) h.LifeRegen += _lifeRegen;
        if (_manaRegen != 0) h.TechManaRegenBonus += _manaRegen;
        if (_moveSpeed != 0) s.SpeedBonus += _moveSpeed;
        if (_attackSpeed != 0) s.AttackSpeedBonusPct += _attackSpeed;
    }

    protected override void OnBuffRemove()
    {
        var s = Owner.Stats;
        var h = Owner.Health;
        if (_armor != 0) s.ArmorBonus -= _armor;
        if (_damage != 0) s.DamageBonus -= _damage;
        if (_maxLife != 0) { s.MaxLifeBonus -= _maxLife; Owner.Life = Math.Min(Owner.Life, Owner.MaxLife); }
        if (_maxMana != 0) { h.MaxManaBonus -= _maxMana; Owner.Mana = Math.Min(Owner.Mana, Owner.MaxMana); }
        if (_manaMul != 0) { h.TechMaxManaMul -= _manaMul; Owner.Mana = Math.Min(Owner.Mana, Owner.MaxMana); }
        if (_lifeRegen != 0) h.LifeRegen -= _lifeRegen;
        if (_manaRegen != 0) h.TechManaRegenBonus -= _manaRegen;
        if (_moveSpeed != 0) s.SpeedBonus -= _moveSpeed;
        if (_attackSpeed != 0) s.AttackSpeedBonusPct -= _attackSpeed;
    }

    /// <summary>
    /// 按顺序读第一个**真正存在于物编**的列。
    /// getter 用 null 区分缺列；显式空白与数值 0 都会终止别名查找。
    /// </summary>
    float ReadAny(params string[] fields)
    {
        var d = ObjectData;
        if (d == null) return 0f;
        foreach (string field in fields)
        {
            string raw = d.GetString(field, Math.Max(1, Level), 0, null);
            if (RuntimeObjectNumbers.TryReadFloat(raw, out float parsed)) return parsed;
        }
        return 0f;
    }

    void ApplyLife(float bonus)
    {
        if (bonus == 0) return;
        float oldMax = Owner.MaxLife;
        Owner.Stats.MaxLifeBonus += bonus;
        float newMax = Owner.MaxLife;
        if (oldMax > 0) Owner.Life = Math.Min(Owner.MaxLife, Owner.Life * newMax / oldMax);
    }

    void ApplyMana(float flat, float mul)
    {
        if (flat != 0)
        {
            float oldMax = Owner.MaxMana;
            Owner.Health.MaxManaBonus += flat;
            float newMax = Owner.MaxMana;
            if (oldMax > 0) Owner.Mana = Math.Min(Owner.MaxMana, Owner.Mana * newMax / oldMax);
        }
        if (mul != 0) Owner.Health.TechMaxManaMul += mul;
    }
}
