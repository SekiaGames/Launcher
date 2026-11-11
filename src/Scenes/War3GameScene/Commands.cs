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
        PlayVoice(movers[0], "Yes");
        _hud?.SetHint(queue
            ? $"追加巡逻 {movers.Count} 个单位"
            : $"巡逻：{movers.Count} 个单位往返 ({ground.X:0}, {ground.Z:0})");
    }

    public void IssueAttackOrders(Unit target, bool queue)
    {
        if (_world == null || target == null || !target.Alive) return;

        int attackers = 0;
        foreach (Unit unit in _selected)
        {
            if (!unit.Alive || unit.IsBuilding || !unit.Type.CanAttack) continue;
            _world.IssueOrder(unit, UnitOrder.Attack(target), queue);
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

    private void AddStandardCommands(List<CommandEntry> commands, bool canMove, bool compact = false)
    {
        commands.Add(new CommandEntry
        {
            Id = "cmd-move",
            Name = "移动",
            SlotIndex = compact ? 8 : 0,
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
            SlotIndex = compact ? 9 : 1,
            Hotkey = "S",
            Tooltip = "停下手上的指令并清空队列",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNStop.blp"),
            Enabled = true,
            Action = IssueStopOrders,
        });

        if (compact) return;

        commands.Add(new CommandEntry
        {
            Id = "cmd-hold",
            Name = "保持位置",
            SlotIndex = 4,
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
            SlotIndex = 5,
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
            SlotIndex = 8,
            Hotkey = "P",
            Tooltip = "在当前位置与落点之间往返，遇敌自动开火",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNPatrol.blp"),
            Enabled = canMove,
            Action = BeginPatrolCommand,
        });
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