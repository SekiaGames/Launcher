using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 物品的「用一次」效果：一次性卷轴/药水、区域增益、揭示、守卫、回城、保存权杖这类
/// 不需要复用技能实现的效果。表驱动（码 → 处理函数），与 <see cref="ItemUseRules"/> 共用同一张表：
/// 表里有 = 这个码由物品自己处理；表里没有 = 交给技能注册表复用同名实现。
/// </summary>
public static class ItemUseEffect
{
    private delegate bool Handler(World world, Unit unit, AbilityType type, Unit target, Vector3 point);

    private static readonly Dictionary<string, Handler> Handlers = new(StringComparer.OrdinalIgnoreCase)
    {
        // —— 自身状态：无敌、加速、魔免、隐身、吸血药水 ——
        ["AIVU"] = (world, unit, type, target, point) => AddState(unit, StateMod.Invulnerable, Seconds(type, 15f)),
        ["AIVL"] = (world, unit, type, target, point) => AddState(unit, StateMod.Invulnerable, Seconds(type, 15f)),
        ["AISP"] = (world, unit, type, target, point) => BuffSelf(world, unit,
            new StatBuff { MoveSpeedPct = type.DataAt(1, 'A') }, Seconds(type, 45f)),
        ["AIMX"] = (world, unit, type, target, point) => AddState(unit, StateMod.MagicImmune,
            Seconds(type, type.DataAt(1, 'A'))),
        ["AIPV"] = (world, unit, type, target, point) => BuffSelf(world, unit, new StatBuff
        {
            FlatDamage = type.DataAt(1, 'A'),
            LifestealPct = type.DataAt(1, 'B'),
        }, Seconds(type, 60f)),
        ["AIV1"] = (world, unit, type, target, point) => Invisible(world, unit, type, 120f),
        ["AIV2"] = (world, unit, type, target, point) => Invisible(world, unit, type, 180f),
        ["AIGU"] = (world, unit, type, target, point) => UltravisionToAll(world, unit),
        ["AISA"] = (world, unit, type, target, point) => SpeedToAllies(world, unit, type),

        // —— 回血 / 回蓝（药剂、卷轴、符文、恢复指环类） ——
        ["AIH1"] = (world, unit, type, target, point) => HealSelf(world, unit, type.DataAt(1, 'A')),
        ["AIH2"] = (world, unit, type, target, point) => HealSelf(world, unit, type.DataAt(1, 'A')),
        ["AIH3"] = (world, unit, type, target, point) => HealSelf(world, unit, type.DataAt(1, 'A')),
        ["AIHX"] = (world, unit, type, target, point) => HealSelf(world, unit, type.DataAt(1, 'A')),
        ["AIRE"] = (world, unit, type, target, point) => RestoreSelf(world, unit, type.DataAt(1, 'A'), type.DataAt(1, 'B')),
        ["AIP1"] = (world, unit, type, target, point) => RestoreSelf(world, unit, type.DataAt(1, 'A'), type.DataAt(1, 'B')),
        ["AIP2"] = (world, unit, type, target, point) => RestoreSelf(world, unit, type.DataAt(1, 'A'), type.DataAt(1, 'B')),
        ["AIP3"] = (world, unit, type, target, point) => RestoreSelf(world, unit, type.DataAt(1, 'A'), type.DataAt(1, 'B')),
        ["AIP4"] = (world, unit, type, target, point) => RestoreSelf(world, unit, type.DataAt(1, 'A'), type.DataAt(1, 'B')),
        ["AIP5"] = (world, unit, type, target, point) => RestoreArea(world, unit, type),
        ["AIP6"] = (world, unit, type, target, point) => RestoreArea(world, unit, type),
        ["AIRA"] = (world, unit, type, target, point) => RestoreArea(world, unit, type),
        ["AIHA"] = (world, unit, type, target, point) => HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["AIHB"] = (world, unit, type, target, point) => HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["APH1"] = (world, unit, type, target, point) => HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["APH2"] = (world, unit, type, target, point) => HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["APH3"] = (world, unit, type, target, point) => HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["APRA"] = (world, unit, type, target, point) => HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["AISL"] = (world, unit, type, target, point) => HealArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["AIRL"] = (world, unit, type, target, point) => RegenSelf(world, unit, type),
        ["AIM1"] = (world, unit, type, target, point) => ManaSelf(world, unit, type.DataAt(1, 'A')),
        ["AIM2"] = (world, unit, type, target, point) => ManaSelf(world, unit, type.DataAt(1, 'A')),
        ["AIMR"] = (world, unit, type, target, point) => RestoreManaArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["APMR"] = (world, unit, type, target, point) => RestoreManaArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),
        ["APMG"] = (world, unit, type, target, point) => RestoreManaArea(world, unit, RadiusOf(type), type.DataAt(1, 'A')),

