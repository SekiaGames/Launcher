using Godot;
using System;
using System.Collections.Generic;

using OpenWC3.Game.UI;
namespace OpenWC3.Game;

/// <summary>
/// 技能施放输入与命令卡。对标客户端 Game/Scenes/War3GameScene/QuickCast.cs：
/// 点技能按钮进入待施放状态 → 再点目标/地面施放；右键或 Esc 取消。
/// </summary>
public sealed partial class War3GameScene
{
    private Ability _pendingCast;
    private Unit _pendingCaster;
    private Item _pendingItem;
    private Unit _pendingItemHero;
    private int _pendingItemSlot = -1;

    public bool CastPending => _pendingCast != null || _pendingItem != null || CommandPending;

    /// <summary>命令卡点技能：无目标技能立刻放，有目标的进入待施放。</summary>
    public void BeginCast(Unit caster, Ability ability)
    {
        if (caster == null || ability == null) return;
        if (_pendingItem != null) CancelCast();
        ClearPendingCommand();
        if (!ability.Learned || !ability.Ready)
        {
            _hud?.SetHint(ability.Learned ? $"{ability.Name} 冷却中（{ability.CooldownRemaining:0.0}s）" : "技能还没学会");
            return;
        }
        // 开关型技能「关掉」不花法力，所以此时不校验法力
        bool turningOff = ability.IsToggle && ability.ToggledOn;
        if (!turningOff && caster.Mana < ability.ManaCost)
        {
            _hud?.SetHint($"{ability.Name}：法力不足（需要 {ability.ManaCost:0}）");
            return;
        }

        if (ability.TargetMode == AbilityTargetMode.None)
        {
            CastNow(caster, ability, null, caster.Position);
            return;
        }

        _pendingCast = ability;
        _pendingCaster = caster;
        _hud?.SetHint(ability.TargetMode == AbilityTargetMode.Unit
            ? $"选择 {ability.Name} 的目标{(ability.TargetsCorpse ? "（尸体）" : "")}（右键取消）"
            : $"选择 {ability.Name} 的落点（右键取消）");
    }

    public void CancelCast()
    {
        bool had = _pendingCast != null || _pendingItem != null;
        _pendingCast = null;
        _pendingCaster = null;
        _pendingItem = null;
        _pendingItemHero = null;
        _pendingItemSlot = -1;
        ClearPendingCommand();
        if (had) _hud?.SetHint("");
    }

    /// <summary>左键点击时优先走施法/物品使用；返回 true 表示这次点击已被消费掉。</summary>
    public bool TryCastAt(Vector2 screenPosition)
    {
        if (_pendingItem != null) return TryUsePendingItem(screenPosition);
        if (_pendingCast == null) return false;

        Ability ability = _pendingCast;
        Unit caster = _pendingCaster;
        if (caster == null || !caster.Alive || !ability.Ready)
        {
            CancelCast();
            return false;
        }

        Unit target = null;
        if (ability.TargetMode == AbilityTargetMode.Unit)
        {
            target = PickUnit(screenPosition, ViewPlayer, true, ability.TargetsCorpse);
            if (target == null)
            {
                _hud?.SetHint($"没点到 {ability.Name} 的目标{(ability.TargetsCorpse ? "（尸体）" : "")}");
                return true;
            }
        }

        Vector3 point = caster.Position;
        if (ability.TargetMode == AbilityTargetMode.Point && !TryGroundPoint(screenPosition, out point))
        {
            CancelCast();
            return true;
        }

        CastNow(caster, ability, target, point);
        CancelCast();
        return true;
    }

    /// <summary>待施放的物品：按计划收目标/落点再去用（对标客户端 UseItem 的目标先前置验证）。</summary>
    private bool TryUsePendingItem(Vector2 screenPosition)
    {
        Item item = _pendingItem;
        Unit hero = _pendingItemHero;
        int slot = _pendingItemSlot;
        CancelCast();

        if (hero == null || !hero.Alive || item?.Type == null) return false;
        if (slot < 0 || slot >= hero.Inventory.Length || hero.Inventory[slot] != item) return true;

        ItemTargetMode mode = ItemUseRules.TargetModeFor(item.Type);
        Unit target = null;
        Vector3 point = hero.Position;

        if (mode == ItemTargetMode.Unit)
        {
            target = PickUnit(screenPosition, ViewPlayer, true);
            if (target == null)
            {
                _hud?.SetHint($"没点到 {item.Type.Name} 的目标");
                return true;
            }
        }
        else if (mode == ItemTargetMode.Point && !TryGroundPoint(screenPosition, out point))
        {
            return true;
        }

        UseItemNow(hero, item, slot, target, point);
        return true;
    }

