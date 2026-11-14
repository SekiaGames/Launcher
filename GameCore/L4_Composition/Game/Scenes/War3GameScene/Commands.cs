using Godot;
using System;
using System.Collections.Generic;

using OpenWC3.Game.UI;
using OpenWC3.UI;
namespace OpenWC3.Game;

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
        _hud?.SetPendingCommand(kind switch
        {
            PendingCommandKind.Move => UiCommandKind.Move,
            PendingCommandKind.Attack => UiCommandKind.AttackMove,
            PendingCommandKind.Patrol => UiCommandKind.Patrol,
            _ => null,
        });
        _hud?.SetHint(hint);
    }

    private void ClearPendingCommand()
    {
        _pendingCommand = PendingCommandKind.None;
        _hud?.SetPendingCommand(null);
    }

    public bool TryCommandAt(Vector2 screenPosition)
    {
        if (_pendingCommand == PendingCommandKind.None) return false;

        PendingCommandKind kind = _pendingCommand;
        _pendingCommand = PendingCommandKind.None;
        _hud?.SetPendingCommand(null);

        if (kind == PendingCommandKind.Attack)
        {
            Unit target = PickUnit(screenPosition, ViewPlayer, anyPlayer: true);
            if (target != null)
            {
                IssueAttackOrders(target, false);
                return true;
            }
            // 客户端 A 键点地面就是攻击移动（COrderTarget(Point)），不是"没点到目标"。
            if (!TryGroundPoint(screenPosition, out Vector3 attackGround))
            {
                _hud?.SetHint("没点到攻击目标");
                return true;
            }
            IssueAttackMoveOrders(attackGround, false);
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

    public void IssueMoveOrders(Vector3 ground, bool queue)
    {
        if (_commandProcessor == null) return;
        _commandProcessor.Move(new Vec2(ground.X, ground.Z), queue);
        _hud?.SetHint(queue
            ? $"追加移动 {_selected.Count} 个单位"
            : $"移动到 ({ground.X:0}, {ground.Z:0})，{_selected.Count} 个单位按阵型展开");
    }

    public void IssuePatrolOrders(Vector3 ground, bool queue)
    {
        if (_commandProcessor == null) return;
        _commandProcessor.Patrol(new Vec2(ground.X, ground.Z), queue);
        _hud?.SetHint(queue
            ? $"追加巡逻 {_selected.Count} 个单位"
            : $"巡逻：{_selected.Count} 个单位往返 ({ground.X:0}, {ground.Z:0})");
    }

    public void IssueAttackMoveOrders(Vector3 ground, bool queue)
    {
        if (_commandProcessor == null) return;
        _commandProcessor.AttackMove(new Vec2(ground.X, ground.Z), queue);
        _hud?.SetHint(queue
            ? $"追加攻击移动 {_selected.Count} 个单位"
            : $"攻击移动：{_selected.Count} 个单位前往 ({ground.X:0}, {ground.Z:0})");
    }

    public void IssueAttackOrders(Unit target, bool queue)
    {
        if (_commandProcessor == null || target == null || !target.Alive) return;
        int attackers = _commandProcessor.Attack(target, queue, allowFriendly: true);
        _hud?.SetHint(attackers == 0 ? "选中的单位不能攻击"
            : queue ? $"追加攻击 {target.Type.Name}"
            : $"攻击 {target.Type.Name}（{attackers} 个单位）");
    }

    public void IssueStopOrders()
    {
        if (_commandProcessor == null) return;
        int count = _selected.Count(unit => unit != null && unit.Alive && !unit.IsBuilding);
        _commandProcessor.Stop();
        _hud?.SetHint(count > 0 ? $"停止 {count} 个单位（队列已清空）" : "没有可停止的单位");
        RefreshHud();
        UpdateCommandCard();
    }

    public void IssueHoldOrders()
    {
        if (_commandProcessor == null) return;
        int count = _selected.Count(unit => unit != null && unit.Alive && !unit.IsBuilding);
        _commandProcessor.HoldPosition();
        _hud?.SetHint(count > 0 ? $"原地据守 {count} 个单位（只打射程内）" : "没有可据守的单位");
        RefreshHud();
        UpdateCommandCard();
    }

    /// <summary>
    /// 基础命令（客户端 AddBaseCommands）：移动/巡逻/保持位置只在能移动时才有格子，
    /// 攻击按单位有没有武器给格 —— 混选时「全员都有」才留得下，所以这里不能拿禁用代替缺失。
    /// </summary>
    private void AddStandardCommands(List<CommandEntry> commands, Unit unit)
    {
        if (unit == null) return;

        if (!unit.IsBuilding)
        {
            commands.Add(new CommandEntry
            {
                Id = "cmd-move",
                Name = "移动",
                Kind = UiCommandKind.Move,
                SlotIndex = 0,
                Hotkey = "M",
                Tooltip = "点地面移动。Shift 追加到队列末尾",
                Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNMove.blp"),
                Enabled = true,
                Action = BeginMoveCommand,
            });

            commands.Add(new CommandEntry
            {
                Id = "cmd-patrol",
                Name = "巡逻",
                Kind = UiCommandKind.Patrol,
                SlotIndex = 4,
                Hotkey = "P",
                Tooltip = "在当前位置与落点之间往返，遇敌自动开火",
                Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNPatrol.blp"),
                Enabled = true,
                Action = BeginPatrolCommand,
            });

            commands.Add(new CommandEntry
            {
                Id = "cmd-hold",
                Name = "保持位置",
                Kind = UiCommandKind.Hold,
                SlotIndex = 2,
                Hotkey = "H",
                Tooltip = "原地不动，只攻击进入射程的敌人",
                Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNHoldPosition.blp"),
                Enabled = true,
                Action = IssueHoldOrders,
            });
        }

        if (unit.Type is { CanAttack: true })
        {
            commands.Add(new CommandEntry
            {
                Id = "cmd-attack",
                Name = "攻击",
                Kind = UiCommandKind.AttackMove,
                SlotIndex = 3,
                Hotkey = "A",
                Tooltip = "点目标攻击。Shift 追加到队列末尾",
                Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNAttack.blp"),
                Enabled = true,
                Action = BeginAttackCommand,
            });
        }

        commands.Add(new CommandEntry
        {
            Id = "cmd-stop",
            Name = "停止",
            Kind = UiCommandKind.Stop,
            SlotIndex = 1,
            Hotkey = "S",
            Tooltip = "停下手上的指令并清空队列",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNStop.blp"),
            Enabled = true,
            Action = IssueStopOrders,
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

    private void ReleaseCommandKey(Key keycode)
    {
        if (_panBlockedKey != Key.None && keycode == _panBlockedKey) _panBlockedKey = Key.None;
    }

    private bool PanKeyAvailable(Key keycode)
    {
        return _panBlockedKey != keycode;
    }
}