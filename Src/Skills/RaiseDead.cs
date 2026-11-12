using Godot;

namespace Launcher;

/// <summary>
/// 复活死尸 Arai（亡灵巫师）。对标客户端：从附近尸体里拉出 DataA 个骷髅（UnitID=uske）。
/// 物编：Cool=8 Cost=75 Rng=600 Dur=40 DataA=2。
/// </summary>
public sealed class RaiseDead : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int Count => Mathf.Max(1, (int)Data('A'));
    public float Radius => CastRange > 0f ? CastRange : 600f;
    public string SkeletonId => Type?.SummonAt(Level) ?? "uske";

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (Owner == null || world?.SummonHandler == null) return false;

        var corpses = new System.Collections.Generic.List<Unit>();
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse || other.IsHero) continue;
            if (Owner.DistanceTo(other) > Radius) continue;
            corpses.Add(other);
            if (corpses.Count >= Count) break;
        }
        if (corpses.Count == 0) return false;

        foreach (Unit corpse in corpses)
        {
            Vector3 where = corpse.Position;
            world.ConsumeCorpse(corpse);
            world.Summon(Owner, SkeletonId, where, Duration > 0f ? Duration : 40f);
        }
        return true;
    }

    /// <summary>自动施法：有交战目标、又有尸体可拉时走正常施法流程（扣法力/进冷却）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (Owner?.Target == null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
