using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
    private void OrderAt(Vector2 screenPosition)
    {
        if (_selected.Count == 0) return;

        bool allBuildings = true;
        foreach (Unit unit in _selected)
        {
            if (!unit.IsBuilding)
            {
                allBuildings = false;
                break;
            }
        }
        if (allBuildings)
        {
            OrderRallyAt(screenPosition);
            return;
        }

        foreach (Unit unit in _selected)
        {
            if (!unit.Alive || unit.IsBuilding) continue;
            PlayVoice(unit, "Yes");
            break;
        }

        Unit enemy = null;
        Camera3D camera = _camera.Camera;
        float bestDistance = 55f;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || AreAllied(unit.Player, ViewPlayer)) continue;
            if (_fog != null && _fog.Enabled && !_fog.IsVisible(unit.Position)) continue;

            Vector3 point = unit.Position + new Vector3(0f, unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
            if (camera.IsPositionBehind(point)) continue;
            float distance = camera.UnprojectPosition(point).DistanceTo(screenPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                enemy = unit;
            }
        }

        if (enemy != null)
        {
            if (enemy.Gold > 0)
            {
                int gatherers = 0;
                foreach (Unit unit in _selected)
                {
                    if (unit.IsWorker)
                    {
                        _world.SetGather(unit, enemy);
                        gatherers++;
                    }
                }
                _hud?.SetHint(gatherers > 0
                    ? $"采金：{gatherers} 个农民 → {enemy.Type.Name}（剩余 {enemy.Gold}）"
                    : "金矿只能由农民采集");
                return;
            }

            foreach (Unit unit in _selected)
            {
                if (!unit.Type.CanAttack) continue;
                unit.GoldMine = null;
                unit.Tree = null;
                unit.Target = enemy;
                unit.MoveTarget = null;
                unit.Path = null;
            }
            _hud?.SetHint($"攻击 {enemy.Type.Name}");
            return;
        }

        TreeState tree = PickTree(screenPosition);
        if (tree != null)
        {
            int choppers = 0;
            foreach (Unit unit in _selected)
            {
                if (!unit.IsWorker) continue;
                _world.SetChop(unit, tree);
                choppers++;
            }
            if (choppers > 0)
            {
                _hud?.SetHint($"伐木：{choppers} 个农民 → 树（{tree.Hp:0} HP）");
                return;
            }
        }

        if (!TryGroundPoint(screenPosition, out Vector3 ground)) return;

        var movers = new List<Unit>();
        Vector3 centroid = Vector3.Zero;
        foreach (Unit unit in _selected)
        {
            if (unit.IsBuilding || !unit.Alive) continue;
            movers.Add(unit);
            centroid += unit.Position;
        }
        if (movers.Count == 0) return;
        centroid /= movers.Count;

        // 按物编 formation 分层排队：近战在前、远程/法师在后（对标客户端 MovementFormationOffsets）
        Dictionary<Unit, Vector3> slots = GroupFormation.Layout(movers, ground, ground - centroid);
        foreach (Unit unit in movers)
        {
            unit.Target = null;
            unit.GoldMine = null;
            unit.Tree = null;
            unit.MoveTarget = slots.TryGetValue(unit, out Vector3 spot) ? spot : ground;
            unit.Path = null;
        }
        _hud?.SetHint($"移动到 ({ground.X:0}, {ground.Z:0})，{movers.Count} 个单位按阵型展开");
    }

    /// <summary>选中建筑时右键 = 设集结点：指针下是单位/树就绑目标，否则设地面点。</summary>
    private void OrderRallyAt(Vector2 screenPosition)
    {
        Camera3D camera = _camera.Camera;

        Unit picked = null;
        float bestDistance = 55f;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.View == null) continue;
            if (unit.IsBuilding && _selected.Contains(unit)) continue;
            Vector3 point = unit.Position + new Vector3(0f, unit.View.Height * 0.5f, 0f);
            if (camera.IsPositionBehind(point)) continue;
            float distance = camera.UnprojectPosition(point).DistanceTo(screenPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                picked = unit;
            }
        }

        if (picked != null)
        {
            foreach (Unit building in _selected) _world.SetRally(building, picked);
            string label = picked.Gold > 0
                ? $"集结点：采金 {picked.Type.Name}"
                : AreAllied(picked.Player, ViewPlayer)
                    ? $"集结点：跟随 {picked.Type.Name}"
                    : $"集结点：攻击 {picked.Type.Name}";
            _hud?.SetHint(label);
            return;
        }

        TreeState tree = PickTree(screenPosition);
        if (tree != null)
        {
            foreach (Unit building in _selected) _world.SetRally(building, tree);
            _hud?.SetHint($"集结点：伐木（{tree.Hp:0} HP）");
            return;
        }

        if (!TryGroundPoint(screenPosition, out Vector3 ground)) return;
        foreach (Unit building in _selected) _world.SetRally(building, ground);
        _hud?.SetHint($"集结点：({ground.X:0}, {ground.Z:0})");
    }

    private TreeState PickTree(Vector2 screenPosition)
    {
        Camera3D camera = _camera.Camera;
        TreeState best = null;
        float bestDistance = 45f;
        foreach (TreeState tree in _trees)
        {
            if (!tree.Alive) continue;
            Vector3 point = tree.Position + new Vector3(0f, 90f, 0f);
            if (camera.IsPositionBehind(point)) continue;
            float distance = camera.UnprojectPosition(point).DistanceTo(screenPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = tree;
            }
        }
        return best;
    }

    private bool TryGroundPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.Zero;
        Camera3D camera = _camera.Camera;
        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = camera.ProjectRayNormal(screenPosition);

        Vector3 previous = origin;
        for (float travel = 50f; travel < 20000f; travel += 50f)
        {
            Vector3 sample = origin + direction * travel;
            if (sample.Y <= MapWorldBuilder.HeightAt(_map.Terrain, sample.X, sample.Z))
            {
                Vector3 low = previous;
                Vector3 high = sample;
                for (int i = 0; i < 10; i++)
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
