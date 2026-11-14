using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game.UI.Views;
using OpenWC3.RHI;
using OpenWC3.UI;
using NVector2 = System.Numerics.Vector2;

namespace OpenWC3.Game.UI;

public sealed partial class GameHudScreen : Control
{
    public const float DesignWidth = 1024f;
    public const float DesignHeight = 768f;
    private const float TopChromeHeight = 40.96f;
    private const float BottomChromeHeight = 225.28f;

    private UiRoot _root;
    private UiEventRouter _router;
    private GodotUiRenderDevice _device;
    private GodotWar3FontFace _font;
    private ConsoleSkinFrame _console;
    private TopBarFrame _topBar;
    private CommandCardBuilder _card;
    private UnitInfoPanelFrame _infoPanel;
    private ItemDetailFrame _itemDetail;
    private MinimapFrame _minimap;
    private HeroBarFrame _heroBar;
    private PortraitFrame _portrait;
    private SelectionGroupFrame _selectionGroup;
    private IdlePeonFrame _idlePeon;
    private PortraitViewport _portraitViewport;
    private TimeOfDayViewport _timeOfDayViewport;
    private InventoryFrame _inventory;
    private QueueIconRowFrame _queue;
    private TooltipFrame _tooltip;
    private CommandErrorFrame _commandError;
    private FdfUiFactory _strings;
    private TextTagFrame _textTag;
private AboutDialogFrame _aboutDialog;
    private ChatFrame _chat;
    private ControlGroupFrame _controlGroups;
    private IReadOnlyList<TextTagFrame.TagView> _worldText;
    private readonly CommandEntry[] _inventoryEntries = new CommandEntry[6];
    private GodotUiTexture _portraitTexture;
    private GodotUiTexture _timeOfDayTexture;
    private Unit _selectedUnit;
    public Action<Unit> SelectionClicked;
    public Action<int> ItemDragStarted;
    public Action<int, int, NVector2> ItemDragReleased;
    public Action<Unit> IdlePeonClicked;
    public Action<Unit> PortraitFollowStarted;
    public Action PortraitFollowStopped;
public Action MenuClicked;
    public Action<float> CameraDistanceChanged;
    public Action<float> PanSensitivityChanged;
    public Action<float> MusicVolumeChanged;
    public Action<float> SoundVolumeChanged;
    public Action<int> FrameRateLimitChanged;
    public Action<float> RenderScaleChanged;
    public Action AboutDialogClosed;
    public Action AboutDialogExitRequested;
    public Action<int, bool> ControlGroupClicked;
    private int _gold, _lumber, _food, _foodCap;
    private double _timeOfDay;
    private CommandCardView _commands = CommandCardView.Empty;
    private readonly CommandEntry[] _slotEntries = new CommandEntry[CommandCardBuilder.SlotCount];
    private bool _uiPointerDown;
    private UiRect? _selectionBox;
    private UiCommandKind? _pendingKind;

    public IUiCommandListener Listener { get; set; }

    public int FrameCount => _root?.Children.Count ?? 0;
    internal MinimapFrame Minimap => _minimap;
    internal HeroBarFrame HeroBar => _heroBar;
    internal ChatFrame Chat => _chat;

    public void Load(string race = "Human", bool sample = true, GameUserSettings settings = null)
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Pass;

        War3UiSkin skin = War3UiSkin.Load(race);
        _font = GodotWar3FontFace.Load(skin);
        var resolver = new GodotUiTextureResolver { Skin = skin };
        _device = new GodotUiRenderDevice(this, _font.Font)
        {
            LineSizeResolver = _font.PixelSizeForLine,
        };