        // —— 资源类（金袋 / 木材堆） ——
        ["AIGO"] = (world, unit, type, target, point) => GrantGold(world, unit, type.DataAt(1, 'A')),
        ["AILU"] = (world, unit, type, target, point) => GrantLumber(world, unit, type.DataAt(1, 'A')),

        // —— 属性书 / 经验书（永久成长，点一下就用） ——
        ["AIAM"] = (world, unit, type, target, point) => AddAttributes(world, unit, type.DataAt(1, 'A'), 0f, 0f),
        ["AIGM"] = (world, unit, type, target, point) => AddAttributes(world, unit, type.DataAt(1, 'A'), 0f, 0f),
        ["AISM"] = (world, unit, type, target, point) => AddAttributes(world, unit, 0f, 0f, type.DataAt(1, 'C')),
        ["AINM"] = (world, unit, type, target, point) => AddAttributes(world, unit, 0f, 0f, type.DataAt(1, 'C')),
        ["AIIM"] = (world, unit, type, target, point) => AddAttributes(world, unit, 0f, type.DataAt(1, 'B'), 0f),
        ["AITM"] = (world, unit, type, target, point) => AddAttributes(world, unit, 0f, type.DataAt(1, 'B'), 0f),
        ["AIXM"] = (world, unit, type, target, point) => AddAttributes(world, unit,
            type.DataAt(1, 'A'), type.DataAt(1, 'B'), type.DataAt(1, 'C')),
        ["AIMH"] = (world, unit, type, target, point) => AddMaxHp(world, unit, type.DataAt(1, 'A')),
        ["AIPX"] = (world, unit, type, target, point) => AddMaxHp(world, unit, type.DataAt(1, 'A')),
        ["AILM"] = (world, unit, type, target, point) => AddHeroLevel(world, unit, type.DataAt(1, 'A')),
        ["AIEM"] = (world, unit, type, target, point) => AddExperience(world, unit, type.DataAt(1, 'A')),
        ["AIE2"] = (world, unit, type, target, point) => AddExperience(world, unit, type.DataAt(1, 'A')),
        ["AIPR"] = (world, unit, type, target, point) => ManaSelf(world, unit, type.DataAt(1, 'B')),
        ["AIPL"] = (world, unit, type, target, point) => ManaSelf(world, unit, type.DataAt(1, 'B')),

        // —— 召唤雕像（DataC 里是单位 id，DataA 是只数） ——
        ["AIFU"] = (world, unit, type, target, point) => SummonFigurine(world, unit, type),
        ["AIIR"] = (world, unit, type, target, point) => SummonFigurine(world, unit, type),
        ["AIUW"] = (world, unit, type, target, point) => SummonFigurine(world, unit, type),
        ["AIFF"] = (world, unit, type, target, point) => SummonFigurine(world, unit, type),
        ["AIFS"] = (world, unit, type, target, point) => SummonFigurine(world, unit, type),