    /// <summary>命令卡/小键盘点击物品：需要目标的进待施放，其余立刻用掉。</summary>
    private void ActivateItem(Unit hero, Item item, int slot)
    {
        if (hero == null || item?.Type == null) return;

        ItemUsePlan plan = ItemUseRules.PlanFor(item.Type);
        if (!plan.Usable)
        {
            _hud?.SetHint($"{item.Type.Name} 不能主动使用（左键丢弃）");
            return;
        }
        if (item.CooldownRemaining > 0f)
        {
            _hud?.SetHint($"{item.Type.Name} 冷却中（{Mathf.CeilToInt(item.CooldownRemaining)}s）");
            return;
        }
        if (plan.TargetMode == ItemTargetMode.None)
        {
            UseItemNow(hero, item, slot, null, hero.Position);
            return;
        }

        CancelCast();
        _pendingItem = item;
        _pendingItemHero = hero;
        _pendingItemSlot = slot;
        _hud?.SetHint(plan.TargetMode == ItemTargetMode.Unit
            ? $"选择 {item.Type.Name} 的目标（右键取消）"
            : $"选择 {item.Type.Name} 的落点（右键取消）");
    }

    private void UseItemNow(Unit hero, Item item, int slot, Unit target, Vector3 point)
    {
        if (_world == null || item?.Type == null) return;

        if (_world.UseItem(hero, item, target, point))
        {
            string victim = target?.Type?.Name != null ? $" → {target.Type.Name}" : "";
            _hud?.SetHint($"{hero.Type.Name} 使用了 {item.Type.Name}{victim}");
        }
        else
        {
            _hud?.SetHint($"{item.Type.Name} 现在用不了");
        }

        RefreshHud();
        UpdateCommandCard();
    }

    private bool CastNow(Unit caster, Ability ability, Unit target, Vector3 point)
    {
        if (_world == null) return false;

        bool ok = _world.TryCast(caster, ability, target, point);
        if (!ok)
        {
            _hud?.SetHint($"{ability.Name} 施放失败");
            return false;
        }

        PlayVoice(caster, "What", null);
        RefreshHud();
        UpdateCommandCard();
        return true;
    }

