using Godot;

namespace Launcher;

/// <summary>
/// 魅惑 ANch（黑暗游侠终极）。对标客户端 Src/Skills/Charm.cs：
/// 把 DataA（5）级以内的非英雄敌方单位永久变成自己的。物编 Cool=45 Cost=150 Rng=700，只有 1 级。
/// </summary>
public sealed class Charm : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int MaxUnitLevel => Mathf.Max(1, Mathf.RoundToInt(Data('A')));

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding || target.IsUnderConstruction) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Type != null && target.Type.Level > MaxUnitLevel) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;
        target.TransferTo(Owner.Player);
        target.AutoCast = Owner.AutoCast;
        return true;
    }

    /// <summary>AI：挑等级够低、不是英雄的敌方单位。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            !other.IsHero && !other.IsBuilding && other.Type != null && other.Type.Level <= MaxUnitLevel);
        return target != null && world.TryCast(Owner, this, target, target.Position);
    }
}