        // —— 其余一次性物品 ——
        ["AIND"] = (world, unit, type, target, point) => world.ReviveCorpsesAround(unit.Position, 900f,
            Mathf.Max(1, Mathf.RoundToInt(type.DataAt(1, 'A'))), unit.Player, Seconds(type, 60f), 1f) > 0,
        ["ANSE"] = (world, unit, type, target, point) => ShieldAllies(world, unit, type),
        ["AIDB"] = (world, unit, type, target, point) => ArmorAndHealAllies(world, unit, type),
        ["AIMT"] = (world, unit, type, target, point) => TeleportToTarget(world, unit, target),
        ["AIPM"] = (world, unit, type, target, point) => PlaceMine(world, unit, type, point),

        // —— 驱散 / 护盾 / 守卫 ——
        ["AIDI"] = (world, unit, type, target, point) => DispelAround(world, unit, type),
        ["AIDS"] = (world, unit, type, target, point) => DispelAround(world, unit, type),
        ["APDI"] = (world, unit, type, target, point) => DispelAround(world, unit, type),
        ["AIDC"] = (world, unit, type, target, point) => DispelAround(world, unit, type),
        ["AILS"] = (world, unit, type, target, point) => BuffSelf(world, unit, new LightningShieldBuff
        {
            DamageSource = unit,
            Radius = RadiusOf(type),
            DamagePerSecond = type.DataAt(1, 'A'),
        }, Seconds(type, 20f)),
        ["AIXS"] = (world, unit, type, target, point) => BuffSelf(world, unit, new AntiMagicShieldBuff
        {
            Caster = unit,
            Absorb = type.DataAt(1, 'A') > 0f ? type.DataAt(1, 'A') : 300f,
        }, Seconds(type, 30f)),
        ["AIHW"] = (world, unit, type, target, point) => world.Summon(unit, "ohwd", unit.Position, Seconds(type, 30f)) != null,
        ["AISW"] = (world, unit, type, target, point) => world.Summon(unit, "oeye", unit.Position, Seconds(type, 300f)) != null,
        ["APWT"] = (world, unit, type, target, point) => world.Summon(unit, "nwad", unit.Position, Seconds(type, 120f)) != null,

        // —— 回城 / 保存 / 避难（目标单位的传送） ——
        ["AIRT"] = (world, unit, type, target, point) => TownPortal(world, unit, type),
        ["AITP"] = (world, unit, type, target, point) => TownPortal(world, unit, type),
        ["ANPR"] = (world, unit, type, target, point) => RescueToHall(world, unit, target, 0f, 0f),
        ["ANSA"] = (world, unit, type, target, point) => RescueToHall(world, unit, target,
            type.DataAt(1, 'A'), type.DataAt(1, 'E')),

        // —— 尸体 / 召唤 ——
        ["AIAN"] = (world, unit, type, target, point) =>
            world.ReviveCorpsesAround(unit.Position, 900f, 6, unit.Player, Seconds(type, 40f), 1f) > 0,
        ["AIRS"] = (world, unit, type, target, point) =>
            world.ReviveCorpsesAround(unit.Position, 900f, 6, unit.Player, 0f, 1f) > 0,
        ["AIRD"] = (world, unit, type, target, point) => world.ReviveCorpsesAround(unit.Position, 900f,
            Mathf.Max(1, Mathf.RoundToInt(type.DataAt(1, 'A'))), unit.Player, 0f, 1f) > 0,
        ["APRL"] = (world, unit, type, target, point) =>
            world.ReviveCorpsesAround(unit.Position, 1400f, 1, unit.Player, 0f, 1f) > 0,
        ["APRR"] = (world, unit, type, target, point) =>
            world.ReviveCorpsesAround(unit.Position, 1400f, 3, unit.Player, 0f, 1f) > 0,
        ["AIMO"] = (world, unit, type, target, point) => world.Summon(unit, "nlur", unit.Position, Seconds(type, 60f)) != null,
        ["AIWM"] = (world, unit, type, target, point) => world.Summon(unit, "nmrr", unit.Position, Seconds(type, 60f)) != null,
        ["AISH"] = (world, unit, type, target, point) => SummonMany(world, unit, "otbk",
            Mathf.Max(1, Mathf.RoundToInt(type.DataAt(1, 'B'))), Seconds(type, 60f)),
        ["AIFD"] = (world, unit, type, target, point) => world.Summon(unit, "nrdr", unit.Position, Seconds(type, 60f)) != null,
        ["AIFR"] = (world, unit, type, target, point) => world.Summon(unit, "ngst", unit.Position, Seconds(type, 180f)) != null,
        ["AIFH"] = (world, unit, type, target, point) => world.Summon(unit, "nfel", unit.Position, Seconds(type, 180f)) != null,

