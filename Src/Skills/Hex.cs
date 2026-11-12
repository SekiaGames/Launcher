using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 妖术 AOhx（暗影猎手）。目标 Dur(15/30/45) 秒变成一只随机小动物：
/// 小动物 id 来自物编 DataB..DataE 四列（每列一串 npig,nsea…），变形期间禁攻击禁施法、仍能移动。
/// 物编：Cool=7 Cost=70 Rng=800。
/// </summary>
public sealed class Hex : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Control;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsBuilding || target.IsUnderConstruction) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.State.Invulnerable || target.State.MagicImmune) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || target == null || !target.Alive) return false;

        string critterId = PickCritter(world);
        UnitType critter = UnitCatalog.Get(critterId);
        if (critter == null) return false;

        return world.BeginMorph(target, critter, Duration, disable: true);
    }

    /// <summary>四列小动物合起来随机挑一只（原版就是随机小动物）。</summary>
    private string PickCritter(World world)
    {
        var ids = new List<string>();
        foreach (char slot in "BCDE")
        {
            string raw = Type.DataTextAt(Level, slot);
            if (string.IsNullOrWhiteSpace(raw) || raw == "-") continue;

            foreach (string part in raw.Split(',', ';'))
            {
                string id = part.Trim();
                if (id.Length >= 4) ids.Add(id[..4]);
            }
        }

        return ids.Count == 0 ? "" : ids[world.RandomIndex(ids.Count)];
    }

    /// <summary>AI：控制优先，挑范围内的敌方英雄变羊，没有英雄再挑最近的。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.IsBuilding && !other.Morphed);
        if (target == null) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
