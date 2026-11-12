namespace Launcher;

public sealed partial class Unit
{
    /// <summary>换阵营（魅惑 / 灵魂保存）：清掉全部指令，界面队伍色跟着换。</summary>
    public void TransferTo(int player)
    {
        Player = player;
        CancelAttack();
        Target = null;
        MoveTarget = null;
        Path = null;
        PathIndex = 0;
        RepathTimer = 0f;
        GoldMine = null;
        Tree = null;
        BuildSite = null;
        BuildingNow = false;
        RallyPoint = null;
        RallyTarget = null;
        RallyTree = null;
        TargetFacing = Facing;
        View?.SetTeamColor(UnitSceneComponent.ColorOf(player));
    }
}