        // —— 揭示 / 反隐 ——
        ["AIRV"] = (world, unit, type, target, point) =>
        {
            world.AddFogReveal(unit.Player, unit.Position, 100000f, Seconds(type, 5f));
            return true;
        },
        ["AIFA"] = (world, unit, type, target, point) =>
        {
            world.AddFogReveal(unit.Player, unit.Position, 1800f, Seconds(type, 15f));
            return true;
        },
        ["AITA"] = (world, unit, type, target, point) =>
        {
            world.AddFogReveal(unit.Player, unit.Position, RadiusOf(type), Seconds(type, 10f));
            return true;
        },
        ["AITB"] = (world, unit, type, target, point) =>
        {
            world.AddFogReveal(unit.Player, unit.Position, RadiusOf(type), Seconds(type, 10f));
            return true;
        },

        // —— 幻象 / 灵魂链 / 云雾 / 建筑加固 / 群体增益 ——
        ["AIIL"] = (world, unit, type, target, point) => SpawnIllusions(world, unit, type),
        ["ASPP"] = (world, unit, type, target, point) => LinkAllies(world, unit, type),
        ["AIFG"] = (world, unit, type, target, point) => CloudOfFog(world, unit, type),
        ["AIGF"] = (world, unit, type, target, point) => FortifyBuildings(world, unit, type),
        ["AIDA"] = (world, unit, type, target, point) => ArmorToAllies(world, unit, type),
        ["APSA"] = (world, unit, type, target, point) =>
        {
            foreach (Unit ally in world.AlliesInRange(unit, unit.Position, RadiusOf(type)))
            {
                if (!ally.Alive || ally.IsBuilding) continue;
                world.ApplyBuff(ally, new StatBuff { MoveSpeedPct = 0.3f }, Seconds(type, 60f));
            }
            return true;
        },