    /// <summary>
    /// 英雄技能命令卡，口径与原版一致：
    /// 没学的技能占一格（点=学）；**有点数且还能升级时，技能图标上挂「+」，点它就是升级**；
    /// 否则点它才是施法。被动技能只在没学/可升级时占格。
    /// </summary>
    private void AddAbilityCommands(System.Collections.Generic.List<CommandEntry> commands, Unit hero)
    {
        foreach (string id in hero.Type.HeroAbilList)
        {
            AbilityType type = AbilityCatalog.Get(id);
            if (type == null) continue;

            Ability ability = hero.FindAbility(id) ?? hero.GiveAbility(id, 0);
            if (ability == null) continue;

            bool canUpgrade = ability.CanLevelUp && hero.SkillPoints > 0 && LevelRequirementMet(hero, ability);
            if (!ability.Learned || (ability.IsPassive && canUpgrade))
            {
                commands.Add(LearnEntry(hero, ability));
                continue;
            }

            if (canUpgrade)
            {
                // 原版：技能图标右上角一个「+」，点一下就是加点（优先于施法）
                CommandEntry upgrade = LearnEntry(hero, ability);
                upgrade.Name = ability.Name;
                upgrade.Cost = "+";
                upgrade.Tooltip = AbilityTooltip(ability, ability.Level + 1) + "\n左键：升级（1 技能点）";
                commands.Add(upgrade);
                continue;
            }

            if (ability.IsPassive && ability.SupportsAutoCast)
            {
                AddArrowCommand(commands, hero, ability);
                continue;
            }

            // 已学的被动（重击/闪避/重生这类）：占一格但不给点
            if (ability.IsPassive)
            {
                commands.Add(new CommandEntry
                {
                    Id = id,
                    Name = ability.Name,
                    Cost = "被动",
                    Hotkey = ability.Hotkey,
                    Tooltip = AbilityTooltip(ability, ability.Level),
                    Icon = TechTree.Icon(ability.IconArt),
                    Enabled = false,
                });
                continue;
            }

            // 开关型技能开着时角标显示「开启」，关掉不花法力所以法力不足也能点
            bool toggledOn = ability.IsToggle && ability.ToggledOn;
            bool autoOn = ability.SupportsAutoCast && ability.AutoCastEnabled && hero.AutoCast;
            string cost = toggledOn
                ? "开启"
                : autoOn
                    ? "自动"
                    : ability.Ready
                        ? $"{ability.ManaCost:0} 法力"
                        : $"{Mathf.CeilToInt(ability.CooldownRemaining)}s";

            commands.Add(new CommandEntry
            {
                Id = id,
                Name = ability.Name,
                Cost = cost,
                Hotkey = ability.Hotkey,
                Tooltip = AbilityTooltip(ability, ability.Level) + AutoCastHint(ability),
                Icon = TechTree.Icon(ability.IconArt),
                Enabled = ability.Ready && (toggledOn || hero.Mana >= ability.ManaCost),
                Action = () => BeginCast(hero, ability),
                RightAction = ability.SupportsAutoCast ? () => ToggleAutoCast(hero, ability) : null,
                CooldownRemaining = ability.CooldownRemaining,
                CooldownTotal = ability.Type.CooldownAt(Mathf.Max(1, ability.Level)),
            });
        }

        AddInventoryCommands(hero);
    }

    /// <summary>
    /// 非英雄单位的技能格（德鲁伊熊/乌鸦形态这类）：等级固定、没有技能点，点一下就是施放。
    /// </summary>
    private void AddUnitAbilityCommands(System.Collections.Generic.List<CommandEntry> commands, Unit unit)
    {
        foreach (Ability ability in unit.Abilities)
        {
            if (!ability.Learned || ability.IsAura) continue;
            if (ability.IsPassive)
            {
                if (ability.SupportsAutoCast) AddArrowCommand(commands, unit, ability);
                continue;
            }

            bool toggledOn = ability.IsToggle && ability.ToggledOn;
            bool autoOn = ability.SupportsAutoCast && ability.AutoCastEnabled && unit.AutoCast;
            string cost = toggledOn
                ? "开启"
                : autoOn
                    ? "自动"
                    : ability.Ready
                        ? $"{ability.ManaCost:0} 法力"
                        : $"{Mathf.CeilToInt(ability.CooldownRemaining)}s";

            commands.Add(new CommandEntry
            {
                Id = ability.Id,
                Name = ability.Name,
                Cost = cost,
                Hotkey = ability.Hotkey,
                Tooltip = AbilityTooltip(ability, ability.Level) + AutoCastHint(ability),
                Icon = TechTree.Icon(ability.IconArt),
                Enabled = ability.Ready && (toggledOn || unit.Mana >= ability.ManaCost),
                Action = () => BeginCast(unit, ability),
                RightAction = ability.SupportsAutoCast ? () => ToggleAutoCast(unit, ability) : null,
                CooldownRemaining = ability.CooldownRemaining,
                CooldownTotal = ability.Type.CooldownAt(Mathf.Max(1, ability.Level)),
            });
        }
    }

    private void AddArrowCommand(System.Collections.Generic.List<CommandEntry> commands, Unit owner, Ability ability)
    {
        commands.Add(new CommandEntry
        {
            Id = ability.Id,
            Name = ability.Name,
            Cost = ability.AutoCastEnabled ? "自动" : "关闭",
            Hotkey = ability.Hotkey,
            Tooltip = AbilityTooltip(ability, ability.Level) + "\n左键/右键：开/关自动施法",
            Icon = TechTree.Icon(ability.IconArt),
            Active = ability.AutoCastEnabled,
            Action = () => ToggleAutoCast(owner, ability),
            RightAction = () => ToggleAutoCast(owner, ability),
        });
    }

