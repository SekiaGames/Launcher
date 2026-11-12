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

    static int _nextSimulationId;

    /// <summary>客户端每单位唯一 id（命令光环按它区分来源）。</summary>
    public int SimulationId { get; } = ++_nextSimulationId;

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

    public bool Invisible => State.Invisible;

    public bool Hidden { get; set; }

    public bool MagicImmune => State.MagicImmune;

    public bool Snared => State.Snared;

    public int Team => Player;

    /// <summary>单位自身的目标类别位（客户端 Unit.TargetType）。</summary>
    public TargetFlags TargetType
    {
        get
        {
            TargetFlags flags = TargetFlags.None;
            if (IsBuilding) flags |= TargetFlags.Structure;
            if (Type != null && Type.Flying) flags |= TargetFlags.Air;
            else flags |= TargetFlags.Ground;
            return flags;
        }
    }

    public List<string> AnimationProperties { get; } = new();

    public bool Ethereal => State.Ethereal;

    /// <summary>客户端 Unit.ManaRegen 是法力回复字段；Launcher 在 Type 上放基础值，这里折成同样的可读写口径。</summary>
    public float ManaRegen
    {
        get => _manaRegenBonus + (Type?.ManaRegen ?? 0f);
        set => _manaRegenBonus = value - (Type?.ManaRegen ?? 0f);
    }

    float _manaRegenBonus;

    public MovementType MovementType => Type?.MoveType switch
    {
        "fly" => MovementType.Fly,
        "hover" => MovementType.Hover,
        "horse" => MovementType.Horse,
        "float" => MovementType.Float,
        "amph" => MovementType.Amphibious,
        "none" => MovementType.Disabled,
        _ => MovementType.Foot,
    };

    public void AddState(StateMod mod, float seconds = 0f) => State.Add(mod, seconds);

    public void RemoveState(StateMod mod) => State.RemoveAll(mod);

    public Health Health => _health ??= new Health(this);

    public Stats Stats => _stats ??= new Stats(this);

    Stats _stats;

    public float NonStackArmor { get; internal set; }

    /// <summary>生命之泉/腐地这类常驻回复（客户端 buff 直接加在单位上的字段）。</summary>
    public float AlwaysLifeRegenBonus;

    public float SpeedBonus
    {
        get => BonusMoveSpeedFlat;
        set => BonusMoveSpeedFlat = value;
    }

    public float AttackSpeedBonusPct
    {
        get => BonusAttackSpeedPct;
        set => BonusAttackSpeedPct = value;
    }

    Health _health;

    /// <summary>客户端 Unit.TakeDamage：把伤害包交给世界结算，返回实际掉血。</summary>
    public float TakeDamage(DamageData damage)
    {
        if (damage == null || !Alive) return 0f;
        damage.Target ??= this;
        float before = Hp;
        World?.DealDamage(damage);
        return before - Hp;
    }

    public void AddState(StateModType type, float seconds = 0f) => State.Add(ToStateMod(type), seconds);

    public void RemoveState(StateModType type) => State.RemoveAll(ToStateMod(type));

    public bool HasState(StateModType type) => State.Has(ToStateMod(type));

    static StateMod ToStateMod(StateModType type)
        => Enum.TryParse(type.ToString(), out StateMod mod) ? mod : StateMod.Pause;

    public void AddAbility(Ability ability)
    {
        if (ability == null) return;
        if (!string.IsNullOrEmpty(ability.Id) && Abilities.Get(ability.Id) != null) return;
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
        bool restrictDomain = (rules & (TargetFlags.Ground | TargetFlags.Air)) != 0;
        if (restrictDomain)
        {
            if (Type != null && Type.Flying)
            {
                if ((rules & TargetFlags.Air) == 0) return false;
            }
            else if ((rules & TargetFlags.Ground) == 0) return false;
        }

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
                if ((rules & (TargetFlags.Player | TargetFlags.Ally)) == 0) return false;
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

public sealed class Stats
{
    readonly Unit _unit;
    readonly Dictionary<string, float> _nonStack = new();

    public Stats(Unit unit) => _unit = unit;

    public float MoveSpeedBase => _unit.MoveSpeedBase;

    public float SpeedBonus
    {
        get => _unit.BonusMoveSpeedFlat;
        set => _unit.BonusMoveSpeedFlat = value;
    }

    public float AttackSpeedBonusPct
    {
        get => _unit.BonusAttackSpeedPct;
        set => _unit.BonusAttackSpeedPct = value;
    }

    public float DamageBonus
    {
        get => _unit.BonusDamageFlat;
        set => _unit.BonusDamageFlat = value;
    }

    public void AddNonStack(string key, float armor, float unused, object source)
    {
        if (string.IsNullOrEmpty(key)) return;
        _nonStack[key] = armor;
        Recompute();
    }

    public void RemoveNonStack(string key, object source)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!_nonStack.Remove(key)) return;
        Recompute();
    }

    void Recompute()
    {
        float total = 0f;
        foreach (float value in _nonStack.Values) total += value;
        _unit.NonStackArmor = total;
    }
}


public enum StateModType
{
    Stun, Snare, Sleep, Ethereal, Disarm, Silence, Invisible, Invulnerable, MagicImmune,
    Resistant, MorphImmune, Detect, Detector,
    DisableAutoAttack, DisableAutoCast,
    DisableUnitCollision, DisableBuildingCollision,
    InternalPause, PauseTimedLife,
    Phased,
    RepairDisabled,
    PhysicalImmune,
}
