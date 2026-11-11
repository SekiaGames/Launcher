using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace Launcher;

public sealed class UnitType
{
    public string Id;
    public string Name;
    public string Tip;
    public string IconPath;
    public string ModelPath;
    public float ModelScale = 1f;
    public float Hp = 100f;
    public float Speed = 270f;
    public bool IsBuilding;
    public bool CanAttack;
    public float AttackRange = 100f;
    public float AttackCooldown = 1.5f;
    public float MinDamage = 5f;
    public float MaxDamage = 10f;
    public float AcquireRange = 500f;
    public float Armor;
    public int GoldCost;
    public int LumberCost;
    public float TrainSeconds = 15f;
    public float BuildSeconds = 30f;
    public int FoodUsed;
    public int FoodMade;

    // —— 战斗结算字段（UnitWeapons.atkType1 + UnitBalance.defType/collision/bounty*） ——
    public AttackType AttackType = AttackType.Normal;
    public DefenseType DefenseType = DefenseType.Unarmored;
    /// <summary>本单位能打的目标类别（UnitWeapons.targs1）。</summary>
    public TargetKind Targets = TargetKind.Ground | TargetKind.Structure;
    /// <summary>UnitData.targType，本单位被索敌时匹配对方的 Targets。</summary>
    public TargetKind Kind = TargetKind.Ground;
    /// <summary>UnitBalance.collision，真实碰撞/推挤半径（不是 footprint）。</summary>
    public float CollisionRadius = 16f;
    /// <summary>UnitData.movetp=fly，空中单位不吃地面路径阻挡。</summary>
    public bool Flying;

    // —— 攻击时序（UnitWeapons.castpt/dmgpt1/backSw1） ——
    /// <summary>起手到伤害点的秒数。</summary>
    public float DamagePoint = 0.5f;
    /// <summary>伤害点之后的后摇秒数。</summary>
    public float Backswing = 0.5f;

    // —— 溅射（UnitWeapons.Farea1/Harea1/Qarea1/targCount1，三档：全伤/半伤/四分之一） ——
    public float SplashFull;
    public float SplashHalf;
    public float SplashQuarter;
    public int SplashTargets = 1;

    public int Level = 1;
    public int BountyDice = 1;
    public int BountySides;
    public int BountyPlus;

    public string[] Trains = Array.Empty<string>();
    public string[] Builds = Array.Empty<string>();
    public string[] Researches = Array.Empty<string>();
    public string[] UpgradeTo = Array.Empty<string>();
    public string[] Requires = Array.Empty<string>();
    public string[] UpgradesApplied = Array.Empty<string>();
    public string[] Abilities = Array.Empty<string>();
    public string SoundSet = "";
    /// <summary>UnitWeapons.weapType1，如 MetalMediumSlice（战斗音效前缀）。</summary>
    public string WeaponSound = "";

    // —— 英雄字段（UnitBalance 的 STR/AGI/INT + 成长 + Primary） ——
    public bool IsHero;
    public int Strength;
    public int Agility;
    public int Intelligence;
    public float StrengthGain;
    public float AgilityGain;
    public float IntelligenceGain;
    public string Primary = "";
    public float ManaRegen;
    public float SightDay = 1400f;
    /// <summary>转身角速度（弧度/秒）。UnitData.turnRate 是原版字段，这里按每 tick 0.1 的量纲折算。</summary>
    public float TurnSpeed = 3.6f;
    public float SightNight = 800f;
    public int FootprintCells = 2;
    public string MissileScene;
    public float MissileSpeed = 900f;
    public float MissileArc;

    public bool IsWorkerType => Builds.Length > 0;
    public bool CanGatherGold => HasAbility("Ahar") || HasAbility("Awha") || HasAbility("Aaha");
    public bool EntersMine => HasAbility("Aaha");
    public float FootprintRadius => FootprintCells * 32f * 0.5f;
    public bool CanGatherLumber => HasAbility("Ahar") || HasAbility("Awha") || HasAbility("Ahrl");
    /// <summary>能打到某类目标：本地图/表未给出掩码时按全地面处理，避免误判成不能还手。</summary>
    public bool CanTarget(TargetKind kind) => Targets == TargetKind.None || (Targets & kind) != 0;

