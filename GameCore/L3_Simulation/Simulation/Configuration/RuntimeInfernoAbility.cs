using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 地狱火 AUin（恐惧魔王终极）。落点 Area(250) 内敌人吃 DataA(50) 伤害并眩晕 Dur(4) 秒，
/// 然后召出 UnitID(ninf) 存在 DataB(180) 秒。
/// 物编 Cost=175 Cool=180 Rng=900。
/// </summary>
public sealed class RuntimeInfernoAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;

    public float SummonSeconds => Data('B') > 0f ? Data('B') : 180f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        return FlatDistance(Owner.Position, point) <= Mathf.Max(1f, CastRange) + 120f;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        Vector3 landing = Flatten(point);
        foreach (Unit victim in world.EnemiesInRange(Owner, landing, Area))
        {
            world.DealDamage(World.MakeAbilityDamage(Owner, victim, Data('A'), AttackType.Spells, DamageType.Magic));
            if (victim.Alive) victim.State.Add(StateMod.Stun, Duration);
        }

        world.Summon(Owner, Type.SummonAt(Level), landing, SummonSeconds);
        return true;
    }
}
