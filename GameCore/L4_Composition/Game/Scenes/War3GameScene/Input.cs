using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    private void PushClick(Vector2 position, MouseButton button)
    {
        var press = new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = true,
            Position = position,
            GlobalPosition = position,
        };
        var release = new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = false,
            Position = position,
            GlobalPosition = position,
        };
        GetViewport().PushInput(press);
        GetViewport().PushInput(release);
    }

    public override void _UnhandledInput(InputEvent @event) => _inputComponent?.HandleEvent(@event);

    /// <summary>
    /// 选择里"当前聚焦"的那个单位。命令卡、头像、热键都跟它走，成员本身不因切子组而减少
    /// （客户端 PlayerSelectionController.Primary）。
    /// </summary>
    private Unit PrimarySelection => _selection?.Primary
        ?? (_selected.Count == 1 ? _selected[0] : null);

    private void RefreshSelection()
    {
        Unit primary = PrimarySelection;
        if (primary != null && !_selected.Contains(primary))
            primary = _selected.Count > 0 ? _selected[0] : null;
        IReadOnlyList<Unit> cargo = UI.SelectionGroupFrame.DisplayedCargo(primary, _selected,
            primary != null && CanControl(primary));
        _hud?.SetSelection(_selected, primary, cargo);
    }

    internal bool CanControl(Unit unit, bool advanced = false)
        => _world?.CanControl(unit, ViewPlayer, advanced) == true;

    /// <summary>Tab: 在当前选择内部轮换主位到下一个子组（原版 "Switch between subgroups"）。</summary>
    public void CycleSubgroup() => _selection?.CycleSubgroup();

    /// <summary>玩家自己的操作改了选择/目标：作废待定的指令与施法（客户端 CancelPending）。</summary>
    public void CancelPending()
    {
        ClearPendingCommand();
        CancelCast();
    }

    /// <summary>把单位从选择里摘掉：走控制器，主位与选/取消选中事件都跟着更新。</summary>
    private bool DropFromSelection(Unit unit)
    {
        if (unit == null || !_selected.Contains(unit)) return false;
        if (_selection != null)
        {
            _selection.Remove(unit);
            return true;
        }
        _selected.Remove(unit);
        RefreshSelection();
        UpdateCommandCard();
        return true;
    }

    private void ClearSelection()
    {
        if (_selection != null)
        {
            _selection.Clear();
            return;
        }
        foreach (Unit unit in _selected) unit.View?.SetSelected(false);
        _selected.Clear();
        RefreshSelection();
        UpdateCommandCard();
    }

    private Unit PickUnit(Vector2 screenPosition, int player, bool anyPlayer = false,
        bool includeCorpses = false)
        => _picking?.PickUnit(screenPosition, player, anyPlayer, includeCorpses);
}