        _root = new UiRoot { MouseTransparent = true };
        _router = new UiEventRouter(_root);
        _router.CommandClicked += OnCommandClicked;
        _router.CommandRightClicked += OnCommandRightClicked;
        _router.CommandLongPressed += (frame, command, button) =>
        {
            if (command.Kind == UiCommandKind.UseItem)
            {
                if (_inventory.BeginDrag(command.Slot, _inventory.SlotCenter(command.Slot)))
                    ItemDragStarted?.Invoke(command.Slot);
                return;
            }
            DispatchCommand(command, 3);
        };
        _router.CommandDragMoved += (frame, command, position) =>
        {
            if (command.Kind == UiCommandKind.UseItem) _inventory.MoveDrag(position);
        };
        _router.CommandDragReleased += (frame, command, position) =>
        {
            if (command.Kind != UiCommandKind.UseItem) return;
            _inventory.EndDrag();
            ItemDragReleased?.Invoke(command.Slot, _inventory.HitSlot(position), position);
        };
        _console = new ConsoleSkinFrame(resolver, skin);
        _topBar = new TopBarFrame(resolver, skin);
        _topBar.SetChromeVisible(true);
        _card = new CommandCardBuilder(resolver);
        _infoPanel = new UnitInfoPanelFrame(resolver, skin);
        _itemDetail = new ItemDetailFrame(resolver.Resolve);
        _minimap = new MinimapFrame(resolver, skin, new War3MiscData());
        _heroBar = new HeroBarFrame(resolver);
        _portrait = new PortraitFrame();
        _selectionGroup = new SelectionGroupFrame(resolver);
        _idlePeon = new IdlePeonFrame(resolver, skin);
        _inventory = new InventoryFrame(resolver);
        _queue = new QueueIconRowFrame(resolver, skin);
        _tooltip = new TooltipFrame(resolver, skin)
        {
            TextMeasure = (text, size) => _device.MeasureTextLineWidth(text, size),
            TextHeightMeasure = (text, size) => _device.MeasureTextHeight(text, size),
        };
        _commandError = new CommandErrorFrame();
        _strings = new FdfUiFactory(resolver, FdfSource.Read);
        _strings.LoadStrings(@"UI\FrameDef\GlobalStrings.fdf");
        _strings.LoadStrings(@"UI\FrameDef\InfoPanelStrings.fdf");
        _textTag = new TextTagFrame();
        _portraitViewport = new PortraitViewport(this);
        _portraitTexture = new GodotUiTexture(_portraitViewport.Texture);
        _timeOfDayViewport = new TimeOfDayViewport(this, skin.Get("TimeOfDayIndicator",
            @"UI\Console\Human\HumanUI-TimeIndicator.mdl"));
        _timeOfDayTexture = new GodotUiTexture(_timeOfDayViewport.Texture);
        _portrait.OnStartFollow = () => PortraitFollowStarted?.Invoke(_selectedUnit);
        _portrait.OnStopFollow = () => PortraitFollowStopped?.Invoke();
        _selectionGroup.OnUnitClick = unit => SelectionClicked?.Invoke(unit);
        _idlePeon.OnIdlePeonClick = unit => IdlePeonClicked?.Invoke(unit);
        settings ??= GameUserSettings.Load();
        _aboutDialog = new AboutDialogFrame(resolver, skin,
            settings.CameraDistanceScale, settings.PanSensitivity,
            settings.MusicVolume, settings.SoundVolume,
            settings.FrameRateLimit, settings.RenderScale,
            value => CameraDistanceChanged?.Invoke(value),
            value => PanSensitivityChanged?.Invoke(value),
            value => MusicVolumeChanged?.Invoke(value),
            value => SoundVolumeChanged?.Invoke(value),
            value => FrameRateLimitChanged?.Invoke(value),
            value => RenderScaleChanged?.Invoke(value));
        _aboutDialog.Closed += () => AboutDialogClosed?.Invoke();
        _aboutDialog.ExitToMenuRequested += () => AboutDialogExitRequested?.Invoke();
        _topBar.MenuButton.Clicked += button =>
        {
            if (button == 0) MenuClicked?.Invoke();
        };
        _root.AddChild(_console);
        _root.AddChild(_infoPanel);
        _root.AddChild(_itemDetail);
        _root.AddChild(_card.Root);
        _root.AddChild(_topBar);
        _root.AddChild(_minimap);
        _root.AddChild(_heroBar);
        _root.AddChild(_portrait);
        _root.AddChild(_selectionGroup);
        _root.AddChild(_idlePeon);
        _root.AddChild(_inventory);
        _root.AddChild(_queue);
        _root.AddChild(_tooltip);
_chat = new ChatFrame(this);
        _controlGroups = new ControlGroupFrame(resolver);
        _controlGroups.GroupClicked = (group, center) => ControlGroupClicked?.Invoke(group, center);
        _root.AddChild(_controlGroups);
        _root.AddChild(_chat);
        _root.AddChild(_aboutDialog);
        _root.AddChild(_commandError);
        _router.HoverChanged += OnHoverChanged;

