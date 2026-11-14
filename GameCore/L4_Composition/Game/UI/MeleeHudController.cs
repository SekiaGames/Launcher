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
    public int[] TooltipCosts;
    public bool StructuredCostOnly;
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
    /// <summary>图标右下角的角标（客户端 CommandSlot.NumberText）：学习页显示剩余技能点。</summary>
    public string Overlay;

    public static int[] Costs(int mana, int gold, int lumber, int supply)
    {
        if (mana <= 0 && gold <= 0 && lumber <= 0 && supply <= 0) return null;
        return new[]
        {
            Mathf.Max(0, mana), Mathf.Max(0, gold), Mathf.Max(0, lumber), Mathf.Max(0, supply),
        };
    }
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
    public string Identity;
    public string IconArt;
    public string Name;
    public string Title;
    public string Description;
    public float Remaining;
    public bool Aura;

    public static List<BuffEntry> Collect(Unit unit)
    {
        var buffs = new List<BuffEntry>();
        foreach (Ability ability in unit.Abilities)
        {
            if (ability is not Buff buff) continue;

            string identity = buff.Rawcode;
            IRuntimeObjectData data = War3ObjectData.GetBuffType(identity) ?? ((Ability)buff).ObjectData;
            if (string.IsNullOrEmpty(identity))
            {
                Ability source = buff.SourceAbility;
                if (source != null && source.BuffRawcodes.Count > 0)
                {
                    identity = source.BuffRawcodes[0];
                    data = War3ObjectData.GetBuffType(identity) ?? data;
                }
            }

            int level = buff is AuraBuff aura && aura.AuraLevel > 0 ? aura.AuraLevel : Math.Max(1, buff.Level);
            string title = War3Text.Describe(data?.GetString("Bufftip", level, 0, "") ?? "");
            string body = War3Text.Describe(data?.GetString("BuffUbertip", level, 0, "") ?? "");
            if (string.IsNullOrWhiteSpace(title)) title = buff.Name;
            if (string.IsNullOrWhiteSpace(body)) body = title;

            buffs.Add(new BuffEntry
            {
                Identity = identity,
                IconArt = data?.GetString("Buffart", level),
                Name = buff.Name,
                Title = title,
                Description = body,
                Remaining = buff.Remaining,
                Aura = buff.Aura,
            });
        }
        return buffs;
    }
}

public sealed class MeleeHudController : IUiCommandListener
{
    readonly GameHudScreen _host;
    readonly List<CommandEntry> _commands = new();
    readonly List<CommandEntry> _inventory = new();
    readonly List<QueueEntry> _queue = new();
    string _hint;
    string _playerInfo;
    string _unitSummary;

    public Func<Unit, string> DetailProvider;
    public Action<Unit> SelectionSlotClicked;
    public Func<IReadOnlyList<CommandEntry>> CommandSource;

    public MeleeHudController(GameHudScreen host, string mapName)
    {
        _host = host;
        _host.Listener = this;
    }

    public void SetResources(int gold, int lumber, int food, int foodCap)
        => _host.SetResources(gold, lumber, food, foodCap);

    public void SetClock(string text) { }

    public void SetPlayerInfo(string text) => _playerInfo = text;

    public void SetSelection(IReadOnlyList<Unit> selected, Unit primary,
        IReadOnlyList<Unit> cargo = null)
    {
        _host.SetSelection(primary, selected ?? Array.Empty<Unit>(), cargo);
    }

    public bool LearnMenuOpen => _host.LearnMenuOpen;

    public bool BuildMenuOpen => _host.BuildMenuOpen;

    public bool SpellBookOpen => _host.SpellBookOpen;

    public RuntimeSpellBookAbility CurrentSpellBook => _host.CurrentSpellBook;

    public void OpenLearnMenu(Unit selected) => _host.OpenLearnMenu(selected);

    public void CloseLearnMenu() => _host.CloseLearnMenu();

    public void OpenBuildMenu(Unit selected) => _host.OpenBuildMenu(selected);

    public void CloseBuildMenu() => _host.CloseBuildMenu();

    public void OpenSpellBook(Unit selected, RuntimeSpellBookAbility book) => _host.OpenSpellBook(selected, book);

    public void BackFromSpellBook() => _host.BackFromSpellBook();

    public void BackMenu() => _host.BackMenu();

    public void ClickCommandSlot(int slot) => _host.ClickCommandSlot(slot);

    public CommandEntry CommandAtHotkey(char hotkey) => _host.CommandAtHotkey(hotkey);

    public string DescribeCommandSlots() => _host.DescribeCommandSlots();

    public void SetSelectionBox(Rect2 rect, bool visible) => _host.SetSelectionBox(rect, visible);

    public void SetPendingCommand(UiCommandKind? kind) => _host.SetPendingCommand(kind);

    public void SetCommands(IReadOnlyList<CommandEntry> commands)
    {
        _commands.Clear();
        if (commands != null) _commands.AddRange(commands);
        _host.SetCommands(commands);
    }

    public void RefreshCommands()
    {
        SetCommands(CommandSource?.Invoke() ?? Array.Empty<CommandEntry>());
    }

    public void SetInventory(IReadOnlyList<CommandEntry> items)
    {
        _inventory.Clear();
        for (int i = 0; i < Unit.InventorySize; i++)
            _inventory.Add(items != null && i < items.Count ? items[i] : null);
        _host.SetInventory(items);
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
        _host.SetQueueSlots(entries);
    }

    public void SetQueue(string text) { }

    public void SetBuffs(IReadOnlyList<BuffEntry> buffs) => _host.SetBuffs(buffs);

    public void SetUnitSummary(Texture2D portrait, string attack, string armor) =>
        _unitSummary = $"{portrait?.GetSize().ToString() ?? "-"} {attack} {armor}";

    public void SetHint(string text)
    {
        _hint = text;
        _host.ShowCommandError(text);
    }

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

    public War3UiSoundService UiSounds { get; set; }

    public void OnUiCommand(UiCommand command)
    {
        UiSounds?.Play(command.Kind == UiCommandKind.SelectUnit
            ? "SubGroupSelectionChange" : "InterfaceClick");
        if (command.Context is not CommandEntry entry) return;
        if (command.NativeEventKind >= 2)
        {
            entry.RightAction?.Invoke();
            return;
        }
        entry.Action?.Invoke();
    }
}