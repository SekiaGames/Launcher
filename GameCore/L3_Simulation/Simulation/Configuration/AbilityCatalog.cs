using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 技能类型（静态数据）。字段全部来自技能物编合并（War3ObjectData）：
/// Units\AbilityData.slk 的结构列与等级列，加上 Units\*AbilityFunc/Strings 的
/// 名字、提示、热键、美术与命令卡位置。对标客户端 ObjectDataAbility。
/// </summary>
public sealed class AbilityType
{
    public const int MaxLevels = 4;

    /// <summary>技能物编：AbilityData.slk 的结构列 + 16 张技能文本表的全部字段。</summary>
    public AbilityTypeData ObjectData;

    public string Id;
    /// <summary>技能基类码（alias 的 code 列）。自定义技能用它找实现。</summary>
    public string Code;
    public string Name = "";
    public string Tip = "";
    public string Ubertip = "";
    public string Hotkey = "";

    public int MaxLevel;
    public int RequiredLevel;
    /// <summary>每几级英雄等级可以升一级（物编 levelSkip）。</summary>
    public int LevelSkip = 1;
    public bool HeroAbility;
    public bool ItemAbility;

    public SpellTargets Targets = SpellTargets.None;

    /// <summary>物编 targs1 原始串（客户端 ParseTargets 读的就是它）。</summary>
    public string TargetRules = "";

    public readonly float[] Cooldown = new float[MaxLevels];
    public readonly float[] ManaCost = new float[MaxLevels];
    public readonly float[] CastRange = new float[MaxLevels];
    public readonly float[] Area = new float[MaxLevels];
    public readonly float[] Duration = new float[MaxLevels];
    /// <summary>物编 HeroDur 列：英雄身上的持续时间（变身 60s、宁静/星落这类也是它）。</summary>
    public readonly float[] HeroDuration = new float[MaxLevels];
    /// <summary>物编 Cast 列：暗影突袭的持续伤害间隔就写在这里。</summary>
    public readonly float[] Cast = new float[MaxLevels];
    /// <summary>DataA..DataI，每级 9 个。</summary>
    public readonly float[,] Data = new float[MaxLevels, 9];
    /// <summary>DataA..DataI 的原始文本：DataC 存单位 id 这类非数字值时用它。</summary>
    public readonly string[,] DataText = new string[MaxLevels, 9];
    public readonly string[] SummonId = new string[MaxLevels];
    public readonly string[] BuffId = new string[MaxLevels];

    /// <summary>终极技能：英雄技能且只有一级（天神下凡 / 复活死尸 / 亡灵复生）。</summary>
    public bool Ultimate => HeroAbility && MaxLevel <= 1;

    public string IconArt = "";

    /// <summary>学习页的图标（物编 Researchart）。</summary>
    public string ResearchArt = "";
    public string ResearchHotkey = "";
    public string ResearchTip = "";
    public string ResearchUbertip = "";

    /// <summary>
    /// 命令卡位置字段（客户端的 ability ObjectData 同口径）：Buttonpos / Unbuttonpos /
    /// Researchbuttonpos 及拆开的 abpx·abpy / aubx·auby / arpx·arpy。
    /// 数据来自 Units\*AbilityFunc.txt。
    /// </summary>
    public readonly Dictionary<string, string> CommandFields = new(StringComparer.OrdinalIgnoreCase);

    public string CommandField(string name)
        => name != null && CommandFields.TryGetValue(name, out string value) ? value : null;

    public float CooldownAt(int level) => Pick(Cooldown, level);
    public float CostAt(int level) => Pick(ManaCost, level);
    public float RangeAt(int level) => Pick(CastRange, level);
    public float AreaAt(int level) => Pick(Area, level);
    public float DurationAt(int level) => Pick(Duration, level);
    public float HeroDurationAt(int level) => Pick(HeroDuration, level);
    public float CastAt(int level) => Pick(Cast, level);

