using System;
using System.Collections.Generic;
using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 客户端对战开局模板，逐条对应 War3.mpq/Scripts/RuntimeBlizzardAbility.j 的 Melee* 函数：
/// MeleeStartingVisibility / MeleeStartingResources / MeleeStartingHeroLimit /
/// MeleeClearExcessUnits / MeleeStartingUnits{雷族} / MeleeStartingAI。
/// 所有坐标都是「地图坐标」（x 向东、y 向北），由调用方转成世界坐标。
/// </summary>
public static class MeleeRules
{
    // bj_MELEE_STARTING_TOD
    public const float StartingTod = 8f;

    // bj_MELEE_STARTING_GOLD_V0/V1、bj_MELEE_STARTING_LUMBER_V0/V1
    public const int RocStartingGold = 750;
    public const int RocStartingLumber = 200;
    public const int TftStartingGold = 500;
    public const int TftStartingLumber = 150;

    // bj_MELEE_STARTING_HERO_TOKENS / bj_MELEE_HERO_LIMIT / bj_MELEE_HERO_TYPE_LIMIT
    public const int StartingHeroTokens = 1;
    public const int HeroLimit = 3;
    public const int HeroTypeLimit = 1;

    // bj_MELEE_MINE_SEARCH_RADIUS / bj_MELEE_CLEAR_UNITS_RADIUS
    public const float MineSearchRadius = 2000f;
    public const float ClearUnitsRadius = 1500f;

    // bj_CELLWIDTH / bj_UNIT_FACING / MeleeStartingUnits* 里的 unitSpacing
    public const float CellWidth = 128f;
    public const float UnitFacing = 270f;
    public const float UnitSpacing = 64f;

    /// <summary>亡灵 SetBlightLoc(768)。</summary>
    public const float BlightRadius = 768f;

    public const int PlayerNeutralPassive = 15;
    public const int PlayerNeutralHostile = 12;

    public enum MineEffect
    {
        None,
        Haunted,
        Entangled,
    }

    public readonly struct StartUnit
    {
        public readonly string Id;
        public readonly Vector2 Map;

        public StartUnit(string id, Vector2 map)
        {
            Id = id;
            Map = map;
        }
    }

    public sealed class StartPlan
    {
        public readonly List<StartUnit> Units = new();
        public string HallId = "";
        public MineEffect Mine;
        public float BlightRadius;
        public Vector2 Camera;
    }

    /// <summary>w3i 的 controller：1 = 玩家、2 = 电脑；3/4 是中立与可营救。</summary>
    public static bool IsPlayingController(int controller)
    {
        return controller is 1 or 2;
    }

    public static (int Gold, int Lumber) StartingResources(bool reignOfChaos)
    {
        return reignOfChaos ? (RocStartingGold, RocStartingLumber) : (TftStartingGold, TftStartingLumber);
    }

    /// <summary>MeleeStartingUnitsForPlayer：按种族给出主基地、工人与金矿处理。</summary>
    public static StartPlan Plan(int race, Vector2 start, Vector2? mine)
    {
        var plan = new StartPlan { Camera = start };
        switch (race)
        {
            case 1:
                PlanHallRace(plan, start, mine, "htow", "hpea", WorkersAroundMine, out _);
                break;
            case 2:
                PlanHallRace(plan, start, mine, "ogre", "opeo", WorkersAroundMine, out _);
                break;
            case 3:
                PlanUndead(plan, start, mine);
                break;
            case 4:
                PlanNightElf(plan, start, mine);
                break;
            default:
                PlanUnknownRace(plan, start);
                break;
        }
        return plan;
    }

    private static readonly Vector2[] WorkersAroundMine =
    {
        new(0f, 1.00f), new(1.00f, 0.15f), new(-1.00f, 0.15f), new(0.60f, -1.00f), new(-0.60f, -1.00f),
    };

    private static readonly Vector2[] WispsAroundMine =
    {
        new(0f, 1.00f), new(1.00f, 0.15f), new(-1.00f, 0.15f), new(0.58f, -1.00f), new(-0.58f, -1.00f),
    };

    private static readonly Vector2[] WorkersSouth =
    {
        new(2f, 0f), new(1f, 0f), new(0f, 0f), new(-1f, 0f), new(-2f, 0f),
    };

    private static void PlanHallRace(StartPlan plan, Vector2 start, Vector2? mine, string hallId, string workerId,
        Vector2[] mineOffsets, out Vector2 workerAnchor)
    {
        plan.HallId = hallId;
        plan.Units.Add(new StartUnit(hallId, start));

        if (mine.HasValue)
        {
            workerAnchor = Project(mine.Value, start, 320f, 0f);
            AddCluster(plan, workerId, workerAnchor, mineOffsets);
            plan.Camera = workerAnchor;
            return;
        }

        workerAnchor = new Vector2(start.X, start.Y - 224f);
        AddCluster(plan, workerId, workerAnchor, WorkersSouth);
        plan.Camera = workerAnchor;
    }

