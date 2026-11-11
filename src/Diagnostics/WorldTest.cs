using Godot;
using System;

namespace Launcher;

public static class WorldTest
{
    public static int Run(string gmapPath)
    {
        MapSource map = MapSource.Open(gmapPath);
        if (map == null)
        {
            Console.Error.WriteLine("[SIM] 打不开地图");
            return 1;
        }

        const float x = -8192f;
        const float z = 8192f;
        Unit attacker = Make("hfoo", 0, x, z, map.Terrain);
        Unit defender = Make("hfoo", 12, x + 240f, z, map.Terrain);
        if (attacker == null || defender == null)
        {
            Console.Error.WriteLine("[SIM] 造单位失败");
            return 1;
        }

        var sim = new World(map.Terrain, PathGrid.Build(map.Pathing, map.Terrain));
        sim.Units.Add(attacker);
        sim.Units.Add(defender);
        attacker.Target = defender;

        int tick = 0;
        for (; tick < 4000; tick++)
        {
            sim.Advance();
            if (!attacker.Alive || !defender.Alive) break;
            if (tick % 40 == 0)
            {
                Console.WriteLine($"[SIM] tick={tick} 距离={attacker.DistanceTo(defender):0} 攻击方HP={attacker.Hp:0} 目标HP={defender.Hp:0}");
            }
        }

        Console.WriteLine($"[SIM] 结束 tick={tick} 攻击方存活={attacker.Alive} 目标存活={defender.Alive} 目标HP={defender.Hp:0}");
        return defender.Alive ? 1 : 0;
    }

    private static Unit Make(string id, int player, float x, float z, WorldEdit.Core.Formats.W3x.W3eTerrain terrain)
    {
        UnitType type = UnitCatalog.Get(id);
        if (type == null) return null;

        return new Unit
        {
            Type = type,
            Player = player,
            Position = new Vector3(x, MapWorldBuilder.HeightAt(terrain, x, z), z),
            Hp = type.Hp,
            MaxHp = type.Hp,
            IsBuilding = type.IsBuilding,
        };
    }
}