    private static bool LevelRequirementMet(Unit hero, Ability ability)
    {
        if (ability?.Type == null) return false;
        int next = ability.Level + 1;
        int skip = Mathf.Max(1, ability.Type.LevelSkip);
        int need = (next - 1) * skip + Mathf.Max(1, ability.Type.RequiredLevel);
        return hero.Level >= need;
    }

    private static string AutoCastHint(Ability ability)
    {
        return ability != null && ability.SupportsAutoCast ? "\n右键：开/关自动施法" : "";
    }

    /// <summary>
    /// 原版命令卡是 4×3，物品栏固定占**右两列**（第 3、4 列 × 3 行），技能占左两列。
    /// 槽位顺序按行铺：2,3 / 6,7 / 10,11。
    /// </summary>
    /// <summary>
    /// 右键物品：附近有友方英雄就转交给他（原版把物品给单位），否则丢到地上。
    /// </summary>
    private void GiveOrDropItem(Unit hero, int slot)
    {
        Item item = hero?.Inventory != null && slot < hero.Inventory.Length ? hero.Inventory[slot] : null;
        if (item?.Type == null) return;

        Unit ally = NearestFriendlyHero(hero.Player, hero.Position, 450f);
        if (ally != null && !ReferenceEquals(ally, hero) && _world != null && _world.GiveItem(hero, slot, ally))
        {
            _hud?.SetHint($"{hero.Type.Name} 把 {item.Type.Name} 交给了 {ally.Type.Name}");
            RefreshHud();
            UpdateCommandCard();
            return;
        }

        DropInventorySlot(hero, slot);
    }

    /// <summary>右键丢物品（原版也是右键把物品丢到地上）。</summary>
    private void DropInventorySlot(Unit hero, int slot)
    {
        Item item = hero.Inventory[slot];
        if (_world == null || item?.Type == null) return;

        if (_world.DropItemFromSlot(hero, slot))
        {
            _hud?.SetHint($"{hero.Type.Name} 丢下了 {item.Type.Name}");
            RefreshHud();
            UpdateCommandCard();
        }
    }

    /// <summary>选中单位的热键：小键盘 1~6 用物品（仅英雄），字母键按物编热键施法（英雄与德鲁伊这类单位都算）。</summary>
    public bool TrySelectionHotkey(Key keycode)
    {
        if (_selected.Count == 0) return false;
        Unit picked = _selected.Count == 1 ? _selected[0] : null;

        if (picked != null && TrySelectionItemOrAbilityHotkey(picked, keycode)) return true;
        return TryStandardCommandHotkey(_selected[0], keycode);
    }

    /// <summary>小键盘 1~6 对应英雄物品六格（原版物品热键就在小键盘上）。</summary>
    public bool TryInventoryHotkey(Key keycode)
    {
        if (_hud == null) return false;
        if (_selected.Count != 1 || !_selected[0].IsHero) return false;
        int slot = keycode switch
        {
            Key.Kp1 => 0,
            Key.Kp2 => 1,
            Key.Kp3 => 2,
            Key.Kp4 => 3,
            Key.Kp5 => 4,
            Key.Kp6 => 5,
            _ => -1,
        };
        return slot >= 0 && _hud.ActivateItemSlot(slot);
    }

    private bool TrySelectionItemOrAbilityHotkey(Unit picked, Key keycode)
    {
        int slot = (int)(keycode - Key.Kp1);
        if (slot is >= 0 and < Unit.InventorySize && picked.IsHero)
        {
            Item item = picked.Inventory[slot];
            if (item?.Type == null) return false;
            ActivateItem(picked, item, slot);
            return true;
        }

        foreach (Ability ability in picked.Abilities)
        {
            if (!ability.Learned || ability.IsPassive) continue;
            if (string.IsNullOrEmpty(ability.Hotkey)) continue;
            char pressed = KeyToLetter(keycode);
            if (pressed == '\0') continue;
            if (char.ToUpperInvariant(ability.Hotkey[0]) != pressed) continue;
            BeginCast(picked, ability);
            return true;
        }

        return false;
    }

