using Godot;

namespace OpenWC3.Game;

public sealed class RuntimeClusterRocketsAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float Radius => Area > 0f ? Area : 200f;
    public int Rockets => Mathf.Max(1, Mathf.RoundToInt(Data('C')));
    public float DamagePerRocket => Data('A');

    public override bool CheckTarget(World world, Unit target, Vector3 point) => Owner != null;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        Vector3 spot = Flatten(point);
        for (int index = 0; index < Rockets; index++)
        {
            foreach (Unit victim in world.EnemiesInRange(Owner, spot, Radius))
            {
                world.DealDamage(World.MakeAbilityDamage(Owner, victim, DamagePerRocket,
                    AttackType.Siege, DamageType.Magic));
            }
        }
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, target.Position);
    }
}
