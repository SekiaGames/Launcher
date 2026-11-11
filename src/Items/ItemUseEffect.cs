using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>物品的“用一次”效果：无敌、加速、魔法免疫、群体治疗/恢复、净化、守卫、回城。</summary>
public static class ItemUseEffect
{
    public static bool TryUse(World world, Unit unit, Item item)
    {
        if (world == null || unit == null || item?.Type == null) return false;
        if (string.IsNullOrWhiteSpace(item.Type.AbilList)) return false;

        foreach (string raw in item.Type.AbilList.Split(',', ';', StringSplitOptions.TrimEntries))
        {
            if (raw.Length < 4) continue;
            AbilityType type = AbilityCatalog.Get(raw[..4]);
            if (type == null) continue;
            if (Apply(world, unit, type)) return true;
        }
        return false;
    }

    private static bool Apply(World world, Unit unit, AbilityType type)
    {
        switch ((type.Id ?? "").ToUpperInvariant())
        {
            case "AIVU":
            case "AIVL":
                unit.State.Add(StateMod.Invulnerable, Seconds(type, 15f));
                return true;
            case "AISP":
                world.ApplyBuff(unit, new StatBuff { MoveSpeedPct = type.DataAt(1, 'A') }, Seconds(type, 45f));
                return true;
            case "AIMX":
                unit.State.Add(StateMod.MagicImmune, Seconds(type, type.DataAt(1, 'A')));
                return true;
            case "AIHA":
            case "AIHB":
            case "APH1":
            case "APH2":
            case "APH3":
                HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A'));
                return true;
            case "AIDI":
            case "AIDS":
            case "APDI":
                world.DispelArea(unit, unit.Position, RadiusOf(type), hostile: true, summonDamage: 0f);
                world.DispelArea(unit, unit.Position, RadiusOf(type), hostile: false, summonDamage: 0f);
                return true;
            case "AILS":
                world.ApplyBuff(unit, new LightningShieldBuff
                {
                    DamageSource = unit,
                    Radius = RadiusOf(type),
                    DamagePerSecond = type.DataAt(1, 'A'),
                }, Seconds(type, 20f));
                return true;
            case "AIHW":
                return world.Summon(unit, "ohwd", unit.Position, Seconds(type, 30f)) != null;
            case "AISW":
                return world.Summon(unit, "oeye", unit.Position, Seconds(type, 300f)) != null;
            case "AIRT":
            case "AITP":
                return TownPortal(world, unit, type);
            default:
                return false;
        }
    }

    private static void HealArea(World world, Unit unit, float radius, float amount)
    {
        if (amount <= 0f) return;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, radius))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            ally.Heal(amount);
        }
    }

    private static bool TownPortal(World world, Unit unit, AbilityType type)
    {
        Unit hall = NearestOwnBuilding(world, unit);
        if (hall == null) return false;

        float radius = RadiusOf(type);
        int max = type.DataAt(1, 'A') > 0f ? Mathf.RoundToInt(type.DataAt(1, 'A')) : 12;
        var group = new List<Unit> { unit };
        foreach (Unit other in world.Units)
        {
            if (group.Count >= max) break;
            if (other == null || !other.Alive || ReferenceEquals(other, unit)) continue;
            if (other.IsBuilding || !world.AreAllied(unit.Player, other.Player)) continue;
            if (unit.DistanceTo(other) > radius) continue;
            group.Add(other);
        }

        for (int index = 0; index < group.Count; index++)
        {
            float ring = index == 0 ? 0f : 96f + (index - 1) / 6 * 64f;
            float angle = index == 0 ? 0f : Mathf.Tau * (index - 1) / 6f;
            var spot = new Vector3(
                hall.Position.X + Mathf.Cos(angle) * ring,
                hall.Position.Y,
                hall.Position.Z + Mathf.Sin(angle) * ring);

            Unit member = group[index];
            member.CancelAttack();
            member.Target = null;
            member.MoveTarget = null;
            member.Path = null;
            world.MoveUnitTo(member, spot);
        }
        return true;
    }

    private static Unit NearestOwnBuilding(World world, Unit unit)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || !other.IsBuilding) continue;
            if (other.Player != unit.Player) continue;

            float distance = unit.DistanceTo(other);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        return best;
    }

    private static float RadiusOf(AbilityType type)
    {
        float radius = type.AreaAt(1);
        if (radius > 0f) return radius;
        float range = type.RangeAt(1);
        return range > 0f && range < 2000f ? range : 600f;
    }

    private static float Seconds(AbilityType type, float fallback)
    {
        float duration = type.DurationAt(1);
        return duration > 0f ? duration : fallback;
    }
}