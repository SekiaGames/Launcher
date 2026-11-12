using Godot;

namespace OpenWC3;

/// <summary>
/// 凤凰 AHpx（血法师终极）。对标 Wiki RuntimePhoenixAbility：
/// 在施法者身边召出 UnitID(hphx)，凤凰自带每秒 -25 生命（50 秒自然烧尽），
/// 阵亡时由 <see cref="RuntimePhoenixEggBuff"/> 接管变蛋 10 秒再孵化。物编 Cool=180 Cost=175 Area=200。
/// </summary>
public sealed class Phoenix : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public string PhoenixUnitId => Type?.SummonAt(Level) ?? "hphx";
    public float SpawnRadius => Area > 0f ? Area : 200f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        Vector3 spot = Owner.Position;
        Unit phoenix = world.Summon(Owner, PhoenixUnitId, spot, 0f);
        if (phoenix == null) return false;

        phoenix.Facing = Owner.Facing;
        return true;
    }

    /// <summary>AI：交战才放（冷却 180 秒，空放等于整场少一个大招）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 1200f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
