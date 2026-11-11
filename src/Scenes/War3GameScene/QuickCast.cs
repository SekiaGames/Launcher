using Godot;
using System;

namespace Launcher;

/// <summary>
/// 技能施放输入与命令卡。对标客户端 Game/Scenes/War3GameScene/QuickCast.cs：
/// 点技能按钮进入待施放状态 → 再点目标/地面施放；右键或 Esc 取消。
/// </summary>
public sealed partial class War3GameScene
{
    private Ability _pendingCast;
    private Unit _pendingCaster;

    public bool CastPending => _pendingCast != null;

    /// <summary>命令卡点技能：无目标技能立刻放，有目标的进入待施放。</summary>
    public void BeginCast(Unit caster, Ability ability)
    {
        if (caster == null || ability == null) return;
        if (!ability.Learned || !ability.Ready)
        {
            _hud?.SetHint(ability.Learned ? $"{ability.Name} 冷却中（{ability.CooldownRemaining:0.0}s）" : "技能还没学会");
            return;
        }
        if (caster.Mana < ability.ManaCost)
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
            ? $"选择 {ability.Name} 的目标（右键取消）"
            : $"选择 {ability.Name} 的落点（右键取消）");
    }

    public void CancelCast()
    {
        if (_pendingCast == null) return;
        _pendingCast = null;
        _pendingCaster = null;
        _hud?.SetHint("");
    }

    /// <summary>左键点击时优先走施法；返回 true 表示这次点击已被技能消费掉。</summary>
    public bool TryCastAt(Vector2 screenPosition)
    {
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
            target = PickUnit(screenPosition, ViewPlayer, true);
            if (target == null)
            {
                _hud?.SetHint($"没点到 {ability.Name} 的目标");
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
    /// 英雄技能命令卡。对标原版口径：已学的技能占施法格，没学的占学习格，
    /// 有点数时在末尾补「升级 X」格（原版是英雄面板上的 + 按钮，这里合并到同一张卡）。
    /// </summary>
    private void AddAbilityCommands(System.Collections.Generic.List<CommandEntry> commands, Unit hero)
    {
        var upgrades = new System.Collections.Generic.List<CommandEntry>();

        foreach (string id in hero.Type.HeroAbilList)
        {
            AbilityType type = AbilityCatalog.Get(id);
            if (type == null) continue;

            Ability ability = hero.FindAbility(id) ?? hero.GiveAbility(id, 0);
            if (ability == null) continue;

            if (!ability.Learned || ability.IsPassive)
            {
                commands.Add(LearnEntry(hero, ability));
                continue;
            }

            commands.Add(new CommandEntry
            {
                Id = id,
                Name = ability.Name,
                Cost = $"{ability.ManaCost:0} 法力",
                Tooltip = AbilityTooltip(ability, ability.Level),
                Icon = TechTree.Icon(ability.IconArt),
                Enabled = ability.Ready && hero.Mana >= ability.ManaCost,
                Action = () => BeginCast(hero, ability),
            });

            if (ability.CanLevelUp && hero.SkillPoints > 0) upgrades.Add(LearnEntry(hero, ability));
        }

        foreach (CommandEntry entry in upgrades) commands.Add(entry);

        AddInventoryCommands(commands, hero);
    }

    /// <summary>
    /// 物品格：原版是英雄面板右下 2×3 的格子，这里先并到命令卡上（一物一格）。
    /// 可用的（药水/卷轴）点一下就用掉，其余点一下丢到地上。
    /// </summary>
    private void AddInventoryCommands(System.Collections.Generic.List<CommandEntry> commands, Unit hero)
    {
        for (int slot = 0; slot < Unit.InventorySize; slot++)
        {
            Item item = hero.Inventory[slot];
            if (item?.Type == null) continue;

            int captured = slot;
            bool usable = item.Type.Usable && !item.Type.Consumable.IsEmpty;
            commands.Add(new CommandEntry
            {
                Id = item.Type.Id,
                Name = item.Type.Name,
                Cost = $"格 {slot + 1}",
                Tooltip = ItemTooltip(item.Type, usable),
                Icon = TechTree.Icon(item.Type.IconArt),
                Enabled = true,
                Action = () => UseOrDropItem(hero, item, captured, usable),
            });
        }
    }

    private void UseOrDropItem(Unit hero, Item item, int slot, bool usable)
    {
        if (_world == null || item?.Type == null) return;

        if (usable)
        {
            if (_world.UseItem(hero, item))
            {
                _hud?.SetHint($"{hero.Type.Name} 使用了 {item.Type.Name}（{item.Type.Consumable}）");
            }
            else
            {
                _hud?.SetHint($"{item.Type.Name} 现在用不了");
            }
        }
        else if (_world.DropItemFromSlot(hero, slot))
        {
            _hud?.SetHint($"{hero.Type.Name} 丢下了 {item.Type.Name}");
        }

        RefreshHud();
        UpdateCommandCard();
    }

    private static string ItemTooltip(ItemType type, bool usable)
    {
        var text = $"{type.Name}（等级 {type.Level} {type.Class}）";
        if (!string.IsNullOrEmpty(type.BonusText) && type.BonusText != "-") text += "\n" + type.BonusText;
        text += usable ? "\n左键：使用" : "\n左键：丢到地上";
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
