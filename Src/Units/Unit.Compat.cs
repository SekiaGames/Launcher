using Godot;

namespace OpenWC3;

public sealed partial class Unit
{
    public Vec2 Pos
    {
        get => new(Position.X, Position.Z);
        set => Position = new Vector3(value.X, Position.Y, value.Y);
    }

    public int PlayerId => Player;

    public float Life
    {
        get => Hp;
        set => Hp = value;
    }

    public float MaxLife
    {
        get => MaxHp;
        set => MaxHp = value;
    }

    public bool Removed { get; set; }

    public World World { get; set; }

    public float MoveSpeedBase => Type?.Speed ?? 0f;

    public void AddState(StateMod mod, float seconds = 0f) => State.Add(mod, seconds);

    public void RemoveState(StateMod mod) => State.RemoveAll(mod);

    public Health Health => _health ??= new Health(this);

    Health _health;

    public void AddAbility(Ability ability)
    {
        if (ability == null || string.IsNullOrEmpty(ability.Id)) return;
        if (Abilities.Get(ability.Id) != null) return;
        ability.Owner = this;
        Abilities.Add(ability);
        ability.OnAdd();
    }

    public bool RemoveAbility(Ability ability)
    {
        if (ability == null) return false;
        ability.OnRemove();
        return Abilities.Remove(ability);
    }

    /// <summary>客户端的合法目标判定：TargetFlags 位掩码 + 阵营关系 + 状态门。</summary>
    public bool CanBeTargetedBy(Unit caster, TargetFlags rules, bool useHeroSpellRules = false)
    {
        if (rules == TargetFlags.None) return true;

        bool wantDead = (rules & TargetFlags.Dead) != 0;
        if (!Alive) return wantDead;
        if (wantDead) return false;

        if ((rules & TargetFlags.Structure) == 0 && IsBuilding) return false;
        if (Type != null && Type.Flying)
        {
            if ((rules & TargetFlags.Air) == 0) return false;
        }
        else if ((rules & TargetFlags.Ground) == 0) return false;

        if ((rules & TargetFlags.Hero) != 0 && !IsHero) return false;
        if ((rules & TargetFlags.NonHero) != 0 && IsHero) return false;
        if (Type != null && Type.IsBuilding && (rules & TargetFlags.Mechanical) != 0) { }
        else if ((rules & TargetFlags.Mechanical) != 0) return false;

        if (caster != null)
        {
            bool self = ReferenceEquals(caster, this);
            bool same = caster.Player == Player;
            bool ally = World != null && World.AreAllied(caster.Player, Player);
            if (self)
            {
                if ((rules & TargetFlags.Self) == 0) return false;
            }
            else if (same)
            {
                if ((rules & TargetFlags.Player) == 0) return false;
            }
            else if (ally)
            {
                if ((rules & TargetFlags.Ally) == 0) return false;
            }
            else if ((rules & TargetFlags.Enemy) == 0) return false;
        }

        if ((rules & TargetFlags.NonMagicImmune) != 0 && State.MagicImmune) return false;
        if ((rules & TargetFlags.NonEthereal) != 0 && State.Ethereal) return false;
        if ((rules & TargetFlags.Vulnerable) != 0 && State.Invulnerable) return false;
        return true;
    }
}

public sealed class Health
{
    readonly Unit _unit;

    public Health(Unit unit) => _unit = unit;

    public float Life
    {
        get => _unit.Hp;
        set => _unit.Hp = value;
    }

    public float MaxLife
    {
        get => _unit.MaxHp;
        set => _unit.MaxHp = value;
    }

    public float InitialMana => _unit.Type?.ManaStart ?? 0f;

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        _unit.Hp = Mathf.Min(_unit.MaxHp, _unit.Hp + amount);
        _unit.View?.SetHp(_unit.Hp / Mathf.Max(1f, _unit.MaxHp), _unit.Alive);
    }

    public void AddEvasionSource(float chance)
    {
        if (chance <= 0f) return;
        _unit.BonusEvasionPct += chance;
    }

    public void RemoveEvasionSource(float chance)
    {
        if (chance <= 0f) return;
        _unit.BonusEvasionPct = Mathf.Max(0f, _unit.BonusEvasionPct - chance);
    }

    public void NotifySleepRemoval() => _unit.State.RemoveAll(StateMod.Sleep);
}
