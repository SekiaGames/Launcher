using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 镜像分身 AOmi（剑圣）。对标 Wiki RuntimeMirrorImageAbility：
/// 本体随机换到一圈里的某个位置，其余位置各站一个幻象。
/// 物编 DataA 幻象数(1/2/3)、DataB 造成伤害倍率(0)、DataC 受到伤害倍率(2)、
/// Rng(128) 站位半径、Dur(60) 秒。再放一次会先收掉上一批，避免越叠越多。
/// </summary>
public sealed class RuntimeMirrorImageAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int ImageCount => Mathf.Clamp(Mathf.RoundToInt(Data('A')), 0, 9);
    public float DamageDealtFactor => Data('B');
    public float DamageReceivedFactor => Data('C') > 0f ? Data('C') : 1f;
    public float PlacementRadius => CastRange > 0f ? CastRange : 128f;
    public float ImageSeconds => Duration > 0f ? Duration : 60f;

    private readonly List<Unit> _images = new();

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        DismissImages(world);

        int count = ImageCount;
        if (count <= 0) return false;

        int slots = count + 1;
        int realSlot = world.RandomIndex(slots);
        Vector3 origin = Owner.Position;
        float facing = Owner.Facing;

        Vector3 ownOffset = FormationOffset(realSlot, slots, PlacementRadius, facing);
        Owner.Position = new Vector3(origin.X + ownOffset.X, origin.Y, origin.Z + ownOffset.Z);
        if (Owner.View != null) Owner.View.Root.Position = Owner.Position;

        for (int index = 0; index < slots; index++)
        {
            if (index == realSlot) continue;

            Vector3 offset = FormationOffset(index, slots, PlacementRadius, facing);
            var spot = new Vector3(origin.X + offset.X, origin.Y, origin.Z + offset.Z);
            Unit image = world.CreateIllusion(Owner, spot, ImageSeconds, DamageDealtFactor, DamageReceivedFactor);
            if (image != null) _images.Add(image);
        }
        return true;
    }

    public override void OnOwnerDeath(World world) => DismissImages(world);

    private void DismissImages(World world)
    {
        foreach (Unit image in _images)
        {
            world?.DismissIllusion(image);
        }
        _images.Clear();
    }

    private static Vector3 FormationOffset(int index, int slots, float radius, float facing)
    {
        float angle = Mathf.Tau * index / slots;
        float x = Mathf.Sin(angle) * radius;
        float z = Mathf.Cos(angle) * radius;
        return new Vector3(
            x * Mathf.Cos(facing) - z * Mathf.Sin(facing),
            0f,
            x * Mathf.Sin(facing) + z * Mathf.Cos(facing));
    }

    /// <summary>AI：交战且身上没幻象时放（镜像是保命 + 迷惑，冷却只有 3 秒）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (_images.Count > 0) return false;
        if (!HasEnemyNearby(world, 900f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
