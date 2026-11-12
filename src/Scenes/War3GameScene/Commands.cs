using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public sealed partial class War3GameScene
{
    private enum PendingCommandKind
    {
        None,
        Move,
        Attack,
        Patrol,
    }

    private PendingCommandKind _pendingCommand;
    private Key _panBlockedKey = Key.None;

    public bool CommandPending => _pendingCommand != PendingCommandKind.None;

    public void BeginMoveCommand() => BeginPendingCommand(PendingCommandKind.Move, "选择移动落点（Shift 追加队列，右键取消）");

    public void BeginAttackCommand() => BeginPendingCommand(PendingCommandKind.Attack, "选择攻击目标（Shift 追加队列，右键取消）");

    public void BeginPatrolCommand() => BeginPendingCommand(PendingCommandKind.Patrol, "选择巡逻折返点（Shift 追加队列，右键取消）");

    private void BeginPendingCommand(PendingCommandKind kind, string hint)
    {
        if (_selected.Count == 0 || _world == null) return;
        CancelCast();
        _pendingCommand = kind;
        _hud?.SetHint(hint);
    }

    private void ClearPendingCommand()
    {
        _pendingCommand = PendingCommandKind.None;
    }

    public bool TryCommandAt(Vector2 screenPosition)
    {
        if (_pendingCommand == PendingCommandKind.None) return false;

        PendingCommandKind kind = _pendingCommand;
        _pendingCommand = PendingCommandKind.None;

        if (kind == PendingCommandKind.Attack)
        {
            Unit target = PickUnit(screenPosition, ViewPlayer, anyPlayer: true);
            if (target == null)
            {
                _hud?.SetHint("没点到攻击目标");
                return true;
            }
            IssueAttackOrders(target, false);
            return true;
        }

        if (!TryGroundPoint(screenPosition, out Vector3 ground))
        {
            _hud?.SetHint("地面位置无效");
            return true;
        }

        if (kind == PendingCommandKind.Patrol) IssuePatrolOrders(ground, false);
        else IssueMoveOrders(ground, false);
        return true;
    }

    private List<Unit> SelectMovers(out Vector3 centroid)
    {
        var movers = new List<Unit>();
        centroid = Vector3.Zero;
        foreach (Unit unit in _selected)
        {
            if (!unit.Alive || unit.IsBuilding) continue;
            movers.Add(unit);
            centroid += unit.Position;
        }
        if (movers.Count > 0) centroid /= movers.Count;
        return movers;
    }

    public void IssueMoveOrders(Vector3 ground, bool queue)
    {
        if (_world == null) return;

        List<Unit> movers = SelectMovers(out Vector3 centroid);
        if (movers.Count == 0) return;

        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(movers, ground, ground - centroid);
        foreach (Unit unit in movers)
        {
            Vector3 spot = slots.TryGetValue(unit, out Vector3 value) ? value : ground;
            _world.IssueOrder(unit, UnitOrder.Move(spot), queue);
        }
        ApplyGroupSpeedCap(movers, slots, ground);
        if (!queue) _world.ApplyGroupMovePath(movers, centroid, ground, slots);
        PlayVoice(movers[0], "Yes");
        _hud?.SetHint(queue
            ? $"追加移动 {movers.Count} 个单位"
            : $"移动到 ({ground.X:0}, {ground.Z:0})，{movers.Count} 个单位按阵型展开");
    }

    public void IssuePatrolOrders(Vector3 ground, bool queue)
    {
        if (_world == null) return;

        List<Unit> movers = SelectMovers(out Vector3 centroid);
        if (movers.Count == 0) return;

        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(movers, ground, ground - centroid);
        foreach (Unit unit in movers)
        {
            Vector3 spot = slots.TryGetValue(unit, out Vector3 value) ? value : ground;
            _world.IssueOrder(unit, UnitOrder.Patrol(spot), queue);
        }
        ApplyGroupSpeedCap(movers, slots, ground);
        if (!queue) _world.ApplyGroupMovePath(movers, centroid, ground, slots);
        PlayVoice(movers[0], "Yes");
        _hud?.SetHint(queue
            ? $"追加巡逻 {movers.Count} 个单位"
            : $"巡逻：{movers.Count} 个单位往返 ({ground.X:0}, {ground.Z:0})");
    }

    private void ApplyGroupSpeedCap(List<Unit> movers, Dictionary<Unit, Vector3> slots, Vector3 fallback)
    {
        foreach (Unit unit in movers)
        {
            unit.GroupSpeedCap = 0f;
            unit.GroupMoveGoal = default;
        }

        if (movers.Count <= 1) return;

        float cap = float.MaxValue;
        foreach (Unit unit in movers) cap = Mathf.Min(cap, World.SpeedOf(unit));
        foreach (Unit unit in movers)
        {
            unit.GroupSpeedCap = cap;
            unit.GroupMoveGoal = slots.TryGetValue(unit, out Vector3 spot) ? spot : fallback;
        }
    }

    public void IssueAttackOrders(Unit target, bool queue)
    {
        if (_world == null || target == null || !target.Alive) return;

        int attackers = 0;
        foreach (Unit unit in _selected)
        {
            if (!unit.Alive || unit.IsBuilding || !unit.Type.CanAttack) continue;
            _world.IssueOrder(unit, UnitOrder.Attack(target), queue);
            unit.GroupSpeedCap = 0f;
            attackers++;
        }
        if (attackers == 0)
        {
            _hud?.SetHint("选中的单位不能攻击");
            return;
        }
        PlayVoice(_selected[0], "Yes");
        _hud?.SetHint(queue ? $"追加攻击 {target.Type.Name}" : $"攻击 {target.Type.Name}（{attackers} 个单位）");
    }

    public void IssueStopOrders()
    {
        if (_world == null) return;

        int count = 0;
        foreach (Unit unit in _selected)
        {
            if (!unit.Alive || unit.IsBuilding) continue;
            _world.StopUnit(unit);
            count++;
        }
        _hud?.SetHint(count > 0 ? $"停止 {count} 个单位（队列已清空）" : "没有可停止的单位");
        RefreshHud();
        UpdateCommandCard();
    }

    public void IssueHoldOrders()
    {
        if (_world == null) return;

        int count = 0;
        foreach (Unit unit in _selected)
        {
            if (!unit.Alive || unit.IsBuilding) continue;
            _world.HoldPosition(unit);
            count++;
        }
        _hud?.SetHint(count > 0 ? $"原地据守 {count} 个单位（只打射程内）" : "没有可据守的单位");
        RefreshHud();
        UpdateCommandCard();
    }

    private void AddStandardCommands(List<CommandEntry> commands, bool canMove)
    {
        commands.Add(new CommandEntry
        {
            Id = "cmd-move",
            Name = "移动",
            SlotIndex = 0,
            Hotkey = "M",
            Tooltip = "点地面移动。Shift 追加到队列末尾",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNMove.blp"),
            Enabled = canMove,
            Action = BeginMoveCommand,
        });

        commands.Add(new CommandEntry
        {
            Id = "cmd-stop",
            Name = "停止",
            SlotIndex = 1,
            Hotkey = "S",
            Tooltip = "停下手上的指令并清空队列",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNStop.blp"),
            Enabled = true,
            Action = IssueStopOrders,
        });

        commands.Add(new CommandEntry
        {
            Id = "cmd-hold",
            Name = "保持位置",
            SlotIndex = 2,
            Hotkey = "H",
            Tooltip = "原地不动，只攻击进入射程的敌人",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNHoldPosition.blp"),
            Enabled = canMove,
            Action = IssueHoldOrders,
        });

        commands.Add(new CommandEntry
        {
            Id = "cmd-attack",
            Name = "攻击",
            SlotIndex = 3,
            Hotkey = "A",
            Tooltip = "点目标攻击。Shift 追加到队列末尾",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNAttack.blp"),
            Enabled = true,
            Action = BeginAttackCommand,
        });

        commands.Add(new CommandEntry
        {
            Id = "cmd-patrol",
            Name = "巡逻",
            SlotIndex = 4,
            Hotkey = "P",
            Tooltip = "在当前位置与落点之间往返，遇敌自动开火",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNPatrol.blp"),
            Enabled = canMove,
            Action = BeginPatrolCommand,
        });
    }

    private string _subgroupType = "";

    /// <summary>
    /// 子组池：一次点选/框选选中的所有单位。Tab 与「点头像」只切换当前子组（`_selected`），
    /// 池子保持不动，所以切走还能切回来（原版就是这样，不会把别的兵丢掉）。
    /// </summary>
    private readonly List<Unit> _subgroupPool = new();

    private void RememberSubgroupPool()
    {
        _subgroupPool.Clear();
        foreach (Unit unit in _selected)
        {
            if (unit != null && unit.Alive) _subgroupPool.Add(unit);
        }
    }

    /// <summary>把当前子组切到指定类型（单位类型 id），其余池内单位只取消高亮、留在池子里。</summary>
    private void ShowSubgroup(string typeId)
    {
        if (_subgroupPool.Count == 0) return;

        var keep = new List<Unit>();
        foreach (Unit unit in _subgroupPool)
        {
            if (unit == null || !unit.Alive) continue;
            if (!string.Equals(unit.Type?.Id, typeId, StringComparison.OrdinalIgnoreCase)) continue;
            keep.Add(unit);
        }
        if (keep.Count == 0) keep.AddRange(_subgroupPool);

        foreach (Unit unit in _selected) unit.View?.SetSelected(false);
        _selected.Clear();
        foreach (Unit unit in keep)
        {
            _selected.Add(unit);
            unit.View?.SetSelected(true);
        }

        _subgroupType = typeId;
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
    }

    private void CycleSubgroup()
    {
        if (_subgroupPool.Count == 0) RememberSubgroupPool();
        if (_subgroupPool.Count <= 1) return;

        var ids = new List<string>();
        foreach (Unit unit in _subgroupPool)
        {
            if (unit?.Type == null) continue;
            if (!ids.Contains(unit.Type.Id)) ids.Add(unit.Type.Id);
        }
        if (ids.Count <= 1)
        {
            _hud?.SetHint("当前选择只有一种单位");
            return;
        }

        int index = string.IsNullOrEmpty(_subgroupType) ? 0 : ids.IndexOf(_subgroupType);
        index = (index + 1) % ids.Count;
        ShowSubgroup(ids[index]);
        _hud?.SetHint($"子组 {index + 1}/{ids.Count}：{UnitCatalog.Get(ids[index])?.Name}（{_selected.Count} 个）");
    }

    /// <summary>点头像：切到该英雄所在的子组（同类型单位一起），池子里其它兵不丢。</summary>
    private void ShowHeroSubgroup(Unit hero)
    {
        if (hero?.Type == null) return;
        if (!_subgroupPool.Contains(hero))
        {
            RememberSubgroupPool();
            if (!_subgroupPool.Contains(hero))
            {
                _subgroupPool.Clear();
                if (hero.Alive) _subgroupPool.Add(hero);
            }
        }
        ShowSubgroup(hero.Type.Id);
    }

    private void ToggleAutoCast(Unit owner, Ability ability)
    {
        if (owner == null || ability == null) return;

        bool turningOn = !ability.AutoCastEnabled;
        if (turningOn && owner.Player == ViewPlayer && !owner.AutoCast)
        {
            foreach (Ability other in owner.Abilities)
            {
                if (!ReferenceEquals(other, ability) && other.SupportsAutoCast) other.AutoCastEnabled = false;
            }
        }

        ability.AutoCastEnabled = turningOn;
        if (owner.Player == ViewPlayer) owner.AutoCast = AnyAutoCastEnabled(owner);

        _hud?.SetHint($"{ability.Name} 自动施法：{(ability.AutoCastEnabled ? "开" : "关")}");
        UpdateCommandCard();
    }

    private static bool AnyAutoCastEnabled(Unit owner)
    {
        foreach (Ability ability in owner.Abilities)
        {
            if (ability.SupportsAutoCast && ability.AutoCastEnabled) return true;
        }
        return false;
    }

    private bool TryStandardCommandHotkey(Unit unit, Key keycode)
    {
        char pressed = KeyToLetter(keycode);
        if (pressed == '\0') return false;
        if (unit == null || !unit.Alive || unit.IsBuilding) return false;

        switch (pressed)
        {
            case 'M':
                _panBlockedKey = keycode;
                BeginMoveCommand();
                return true;
            case 'S':
                _panBlockedKey = keycode;
                IssueStopOrders();
                return true;
            case 'H':
                _panBlockedKey = keycode;
                IssueHoldOrders();
                return true;
            case 'A':
                _panBlockedKey = keycode;
                BeginAttackCommand();
                return true;
            case 'P':
                _panBlockedKey = keycode;
                BeginPatrolCommand();
                return true;
            default:
                return false;
        }
    }

    private void ReleaseCommandKey(Key keycode)
    {
        if (_panBlockedKey != Key.None && keycode == _panBlockedKey) _panBlockedKey = Key.None;
    }

    private bool PanKeyAvailable(Key keycode)
    {
        return _panBlockedKey != keycode;
    }
}