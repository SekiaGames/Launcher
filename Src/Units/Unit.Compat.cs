using Godot;

namespace OpenWC3;

public sealed partial class Unit
{
    public Vec2 Pos
    {
        get => new(Position.X, Position.Z);
        set => Position = new Vector3(value.X, Position.Y, value.Y);
    }

    public int PlayerId
    {
        get => Player;
        set => Player = value;
    }

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

    public string TypeId => Type?.Id ?? "";

    public int TypeLevel => Level > 0 ? Level : 1;

    public int TeamColorIndex
    {
        get => Player;
        set => Player = value;
    }

    public string Name => Type?.Name ?? "";

    public bool CanMove => Type != null && State.CanMove;

    public bool CanAttack => Type != null && Type.CanAttack && State.CanAttack;

    /// <summary>动画请求版本号：改动动画属性后自增，表现层据此重放。</summary>
    public int AnimationRequestVersion;

    public bool Is(Classification classification)
    {
        if (classification == Classification.None) return false;
        Classification flags = Type?.Classifications ?? Classification.None;
        if (IsHero) flags |= Classification.Hero;
        if (SummonRemaining > 0f) flags |= Classification.Summoned;
        if (IsBuilding && (flags & Classification.TownHall) == 0) flags |= Classification.None;
        if (!IsBuilding && (flags & Classification.Mechanical) == 0 && (flags & Classification.Undead) == 0)
            flags |= Classification.Organic;
        return (flags & classification) != 0;
    }

    public bool Untargetable => State.Invulnerable || State.Phased || Hidden;

    public bool Invulnerable => State.Invulnerable;

    public Unit CurrentTarget() => Target;

    /// <summary>客户端 Unit.Attack 组件：Launcher 侧用当前武器代表「能打」。</summary>
    public Weapon Attack => ActiveWeapon;

    /// <summary>顶点色（幻象半透明染色的来源，表现层读取）。</summary>
    public Vector4 VertexColorRgba = Vector4.One;

    /// <summary>模型缩放（幻象沿用本体比例）。</summary>
    public float VisualScale = 1f;
    public float VisualScaleX = 1f;
    public float VisualScaleY = 1f;
    public float VisualScaleZ = 1f;

    /// <summary>静态分类 + 运行时分类（召唤物在存活期间才算召唤物）。显式赋值会覆盖静态部分。</summary>
    public Classification Classifications
    {
        get
        {
            Classification classes = _classificationOverride ?? (Type?.Classifications ?? Classification.None);
            if (SummonRemaining > 0f) classes |= Classification.Summoned;
            if (IsIllusion) classes |= Classification.Summoned;
            return classes;
        }
        set
        {
            Classification before = Classifications;
            _classificationOverride = value;
            Classification after = Classifications;
            if (((before ^ after) & (Classification.Hero | Classification.Peon)) != 0)
                _identity?.NotifyUiClassificationMembershipChanged(this);
        }
    }

    Classification? _classificationOverride;

    public bool IsDetectedByPlayer(int player) => !Hidden || true;

    public bool UsesHeroSpellRules => IsHero;

    public Identity Identity => _identity ??= new Identity(this);

    Identity _identity;

    public Death Death => _death ??= new Death(this);

    Death _death;

    public bool CorpseDecaySuspended;

    public int TeleportVersion;

    public float FlyHeight => Type != null && Type.Flying ? 100f : 0f;

    public float ProjectileLaunchZ => Type?.ShadowHeight ?? 0f;

    /// <summary>弹道落点高度偏移（客户端默认 60）。</summary>
    public float? ProjectileImpactZ { get; set; }

    public float CollisionSize => Type == null ? 16f : (IsBuilding ? Type.FootprintRadius : Type.CollisionRadius);

    public Ability GetAbility(string id) => FindAbility(id);

    public T GetAbility<T>(string id = null) where T : Ability
    {
        for (int i = 0; i < Abilities.Count; i++)
        {
            if (Abilities[i] is not T typed) continue;
            if (id == null || typed.Id == id) return typed;
        }
        return null;
    }

    public bool MagicImmune => State.MagicImmune;

    public bool Silenced => State.Silenced;

    public UnitFlags Flags
    {
        get
        {
            UnitFlags flags = UnitFlags.None;
            if (State.Stunned) flags |= UnitFlags.Stunned;
            if (State.Silenced) flags |= UnitFlags.Silenced;
            if (State.Channeling) flags |= UnitFlags.Channeling;
            if (!Alive) flags |= UnitFlags.Dead;
            return flags;
        }
        set
        {
            if ((value & UnitFlags.Stunned) != 0) State.Add(StateMod.Stun);
            if ((value & UnitFlags.Channeling) != 0 && World != null) World.BeginChannel(this, null, Position, 0f, 0f);
        }
    }

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

    static StateMod ToStateMod(StateModType type) => State.ToMod(type);

    public UnitOrders Orders => _orders ??= new UnitOrders(this);

    UnitOrders _orders;

    public void CancelOrders()
    {
        PendingOrders.Clear();
        MoveTarget = null;
        Target = null;
        AttackMoving = false;
        Patrolling = false;
        HoldingPosition = false;
    }