    private static void PlanUndead(StartPlan plan, Vector2 start, Vector2? mine)
    {
        plan.HallId = "unpl";
        plan.Units.Add(new StartUnit("unpl", start));

        if (mine.HasValue)
        {
            plan.Mine = MineEffect.Haunted;
            plan.BlightRadius = BlightRadius;

            Vector2 ghoul = Project(start, mine.Value, 288f, 0f);
            plan.Units.Add(new StartUnit("ugho", ghoul));

            Vector2 acolytes = Project(mine.Value, start, 320f, 0f);
            plan.Units.Add(new StartUnit("uaco", acolytes + new Vector2(0f, 0.50f) * UnitSpacing));
            plan.Units.Add(new StartUnit("uaco", acolytes + new Vector2(0.65f, -0.50f) * UnitSpacing));
            plan.Units.Add(new StartUnit("uaco", acolytes + new Vector2(-0.65f, -0.50f) * UnitSpacing));
            plan.Camera = acolytes;
            return;
        }

        plan.BlightRadius = BlightRadius;
        Vector2 south = new(start.X, start.Y - 224f);
        plan.Units.Add(new StartUnit("uaco", south + new Vector2(-1.5f, 0f) * UnitSpacing));
        plan.Units.Add(new StartUnit("uaco", south + new Vector2(-0.5f, 0f) * UnitSpacing));
        plan.Units.Add(new StartUnit("uaco", south + new Vector2(0.5f, 0f) * UnitSpacing));
        plan.Units.Add(new StartUnit("ugho", south + new Vector2(1.5f, 0f) * UnitSpacing));
        plan.Camera = south;
    }

    private static void PlanNightElf(StartPlan plan, Vector2 start, Vector2? mine)
    {
        plan.HallId = "etol";

        if (mine.HasValue)
        {
            // 主基地贴着矿放（650 距离，再夹到 3.5 格以内），并立刻缠绕金矿
            Vector2 treeLoc = ClampWithin(Project(mine.Value, start, 650f, 0f), mine.Value, 3.5f * CellWidth);
            plan.Units.Add(new StartUnit("etol", treeLoc));
            plan.Mine = MineEffect.Entangled;

            Vector2 wisps = ClampWithin(Project(mine.Value, start, 320f, 0f), mine.Value, 1.75f * CellWidth);
            AddCluster(plan, "ewsp", wisps, WispsAroundMine);
            plan.Camera = wisps;
            return;
        }

        plan.Units.Add(new StartUnit("etol", start));
        Vector2 south = new(start.X, start.Y - 224f);
        AddCluster(plan, "ewsp", south, WorkersSouth);
        plan.Camera = south;
    }

    private static void PlanUnknownRace(StartPlan plan, Vector2 start)
    {
        // 客户端对未知种族给 12 只羊
        var random = new Random(1337);
        for (int i = 0; i < 12; i++)
        {
            float dx = (float)(random.NextDouble() * 512.0 - 256.0);
            float dy = (float)(random.NextDouble() * 512.0 - 256.0);
            plan.Units.Add(new StartUnit("nshe", start + new Vector2(dx, dy)));
        }
        plan.Camera = start;
    }

    private static void AddCluster(StartPlan plan, string id, Vector2 anchor, Vector2[] offsets)
    {
        foreach (Vector2 offset in offsets)
        {
            plan.Units.Add(new StartUnit(id, anchor + offset * UnitSpacing));
        }
    }

    /// <summary>MeleeGetProjectedLoc：从 src 朝 targ 走 distance，再转 deltaAngle。</summary>
    public static Vector2 Project(Vector2 src, Vector2 targ, float distance, float deltaAngle)
    {
        float direction = Mathf.Atan2(targ.Y - src.Y, targ.X - src.X) + deltaAngle;
        return new Vector2(src.X + distance * Mathf.Cos(direction), src.Y + distance * Mathf.Sin(direction));
    }

    /// <summary>MeleeGetLocWithinRect(GetRectFromCircleBJ(center, radius))：按轴夹到圆的外接方框内。</summary>
    public static Vector2 ClampWithin(Vector2 point, Vector2 center, float radius)
    {
        return new Vector2(
            Mathf.Clamp(point.X, center.X - radius, center.X + radius),
            Mathf.Clamp(point.Y, center.Y - radius, center.Y + radius));
    }

    /// <summary>MeleeClearExcessUnit 的判定：中立敌对一律清，中立被动只清非建筑。</summary>
    public static bool ShouldClearAtStart(int owner, bool isStructure)
    {
        if (owner == PlayerNeutralHostile) return true;
        if (owner == PlayerNeutralPassive) return !isStructure;
        return false;
    }

    public static void Log(bool reignOfChaos)
    {
        (int gold, int lumber) = StartingResources(reignOfChaos);
        GD.Print($"[MELEE] 开局模板 版本={(reignOfChaos ? "RoC" : "TFT")} 起始资源={gold}/{lumber}"
            + $" 清场半径={ClearUnitsRadius:0} 找矿半径={MineSearchRadius:0} 英雄上限={HeroLimit}(同类{HeroTypeLimit})"
            + $" 起始时刻={StartingTod:0.0}h");
    }
}
