using Godot;

namespace Launcher;

/// <summary>被腐化的金矿（Abgm）：只是标记这口矿自带腐地，采集逻辑不需要额外处理。</summary>
public sealed class CAbilityBlightedGoldMine : Ability
{
    public override bool IsPassive => true;
}
