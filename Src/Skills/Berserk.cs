using Godot;

namespace Launcher;

public sealed class Berserk : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float MoveSpeedPct => Data('A');
    public float AttackSpeedPct => Data('B');
    public float DamageTakenPct => Data('C');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        world.ApplyBuff(Owner, new StatBuff
        {
            MoveSpeedPct = MoveSpeedPct,
            AttackSpeedPct = AttackSpeedPct,
            DamageTakenPct = DamageTakenPct,
        }, Duration > 0f ? Duration : 12f);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 700f)) return false;
        if (Owner.FindBuff<StatBuff>() != null) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