        SetResources(500, 220, 12, 24);
        SetClock(6.0);
        if (sample)
        {
            _card.Build(SampleCommands());
            SetUnitInfo(new UnitInfoView("步兵", false, TypeLine: "人族",
                AttackText: "12-14", ArmorText: "2.5", Strength: 0));
            SetUnitInfo(new UnitInfoView("山丘之王", true, ClassLine: "等级 3 山丘之王",
                XpRatio: 0.45f, AttackText: "22-28", AttackLevel: "3",
                ArmorText: "4.2", ArmorLevel: "3", Strength: 24, Agility: 11, Intellect: 14));
        }
        else SetCommands(null);
        QueueRedraw();
    }

    public void SetResources(int gold, int lumber, int food, int foodCap)
    {
        _gold = gold;
        _lumber = lumber;
        _food = food;
        _foodCap = foodCap;
        _topBar.UpdateResources(gold, lumber, food, foodCap);
        QueueRedraw();
    }

    public void SetClock(double hours)
    {
        _timeOfDay = hours;
        _topBar.UpdateTime(hours);
        _timeOfDayViewport?.SetClock(hours);
        QueueRedraw();
    }

    public void SetUnitInfo(UnitInfoView? view)
    {
        _infoPanel.Update(view);
        QueueRedraw();
    }

    public void SetBuffs(IReadOnlyList<BuffEntry> buffs)
    {
        _infoPanel.SetBuffs(buffs);
        if (_router?.Hovered?.Name?.StartsWith("BuffIcon", StringComparison.Ordinal) == true)
            OnHoverChanged(_router.Hovered);
    }

    /// <summary>命令/建造提示（客户端 CommandErrorFrame）：黄字、console 上方、3s+每字符 0.1s 后淡出。</summary>
    public void ShowCommandError(string text) => _commandError?.Show(text);

    private string UiString(string key, string fallback = "")
        => _strings?.Localized(key, fallback) ?? fallback;

    private void OnHoverChanged(UiFrame frame)
    {
        if (frame == null)
        {
            _tooltip.Hide();
            return;
        }

        switch (frame.Name)
        {
            case "ResIcon0":
            case "ResVal0":
                ShowTip(frame, "金币", UiString("RESOURCE_UBERTIP_GOLD", "黄金是从金矿中采集到的。"));
                return;
            case "ResIcon1":
            case "ResVal1":
                ShowTip(frame, "木材", UiString("RESOURCE_UBERTIP_LUMBER", "木材是砍伐树木得到的。"));
                return;
            case "ResIcon2":
            case "ResVal2":
                ShowTip(frame, "人口", UiString("RESOURCE_UBERTIP_SUPPLY", "你目前使用的食物数量超过你所能携带的数值。"));
                return;
            case "ResVal3":
                ShowTip(frame, "维修费用", UpkeepTip());
                return;
            case "UnitAttackIcon":
            case "UnitAttackLabel":
                ShowTip(frame, "攻击力", DamageTip());
                return;
            case "UnitArmorIcon":
            case "UnitArmorLabel":
                ShowTip(frame, "护甲", ArmorTip());
                return;
            case "UnitStrLabel":
                ShowTip(frame, "力量", AttributeTip(StrengthAttributeTip(), "STR"));
                return;
            case "UnitAgiLabel":
                ShowTip(frame, "敏捷", AttributeTip(AgilityAttributeTip(), "AGI"));
                return;
            case "UnitIntLabel":
                ShowTip(frame, "智力", AttributeTip(IntellectAttributeTip(), "INT"));
                return;
            case "UnitStatusLabel":
                ShowTip(frame, "状态", "当前单位身上的魔法效果状态。");
                return;
            case "UnitXpBar":
                if (_selectedUnit is { IsHero: true } hero)
                {
                    int need = HeroRules.XpToNext(hero.Level);
                    string body = hero.Level >= HeroRules.MaxLevel
                        ? $"已满级  经验: {hero.Xp:0}"
                        : $"经验: {hero.Xp:0} / {need:0}";
                    ShowTip(frame, "经验", body);
                }
                else _tooltip.Hide();
                return;
        }

        if (frame.Name?.StartsWith("BuffIcon", StringComparison.Ordinal) == true && frame.Visible)
        {
            if (int.TryParse(frame.Name["BuffIcon".Length..], out int buffSlot)
                && _infoPanel.TryGetBuffTooltip(buffSlot, out string buffTitle, out string buffBody))
            {
                ShowTip(frame, string.IsNullOrWhiteSpace(buffTitle) ? "状态效果" : buffTitle,
                    string.IsNullOrWhiteSpace(buffBody) ? "该单位身上的魔法效果。" : buffBody);
                return;
            }
            _tooltip.Hide();
            return;
        }

        if (frame is ButtonFrame { Visible: true } button
            && (!string.IsNullOrWhiteSpace(button.TooltipTitle)
                || !string.IsNullOrWhiteSpace(button.TooltipBody)))
        {
            _tooltip.ShowEx(frame, button.TooltipTitle, button.TooltipBody, button.TooltipExt,
                button.TooltipCosts, DesignWidth, DesignHeight);
            return;
        }
        _tooltip.Hide();
    }

    private void ShowTip(UiFrame anchor, string title, string body)
        => _tooltip.Show(anchor, title, body, DesignWidth, DesignHeight);

    private string UpkeepTip()
    {
        string text = UiString("RESOURCE_UBERTIP_UPKEEP", "");
        if (!string.IsNullOrWhiteSpace(text)) return text;
        int tier = War3GameplayConfig.Default.UpkeepTier(_food);
        float tax = tier switch { 0 => 0f, 1 => 0.3f, _ => 0.6f };
        string detail = tier switch
        {
            0 => "无维修费用: 金币 100% 入账",
            1 => $"低维修费用: 金币 {MathF.Round((1 - tax) * 100)}% 入账 (税 {MathF.Round(tax * 100)}%)",
            _ => $"高维修费用: 金币 {MathF.Round((1 - tax) * 100)}% 入账 (税 {MathF.Round(tax * 100)}%)",
        };
        return $"只影响金币获取, 不影响木材。{detail}";
    }

    private string DamageTip()
    {
        string kind = _selectedUnit?.Type?.AttackType switch
        {
            AttackType.Pierce => "PIERCE",
            AttackType.Siege => "SIEGE",
            AttackType.Magic or AttackType.Spells => "MAGIC",
            AttackType.Chaos => "CHAOS",
            AttackType.Hero => "HERO",
            _ => "NORMAL",
        };
        string line = UiString($"DAMAGE_{kind}", "");
        string tip = UiString($"DAMAGETIP_{kind}", "");
        if (string.IsNullOrWhiteSpace(line) && string.IsNullOrWhiteSpace(tip))
            return "普通攻击对中型护甲造成额外伤害，对加强型护甲造成较少伤害。";
        return string.IsNullOrWhiteSpace(line) ? tip : $"{line}|n{tip}";
    }

    private string ArmorTip()
    {
        string kind = _selectedUnit?.Type?.DefenseType switch
        {
            DefenseType.Small => "SMALL",
            DefenseType.Medium => "MEDIUM",
            DefenseType.Large => "LARGE",
            DefenseType.Fort => "FORT",
            DefenseType.Hero => "HERO",
            DefenseType.Divine => "DIVINE",
            DefenseType.None => "NONE",
            _ => "NORMAL",
        };
        string line = UiString($"ARMOR_{kind}", "");
        string tip = UiString($"ARMORTIP_{kind}", "");
        if (string.IsNullOrWhiteSpace(line) && string.IsNullOrWhiteSpace(tip))
            return "中型护甲会受到普通攻击的额外伤害，而受穿刺，魔法和攻城攻击的伤害较少。";
        return string.IsNullOrWhiteSpace(line) ? tip : $"{line}|n{tip}";
    }

    private string AttributeTip(string text, string primary)
        => _selectedUnit?.Type?.Primary == primary && !string.IsNullOrWhiteSpace(text)
            ? text + "|n作为主属性，每点增加1点攻击力。"
            : text;

    private string StrengthAttributeTip()
    {
        string highlight = UiString("STRENGTH_HILIGHT", "");
        string hp = FillPercent(UiString("BONUS_HITPOINTS", ""), HeroRules.StrHpBonus);
        string regen = UiString("BONUS_HPREGEN", "");
        return JoinTip(highlight, JoinTip(hp, regen),
            $"每点力量增加英雄{HeroRules.StrHpBonus:0.###}点生命、每秒{HeroRules.StrRegenBonus:0.###}点生命恢复。");
    }

    private string AgilityAttributeTip()
    {
        string highlight = UiString("AGILITY_HILIGHT", "");
        string armor = FillPercent(UiString("BONUS_DEFENSE_FIXED", ""),
            HeroRules.AgiDefenseBonus > 0f ? 1f / HeroRules.AgiDefenseBonus : 0f);
        string speed = UiString("BONUS_ATTACK_SPEED", "");
        return JoinTip(highlight, JoinTip(armor, speed),
            $"每点敏捷增加英雄{HeroRules.AgiDefenseBonus:0.###}点护甲、{HeroRules.AgiAttackSpeedBonus * 100:0.###}%攻击速度。");
    }

    private string IntellectAttributeTip()
    {
        string highlight = UiString("INTELLECT_HILIGHT", "");
        string mana = FillPercent(UiString("BONUS_MANA", ""), HeroRules.IntManaBonus);
        string regen = UiString("BONUS_MANAREGEN", "");
        return JoinTip(highlight, JoinTip(mana, regen),
            $"每点智力增加英雄{HeroRules.IntManaBonus:0.###}点魔法值、每秒{HeroRules.IntRegenBonus:0.###}点魔法恢复。");
    }

    private static string FillPercent(string template, float value)
        => string.IsNullOrWhiteSpace(template) ? "" : template.Replace("%d", value.ToString("0.###"));

    private static string JoinTip(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first)) return string.IsNullOrWhiteSpace(second) ? "" : second;
        return string.IsNullOrWhiteSpace(second) ? first : $"{first}|n{second}";
    }

    private static string JoinTip(string first, string second, string fallback)
    {
        string joined = JoinTip(first, second);
        return string.IsNullOrWhiteSpace(joined) ? fallback : joined;
    }

    /// <summary>
    /// 世界层飘字（客户端 MeleeHudController.RenderWorldText）：只领取本帧的投影结果，
    /// 真正的绘制在 <see cref="_Draw"/> 里、所有 HUD 之前完成——数字盖在任何 UI 上都是错的。
    /// </summary>
    internal void RenderWorldText(IReadOnlyList<TextTagFrame.TagView> tags)
    {
        _worldText = tags;
        QueueRedraw();
    }

    /// <summary>选中地面物品时的详情面板；传 null 收起。</summary>
    public void SetGroundItem(ItemType type, int charges)
    {
        _itemDetail.Update(type, charges);
        QueueRedraw();
    }

    public void SetHeroes(IReadOnlyList<Unit> heroes, Unit selected)
    {
        _heroBar.Update(heroes, selected);
        QueueRedraw();
    }

    public void SetSelection(Unit selected, IReadOnlyList<Unit> selection,
        IReadOnlyList<Unit> cargo = null)
    {
        _selectedUnit = selected;
        _card.UpdateMenuSelection(selected);
        _portrait.Update(selected);
        _portraitViewport.Show(selected);
        bool cargoMode = cargo != null && cargo.Count > 0;
        _selectionGroup.Update(selected, cargoMode ? cargo : selection, cargoMode);
        QueueRedraw();
    }

    public bool LearnMenuOpen => _card.LearnMenuOpen;

    public bool AboutDialogVisible => _aboutDialog?.Visible == true;

    public void ShowAboutDialog() => _aboutDialog?.Show();

    public void HideAboutDialog() => _aboutDialog?.Close();

    public void ToggleAboutDialog() => _aboutDialog?.Toggle();

    public bool BuildMenuOpen => _card.BuildMenuOpen;

    public bool SpellBookOpen => _card.SpellBookOpen;

    public RuntimeSpellBookAbility CurrentSpellBook => _card.CurrentSpellBook;

    public void OpenLearnMenu(Unit selected) => _card.OpenLearnMenu(selected);

    public void CloseLearnMenu() => _card.CloseLearnMenu();

    public void OpenBuildMenu(Unit selected) => _card.OpenBuildMenu(selected);

    public void CloseBuildMenu() => _card.CloseBuildMenu();

    public void OpenSpellBook(Unit selected, RuntimeSpellBookAbility book) => _card.OpenSpellBook(selected, book);

    public void BackFromSpellBook() => _card.BackFromSpellBook();

    public void BackMenu() => _card.BackMenu();

    /// <summary>调试用：等价于左键点某一格命令卡（走真实点击链路）。</summary>
    public void ClickCommandSlot(int slot) => InvokeEntry(SlotEntry(slot));

    public CommandEntry CommandAtHotkey(char hotkey)
    {
        for (int column = 0; column < 4; column++)
            for (int row = 0; row < 3; row++)
            {
                int slot = row * 4 + column;
                CommandEntry entry = _slotEntries[slot];
                if (entry == null || !entry.Enabled) continue;
                string key = string.IsNullOrWhiteSpace(entry.Hotkey)
                    ? CommandCardBuilder.FallbackHotkey(slot) : entry.Hotkey;
                if (key.Length != 1 || char.ToUpperInvariant(key[0]) != hotkey) continue;
                return entry;
            }
        return null;
    }

    /// <summary>命令卡当前 12 格的占用情况（调试用：格号:名字）。</summary>
    public string DescribeCommandSlots()
    {
        var parts = new List<string>();
        for (int i = 0; i < _slotEntries.Length; i++)
        {
            CommandEntry entry = _slotEntries[i];
            if (entry == null) continue;
            parts.Add($"{i}:{entry.Name}{(entry.Enabled ? "" : "(禁用)")}"
                + (string.IsNullOrEmpty(entry.Overlay) ? "" : $"[{entry.Overlay}]"));
        }
        return string.Join(" ", parts);
    }

    public void SetInventory(IReadOnlyList<CommandEntry> entries)
    {
        for (int i = 0; i < _inventoryEntries.Length; i++)
            _inventoryEntries[i] = entries != null && i < entries.Count ? entries[i] : null;
        _inventory.Update(entries);
        QueueRedraw();
    }

    public void SetQueueSlots(IReadOnlyList<QueueEntry> entries)
    {
        _queue.Update(entries);
        QueueRedraw();
    }

    private void InvokeEntry(CommandEntry entry)
    {
        if (entry == null) return;
        if (Listener != null)
        {
            Listener.OnUiCommand(new UiCommand(entry.Kind, entry.Payload,
                Context: entry, NativeEventKind: 1));
            return;
        }
        entry.Action?.Invoke();
    }

    public void UpdateIdleWorkers(IReadOnlyList<Unit> workers, int localPlayerId, float time)
    {
        _idlePeon.Update(workers, localPlayerId, time);
        QueueRedraw();
    }

    public void TriggerIdlePeon() => _idlePeon.TriggerIdlePeon();

    public void SetCommands(IReadOnlyList<CommandEntry> commands)
    {
        var slots = new CommandSlotView?[CommandCardBuilder.SlotCount];
        Array.Clear(_slotEntries, 0, _slotEntries.Length);
        int next = 0;
        if (commands != null)
        {
            foreach (CommandEntry entry in commands)
            {
                if (entry == null) continue;
                int requested = entry.SlotIndex is >= 0 and < CommandCardBuilder.SlotCount
                    ? entry.SlotIndex
                    : next;
                int index = CommandCardBuilder.FindNativeSlot(slots, requested);
                if (index < 0) continue;
                next = index + 1;

                float cooldown = entry.CooldownTotal > 0.01f
                    ? Mathf.Clamp(entry.CooldownRemaining / entry.CooldownTotal, 0f, 1f)
                    : 0f;
                string overlay = !string.IsNullOrEmpty(entry.Overlay)
                    ? entry.Overlay
                    : entry.MaxCharges > 0 && entry.Charges > 0 ? entry.Charges.ToString() : null;
                slots[index] = new CommandSlotView(
                    Icon: "", Hotkey: entry.Hotkey, Disabled: !entry.Enabled,
                    Cooldown: cooldown, Overlay: overlay,
                    Title: entry.Name,
                    Body: string.IsNullOrWhiteSpace(entry.Cost)
                        ? entry.Tooltip
                        : (entry.Tooltip ?? "")
                            + (string.IsNullOrEmpty(entry.Tooltip) ? "" : "\n") + entry.Cost,
                    IconTexture: entry.Icon == null ? null : new GodotUiTexture(entry.Icon));
                _slotEntries[index] = entry;
            }
        }
        _commands = new CommandCardView(slots);
        _card.Build(_commands, _selectedUnit);
        ApplyPendingKind();
        QueueRedraw();
    }

    public void SetPendingCommand(UiCommandKind? kind)
    {
        _pendingKind = kind;
        ApplyPendingKind();
        QueueRedraw();
    }

    private void ApplyPendingKind()
    {
        _card.SetPending(slot =>
        {
            CommandEntry entry = SlotEntry(slot);
            return _pendingKind.HasValue && entry != null && entry.Kind == _pendingKind.Value;
        });
    }

    private void OnCommandClicked(UiFrame frame, UiCommand command, int button)
    {
        if (command.Kind == UiCommandKind.UseItem)
        {
            if (button == 1) DispatchItem(command.Slot, right: true);
            else DispatchItem(command.Slot, right: false);
            return;
        }
        DispatchCommand(command, button == 1 ? 2 : 1);
    }

    private void DispatchItem(int slot, bool right)
    {
        CommandEntry entry = slot >= 0 && slot < _inventoryEntries.Length ? _inventoryEntries[slot] : null;
        if (entry == null) return;
        if (!right)
        {
            InvokeEntry(entry);
            return;
        }
        if (Listener != null)
        {
            Listener.OnUiCommand(new UiCommand(entry.Kind, entry.Payload, slot, entry, NativeEventKind: 2));
            return;
        }
        entry.RightAction?.Invoke();
    }

    /// <summary>UI 设计单位 → 窗口像素（拖放落点要拿去射线拾取）。</summary>
    public Vector2 UiToScreen(NVector2 point)
    {
        Vector2 size = Size;
        if (size.X <= 0f || size.Y <= 0f) size = GetViewportRect().Size;
        return new Vector2(point.X * size.X / DesignWidth, point.Y * size.Y / DesignHeight);
    }

    private void OnCommandRightClicked(UiFrame frame, UiCommand command)
        => DispatchCommand(command, 2);

    private void DispatchCommand(UiCommand command, int eventKind)
    {
        CommandEntry entry = SlotEntry(command.Slot);
        if (entry == null) return;
        if (Listener != null)
        {
            Listener.OnUiCommand(new UiCommand(entry.Kind, entry.Payload, command.Slot,
                entry, NativeEventKind: eventKind));
            return;
        }
        if (eventKind >= 2) entry.RightAction?.Invoke();
        else entry.Action?.Invoke();
    }

    public void SetSelectionBox(Rect2 rect, bool visible)
    {
        if (!visible || rect.Size.X <= 1f || rect.Size.Y <= 1f)
        {
            _selectionBox = null;
            QueueRedraw();
            return;
        }
        Vector2 size = GetViewportRect().Size;
        var lo = new Vector2(rect.Position.X * DesignWidth / Mathf.Max(1f, size.X),
            rect.Position.Y * DesignHeight / Mathf.Max(1f, size.Y));
        var hi = new Vector2(rect.End.X * DesignWidth / Mathf.Max(1f, size.X),
            rect.End.Y * DesignHeight / Mathf.Max(1f, size.Y));
        _selectionBox = new UiRect(lo.X, lo.Y, hi.X - lo.X, hi.Y - lo.Y);
        QueueRedraw();
    }

    private CommandEntry SlotEntry(int slot)
        => slot >= 0 && slot < _slotEntries.Length ? _slotEntries[slot] : null;

    public bool HandlePointerDown(NVector2 uiPos, int button)
    {
        _router.PointerDown(uiPos, button);
        return IsPointerOverHud(uiPos);
    }

    public bool HandlePointerUp(NVector2 uiPos, int button)
    {
        bool hit = IsPointerOverHud(uiPos);
        _router.PointerUp(uiPos, button);
        return hit;
    }

    public void HandlePointerMove(NVector2 uiPos) => _router.PointerMove(uiPos);

    public bool HandleWheel(NVector2 uiPos, float delta) => _router.Wheel(uiPos, delta);

    public void ClearHover() => _router.ClearHover();

    public bool IsPointerOverHud(NVector2 uiPos)
    {
        if (uiPos.X >= 0f && uiPos.X < DesignWidth
            && (uiPos.Y <= TopChromeHeight || uiPos.Y >= DesignHeight - BottomChromeHeight))
            return true;
        UiFrame hit = _root.HitTest(uiPos);
        return hit != null && !ReferenceEquals(hit, _root);
    }

    public bool IsScreenPointOverHud(Vector2 screenPoint)
        => IsPointerOverHud(ScreenToUi(screenPoint));

    public override void _GuiInput(InputEvent @event)
    {
        if (_router == null) return;

        if (@event is InputEventMouseButton button)
        {
            NVector2 point = ScreenToUi(button.Position);
            if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (button.Pressed && HandleWheel(point,
                    button.ButtonIndex == MouseButton.WheelUp ? 1f : -1f)) AcceptEvent();
                return;
            }

            if (button.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return;
            int index = button.ButtonIndex == MouseButton.Right ? 1 : 0;
            if (button.Pressed)
            {
                if (HandlePointerDown(point, index))
                {
                    _uiPointerDown = true;
                    AcceptEvent();
                }
            }
            else
            {
                bool hit = HandlePointerUp(point, index);
                if (hit || _uiPointerDown)
                {
                    _uiPointerDown = false;
                    AcceptEvent();
                }
            }
            return;
        }

        if (@event is InputEventMouseMotion motion)
        {
            HandlePointerMove(ScreenToUi(motion.Position));
            if (_uiPointerDown) AcceptEvent();
        }
    }

    private NVector2 ScreenToUi(Vector2 point)
    {
        Vector2 size = Size;
        if (size.X <= 0f || size.Y <= 0f) size = GetViewportRect().Size;
        if (size.X <= 0f || size.Y <= 0f) size = new Vector2(1920f, 1080f);
        return new NVector2(point.X * DesignWidth / Mathf.Max(1f, size.X),
            point.Y * DesignHeight / Mathf.Max(1f, size.Y));
    }

    public override void _Process(double delta)
    {
        _commandError?.Update();
        QueueRedraw();
    }

    internal bool HoverForProbe(string frameName)
    {
        UiFrame frame = FindFrame(_root, frameName);
        if (frame == null) return false;
        OnHoverChanged(frame);
        return true;
    }

    internal void SelectForProbe(Unit unit) => _selectedUnit = unit;
    internal bool TooltipVisible => _tooltip.Visible;
    internal string TooltipTitle => _tooltip.TitleText;
    internal string TooltipBody => _tooltip.BodyText;
    internal bool BuffIconVisible(int slot) => _infoPanel.BuffIconVisible(slot);
    internal UiRect BuffIconBounds(int slot) => _infoPanel.BuffIconBounds(slot);
    internal bool TryGetBuffTooltip(int slot, out string title, out string body)
        => _infoPanel.TryGetBuffTooltip(slot, out title, out body);
    internal void LayoutInfoPanelForProbe() => _infoPanel.Layout(DesignWidth, DesignHeight);
    internal bool CommandErrorVisible => _commandError?.Visible ?? false;
    internal string CommandErrorMessage => _commandError?.Message ?? "";

    private static UiFrame FindFrame(UiFrame frame, string name)
    {
        if (frame == null) return null;
        if (frame.Name == name) return frame;
        foreach (UiFrame child in frame.Children)
        {
            UiFrame found = FindFrame(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static CommandCardView SampleCommands()
    {
        var slots = new CommandSlotView?[CommandCardBuilder.SlotCount];
        slots[0] = new(@"ReplaceableTextures\CommandButtons\BTNMove.blp", "M", Title: "移动");
        slots[1] = new(@"ReplaceableTextures\CommandButtons\BTNStop.blp", "S", Title: "停止");
        slots[2] = new(@"ReplaceableTextures\CommandButtons\BTNHoldPosition.blp", "H", Title: "保持位置");
        slots[3] = new(@"ReplaceableTextures\CommandButtons\BTNAttack.blp", "A", Title: "攻击");
        slots[4] = new(@"ReplaceableTextures\CommandButtons\BTNPatrol.blp", "P", Title: "巡逻");
        slots[5] = new(@"ReplaceableTextures\CommandButtons\BTNHolyBolt.blp", "T",
            Cooldown: 0.45f, Overlay: "3", Title: "圣光术");
        slots[7] = new(@"ReplaceableTextures\CommandButtons\BTNBuild.blp", "B", Disabled: true, Title: "建造");
        return new CommandCardView(slots);
    }

    public override void _Draw()
    {
        if (_root == null || _device == null) return;

        Vector2 viewport = GetViewportRect().Size;
        if (Size != viewport) Size = viewport;
        float scaleX = viewport.X / DesignWidth;
        float scaleY = viewport.Y / DesignHeight;

        _root.UpdateViewport(DesignWidth, DesignHeight);
        _console.Layout(DesignWidth, DesignHeight);
        _infoPanel.Layout(DesignWidth, DesignHeight);
        _itemDetail.Layout(DesignWidth, DesignHeight);
        _card.Layout(DesignWidth, DesignHeight);
        _topBar.Layout(DesignWidth, DesignHeight);
        _minimap.Layout(DesignWidth, DesignHeight);
        _heroBar.Layout(DesignWidth, DesignHeight);
        _portrait.Layout(DesignWidth, DesignHeight);
        _selectionGroup.Layout(DesignWidth, DesignHeight);
        _idlePeon.Layout(DesignWidth, DesignHeight);
        _inventory.Layout(DesignWidth, DesignHeight);
        _queue.Layout(DesignWidth, DesignHeight);
        _tooltip.Layout(DesignWidth, DesignHeight);
_controlGroups.Layout(DesignWidth, DesignHeight);
        _aboutDialog.Layout(DesignWidth, DesignHeight);
        _commandError.Layout();

        _device.Begin(DesignWidth, DesignHeight, scaleX, scaleY);
        _chat.Update(DesignWidth, DesignHeight, _device);
        _textTag.Update(_worldText, DesignWidth, DesignHeight);
        if (_textTag.Visible) _textTag.Render(_device);
        if (_selectionBox is { } box)
            _device.DrawTexturedQuad(box, _device.WhiteTexture,
                new System.Numerics.Vector4(0.35f, 0.85f, 0.35f, 0.16f));
        if (_timeOfDayViewport is { Loaded: true })
            _device.DrawTexturedQuad(_timeOfDayViewport.ModelArea,
                _timeOfDayTexture, System.Numerics.Vector4.One);
        _root.Render(_device);
        if (_selectedUnit != null && _portrait.Visible)
            _device.DrawTexturedQuad(PortraitFrame.ModelArea(DesignHeight),
                _portraitTexture, System.Numerics.Vector4.One);
        _device.End();
    }
}
