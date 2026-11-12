using Godot;

namespace OpenWC3;

/// <summary>蝗群的放出节奏：每 Interval 只出一只，出满 Count 只后结束。</summary>
public sealed class LocustSwarmRelease : IGroundSpell
{
    public Unit Caster;
    public string UnitId;
    public int Count;
    public float Interval;
    public float Lifetime;
    public float Factor;

    private int _released;
    private float _timer;

    public int Released => _released;

    public bool Step(World world, float delta)
    {
        if (Caster == null || !Caster.Alive) return false;
        if (_released >= Count) return false;

        _timer -= Mathf.Max(0f, delta);
        int guard = 0;
        while (_timer <= 0f && _released < Count && guard++ < 40)
        {
            Unit locust = world.Summon(Caster, UnitId, Caster.Position, Lifetime);
            if (locust != null)
            {
                var agent = new LocustAgent { Owner = locust, Caster = Caster, Factor = Factor, Level = 1 };
                locust.Abilities.Add(agent);
            }
            _released++;
            _timer += Mathf.Max(World.Tick, Interval);
        }
        return _released < Count;
    }
}
