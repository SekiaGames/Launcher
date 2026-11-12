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
