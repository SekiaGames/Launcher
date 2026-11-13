namespace OpenWC3;

public abstract record PlayerCommand(int PlayerId);

public sealed record SelectUnitCommand(int PlayerId, Unit Unit, bool Additive) : PlayerCommand(PlayerId);
public sealed record DeselectCommand(int PlayerId, Unit Unit) : PlayerCommand(PlayerId);
public sealed record MoveCommand(int PlayerId, Vec2 Destination, bool Queued) : PlayerCommand(PlayerId);
public sealed record AttackCommand(int PlayerId, Unit Target, bool Queued) : PlayerCommand(PlayerId);
public sealed record AttackMoveCommand(int PlayerId, Vec2 Destination, bool Queued) : PlayerCommand(PlayerId);
public sealed record PatrolCommand(int PlayerId, Vec2 Destination, bool Queued) : PlayerCommand(PlayerId);
public sealed record StopCommand(int PlayerId) : PlayerCommand(PlayerId);
public sealed record HoldPositionCommand(int PlayerId) : PlayerCommand(PlayerId);
public sealed record CastCommand(int PlayerId, Ability Ability, Unit Target, Vec2 Point, bool Queued)
    : PlayerCommand(PlayerId);
public sealed record BuildCommand(int PlayerId, string TypeId, Vec2 Point) : PlayerCommand(PlayerId);