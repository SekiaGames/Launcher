using Godot;

namespace Launcher;

/// <summary>
/// 变形术 Aply（女巫）。把目标变成 DataB 指定的那只羊（nshe），持续 Dur(60) 秒。
/// 和妖术共用同一条变形链路，区别只是目标类型固定、不随机。
/// </summary>
public sealed class Polymorph : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;

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

        UnitType sheep = UnitCatalog.Get(Type.DataTextAt(Level, 'B'));
        if (sheep == null) return false;

        return world.BeginMorph(target, sheep, Duration, disable: true);
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = Owner?.Target;
        if (target == null || !target.Alive || target.Morphed) return false;
        return world.TryCast(Owner, this, target, target.Position);
    }
}
