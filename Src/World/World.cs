using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3;

public sealed partial class World
{
    public const float FrameSeconds = 0.05f;

    /// <summary>客户端 World.Tick：单调递增的帧计数，配合 TickSeconds 换算经过时间。</summary>
    public long Tick { get; private set; }

    public long UnitCollectionVersion { get; private set; }

    internal void BumpUnitCollectionVersion() => UnitCollectionVersion++;

    public const float TickSeconds = FrameSeconds;

    public object Terrain => null;

    public System.Random ScriptRandom => _random;

    /// <summary>事件总线：客户端技能/触发器按 GameEvent 订阅，Launcher 侧在相应时机 Fire。</summary>
    public EventBus Events { get; } = new();

    public System.Random Rng => _random;

    public Func<int, bool> ComputerSpellAiQuery { get; set; } = _ => true;

    public bool LoggingEnabled { get; set; }

    public void Log(string message)
    {
        if (LoggingEnabled) GameLogger.Write(message);
    }

    readonly System.Random _hardenedSkinRng = new(20261113);

    /// <summary>硬化皮肤/减伤掷骰的独立随机流（与战斗随机分开，避免扰动既有序列）。</summary>
    public System.Random HardenedSkinRng => _hardenedSkinRng;

    /// <summary>客户端 World.SharedSpellsQuery：同队共享视野/共享单位控制下的友军判定。</summary>
    public Func<Unit, Unit, bool> SharedSpellsQuery { get; set; } = (a, b) => a != null && b != null && a.Team == b.Team;

    public MovementBatchStub MovementBatch => _movementBatch;

    readonly MovementBatchStub _movementBatch = new();

    public sealed class MovementBatchStub
    {
        public void Cancel(Unit unit, object request)
        {
            unit?.CancelOrders();
        }

        public void BeginMovementEvent() { }

        public void DriveRequestHost(Unit host, object tick) { }
    }

    /// <summary>场景层每帧写入：影遁这类技能按昼夜决定是否生效。</summary>
    public bool IsNight;

    /// <summary>场景层注入的迷雾可见查询 (x,z -> 可见)，供选择/命令层使用。</summary>
    public Func<float, float, bool> FogVisible { get; set; }

    public bool CanControl(Unit unit, int player, bool advanced = false)
    {
        if (unit == null || !unit.Alive || unit.Removed) return false;
        if (unit.Player == player) return true;
        return advanced && IsAllied(player, unit.Player);
    }
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

    public UnitList Units { get; }

    public War3GameplayConfig GameplayConfig => War3GameplayConfig.Default;

    private CargoTransport _cargo;
    /// <summary>驻军子系统（对标客户端 Src/Units/CargoTransport.cs）。</summary>
    public CargoTransport Cargo => _cargo ??= new CargoTransport(this);
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
        Units = new UnitList(this);
    }

    public void Advance()
    {
        Tick++;
        TickCreepSleep();

        // 用下标遍历：技能/召唤会在推进过程中往 Units 里加新单位（狼、骷髅、黑暗转换），
        // 用 foreach 会直接撞 InvalidOperationException。
        for (int index = 0; index < Units.Count; index++)
        {
            Unit unit = Units[index];
            if (!unit.Alive)
            {
                unit.TickCorpse(FrameSeconds);
                continue;
            }
            unit.State.FrameSeconds(FrameSeconds);
            Step(unit);
        }

        TickWeapons();
        TickEffects();
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
        TickHeroRevive(FrameSeconds);
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

        // 攻击移动：途中没有目标就按索敌范围找一下，找到先打；打完继续走向原目标点。
        if (unit.AttackMoving && unit.Target == null)
        {
            Unit encountered = FindAcquireTarget(unit);
            if (encountered != null) EngageTarget(unit, encountered);
            else if (!unit.MoveTarget.HasValue) unit.MoveTarget = unit.AttackMoveGoal;
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
                unit.AttackMoving = false;
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
