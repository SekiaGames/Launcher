using Godot;
using System.Collections.Generic;

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
    public float WorkTimer = 0f;
    public int Gold;

    public Vector3 Home;
    public bool AutoAcquire;
    public bool Returning;
    public float ReturnDelay;
    public float LeashRange = 1000f;

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

    public float DistanceTo(Unit other) =>
        new Vector2(Position.X - other.Position.X, Position.Z - other.Position.Z).Length();
}
