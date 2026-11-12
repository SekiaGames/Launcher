using Godot;

namespace Launcher;

public sealed class RuntimeStatueReplenishAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 700f;
    public float HpPerSecond => Data('A');
    public float ManaPerSecond => Data('B');
    public float AmountPerTick => Data('E') > 0f ? Data('E') : 1f;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        bool did = false;
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (ally == Owner || ally.IsBuilding || !ally.Alive) continue;

            if (HpPerSecond > 0f && ally.Hp < ally.MaxHp)
            {
                ally.Heal(AmountPerTick);
                did = true;
            }
            if (ManaPerSecond > 0f && ally.MaxMana > 0f && ally.Mana < ally.MaxMana)
            {
                ally.Mana = Mathf.Min(ally.MaxMana, ally.Mana + ManaPerSecond);
                did = true;
            }
        }
        return did;
    }

    public override bool TryAutoCast(World world)
    {
        foreach (Unit ally in world.AlliesInRange(Owner, Owner.Position, Radius))
        {
            if (ally == Owner || ally.IsBuilding || !ally.Alive) continue;
            if (HpPerSecond > 0f && ally.Hp < ally.MaxHp) return world.TryCast(Owner, this, null, Owner.Position);
            if (ManaPerSecond > 0f && ally.Mana < ally.MaxMana) return world.TryCast(Owner, this, null, Owner.Position);
        }
        return false;
    }
}
