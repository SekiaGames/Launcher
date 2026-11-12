using Godot;

namespace OpenWC3;

public sealed class RuntimeDevourMagicAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float Radius => Area > 0f ? Area : 200f;
    public float ManaPerDispel => Data('E');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        int count = world.DispelArea(Owner, Owner.Position, Radius, hostile: true, summonDamage: 0f);
        if (count > 0 && ManaPerDispel > 0f)
        {
            Owner.Mana = Mathf.Min(Owner.MaxMana, Owner.Mana + count * ManaPerDispel);
        }
        return count > 0;
    }

    public override bool TryAutoCast(World world)
    {
        foreach (Unit enemy in world.EnemiesInRange(Owner, Owner.Position, Radius))
        {
            if (enemy.IsBuilding) continue;
            if (enemy.HasBuff(harmful: false) || enemy.IsSummon)
            {
                return world.TryCast(Owner, this, null, Owner.Position);
            }
        }
        return false;
    }
}