        // —— 偷蓝法杖（Aste）：只吸 DataA，不是吸干 ——
        ["ASTE"] = (world, unit, type, target, point) => StealMana(world, unit, target, type.DataAt(1, 'A')),
    };

    /// <summary>需要点目标的物品码（其余都是一点即用）。</summary>
    private static readonly Dictionary<string, ItemTargetMode> TargetModes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASTE"] = ItemTargetMode.Unit,
        ["ANPR"] = ItemTargetMode.Unit,
        ["ANSA"] = ItemTargetMode.Unit,
        ["AIMT"] = ItemTargetMode.Unit,
        ["AIPM"] = ItemTargetMode.Point,
    };

    public static bool Handles(string code) => code != null && Handlers.ContainsKey(code);

    public static ItemTargetMode TargetModeOf(string code)
    {
        if (code == null) return ItemTargetMode.None;
        return TargetModes.TryGetValue(code, out ItemTargetMode mode) ? mode : ItemTargetMode.None;
    }

    /// <summary>
    /// 用一次物品。返回 false 表示这次没生效（不消耗、不进冷却）。
    /// 目标/落点由调用方按 <see cref="ItemUseRules"/> 的计划给出。
    /// </summary>
    public static bool TryUse(World world, Unit unit, Item item, Unit target, Vector3 point)
    {
        if (world == null || unit == null || item?.Type == null) return false;
        if (string.IsNullOrWhiteSpace(item.Type.AbilList)) return false;

        if (ItemUseRules.PlanFor(item.Type).Kind != ItemUseKind.OneShot) return false;

        foreach (string raw in item.Type.AbilList.Split(',', ';', StringSplitOptions.TrimEntries))
        {
            if (raw.Length < 4) continue;
            string code = raw[..4];
            if (!Handlers.TryGetValue(code, out Handler handler)) continue;
            AbilityType type = AbilityCatalog.Get(code);
            if (type == null) continue;
            if (handler(world, unit, type, target, point)) return true;
        }
        return false;
    }

    private static bool AddState(Unit unit, StateMod mod, float seconds)
    {
        unit.State.Add(mod, seconds);
        return true;
    }

    private static bool BuffSelf(World world, Unit unit, Buff buff, float seconds)
    {
        world.ApplyBuff(unit, buff, seconds);
        return true;
    }

    private static bool Invisible(World world, Unit unit, AbilityType type, float fallback)
    {
        world.ApplyBuff(unit, new InvisibilityBuff(), Seconds(type, fallback));
        return true;
    }

    private static bool UltravisionToAll(World world, Unit unit)
    {
        bool any = false;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Player != unit.Player) continue;
            if (other.GiveAbility("AIgu", 1) == null) continue;
            any = true;
        }
        return any;
    }

    private static bool SpeedToAllies(World world, Unit unit, AbilityType type)
    {
        float bonus = Mathf.Max(0f, type.DataAt(1, 'A') - 1f);
        if (bonus <= 0f) bonus = 0.6f;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, RadiusOf(type)))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            world.ApplyBuff(ally, new StatBuff { MoveSpeedPct = bonus }, Seconds(type, 10f));
        }
        return true;
    }

    private static bool HealSelf(World world, Unit unit, float amount)
    {
        if (amount <= 0f) return false;
        if (unit.Hp >= unit.MaxHp) return false;
        unit.Heal(amount);
        return true;
    }

    private static bool RestoreSelf(World world, Unit unit, float hp, float mana)
    {
        bool needed = (hp > 0f && unit.Hp < unit.MaxHp) || (mana > 0f && unit.MaxMana > 0f && unit.Mana < unit.MaxMana);
        if (!needed) return false;
        if (hp > 0f) unit.Heal(hp);
        if (mana > 0f) unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + mana);
        return true;
    }

    private static bool ManaSelf(World world, Unit unit, float amount)
    {
        if (amount <= 0f || unit.MaxMana <= 0f || unit.Mana >= unit.MaxMana) return false;
        unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + amount);
        return true;
    }

    private static bool RegenSelf(World world, Unit unit, AbilityType type)
    {
        float amount = type.DataAt(1, 'A');
        if (amount <= 0f) return false;
        if (unit.Hp >= unit.MaxHp) return false;

        // 物编 DataA 是「整瓶回多少」，这里按 Dur 摊到每秒（医疗剂 400 / 45s）
        float seconds = Seconds(type, 45f);
        float perSecond = amount / Mathf.Max(1f, seconds);
        world.ApplyBuff(unit, new HealOverTimeBuff { AmountPerTick = perSecond, Interval = 1f }, seconds);
        return true;
    }

    private static bool RestoreArea(World world, Unit unit, AbilityType type)
    {
        float hp = type.DataAt(1, 'A');
        float mana = type.DataAt(1, 'B');
        bool any = false;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, RadiusOf(type)))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            if (hp > 0f && ally.Hp < ally.MaxHp)
            {
                ally.Heal(hp);
                any = true;
            }
            if (mana > 0f && ally.MaxMana > 0f && ally.Mana < ally.MaxMana)
            {
                ally.Mana = Mathf.Min(ally.MaxMana, ally.Mana + mana);
                any = true;
            }
        }
        return any;
    }

    private static bool GrantGold(World world, Unit unit, float amount)
    {
        if (amount <= 0f) return false;
        world.GrantGold(unit.Player, Mathf.RoundToInt(amount));
        return true;
    }

    private static bool GrantLumber(World world, Unit unit, float amount)
    {
        if (amount <= 0f) return false;
        world.GrantLumber(unit.Player, Mathf.RoundToInt(amount));
        return true;
    }

    private static bool AddAttributes(World world, Unit unit, float agility, float intelligence, float strength)
    {
        if (!unit.IsHero) return false;
        if (agility == 0f && intelligence == 0f && strength == 0f) return false;

        unit.BonusAgility += Mathf.RoundToInt(agility);
        unit.BonusIntelligence += Mathf.RoundToInt(intelligence);
        unit.BonusStrength += Mathf.RoundToInt(strength);
        world.RefreshHeroAttributes(unit);
        return true;
    }

    private static bool AddMaxHp(World world, Unit unit, float amount)
    {
        if (!unit.IsHero || amount <= 0f) return false;
        unit.BonusMaxHp += amount;
        unit.MaxHp += amount;
        unit.Hp += amount;
        world.RefreshHeroAttributes(unit);
        return true;
    }

    /// <summary>经验书/力量书这类把英雄直接提一级的口径（物编 DataA 是级数）。</summary>
    private static bool AddHeroLevel(World world, Unit unit, float levels)
    {
        if (!unit.IsHero || levels <= 0f) return false;
        int count = Mathf.Max(1, Mathf.RoundToInt(levels));
        for (int index = 0; index < count; index++) world.AddExperience(unit, HeroRules.XpToNext(unit.Level));
        return true;
    }

    private static bool AddExperience(World world, Unit unit, float amount)
    {
        if (!unit.IsHero || amount <= 0f) return false;
        world.AddExperience(unit, Mathf.RoundToInt(amount));
        return true;
    }

    /// <summary>雕像类物品：DataC 存单位 id，DataA 是召唤只数，Dur 是存在时间。</summary>
    private static bool SummonFigurine(World world, Unit unit, AbilityType type)
    {
        string unitId = type.DataTextAt(1, 'C').Trim();
        if (unitId.Length < 4) return false;
        int count = Mathf.Max(1, Mathf.RoundToInt(type.DataAt(1, 'A')));
        return SummonMany(world, unit, unitId, count, Seconds(type, 180f));
    }

    private static bool ShieldAllies(World world, Unit unit, AbilityType type)
    {
        float absorb = type.DataAt(1, 'A') > 0f ? type.DataAt(1, 'A') : 300f;
        bool any = false;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, RadiusOf(type)))
        {
            if (!ally.Alive) continue;
            world.ApplyBuff(ally, new AntiMagicShieldBuff { Caster = unit, Absorb = absorb }, Seconds(type, 30f));
            any = true;
        }
        return any;
    }

    /// <summary>秘法卷轴这类：先给护甲再回血（DataA 护甲 / DataB 回血）。</summary>
    private static bool ArmorAndHealAllies(World world, Unit unit, AbilityType type)
    {
        float armor = type.DataAt(1, 'A');
        float heal = type.DataAt(1, 'B');
        bool any = false;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, RadiusOf(type)))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            if (armor > 0f) world.ApplyBuff(ally, new StatBuff { Armor = armor }, Seconds(type, 30f));
            if (heal > 0f) ally.Heal(heal);
            any = true;
        }
        return any;
    }

    /// <summary>地精地雷：在落点埋一颗（UnitID 读物编，默认 nglm）。</summary>
    private static bool PlaceMine(World world, Unit unit, AbilityType type, Vector3 point)
    {
        string mineId = type.SummonAt(1);
        if (mineId.Length < 4) mineId = "nglm";
        return world.Summon(unit, mineId, point, 0f) != null;
    }

    /// <summary>传送法杖：把携带者送到目标友军/建筑身边。</summary>
    private static bool TeleportToTarget(World world, Unit unit, Unit target)
    {
        if (target == null || !target.Alive) return false;
        if (!world.AreAllied(unit.Player, target.Player)) return false;

        var spot = new Vector3(target.Position.X + 64f, target.Position.Y, target.Position.Z + 64f);
        unit.CancelAttack();
        unit.Target = null;
        unit.MoveTarget = null;
        unit.Path = null;
        world.MoveUnitTo(unit, spot);
        return true;
    }

    private static bool DispelAround(World world, Unit unit, AbilityType type)
    {
        float radius = RadiusOf(type);
        world.DispelArea(unit, unit.Position, radius, hostile: true, summonDamage: 0f);
        world.DispelArea(unit, unit.Position, radius, hostile: false, summonDamage: 0f);
        return true;
    }

    private static bool CloudOfFog(World world, Unit unit, AbilityType type)
    {
        world.AddGroundSpell(new CloudOfFogSpell
        {
            Caster = unit,
            Position = unit.Position,
            Radius = RadiusOf(type),
            Remaining = Seconds(type, 30f),
            MissChance = 0.7f,
        });
        return true;
    }

    private static bool FortifyBuildings(World world, Unit unit, AbilityType type)
    {
        float armor = type.DataAt(1, 'A') > 0f ? type.DataAt(1, 'A') : 1f;
        bool any = false;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, 900f))
        {
            if (!ally.Alive || !ally.IsBuilding) continue;
            world.ApplyBuff(ally, new StatBuff { Armor = armor }, Seconds(type, 60f));
            any = true;
        }
        return any;
    }

    private static bool ArmorToAllies(World world, Unit unit, AbilityType type)
    {
        float armor = type.DataAt(1, 'A');
        if (armor <= 0f) return false;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, 900f))
        {
            if (!ally.Alive) continue;
            world.ApplyBuff(ally, new StatBuff { Armor = armor }, Seconds(type, 30f));
        }
        return true;
    }

    private static bool StealMana(World world, Unit unit, Unit target, float amount)
    {
        if (target == null || !target.Alive || target.Mana <= 0f) return false;
        if (world.AreAllied(unit.Player, target.Player)) return false;
        if (target.IsBuilding) return false;

        float range = AbilityCatalog.Get("Aste")?.RangeAt(1) ?? 650f;
        if (unit.DistanceTo(target) > range + target.Radius) return false;

        float stolen = Mathf.Min(target.Mana, amount > 0f ? amount : target.Mana);
        target.Mana -= stolen;
        unit.Mana = Mathf.Min(unit.MaxMana, unit.Mana + stolen);
        return true;
    }

    /// <summary>保存权杖 / 避难权杖：把目标单位挪回自己最近的主基地（避难附带眩晕与持续回血）。</summary>
    private static bool RescueToHall(World world, Unit caster, Unit subject, float stunSeconds, float healPerSecond)
    {
        if (subject == null || !subject.Alive || subject.IsBuilding) return false;
        if (!world.AreAllied(caster.Player, subject.Player)) return false;

        Unit hall = NearestOwnBuilding(world, caster);
        if (hall == null) return false;

        var spot = new Vector3(hall.Position.X + 96f, hall.Position.Y, hall.Position.Z + 96f);
        subject.CancelAttack();
        subject.Target = null;
        subject.MoveTarget = null;
        subject.Path = null;
        world.MoveUnitTo(subject, spot);

        if (stunSeconds > 0f) subject.State.Add(StateMod.Stun, stunSeconds);
        if (healPerSecond > 0f)
        {
            world.ApplyBuff(subject, new HealOverTimeBuff { AmountPerTick = healPerSecond, Interval = 1f }, 30f);
        }
        return true;
    }

    private static bool SummonMany(World world, Unit unit, string unitId, int count, float seconds)
    {
        bool any = false;
        for (int index = 0; index < count; index++)
        {
            float angle = Mathf.Tau * index / Mathf.Max(1, count);
            var spot = new Vector3(
                unit.Position.X + Mathf.Cos(angle) * 80f,
                unit.Position.Y,
                unit.Position.Z + Mathf.Sin(angle) * 80f);
            if (world.Summon(unit, unitId, spot, seconds) != null) any = true;
        }
        return any;
    }

    private static bool SpawnIllusions(World world, Unit unit, AbilityType type)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(type.DataAt(1, 'B')));
        float seconds = Seconds(type, 30f);
        bool any = false;
        for (int index = 0; index < count; index++)
        {
            float angle = Mathf.Tau * index / Mathf.Max(1, count);
            var spot = new Vector3(
                unit.Position.X + Mathf.Cos(angle) * 96f,
                unit.Position.Y,
                unit.Position.Z + Mathf.Sin(angle) * 96f);
            if (world.CreateIllusion(unit, spot, seconds, 1f, 2f) != null) any = true;
        }
        return any;
    }

    private static bool RestoreManaArea(World world, Unit unit, float radius, float amount)
    {
        if (amount <= 0f) return false;
        bool any = false;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, radius))
        {
            if (!ally.Alive || ally.IsBuilding || ally.MaxMana <= 0f) continue;
            ally.Mana = Mathf.Min(ally.MaxMana, ally.Mana + amount);
            any = true;
        }
        return any;
    }

    private static bool LinkAllies(World world, Unit unit, AbilityType type)
    {
        var members = new List<Unit> { unit };
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, RadiusOf(type)))
        {
            if (members.Count >= 4) break;
            if (!ally.Alive || ally.IsBuilding || ally == unit) continue;
            members.Add(ally);
        }
        if (members.Count < 2) return false;

        var group = new SpiritLinkGroup { Members = members, Ratio = Mathf.Clamp(type.DataAt(1, 'A'), 0f, 0.9f) };
        float seconds = Seconds(type, 75f);
        foreach (Unit member in members)
        {
            world.ApplyBuff(member, new SpiritLinkBuff { Group = group }, seconds);
        }
        return true;
    }

    private static bool HealArea(World world, Unit unit, float radius, float amount)
    {
        if (amount <= 0f) return false;
        bool any = false;
        foreach (Unit ally in world.AlliesInRange(unit, unit.Position, radius))
        {
            if (!ally.Alive || ally.IsBuilding) continue;
            ally.Heal(amount);
            any = true;
        }
        return any;
    }

    private static bool TownPortal(World world, Unit unit, AbilityType type)
    {
        Unit hall = NearestOwnBuilding(world, unit);
        if (hall == null) return false;

        float radius = RadiusOf(type);
        int max = type.DataAt(1, 'A') > 0f ? Mathf.RoundToInt(type.DataAt(1, 'A')) : 12;
        var group = new List<Unit> { unit };
        foreach (Unit other in world.Units)
        {
            if (group.Count >= max) break;
            if (other == null || !other.Alive || ReferenceEquals(other, unit)) continue;
            if (other.IsBuilding || !world.AreAllied(unit.Player, other.Player)) continue;
            if (unit.DistanceTo(other) > radius) continue;
            group.Add(other);
        }

        for (int index = 0; index < group.Count; index++)
        {
            float ring = index == 0 ? 0f : 96f + (index - 1) / 6 * 64f;
            float angle = index == 0 ? 0f : Mathf.Tau * (index - 1) / 6f;
            var spot = new Vector3(
                hall.Position.X + Mathf.Cos(angle) * ring,
                hall.Position.Y,
                hall.Position.Z + Mathf.Sin(angle) * ring);

            Unit member = group[index];
            member.CancelAttack();
            member.Target = null;
            member.MoveTarget = null;
            member.Path = null;
            world.MoveUnitTo(member, spot);
        }
        return true;
    }

    private static Unit NearestOwnBuilding(World world, Unit unit)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        foreach (Unit other in world.Units)
        {
            if (other == null || !other.Alive || !other.IsBuilding) continue;
            if (other.Player != unit.Player) continue;

            float distance = unit.DistanceTo(other);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = other;
        }
        return best;
    }

    private static float RadiusOf(AbilityType type)
    {
        float radius = type.AreaAt(1);
        if (radius > 0f) return radius;
        float range = type.RangeAt(1);
        return range > 0f && range < 2000f ? range : 600f;
    }

    private static float Seconds(AbilityType type, float fallback)
    {
        float duration = type.DurationAt(1);
        return duration > 0f ? duration : fallback;
    }
}