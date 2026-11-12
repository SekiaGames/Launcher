using Godot;

namespace OpenWC3;

/// <summary>
/// 占据（Apos）：抢一个非英雄、非飞行、等级不超过 DataA 的敌方单位，代价是女妖自己。
/// 转属在施法当帧完成，自毁延后一帧，避免施法管线在扣法力/进冷却时对着已死单位收尾。
/// </summary>
public sealed class RuntimePossessionAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int MaxUnitLevel => Mathf.Max(0, Mathf.RoundToInt(Data('A')));

    private bool _consumeSelf;
    private float _consumeDelay;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding || target.IsUnderConstruction) return false;
        if (target.Type != null && (target.Type.Flying || target.Type.Level > MaxUnitLevel)) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        return InRange(target);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (!CheckTarget(world, target, point)) return false;

        target.TransferTo(Owner.Player);
        target.AutoCast = Owner.AutoCast;
        _consumeSelf = true;
        _consumeDelay = World.FrameSeconds * 2f;
        return true;
    }

    public override void OnWorldTick(World world, float delta)
    {
        if (!_consumeSelf) return;

        _consumeDelay -= delta;
        if (_consumeDelay > 0f) return;

        _consumeSelf = false;
        if (Owner != null && Owner.Alive) world.KillUnit(Owner);
    }

    public override void OnOwnerDeath(World world)
    {
        _consumeSelf = false;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            !other.IsHero && !other.IsBuilding && other.Type != null
            && !other.Type.Flying && other.Type.Level <= MaxUnitLevel && other.Type.Level >= 2);
        return target != null && world.TryCast(Owner, this, target, target.Position);
    }
}
