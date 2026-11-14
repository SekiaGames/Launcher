using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.War3Formats;

namespace OpenWC3.Game;

public sealed class UnitType
{
    public string Id;
    public string Name;
    public string Tip;
    public string IconPath;
    public string ModelPath;
    public float ModelScale = 1f;
    public float SelectionDiameter = 72f;
    public float WalkAnimationSpeed;
    public float RunAnimationSpeed;
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
    public Weapon Weapon = new();
    /// <summary>第二套武器（*2 列），null 表示没有。打第一武器打不到的目标类别时切换。</summary>
    public Weapon Weapon2;

    public DefenseType DefenseType = DefenseType.Unarmored;
    /// <summary>UnitData.targType，本单位被索敌时匹配对方的武器 TargetKind。</summary>
    public TargetKind Kind = TargetKind.Ground;
    /// <summary>UnitData.formation：编队层号（0 = 近战前排，2 = 远程/法师后排）。</summary>
    public int FormationRank;
    /// <summary>UnitBalance.collision，真实碰撞/推挤半径（不是 footprint）。</summary>
    public float CollisionRadius = 16f;
    /// <summary>UnitData.movetp=fly/hover，空中单位不吃地面路径阻挡。</summary>
    public bool Flying;
    public bool CanSleep;
    public string MoveType = "foot";

    public PathLayer PathingLayer => MoveType switch
    {
        "fly" or "hover" => PathLayer.Air,
        "float" => PathLayer.Water,
        "amph" => PathLayer.Amphibious,
        _ => PathLayer.Ground,
    };
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

    /// <summary>物编 type 列（mechanical/undead/ancient/townhall...）解析出来的分类位。</summary>
    public Classification Classifications;
    public string SoundSet = "";

    public string ShadowArt = "";
    public string BuildingShadowArt = "";
    public float ShadowWidth;
    public float ShadowHeight;
    public float ShadowOffsetX;
    public float ShadowOffsetY;

    public string GroundSplatTexture = "";
    public float GroundSplatScale;
    public float GroundSplatBirth;
    public float GroundSplatPause;
    public float GroundSplatDecay;
    public Color GroundSplatStartColor = new(1f, 1f, 1f, 1f);
    public Color GroundSplatMiddleColor = new(1f, 1f, 1f, 1f);
    public Color GroundSplatEndColor = new(1f, 1f, 1f, 1f);
    public int GroundSplatBlendMode;

    public bool HasGroundSplat => GroundSplatScale > 0f && !string.IsNullOrEmpty(GroundSplatTexture);

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
    public Weapon WeaponFor(TargetKind kind)
    {
        if (Weapon.Enabled && Weapon.CanTarget(kind)) return Weapon;
        if (Weapon2 != null && Weapon2.Enabled && Weapon2.CanTarget(kind)) return Weapon2;
        return null;
    }

    public string[] SellItems = Array.Empty<string>();
    /// <summary>中立建筑卖的兵（酒馆/雇佣兵营地/地精实验室）。</summary>
    public string[] SellUnits = Array.Empty<string>();
    /// <summary>卖物品的商店（能买卖、能回收物品）。酒馆只卖兵，不算。</summary>
    public bool IsShop => SellItems.Length > 0;
    public bool SellsUnits => SellUnits.Length > 0;
    /// <summary>有货架就要跑库存（物品或单位任一）。</summary>
    public bool HasStock => IsShop || SellsUnits;

    /// <summary>UnitBalance 的 stockMax / stockRegen / stockStart：出售时的库存上限、补一件的秒数、开局延迟。</summary>
    public int StockMax;
    public float StockRegen;
    public float StockStart;

    public bool IsWorkerType => Builds.Length > 0;
    public bool CanGatherGold => HasAbility("Ahar") || HasAbility("Awha") || HasAbility("Aaha");
    public bool EntersMine => HasAbility("Aaha");
    public float FootprintRadius => FootprintCells * 32f * 0.5f;
    public bool CanGatherLumber => HasAbility("Ahar") || HasAbility("Awha") || HasAbility("Ahrl");
    /// <summary>能打到某类目标：两套武器任一能打即可。</summary>
    public static Classification ParseClassifications(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "_") return Classification.None;
        Classification flags = Classification.None;
        foreach (string token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(token, true, out Classification value)) flags |= value;
        }
        return flags;
    }

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

