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
    public float Armor;
    public int GoldCost;
    public int LumberCost;
    public float TrainSeconds = 15f;
    public float BuildSeconds = 30f;
    public int FoodUsed;
    public int FoodMade;

    /// <summary>第一套武器（UnitWeapons 的 *1 列）。</summary>
    public WeaponProfile Weapon = new();
    /// <summary>第二套武器（*2 列），null 表示没有。打第一武器打不到的目标类别时切换。</summary>
    public WeaponProfile Weapon2;

    public DefenseType DefenseType = DefenseType.Unarmored;
    /// <summary>UnitData.targType，本单位被索敌时匹配对方的武器 TargetKind。</summary>
    public TargetKind Kind = TargetKind.Ground;
    /// <summary>UnitData.formation：编队层号（0 = 近战前排，2 = 远程/法师后排）。</summary>
    public int FormationRank;
    /// <summary>UnitBalance.collision，真实碰撞/推挤半径（不是 footprint）。</summary>
    public float CollisionRadius = 16f;
    /// <summary>UnitData.movetp=fly，空中单位不吃地面路径阻挡。</summary>
    public bool Flying;
    /// <summary>UnitData.deathType：0/1 不衰减，2/3 会衰减；1/3 的尸体会被利用（吞尸、召唤骷髅）。</summary>
    public int DeathType = 3;
    public bool CorpseDecays => DeathType is 2 or 3;
    public bool CanBeRaised => DeathType is 1 or 3;

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
    /// <summary>UnitAbilities.heroAbilList：可学技能，等级从 0 开始（技能点加点）。</summary>
    public string[] HeroAbilList = Array.Empty<string>();
    /// <summary>UnitData.race：human / orc / undead / nightelf / creeps / critters…（圣光术判定亡灵要用）。</summary>
    public string Race = "";
    public string SoundSet = "";

    public string ShadowArt = "";
    public string BuildingShadowArt = "";
    public float ShadowWidth;
    public float ShadowHeight;
    public float ShadowOffsetX;
    public float ShadowOffsetY;

    // —— 英雄字段（UnitBalance 的 STR/AGI/INT + 成长 + Primary） ——
    public bool IsHero;
    public bool HideInHeroBar;
    public int Strength;
    public int Agility;
    public int Intelligence;
    public float StrengthGain;
    public float AgilityGain;
    public float IntelligenceGain;
    public string Primary = "";
    public float ManaRegen;
    /// <summary>UnitBalance.manaN：最大法力（非英雄法师也有蓝池）。</summary>
    public float ManaMax;
    /// <summary>UnitBalance.mana0：出生法力。</summary>
    public float ManaStart;
    public float SightDay = 1400f;
    /// <summary>转身角速度（弧度/秒）。UnitData.turnRate 是原版字段，这里按每 tick 0.1 的量纲折算。</summary>
    public float TurnSpeed = 3.6f;
    public float SightNight = 800f;
    /// <summary>UnitBalance.regenHP，生命恢复（点/秒）；regenType=none 的单位不回血。</summary>
    public float RegenHp;
    /// <summary>UnitBalance.regenType：always / none / night / blight。</summary>
    public string RegenType = "always";
    public int FootprintCells = 2;

    // —— 武器参数转发（外部按第一武器读；真正开火时用 WeaponFor 选中的那套） ——
    public bool CanAttack => Weapon.Enabled;
    public float AttackRange => Weapon.Range;
    public float AttackCooldown => Weapon.Cooldown;
    public float MinDamage => Weapon.MinDamage;
    public float MaxDamage => Weapon.MaxDamage;
    public float AcquireRange => Weapon.AcquireRange;
    public AttackType AttackType => Weapon.AttackType;
    public TargetKind Targets => Weapon.Targets;
    public string WeaponSound => Weapon.Sound;
    public float DamagePoint => Weapon.DamagePoint;
    public float Backswing => Weapon.Backswing;
    public float SplashFull => Weapon.SplashFull;
    public float SplashHalf => Weapon.SplashHalf;
    public float SplashQuarter => Weapon.SplashQuarter;
    public int SplashTargets => Weapon.SplashTargets;
    public string MissileScene => Weapon.MissileScene;
    public float MissileSpeed => Weapon.MissileSpeed;
    public float MissileArc => Weapon.MissileArc;

    /// <summary>两套武器合起来能打的目标类别。</summary>
    public TargetKind AllTargets => Weapon2 == null ? Weapon.Targets : Weapon.Targets | Weapon2.Targets;

    /// <summary>选武器：先看第一套能不能打，再退到第二套；两套都不行就是打不到。</summary>
    public WeaponProfile WeaponFor(TargetKind kind)
    {
        if (Weapon.Enabled && Weapon.CanTarget(kind)) return Weapon;
        if (Weapon2 != null && Weapon2.Enabled && Weapon2.CanTarget(kind)) return Weapon2;
        return null;
    }

    /// <summary>商店卖的东西（没有就是空）。</summary>
    public string[] SellItems = Array.Empty<string>();
    public bool IsShop => SellItems.Length > 0;

    public bool IsWorkerType => Builds.Length > 0;
    public bool CanGatherGold => HasAbility("Ahar") || HasAbility("Awha") || HasAbility("Aaha");
    public bool EntersMine => HasAbility("Aaha");
    public float FootprintRadius => FootprintCells * 32f * 0.5f;
    public bool CanGatherLumber => HasAbility("Ahar") || HasAbility("Awha") || HasAbility("Ahrl");
    /// <summary>能打到某类目标：两套武器任一能打即可。</summary>
    public bool CanTarget(TargetKind kind) => WeaponFor(kind) != null;

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
            ShadowArt = (ui.Get(uiRow, "unitShadow") ?? "").Trim(),
            BuildingShadowArt = (ui.Get(uiRow, "buildingShadow") ?? "").Trim(),
            ShadowWidth = Number(ui.Get(uiRow, "shadowW"), 0f),
            ShadowHeight = Number(ui.Get(uiRow, "shadowH"), 0f),
            ShadowOffsetX = Number(ui.Get(uiRow, "shadowX"), 0f),
            ShadowOffsetY = Number(ui.Get(uiRow, "shadowY"), 0f),
            HideInHeroBar = Number(ui.Get(uiRow, "hideHeroBar"), 0f) > 0.5f,
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
            }
            type.ManaRegen = Number(balance.Get(balanceRow, "regenMana"), 0f);
            type.ManaMax = Number(balance.Get(balanceRow, "manaN"), 0f);
            type.ManaStart = Number(balance.Get(balanceRow, "mana0"), 0f);
            if (DataRows.TryGetValue(id, out int turnRow) && !type.IsBuilding)
            {
                SlkTable unitData = War3Tables.UnitData();
                float turnRate = Number(unitData.Get(turnRow, "turnRate"), 0f);
                if (turnRate > 0f) type.TurnSpeed = Mathf.Clamp(turnRate * 6f, 1.5f, 14f);
                type.FormationRank = Mathf.Clamp((int)Number(unitData.Get(turnRow, "formation"), 0f), 0, 15);
            }
            type.SightNight = Number(balance.Get(balanceRow, "nsight"), 800f);
            type.RegenHp = Number(balance.Get(balanceRow, "regenHP"), 0f);
            type.RegenType = (balance.Get(balanceRow, "regenType") ?? "always").Trim();
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
            WeaponProfile first = ReadWeapon(weapons, weaponRow, "1");
            if (first != null) type.Weapon = first;
            type.Weapon2 = ReadWeapon(weapons, weaponRow, "2");
        }

        if (DataRows.TryGetValue(id, out int dataRow))
        {
            SlkTable data = War3Tables.UnitData();
            type.FootprintCells = FootprintFromPathTexture(data.Get(dataRow, "pathTex"), type.FootprintCells);
            TargetKind kind = CombatCodes.ParseTargets(data.Get(dataRow, "targType"));
            type.Kind = kind != TargetKind.None ? kind : (type.IsBuilding ? TargetKind.Structure : TargetKind.Ground);
            type.DeathType = data.GetInt(dataRow, "deathType", 3);
            type.Race = (data.Get(dataRow, "race") ?? "").Trim();
            string moveType = (data.Get(dataRow, "movetp") ?? "").Trim();
            type.Flying = string.Equals(moveType, "fly", StringComparison.OrdinalIgnoreCase);
        }

        if (AbilityRows.TryGetValue(id, out int abilityRow))
        {
            SlkTable abilities = War3Tables.UnitAbilities();
            type.Abilities = SplitList(abilities.Get(abilityRow, "abilList"));
            type.HeroAbilList = SplitList(abilities.Get(abilityRow, "heroAbilList"));
        }

        UnitFuncRow row = TechTree.Row(id);
        if (row != null)
        {
            type.Trains = row.Trains;
            type.Builds = row.Builds;
            type.Researches = row.Researches;
            type.UpgradeTo = row.UpgradeTo;
            type.Requires = row.Requires;
            type.SellItems = row.SellItems;
            string missileScene = PathResolve.ModelScene(row.MissileArt);
            type.Weapon.MissileScene = missileScene;
            type.Weapon.MissileArc = row.MissileArc;
            if (row.MissileSpeed > 0f) type.Weapon.MissileSpeed = row.MissileSpeed;
            if (type.Weapon2 != null)
            {
                type.Weapon2.MissileScene = missileScene;
                type.Weapon2.MissileArc = type.Weapon.MissileArc;
                type.Weapon2.MissileSpeed = type.Weapon.MissileSpeed;
            }
        }

        string localized = TechTree.UnitName(id);
        if (!string.IsNullOrWhiteSpace(localized)) type.Name = localized;
        type.Tip = TechTree.UnitTip(id);
        type.IconPath = PathResolve.IconPng(TechTree.UnitIconArt(id));

        return type;
    }

    private static WeaponProfile ReadWeapon(SlkTable weapons, int row, string slot)
    {
        string kind = weapons.Get(row, "weapTp" + slot)?.Trim();
        bool enabled = !string.IsNullOrEmpty(kind)
            && kind != "_" && kind != "-"
            && !kind.Equals("none", StringComparison.OrdinalIgnoreCase);
        if (!enabled) return null;

        var weapon = new WeaponProfile { Enabled = true };

        string sound = weapons.Get(row, "weapType" + slot)?.Trim();
        if (!string.IsNullOrEmpty(sound) && sound != "_" && sound != "-") weapon.Sound = sound;

        weapon.AttackType = CombatCodes.ParseAttack(weapons.Get(row, "atkType" + slot));
        TargetKind targets = CombatCodes.ParseTargets(weapons.Get(row, "targs" + slot));
        weapon.Targets = targets != TargetKind.None ? targets : TargetKind.Ground | TargetKind.Structure;
        weapon.Range = Number(weapons.Get(row, "rangeN" + slot), slot == "1" ? 90f : 500f);
        weapon.Cooldown = Number(weapons.Get(row, "cool" + slot), 1.5f);
        weapon.MinDamage = Number(weapons.Get(row, "mindmg" + slot), 5f);
        weapon.MaxDamage = Number(weapons.Get(row, "maxdmg" + slot), 10f);
        weapon.AcquireRange = Number(weapons.Get(row, "acquire"), 500f);
        weapon.DamagePoint = Number(weapons.Get(row, "dmgpt" + slot), 0.5f);
        weapon.Backswing = Number(weapons.Get(row, "backSw" + slot), 0.5f);
        weapon.SplashFull = Number(weapons.Get(row, "Farea" + slot), 0f);
        weapon.SplashHalf = Number(weapons.Get(row, "Harea" + slot), 0f);
        weapon.SplashQuarter = Number(weapons.Get(row, "Qarea" + slot), 0f);
        weapon.SplashTargets = Mathf.Max(1, weapons.GetInt(row, "targCount" + slot, 1));

        string spill = weapons.Get(row, "splashTargs" + slot);
        TargetKind spillShapes = CombatCodes.ParseTargets(spill);
        weapon.SpillTargets = spillShapes != TargetKind.None ? spillShapes : weapon.Targets;
        weapon.SpillRelations = CombatCodes.ParseRelations(spill);
        weapon.SplashHalfRatio = Number(weapons.Get(row, "Hfact" + slot), 0.5f);
        weapon.SplashQuarterRatio = Number(weapons.Get(row, "Qfact" + slot), 0.25f);
        return weapon;
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
