using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    public const float Tick = 0.05f;
    private const float GatherRange = 190f;
    private const float WorkSeconds = 2.2f;

    private readonly W3eTerrain _terrain;
    private readonly PathGrid _pathGrid;
    private readonly List<TreeState> _trees;
    private readonly Random _random = new(20261111);

    public List<Unit> Units { get; } = new();
    public event Action<Unit> Died;
    public event Action<int, int> GoldDeposited;
    public event Action<int, int> LumberDeposited;
    public event Action<Unit, Unit> MissileFired;
    public event Action<Unit, Unit> AttackLanded;
    public event Action<Unit> ConstructionFinished;
    /// <summary>结算前钩子：技能/物品可在此改 Amount、AttackType 或置 Cancelled。</summary>
    public event Action<DamageInfo> DamagePreview;
    /// <summary>结算后钩子：吸血、荆棘、伤害数字都挂这里。</summary>
    public event Action<DamageInfo> DamageApplied;

    /// <summary>同盟判定由对局层注入（w3i 队伍），模拟层不假设玩家号顺序。</summary>
    public Func<int, int, bool> Allied;

    private readonly Dictionary<string, int> _research = new(StringComparer.OrdinalIgnoreCase);

    public int ResearchLevel(int player, string upgradeId)
    {
        return _research.TryGetValue(player + "|" + upgradeId, out int level) ? level : 0;
    }

    public void SetResearchLevel(int player, string upgradeId, int level)
    {
        _research[player + "|" + upgradeId] = Mathf.Max(0, level);
    }

    public World(W3eTerrain terrain, PathGrid pathGrid, List<TreeState> trees = null)
    {
        _terrain = terrain;
        _pathGrid = pathGrid;
        _trees = trees ?? new List<TreeState>();
    }

    public void Advance()
    {
        // 用下标遍历：技能/召唤会在推进过程中往 Units 里加新单位（狼、骷髅、黑暗转换），
        // 用 foreach 会直接撞 InvalidOperationException。
        for (int index = 0; index < Units.Count; index++)
        {
            Unit unit = Units[index];
            if (!unit.Alive)
            {
                unit.TickCorpse(Tick);
                continue;
            }
            unit.State.Tick(Tick);
            Step(unit);
        }

        TickWeapons();
        TickAbilityMissiles();
        TickChannels();
        TickAbilities();
        TickFogReveals();
        TickMorph();
        TickRegeneration();
        TickItemPickup();
        TickShops();
        ResolveRepulse();
        TickHeroRevive(Tick);
    }

    private void Step(Unit unit)
    {
        ApplyFacing(unit);

        if (unit.State.Stunned)
        {
            unit.View?.Play("Stand");
            return;
        }

        // 引导中站着不动，也不能顺势普攻（客户端 Unit.Step:70 同口径）
        if (unit.IsChanneling)
        {
            unit.View?.Play("Spell");
            return;
        }

        if (unit.AutoCast) TickAutoCast(unit);

        if (unit.IsUnderConstruction)
        {
            TickConstruction(unit);
            return;
        }

        if (unit.BuildSite != null)
        {
            StepBuild(unit);
            return;
        }

        if (unit.Tree != null)
        {
            StepChop(unit);
            return;
        }

        if (unit.GoldMine != null)
        {
            StepGather(unit);
            return;
        }

        if (unit.AutoAcquire)
        {
            StepCreep(unit);
            return;
        }

        if (unit.Target != null && !unit.Target.Alive) unit.Target = null;
        if (unit.Target != null && !CanStrike(unit, unit.Target)) unit.Target = null;

        if (unit.Target != null && unit.Type.CanAttack && unit.State.CanAttack)
        {
            float distance = unit.DistanceTo(unit.Target);
            if (distance <= unit.Type.AttackRange + unit.Target.Radius)
            {
                FaceTarget(unit, unit.Target);
                BeginAttack(unit, unit.Target);
                return;
            }
            if (unit.IsBuilding)
            {
                unit.Target = null;
                return;
            }
            Advance(unit, unit.Target.Position, unit.Type.AttackRange + unit.Target.Radius);
            return;
        }

        if (unit.MoveTarget.HasValue)
        {
            if (unit.IsBuilding)
            {
                unit.MoveTarget = null;
                return;
            }
            if (Advance(unit, unit.MoveTarget.Value, 12f))
            {
                unit.MoveTarget = null;
                unit.View?.Play("Stand");
            }
            return;
        }

        unit.View?.Play("Stand");
    }
}