    private static char KeyToLetter(Key keycode)
    {
        if (keycode < Key.A || keycode > Key.Z) return '\0';
        return (char)('A' + (int)(keycode - Key.A));
    }

    /// <summary>
    /// 物品格：原版是英雄面板右下 2×3 的格子，这里先并到命令卡上（一物一格）。
    /// 能主动用的点一下就用（或进选目标），用不了的/右键都是丢到地上。
    /// </summary>
    private void AddInventoryCommands(Unit hero)
    {
        var entries = new List<CommandEntry>(Unit.InventorySize);
        for (int slot = 0; slot < Unit.InventorySize; slot++)
        {
            Item item = hero.Inventory[slot];
            if (item?.Type == null)
            {
                entries.Add(null);
                continue;
            }

            int captured = slot;
            ItemUsePlan plan = ItemUseRules.PlanFor(item.Type);
            bool ready = plan.Usable && item.CooldownRemaining <= 0f;
            int charges = World.ChargesLeft(item);
            string cost = item.CooldownRemaining > 0f
                ? $"{Mathf.CeilToInt(item.CooldownRemaining)}s"
                : charges > 0 ? charges.ToString() : "";
            entries.Add(new CommandEntry
            {
                Id = item.Type.Id,
                Name = item.Type.Name,
                Cost = cost,
                Hotkey = (slot + 1).ToString(),
                Tooltip = ItemTooltip(item, plan),
                Icon = TechTree.Icon(item.Type.IconArt),
                Enabled = ready,
                Action = () => ActivateItem(hero, item, captured),
                RightAction = () => GiveOrDropItem(hero, captured),
                CooldownRemaining = item.CooldownRemaining,
                CooldownTotal = plan.Cooldown > 0f ? plan.Cooldown : item.CooldownRemaining,
                Charges = charges,
                MaxCharges = item.Type.Uses,
            });
        }
        _hud?.SetInventory(entries);
    }

    private static string ItemTooltip(Item item, ItemUsePlan plan)
    {
        ItemType type = item.Type;
        var text = $"{type.Name}（等级 {type.Level} {type.Class}）";
        if (!string.IsNullOrEmpty(type.BonusText) && type.BonusText != "-") text += "\n" + type.BonusText;
        text += plan.TargetMode switch
        {
            ItemTargetMode.Unit => "\n左键：使用（选目标）",
            ItemTargetMode.Point => "\n左键：使用（选落点）",
            _ => plan.Usable ? "\n左键：使用" : "\n左键：丢到地上",
        };
        if (item.CooldownRemaining > 0f) text += $"\n冷却 {item.CooldownRemaining:0.0}s";
        int charges = World.ChargesLeft(item);
        if (charges > 0) text += $"\n剩余 {charges} 次";
        return text;
    }

    /// <summary>学习/升级格：技能点 + 英雄等级 双重门槛。</summary>
    private CommandEntry LearnEntry(Unit hero, Ability ability)
    {
        AbilityType type = ability.Type;
        int nextLevel = ability.Level + 1;
        int need = (nextLevel - 1) * Mathf.Max(1, type.LevelSkip) + Mathf.Max(1, type.RequiredLevel);
        bool levelOk = hero.Level >= need;
        bool hasPoint = hero.SkillPoints > 0;
        string action = ability.Learned ? "升级" : "学习";

        return new CommandEntry
        {
            Id = ability.Id,
            Name = $"{action} {ability.Name}",
            Cost = hasPoint ? "1 技能点" : "无点数",
            Tooltip = AbilityTooltip(ability, nextLevel) + (levelOk ? "" : $"\n需要英雄等级 {need}"),
            Icon = TechTree.Icon(ability.IconArt),
            Enabled = levelOk && hasPoint,
            Action = () =>
            {
                if (_world == null) return;
                if (_world.LearnAbility(hero, ability.Id))
                {
                    _hud?.SetHint($"{ability.Name} 升到 {ability.Level} 级");
                    RefreshHud();
                    UpdateCommandCard();
                }
                else
                {
                    _hud?.SetHint(levelOk ? "没有技能点" : $"需要英雄等级 {need}");
                }
            },
        };
    }

