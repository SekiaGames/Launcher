using Godot;

namespace Launcher;

/// <summary>
/// 凤凰 AHpx（血法师终极）。对标 Wiki RuntimePhoenixAbility：
/// 在施法者身边召出 UnitID(hphx)，凤凰自带每秒 -25 生命（50 秒自然烧尽），
/// 阵亡时由 <see cref="PhoenixEgg"/> 接管变蛋 10 秒再孵化。物编 Cool=180 Cost=175 Area=200。
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

/// <summary>
/// 凤凰蛋 Aphx。对标 Wiki RuntimePhoenixEggBuff：凤凰生命耗尽时不是死亡，
/// 而是换成 UnitID(hpxe) 蛋形态，HeroDur(10) 秒后孵化回 hphx 并满血。
/// 蛋被打死就真的死了。技能不出现在命令卡上（原版 NativeCommandProviderEmpty）。
/// </summary>
public sealed class PhoenixEgg : Ability
{
    public override bool IsPassive => true;

    public string EggUnitId => Type?.SummonAt(Level) ?? "hpxe";
    public float HatchSeconds => HeroDuration > 0f ? HeroDuration : 10f;
}