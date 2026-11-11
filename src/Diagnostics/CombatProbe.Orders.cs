using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

public static partial class CombatProbe
{
    private static void CheckOrders()
    {
        GD.Print("==== 单位订单 ====");

        CheckOrderQueue();
        CheckOrderPatrol();
        CheckOrderStop();
        CheckOrderHold();
        CheckOrderChainAfterKill();
        CheckOrderQueueLimit();
        CheckOrderAttackMoveCancelsHarvest();
    }

    private static void CheckOrderQueue()
    {
        var world = new World(null, null);
        Unit unit = ProbeUnit(world, "hfoo", 0, Vector3.Zero);
        if (unit == null) return;

        world.IssueOrder(unit, UnitOrder.Move(new Vector3(400f, 0f, 0f)), false);
        Verify("移动订单立刻执行", 400f, unit.MoveTarget.HasValue ? unit.MoveTarget.Value.X : 0f, 0.5f);

        world.IssueOrder(unit, UnitOrder.Move(new Vector3(800f, 0f, 0f)), true);
        Verify("Shift 右键追加进队列", 1f, unit.PendingOrders.Count, 0f);
        Verify("排队不改当前目标", 400f, unit.MoveTarget.HasValue ? unit.MoveTarget.Value.X : 0f, 0.5f);

        for (int tick = 0; tick < 400 && unit.PendingOrders.Count > 0; tick++) world.Advance();
        Verify("到达后自动接下一条", 800f, unit.MoveTarget.HasValue ? unit.MoveTarget.Value.X : -1f, 2f);

        for (int tick = 0; tick < 800 && unit.MoveTarget.HasValue; tick++) world.Advance();
        Verify("第二条走到位", 800f, unit.Position.X, 14f);
    }

    private static void CheckOrderPatrol()
    {
        var world = new World(null, null);
        Unit unit = ProbeUnit(world, "hfoo", 0, Vector3.Zero);
        if (unit == null) return;

        world.IssueOrder(unit, UnitOrder.Patrol(new Vector3(300f, 0f, 0f)), false);
        Verify("巡逻标记生效", 1f, unit.Patrolling ? 1f : 0f, 0f);
        Verify("巡逻先走落点", 300f, unit.MoveTarget.HasValue ? unit.MoveTarget.Value.X : -1f, 1f);

        for (int tick = 0; tick < 600 && unit.PatrolTo.X > 1f; tick++) world.Advance();
        Verify("到点后折返起点", 0f, unit.PatrolTo.X, 3f);
        Verify("折返后仍在巡逻", 1f, unit.Patrolling ? 1f : 0f, 0f);
    }

    private static void CheckOrderStop()
    {
        var world = new World(null, null);
        Unit unit = ProbeUnit(world, "hfoo", 0, Vector3.Zero);
        if (unit == null) return;

        world.IssueOrder(unit, UnitOrder.Patrol(new Vector3(600f, 0f, 0f)), false);
        world.IssueOrder(unit, UnitOrder.Move(new Vector3(900f, 0f, 0f)), true);
        for (int tick = 0; tick < 20; tick++) world.Advance();

        world.StopUnit(unit);
        Verify("停止清空队列", 0f, unit.PendingOrders.Count, 0f);
        Verify("停止清掉巡逻标记", 0f, unit.Patrolling ? 1f : 0f, 0f);
        Verify("停止清掉移动目标", 0f, unit.MoveTarget.HasValue ? 1f : 0f, 0f);

        Vector3 before = unit.Position;
        for (int tick = 0; tick < 60; tick++) world.Advance();
        float drift = new Vector2(unit.Position.X - before.X, unit.Position.Z - before.Z).Length();
        Verify("停止后不再移动", 0f, drift, 2f);
    }

    private static void CheckOrderHold()
    {
        var world = new World(null, null);
        Unit guard = ProbeUnit(world, "hfoo", 0, Vector3.Zero);
        Unit far = ProbeUnit(world, "hfoo", 1, new Vector3(700f, 0f, 0f));
        if (guard == null || far == null) return;

        world.IssueOrder(guard, UnitOrder.Attack(far), false);
        Verify("攻击订单锁定目标", 1f, ReferenceEquals(guard.Target, far) ? 1f : 0f, 0f);

        world.HoldPosition(guard);
        Verify("据守丢掉射程外目标", 0f, guard.Target != null ? 1f : 0f, 0f);

        Vector3 before = guard.Position;
        for (int tick = 0; tick < 100; tick++) world.Advance();
        float drift = new Vector2(guard.Position.X - before.X, guard.Position.Z - before.Z).Length();
        Verify("据守不追人", 0f, drift, 6f);

        Unit near = ProbeUnit(world, "hfoo", 1, new Vector3(70f, 0f, 0f));
        if (near == null) return;
        float hpBefore = near.Hp;
        for (int tick = 0; tick < 60; tick++) world.Advance();
        Verify("据守照打射程内敌人", 1f, near.Hp < hpBefore ? 1f : 0f, 0f);
    }

    private static void CheckOrderChainAfterKill()
    {
        var world = new World(null, null);
        Unit killer = ProbeUnit(world, "hfoo", 0, Vector3.Zero);
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(70f, 0f, 0f));
        if (killer == null || victim == null) return;
        victim.Hp = 1f;

        world.IssueOrder(killer, UnitOrder.Attack(victim), false);
        world.IssueOrder(killer, UnitOrder.Move(new Vector3(500f, 0f, 0f)), true);

        for (int tick = 0; tick < 200 && killer.MoveTarget == null; tick++) world.Advance();
        Verify("目标死亡", 0f, victim.Alive ? 1f : 0f, 0f);
        Verify("目标死后接队列移动", 500f, killer.MoveTarget.HasValue ? killer.MoveTarget.Value.X : -1f, 2f);
    }

    private static void CheckOrderQueueLimit()
    {
        var world = new World(null, null);
        Unit unit = ProbeUnit(world, "hfoo", 0, Vector3.Zero);
        if (unit == null) return;

        world.IssueOrder(unit, UnitOrder.Move(new Vector3(2000f, 0f, 0f)), false);
        for (int index = 0; index < 20; index++)
        {
            world.IssueOrder(unit, UnitOrder.Move(new Vector3(3000f, 0f, index * 20f)), true);
        }
        Verify("队列上限", World.MaxQueuedOrders, unit.PendingOrders.Count, 0f);
    }

    private static void CheckOrderAttackMoveCancelsHarvest()
    {
        var world = new World(null, null);
        Unit worker = ProbeUnit(world, "hpea", 0, Vector3.Zero);
        Unit mine = ProbeUnit(world, "ngol", 15, new Vector3(300f, 0f, 0f));
        if (worker == null || mine == null) return;

        world.SetGather(worker, mine);
        Verify("采集订单已接", 1f, ReferenceEquals(worker.GoldMine, mine) ? 1f : 0f, 0f);

        world.IssueOrder(worker, UnitOrder.Move(new Vector3(-300f, 0f, 0f)), false);
        Verify("移动打断采集", 0f, worker.GoldMine != null ? 1f : 0f, 0f);
    }
}