using Godot;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class Unit
{
    public UnitType Type;
    public int Player;
    public Vector3 Position;
    public float Facing;
    public float TargetFacing;
    public float Hp;
    public float MaxHp;
    public bool Alive = true;
    public bool IsBuilding;
    public Unit Target;
    public Vector3? MoveTarget;
    public float AttackCooldown;
    public UnitSceneComponent View;

    public Unit GoldMine;
    public Unit HomeHall;
    public TreeState Tree;
    /// <summary>建筑的集结点：三选一，点/单位/树。新训练出来的单位出生后往这里去。</summary>
    public Vector3? RallyPoint;
    public Unit RallyTarget;
    public TreeState RallyTree;
    public int Carry;
    public int CarryLumber;
    /// <summary>war3mapUnits.doo 里这个单位自带的掉落集（野怪死亡时掷骰）。</summary>
    public readonly List<UnitItemDropSet> DropSets = new();

    /// <summary>入矿倒计时：侍僧（Aaha）进矿时隐藏模型，出矿再显示。</summary>
    public float InMineRemaining;
    public bool InMine => InMineRemaining > 0f;
    public float WorkTimer = 0f;
    public int Gold;

    public Vector3 Home;
    public bool AutoAcquire;
    public bool Returning;
    public float ReturnDelay;
    public float LeashRange = 1000f;
    /// <summary>AI 已经下令撤退（避免每 0.3s 重复下同一个命令）。</summary>
    public bool RetreatOrdered;
    /// <summary>交战时被贴脸后的后撤（远程/法师）：后撤期间优先走位，不走目标。</summary>
    public bool Kiting;
    public float KiteCooldown;
    /// <summary>绕侧翼已持续的时间；超过 10 秒就直接压上去，避免绕路卡住。</summary>
    public float FlankTimer;

    // —— 英雄状态 ——
    public bool IsHero;
    public int Level = 1;
    public float Xp;
    public int SkillPoints;
    public float Mana;
    public float MaxMana;
    public int Strength;
    public int Agility;
    public int Intelligence;

    public readonly State State = new();

    public bool IsUnderConstruction;
    public float BuildRemaining;
    public float BuildTotal;
    public Unit BuildSite;
    public bool BuildingNow;
    /// <summary>正在修理的目标建筑（工人修理指令）。</summary>
    public Unit RepairTarget;
    public bool Repairing;
    public float RepairGoldDebt;
    public bool NoBuilderLogged;
    public float NoBuilderTimer;

    public List<Vector3> Path;
    public int PathIndex;
    public float RepathTimer;

    public float Radius => Type == null
        ? 28f
        : IsBuilding
            ? Mathf.Max(32f, Type.FootprintRadius)
            : Mathf.Max(8f, Type.CollisionRadius);
    public bool IsWorker => Type != null && Type.IsWorkerType;

    /// <summary>编队层号（物编 formation）：0 近战前排、2 远程后排，阵型按它分排。</summary>
    public int FormationRank => Type?.FormationRank ?? 0;

    public float DistanceTo(Unit other) =>
        new Vector2(Position.X - other.Position.X, Position.Z - other.Position.Z).Length();

    // —— 驻军 / 载具（兽族地洞 Abun） ——
    /// <summary>自己肚子里装着的单位（地洞里的苦工）。</summary>
    public readonly List<Unit> Cargo = new();
    /// <summary>装载容量：&gt; 0 表示这个单位能装人。</summary>
    public int CargoCapacity;
    /// <summary>被谁装进去了：非空表示自己在别人肚子里，已经不在世界单位表里。</summary>
    public Unit CargoHolder;
    public bool IsCargoed => CargoHolder != null;
    /// <summary>地洞类驻军建筑：只有装了人才开火。</summary>
    public bool GarrisonOnly;

    // —— 腐地（Abgl/Abgs 铺、Abdl/Abds 压） ——
    /// <summary>自己铺出去的腐地半径；0 表示按种族默认半径算。</summary>
    public float BlightRadius;
    /// <summary>自己压制腐地的半径（建筑周围这一圈不算腐地）。</summary>
    public float BlightSuppressRadius;
}
