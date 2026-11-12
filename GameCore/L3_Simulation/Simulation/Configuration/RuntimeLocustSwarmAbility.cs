using Godot;

namespace Launcher;

/// <summary>
/// 蝗群 AUls（地穴领主终极）。对标 Wiki RuntimeLocustSwarmAbility：
/// 每隔 DataB(0.2s) 放出一只 UnitID(uloc)，共 DataA(20) 只，Dur(30) 秒后消失；
/// 蝗虫自带 Aloc（无碰撞），会自己扑向附近敌人。物编 Cool=180 Cost=150。
/// </summary>
public sealed class RuntimeLocustSwarmAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int SwarmCount => Mathf.Clamp(Mathf.RoundToInt(Data('A')), 1, 40);
    public float ReleaseInterval => Data('B') > 0.05f ? Data('B') : 0.05f;
    public float SwarmDuration => Duration > 0f ? Duration : 30f;
    public string SwarmUnitId => Type?.SummonAt(Level) ?? "uloc";

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.AddGroundSpell(new LocustSwarmRelease
        {
            Caster = Owner,
            UnitId = SwarmUnitId,
            Count = SwarmCount,
            Interval = ReleaseInterval,
            Lifetime = SwarmDuration,
            Factor = Data('D'),
        });
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (Owner == null || !IsFighting) return false;
        float radius = Mathf.Max(700f, Owner.Type?.AcquireRange ?? 0f);
        int enemies = NearbyEnemyCount(world, radius, out bool anyHero);
        if (enemies < 3 && !anyHero) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
