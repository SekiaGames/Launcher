using Godot;

namespace Launcher;

/// <summary>
/// 龙卷风 ANto（海巫）。对标 Wiki RuntimeTornadoAbility：
/// 在落点召出 UnitID(ntor)，施法者要一直引导 Dur(40) 秒（第 2、3 级 30 秒）。
/// 引导一断（被打断、自己动、蓝不够）龙卷风立刻散掉。物编 Cool=120 Cost=200 Rng=700。
/// </summary>
public sealed class RuntimeTornadoAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;
    public override bool IsChanneled => true;

    public string TornadoUnitId => Type?.SummonAt(Level) ?? "ntor";
    public float Lifetime => Duration > 0f ? Duration : 40f;
    public float SpawnRadius => Area > 0f ? Area : 128f;
    public override float ChannelTotal => Lifetime;

    private Unit _summoned;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float range = CastRange > 0f ? CastRange : 700f;
        return FlatDistance(Owner.Position, point) <= range;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        _summoned = world.Summon(Owner, TornadoUnitId, Flatten(point), Lifetime);
        if (_summoned == null) return false;

        if (!world.BeginChannel(Owner, this, Flatten(point), Lifetime, 0f))
        {
            world.KillUnit(_summoned, Owner);
            _summoned = null;
            return false;
        }
        return true;
    }

    public override void OnChannelEnded(World world, bool interrupted)
    {
        Unit summoned = _summoned;
        _summoned = null;
        if (summoned == null || !summoned.Alive) return;
        summoned.ExpireSummon();
    }

    /// <summary>AI：身边打起来了才放（引导 40 秒不能动，空放等于整场站桩）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 1200f)) return false;

        Unit target = FindEnemyInRange(world);
        Vector3 point = target?.Position ?? Owner.Position;
        return world.TryCast(Owner, this, null, point);
    }
}
