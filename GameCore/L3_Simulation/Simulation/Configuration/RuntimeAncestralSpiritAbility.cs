using Godot;
using System;

namespace Launcher;

/// <summary>
/// 牛头人祖灵（Aast，灵魂行者）。对标客户端 RuntimeAncestralSpiritAbility：
/// 不选目标，在 Rng(350) 内自己找一具己方牛头人尸体（优先等级高的），
/// 按 DataA 生命比例、DataB 法力比例原地复活；尸体要还在场地、不能被别的载体占着。
/// </summary>
public sealed class RuntimeAncestralSpiritAbility : Ability
{
    /// <summary>原版靠单位分类 Tauren 选目标，导出的表里没有分类列，这里按牛头人单位码认。</summary>
    private static readonly string[] TaurenTypes = { "otau", "otot" };

    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float SearchRange => CastRange > 0f ? CastRange : 350f;
    public float LifeFactor => Mathf.Clamp(Data('A'), 0.01f, 1f);
    public float ManaFactor => Mathf.Clamp(Data('B'), 0f, 1f);

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        Unit corpse = target != null && CanRevive(world, target) ? target : FindCorpse(world);
        if (corpse == null) return false;

        Revive(corpse);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (world == null || Owner == null) return false;
        if (!HasEnemyNearby(world, 900f)) return false;
        if (FindCorpse(world) == null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }

    private bool CanRevive(World world, Unit unit)
    {
        if (unit == null || unit.Alive || !unit.IsCorpse) return false;
        if (unit.IsHero || unit.IsBuilding || unit.Type == null) return false;
        if (!world.AreAllied(Owner.Player, unit.Player)) return false;
        if (Owner.DistanceTo(unit) > SearchRange) return false;
        return IsTauren(unit);
    }

    private static bool IsTauren(Unit unit)
    {
        foreach (string id in TaurenTypes)
        {
            if (string.Equals(unit.Type.Id, id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return unit.Type.Name != null && unit.Type.Name.Contains("牛头人");
    }

    private Unit FindCorpse(World world)
    {
        Unit best = null;
        foreach (Unit other in world.Units)
        {
            if (!CanRevive(world, other)) continue;
            if (best == null || other.Level > best.Level || (other.Level == best.Level && Owner.DistanceTo(other) < Owner.DistanceTo(best)))
            {
                best = other;
            }
        }
        return best;
    }

    private void Revive(Unit corpse)
    {
        corpse.ReviveFromCorpse(corpse.Position, LifeFactor);
        if (corpse.MaxMana > 0f) corpse.Mana = corpse.MaxMana * ManaFactor;
    }
}
