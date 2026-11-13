using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed class CommandEntry
{
    public string Id;
    public string Name;
    public string Cost;
    public string Tooltip;
    public Texture2D Icon;
    public bool Enabled = true;
    public bool Active;
    public int SlotIndex = -1;
    public string Hotkey = "";
    public UiCommandKind Kind = UiCommandKind.Ability;
    public string Payload = "";
    public Action Action;
    public Action RightAction;
    public float CooldownRemaining;
    public float CooldownTotal;
    public double CooldownEnd;
    public int Charges;
    public int MaxCharges;
}

public sealed class QueueEntry
{
    public Texture2D Icon;
    public string Name;
    public float Remaining;
    public float Total;
}

public sealed class BuffEntry
{
    public Texture2D Icon;
    public string Name;
    public float Remaining;
    public bool Aura;
}

public sealed class MeleeHudController : IUiCommandListener
{
    readonly GameHudScreen _host;
    readonly List<CommandEntry> _commands = new();
    readonly List<CommandEntry> _inventory = new();
    readonly List<QueueEntry> _queue = new();
    readonly List<BuffEntry> _buffs = new();
    string _hint;
    string _playerInfo;
    string _unitSummary;

    public Func<Unit, string> DetailProvider;
    public Action<Unit> SelectionSlotClicked;

    public MeleeHudController(GameHudScreen host, string mapName)
    {
        _host = host;
        _host.Listener = this;
    }

    public void SetResources(int gold, int lumber, int food, int foodCap)
        => _host.SetResources(gold, lumber, food, foodCap);

    public void SetClock(string text) { }

    public void SetPlayerInfo(string text) => _playerInfo = text;

    public void SetSelection(IReadOnlyList<Unit> selected) { }

    public void SetSelectionBox(Rect2 rect, bool visible) => _host.SetSelectionBox(rect, visible);

    public void SetCommands(IReadOnlyList<CommandEntry> commands)
    {
        _commands.Clear();
        if (commands != null) _commands.AddRange(commands);
        _host.SetCommands(commands);
    }

    public void SetInventory(IReadOnlyList<CommandEntry> items)
    {
        _inventory.Clear();
        for (int i = 0; i < Unit.InventorySize; i++)
            _inventory.Add(items != null && i < items.Count ? items[i] : null);
    }

    public void SetQueue(IReadOnlyList<CommandEntry> entries)
    {
        _queue.Clear();
        if (entries == null) return;
        foreach (CommandEntry entry in entries)
            if (entry != null) _queue.Add(new QueueEntry { Name = entry.Name });
    }

    public void SetQueueSlots(IReadOnlyList<QueueEntry> entries)
    {
        _queue.Clear();
        if (entries != null) _queue.AddRange(entries);
    }

    public void SetQueue(string text) { }

    public void SetBuffs(IReadOnlyList<BuffEntry> buffs)
    {
        _buffs.Clear();
        if (buffs != null) _buffs.AddRange(buffs);
    }

    public void SetUnitSummary(Texture2D portrait, string attack, string armor) =>
        _unitSummary = $"{portrait?.GetSize().ToString() ?? "-"} {attack} {armor}";

    public void SetHint(string text) => _hint = text;

    public bool ActivateItemSlot(int index)
    {
        if (index < 0 || index >= _inventory.Count) return false;
        CommandEntry entry = _inventory[index];
        if (entry is not { Enabled: true, Action: not null }) return false;
        entry.Action();
        return true;
    }

    public void ShowItemDetailForTest(int index) { }

    public static string ItemDetailText(ItemType item)
    {
        var lines = new List<string> { item.Name };
        string bonus = War3Text.Describe(item.BonusText);
        if (!string.IsNullOrWhiteSpace(bonus) && bonus != "-") lines.Add(bonus);
        var meta = new List<string> { $"等级 {item.Level}" };
        if (item.GoldCost > 0) meta.Add($"{item.GoldCost} 金");
        if (item.Uses > 0) meta.Add($"{item.Uses} 次");
        if (item.PowerUp) meta.Add("一次性");
        lines.Add(string.Join("    ", meta));
        string description = War3Text.Describe(item.Ubertip);
        if (string.IsNullOrWhiteSpace(description)) description = War3Text.Describe(item.Tip);
        if (!string.IsNullOrWhiteSpace(description)) lines.Add(description);
        return string.Join("\n", lines);
    }

    public void RefreshCooldownNow() { }

    public string DescribeExtras() =>
        $"commands={_commands.Count} inventory={_inventory.Count} queue={_queue.Count} summary={_unitSummary}";

    public string DescribeVisibleCommands()
    {
        var names = new List<string>();
        foreach (CommandEntry entry in _commands)
            if (entry != null) names.Add(entry.Name);
        return string.Join(" | ", names);
    }

    public void OnUiCommand(UiCommand command)
    {
        if (command.Context is not CommandEntry entry) return;
        if (command.NativeEventKind >= 2)
        {
            entry.RightAction?.Invoke();
            return;
        }
        entry.Action?.Invoke();
    }
}