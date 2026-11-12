using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 腐尸甲虫 AUcb（地穴领主）。从目标尸体里爬出 DataA(1) 只甲虫（UnitID=ucs1/ucs2/ucs3），
/// 尸体当场消失；甲虫不限期，直到被杀。上限 DataE=5：到上限就放不出来，超出的按等级低→离得远先死。
/// 物编：Cool=6 Cost=30 Rng=900 Area=900。
/// </summary>
public sealed class RuntimeCarrionBeetlesAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;
    public override bool TargetsCorpse => true;

    private int MaximumBeetles => Mathf.Max(0, Mathf.RoundToInt(Data('E')));

    private int SummonCount => Mathf.Max(1, Mathf.RoundToInt(Data('A')));

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || target.Alive || !target.IsCorpse) return false;
        if (!InRange(target)) return false;
        return OwnedBeetles(world).Count + SummonCount <= MaximumBeetles;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || target.Alive) return false;

        Vector3 center = target.Position;
        world.ConsumeCorpse(target);

        string unitId = Type.DataTextAt(Level, 'C');
        if (string.IsNullOrEmpty(unitId) || unitId == "-") unitId = Type.SummonAt(Level);
        int count = SummonCount;
        for (int index = 0; index < count; index++)
        {
            float angle = Mathf.Tau * index / count;
            Vector3 offset = new(Mathf.Cos(angle) * 48f, 0f, Mathf.Sin(angle) * 48f);
            world.Summon(Owner, unitId, center + offset, 0f);
        }

        TrimExcessBeetles(world);
        return true;
    }

    /// <summary>超出上限时按客户端口径淘汰：等级低的先死，同级离施法者远的先死。</summary>
    private void TrimExcessBeetles(World world)
    {
        List<Unit> owned = OwnedBeetles(world);
        int excess = owned.Count - MaximumBeetles;
        if (excess <= 0) return;

        owned.Sort((left, right) =>
        {
            int rank = BeetleRank(left).CompareTo(BeetleRank(right));
            if (rank != 0) return rank;
            return Owner.DistanceTo(right).CompareTo(Owner.DistanceTo(left));
        });
        for (int index = 0; index < excess && index < owned.Count; index++)
        {
            world.KillUnit(owned[index]);
        }
    }

    private List<Unit> OwnedBeetles(World world)
    {
        var found = new List<Unit>();
        if (world == null || Owner == null) return found;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || other.Summoner != Owner) continue;
            if (BeetleRank(other) < 0) continue;
            found.Add(other);
        }
        return found;
    }

    /// <summary>甲虫等级档：物编 DataC/DataD 在 1~3 级分别是哪种甲虫。</summary>
    private int BeetleRank(Unit unit)
    {
        string id = unit?.Type?.Id;
        if (string.IsNullOrEmpty(id) || Type == null) return -1;
        for (int level = 3; level >= 1; level--)
        {
            if (id == Type.DataTextAt(level, 'C') || id == Type.DataTextAt(level, 'D')) return level - 1;
        }
        return -1;
    }

    /// <summary>AI：射程内有尸体就召（尸体是最稀缺的资源，不挑是哪方的）。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit corpse = null;
        float bestDistance = float.MaxValue;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse) continue;
            if (!InRange(other)) continue;
            float distance = Owner.DistanceTo(other);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            corpse = other;
        }
        return corpse != null && world.TryCast(Owner, this, corpse, corpse.Position);
    }
}
