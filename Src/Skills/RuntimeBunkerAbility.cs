using Godot;

namespace Launcher;

/// <summary>
/// 地洞驻军（Abun）。对标客户端 RuntimeBunkerAbility + CargoTransport：
/// DataA 是容量（4 个苦工）、Area 是装载半径；点一下把半径内的苦工收进去，
/// 再点一下（或者 Stand Down）全部放出来。地洞里有人才开火，见 <see cref="RuntimeBattleStationsAbility"/>。
/// </summary>
public sealed class RuntimeBunkerAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public int Capacity => Mathf.Max(1, (int)Data('A'));
    public float LoadRadius => Area > 0f ? Area : 250f;
    public int LoadedCount => Owner?.Cargo.Count ?? 0;

    public override void OnLearned() => ApplyGarrison();

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        ApplyGarrison();

        if (Owner.Cargo.Count > 0) return world.UnloadAllCargo(Owner);
        return world.LoadNearbyWorkers(Owner, LoadRadius) > 0;
    }

    public override void OnOwnerDeath(World world) => world?.ReleaseCargoOnDeath(Owner);
    public override bool TryAutoCast(World world) => false;

    /// <summary>地洞只在装了人的时候开火，所以这里把容量与开关一起写进单位。</summary>
    private void ApplyGarrison()
    {
        if (Owner == null) return;
        Owner.CargoCapacity = Mathf.Max(Owner.CargoCapacity, Capacity);
        Owner.GarrisonOnly = true;
    }
}
