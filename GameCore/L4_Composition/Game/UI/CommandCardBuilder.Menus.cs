using System;
using System.Collections.Generic;
using OpenWC3.Game;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

public sealed partial class CommandCardBuilder
{
    /// <summary>
    /// 建造入口的图标（客户端 BuildMenuIcon）：物编里没有 BTNOrcBuild / BTNUndeadBuild，
    /// 这两个族共用基础建筑图；暗夜精灵用 BTNNightElfBuild。
    /// </summary>
    internal static string BuildMenuIcon(string race) => (race ?? "").ToLowerInvariant() switch
    {
        "orc" or "undead" => @"ReplaceableTextures\CommandButtons\BTNBasicStruct.blp",
        "nightelf" => @"ReplaceableTextures\CommandButtons\BTNNightElfBuild.blp",
        _ => @"ReplaceableTextures\CommandButtons\BTNHumanBuild.blp",
    };

    internal static bool TryGetConfiguredSlot(Ability ability, bool research, out int slot, bool reverse = false)
    {
        slot = -1;
        if (ability?.Type == null) return false;
        return NativeCommandPosition.TryRead(field => ability.Type.CommandField(field),
            research ? "Researchbuttonpos" : reverse ? "Unbuttonpos" : "Buttonpos",
            research ? "arpx" : reverse ? "aubx" : "abpx",
            research ? "arpy" : reverse ? "auby" : "abpy", out slot);
    }

    internal static bool TryGetUnitConfiguredSlot(UnitType data, out int slot)
    {
        slot = -1;
        if (data == null) return false;
        return NativeCommandPosition.TryRead(field => TechTree.UnitCommandField(data.Id, field),
            "Buttonpos", "ubpx", "ubpy", out slot, "ButtonposX", "ButtonposY");
    }

    internal static bool TryGetUpgradeConfiguredSlot(UpgradeType data, out int slot)
    {
        slot = -1;
        if (data == null) return false;
        return NativeCommandPosition.TryRead(_ => data.Buttonpos,
            "Buttonpos", "gbpx", "gbpy", out slot, "ButtonposX", "ButtonposY");
    }

    /// <summary>学习页 / 建造页的第 12 格：BTNCancel + Esc，点了退回上一页。</summary>
    internal static CommandEntry BackEntry(Action back) => new()
    {
        Id = "cmd-back",
        Name = "返回",
        Kind = UiCommandKind.Back,
        SlotIndex = Slots - 1,
        Hotkey = "Esc",
        Icon = TechTree.Icon(@"ReplaceableTextures\CommandButtons\BTNCancel.blp"),
        Action = back,
    };

    /// <summary>
    /// 学习技能页（客户端 CollectLearnMenu）：按物编 Researchbuttonpos 排位，
    /// 文案/图标用 Researchtip / Researchubertip / Researchart，第 12 格「返回」。
    /// </summary>
    internal static List<CommandEntry> CollectLearnMenu(Unit hero, Action<Ability> learn, Action back)
    {
        var commands = new List<CommandEntry> { BackEntry(back) };
        if (hero?.Type == null) return commands;

        foreach (string id in hero.Type.HeroAbilList)
        {
            Ability ability = hero.FindAbility(id) ?? hero.GiveAbility(id, 0);
            if (ability?.Type == null) continue;
            if (!ability.CanLevelUp) continue;
            if (!TryGetConfiguredSlot(ability, research: true, out int slot)) continue;

            AbilityType type = ability.Type;
            int need = LearnLevelRequirement(ability);
            bool levelOk = hero.Level >= need;
            bool hasPoint = hero.SkillPoints > 0;
            string title = LearnText(string.IsNullOrWhiteSpace(type.ResearchTip) ? type.Name : type.ResearchTip, ability);
            string body = LearnText(string.IsNullOrWhiteSpace(type.ResearchUbertip) ? type.Ubertip : type.ResearchUbertip, ability);
            string hotkey = string.IsNullOrWhiteSpace(type.ResearchHotkey) ? ability.Hotkey : type.ResearchHotkey;

            commands.Add(new CommandEntry
            {
                Id = ability.Id,
                Name = title,
                Kind = UiCommandKind.LearnSkill,
                Payload = ability.Id,
                SlotIndex = slot,
                Hotkey = hotkey,
                Tooltip = (body ?? "") + (levelOk ? "" : $"\n需要英雄等级 {need}"),
                Icon = TechTree.Icon(type.ResearchArt),
                Enabled = levelOk && hasPoint,
                Action = () => learn(ability),
            });
        }

        return commands;
    }

    /// <summary>英雄下一级技能要求的英雄等级（物编 reqLevel / levelSkip）。</summary>
    internal static int LearnLevelRequirement(Ability ability)
    {
        int nextLevel = ability.Level + 1;
        return (nextLevel - 1) * Math.Max(1, ability.Type.LevelSkip) + Math.Max(1, ability.Type.RequiredLevel);
    }

    /// <summary>学习页文案：物编里的 %d 换成技能等级，颜色码/引号按客户端 GetLevelString 同口径处理。</summary>
    private static string LearnText(string raw, Ability ability)
        => War3Text.Describe((raw ?? "").Replace("%d", Math.Max(1, ability?.Level ?? 1).ToString()));

    /// <summary>
    /// 建造页（客户端 CollectBuildMenu）：可造建筑按物编 Buttonpos 铺开，
    /// 12 格放得下多族建筑；第 12 格「返回」。
    /// </summary>
    internal static List<CommandEntry> CollectBuildMenu(Unit worker, Func<UnitType, bool> canBuild,
        Action<string> place, Action back)
    {
        var commands = new List<CommandEntry> { BackEntry(back) };
        if (worker?.Type == null) return commands;

        foreach (string rawcode in worker.Type.Builds)
        {
            UnitType type = UnitFactory.Get(rawcode);
            if (type == null || !type.IsBuilding) continue;
            if (!TryGetUnitConfiguredSlot(type, out int slot)) continue;

            string id = rawcode;
            string cost = type.GoldCost > 0 ? $"{type.GoldCost}金" : "";
            if (type.LumberCost > 0) cost += (cost.Length > 0 ? "/" : "") + $"{type.LumberCost}木";
            if (type.FoodUsed > 0) cost += (cost.Length > 0 ? " " : "") + $"{type.FoodUsed}食";

            commands.Add(new CommandEntry
            {
                Id = id,
                Name = type.Name,
                Kind = UiCommandKind.Build,
                Payload = id,
                SlotIndex = slot,
                Hotkey = TechTree.UnitHotkey(id),
                Cost = cost,
                Tooltip = type.Tip,
                Icon = TechTree.Icon(TechTree.UnitIconArt(id)),
                Enabled = canBuild?.Invoke(type) ?? true,
                Action = () => place(id),
            });
        }

        return commands;
    }
}
