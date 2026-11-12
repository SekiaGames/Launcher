using Godot;

namespace OpenWC3;

/// <summary>烈焰风暴的地面结算：满伤阶段 + 余烬阶段，建筑按系数打折。</summary>
public sealed class FlameStrikeBurn : IGroundSpell
{
    public Unit Source;
    public Vector3 Point;
    public float Radius;
    public float TotalDuration;
    public float FullDuration;
    public float FullDamage;
    public float FullInterval;
    public float HalfDamage;
    public float HalfInterval;
    public float BuildingFactor;
    public float Timer;

    private float _elapsed;

    public bool Step(World world, float delta)
    {
        _elapsed += Mathf.Max(0f, delta);
        if (_elapsed > TotalDuration + World.FrameSeconds * 0.5f) return false;

        bool full = _elapsed <= FullDuration + World.FrameSeconds * 0.5f;
        float damage = full ? FullDamage : HalfDamage;
        float interval = Mathf.Max(World.FrameSeconds, full ? FullInterval : HalfInterval);

        Timer -= Mathf.Max(0f, delta);
        int guard = 0;
        while (Timer <= 0f && guard++ < 16)
        {
            DamageWave(world, damage);
            Timer += interval;
        }
        return true;
    }

    private void DamageWave(World world, float damage)
    {
        if (damage <= 0f || Source == null) return;

        foreach (Unit victim in world.EnemiesInRange(Source, Point, Radius))
        {
            float amount = damage * (victim.IsBuilding ? BuildingFactor : 1f);
            if (amount <= 0f) continue;
            world.DealDamage(World.MakeAbilityDamage(Source, victim, amount, AttackType.Spells, DamageType.Magic));
        }
    }
}
