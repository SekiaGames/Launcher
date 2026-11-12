using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class World
{
    public const float Tick = 0.05f;

    /// <summary>场景层每帧写入：影遁这类技能按昼夜决定是否生效。</summary>
    public bool IsNight;
    /// <summary>普攻飘字开关（客户端默认关，只有暴击常驻）。</summary>
    public bool ShowDamageNumbers;
    /// <summary>ApplyAttackModifiers 刚刚判定出致命一击，FireWeapon 据此打标。</summary>
    public bool LastAttackWasCritical;
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
    /// <summary>单位被从世界上直接拿掉（骑乘合成 / 分离），场景层据此释放模型。</summary>
    public event Action<Unit> UnitRemoved;
    /// <summary>结算前钩子：技能/物品可在此改 Amount、AttackType 或置 Cancelled。</summary>
    public event Action<DamageData> DamagePreview;
    /// <summary>结算后钩子：吸血、荆棘、伤害数字都挂这里。</summary>
    public event Action<DamageData> DamageApplied;
    /// <summary>法力燃烧抽掉的量（客户端专门的紫色数字）。</summary>
    public event Action<Unit, float> ManaBurned;

    public void ReportManaBurned(Unit target, float amount) => ManaBurned?.Invoke(target, amount);
    /// <summary>工人砍下一下木材（循环伐木音的触发点）。</summary>
    public event Action<Unit> TreeChopped;

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
        TickCreepSleep();

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
        TickGroundSpells();
        TickRepairs();
        TickAbilities();
        TickFogReveals();
        TickMorph();
        TickRegeneration();
        TickItemPickup();
        TickItemCooldowns();
        TickShops();
        TickSeparation();
        ResolveRepulse();
        TickHeroRevive(Tick);
        TickReincarnation();
        TickPhoenixEgg();
    }

    private void Step(Unit unit)
    {
        ApplyFacing(unit);

        if (unit.State.Stunned)
        {
            unit.View?.Play(unit.State.Has(StateMod.Sleep) ? "Sleep" : "Stand");
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

        if (unit.RepairTarget != null)
        {
            StepRepair(unit);
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

        if (unit.HoldingPosition)
        {
            StepHoldPosition(unit);
            return;
        }

        if (unit.AutoAcquire)
        {
            StepCreep(unit);
            return;
        }

        if (unit.Target != null && !unit.Target.Alive)
        {
            unit.Target = null;
            AdvanceOrder(unit);
        }
        if (unit.Target != null && !CanStrike(unit, unit.Target))
        {
            unit.Target = null;
            AdvanceOrder(unit);
        }

        if (unit.Target != null && unit.Type.CanAttack && unit.State.CanAttack)
        {
            float distance = unit.DistanceTo(unit.Target);
            if (distance <= unit.AttackReach + unit.Target.Radius)
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
            Advance(unit, unit.Target.Position, unit.AttackReach + unit.Target.Radius);
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
                if (unit.Patrolling)
                {
                    TickPatrolEndpoint(unit);
                    return;
                }
                unit.MoveTarget = null;
                unit.GroupSpeedCap = 0f;
                unit.GroupMoveId = 0;
                unit.View?.Play("Stand");
                AdvanceOrder(unit);
            }
            return;
        }

        if (unit.Patrolling)
        {
            TickPatrolEndpoint(unit);
            return;
        }

        unit.View?.Play("Stand");
    }
}