    private bool HasAbility(string code)
    {
        foreach (string ability in Abilities)
        {
            if (string.Equals(ability, code, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

public static class UnitCatalog
{
    private static readonly Dictionary<string, UnitType> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> UiRows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> BalanceRows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> WeaponRows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> AbilityRows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> DataRows = new(StringComparer.OrdinalIgnoreCase);
    private static bool _indexed;

    private static void EnsureIndex()
    {
        if (_indexed) return;
        _indexed = true;
        Index(War3Tables.UnitUI(), "unitUIID", UiRows);
        Index(War3Tables.UnitBalance(), "unitBalanceID", BalanceRows);
        Index(War3Tables.UnitWeapons(), "unitWeapID", WeaponRows);
        Index(War3Tables.UnitAbilities(), "unitAbilID", AbilityRows);
        Index(War3Tables.UnitData(), "unitID", DataRows);
        GD.Print($"[UNIT] 索引 unitUI={UiRows.Count} balance={BalanceRows.Count} weapons={WeaponRows.Count} abilities={AbilityRows.Count}");
    }

    private static void Index(SlkTable table, string column, Dictionary<string, int> target)
    {
        foreach (int row in table.DataRows)
        {
            string id = table.Get(row, column);
            if (string.IsNullOrWhiteSpace(id)) continue;
            target[id.Trim()] = row;
        }
    }

    public static UnitType Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (Cache.TryGetValue(id, out UnitType cached)) return cached;
        EnsureIndex();

        UnitType type = Build(id);
        Cache[id] = type;
        return type;
    }

    private static UnitType Build(string id)
    {
        if (!UiRows.TryGetValue(id, out int uiRow)) return null;

        SlkTable ui = War3Tables.UnitUI();
        string file = ui.Get(uiRow, "file");
        if (string.IsNullOrWhiteSpace(file)) return null;

        var type = new UnitType
        {
            Id = id,
            ModelPath = PathResolve.ModelScene(file),
            ModelScale = Number(ui.Get(uiRow, "modelScale"), 1f),
            Name = file.Replace('\\', '/').Split('/')[^1],
            SoundSet = ui.Get(uiRow, "unitSound") ?? "",
        };

        if (BalanceRows.TryGetValue(id, out int balanceRow))
        {
            SlkTable balance = War3Tables.UnitBalance();
            type.Hp = Number(balance.Get(balanceRow, "HP"), 100f);
            type.Speed = Number(balance.Get(balanceRow, "spd"), 270f);
            type.SightDay = Number(balance.Get(balanceRow, "sight"), 1400f);

            type.Primary = (balance.Get(balanceRow, "Primary") ?? "").Trim();
            type.IsHero = type.Primary is "STR" or "AGI" or "INT";
            if (type.IsHero)
            {
                type.Strength = (int)Number(balance.Get(balanceRow, "STR"), 0f);
                type.Agility = (int)Number(balance.Get(balanceRow, "AGI"), 0f);
                type.Intelligence = (int)Number(balance.Get(balanceRow, "INT"), 0f);
                type.StrengthGain = Number(balance.Get(balanceRow, "STRplus"), 0f);
                type.AgilityGain = Number(balance.Get(balanceRow, "AGIplus"), 0f);
                type.IntelligenceGain = Number(balance.Get(balanceRow, "INTplus"), 0f);
                type.ManaRegen = Number(balance.Get(balanceRow, "regenMana"), 0f);
            }
            if (DataRows.TryGetValue(id, out int turnRow) && !type.IsBuilding)
            {
                float turnRate = Number(War3Tables.UnitData().Get(turnRow, "turnRate"), 0f);
                if (turnRate > 0f) type.TurnSpeed = Mathf.Clamp(turnRate * 6f, 1.5f, 14f);
            }
            type.SightNight = Number(balance.Get(balanceRow, "nsight"), 800f);
            type.Armor = Number(balance.Get(balanceRow, "def"), 0f);
            type.DefenseType = CombatCodes.ParseDefense(balance.Get(balanceRow, "defType"));
            float collision = Number(balance.Get(balanceRow, "collision"), 0f);
            if (collision > 0f) type.CollisionRadius = collision;
            type.Level = Mathf.Max(1, balance.GetInt(balanceRow, "level", 1));
            type.BountyDice = Mathf.Max(1, balance.GetInt(balanceRow, "bountydice", 1));
            type.BountySides = Mathf.Max(0, balance.GetInt(balanceRow, "bountysides"));
            type.BountyPlus = Mathf.Max(0, balance.GetInt(balanceRow, "bountyplus"));
            type.IsBuilding = balance.Get(balanceRow, "isbldg") == "1";
            type.GoldCost = balance.GetInt(balanceRow, "goldcost");
            type.LumberCost = balance.GetInt(balanceRow, "lumbercost");
            type.TrainSeconds = Number(balance.Get(balanceRow, "reptm"), 15f);
            type.BuildSeconds = Number(balance.Get(balanceRow, "bldtm"), 30f);
            type.FoodUsed = Mathf.Max(0, balance.GetInt(balanceRow, "fused"));
            type.FoodMade = Mathf.Max(0, balance.GetInt(balanceRow, "fmade"));
            type.UpgradesApplied = SplitList(balance.Get(balanceRow, "upgrades"));
            string comment = balance.Get(balanceRow, "comment(s)");
            if (!string.IsNullOrWhiteSpace(comment)) type.Name = comment.Trim();
        }

        if (WeaponRows.TryGetValue(id, out int weaponRow))
        {
            SlkTable weapons = War3Tables.UnitWeapons();
            string weaponSound = weapons.Get(weaponRow, "weapType1")?.Trim();
            if (!string.IsNullOrEmpty(weaponSound) && weaponSound != "_" && weaponSound != "-")
            {
                type.WeaponSound = weaponSound;
            }

            string kind = weapons.Get(weaponRow, "weapTp1")?.Trim();
            type.CanAttack = !string.IsNullOrEmpty(kind)
                && kind != "_" && kind != "-"
                && !kind.Equals("none", StringComparison.OrdinalIgnoreCase);
            type.AttackRange = Number(weapons.Get(weaponRow, "rangeN1"), 90f);
            type.AttackCooldown = Number(weapons.Get(weaponRow, "cool1"), 1.5f);
            type.MinDamage = Number(weapons.Get(weaponRow, "mindmg1"), 5f);
            type.MaxDamage = Number(weapons.Get(weaponRow, "maxdmg1"), 10f);
            type.AcquireRange = Number(weapons.Get(weaponRow, "acquire"), 500f);
            type.AttackType = CombatCodes.ParseAttack(weapons.Get(weaponRow, "atkType1"));
            type.DamagePoint = Number(weapons.Get(weaponRow, "dmgpt1"), 0.5f);
            type.Backswing = Number(weapons.Get(weaponRow, "backSw1"), 0.5f);
            type.SplashFull = Number(weapons.Get(weaponRow, "Farea1"), 0f);
            type.SplashHalf = Number(weapons.Get(weaponRow, "Harea1"), 0f);
            type.SplashQuarter = Number(weapons.Get(weaponRow, "Qarea1"), 0f);
            type.SplashTargets = Mathf.Max(1, weapons.GetInt(weaponRow, "targCount1", 1));
            TargetKind targets = CombatCodes.ParseTargets(weapons.Get(weaponRow, "targs1"));
            if (targets != TargetKind.None) type.Targets = targets;
        }

        if (DataRows.TryGetValue(id, out int dataRow))
        {
            SlkTable data = War3Tables.UnitData();
            type.FootprintCells = FootprintFromPathTexture(data.Get(dataRow, "pathTex"), type.FootprintCells);
            TargetKind kind = CombatCodes.ParseTargets(data.Get(dataRow, "targType"));
            if (kind == TargetKind.None) kind = type.IsBuilding ? TargetKind.Structure : TargetKind.Ground;
            // 建筑同时具备「地面」身份：迫击炮/投石车的 targs1 只写 ground 不写 structure，原版照样能砸房子。
            if (type.IsBuilding) kind |= TargetKind.Ground;
            type.Kind = kind;
            string moveType = (data.Get(dataRow, "movetp") ?? "").Trim();
            type.Flying = string.Equals(moveType, "fly", StringComparison.OrdinalIgnoreCase);
        }

        if (AbilityRows.TryGetValue(id, out int abilityRow))
        {
            SlkTable abilities = War3Tables.UnitAbilities();
            type.Abilities = SplitList(abilities.Get(abilityRow, "abilList"));
        }

        UnitFuncRow row = TechTree.Row(id);
        if (row != null)
        {
            type.Trains = row.Trains;
            type.Builds = row.Builds;
            type.Researches = row.Researches;
            type.UpgradeTo = row.UpgradeTo;
            type.Requires = row.Requires;
            type.MissileScene = PathResolve.ModelScene(row.MissileArt);
            type.MissileArc = row.MissileArc;
            if (row.MissileSpeed > 0f) type.MissileSpeed = row.MissileSpeed;
        }

        string localized = TechTree.UnitName(id);
        if (!string.IsNullOrWhiteSpace(localized)) type.Name = localized;
        type.Tip = TechTree.UnitTip(id);
        type.IconPath = PathResolve.IconPng(TechTree.UnitIconArt(id));

        return type;
    }

    private static string[] SplitList(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();
        var list = new List<string>();
        foreach (string part in csv.Split(',', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(part) || part == "_") continue;
            list.Add(part);
        }
        return list.ToArray();
    }

    private static int FootprintFromPathTexture(string pathTexture, int fallback)
    {
        if (string.IsNullOrWhiteSpace(pathTexture)) return fallback;

        string name = pathTexture.Replace('\\', '/');
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name[(slash + 1)..];

        int x = name.IndexOf('x');
        if (x <= 0) return fallback;

        int start = x - 1;
        while (start >= 0 && char.IsDigit(name[start])) start--;
        if (!int.TryParse(name[(start + 1)..x], out int cells)) return fallback;
        return cells > 0 ? cells : fallback;
    }

    private static float Number(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        return float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
