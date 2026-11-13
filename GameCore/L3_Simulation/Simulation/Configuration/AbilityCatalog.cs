using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

/// <summary>
/// 技能类型（静态数据）。数据来自 Units\AbilityData.slk（`UnitsAbilityData`），
/// 名字/提示来自 Units\*AbilityStrings。对标客户端 Simulation/Configuration 的
/// ObjectDataAbility + RuntimeObjectNumbers：全部数值按等级从表里读，不写死。
/// </summary>
public sealed class AbilityType
{
    public const int MaxLevels = 4;

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
    private const string Table = "UnitsAbilityData";

    private static readonly string[] StringFiles =
    {
        "UnitsHumanAbilityStrings.json",
        "UnitsOrcAbilityStrings.json",
        "UnitsUndeadAbilityStrings.json",
        "UnitsNightElfAbilityStrings.json",
        "UnitsNeutralAbilityStrings.json",
        "UnitsCampaignAbilityStrings.json",
        "UnitsCommonAbilityStrings.json",
        "UnitsItemAbilityStrings.json",
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
    private static readonly Dictionary<string, int> Rows = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> _strings;
    private static bool _indexed;

    public static int Count
    {
        get
        {
            EnsureIndex();
            return Rows.Count;
        }
    }

    private static void EnsureIndex()
    {
        if (_indexed) return;
        _indexed = true;

        SlkTable table = War3SlkReader.Get(Table);
        if (table == null)
        {
            GD.Print("[ABILITY] 缺少 UnitsAbilityData，技能系统不可用");
            return;
        }

        foreach (int row in table.DataRows)
        {
            string id = table.Get(row, "alias");
            if (string.IsNullOrWhiteSpace(id)) continue;
            Rows[id.Trim()] = row;
        }
        GD.Print($"[ABILITY] 技能索引 {Rows.Count} 条");
    }

    public static bool Exists(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        EnsureIndex();
        return Rows.ContainsKey(id);
    }

    public static AbilityType Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out AbilityType cached)) return cached;
        EnsureIndex();
        if (!Rows.TryGetValue(id, out int row)) return null;

        SlkTable table = War3SlkReader.Get(Table);
        var type = new AbilityType
        {
            Id = id,
            Code = (table.Get(row, "code") ?? "").Trim(),
            MaxLevel = Mathf.Max(1, table.GetInt(row, "levels", 1)),
            RequiredLevel = Mathf.Max(1, table.GetInt(row, "reqLevel", 1)),
            LevelSkip = Mathf.Max(1, table.GetInt(row, "levelSkip", 1)),
            HeroAbility = table.Get(row, "hero") == "1",
            ItemAbility = table.Get(row, "item") == "1",
            TargetRules = table.Get(row, "targs1"),
            Targets = SpellTargetCode.Parse(table.Get(row, "targs1")),
        };

        for (int level = 1; level <= AbilityType.MaxLevels; level++)
        {
            int index = level - 1;
            type.Cooldown[index] = Number(table.Get(row, "Cool" + level), 0f);
            type.ManaCost[index] = Number(table.Get(row, "Cost" + level), 0f);
            type.CastRange[index] = Number(table.Get(row, "Rng" + level), 0f);
            type.Area[index] = Number(table.Get(row, "Area" + level), 0f);
            type.Duration[index] = Number(table.Get(row, "Dur" + level), 0f);
            type.HeroDuration[index] = Number(table.Get(row, "HeroDur" + level), 0f);
            type.Cast[index] = Number(table.Get(row, "Cast" + level), 0f);
            type.SummonId[index] = (table.Get(row, "UnitID" + level) ?? "").Trim();
            type.BuffId[index] = (table.Get(row, "BuffID" + level) ?? "").Trim();

            for (int slot = 0; slot < 9; slot++)
            {
                char letter = (char)('A' + slot);
                string raw = table.Get(row, "Data" + letter + level) ?? "";
                type.Data[index, slot] = Number(raw, 0f);
                type.DataText[index, slot] = raw.Trim();
            }
        }

        Dictionary<string, string> strings = Strings();
        type.Name = Pick(strings, id + ".Name");
        type.Tip = Pick(strings, id + ".Tip");
        type.Ubertip = Pick(strings, id + ".Ubertip");
        if (string.IsNullOrEmpty(type.Ubertip)) type.Ubertip = Pick(strings, id + ".UberTip");
        type.Hotkey = Pick(strings, id + ".Hotkey");

        if (string.IsNullOrEmpty(type.Name)) type.Name = id;
        if (IconFallback.TryGetValue(id, out string icon)) type.IconArt = icon;
        else if (IconFallback.TryGetValue(type.Code, out string codeIcon)) type.IconArt = codeIcon;

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

    private static Dictionary<string, string> Strings()
    {
        if (_strings != null) return _strings;

        _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in StringFiles)
        {
            Variant json = GeneratedTexts.Get(file);
            if (json.VariantType != Variant.Type.Dictionary) continue;

            Godot.Collections.Dictionary root = json.AsGodotDictionary();
            if (!root.ContainsKey("keys")) continue;

            Godot.Collections.Dictionary keys = root["keys"].AsGodotDictionary();
            foreach (KeyValuePair<Variant, Variant> pair in keys)
            {
                string key = pair.Key.AsString();
                if (!_strings.ContainsKey(key)) _strings[key] = pair.Value.AsString();
            }
        }
        return _strings;
    }

    private static string Pick(Dictionary<string, string> map, string key)
    {
        return map.TryGetValue(key, out string value) ? value : "";
    }

    private static float Number(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "_") return fallback;
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }
}
