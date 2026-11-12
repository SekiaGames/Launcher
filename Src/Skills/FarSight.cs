using Godot;

namespace Launcher;

/// <summary>
/// 远视 AOfs（先知）。对标客户端 Src/Skills/CampaignAndAuraSkills.cs 的 FarSight：
/// 点地面后把 Area（600/1800/4000）半径内保持可见 Dur（8s）；物编 Rng=99999 是「全图可点」，
/// Cost=75/60/50。AI 的地图知识本身不受迷雾限制，所以不自动施放（留给玩家用）。
/// </summary>
public sealed class FarSight : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;

    public override bool CheckTarget(World world, Unit target, Vector3 point) => true;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        float radius = Area > 0f ? Area : 600f;
        float duration = Duration > 0f ? Duration : 8f;
        world.AddFogReveal(Owner.Player, Flatten(point), radius, duration);
        return true;
    }
}
