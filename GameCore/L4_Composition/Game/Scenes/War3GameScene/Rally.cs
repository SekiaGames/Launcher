using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    /// <summary>客户端 RallyPointDisplay.CanSet：建筑、未在建、不是载具，且物编里有产出/科技字段。</summary>
    internal bool CanSetRally(Unit unit)
    {
        if (unit == null || !unit.Alive || !unit.IsBuilding) return false;
        if (unit.IsUnderConstruction || unit.CargoCapacity > 0) return false;
        UnitType type = unit.Type;
        if (type == null) return false;
        return type.Trains.Length > 0 || type.Builds.Length > 0
            || type.Researches.Length > 0 || type.UpgradeTo.Length > 0;
    }

    /// <summary>
    /// 集结点：逐单位按 <see cref="CanSetRally"/> 过滤，返回真正吃到这条命令的单位（客户端 TryIssueRallyAt）。
    /// 混选里的其余单位由调用方继续按右键本义执行。
    /// </summary>
    internal bool TryIssueRallyAt(Vector2 screenPosition, out List<Unit> producers)
    {
        producers = new List<Unit>();
        foreach (Unit unit in _selection.LiveControlled())
        {
            if (CanSetRally(unit)) producers.Add(unit);
        }
        if (producers.Count == 0) return false;

        Unit target = PickUnit(screenPosition, unit => unit.Alive);
        if (target != null)
        {
            foreach (Unit producer in producers) _world.SetRally(producer, target);
            _commandProcessor?.NotifyExternalOrder(new Vec2(target.Position.X, target.Position.Z), false);
            _hud?.SetHint($"集结点 {producers.Count} 座：{RallyTargetLabel(target)}");
            return true;
        }

        TreeState tree = _picking.PickTree(screenPosition);
        if (tree != null)
        {
            foreach (Unit producer in producers) _world.SetRally(producer, tree);
            _commandProcessor?.NotifyExternalOrder(new Vec2(tree.Position.X, tree.Position.Z), false);
            _hud?.SetHint($"集结点 {producers.Count} 座：伐木");
            return true;
        }

        if (!TryGroundPoint(screenPosition, out Vector3 ground)) return true;
        foreach (Unit producer in producers) _world.SetRally(producer, ground);
        _commandProcessor?.NotifyExternalOrder(new Vec2(ground.X, ground.Z), false);
        _hud?.SetHint($"集结点 {producers.Count} 座：({ground.X:0}, {ground.Z:0})");
        return true;
    }

    private string RallyTargetLabel(Unit target)
    {
        if (target.Gold > 0) return $"采金 {target.Type.Name}";
        if (AreAllied(target.Player, ViewPlayer)) return $"跟随 {target.Type.Name}";
        return $"攻击 {target.Type.Name}";
    }

    private bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.Zero;
        Camera3D camera = _camera.Camera;
        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = camera.ProjectRayNormal(screenPosition);

        Vector3 previous = origin;
        for (float travel = 16f; travel < 20000f; travel += 16f)
        {
            Vector3 sample = origin + direction * travel;
            float height = MapWorldBuilder.HeightAt(_map.Terrain, sample.X, sample.Z);
            if (sample.Y <= height)
            {
                Vector3 low = previous;
                Vector3 high = sample;
                for (int i = 0; i < 14; i++)
                {
                    Vector3 mid = (low + high) * 0.5f;
                    if (mid.Y <= MapWorldBuilder.HeightAt(_map.Terrain, mid.X, mid.Z)) high = mid;
                    else low = mid;
                }
                point = high;
                return true;
            }
            previous = sample;
        }
        return false;
    }
}
