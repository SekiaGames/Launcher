using System;
using System.Collections.Generic;
using OpenWC3.Game;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

/// <summary>命令卡刷新模式（客户端 CommandCardMode.FromPending）：普通 / 选目标 / 放置。</summary>
public enum CommandCardRefreshMode { Normal, Target, Placement }

public sealed partial class CommandCardBuilder
{
    /// <summary>
    /// 混选聚合（客户端 CollectCommands + CommandCandidates）：
    /// 每个成员各收一遍命令；带物编身份的命令（移动/停止/保持/攻击/巡逻/集结点/技能/取消）
    /// 要全员都有才留一格，合并后只要有一个人不可用就是不可用；
    /// 没有身份的命令（训练/研究/升级/建造/商店/学习）只认主位，不会因为混选消失。
    /// </summary>
    internal static List<CommandEntry> CollectCommands(IReadOnlyList<Unit> members, Unit primary,
        CommandCardRefreshMode mode, Func<Unit, List<CommandEntry>> collect, Action cancel = null)
    {
        var commands = new List<CommandEntry>();
        if (primary == null || collect == null) return commands;

        List<Unit> ordered = OrderMembers(members, primary);
        List<CommandEntry> primaryCommands = collect(primary) ?? new List<CommandEntry>();
        commands.AddRange(primaryCommands);

        if (ordered.Count > 1)
        {
            var others = new List<List<CommandEntry>>(ordered.Count - 1);
            foreach (Unit member in ordered)
            {
                if (ReferenceEquals(member, primary)) continue;
                others.Add(collect(member) ?? new List<CommandEntry>());
            }
            commands = Aggregate(primaryCommands, others);
        }

        if (mode != CommandCardRefreshMode.Normal) commands.Add(CancelEntry(mode, cancel));
        return commands;
    }

    private static List<Unit> OrderMembers(IReadOnlyList<Unit> members, Unit primary)
    {
        var ordered = new List<Unit> { primary };
        if (members == null) return ordered;
        foreach (Unit member in members)
        {
            if (member == null || member.Removed) continue;
            if (ReferenceEquals(member, primary) || ordered.Contains(member)) continue;
            ordered.Add(member);
        }
        return ordered;
    }

    private static List<CommandEntry> Aggregate(List<CommandEntry> primaryCommands,
        List<List<CommandEntry>> others)
    {
        var result = new List<CommandEntry>(primaryCommands.Count);
        foreach (CommandEntry entry in primaryCommands)
        {
            if (entry == null) continue;
            if (!Aggregates(entry))
            {
                result.Add(entry);
                continue;
            }
            var matches = new List<CommandEntry>(others.Count);
            bool everywhere = true;
            foreach (List<CommandEntry> other in others)
            {
                CommandEntry match = FindMatch(other, entry);
                if (match == null)
                {
                    everywhere = false;
                    break;
                }
                matches.Add(match);
            }
            if (everywhere) result.Add(Merge(entry, matches));
        }
        return result;
    }

    private static CommandEntry FindMatch(List<CommandEntry> commands, CommandEntry entry)
    {
        string key = CommandKey(entry);
        foreach (CommandEntry candidate in commands)
            if (candidate != null && string.Equals(CommandKey(candidate), key, StringComparison.Ordinal))
                return candidate;
        return null;
    }

    private static string CommandKey(CommandEntry entry) => $"{(int)entry.Kind}|{entry.Id}";

    /// <summary>带物编身份的命令：混选时要全员都有（客户端 NativeCommandCard.Survives）。</summary>
    private static bool Aggregates(CommandEntry entry) => entry.Kind
        is UiCommandKind.Move or UiCommandKind.Stop or UiCommandKind.Hold or UiCommandKind.AttackMove
        or UiCommandKind.Patrol or UiCommandKind.Rally or UiCommandKind.Ability
        or UiCommandKind.PassiveAbility or UiCommandKind.ReturnResources
        or UiCommandKind.EntangleGoldMine or UiCommandKind.CancelTargeting
        or UiCommandKind.CancelBuildPlacement;

    /// <summary>聚合后的呈现：图标/文案取主位，可用性取全员“与”，冷却取最长的那一份。</summary>
    private static CommandEntry Merge(CommandEntry primary, List<CommandEntry> matches)
    {
        bool enabled = primary.Enabled;
        float cooldownRemaining = primary.CooldownRemaining;
        float cooldownTotal = primary.CooldownTotal;
        foreach (CommandEntry other in matches)
        {
            enabled &= other.Enabled;
            if (other.CooldownRemaining <= cooldownRemaining) continue;
            cooldownRemaining = other.CooldownRemaining;
            cooldownTotal = other.CooldownTotal;
        }
        return new CommandEntry
        {
            Id = primary.Id,
            Name = primary.Name,
            Cost = primary.Cost,
            TooltipCosts = primary.TooltipCosts,
            StructuredCostOnly = primary.StructuredCostOnly,
            Tooltip = primary.Tooltip,
            Icon = primary.Icon,
            Enabled = enabled,
            Active = primary.Active,
            SlotIndex = primary.SlotIndex,
            Hotkey = primary.Hotkey,
            Kind = primary.Kind,
            Payload = primary.Payload,
            Action = primary.Action,
            RightAction = primary.RightAction,
            CooldownRemaining = cooldownRemaining,
            CooldownTotal = cooldownTotal,
            CooldownEnd = primary.CooldownEnd,
            Charges = primary.Charges,
            MaxCharges = primary.MaxCharges,
            Overlay = primary.Overlay,
        };
    }

    /// <summary>
    /// 选目标 / 放置模式的第 12 格（客户端 BuildSelectedCommandList 的 cancel 分支）：
    /// BTNCancel + Esc，点了就退出当前模式。
    /// </summary>
    internal static CommandEntry CancelEntry(CommandCardRefreshMode mode, Action cancel)
    {
        bool placement = mode == CommandCardRefreshMode.Placement;
        return new CommandEntry
        {
            Id = "cmd-cancel",
            Name = placement ? "取消建造" : "取消目标选择",
            Kind = placement ? UiCommandKind.CancelBuildPlacement : UiCommandKind.CancelTargeting,
            SlotIndex = Slots - 1,
            Hotkey = "Esc",
            Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNCancel.blp"),
            Action = cancel,
        };
    }
}