    public void AddAbility(Ability ability)
    {
        if (ability == null) return;
        if (!string.IsNullOrEmpty(ability.Id) && Abilities.Get(ability.Id) != null) return;
        ability.Owner = this;
        Abilities.Add(ability);
        ability.OnAdd();
        // 客户端 Buff 只在 Duration 上写时长，剩余时间要在这里起算，否则挂着即过期
        if (ability is Buff buff && buff.Remaining <= 0f) buff.RefreshExpiration();
    }

    public bool RemoveAbility(Ability ability)
    {
        if (ability == null) return false;
        // A 的 Buff 在 OnRemove(World) 里回退属性，客户端 Buff 在 OnBuffRemove 里收尾，两个都要走
        if (ability is Buff buff)
        {
            buff.OnRemove(World);
            buff.OnRemove();
        }
        else ability.OnRemove();
        return Abilities.Remove(ability);
    }

    /// <summary>客户端的合法目标判定：TargetFlags 位掩码 + 阵营关系 + 状态门。</summary>
    public bool CanBeTargetedBy(Unit caster, TargetFlags rules, bool useHeroSpellRules = false, bool ignoreVisibility = false)
    {
        if (rules == TargetFlags.None) return true;
        // 缺阵营位表示双方都允许（客户端 TargetRules.Normalize 的 Relations 默认）
        if ((rules & (TargetFlags.Self | TargetFlags.Player | TargetFlags.Ally
            | TargetFlags.Neutral | TargetFlags.Enemy)) == 0)
        {
            rules |= TargetFlags.Self | TargetFlags.Player | TargetFlags.Ally
                | TargetFlags.Neutral | TargetFlags.Enemy;
        }

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

    public float LifeRegen
    {
        get => _unit.BonusHpRegenPerSecond;
        set => _unit.BonusHpRegenPerSecond = value;
    }

    public float TechManaRegenBonus
    {
        get => _unit.TechManaRegen;
        set => _unit.TechManaRegen = value;
    }

    public float MaxManaBonus
    {
        get => _unit.BonusMaxMana;
        set => _unit.BonusMaxMana = value;
    }

    public float TechMaxManaMul
    {
        get => _unit.TechManaPercent;
        set => _unit.TechManaPercent = value;
    }


    public float Mana
    {
        get => _unit.Mana;
        set => _unit.Mana = value;
    }

    public float MaxMana
    {
        get => _unit.MaxMana;
        set => _unit.MaxMana = value;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        _unit.Hp = Mathf.Min(_unit.MaxHp, _unit.Hp + amount);
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

    public void NotifySleepRemoval(Unit caster) => _unit.State.RemoveAll(StateMod.Sleep);
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

    public float ArmorBonus
    {
        get => _unit.BonusArmor;
        set => _unit.BonusArmor = value;
    }

    public float MaxLifeBonus
    {
        get => _unit.BonusMaxHp;
        set => _unit.BonusMaxHp = value;
    }

    public AttackType AttackType => _unit.ActiveWeapon?.AttackType ?? AttackType.Normal;

    public float TechDamageBonus => _unit.TechDamageFlat;

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

public sealed class Death
{
    readonly Unit _unit;

    public Death(Unit unit) => _unit = unit;

    /// <summary>就地死亡（客户端 Death.Die）。</summary>
    public void Die(Unit killer) => _unit.World?.KillUnit(_unit, killer);

    public bool IsCorpse => _unit.IsCorpse;

    public bool RevivalForbidden => _unit.IsCorpse && _unit.CorpseRemaining <= 0f;

    public bool IsDead => !_unit.Alive;

    public float CorpseSecondsLeft => _unit.CorpseRemaining;

    public void SuspendDecay(bool suspend) => _unit.CorpseDecaySuspended = suspend;

    public void ResumeDecay() => _unit.CorpseDecaySuspended = false;

    /// <summary>把单位变成一具尸体（客户端 Death.InitializeAsCorpse）。</summary>
    public void InitializeAsCorpse()
    {
        _unit.Alive = false;
        _unit.IsCorpse = true;
        _unit.Hp = 0f;
        _unit.CorpseRemaining = Unit.CorpseSeconds;
    }
}

public sealed class Identity
{
    readonly Unit _unit;

    public Identity(Unit unit) => _unit = unit;

    public string Name => _unit.Type?.Name ?? "";

    public int Team => _unit.Team;

    public TargetFlags TargetType => _unit.TargetType;

    public Classification Classifications => _unit.Classifications;

    public bool Is(Classification classification) => _unit.Is(classification);

    internal event Action<Unit> UiClassificationMembershipChanged;

    internal void NotifyUiClassificationMembershipChanged(Unit unit)
        => UiClassificationMembershipChanged?.Invoke(unit);
}

public sealed class UnitOrders
{
    readonly Unit _unit;

    public UnitOrders(Unit unit) => _unit = unit;

    public void InterruptCurrentCast()
    {
        _unit.CancelOrders();
        _unit.World?.CancelChannel(_unit);
    }
}
