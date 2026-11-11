using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 英雄复活与赏金。复活公式对齐客户端 GameplayConstants.HeroReviveGoldCost 与 NativeHeroBuildTime；
/// 赏金来自 UnitBalance 的 bountydice/bountysides/bountyplus。
/// </summary>
public sealed partial class World
{
    private static readonly HashSet<string> AltarTypes = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "halt", "oalt", "uaod", "eate",
    };

    /// <summary>击杀赏金：victim 是谁、给了多少钱。对局层据此加到玩家金库里。</summary>
    public event System.Action<int, int, Unit> BountyAwarded;
    public event System.Action<Unit> HeroRevived;

    /// <summary>扣钱回调由对局层注入（模拟层不持有玩家金库），返回 true 表示扣款成功。</summary>
    public System.Func<int, int, bool> TrySpendGold;

    /// <summary>
    /// 赏金资格：客户端 TryRollKillBounty 默认只有中立敌对（玩家 12）给钱，
    /// 击杀敌方玩家的单位不给赏金（钱只从矿里来）。
    /// </summary>
    public static bool GivesBounty(Unit victim)
    {
        return victim != null && victim.Player == 12;
    }

    public int BountyGoldOf(Unit victim)
    {
        UnitType type = victim?.Type;
        if (type == null) return 0;

        int gold = Mathf.Max(0, type.BountyPlus);
        if (type.BountySides > 0)
        {
            for (int dice = 0; dice < type.BountyDice; dice++) gold += _random.Next(1, type.BountySides + 1);
        }
        return gold;
    }

    public int HeroReviveCostOf(Unit hero)
    {
        return hero?.Type == null ? 0 : War3GameplayConfig.HeroReviveGoldCost(hero.Type.GoldCost, hero.Level);
    }

    public float HeroReviveSecondsOf(Unit hero)
    {
        return hero?.Type == null ? 0f : War3GameplayConfig.HeroReviveSeconds(hero.Type.TrainSeconds, hero.Level);
    }

    public Unit FindAltar(int player)
    {
        foreach (Unit unit in Units)
        {
            if (!unit.Alive || !unit.IsBuilding || unit.Player != player) continue;
            if (AltarTypes.Contains(unit.Type.Id)) return unit;
        }
        return null;
    }

    /// <summary>每 tick 推进：尸体衰减 → 复活倒计时 → 排队等钱/等祭坛。</summary>
    public void TickHeroRevive(float delta)
    {
        foreach (Unit hero in Units)
        {
            if (hero.Alive || !hero.IsHero) continue;

            if (hero.ReviveRemaining > 0f)
            {
                hero.ReviveRemaining -= delta;
                if (hero.ReviveRemaining <= 0f) FinishRevive(hero);
                continue;
            }

            if (!hero.AwaitingRevive) continue;

            hero.ReviveRetry -= delta;
            if (hero.ReviveRetry > 0f) continue;
            hero.ReviveRetry = 1f;

            Unit altar = FindAltar(hero.Player);
            if (altar == null) continue;

            int cost = HeroReviveCostOf(hero);
            if (cost > 0 && TrySpendGold != null && !TrySpendGold(hero.Player, cost)) continue;

            hero.ReviveGold = cost;
            hero.ReviveAltar = altar;
            hero.ReviveTotal = HeroReviveSecondsOf(hero);
            hero.ReviveRemaining = hero.ReviveTotal;
            hero.AwaitingRevive = false;
            GD.Print($"[REVIVE] P{hero.Player} {hero.Type.Name} Lv{hero.Level} 开始复活 {hero.ReviveTotal:0}s 花费 {cost}");
        }
    }

    private void FinishRevive(Unit hero)
    {
        Unit altar = hero.ReviveAltar;
        if (altar == null || !altar.Alive)
        {
            hero.ReviveAltar = null;
            hero.AwaitingRevive = true;
            return;
        }

        float angle = (float)(_random.NextDouble() * Mathf.Tau);
        float distance = altar.Radius + 48f;
        var position = new Vector3(
            altar.Position.X + Mathf.Cos(angle) * distance,
            altar.Position.Y,
            altar.Position.Z + Mathf.Sin(angle) * distance);

        hero.ReviveAt(position, -angle);
        GD.Print($"[REVIVE] P{hero.Player} {hero.Type.Name} Lv{hero.Level} 在 {altar.Type.Name} 复活，生命 {hero.Hp:0}/{hero.MaxHp:0}");
        HeroRevived?.Invoke(hero);
    }
}