    /// <summary>Data 槽：'A'..'I'。</summary>
    public float DataAt(int level, char slot)
    {
        int index = char.ToUpperInvariant(slot) - 'A';
        if (index < 0 || index >= 9) return 0f;
        int row = Mathf.Clamp(level, 1, MaxLevels) - 1;
        return Data[row, index];
    }

    public string SummonAt(int level)
    {
        int index = Mathf.Clamp(level, 1, MaxLevels) - 1;
        return SummonId[index];
    }

    /// <summary>Data 槽的原始文本（'A'..'I'）。</summary>
    public string DataTextAt(int level, char slot)
    {
        int index = char.ToUpperInvariant(slot) - 'A';
        if (index < 0 || index >= 9) return "";
        int row = Mathf.Clamp(level, 1, MaxLevels) - 1;
        return DataText[row, index] ?? "";
    }

    private static float Pick(float[] values, int level)
    {
        if (values == null || values.Length == 0) return 0f;
        int index = Mathf.Clamp(level, 1, values.Length) - 1;
        return values[index];
    }
}

public static class AbilityCatalog
{
    private static readonly (string Field, string SplitX, string SplitY)[] CommandPositionFields =
    {
        ("Buttonpos", "abpx", "abpy"),
        ("Unbuttonpos", "aubx", "auby"),
        ("Researchbuttonpos", "arpx", "arpy"),
    };