    /// <summary>
    /// 召唤回调：World 不知道场景，建单位这一步由这里做。
    /// 召唤物继承施法者的玩家号，落在施法者身边。
    /// </summary>
    private Unit SummonUnitFor(Unit caster, string unitId, Vector3 position)
    {
        if (caster == null || string.IsNullOrEmpty(unitId)) return null;

        Unit created = Spawn(unitId, caster.Player, position.X, position.Z, caster.Facing, 0f, 1f);
        if (created == null)
        {
            GD.Print($"[SUMMON] 召唤失败：缺类型或模型 {unitId}");
            return null;
        }

        created.AutoCast = caster.AutoCast;
        _world?.Units.Add(created);
        GD.Print($"[SUMMON] {caster.Type?.Name} 召唤出 {created.Type?.Name}（{unitId}）");
        return created;
    }

    private int _castLogBudget = 24;

    /// <summary>技能放出后：语音 + 一条带数值的日志（AI 自动施法也走这里）。</summary>
    private void OnAbilityCast(Unit caster, Ability ability, Unit target)
    {
        if (caster == null || ability == null) return;
        PlayVoice(caster, "What", null);
        PlayLightningEffects(caster, ability, target);

        if (_castLogBudget <= 0) return;
        _castLogBudget--;

        string victim = target != null ? $" → {target.Type?.Name}（{target.Hp:0}/{target.MaxHp:0}）" : "";
        GD.Print($"[CAST] {caster.Type?.Name}(P{caster.Player}) 施放 {ability.Name} Lv{ability.Level}{victim}"
            + $" 法力 {caster.Mana:0}/{caster.MaxMana:0} 冷却 {ability.CooldownRemaining:0.0}s");
    }

    /// <summary>诊断用：把英雄命令卡的内容摊成一行文本。</summary>
    public string DescribeCommandCard(Unit hero)
    {
        var commands = new System.Collections.Generic.List<CommandEntry>();
        AddAbilityCommands(commands, hero);
        if (commands.Count == 0) return "(空)";

        var parts = new System.Collections.Generic.List<string>();
        foreach (CommandEntry entry in commands)
        {
            parts.Add($"{entry.Name}／{entry.Cost}／图标{(entry.Icon != null ? "有" : "无")}{(entry.Enabled ? "" : "／禁用")}");
        }
        return string.Join(" | ", parts);
    }

    /// <summary>悬停提示：名称 + 等级 + 关键数值（全部来自物编，不写死文案）。</summary>
    private static string AbilityTooltip(Ability ability, int level)
    {
        AbilityType type = ability.Type;
        if (type == null) return ability.Name;

        int shown = Mathf.Clamp(level, 1, type.MaxLevel);
        var text = $"{type.Name}（{Mathf.Min(ability.Level, type.MaxLevel)}/{type.MaxLevel}）";
        if (ability.Level < 1) text = $"{type.Name}（未学，下一级需求英雄等级 {type.RequiredLevel}）";

        if (!ability.IsPassive)
        {
            text += $"\n法力 {type.CostAt(shown):0}   冷却 {type.CooldownAt(shown):0}s";
            if (type.RangeAt(shown) > 0f) text += $"   射程 {type.RangeAt(shown):0}";
        }
        if (type.AreaAt(shown) > 0f) text += $"   半径 {type.AreaAt(shown):0}";
        if (type.DurationAt(shown) > 0f) text += $"   持续 {type.DurationAt(shown):0}s";

        float a = type.DataAt(shown, 'A');
        if (a > 0f) text += $"\nDataA {a:0.##}";
        float b = type.DataAt(shown, 'B');
        if (b > 0f) text += $"   DataB {b:0.##}";
        float c = type.DataAt(shown, 'C');
        if (c > 0f) text += $"   DataC {c:0.##}";

        if (!string.IsNullOrEmpty(type.Ubertip))
        {
            string tip = type.Ubertip.Replace("\"", " ");
            if (tip.Length > 120) tip = tip[..120] + "…";
            text += "\n" + tip;
        }
        return text;
    }
}