public static class UnitFactory
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
        Index(War3SlkReader.UnitUI(), "unitUIID", UiRows);
        Index(War3SlkReader.UnitBalance(), "unitBalanceID", BalanceRows);
        Index(War3SlkReader.UnitWeapons(), "unitWeapID", WeaponRows);
        Index(War3SlkReader.UnitAbilities(), "unitAbilID", AbilityRows);
        Index(War3SlkReader.UnitData(), "unitID", DataRows);
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

    public static Unit Create(UnitType type, int player)
    {
        if (type == null) return null;
        return new Unit
        {
            Type = type,
            Player = player,
            Hp = type.Hp,
            MaxHp = type.Hp,
            Mana = type.ManaStart,
            IsBuilding = type.IsBuilding,
            IsHero = type.IsHero,
        };
    }

    public static Unit Create(War3ObjectData objects, string id, int team, Vec2 position, float facing, int heroLevel = 0)
    {
        UnitType type = Get(id);
        if (type == null) return null;
        return new Unit
        {
            Type = type,
            Player = team,
            Position = new Vector3(position.X, 0f, position.Y),
            Facing = facing,
            Hp = type.Hp,
            MaxHp = type.Hp,
            Mana = type.ManaStart,
            IsBuilding = type.IsBuilding,
            IsHero = type.IsHero,
        };
    }

    /// <summary>幻象继承英雄三围（客户端 UnitFactory.CopyIllusionHeroAttributes）。</summary>
    /// <summary>幻象复制收尾：攻击/护甲/移速按本体取值（Launcher 侧直接抄现有数值）。</summary>
    public static void CompleteIllusionCopy(Unit source, Unit illusion)
    {
        if (source == null || illusion == null) return;
        illusion.SpeedBonus = source.SpeedBonus;
        illusion.BonusArmor = source.BonusArmor;
    }

    public static void CopyIllusionHeroAttributes(Unit source, Unit illusion)
    {
        if (source == null || illusion == null) return;
        illusion.Strength = source.Strength;
        illusion.Agility = source.Agility;
        illusion.Intelligence = source.Intelligence;
        illusion.Level = source.Level;
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

        SlkTable ui = War3SlkReader.UnitUI();
        string file = ui.Get(uiRow, "file");
        if (string.IsNullOrWhiteSpace(file)) return null;

        var type = new UnitType
        {
            Id = id,
            ModelPath = PathResolve.ModelScene(file),
            ModelScale = Number(ui.Get(uiRow, "modelScale"), 1f),
            SelectionDiameter = Number(ui.Get(uiRow, "scale"), 1f) * SelectionScaleFactor,
            WalkAnimationSpeed = Number(ui.Get(uiRow, "walk"), 0f),
            RunAnimationSpeed = Number(ui.Get(uiRow, "run"), 0f),
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
        ApplyGroundSplat(type, ui.Get(uiRow, "uberSplat"));

        if (BalanceRows.TryGetValue(id, out int balanceRow))
        {
            SlkTable balance = War3SlkReader.UnitBalance();
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
                SlkTable unitData = War3SlkReader.UnitData();
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
            type.StockMax = Mathf.Max(0, balance.GetInt(balanceRow, "stockMax"));
            type.StockRegen = Number(balance.Get(balanceRow, "stockRegen"), 0f);
            type.StockStart = Number(balance.Get(balanceRow, "stockStart"), 0f);
            type.UpgradesApplied = SplitList(balance.Get(balanceRow, "upgrades"));
            type.Classifications = UnitType.ParseClassifications(balance.Get(balanceRow, "type"));
            string comment = balance.Get(balanceRow, "comment(s)");
            if (!string.IsNullOrWhiteSpace(comment)) type.Name = comment.Trim();
        }

        if (WeaponRows.TryGetValue(id, out int weaponRow))
        {
            SlkTable weapons = War3SlkReader.UnitWeapons();
            Weapon first = ReadWeapon(weapons, weaponRow, "1");
            if (first != null) type.Weapon = first;
            type.Weapon2 = ReadWeapon(weapons, weaponRow, "2");
        }

        if (DataRows.TryGetValue(id, out int dataRow))
        {
            SlkTable data = War3SlkReader.UnitData();
            type.FootprintCells = FootprintFromPathTexture(data.Get(dataRow, "pathTex"), type.FootprintCells);
            TargetKind kind = CombatCodes.ParseTargets(data.Get(dataRow, "targType"));
            type.Kind = kind != TargetKind.None ? kind : (type.IsBuilding ? TargetKind.Structure : TargetKind.Ground);
            type.DeathType = data.GetInt(dataRow, "deathType", 3);
            type.Race = (data.Get(dataRow, "race") ?? "").Trim();
            string moveType = (data.Get(dataRow, "movetp") ?? "").Trim().ToLowerInvariant();
            type.MoveType = moveType.Length == 0 || moveType == "_" ? "foot" : moveType;
            type.Flying = type.MoveType is "fly" or "hover";
            type.CanSleep = data.GetInt(dataRow, "canSleep", 0) != 0;
        }

        if (AbilityRows.TryGetValue(id, out int abilityRow))
        {
            SlkTable abilities = War3SlkReader.UnitAbilities();
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
            type.SellItems = MergeShopItems(row.SellItems, row.MakeItems);
            type.SellUnits = row.SellUnits;
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

    private static Weapon ReadWeapon(SlkTable weapons, int row, string slot)
    {
        string kind = weapons.Get(row, "weapTp" + slot)?.Trim();
        bool enabled = !string.IsNullOrEmpty(kind)
            && kind != "_" && kind != "-"
            && !kind.Equals("none", StringComparison.OrdinalIgnoreCase);
        if (!enabled) return null;

        var weapon = new Weapon { Enabled = true };

        string sound = weapons.Get(row, "weapType" + slot)?.Trim();
        if (!string.IsNullOrEmpty(sound) && sound != "_" && sound != "-") weapon.Sound = sound;

        weapon.AttackType = CombatCodes.ParseAttack(weapons.Get(row, "atkType" + slot));
        TargetKind targets = CombatCodes.ParseTargets(weapons.Get(row, "targs" + slot));
        weapon.Targets = targets != TargetKind.None ? targets : TargetKind.Ground | TargetKind.Structure;
        weapon.Range = Number(weapons.Get(row, "rangeN" + slot), slot == "1" ? 90f : 500f);
        weapon.Cooldown = Number(weapons.Get(row, "cool" + slot), 1.5f);
        weapon.MinDamage = Number(weapons.Get(row, "mindmg" + slot), 5f);
        weapon.MaxDamage = Number(weapons.Get(row, "maxdmg" + slot), 10f);
        // 客户端按 dice1/sides1/dmgplus1 掷骰（Weapon.cs:114-119），不读折算后的 min/max。
        weapon.DamageBase = weapons.GetInt(row, "dmgplus" + slot, 0);
        weapon.DamageDice = weapons.GetInt(row, "dice" + slot, 0);
        weapon.DamageSides = weapons.GetInt(row, "sides" + slot, 0);
        if (weapon.DamageDice <= 0 && weapon.DamageSides <= 0)
        {
            weapon.DamageBase = Mathf.RoundToInt(weapon.MinDamage);
            int span = Mathf.Max(0, Mathf.RoundToInt(weapon.MaxDamage - weapon.MinDamage));
            weapon.DamageDice = span > 0 ? 1 : 0;
            weapon.DamageSides = span;
        }
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

    private static string[] MergeShopItems(string[] sellItems, string[] makeItems)
    {
        if ((makeItems?.Length ?? 0) == 0) return sellItems ?? Array.Empty<string>();
        if ((sellItems?.Length ?? 0) == 0) return makeItems;

        var merged = new List<string>(sellItems.Length + makeItems.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in sellItems)
        {
            if (seen.Add(id)) merged.Add(id);
        }
        foreach (string id in makeItems)
        {
            if (seen.Add(id)) merged.Add(id);
        }
        return merged.ToArray();
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

    private static float? _selectionScaleFactor;

    private static float SelectionScaleFactor => _selectionScaleFactor ??=
        GeneratedTexts.TryGet("UIMiscData", "SelectionCircle.ScaleFactor", out string factor)
            ? Number(factor, 72f) : 72f;

    private static float Number(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        return float.TryParse(text.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }

    private static void ApplyGroundSplat(UnitType type, string uberSplat)
    {
        string id = (uberSplat ?? "").Trim();
        if (id.Length == 0 || id == "_" || id == "-") return;
        SlkTable table = War3SlkReader.UberSplatData();
        int row = table.FindRow("Name", id);
        if (row < 0) return;

        string directory = (table.Get(row, "Dir") ?? "").Replace('\\', '/').Trim().TrimEnd('/');
        string file = (table.Get(row, "file") ?? "").Trim();
        if (file.Length == 0) return;

        type.GroundSplatTexture = directory.Length == 0 ? file + ".png" : directory + "/" + file + ".png";
        type.GroundSplatScale = Number(table.Get(row, "Scale"), 0f);
        type.GroundSplatBirth = MathF.Max(0f, Number(table.Get(row, "BirthTime"), 0f));
        type.GroundSplatPause = MathF.Max(0f, Number(table.Get(row, "PauseTime"), 0f));
        type.GroundSplatDecay = MathF.Max(0f, Number(table.Get(row, "Decay"), 0f));
        type.GroundSplatStartColor = SplatColor(table, row, "Start");
        type.GroundSplatMiddleColor = SplatColor(table, row, "Middle");
        type.GroundSplatEndColor = SplatColor(table, row, "End");
        type.GroundSplatBlendMode = (int)Number(table.Get(row, "BlendMode"), 0f);
    }

    private static Color SplatColor(SlkTable table, int row, string phase) => new(
        Mathf.Clamp(Number(table.Get(row, phase + "R"), 255f) / 255f, 0f, 1f),
        Mathf.Clamp(Number(table.Get(row, phase + "G"), 255f) / 255f, 0f, 1f),
        Mathf.Clamp(Number(table.Get(row, phase + "B"), 255f) / 255f, 0f, 1f),
        Mathf.Clamp(Number(table.Get(row, phase + "A"), 255f) / 255f, 0f, 1f));

}
