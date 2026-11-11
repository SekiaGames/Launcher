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
            case "AIAN":
                return world.ReviveCorpsesAround(unit.Position, 900f, 6, unit.Player, Seconds(type, 40f), 1f) > 0;
            case "AIRS":
                return world.ReviveCorpsesAround(unit.Position, 900f, 6, unit.Player, 0f, 1f) > 0;
            case "AIMO":
                return world.Summon(unit, "nlur", unit.Position, Seconds(type, 60f)) != null;
            case "AIWM":
                return world.Summon(unit, "nmrr", unit.Position, Seconds(type, 60f)) != null;
            case "AISH":
                return SummonMany(world, unit, "otbk", Mathf.Max(1, Mathf.RoundToInt(type.DataAt(1, 'B'))), Seconds(type, 60f));
            case "AIRV":
                world.AddFogReveal(unit.Player, unit.Position, 100000f, Seconds(type, 5f));
                return true;
            case "AIDC":
                world.DispelArea(unit, unit.Position, RadiusOf(type), hostile: true, summonDamage: 0f);
                world.DispelArea(unit, unit.Position, RadiusOf(type), hostile: false, summonDamage: 0f);
                return true;
            case "APRA":
                HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A'));
                return true;
            case "APMR":
                RestoreManaArea(world, unit, RadiusOf(type), type.DataAt(1, 'A'));
                return true;
            case "APSA":
                foreach (Unit ally in world.AlliesInRange(unit, unit.Position, RadiusOf(type)))
                {
                    if (!ally.Alive || ally.IsBuilding) continue;
                    world.ApplyBuff(ally, new StatBuff { MoveSpeedPct = 0.3f }, Seconds(type, 60f));
                }
                return true;
            case "APWT":
                return world.Summon(unit, "nwad", unit.Position, Seconds(type, 120f)) != null;
            case "APRL":
                return world.ReviveCorpsesAround(unit.Position, 1400f, 1, unit.Player, 0f, 1f) > 0;
            case "APRR":
                return world.ReviveCorpsesAround(unit.Position, 1400f, 3, unit.Player, 0f, 1f) > 0;
            case "ASPP":
                return LinkAllies(world, unit, type);
            case "AIFA":
                world.AddFogReveal(unit.Player, unit.Position, 1800f, Seconds(type, 15f));
                return true;
            case "AITA":
            case "AITB":
                world.AddFogReveal(unit.Player, unit.Position, 900f, Seconds(type, 10f));
                return true;
            case "AIIL":
                return SpawnIllusions(world, unit, type);
            case "AIRD":
                return world.ReviveCorpsesAround(unit.Position, 900f,
                    Mathf.Max(1, Mathf.RoundToInt(type.DataAt(1, 'A'))), unit.Player, 0f, 1f) > 0;
            case "AIPV":
                world.ApplyBuff(unit, new StatBuff { DamagePct = type.DataAt(1, 'A') }, Seconds(type, 12f));
                return true;
            case "AIXS":
                world.ApplyBuff(unit, new AntiMagicShieldBuff
                {
                    Caster = unit,
                    Absorb = type.DataAt(1, 'A') > 0f ? type.DataAt(1, 'A') : 300f,
                }, Seconds(type, 30f));
                return true;
            case "AISE":
                foreach (Unit enemy in world.EnemiesInRange(unit, unit.Position, 500f, false))
                {
                    enemy.State.Add(StateMod.Silence, Seconds(type, 8f));
                }
                return true;
            case "AIGF":
                foreach (Unit ally in world.AlliesInRange(unit, unit.Position, 900f))
                {
                    if (!ally.Alive || !ally.IsBuilding) continue;
                    world.ApplyBuff(ally, new StatBuff { Armor = 3f }, Seconds(type, 60f));
                }
                return true;
            case "AIDA":
                foreach (Unit ally in world.AlliesInRange(unit, unit.Position, 900f))
                {
                    if (!ally.Alive) continue;
                    world.ApplyBuff(ally, new StatBuff { Armor = type.DataAt(1, 'A') }, Seconds(type, 30f));
                }
                return true;
            case "AIFD":
                return world.Summon(unit, "nrdr", unit.Position, Seconds(type, 60f)) != null;
            case "AIFR":
                return world.Summon(unit, "ngst", unit.Position, Seconds(type, 180f)) != null;
            case "AIFH":
                return world.Summon(unit, "nfel", unit.Position, Seconds(type, 180f)) != null;
            case "AIFG":
                world.AddGroundSpell(new CloudOfFogSpell
                {
                    Caster = unit,
                    Position = unit.Position,
                    Radius = RadiusOf(type),
                    Remaining = Seconds(type, 30f),
                    MissChance = 0.7f,
                });
                return true;
            default:
                return false;
        }
    }

    private static bool SummonMany(World world, Unit unit, string unitId, int count, float seconds)
    {
        bool any = false;
        for (int index = 0; index < count; index++)
        {
            float angle = Mathf.Tau * index / Mathf.Max(1, count);
            var spot = new Vector3(
                unit.Position.X + Mathf.Cos(angle) * 80f,
                unit.Position.Y,
                unit.Position.Z + Mathf.Sin(angle) * 80f);
            if (world.Summon(unit, unitId, spot, seconds) != null) any = true;
        }
        return any;
    }

    private static bool SpawnIllusions(World world, Unit unit, AbilityType type)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(type.DataAt(1, 'B')));
        float seconds = Seconds(type, 30f);
        bool any = false;
        for (int index = 0; index < count; index++)
        {
            float angle = Mathf.Tau * index / Mathf.Max(1, count);
            var spot = new Vector3(
                unit.Position.X + Mathf.Cos(angle) * 96f,
                unit.Position.Y,
                unit.Position.Z + Mathf.Sin(angle) * 96f);
            if (world.CreateIllusion(unit, spot, seconds, 1f, 2f) != null) any = true;
        }
        return any;
    }

    private static void RestoreManaArea(World world, Unit unit, float radius, float amount)
    {
        if (amount <= 0f) return;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, radius))
        {
            if (!ally.Alive || ally.IsBuilding || ally.MaxMana <= 0f) continue;
            ally.Mana = Mathf.Min(ally.MaxMana, ally.Mana + amount);
        }
    }

    private static bool LinkAllies(World world, Unit unit, AbilityType type)
    {
        var members = new List<Unit> { unit };
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, RadiusOf(type)))
        {
            if (members.Count >= 4) break;
            if (!ally.Alive || ally.IsBuilding || ally == unit) continue;
            members.Add(ally);
        }
        if (members.Count < 2) return false;

        var group = new SpiritLinkGroup { Members = members, Ratio = Mathf.Clamp(type.DataAt(1, 'A'), 0f, 0.9f) };
        float seconds = Seconds(type, 75f);
        foreach (Unit member in members)
        {
            world.ApplyBuff(member, new SpiritLinkBuff { Group = group }, seconds);
        }
        return true;
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