    /// <summary>物编的 Art 列没导出，常用技能的图标按原版路径补齐；缺了只是没图标。</summary>
    private static readonly Dictionary<string, string> IconFallback = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AHhb"] = @"ReplaceableTextures\CommandButtons\BTNHolyBolt",
        ["AHtb"] = @"ReplaceableTextures\CommandButtons\BTNGolemStormBolt",
        ["AHtc"] = @"ReplaceableTextures\CommandButtons\BTNGolemThunderClap",
        ["AHbh"] = @"ReplaceableTextures\CommandButtons\BTNBash",
        ["AOcr"] = @"ReplaceableTextures\CommandButtons\BTNCriticalStrike",
        ["AHad"] = @"ReplaceableTextures\CommandButtons\BTNDevotion",
        ["AHds"] = @"ReplaceableTextures\CommandButtons\BTNDivineShieldOff",
        ["AHav"] = @"ReplaceableTextures\CommandButtons\BTNAvatar",
        ["AInv"] = @"ReplaceableTextures\CommandButtons\BTNSelectHeroOn",
        ["AHre"] = @"ReplaceableTextures\CommandButtons\BTNResurrection",
        ["AHbz"] = @"ReplaceableTextures\CommandButtons\BTNBlizzard",
        ["AUdd"] = @"ReplaceableTextures\CommandButtons\BTNDeathAndDecay",
        ["AOcl"] = @"ReplaceableTextures\CommandButtons\BTNChainLightning",
        ["AHbl"] = @"ReplaceableTextures\CommandButtons\BTNBlink",
        ["ANsi"] = @"ReplaceableTextures\CommandButtons\BTNSilence",
        ["AEev"] = @"ReplaceableTextures\CommandButtons\BTNEvasion",
        ["AUfa"] = @"ReplaceableTextures\CommandButtons\BTNFrostArmor",
        ["AUdc"] = @"ReplaceableTextures\CommandButtons\BTNDeathCoil",
        ["AUdp"] = @"ReplaceableTextures\CommandButtons\BTNDeathPact",
        ["AUdr"] = @"ReplaceableTextures\CommandButtons\BTNDarkRitual",
        ["AUau"] = @"ReplaceableTextures\CommandButtons\BTNUnholyAura",
        ["AUan"] = @"ReplaceableTextures\CommandButtons\BTNAnimateDead",
        ["AUfn"] = @"ReplaceableTextures\CommandButtons\BTNFrostBolt",
        ["AUcs"] = @"ReplaceableTextures\CommandButtons\BTNCarrionSwarm",
        ["AUsl"] = @"ReplaceableTextures\CommandButtons\BTNSleep",
        ["AUav"] = @"ReplaceableTextures\CommandButtons\BTNVampiricAura",
        ["AUin"] = @"ReplaceableTextures\CommandButtons\BTNInfernal",
        ["AUim"] = @"ReplaceableTextures\CommandButtons\BTNImpale",
        ["AUts"] = @"ReplaceableTextures\CommandButtons\BTNCreatureCarapace",
        ["AUcb"] = @"ReplaceableTextures\CommandButtons\BTNCarrionScarabs",
        ["AOsh"] = @"ReplaceableTextures\CommandButtons\BTNShockwave",
        ["AOws"] = @"ReplaceableTextures\CommandButtons\BTNWarStomp",
        ["AOae"] = @"ReplaceableTextures\CommandButtons\BTNGnollCommandAura",
        ["AOhw"] = @"ReplaceableTextures\CommandButtons\BTNHealingWave",
        ["AOsw"] = @"ReplaceableTextures\CommandButtons\BTNSerpentWard",
        ["AOhx"] = @"ReplaceableTextures\CommandButtons\BTNHex",
        ["Aply"] = @"ReplaceableTextures\CommandButtons\BTNPolymorph",
        ["AOvd"] = @"ReplaceableTextures\CommandButtons\BTNBigBadVoodooSpell",
        ["AEme"] = @"ReplaceableTextures\CommandButtons\BTNMetamorphosis",
        ["AEtq"] = @"ReplaceableTextures\CommandButtons\BTNTranquility",
        ["AEsf"] = @"ReplaceableTextures\CommandButtons\BTNStarfall",
        ["AOww"] = @"ReplaceableTextures\CommandButtons\BTNWhirlwind",
        ["AEer"] = @"ReplaceableTextures\CommandButtons\BTNEntanglingRoots",
        ["AEah"] = @"ReplaceableTextures\CommandButtons\BTNThorns",
        ["AEar"] = @"ReplaceableTextures\CommandButtons\BTNTrueShot",
        ["AEmb"] = @"ReplaceableTextures\CommandButtons\BTNManaBurn",
        ["AEim"] = @"ReplaceableTextures\CommandButtons\BTNImmolationOn",
        ["AEfk"] = @"ReplaceableTextures\CommandButtons\BTNFanOfKnives",
        ["AEsh"] = @"ReplaceableTextures\CommandButtons\BTNShadowStrike",
        ["AHab"] = @"ReplaceableTextures\CommandButtons\BTNBrilliance",
        ["AHwe"] = @"ReplaceableTextures\CommandButtons\BTNSummonWaterElemental",
        ["AEfn"] = @"ReplaceableTextures\CommandButtons\BTNForceOfNature",
        ["AEst"] = @"ReplaceableTextures\CommandButtons\BTNScout",
        ["AOsf"] = @"ReplaceableTextures\CommandButtons\BTNSpiritWolf",
    };

    private static readonly Dictionary<string, AbilityType> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static bool _indexed;
    private static int _count;

    public static int Count
    {
        get
        {
            EnsureIndex();
            return _count;
        }
    }

    private static void EnsureIndex()
    {
        if (_indexed) return;
        _indexed = true;
        _count = 0;
        foreach (string id in War3ObjectData.TextAbilityIds)
        {
            if (War3ObjectData.GetAbility(id) != null) _count++;
        }
        GD.Print($"[ABILITY] 技能索引 {_count} 条");
    }

    public static bool Exists(string id) => Get(id) != null;

    public static AbilityType Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out AbilityType cached)) return cached;
        AbilityTypeData data = War3ObjectData.GetAbility(id);
        if (data == null) return null;

        var type = new AbilityType
        {
            Id = id,
            ObjectData = data,
            Code = Text(data, "code"),
            MaxLevel = Mathf.Max(1, data.GetInt("levels", 1, 0, 1)),
            RequiredLevel = Mathf.Max(1, data.GetInt("reqLevel", 1, 0, 1)),
            LevelSkip = Mathf.Max(1, data.GetInt("levelSkip", 1, 0, 1)),
            HeroAbility = Text(data, "hero") == "1",
            ItemAbility = Text(data, "item") == "1",
            TargetRules = data.GetString("targs", 1, 0, ""),
            Targets = SpellTargetCode.Parse(data.GetString("targs", 1, 0, "")),
        };

        for (int level = 1; level <= AbilityType.MaxLevels; level++)
        {
            int index = level - 1;
            type.Cooldown[index] = data.GetFloat("Cool", level, 0, 0f);
            type.ManaCost[index] = data.GetFloat("Cost", level, 0, 0f);
            type.CastRange[index] = data.GetFloat("Rng", level, 0, 0f);
            type.Area[index] = data.GetFloat("Area", level, 0, 0f);
            type.Duration[index] = data.GetFloat("Dur", level, 0, 0f);
            type.HeroDuration[index] = data.GetFloat("HeroDur", level, 0, 0f);
            type.Cast[index] = data.GetFloat("Cast", level, 0, 0f);
            type.SummonId[index] = Text(data, "UnitID", level);
            type.BuffId[index] = Text(data, "BuffID", level);

            for (int slot = 0; slot < 9; slot++)
            {
                string raw = Text(data, "Data", level, slot + 1);
                type.Data[index, slot] = RuntimeObjectNumbers.ReadFloat(raw, 0f);
                type.DataText[index, slot] = raw.Trim();
            }
        }

        type.Name = Text(data, "Name");
        type.Tip = Text(data, "Tip");
        type.Ubertip = Text(data, "Ubertip");
        if (string.IsNullOrEmpty(type.Ubertip)) type.Ubertip = Text(data, "UberTip");
        type.Hotkey = Text(data, "Hotkey");
        type.ResearchArt = Text(data, "Researchart");
        type.ResearchHotkey = Text(data, "Researchhotkey");
        type.ResearchTip = Text(data, "Researchtip");
        type.ResearchUbertip = Text(data, "Researchubertip");

        foreach ((string field, string splitX, string splitY) in CommandPositionFields)
        {
            string raw = Text(data, field);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            type.CommandFields[field] = raw;
            string[] parts = raw.Split(',');
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) type.CommandFields[splitX] = parts[0].Trim();
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) type.CommandFields[splitY] = parts[1].Trim();
        }

        if (string.IsNullOrEmpty(type.Name)) type.Name = id;
        type.IconArt = Text(data, "Art");
        if (string.IsNullOrWhiteSpace(type.IconArt) || type.IconArt is "_" or "-")
        {
            if (IconFallback.TryGetValue(id, out string icon)) type.IconArt = icon;
            else if (IconFallback.TryGetValue(type.Code, out string codeIcon)) type.IconArt = codeIcon;
            else type.IconArt = "";
        }
        if (string.IsNullOrWhiteSpace(type.ResearchArt) || type.ResearchArt is "_" or "-")
            type.ResearchArt = type.IconArt;

        Cache[id] = type;
        return type;
    }

    /// <summary>单位技能列表：heroAbilList 是可学技能（等级从 0 开始），abilList 是自带技能。</summary>
    public static string[] SplitList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "_") return Array.Empty<string>();

        var list = new List<string>();
        foreach (string part in raw.Split(',', ';'))
        {
            string id = part.Trim();
            if (id.Length < 4) continue;
            list.Add(id[..4]);
        }
        return list.ToArray();
    }

    static string Text(IRuntimeObjectData data, string field, int level = 0, int dataPointer = 0)
    {
        string value = level > 0
            ? data.GetString(field, level, dataPointer, "")
            : data.GetString(field, "");
        if (value == null) return "";
        value = value.Trim();
        return value == "_" || value == "-" ? "" : value;
    }
}
