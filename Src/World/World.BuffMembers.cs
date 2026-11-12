using Godot;

namespace Launcher;

public sealed partial class World
{
    public void AwardGold(int player, int gold, Unit victim)
    {
        if (gold <= 0) return;
        BountyAwarded?.Invoke(player, gold, victim);
    }

    public Buff ApplyBuff(Unit unit, Buff buff, float seconds)
    {
        if (unit == null || buff == null) return null;

        buff.Owner = unit;
        buff.Remaining = Mathf.Max(Tick, seconds);
        unit.Buffs.Add(buff);
        buff.OnApply(this);
        return buff;
    }

    public int DispelBuffs(Unit target, Unit caster, bool harmful, bool helpful, float summonDamage)
    {
        if (target == null || !target.Alive) return 0;

        int removed = 0;
        for (int index = target.Buffs.Count - 1; index >= 0; index--)
        {
            Buff buff = target.Buffs[index];
            bool match = harmful && buff.IsHarmful || helpful && !buff.IsHarmful;
            if (!match) continue;

            target.Buffs.RemoveAt(index);
            buff.OnRemove(this);
            removed++;
        }

        if (removed > 0) target.ClearSlows();

        if (summonDamage > 0f && target.IsSummon && target.Alive)
        {
            DealDamage(MakeAbilityDamage(caster, target, summonDamage, AttackType.Spells, DamageType.Magic));
        }
        return removed;
    }

    public int DispelArea(Unit caster, Vector3 center, float radius, bool hostile, float summonDamage)
    {
        if (caster == null || radius <= 0f) return 0;

        int total = 0;
        foreach (Unit unit in Units)
        {
            if (unit == null || !unit.Alive) continue;
            float dx = center.X - unit.Position.X;
            float dz = center.Z - unit.Position.Z;
            if (dx * dx + dz * dz > radius * radius) continue;

            bool enemy = !AreAllied(caster.Player, unit.Player);
            if (enemy != hostile) continue;

            total += DispelBuffs(unit, caster, harmful: !enemy, helpful: enemy, summonDamage);
        }
        return total;
    }

    public bool IsDetectedBy(int viewerPlayer, Unit hidden)
    {
        if (hidden == null) return false;

        foreach (Unit unit in Units)
        {
            if (unit == null || !unit.Alive || unit == hidden) continue;
            if (unit.Player != viewerPlayer && !IsAllied(unit.Player, viewerPlayer)) continue;

            foreach (Ability ability in unit.Abilities)
            {
                if (!ability.Learned) continue;
                float radius = ability.PassiveDetectRadius;
                if (radius <= 0f) continue;
                if (unit.DistanceTo(hidden) <= radius) return true;
            }
        }
        return false;
    }
}