using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    internal sealed class War3WorldInteractionComponent
    {
        private readonly War3GameScene _scene;
        private bool _leftDown;
        private Vector2 _dragStart;
        private float _clickTime = -1f;
        private Unit _clickUnit;
        private float _groupPressedAt = -1f;
        private int _groupPressed = -1;

        internal War3WorldInteractionComponent(War3GameScene scene) => _scene = scene;

        internal void PointerDown(Vector2 position, MouseButton button)
        {
            if (button == MouseButton.Left)
            {
                if (Input.IsKeyPressed(Key.Alt) && _scene._placingId == null)
                {
                    if (_scene.TryGroundPoint(position, out Vector3 pingPoint))
                    {
                        _scene._hud?.MinimapFrame.Ping(pingPoint);
                        _scene.PingMinimap(new System.Numerics.Vector2(-pingPoint.X, pingPoint.Z));
                    }
                    return;
                }

                _leftDown = true;
                _dragStart = position;
                _scene._hud?.SetSelectionBox(new Rect2(), false);
                return;
            }

            if (button == MouseButton.Right)
            {
                if (_scene._placingId != null) _scene.CancelPlacement();
                else if (_scene.CastPending) _scene.CancelCast();
                else OrderAt(position, Input.IsKeyPressed(Key.Shift));
            }
        }

        internal void PointerUp(Vector2 position, MouseButton button)
        {
            if (button != MouseButton.Left || !_leftDown) return;
            _leftDown = false;

            if (_scene._placingId != null)
            {
                if (position.DistanceTo(_dragStart) < 8f) _scene.TryPlace(position);
                return;
            }

            if (position.DistanceTo(_dragStart) < 8f)
            {
                _scene._hud?.SetSelectionBox(new Rect2(), false);
                HandleClick(position, Input.IsKeyPressed(Key.Shift));
                return;
            }

            _scene._hud?.SetSelectionBox(new Rect2(), false);
            BoxSelect(_dragStart, position, Input.IsKeyPressed(Key.Shift));
        }

        internal void PointerMove(Vector2 position)
        {
            if (_leftDown && _scene._placingId == null
                && position.DistanceTo(_dragStart) >= 8f)
                _scene._hud?.SetSelectionBox(BoxRect(_dragStart, position), true);
        }

        private void HandleClick(Vector2 position, bool additive)
        {
            if (_scene.TryCommandAt(position)) return;
            if (_scene.TryCastAt(position)) return;

            float now = Time.GetTicksMsec() / 1000f;
            Unit picked = _scene.PickUnit(position, _scene.ViewPlayer);

            if (picked != null && _clickUnit == picked && now - _clickTime < 0.4f)
            {
                SelectSameType(picked);
                _clickUnit = null;
                _clickTime = -1f;
                return;
            }

            SelectAt(position, additive);
            _clickUnit = picked;
            _clickTime = now;
        }

        internal void HandleGroupKey(int group, bool ctrl, bool shift)
        {
            if (_scene._selection == null) return;
            if (ctrl)
            {
                _scene._selection.SetControlGroup(group);
                _scene._hud?.SetHint($"编队 {group} 已保存（{_scene._selected.Count} 个单位）");
                return;
            }
            if (shift)
            {
                _scene._selection.AddToControlGroup(group);
                _scene._hud?.SetHint($"已并入编队 {group}");
                return;
            }

            _scene._selection.RecallControlGroup(group);
            if (_scene._selected.Count == 0) return;

            Vector3 sum = Vector3.Zero;
            foreach (Unit unit in _scene._selected) sum += unit.Position;

            float now = Time.GetTicksMsec() / 1000f;
            bool center = _groupPressed == group && now - _groupPressedAt < 0.5f;
            _groupPressed = group;
            _groupPressedAt = now;
            if (center) _scene._camera.Focus(sum / _scene._selected.Count);
        }

        private void SelectSameType(Unit reference)
        {
            Vector2 size = _scene.GetViewport().GetVisibleRect().Size;
            List<Unit> units = _scene._picking?.UnitsInScreenRect(Vector2.Zero, size,
                unit => unit.Player == _scene.ViewPlayer
                    && string.Equals(unit.Type.Id, reference.Type.Id, StringComparison.OrdinalIgnoreCase))
                ?? new List<Unit>();
            _scene._selection?.SelectRect(units, false);
            if (units.Count > 0)
                _scene._hud?.SetHint($"已选中 {_scene._selected.Count} 个 {reference.Type.Name}");
        }

        private void BoxSelect(Vector2 from, Vector2 to, bool additive)
        {
            Rect2 rect = BoxRect(from, to);
            if (rect.Size.X < 4f || rect.Size.Y < 4f) return;

            List<Unit> units = _scene._picking?.UnitsInScreenRect(rect.Position, rect.End,
                unit => unit.Player == _scene.ViewPlayer) ?? new List<Unit>();
            _scene._selection?.SelectRect(units, additive);
            if (units.Count > 0) _scene._hud?.SetHint($"框选 {_scene._selected.Count} 个单位");
        }

        private void SelectAt(Vector2 screenPosition, bool additive)
        {
            Unit picked = _scene.PickUnit(screenPosition, _scene.ViewPlayer);
            if (picked == null)
            {
                if (!additive) _scene._selection?.Clear();
                return;
            }
            _scene._selection?.SelectClicked(picked, additive, mouseChannel: true);
        }

        internal void MinimapOrderAt(Vector3 worldPoint, bool queue)
        {
            if (_scene._selected.Count == 0) return;

            Unit enemy = null;
            float bestDistance = 90f;
            foreach (Unit unit in _scene._allUnits)
            {
                if (!unit.Alive || _scene.AreAllied(unit.Player, _scene.ViewPlayer)) continue;
                if (_scene._fog != null && _scene._fog.Enabled
                    && !_scene._fog.IsVisible(unit.Position)) continue;
                float distance = new Vector2(unit.Position.X - worldPoint.X,
                    unit.Position.Z - worldPoint.Z).Length();
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                enemy = unit;
            }

            if (enemy != null) _scene.IssueAttackOrders(enemy, queue);
            else _scene.IssueMoveOrders(worldPoint, queue);
        }

        internal void OrderAt(Vector2 screenPosition, bool queue)
        {
            if (_scene._selected.Count == 0) return;

            bool allBuildings = true;
            foreach (Unit unit in _scene._selected)
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

            foreach (Unit unit in _scene._selected)
            {
                if (!unit.Alive || unit.IsBuilding) continue;
                _scene.PlayVoice(unit, "Yes");
                break;
            }

            Unit damagedAlly = _scene.PickUnit(screenPosition, _scene.ViewPlayer, anyPlayer: true);
            if (damagedAlly != null && damagedAlly.Alive && damagedAlly.IsBuilding
                && _scene.AreAllied(damagedAlly.Player, _scene.ViewPlayer)
                && damagedAlly.Hp < damagedAlly.MaxHp)
            {
                int repairers = 0;
                foreach (Unit unit in _scene._selected)
                {
                    if (!unit.IsWorker || unit.IsBuilding) continue;
                    if (unit.FindAbility("Ahrp") == null && unit.FindAbility("Arep") == null
                        && unit.FindAbility("Aren") == null && unit.FindAbility("Arst") == null) continue;

                    _scene._world.ClearOrders(unit);
                    unit.RepairTarget = damagedAlly;
                    unit.Repairing = false;
                    unit.RepairGoldDebt = 0f;
                    unit.BuildSite = null;
                    unit.Tree = null;
                    unit.GoldMine = null;
                    repairers++;
                }
                _scene._hud?.SetHint(repairers > 0
                    ? $"修理：{repairers} 个工人 → {damagedAlly.Type.Name}（{damagedAlly.Hp:0}/{damagedAlly.MaxHp:0}）"
                    : "只有农民 / 苦工能修理建筑");
                return;
            }

            Unit enemy = null;
            Camera3D camera = _scene._camera.Camera;
            float bestDistance = 55f;
            foreach (Unit unit in _scene._allUnits)
            {
                if (!unit.Alive || _scene.AreAllied(unit.Player, _scene.ViewPlayer)) continue;
                if (IsNeutralShop(unit)) continue;
                if (_scene._fog != null && _scene._fog.Enabled
                    && !_scene._fog.IsVisible(unit.Position)) continue;

                Vector3 point = unit.Position + new Vector3(0f,
                    unit.View != null ? unit.View.Height * 0.5f : 60f, 0f);
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
                    foreach (Unit unit in _scene._selected)
                    {
                        if (!unit.IsWorker) continue;
                        _scene._world.SetGather(unit, enemy);
                        gatherers++;
                    }
                    _scene._hud?.SetHint(gatherers > 0
                        ? $"采金：{gatherers} 个农民 → {enemy.Type.Name}（剩余 {enemy.Gold}）"
                        : "金矿只能由农民采集");
                    return;
                }

                foreach (Unit unit in _scene._selected)
                {
                    if (!unit.Type.CanAttack) continue;
                    _scene._world.ClearOrders(unit);
                    _scene._world.IssueOrder(unit, UnitOrder.Attack(enemy), queue);
                }
                _scene._hud?.SetHint(queue ? $"追加攻击 {enemy.Type.Name}" : $"攻击 {enemy.Type.Name}");
                return;
            }

            TreeState tree = PickTree(screenPosition);
            if (tree != null)
            {
                int choppers = 0;
                foreach (Unit unit in _scene._selected)
                {
                    if (!unit.IsWorker) continue;
                    _scene._world.SetChop(unit, tree);
                    choppers++;
                }
                if (choppers > 0)
                {
                    _scene._hud?.SetHint($"伐木：{choppers} 个农民 → 树（{tree.Hp:0} HP）");
                    return;
                }
            }

            if (!_scene.TryGroundPoint(screenPosition, out Vector3 ground)) return;
            _scene.IssueMoveOrders(ground, queue);
        }

        private void OrderRallyAt(Vector2 screenPosition)
        {
            Camera3D camera = _scene._camera.Camera;
            Unit picked = null;
            float bestDistance = 55f;
            foreach (Unit unit in _scene._allUnits)
            {
                if (!unit.Alive || unit.View == null) continue;
                if (unit.IsBuilding && _scene._selected.Contains(unit)) continue;
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
                foreach (Unit building in _scene._selected) _scene._world.SetRally(building, picked);
                string label = picked.Gold > 0
                    ? $"集结点：采金 {picked.Type.Name}"
                    : _scene.AreAllied(picked.Player, _scene.ViewPlayer)
                        ? $"集结点：跟随 {picked.Type.Name}"
                        : $"集结点：攻击 {picked.Type.Name}";
                _scene._hud?.SetHint(label);
                return;
            }

            TreeState tree = PickTree(screenPosition);
            if (tree != null)
            {
                foreach (Unit building in _scene._selected) _scene._world.SetRally(building, tree);
                _scene._hud?.SetHint($"集结点：伐木（{tree.Hp:0} HP）");
                return;
            }

            if (!_scene.TryGroundPoint(screenPosition, out Vector3 ground)) return;
            foreach (Unit building in _scene._selected) _scene._world.SetRally(building, ground);
            _scene._hud?.SetHint($"集结点：({ground.X:0}, {ground.Z:0})");
        }

        private TreeState PickTree(Vector2 screenPosition)
        {
            Camera3D camera = _scene._camera.Camera;
            TreeState best = null;
            float bestDistance = 45f;
            foreach (TreeState tree in _scene._trees)
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

        private static Rect2 BoxRect(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.X, b.X);
            float y = Mathf.Min(a.Y, b.Y);
            return new Rect2(x, y, Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));
        }

        private static bool IsNeutralShop(Unit unit) => War3GameScene.IsNeutralShop(unit);
    }
}