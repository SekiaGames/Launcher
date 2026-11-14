using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    internal void RecallControlGroup(int group, bool center)
    {
        if (_selection == null) return;
        _selection.RecallControlGroup(group);
        if (!center || _selected.Count == 0) return;

        Vector3 sum = Vector3.Zero;
        foreach (Unit unit in _selected) sum += unit.Position;
        _camera?.Focus(sum / _selected.Count);
    }

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
        // 单位选择与"选中的地面物品"互斥：只要选了单位就收起物品详情。
        if (_selected.Count > 0) _selectedGroundItem = null;
        Unit primary = PrimarySelection;
        if (primary != null && !_selected.Contains(primary))
            primary = _selected.Count > 0 ? _selected[0] : null;
        IReadOnlyList<Unit> cargo = UI.SelectionGroupFrame.DisplayedCargo(primary, _selected,
            primary != null && CanControl(primary));
        _hud?.SetSelection(_selected, primary, cargo);
    }

    /// <summary>地面物品被点中：清掉单位选择，右下角改显示物品详情（客户端 SelectGroundItem）。</summary>
    internal void SelectGroundItem(Item item)
    {
        if (item == null || !item.OnGround) return;
        _selection?.Clear();
        _selectedGroundItem = item;
        RefreshHud();
    }

    internal void ClearGroundItemSelection()
    {
        if (_selectedGroundItem == null) return;
        _selectedGroundItem = null;
        RefreshHud();
    }

    private void UpdateGroundItemDetail()
    {
        Item item = _selectedGroundItem;
        if (item != null && (!item.OnGround || item.Node == null)) _selectedGroundItem = item = null;
        if (item == null || _selected.Count > 0)
        {
            _newHud?.SetGroundItem(null, 0);
            return;
        }
        _newHud?.SetGroundItem(item.Type, World.ChargesLeft(item));
    }

    internal bool CanControl(Unit unit, bool advanced = false)
        => _world?.CanControl(unit, ViewPlayer, advanced) == true;

    /// <summary>Tab: 在当前选择内部轮换主位到下一个子组（原版 "Switch between subgroups"）。</summary>
    public void CycleSubgroup() => _selection?.CycleSubgroup();

    private void CloseLearnMenu()
    {
        _newHud?.CloseLearnMenu();
        UpdateCommandCard();
    }

    private void CycleTownCenters()
    {
        if (_world == null) return;
        var halls = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer || !unit.IsBuilding) continue;
            if (unit.Type.FoodMade <= 0 || unit.Type.Trains.Length == 0) continue;
            if (!CanControl(unit)) continue;
            halls.Add(unit);
        }
        if (halls.Count == 0) return;

        Unit current = PrimarySelection;
        int start = 0;
        for (int index = 0; index < halls.Count; index++)
        {
            if (!ReferenceEquals(halls[index], current)) continue;
            start = (index + 1) % halls.Count;
            break;
        }
        Unit next = halls[start];
        _selection?.SelectSingle(next);
        _camera?.Focus(next.Position);
    }

    private void CenterCameraOnSelection()
    {
        Unit primary = PrimarySelection;
        if (primary != null) _camera?.Focus(primary.Position);
    }

    internal void ToggleFormationMovement()
    {
        FormationMovementEnabled = !FormationMovementEnabled;
        _newHud?.Minimap.SetFilterState(MinimapFilter.AllyState, MinimapFilter.ShowCreeps,
            MinimapFilter.AllyEnabled, MinimapFilter.CreepEnabled, FormationMovementEnabled);
    }

    internal bool TryMinimapAltShortcut(char key)
        => _newHud?.Minimap.HandleAltShortcut(key) == true;

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

    private Unit PickUnit(Vector2 screenPosition, Func<Unit, bool> predicate)
        => _picking?.PickUnit(screenPosition, predicate);
}